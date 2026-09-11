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
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, PhysicalKeycode: Key.R })
        {
            NewGame();
            GD.Print($"[BattleRoot] 重开（seed={_seed}）");
            return;
        }

        // F0（#189）：Esc / 右键取消选目标（不消耗行动）
        bool cancel = e is InputEventKey { Pressed: true, PhysicalKeycode: Key.Escape }
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