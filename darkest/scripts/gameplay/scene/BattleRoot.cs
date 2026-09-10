using Darkest.Core.Contracts;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Director;
using Darkest.UI;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// BattleRoot：主战斗场景组合根（blueprint §5d / T-M5-05）——只做装配/转发/订阅：
/// 持有 BattleDirector（纯 C# 确定性内核）与 BattleProjector（只读视图）。
/// 本文件含一个【演示驱动】_Process（每 1.2s 自动推一回合 + 更新撤退按钮数字），
/// 供编辑器 F5 直接观看 MVP 骨架；真实输入流由 M5/M6 的 BattleCommand 管线接入（此处不重复实现规则）。
/// </summary>
public partial class BattleRoot : Node2D
{
    public BattleDirector Director { get; private set; } = null!;
    public BattleProjector Projector { get; private set; } = null!;

    private RngProvider _rng = null!;
    private BattleUi _ui = null!;
    private double _turnTimer;
    private const double TurnInterval = 1.2;
    private bool _ended;

    public override void _Ready()
    {
        var handle = DirectorBridge.BuildFromRes(this);
        Director = handle.Core;
        Projector = handle.Projector;
        _rng = new RngProvider(20260909L);

        _ui = GetNode<BattleUi>("BattleUi");
        _ui.Bind(Director, Projector, _rng, (actor, skillId) => Director.PlayerUseSkill(actor, skillId, _rng));

        GD.Print("[BattleRoot] 装配完成：内核 BattleDirector 就绪。F5 运行 → 每回合自动推进；四分区控件已挂载（槽位/行动序列/技能按钮）。");
    }

    public override void _Process(double delta)
    {
        _ui.Refresh(); // 每帧只读投影刷新（无抽取、零写，blueprint §4 B5）
        if (_ended)
        {
            return;
        }

        _turnTimer += delta;
        if (_turnTimer < TurnInterval)
        {
            return;
        }

        _turnTimer = 0.0;

        if (Director.IsBattleOver)
        {
            _ended = true;
            GD.Print($"[BattleRoot] 战斗结束（第 {Director.Round} 回合）。MVP 演示停止。");
            return;
        }

        // 演示策略：每回合敌方行动 + 两发我方固定动作（重劈 敌1 / 急救 我方满血目标由内核处理）
        Director.StartTurn(_rng);
        Director.EnemyPhase(_rng);
        Director.PlayerUseSkill(new UnitId("warrior"), "warrior_cleave", _rng);
        Director.PlayerUseSkill(new UnitId("medic"), "medic_first_aid", _rng);

        GD.Print($"[BattleRoot] 回合 {Director.Round}: 敌方剩余 {Director.Enemy.OccupiedPositions(false).Count}, " +
                 $"我方剩余 {Director.Player.OccupiedPositions(false).Count}, 撤退成功率 {(int)Director.CurrentRetreatRate()}%");
    }
}