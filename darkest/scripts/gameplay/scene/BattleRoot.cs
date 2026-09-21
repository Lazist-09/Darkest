using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
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

    private Darkest.Core.Events.CombatLog? _xpLog;   // 🆕 升级通道：常驻经验日志（事件可审计 ✓）
    private Darkest.Data.RosterConfig? _rosterCfgForXp;   // 🆕 A10 升级读数：懒解析一次（只读，不重算数字）✓
    private RngProvider _rng = null!;

    /// <summary>
    /// 🆕 **敌方意图预览（显示用）** —— 给表现层的**唯一口子**：
    /// 🔴 **不需要（也不该要）战斗 RNG**：`BattleProjector.IntentPreview` 的实现里写着
    ///    `_ = battleRng; // 只读：战斗用 RNG 一律不参与预览` ⇒ 预览**绝不消耗抽数** ⇒ 确定性不变 ✓
    ///    （若把 `_rng` 泄给表现层，迟早有人用它"顺手预览" ⇒ **抽数被吃 ⇒ 回放/复现全崩** ⚠️）
    /// ⇒ 这里传一个**固定种子的预览专用 RNG**，与战斗抽数完全隔离 ✓
    /// </summary>
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // 🔴 **`#327` 片 3 第一步：宿主侧的【流程驱动口】**（`tasks/seamless_single_scene.md` 片 3 = 我的域）
    //    现状：流程驱动（`Advance`/`OnBattleFinished`）写在 `ExpeditionRoot`（UI 侧，片 2 期间归他）
    //    ⇒ 片 3 的终态是"**由宿主（本文件）驱动流程**"，但那需要**场景内切战斗**（片 4 同批）⚠️
    //    ⇒ 本步只做**加法**：把"宿主能驱动流程"的口开出来并**如实报告**，
    //      不改现有生命周期（远征场景仍负责切场景）⇒ 现有游戏行为**零变化** ✓
    //    📌 判据（红线 25）：这一步**不是**片 3 完成 —— 只是"口开在这里、由谁调用"已定 ✓
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>🔴 片 3：当前是否有**远征流程**可驱动（没有 ⇒ 单场战斗模式，一切谓词/面板都不适用）✓</summary>
    public Darkest.Gameplay.Sim.Run.ExpeditionFlow? ExpeditionFlowOrNull
        => ExpeditionContext.IsActive ? ExpeditionContext.Flow : null;

    /// <summary>
    /// 🔴 片 3：宿主侧**推进流程一步**（地图模式下"选路"的落点）。
    /// 返回 = 这一步是否为**战斗步骤**（若是，片 4 会在这里**场景内切战斗**，而不是切场景）✓
    /// </summary>
    public bool MapModeAdvance(int optionIndex)
    {
        Darkest.Gameplay.Sim.Run.ExpeditionFlow? flow = ExpeditionFlowOrNull;
        if (flow is null)
        {
            return false;
        }

        Darkest.Gameplay.Sim.Run.FlowStep step = flow.Advance(optionIndex);
        bool isBattle = step.Kind == Darkest.Gameplay.Sim.Run.FlowStepKind.Battle;
        GD.Print($"[片3] 宿主驱动流程：Advance({optionIndex}) ⇒ 步骤 {step.Kind}（节点 {step.NodeId}）" +
                 (isBattle ? "　🔴 战斗步骤：**片 4 起在本场景内切战斗**（当前仍由远征场景切场景）✓" : string.Empty));
        return isBattle;
    }
    public Darkest.Gameplay.Sim.Director.IntentProjection PreviewIntent(Darkest.Core.Contracts.UnitId actor)
        => Projector.IntentPreview(actor, _previewRng, enabled: true);

    /// <summary>预览专用 RNG（固定种子；**只喂预览**，永不参与战斗抽数）✓</summary>
    private readonly RngProvider _previewRng = new(20260915);
    private BattleUI _ui = null!;
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

    /// <summary>🔴 **B-1 标记**：本实例是「被面板托管的战斗」（防止面板宿主自己再造面板 ⇒ 递归）✓</summary>
    private const string HostedInPanelMeta = "d87_battle_panel_hosted";

    public override void _Ready()
    {
        // 🔴 **B-1（形态 B · S4 第一步）**：--battle-panel ⇒ **本场景根不再自己驱动战斗**，
        //    而是**造一个战斗面板挂进外壳**（面板内那个 BattleRoot 会正常启动 ✓）
        //    ⚠️ 防递归：面板内那个实例带 meta 标记，不会再进这个分支 ✓

        // 🔴 **B-2 判据（我域）**：`--shell-layer` ⇒ 打印「外壳有效绘层 vs 当前场景有效绘层」✓
        //    （架构要的"外壳三层都在当前场景之上"的**可核对读数** ⇒ 判据本体在 `ShellLayerAudit` ✓）

        // 🔴 **C4 终态判据（我域）**：`--shell-count` ⇒ 数外壳实例数 + 看 `main_scene`/`autoload` ✓
        //    （架构原话：判据「外壳现在有几个实例？」**必须是 1** ✓）
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--shell-count"))
        {
            ShellInstanceAudit.Run(this);
        }

        // 🔴 **形态 B 一键状态（我域）**：`--shell-status` ⇒ 绘层 + 回落账本 + 实例数 一次看全 ✓
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--shell-status"))
        {
            ShellStatus.Run(this);
        }
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--shell-layer"))
        {
            ShellLayerAudit.Run(this);
        }
        bool hosted = HasMeta(HostedInPanelMeta);
        bool wantsPanel = !hosted && System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-panel");
        if (wantsPanel)
        {
            GD.Print("[BattleRoot] 🔴 --battle-panel ⇒ **走面板路径**（本场景根只做宿主，不再自己驱动战斗）✓");
            BattlePanel panel = BattlePanel.Create("BattlePanel");
            panel.Root?.SetMeta(HostedInPanelMeta, true);
            if (!panel.TryMountIntoShell())
            {
                GD.Print("[BattleRoot] 🔴 面板未挂进外壳 ⇒ 如实停在这里（不假装成功 ✓）");
            }

            return;
        }
        // 🔴🔴 **相位在【两个场景】之间的修补**（UI 实测报告：战斗场景里 `Phase` 仍是 `Walking` ⇒ 三谓词全 True ⚠️）
        //   根因：战斗是**另一个场景**，而 `ExpeditionSession.Phase` 只由远征那条循环推动 ⇒ 战斗期间它"诚实但过时" ✓
        //   ⇒ 现在进战斗就显式推进到 `Battle`（片 3 把两场景并成一个状态机后，这行会被状态机自然取代）✓
        if (ExpeditionContext.IsActive)
        {
            ExpeditionContext.Flow!.Session.EnterPhase(Darkest.Gameplay.Sim.Run.FlowPhase.Battle);
        }

        // 🔴 片 3 冒烟触发器：`--battle-piece3-exp` ⇒ 由宿主**在场景内**起流程战斗（验证"不再切场景"这条路）✓
        // 🔴 片 4④：`--dungeon-in-scene` ⇒ **宿主直接进地牢**（用新组装 `ExpeditionComposition`，不经过远征场景）✓
        // 🔴 片 4①：**入口请求**（主菜单/Hamlet 的"出发远征"）⇒ 进地图模式（**默认路径**，不再切远征场景）✓
        if (ExpeditionContext.ConsumeRequestDungeon())
        {
            EnterDungeonInScene();
        }

        // 🔴 片 4：远征驱动类 CLI（`--topology-auto` / `--hamlet-next` / `--e2e`）**已搬进宿主侧**
        //    （原在 `ExpeditionRoot`；搬走它，退休旧场景才不留悬空依赖 ✓）
        if (ExpeditionContext.IsActive && ExpeditionContext.Flow is { } drvFlow)
        {
            DungeonRunDriver.TryHandle(this, drvFlow, ExpeditionContext.Log ?? new Darkest.Core.Events.CombatLog());
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--dungeon-in-scene"))
        {
            EnterDungeonInScene();
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-piece3-exp"))
        {
            StartExpeditionBattleInScene();
        }

        // 🔴🔴 **片 3.1（用户裁定"进地牢不该一进来就是战斗" · 策划 `#384` 第 2 件）**：
        //    旧行为：无论从哪个入口进来，`_Ready` 末尾都**无条件** `NewGame()` ⇒ **进地牢 = 立刻起一场战斗** ❌
        //    新行为：**按【请求】分流** —— 已经进了地牢（有活动流程）⇒ **不起单场战斗**，只进 Walking（地图模式）✓
        //    其它入口（单场战斗 / `--battle-*` 冒烟 / 试验）⇒ 才 `NewGame()` ✓
        bool hostedInDungeon = _dungeonHostedInScene && ExpeditionContext.IsActive;
        if (hostedInDungeon)
        {
            GD.Print($"[片3.1] ✅ **进地牢 = 不起战斗**（按请求分流）：Phase={ExpeditionContext.Flow!.Session.Phase}（应为 Walking）" +
                     $"　CanShowPathChoice={ExpeditionContext.Flow.Session.CanShowPathChoice}（应为 True）" +
                     $"　战斗触发改为【踏进 Battle 格】（策划 #379）✓");
        }
        else
        {
            NewGame();
            GD.Print("[BattleRoot] 战斗就绪：轮到行动者时技能栏/换位可操作；敌方阶段自动结算；R 重开（新 seed）。");
        }

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
        startExpedition.Pressed += () => EnterDungeonInScene(); // 🔴 片 4：场景内进地牢（不再切场景）✓
        AddChild(startExpedition);

        // 🔴 M8.0 ③（红线 18）：**回城入口也必须从启动场景可达**
        var toHamlet = new Button
        {
            Name = "ToHamlet",
            Text = "回城（Hamlet）",
            Position = new Vector2(280, 700),
            Size = new Vector2(220, 40),
        };
        toHamlet.Pressed += GoToHamlet;   // 🆕 外壳优先、缺失回落（见 GoToHamlet）
        AddChild(toHamlet);
        GD.Print("[BattleRoot] 地牢层入口就绪：StartExpedition 按钮（或 --expedition 命令行）⇒ **本场景内进地牢**（片 4：唯一宿主）✓");
        GD.Print("[BattleRoot] 回城入口就绪：ToHamlet 按钮（或 --hamlet 命令行）⇒ res://scenes/hamlet/Hamlet.tscn");

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--hamlet"))
        {
            GD.Print("[BattleRoot] --hamlet ⇒ 直接回城（端到端冒烟路径：启动 → 回城）");
            GoToHamlet();
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--expedition" || a == "--e2e"))
        {
            GD.Print("[BattleRoot] --expedition/--e2e ⇒ 直接进入地牢层（端到端冒烟路径：启动 → 进入地牢层选路）");
            // 🔴 必须 **deferred**：`_Ready` 期间父节点正在增删子节点，直接 ChangeSceneToFile 会报
            // 「Parent node is busy adding/removing children」（实测 exit 1）
            EnterDungeonInScene(); // 🔴 片 4：场景内进地牢（不再切场景）✓
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
        // 🔴 片 3.1：**地图模式没有战斗** ⇒ 冒烟/脚本的"自动打完"必须**如实拒绝**（否则刷屏 NRE ⚠️）✓
        if (!HasActiveBattle)
        {
            GD.Print("[片3.1] 无活动战斗 ⇒ 拒绝「自动打完」（这是地图模式，不是战斗）✓");
            return;
        }

        _autoContinue = true; // 🔴 让 `EndGame` 里的"点继续"也生效（不再依赖命令行旗标）
        CallDeferred(nameof(AutoFinishBattle));
    }

    /// <summary>🔴 片 3.1：**是否正在进行一场战斗**（`Director` 只在 `NewGame()` 后非空）⇒ 战斗专用路径必须先问它 ✓</summary>
    public bool HasActiveBattle => Director is not null;

    /// <summary>🔴 冒烟用：把"按下【放弃远征】"透到 UI（**真实 `Pressed`** ⇒ 走玩家路径，红线 18）✓</summary>
    public void PressAbandonUi() => _ui?.PressAbandon();

    private bool _noBattleNoticeShown; // 只提示一次（防刷屏）✓

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
        const int MapPageIndex = Darkest.UI.BattleUI.MapPageIndex; // 🔴 单一出处：引用 UI 的页签表常量（原另写一份 4 ⇒ 两处真值）✓
        _ui.SetMultiFunctionPage(MapPageIndex);
        GD.Print($"[片③] 战斗界面：E 区当前页 = {_ui.MultiFunctionPage}（{MapPageIndex} = 地图）");
        GD.Print($"[片③] {_ui.DescribeMiniMap()}");
    }

    /// <summary>冒烟用：用**小型自动玩家**把本场**真的打完**（走真实战斗规则）⇒ 再走既有 `EndGame` 路径。</summary>
    private void AutoFinishBattle()
    {
        if (!HasActiveBattle)
        {
            GD.Print("[片3.1] 无活动战斗 ⇒ 不自动打完（地图模式）✓");
            return;
        }
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
            if (_dungeonHostedInScene)
            {
                // 🔴 片 3.1：**地牢模式**下 `R` **不重开单场战斗**（否则又会"进地牢就起战斗"）
                GD.Print("[片3.1] 地牢模式：`R` 不重开单场战斗（要重开请退出本趟）✓");
                return;
            }

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

    /// <summary>把 UI 绑到当前 `Director`（`NewGame` 与"场景内起远征战斗"共用同一条绑定 ⇒ 不会两处各写一份）✓</summary>
    /// <summary>
    /// 🔴🔴 **`#327` 片 4：在【本场景内】进入地牢**（组装流程 + 切地图模式，**不切场景**）
    /// 组装交给 `ExpeditionComposition.BuildInScene`（从 `ExpeditionRoot` 搬过来的那一段）✓
    /// </summary>
    /// <summary>🔴 片 4 过渡标记：本趟地牢是**宿主内进的**（true）还是**从远征场景进来的**（false）——
    /// 战后"继续"按钮据此分流：宿主内 ⇒ 回地图模式（不切场景）；旧路径 ⇒ 仍回远征场景（**保住旧冒烟循环**）✓</summary>
    private bool _dungeonHostedInScene;

    /// <summary>
    /// 🆕 **回城入口（UI Track 1 · 事项 B）**：`UIRoot` 单外壳优先，**缺失/未就绪就回落**到原来的硬切场景 ✓
    /// 🔴 为什么写成"外壳优先 + 回落"：`UIRoot` 是 UI 域的可选外壳（他们另有 `TryInstantiate()` 回落）⇒
    ///    主程序侧**不假设它一定在**（无 autoload / 未注册 / 场景缺失 ⇒ 都不会崩，且行为与改造前一致 ✓）。
    /// ⚠️ 本方法**只用于玩家路径**；`DungeonRunDriver`（e2e 驱动器）**故意不动** —— 不把测试驱动与玩家路径搅在一起 ✓
    /// </summary>

    private void BindUi()
    {
        if (_ui is null)
        {
            _ui = GetNode<BattleUI>("UILayer/BattleUI");
        }

        _ui.Bind(host: this, useSkill: (actor, skillId) => DoUseSkill(actor, skillId),
            reinforce: () => OnReinforceClicked(),
            move: () => OnMoveClicked(),
            retreat: () => DoRetreat(),
            pass: () => OnPassClicked());
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

            // 🔴🆕 **升级通道接线**（契约 `hamlet.md` §7.2/§7.6「**战斗给经验 ⇒ 等级成长**」·
            //    策划 `#399` 给了**占位数值**）⇒ 战斗结算就给全队发经验 ✓
            //    纪律：**数值全部来自 `roster.json`**（缺省 ⇒ 显式不生效 ✓ 不假装）· **事件留在常驻日志里**（可审计 ✓）
            if (ExpeditionContext.Roster is { } xpRoster && xpRoster.ExperienceWired)
            {
                bool won = result == "PlayerVictory";
                _xpLog ??= new Darkest.Core.Events.CombatLog();
                xpRoster.AwardExperienceForBattle(_xpLog, won, "battle");
                GD.Print("[升级通道] 本场" + (won ? "胜" : "负") + " ⇒ 发经验：" +
                         string.Join("、", System.Linq.Enumerable.Select(xpRoster.Heroes,
                             h => $"{h.Name} Lv{h.Level}(XP{xpRoster.ExperienceOf(h.Id)})")) + " ✓");

                // 🔴 **A10 可读性读数**（策划 `#399`；`RosterConfig.BattlesToNextLevel` 的**生产消费点**，2026-09-20 接线）——
                //    玩家感受到的是"**我打了 N 场，升了 1 级**" ⇒ 这句把"还差几场"直接说出来 ✓
                //    此前该读法**只被测试调用** ⇒ "升级通道有没有意义"这个判据**在游戏里看不见** ⚠️
                // 🔴 读法归属：`BattlesToNextLevel` / `CostToNextLevel` 定义在 **`RosterExperience`** 上
                //    （`RosterConfig` 只有 `LevelGrowth` / `Experience` 两个成员）⇒ 必须经 `cfg.Experience?.` 进入 ✓
                Darkest.Data.RosterConfig cfg = _rosterCfgForXp ??= Darkest.Data.RosterConfig.Parse(
                    Godot.FileAccess.GetFileAsString(Darkest.Data.RosterConfig.ResPath));
                Darkest.Data.RosterExperience? xp = cfg.Experience;
                GD.Print("　[A10 升级读数] " + string.Join("、", System.Linq.Enumerable.Select(xpRoster.Heroes, h =>
                {
                    int? battles = xp?.BattlesToNextLevel(h.Level, cfg.LevelMin, cfg.LevelMax);
                    return battles is { } n ? $"{h.Name} 再 {n} 场升 Lv{h.Level + 1}" : $"{h.Name} 已满级";
                })) + " ✓");
            }

            // 🔴 `#352` 打印自证（`retreat.md §11` 要的"撤退 ⇒ 回地图当前格"）：这一行让"撤没撤对"**可读** ✓
            if (result == "DrawRetreat")
            {
                GD.Print($"[片4] ↩️ **撤退**（【一场】的选择）⇒ 回地图当前格**继续走**（本趟**不**结束）✓" +
                         $"　IsFinished={ExpeditionContext.Flow?.IsFinished}（应为 False）" +
                         $"　Outcome={ExpeditionContext.Flow?.Outcome}（应为 InProgress）✓");
            }
            // 🔴 修 UI 报的 4 条：**流程层的 `OnBattleFinished` 只接受【当前步骤是战斗节点】时调用** ——
            //    冒烟的 `auto` 可能在"非战斗步骤"上触发战斗结束（宿主内进地牢的步骤对齐还没做完）⇒ 那会被流程**如实拒绝** ✓
            //    ⇒ 这里**先判**，不把非法调用递进去（红线 21：不静默、也不假装）✓
            // 🔴 判据修正（实测教训）：**拓扑模式下 `flow.Current` 恒为 null**（`BeginTopology` 只设 `_currentRoomId`）
            //    ⇒ 原先按 `Current.Kind` 判 ⇒ 恒假 ⇒ 战斗结算被静默跳过 ⚠️
            //    ⇒ 正解：**按当前房间类型**判（拓扑）＋ 兼容线性步（`Current.Kind`）✓
            bool flowExpectsBattle =
                ExpeditionContext.Flow!.Current?.Kind == Darkest.Gameplay.Sim.Run.FlowStepKind.Battle
                || ExpeditionContext.Flow.CurrentRoomType == "battle";
            if (!flowExpectsBattle)
            {
                GD.Print($"[片4] ⚠️ 战斗结束但**当前步骤不是战斗节点**（{ExpeditionContext.Flow.Current?.Kind}）"
                         + "⇒ 不调用 `OnBattleFinished`（否则流程如实抛错）；这属【宿主内进地牢的步骤对齐未完成】✓");
            }

            if (flowExpectsBattle)
            {
            ExpeditionContext.Flow!.OnBattleFinished(result, Director.Round,
                isAmbush: ExpeditionContext.ConsumePendingAmbush()); // 🔴 #307③：夜袭战斗不是节点步骤

            var toExpedition = new Button
            {
                Name = "ReturnToExpedition",
                Text = "继续（回地图）", // 🔴 片 4②：不再是"回远征**场景**"，而是**回地图模式**（同一场景）✓
                Position = new Vector2(540, 660),
                Size = new Vector2(200, 40),
            };
            // 🔴 片 4②：战后**留在本场景、切回地图模式**（由宿主继续驱动流程；不再切场景、也不再重跑组装）✓
            toExpedition.Pressed += () =>
            {
                if (_dungeonHostedInScene)
                {
                    // 🔴 片 4②：**宿主内进的**地牢 ⇒ 战后**留在本场景、切回地图模式**（不切场景、不重跑组装）✓
                    _ui.EnterMapMode();
                    GD.Print($"[片4] ✅ 战后回地图模式（同一场景）：Phase={ExpeditionContext.Flow!.Session.Phase}　" +
                             $"CanShowPathChoice={ExpeditionContext.Flow.Session.CanShowPathChoice}（应为 True）✓");
                    // 🔴 片 4 收口修：**同场景内**的模式切换**没有新的 `_Ready`** ⇒ 冒烟待办步骤要在此**续跑**
                    //    （实测：`--smoke=main:1,town` 的第 2 步 `town` 曾永远停着 ⚠️）
                    if (SmokeScript.HasPending)
                    {
                        SmokeScript.Step(this);
                    }
                }
                // 🔴 片 4 收尾：**旧场景已退休** ⇒ 两条路都回**同一场景的地图模式**（过渡分支删除 ✓）
                _ui.EnterMapMode();
            };

            AddChild(toExpedition);
            GD.Print($"[BattleRoot] 远征模式：本场结果 {result}，点【继续（回地图）】回地图模式继续走图");

            // 🔴 冒烟：自动点【继续（回远征）】（**真实 `Pressed`** ⇒ 走玩家路径）
            //    ⚠️ 两条入口都要认：① 命令行旗标（旧路径）② `PressAutoFinish()`（步进冒烟的新路径）
            //    —— 此前只认旗标 ⇒ 步进冒烟不带旗标时，"战斗 ⇒ 返回"这一段**走不完**（我实测踩过）。
            if (_autoContinue || System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-auto-finish"))
            {
                GD.Print("[BattleRoot] 自动点【继续（回远征）】（真实 Pressed）");
                toExpedition.EmitSignal(BaseButton.SignalName.Pressed);
            }

            } // ← 收 `if (flowExpectsBattle)`（🔴 修 UI 报的 4 条：非战斗步骤**不递非法调用**给流程）✓
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
