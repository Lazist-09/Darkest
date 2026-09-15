using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Darkest.UI;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// BattleRoot：主战斗场景组合根（blueprint §5d / T-M5-05）——装配 + 游玩状态机。
/// 游玩循环（与 headless 同源，blueprint §10 镜像）：StartTurn（回合钩子+行动序列重掷）
/// → NextActor 逐个出列：我方 → 等待玩家输入（技能/换位/撤退）；敌方 → 自动 EnemyAct；
/// 队列空 → 下回合。全部事件写内核 CombatLog；UI 只读投影 + 命令门面。
/// </summary>
public partial class BattleRoot : Node2D
{
    public BattleDirector Director { get; private set; } = null!;
    public BattleProjector Projector { get; private set; } = null!;

    private RngProvider _rng = null!;

    /// <summary>
    /// 🆕 **敌方意图预览（显示用）** —— 给表现层的**唯一口子**：
    /// 🔴 **不需要（也不该要）战斗 RNG**：`BattleProjector.IntentPreview` 的实现里写着
    ///    `_ = battleRng; // 只读：战斗用 RNG 一律不参与预览` ⇒ 预览**绝不消耗抽数** ⇒ 确定性不变 ✓
    ///    （若把 `_rng` 泄给表现层，迟早有人用它"顺手预览" ⇒ **抽数被吃 ⇒ 回放/复现全崩** ⚠️）
    /// ⇒ 这里传一个**固定种子的预览专用 RNG**，与战斗抽数完全隔离 ✓
    /// </summary>
    public Darkest.Gameplay.Sim.Director.IntentProjection PreviewIntent(Darkest.Core.Contracts.UnitId actor)
        => Projector.IntentPreview(actor, _previewRng, enabled: true);

    /// <summary>预览专用 RNG（固定种子；**只喂预览**，永不参与战斗抽数）✓</summary>
    private readonly RngProvider _previewRng = new(20260915);
    private BattleUi _ui = null!;
    private SkillsConfig _skills = null!;
    private bool _awaitingPlayer;
    private UnitId _activeActor = new("-");
    private bool _gameOver;
    private long _seed = 20260909L;
    private string? _pendingSkill; // 实机单体选一：选定技能后等待玩家点目标
    private int _reinforcePhase;   // 增援两步（#181）：0=无 1=选B(支援位) 2=选X(战斗位)
    private int _reinforceB;

    /// <summary>增援两步阶段（0 none / 1 选B / 2 选X）与已选 B 槽（UI 高亮与点击路由）。</summary>
    public int ReinforcePhase => _reinforcePhase;
    public int ReinforceB => _reinforceB;

