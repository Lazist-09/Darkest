using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// BattleRoot：主战斗场景组合根（blueprint §5d / T-M5-05）——只做装配/转发/订阅：
/// 持有 BattleDirector（纯 C# 确定性内核）与 BattleProjector（只读视图），把 UI 命令转交导演、
/// 把事件流转交表现层。零规则实现（blueprint §4 B4 边界）。
/// </summary>
public partial class BattleRoot : Node2D
{
    // 装配注入点：Res:// 数据 → 内核（Godot 侧薄层；headless 用同一内核同一份 JSON）
    public DirectorBridge.DirectorHandle Director { get; private set; } = null!;

    public override void _Ready()
    {
        // 最小装配：从 res://data 加载配置并构建 BattleDirector（内核零 Godot 引用）
        Director = DirectorBridge.BuildFromRes(this);
        // BattleUi 等子节点通过 IBattleView 只读网关与该组合根单向交互（此处仅记录装配位）
    }
}

/// <summary>
/// 内核装配桥（Godot 表现层专用，只做 JSON→配置→BattleDirector 的薄翻译；内核逻辑零 Godot）。
/// </summary>
public static class DirectorBridge
{
    public sealed class DirectorHandle
    {
        public Darkest.Gameplay.Sim.Director.BattleDirector Core { get; init; } = null!;
        public Darkest.Gameplay.Sim.Director.BattleProjector Projector { get; init; } = null!;
    }

    /// <summary>从 res://data 读 JSON 并构建导演（含只读投影）。</summary>
    public static DirectorHandle BuildFromRes(Node host)
    {
        _ = host;
        string Read(string name) => FileAccess.GetFileAsString($"res://data/{name}");

        var balance = Darkest.Data.BalanceTable.FromTuning(
            Darkest.Data.TuningConfig.Parse(Read("tuning.json")));
        var director = new Darkest.Gameplay.Sim.Director.BattleDirector(
            Darkest.Data.FormationConfig.Parse(Read("formation.json")),
            Darkest.Data.UnitsConfig.Parse(Read("units.json")),
            Darkest.Data.SkillsConfig.Parse(Read("skills.json")),
            balance,
            Darkest.Data.MoraleEventsConfig.Parse(Read("morale_events.json")),
            Darkest.Data.BuffDefsConfig.Parse(Read("buff_defs.json")),
            Darkest.Data.EnemyAiConfig.Parse(Read("enemy_ai.json")),
            new Darkest.Core.Events.CombatLog());
        var projector = new Darkest.Gameplay.Sim.Director.BattleProjector(director, balance,
            Darkest.Data.SkillsConfig.Parse(Read("skills.json")), new Darkest.Gameplay.Sim.Skill.SkillRuntimeState());
        return new DirectorHandle { Core = director, Projector = projector };
    }
}