using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Darkest.UI;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 阶段 1+2（UI 完善计划）：让 BattleRoot 显式实现 <see cref="IBattleView"/>。
/// 全部为纯转发 —— 只读投影 → Projector / Director 只读面；命令 → 已有 Director 处理（DoUseSkill / DoRetreat）。
/// 零新增游戏逻辑、零抽取、零写状态。
///
/// RNG 隔离（R1）：<see cref="IntentPreview"/> 一律走宿主侧的 <c>_previewRng</c>（固定种子预览专用），
/// UI 永远拿不到战斗 RNG ⇒ 预览绝不消耗抽数、不破坏确定性。
///
/// 阶段 2 去直读：本文件新增的 7 个只读方法 + 瘦身的 Skill，让 scripts/ui 不再出现
/// BattleDirector / BattleProjector 对象与内核板引用（详见计划 §五，逐文件转换在批 2/批 3）。
/// </summary>
public sealed partial class BattleRoot
{
    // ── 只读投影：转发到 Projector（零随机、零写） ──
    DecisionSupportProjection IBattleView.Support()
        => Projector.Support();

    IReadOnlyList<UnitProjection> IBattleView.Units(bool player)
        => Projector.Units(player);

    SkillProjection IBattleView.Skill(string skillId, UnitId caster, IReadOnlySet<string>? carried)
        => Projector.Skill(skillId, caster, Director.Player, Director.Enemy, carried);

    int IBattleView.HitRateFor(int targetDodge, int hitMod)
        => Projector.HitRateFor(targetDodge, hitMod);

    DisplaceResult IBattleView.DisplacementPreview(UnitId mover, int fromPos, int toPos, int distance, FormationBoard board)
        => Projector.DisplacementPreview(mover, fromPos, toPos, distance, board);

    // ── 悬停 / 详情投影（阶段 1 新接入） ──
    UnitDetail IBattleView.Detail(bool player, int slot)
        => Projector.Detail(player, slot);

    TargetEstimate IBattleView.Estimate(string skillId, UnitId caster, int targetSlot, bool targetIsPlayer)
        => Projector.Estimate(skillId, caster, targetSlot, targetIsPlayer);

    // 默认值在接口声明处（IBattleView.cs）定义；显式实现不得重复，否则触发 CS1066
    IntentProjection IBattleView.IntentPreview(UnitId actor, bool enabled)
        => Projector.IntentPreview(actor, _previewRng, enabled);

    // ── 阶段 2 去直读：消灭 scripts/ui 对 Director/Projector 对象与板的直读 ──
    IReadOnlyList<string> IBattleView.Buffs(UnitId unit)
        => Director.Buffs.Buffs(unit);

    IReadOnlyList<BattleEvent> IBattleView.LogEvents()
        => Director.Log.Events;

    int IBattleView.ActiveActorSlot()
        => Director.Player.UnitAtPosition(ActiveActor) ?? -1;

    IReadOnlyList<int> IBattleView.SkillTargetCandidates(string skillId, UnitId actor)
        => SkillTargetResolver.Resolve(_skills.Get(skillId), actor, Director.Player, Director.Enemy, Director.Buffs);

    bool IBattleView.IsPlayerUnit(UnitId unit)
        => Director.Player.UnitAtPosition(unit) is { };

    bool IBattleView.SwappedThisRound => Director.SwappedThisRound;

    int IBattleView.SupportCostReinforce => Director.SupportCostReinforce;

    // ── 命令门面：转发到已有处理（Director 侧命令） ──
    void IBattleView.UseSkill(UnitId actor, string skillId)
        => DoUseSkill(actor, skillId);

    void IBattleView.Retreat()
        => DoRetreat();

    // ── 阶段 2：支援包命令委托（消灭 Motion.cs 对 Director.TryUseSupportPackForSp 的直读；命令仍走宿主⇒Director） ──
    public int SupportPoints => Director.SupportPoints;

    public bool TryUseSupportPackForSp() => Director.TryUseSupportPackForSp();
}