    public override void _Ready()
    {
        NewGame();
        GD.Print("[BattleRoot] 战斗就绪：轮到行动者时技能栏/换位可操作；敌方阶段自动结算；R 重开（新 seed）。");

        // 🔴 O-74（阻塞级，`#279/#280`）：**地牢层的入口** ——
        // 此前 `Expedition.tscn` 不可达（`project.godot` 的 main_scene = Battle.tscn，
        // 而 `ExpeditionContext.Begin()` 只在远征场景内部调用 ⇒ `IsActive` 恒 false ⇒ 玩家看不到 M7.5 的任何东西）。
        // 这里给出**启动即可达**的入口：① 界面按钮 ② `--expedition` 命令行直达（供**端到端冒烟**用）。
        var startExpedition = new Button
        {
            Name = "StartExpedition",
            Text = "出发远征（地牢层）",
            Position = new Vector2(520, 700),
            Size = new Vector2(240, 40),
        };
        startExpedition.Pressed += () => GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
        AddChild(startExpedition);

        // 🔴 M8.0 ③（红线 18）：**回城入口也必须从启动场景可达**
        var toHamlet = new Button
        {
            Name = "ToHamlet",
            Text = "回城（Hamlet）",
            Position = new Vector2(280, 700),
            Size = new Vector2(220, 40),
        };
        toHamlet.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/hamlet/Hamlet.tscn");
        AddChild(toHamlet);
        GD.Print("[BattleRoot] 地牢层入口就绪：StartExpedition 按钮（或 --expedition 命令行）⇒ res://scenes/expedition/Expedition.tscn");
        GD.Print("[BattleRoot] 回城入口就绪：ToHamlet 按钮（或 --hamlet 命令行）⇒ res://scenes/hamlet/Hamlet.tscn");

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--hamlet"))
        {
            GD.Print("[BattleRoot] --hamlet ⇒ 直接回城（端到端冒烟路径：启动 → 回城）");
            GetTree().CallDeferred("change_scene_to_file", "res://scenes/hamlet/Hamlet.tscn");
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--expedition" || a == "--e2e"))
        {
            GD.Print("[BattleRoot] --expedition/--e2e ⇒ 直接进入地牢层（端到端冒烟路径：启动 → 进入地牢层选路）");
            // 🔴 必须 **deferred**：`_Ready` 期间父节点正在增删子节点，直接 ChangeSceneToFile 会报
            // 「Parent node is busy adding/removing children」（实测 exit 1）
            GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
        }

        // 🔴 冒烟（`#307`⑤ 流程闭环）：`--battle-auto-finish` ⇒ **自动结束本场并自动点【继续（回远征）】**
        //    目的：把「战斗 → 返回远征地图」这段**真实场景往返**变成可 headless 验证的一步。
        //    做法：敌方 HP 清零（= 胜利）⇒ 走**既有结束路径** `EndGame` ⇒ 触发远征回灌 + 继续按钮。
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-auto-finish"))
        {
            GD.Print("[BattleRoot] --battle-auto-finish ⇒ 自动结束本场（判定胜利）并自动返回远征");
            CallDeferred(nameof(AutoFinishBattle));
        }

        // 🔴 审计清单③ 冒烟：`--focus-audit` ⇒ 打印焦点所有者与可聚焦控件数（键盘/手柄导航的取证）
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--focus-audit"))
        {
            CallDeferred(nameof(PrintFocusAudit));
        }

        // 🔴 片③ 冒烟：`--battle-map` ⇒ **切到 E 区的【地图】页**（验"战斗里能看到同一趟的地图"）
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-map"))
        {
            CallDeferred(nameof(ShowMapPage));
        }

        // 🔴 片③ 冒烟：`--battle-tab=N` ⇒ 切到 E 区第 N 页（0 详情 ／ 1 日志 ／ 2 序列 ／ 3 编成 ／ 4 地图）
        string? tabArg = System.Array.Find(OS.GetCmdlineArgs(), a => a.StartsWith("--battle-tab=", StringComparison.Ordinal));
        if (tabArg is not null && int.TryParse(tabArg["--battle-tab=".Length..], out int tabIdx))
        {
            CallDeferred(nameof(ShowTab), tabIdx);
        }

        // 🔴 片③ 冒烟：`--battle-card=N` ⇒ **真实点击第 N 张我方卡**（验"点单位 ⇒ 锁进详情页"）
        string? cardArg = System.Array.Find(OS.GetCmdlineArgs(), a => a.StartsWith("--battle-card=", StringComparison.Ordinal));
        if (cardArg is not null && int.TryParse(cardArg["--battle-card=".Length..], out int cardSlot))
        {
            CallDeferred(nameof(ShowCardDetail), cardSlot);
        }

        // 🔴 跨场景步进冒烟（`ui_three_screens.md` §3 / `#310`⑦）：每进一个场景消费一步
        SmokeScript.Step(this);
    }

    /// <summary>
    /// 🔴 供**跨场景步进冒烟**：自动打完本场 + 自动点【继续（回远征）】（真实路径）。
    /// ⚠️ **必须延迟调用**（与 `--battle-auto-finish` 同路径）：直接调用会在"信号/`_Ready` 内改场景"时踩坑
    /// （实测：直接调用 ⇒ 战斗打不完、也切不出去）。
    /// </summary>
    public void PressAutoFinish()
    {
        _autoContinue = true; // 🔴 让 `EndGame` 里的"点继续"也生效（不再依赖命令行旗标）
        CallDeferred(nameof(AutoFinishBattle));
    }

    /// <summary>本实例是否要"自动点继续"（由 `PressAutoFinish` 置位；命令行旗标仍并行生效）。</summary>
    private bool _autoContinue;

    /// <summary>🔴 审计清单③ 冒烟：打印焦点审计（键盘/手柄导航的取证）。</summary>
    private void PrintFocusAudit()
    {
        GD.Print($"[焦点审计] {_ui.FocusAudit()}");
        GD.Print($"[容器审计] {_ui.ContainerAudit()}"); // 清单②（容器+锚点）的取证
    }

    /// <summary>🔴 片③ 冒烟：**切到 E 区第 N 页**（0 详情 ／ 1 日志 ／ 2 序列 ／ 3 编成 ／ 4 地图）。</summary>
    public void ShowTab(int page)
    {
        _ui.SetMultiFunctionPage(page);
        GD.Print($"[片③] 战斗界面：E 区当前页 = {_ui.MultiFunctionPage}（请求 {page}）");
        GD.Print($"[片③·页内容] {_ui.DescribeCurrentPage()}");
    }

    /// <summary>
    /// 🔴 片③ 冒烟：**真实点击第 N 张我方卡**（`N` = **卡序，0 基**）⇒ 应锁进 E 区详情页。
    /// ⚠️ 卡序 ≠ 槽位：我方卡按 DD 式从左到右显示 **4 · 3 · 2 · 1** ⇒ 卡序 0 = **槽位 4** ✓
    ///    我原先把 `N` 直接当槽位用 ⇒ `--battle-card=0` 触发 `ArgumentOutOfRangeException: 槽位 0 越界 [1,6]` ⚠️
    ///    （由 UI 设计师在窗口指出，附证据）⇒ 现按【卡序 → 槽位】映射，且**越界只打印不抛异常** ✓
    /// </summary>
    private void ShowCardDetail(int cardIndex)
    {
        const int PlayerCombatCards = 4; // 我方战斗位 4 张（显示顺序 4·3·2·1）
        if (cardIndex < 0 || cardIndex >= PlayerCombatCards)
        {
            GD.Print($"[片③] --battle-card={cardIndex} 越界：卡序合法范围 0..{PlayerCombatCards - 1}" +
                     "（我方卡从左到右显示 4·3·2·1；卡序 0 = 槽位 4）—— 不抛异常，仅提示 ✓");
            return;
        }

        int slot = PlayerCombatCards - cardIndex; // 卡序 0 → 槽 4；1 → 3；2 → 2；3 → 1 ✓
        _ui.PressCard(slot, isPlayer: true);
        GD.Print($"[片③] 点单位卡 ⇒ 卡序 {cardIndex} 映射到槽位 {slot}；E 区详情页锁定槽位 = {_ui.LockedSlot}");
    }

    /// <summary>
    /// 🔴 片③ 冒烟：**切到 E 区多功能框的【地图】页**（真实走 `SetMultiFunctionPage` 同一入口）。
    /// ⚠️ 页签顺序 = { 详情 0 ／ 日志 1 ／ 序列 2 ／ 编成 3 ／ **地图 4** } ⇒ 这里必须是 **4**。
    ///    我原先写 2（= 序列）⇒ 实测 `--battle-map` 落在【序列】页，**地图页冒烟根本走不到** ⚠️
    ///    （由 UI 设计师在窗口指出，附证据：`--battle-tab=4` 才是地图页）—— 已修 ✓
    /// </summary>
    private void ShowMapPage()
    {
        const int MapPageIndex = Darkest.UI.BattleUi.MapPageIndex; // 🔴 单一出处：引用 UI 的页签表常量（原另写一份 4 ⇒ 两处真值）✓
        _ui.SetMultiFunctionPage(MapPageIndex);
        GD.Print($"[片③] 战斗界面：E 区当前页 = {_ui.MultiFunctionPage}（{MapPageIndex} = 地图）");
        GD.Print($"[片③] {_ui.DescribeMiniMap()}");
    }

    /// <summary>冒烟用：用**小型自动玩家**把本场**真的打完**（走真实战斗规则）⇒ 再走既有 `EndGame` 路径。</summary>
    private void AutoFinishBattle()
    {
        DirectorBridge.DirectorHandle handle = DirectorBridge.BuildFromRes(this);
        var auto = new Darkest.Gameplay.Sim.Run.SimplePlayerAuto(handle.Skills);
        var rng = new Darkest.Core.Rng.RngProvider(20260909);

        int round = 1;
        for (; round <= 60 && !Director.IsBattleOver; round++)
        {
            Director.RunFullRound(rng, u => auto.Decide(u, Director));
        }

        bool win = Director.Enemy.OccupiedPositions(false).Count == 0;
        GD.Print($"[BattleRoot] --battle-auto-finish ⇒ **自动玩家打完**：{(win ? "胜" : "败")}　回合 {Director.Round}");
        EndGame(win ? "我方胜利（自动玩家）" : "敌方胜利（自动玩家）");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // 🔴 动作化（附 B ①）：`dd_restart` 见 `project.godot [input]`（玩家可重映射）
        if (e.IsAction("dd_restart"))
        {
            NewGame();
            GD.Print($"[BattleRoot] 重开（seed={_seed}）");
            return;
        }

        // F0（#189）：Esc / 右键取消选目标（不消耗行动）—— Esc 走**引擎内置** `ui_cancel` ✓
        bool cancel = e.IsAction("ui_cancel")
                      || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
        if (cancel && (_pendingSkill is not null || _reinforcePhase != 0))
        {
            _pendingSkill = null;
            _reinforcePhase = 0;
            _ui.FlashHint("已取消选择（不消耗行动）");
        }
    }

    private void NewGame()
    {
        var handle = DirectorBridge.BuildFromRes(this);
        Director = handle.Core;
        Projector = handle.Projector;
        _skills = handle.Skills;
        _rng = new RngProvider(++_seed);
        _awaitingPlayer = false;
        _gameOver = false;
        _activeActor = new("-");
        _pendingSkill = null;
        _reinforcePhase = 0;

        if (_ui is null)
        {
            _ui = GetNode<BattleUi>("BattleUi");
        }

        _ui.Bind(host: this, useSkill: (actor, skillId) => DoUseSkill(actor, skillId),
            reinforce: () => OnReinforceClicked(),
            move: () => OnMoveClicked(),
            retreat: () => DoRetreat(),
            pass: () => OnPassClicked());
    }

    /// <summary>S5.2 待命：显式结束该单位本次行动（不消耗 SP、不算技能、不结算任何效果）。</summary>
    public void OnPassClicked()
    {
        if (!_awaitingPlayer)
        {
            return;
        }

        Director.PassTurn(_activeActor);
        _pendingSkill = null;
        _reinforcePhase = 0;
        _awaitingPlayer = false; // 放弃本次行动
        _ui.FlashHint($"{_ui.ArchetypeNameOf(_activeActor)} 待命（不消耗支援点）");
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_gameOver)
        {
            _ui.Refresh(status: "按 R 重开（新 seed）");
            return;
        }

        // P0/O-47：每轮推进先查结果——任一方归零立即收束（不区分先后，不再多打）
        BattleOutcome outcome = Director.Outcome;
        if (outcome == BattleOutcome.Victory)
        {
            EndGame("胜利：敌方全灭");
            return;
        }

        if (outcome == BattleOutcome.Defeat)
        {
            EndGame("失败：我方全灭");
            return;
        }

        if (_awaitingPlayer)
        {
            _ui.Refresh(status: $"回合 {Director.Round} · 轮到 {_activeActor}");
            return; // 等玩家输入
        }

        // 非玩家阶段（回合开始 / 敌方行动）自动推进
        UnitId? actor = Director.NextActor();
        if (actor is null)
        {
            // 本回合队列耗尽（结果已在顶部检查）→ 下回合
            Director.StartTurn(_rng);
            _ui.Refresh(status: $"回合 {Director.Round} 开始");
            return;
        }

        UnitRuntime? playerUnit = FindPlayerUnit(actor.Value);
        if (playerUnit is not null)
        {
            _awaitingPlayer = true;
            _activeActor = actor.Value;
            _ui.Refresh(status: $"回合 {Director.Round} · 轮到 {_activeActor}");
            return;
        }

        Director.EnemyAct(actor.Value, _rng); // 敌方自动
        _ui.Refresh(status: $"回合 {Director.Round}");
    }

