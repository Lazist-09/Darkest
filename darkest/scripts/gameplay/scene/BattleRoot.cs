using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
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
    private bool _awaitingPlayer;
    private UnitId _activeActor = new("-");
    private bool _gameOver;
    private long _seed = 20260909L;

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
        _rng = new RngProvider(++_seed);
        _awaitingPlayer = false;
        _gameOver = false;
        _activeActor = new("-");

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
            _ui.Refresh(status: "战斗结束");
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

        if (actor.Value.Value is "win" or "lose")
        {
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

        Director.PlayerUseSkill(actor, skillId, _rng);
        _awaitingPlayer = false;
        GD.Print($"[BattleRoot] {actor} 使用 {skillId}");
    }

    private void DoSwap(UnitId actor, int supportPos)
    {
        if (!_awaitingPlayer || actor != _activeActor)
        {
            return;
        }

        bool ok = Director.PlayerSwap(actor, supportPos);
        GD.Print(ok ? $"[BattleRoot] {actor} 与支援位 {supportPos} 换位" : "[BattleRoot] 换位被拒");
        _awaitingPlayer = false; // 不论成败均消耗本次行动（发起者已行动）
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
    public bool IsAwaitingPlayer => _awaitingPlayer;
    public bool GameOver => _gameOver;
}