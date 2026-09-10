using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.UI;

/// <summary>
/// UI 唯一依赖面（blueprint §4 B5 / §12 风险 3 / T-M5-09）：只读投影 + 命令门面。
/// UI/表现层不得触内核结算 API、不得持有可写引用；一切经本网关单向转交 BattleRoot→BattleDirector。
/// </summary>
public interface IBattleView
{
    /// <summary>9 条必显决策支持投影（行动序列/撤退数字/占用等，无抽取）。</summary>
    DecisionSupportProjection Support();

    /// <summary>单位只读投影（我方/敌方，槽升序，含空槽编号）。</summary>
    IReadOnlyList<UnitProjection> Units(bool player);

    /// <summary>技能可用性（灰显 + tooltip 原因）。</summary>
    SkillProjection Skill(string skillId, UnitId caster, FormationBoard ally, FormationBoard target, IReadOnlySet<string>? carried);

    /// <summary>命中率（必显 #4，纯公式）。</summary>
    int HitRateFor(int targetDodge, int hitMod);

    /// <summary>位移预览（必显 #6）：内核 dry-run，与结算同源。</summary>
    DisplaceResult DisplacementPreview(UnitId mover, int fromPos, int toPos, int distance, FormationBoard board);

    /// <summary>命令门面：发技能命令。</summary>
    void UseSkill(UnitId actor, string skillId);

    /// <summary>命令门面：发起撤退（确认后不可撤销）。</summary>
    void Retreat();
}