    private void DoUseSkill(UnitId actor, string skillId)
    {
        if (!_awaitingPlayer || actor != _activeActor)
        {
            return;
        }

        // F2（#193）恐惧 proc：拒放时技能灰掉、不消耗行动、须重选
        if (Director.TryFearRefusal(actor, _rng))
        {
            _ui.FlashHint("恐惧发作：技能被拒绝（不消耗行动，请重选）");
            return;
        }

        SkillTemplateConfig skill = _skills.Get(skillId);
        int[] candidates = SkillTargetResolver.Resolve(skill, actor, Director.Player, Director.Enemy).ToArray();

        // F0（#189）：一律进入选目标——候选池非空就必须点卡确认（单体/AOE/team/self/any_ally 无例外）
        if (candidates.Length == 0)
        {
            _ui.FlashHint("没有合法目标（技能应已灰显）");
            return;
        }

        if (_pendingSkill == skillId)
        {
            _pendingSkill = null; // 再点同一技能 = 取消（不消耗行动）
            _ui.FlashHint("已取消选择");
            return;
        }

        _pendingSkill = skillId;
        _reinforcePhase = 0;
        _ui.FlashHint($"请选择目标：点亮 {candidates.Length} 张卡（点任意亮卡确认，Esc 取消）");
    }

