using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.UI;

/// <summary>
/// UI 唯一依赖面（blueprint §4 B5 / §12 风险 3 / T-M5-09）：只读投影 + 命令门面。
/// UI/表现层不得触内核结算 API、不得持有可写引用；一切经本网关单向转交 BattleRoot→BattleDirector。
///
/// 阶段 2 去直读：新增 7 个只读方法 + 瘦身的 Skill（去掉 board 参数），目标是消灭 scripts/ui 里对
/// BattleDirector / BattleProjector 对象与内核板的任何直读（见 UI_BINDING_MAP.md 与计划 §五）。
/// </summary>
public interface IBattleView
{
    /// <summary>9 条必显决策支持投影（行动序列/撤退数字/占用等，无抽取）。</summary>
    DecisionSupportProjection Support();

    /// <summary>单位只读投影（我方/敌方，槽升序，含空槽编号）。</summary>
    IReadOnlyList<UnitProjection> Units(bool player);

    /// <summary>技能可用性（灰显 + tooltip 原因）。🔴 阶段 2 去直读：去掉 board 参数——board 由实现侧从 Director 取，UI 不再接触内核板。</summary>
    SkillProjection Skill(string skillId, UnitId caster, IReadOnlySet<string>? carried);

    /// <summary>命中率（必显 #4，纯公式）。</summary>
    int HitRateFor(int targetDodge, int hitMod);

    /// <summary>位移预览（必显 #6）：内核 dry-run，与结算同源。</summary>
    DisplaceResult DisplacementPreview(UnitId mover, int fromPos, int toPos, int distance, FormationBoard board);

    /// <summary>命令门面：发技能命令。</summary>
    void UseSkill(UnitId actor, string skillId);

    /// <summary>命令门面：发起撤退（确认后不可撤销）。</summary>
    void Retreat();

    /// <summary>单位详情（悬停/详情面板用）。</summary>
    UnitDetail Detail(bool player, int slot);

    /// <summary>目标伤害估算（悬停态）。</summary>
    TargetEstimate Estimate(string skillId, UnitId caster, int targetSlot, bool targetIsPlayer);

    /// <summary>敌方意图预览（悬停态）：默认不显示；RNG 由实现侧固定种子隔离，UI 不持有 RNG。</summary>
    IntentProjection IntentPreview(UnitId actor, bool enabled = false);

    // ── 阶段 2 去直读：以下 7 个只读方法，消灭 scripts/ui 对 BattleDirector/BattleProjector 对象与板的直读 ──

    /// <summary>单位 buff 列表（字符串 id；源 BattleProjector.cs:68 即规划由 IBattleView 经 IBuffLedger 提供）。</summary>
    IReadOnlyList<string> Buffs(UnitId unit);

    /// <summary>战斗日志事件流（Core.Events 不可变只读 DTO，非结算 API；R1：只读暴露，UI 不持有可写引用）。</summary>
    IReadOnlyList<BattleEvent> LogEvents();

    /// <summary>当前行动者所在槽位（替代 Director.Player.UnitAtPosition(ActiveActor)）。</summary>
    int ActiveActorSlot();

    /// <summary>技能候选目标槽位（封装 SkillTargetResolver.Resolve(... Director.Player, Director.Enemy, Director.Buffs)；替代 BattleDirector d 参数）。</summary>
    IReadOnlyList<int> SkillTargetCandidates(string skillId, UnitId actor);

    /// <summary>某单位是否属于我方（顺序条/音效/动效按阵营分岔；替代 d.Player.UnitAtPosition）。</summary>
    bool IsPlayerUnit(UnitId unit);

    /// <summary>本回合是否已换位（增援按钮可用性判据）。</summary>
    bool SwappedThisRound { get; }

    /// <summary>增援所需支援点（增援按钮可用性判据）。</summary>
    int SupportCostReinforce { get; }
}
