using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
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

        /// <summary>🔴 片 3：把 `BalanceTable` 也带出来（宿主在**场景内**给流程的 director 造 projector 时需要它）✓</summary>
        public BalanceTable Balance { get; init; } = null!;
        public SkillsConfig Skills { get; init; } = null!;

        /// <summary>M7.5：远征场景复用同一桥（避免第二套 res:// 读取路径）。</summary>
        public TuningConfig Tuning { get; init; } = null!;

        public ExpeditionNodesConfig Nodes { get; init; } = null!;
    }

    /// <summary>
    /// 从 res://data 读 JSON 并构建导演（含只读投影与士气初始化）。
    /// M8.0 ①(c)（`#286`）：可传入**名册选出的出征 6 人** —— 阵型模板只提供槽位布局与敌方编成；
    /// 传入时按名册套阵型，并把 **等级成长投影**（(b)）作用到我方单位。
    /// 🔴 `BattleDirector` 仍**单场纯**（不读名册、不持 Hamlet；名册由组合根读、以快照传入）。
    /// 🔴 H-1（2026-09-27）· **2026-09-30 架构裁定①（取「（甲）直读」）**：护甲阶**也走这条快照通道** ——
    ///   `armourTierBySlot` 由组合根从 **`HeroGearState`** 取（`ExpeditionContext.Gear`）⇒ 桥层不读名册、不读容器 ✓
    /// </summary>
    public static DirectorHandle BuildFromRes(Node host,
        System.Collections.Generic.IReadOnlyList<HeroConfig>? sortie = null,
        RosterLevelGrowth? growth = null,
        System.Collections.Generic.IReadOnlyList<int>? openingMoraleBySlot = null,
        System.Collections.Generic.IReadOnlyList<DiseasePenalty>? diseasePenaltyBySlot = null,
        System.Collections.Generic.IReadOnlyList<Darkest.Gameplay.Sim.Run.TraitEffects>? traitEffectsBySlot = null,
        System.Collections.Generic.IReadOnlyList<int>? armourTierBySlot = null)
    {
        _ = host;
        string Read(string name) => FileAccess.GetFileAsString($"res://data/{name}");

        TuningConfig tuning = TuningConfig.Parse(Read("tuning.json"));
        BalanceTable balance = BalanceTable.FromTuning(tuning);
        UnitsConfig unitsCfg = UnitsConfig.Parse(Read("units.json"));

        // 🔴 **R22（A1）**：buff 原语层**加载即校验**（这是本表**唯一的生产消费点**，不是空转的读 ✓）——
        //   `buff_primitives.json`（参考项目 1801 条）落地后，M2 清单里每个名字都必须真是上游的 `stat_type`；
        //   实测踩过 3 个上游不存在的名字（红线 21："写了但没接上"）⇒ 从此**加载时就报** ✓
        BuffPrimitiveTranslation.ValidateAgainst(BuffPrimitivesConfig.Parse(Read("buff_primitives.json")));
        // F1（#190）：我方原型集合由 units.json 数据派生 → 新增角色零代码改动
        SkillsConfig skillsCfg = SkillsConfig.Parse(Read("skills.json"), unitsCfg.PlayerArchetypes);

        // 🔴 阵型模板（(c)：它不再决定"谁出征"，只决定"怎么站"）
        FormationConfig formation = FormationConfig.Parse(Read("formation.json"));
        if (sortie is not null && sortie.Count > 0)
        {
            var archetypes = new string[sortie.Count];
            for (int i = 0; i < sortie.Count; i++)
            {
                archetypes[i] = sortie[i].Archetype;
            }

            formation = FormationSortie.WithPlayerSortie(formation, archetypes);
        }

        var director = new BattleDirector(
            formation,
            unitsCfg,
            skillsCfg,
            balance,
            MoraleEventsConfig.Parse(Read("morale_events.json")),
            BuffDefsConfig.Parse(Read("buff_defs.json")),
            EnemyAiConfig.Parse(Read("enemy_ai.json")),
            new CombatLog());

        // 🔴 (b)：等级成长投影（**运行时投影、不改基准数据**，P4 同源）——按槽位顺序与名册顺序一一对应
        // 🔴 2026-09-30 架构裁定① ②：外层门**只判 `sortie`** —— 理由：**Gear 投影不该被 growth 绑架**
        //    （无成长档时也要有阶）⇒ 等级成长与护甲阶**各判各的** ✓
        if (sortie is not null)
        {
            UnitRuntime[] board = director.Player.UnitsInSlotOrder().ToArray();

            // 🔴 红线 21（架构裁定① ⑤）：**「未接线」≠「第 0 阶」** —— 空表是合法的第 0 阶，
            //    而"没传数组"是根本没接上 ⇒ 两者必须**打印得不一样**（不静默回 0 阶）✓
            if (armourTierBySlot is null)
            {
                GD.Print("[H-1] 装备阶投影：**未接线**（未传 `armourTierBySlot`）⇒ 护甲阶**不作用**（≠ 第 0 阶；不静默）⚠");
            }
            else if (armourTierBySlot.Count < sortie.Count)
            {
                throw new System.InvalidOperationException(
                    $"[H-1] `armourTierBySlot` 只给了 {armourTierBySlot.Count} 条，出征 {sortie.Count} 人 ⇒ **接线错误**（不静默按第 0 阶补）⚠");
            }

            for (int i = 0; i < sortie.Count && i < board.Length; i++)
            {
                // 🔴 (b)：等级成长（**未传 growth ⇒ 不作用**，与既往行为一致 ✓）
                if (growth is not null)
                {
                    Darkest.Gameplay.Sim.Run.HeroProjection.ApplyLevel(sortie[i], board[i], growth);
                }

                // 🔴 H-1（2026-09-27 · 2026-09-30 架构裁定① 换源）：**装备阶 → 减伤读数**
                //    （护甲阶 ⇒ prot/dodge 按 `armour[]` 逐阶取）· 阶来源 = **`HeroGearState`**（组合根取成数组传参）✓
                //    未接线前 `GearProtOverride/DodgeOverride` 恒 null ⇒ 旧口径；接上后玩家单位按阶 ⇒ **行为变更**（策划已裁）✓
                if (armourTierBySlot is not null)
                {
                    Darkest.Gameplay.Sim.Run.HeroProjection.ApplyGearTier(board[i], armourTierBySlot[i]);
                }

                // 🔴 M8.0 ③（#289 (B)）：特质 → 单位修正（伤害类与士气类各归其道）
                // 🔴 M8.2 / V15：**优先用"当前特质效果"**（来自可变名册）⇒ 清除/固化后立即生效（红线 21）
                if (traitEffectsBySlot is not null && i < traitEffectsBySlot.Count)
                {
                    Darkest.Gameplay.Sim.Run.HeroProjection.ApplyTraitEffects(traitEffectsBySlot[i], board[i]);
                }
                else
                {
                    Darkest.Gameplay.Sim.Run.HeroProjection.ApplyTraits(sortie[i], board[i]);
                }

                // 🔴 M8.2（V14）：**疾病 → 单位**（属性惩罚投影；只记 Roster 不投影 = 红线 21）
                if (diseasePenaltyBySlot is not null && i < diseasePenaltyBySlot.Count)
                {
                    Darkest.Gameplay.Sim.Run.HeroProjection.ApplyDisease(diseasePenaltyBySlot[i], board[i]);
                }

                // 🔴 #287（= #245 的落地）：**本趟开局士气来自名册**（跨趟累积；回城不恢复）
                if (openingMoraleBySlot is not null && i < openingMoraleBySlot.Count)
                {
                    board[i].ApplyOpeningMorale(openingMoraleBySlot[i]);
                }
            }
        }

        var projector = new BattleProjector(director, balance, skillsCfg, new SkillRuntimeState());

        return new DirectorHandle
        {
            Core = director,
            Projector = projector,
            Balance = balance,
            Skills = skillsCfg,
            Tuning = tuning,
            Nodes = ExpeditionNodesConfig.Parse(Read("expedition_nodes.json")),
        };
    }
}