    /// <summary>「增援」按钮（#181 两步）：未开始 → 选 B（高亮支援位）；再点 → 取消。</summary>
    public void OnReinforceClicked()
    {
        if (!_awaitingPlayer)
        {
            return;
        }

        _pendingSkill = null;
        _reinforcePhase = _reinforcePhase == 0 ? 1 : 0;
        _ui.FlashHint(_reinforcePhase == 1 ? "增援：先选支援位 B（点 5/6 槽）" : "增援已取消");
    }

    /// <summary>「移动」按钮（#180 常驻）：当前战斗位行动者 → 进入移动目标选择（move_range 候选高亮）。</summary>
    public void OnMoveClicked()
    {
        if (!_awaitingPlayer)
        {
            return;
        }

        int pos = Director.Player.UnitAtPosition(_activeActor) ?? -1;
        if (pos is < 1 or > 4)
        {
            _ui.FlashHint("仅战斗位（1~4）可移动");
            return;
        }

        string moveId = "move"; // F1（#191）：池外通用移动技能（距离从单位 move_distance 读）
        SkillTemplateConfig move = _skills.Get(moveId);
        if (SkillTargetResolver.Resolve(move, _activeActor, Director.Player, Director.Enemy).Count == 0)
        {
            _ui.FlashHint("移动：周围无可交换位置");
            return;
        }

        _pendingSkill = moveId;
        _reinforcePhase = 0;
        _ui.FlashHint("选择移动目标（交换位置）");
    }

