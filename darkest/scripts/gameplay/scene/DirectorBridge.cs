using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 内核装配桥（Godot 表现层专用）：res://data JSON → BattleDirector + BattleProjector。
/// 只做薄翻译；内核逻辑与数据层零 Godot 引用。
/// </summary>
public static class DirectorBridge
{
    public sealed class DirectorHandle
    {
        public BattleDirector Core { get; init; } = null!;
        public BattleProjector Projector { get; init; } = null!;
        public SkillsConfig Skills { get; init; } = null!;
    }

    /// <summary>从 res://data 读 JSON 并构建导演（含只读投影与士气初始化）。</summary>
    public static DirectorHandle BuildFromRes(Node host)
    {
        _ = host;
        string Read(string name) => FileAccess.GetFileAsString($"res://data/{name}");

        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(Read("tuning.json")));
        var director = new BattleDirector(
            FormationConfig.Parse(Read("formation.json")),
            UnitsConfig.Parse(Read("units.json")),
            SkillsConfig.Parse(Read("skills.json")),
            balance,
            MoraleEventsConfig.Parse(Read("morale_events.json")),
            BuffDefsConfig.Parse(Read("buff_defs.json")),
            EnemyAiConfig.Parse(Read("enemy_ai.json")),
            new CombatLog());

        var projector = new BattleProjector(director, balance,
            SkillsConfig.Parse(Read("skills.json")), new SkillRuntimeState());

        return new DirectorHandle { Core = director, Projector = projector, Skills = SkillsConfig.Parse(Read("skills.json")) };
    }
}