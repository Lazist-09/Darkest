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

        if (_ui is null)
        {
            _ui = GetNode<BattleUi>("BattleUi");
        }

        _ui.Bind(host: this, useSkill: (actor, skillId) => DoUseSkill(actor, skillId),
            swap: (actor, supportPos) => DoSwap(actor, supportPos),
            retreat: () => DoRetreat());
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_gameOver)
        {
            _ui.Refresh(status: "按 R 重开（新 seed）");
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
            // 本回合队列耗尽 → 胜负判定 → 下回合
            if (Director.IsBattleOver)
            {
                EndGame("胜利：敌方全灭");
                return;
            }

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

        SkillTemplateConfig skill = _skills.Get(skillId);
        int[] candidates = SkillTargetResolver.Resolve(skill, actor, Director.Player, Director.Enemy).ToArray();
        // #178/#179：单体伤害（非 aoe）或 any_ally 单体支援，候选 >1 → 玩家选择目标（实机交互）
        bool singleton = skill.Damage is not null && !skill.Tags.Contains(FuncTag.Aoe)
                         || skill.Damage is null && skill.Target.Scope == SkillTargetScope.AnyAlly;
        if (singleton && candidates.Length > 1)
        {
            _pendingSkill = skillId;
            _ui.FlashHint($"请选择目标（{candidates.Length} 个候选中点卡）");
            return;
        }

        ExecutePlayerSkill(actor, skillId, null);
    }

    /// <summary>卡片点击（UI 回调）：单体/any_ally 选一阶段点中候选目标 → 执行。</summary>
    public void OnCardClicked(int slot, bool isPlayer)
    {
        if (_pendingSkill is null || !_awaitingPlayer)
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

    private void ExecutePlayerSkill(UnitId actor, string skillId, int[]? chosen)
    {
        _pendingSkill = null;
        Director.PlayerUseSkill(actor, skillId, _rng, chosen);
        _awaitingPlayer = false;
        GD.Print($"[BattleRoot] {actor} 使用 {skillId}" + (chosen is not null ? $" → 槽 {chosen[0]}" : ""));
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

    private void DoSwap(UnitId actor, int supportPos)
    {
        if (!_awaitingPlayer || actor != _activeActor)
        {
            return;
        }

        bool ok = Director.PlayerSwap(actor, supportPos);
        if (!ok)
        {
            // 被拒（支援位空/本回合已换/发起者不在战斗位）：不吞行动，提示原因
            _ui.FlashHint("换位被拒（支援位空或本回合已换位）");
            return;
        }

        GD.Print($"[BattleRoot] {actor} 与支援位 {supportPos} 换位");
        _awaitingPlayer = false;
        _ui.Refresh(status: $"回合 {Director.Round} · 换位完成");
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
        // T-M6-07 系统触发日志（供试玩报告填写）
        var events = Director.Log.Events;
        int collapse = events.OfType<CollapseResultEvent>().Count();
        int weak = events.OfType<WeakEnterEvent>().Count();
        int dd = events.OfType<DeathDoorEvent>().Count();
        bool retreat = events.OfType<RetreatEvent>().Any();
        int virtue = events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Virtue");
        int aff = events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Affliction");
        int disp = events.OfType<DisplaceEvent>().Count();
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
}