    /// <summary>卡片点击（UI 回调）：增援两步（#181）优先；否则单体/移动选一（#178/#180）。</summary>
    public void OnCardClicked(int slot, bool isPlayer)
    {
        // 🔴 `ui_spec` §1.1：**点击单位 ⇒ 锁定到 E 区多功能框（详情页）** —— 任何时刻都能看（只读）
        //    ⚠️ 必须在 `_awaitingPlayer` 早退**之前**做，否则"非我方回合时点单位看详情"会被吞掉
        _ui.ShowUnitDetail(slot, isPlayer);

        if (!_awaitingPlayer)
        {
            return;
        }

        // 增援两步路由
        if (_reinforcePhase == 1)
        {
            if (slot is 5 or 6 && Director.Player.UnitRuntimeAt(slot) is not null && !isPlayer == false)
            {
                _reinforceB = slot;
                _reinforcePhase = 2;
                _ui.FlashHint($"增援：已选支援位 {slot}，再选目标战斗位 X（点 1~4）");
            }
            else
            {
                _ui.FlashHint("增援：请点支援位 5/6 中被占用的槽");
            }

            return;
        }

        if (_reinforcePhase == 2)
        {
            if (slot is >= 1 and <= 4)
            {
                bool ok = Director.Reinforce(_activeActor, _reinforceB, slot);
                _ui.FlashHint(ok ? $"增援完成（{_reinforceB} → {slot}）" : "增援被拒（槽位/此回合已增援）");
                if (ok || Director.SwappedThisRound)
                {
                    _awaitingPlayer = false; // 发起者消耗本次行动
                }
            }
            else
            {
                _ui.FlashHint("增援：目标 X 须为战斗位 1~4");
            }

            _reinforcePhase = 0;
            return;
        }

        // 单体/移动选一
        if (_pendingSkill is null)
        {
            return;
        }

        string skillId = _pendingSkill;
        int[] candidates = SkillTargetResolver.Resolve(_skills.Get(skillId), _activeActor, Director.Player, Director.Enemy).ToArray();
        if (Array.IndexOf(candidates, slot) < 0)
        {
            _ui.FlashHint("该目标不在候选中，请点候选卡");
            return;
        }

        ExecutePlayerSkill(_activeActor, skillId, new[] { slot });
    }

    /// <summary>单体选一阶段的候选槽（UI 高亮用）。</summary>
    public int[] PendingCandidates
    {
        get
        {
            if (_pendingSkill is null)
            {
                return Array.Empty<int>();
            }

            return SkillTargetResolver.Resolve(_skills.Get(_pendingSkill), _activeActor, Director.Player, Director.Enemy).ToArray();
        }
    }

    public bool IsTargeting => _pendingSkill is not null;

    /// <summary>待选目标是否在敌方侧（高亮分阵营用：敌方技能=true；any_ally/move 等友方=false）。</summary>
    public bool PendingTargetsEnemy
        => _pendingSkill is not null && _skills.Get(_pendingSkill).Target.Side == "enemy";

    private void ExecutePlayerSkill(UnitId actor, string skillId, int[]? chosen)
    {
        _pendingSkill = null;
        _reinforcePhase = 0;
        Director.PlayerUseSkill(actor, skillId, _rng, chosen);
        _awaitingPlayer = false;
        GD.Print($"[BattleRoot] {actor} 使用 {skillId}" + (chosen is not null ? $" → 槽 {chosen[0]}" : ""));
    }

    private void DoRetreat()
    {
        if (_gameOver)
        {
            return;
        }

        bool success = Director.PlayerRetreat(_rng);
        if (success || Director.IsBattleOver)
        {
            EndGame(success ? "撤退成功" : "撤退失败");
        }
        else
        {
            _ui.Refresh(status: $"回合 {Director.Round} · 撤退失败，本回合不可再试");
        }
    }

    private void EndGame(string what)
    {
        _gameOver = true;
        _awaitingPlayer = false;
        ResultText = what;
        ResultRound = Director.Round;

        // 🔴 M7.5：远征模式 ⇒ 回灌结果 + 显示【继续（回远征）】按钮（**结算面板照常显示**，不再直接切场景）
        if (ExpeditionContext.IsActive)
        {
            // 🔴 `O-83`：**战后落账**（本场结束血量 → 跨趟台账）—— 必须在回灌/切场景**之前**：
            //    下一场开局要读它；切场景后本场景（及 `Director`）就可能被释放 ✓
            ExpeditionContext.Flow!.CaptureBattleEndHp(Director);

            string result = what.Contains("撤退", System.StringComparison.Ordinal)
                ? "DrawRetreat"
                : Director.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
            ExpeditionContext.Flow!.OnBattleFinished(result, Director.Round,
                isAmbush: ExpeditionContext.ConsumePendingAmbush()); // 🔴 #307③：夜袭战斗不是节点步骤

            var toExpedition = new Button
            {
                Name = "ReturnToExpedition",
                Text = "继续（回远征）",
                Position = new Vector2(540, 660),
                Size = new Vector2(200, 40),
            };
            toExpedition.Pressed += () => GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
            AddChild(toExpedition);
            GD.Print($"[BattleRoot] 远征模式：本场结果 {result}，点【继续（回远征）】返回远征界面");

            // 🔴 冒烟：自动点【继续（回远征）】（**真实 `Pressed`** ⇒ 走玩家路径）
            //    ⚠️ 两条入口都要认：① 命令行旗标（旧路径）② `PressAutoFinish()`（步进冒烟的新路径）
            //    —— 此前只认旗标 ⇒ 步进冒烟不带旗标时，"战斗 ⇒ 返回"这一段**走不完**（我实测踩过）。
            if (_autoContinue || System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-auto-finish"))
            {
                GD.Print("[BattleRoot] 自动点【继续（回远征）】（真实 Pressed）");
                toExpedition.EmitSignal(BaseButton.SignalName.Pressed);
            }
        }
        // T-M6-07 系统触发日志 + P0④ 结算面板数据（同一批计数）
        var events = Director.Log.Events;
        int collapse = events.OfType<CollapseResultEvent>().Count();
        int weak = events.OfType<WeakEnterEvent>().Count();
        int dd = events.OfType<DeathDoorEvent>().Count();
        int retreat = events.OfType<RetreatEvent>().Count();
        int virtue = events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Virtue");
        int aff = events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Affliction");
        int disp = events.OfType<DisplaceEvent>().Count();
        ResultCounts = new[] { collapse, weak, dd, retreat, virtue, aff, disp };
        GD.Print($"[BattleRoot] 战斗结束：{what}（第 {Director.Round} 回合）。系统触发：士气触底 {collapse} / 虚弱 {weak} / 死门 {dd} / " +
                 $"撤退出现 {retreat} / 美德 {virtue} / 折磨 {aff} / 位移 {disp}。按 R 重开。");
    }

    private UnitRuntime? FindPlayerUnit(UnitId id)
        => Director.Player.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id);

    public UnitId ActiveActor => _activeActor;

    /// <summary>当前行动者原型 id（技能池/中文名按原型匹配；实例 id 已唯一化）。</summary>
    public string ActiveArchetype
        => Director.Player.UnitsInSlotOrder().FirstOrDefault(u => u.Id == _activeActor)?.ArchetypeId
           ?? Director.Enemy.UnitsInSlotOrder().FirstOrDefault(u => u.Id == _activeActor)?.ArchetypeId
           ?? _activeActor.Value;

    /// <summary>实例 id → 原型 id（行动序列中文名映射用）。</summary>
    public string ArchetypeOf(UnitId id)
        => Director.Player.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id)?.ArchetypeId
           ?? Director.Enemy.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id)?.ArchetypeId
           ?? id.Value;

    public bool IsAwaitingPlayer => _awaitingPlayer;
    public bool GameOver => _gameOver;

    /// <summary>结算面板数据（P0④）：结果文案 / 回合数 / 7 项系统触发计数。</summary>
    public string ResultText { get; private set; } = "";

    public int ResultRound { get; private set; }
    public int[] ResultCounts { get; private set; } = new int[7];
}