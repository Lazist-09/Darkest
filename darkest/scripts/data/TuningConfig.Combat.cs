// 🔴 从 TuningConfig.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.2 的『②.Combat / ③.Expedition』）
//    本文件 = 战斗域校验（ValidateCombat：物理/精神减伤、命中、暴击、士气、虚弱、死门等）
//    🔴 **只搬家、零行为**：校验**顺序不变**（主文件的 Validate 在同一位置依次调用这两支 ✓）
//    前置检查已做：两块的局部变量**互不越界**（实测 9 + 4 个全部自足 ✓）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

public sealed partial record TuningConfig
{
    private static void ValidateCombat(TuningConfig t, string rawJson)
    {
        if (t.PhysicalMitigation is null || t.PhysicalMitigation.Divisor <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: physical_mitigation.divisor 缺失或 ≤ 0 —— 物理减免除数**必须**来自数据（数字外置纪律）。");
        }

        // 🔴 眩晕累积（数字外置，P29）：成功施加后累积 +N，抗性上限 100 ⇒ 要求 0..100 ✓
        if (t.Stun.BuildupOnApply is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: stun.buildup_on_apply = {t.Stun.BuildupOnApply} 越界（须 0..100）" +
                " —— 它**必须**来自数据（原先硬写在 `EffectsStep` 里是 +50）✓");
        }

        // 🔴 目睹暴击牵连概率（数字外置，P29）：0..100（原先硬写在 `DamagePipeline` 里是 50.0）✓
        if (t.Morale.WitnessCritShockChancePercent is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: morale.witness_crit_shock_chance_percent = {t.Morale.WitnessCritShockChancePercent} 越界（须 0..100）。");
        }

        // 🔴 连续未命中补偿（数字外置，P29）：每多一次未命中 +N 隐藏命中，0..100 ✓
        if (t.ConsecutiveMiss is null || t.ConsecutiveMiss.HitBonusPerMiss is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: consecutive_miss.hit_bonus_per_miss 缺失或越界（须 0..100）" +
                " —— 它**必须**来自数据（原先硬写在 `HitStep` 里是 4）✓");
        }

        // 🔴 暴击治疗概率（数字外置，P29）：两个都必须在 0..100（原先硬写在 `SkillExecutor` 里是 12 / 5）✓
        if (t.HealCrit is null
            || t.HealCrit.SingleTargetPercent is < 0 or > 100
            || t.HealCrit.MultiTargetPercent is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: heal_crit 缺失或越界（single/multi 须 0..100）—— 它们**必须**来自数据。");
        }

        if (t.DamageFloor < 1)
        {
            throw new InvalidDataException($"{ResPath}: damage_floor 必须 ≥ 1（combat_math §2.3）。");
        }

        if (t.HitClamp.Min >= t.HitClamp.Max || t.HitClamp.Min < 0 || t.HitClamp.Max > 100)
        {
            throw new InvalidDataException($"{ResPath}: hit_clamp 取值非法（应 [55,100]）。");
        }

        if (t.CritMultiplier <= 0)
        {
            throw new InvalidDataException($"{ResPath}: crit_multiplier 必须 > 0。");
        }

        if (t.SpeedFloat.Percent is < 0 or > 100)
        {
            throw new InvalidDataException($"{ResPath}: speed_float.percent 越界 [0,100]。");
        }

        if (t.RetreatFormula is null
            || t.RetreatFormula.BasePercent is < 0 or > 100
            || t.RetreatFormula.ClampMin < 0 || t.RetreatFormula.ClampMin > 100
            || t.RetreatFormula.ClampMax < t.RetreatFormula.ClampMin || t.RetreatFormula.ClampMax > 100)
        {
            throw new InvalidDataException($"{ResPath}: retreat_formula 取值非法（O-11/#169）。");
        }

        // 🔴 D-1（M8 地牢层）：回头光照代价 —— 给值就必须 > 0（0/负数会被 `ExpeditionFlow` 当"关闭"静默忽略，
        //    那是**数据写错**而不是"不想开"，故在加载期直接拒绝；要关就整段不写 ✓
        if (t.DungeonLayer?.RevisitLightCost is { } rlc && rlc <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: dungeon_layer.revisit_light_cost = {rlc} 必须 > 0 —— 要「关闭回头代价」请**删掉该键**，" +
                "不要写 0 或负数（`ExpeditionFlow.EnableTileWalk` 会把 ≤0 当未配置静默忽略；D-1）。");
        }

        // 🔴 D-2（M8 地牢层）：回头威胁档位 —— **数组顺序即语义**（暗 → 亮），故必须在此 fail-fast：        //    `RevisitSpawner` 只做"取首个满足 light ≤ max_light 的档"，**不排序**；若数据乱序，行为会悄悄错但不报错。
        //    ① 档数为 0 视为"未配置"（合法，等价于不刷回头威胁）；② max_light 严格递增且 ∈ [0,100]；
        //    ③ percent ∈ (0,100]；④ 两个权重 ≥ 0 且**不同时为 0**（否则触发后无法选种类 ⇒ 白掷一次）。
        if (t.DungeonLayer?.Revisit is { } rev)
        {
            if (rev.Tiers is null || rev.Tiers.Count == 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.revisit.tiers 为空 —— 要「不刷回头威胁」请**整段删掉** `revisit`，不要写空数组（P21 同族纪律）。");
            }

            int lastMaxLight = int.MinValue;
            foreach (RevisitThreatTier tier in rev.Tiers)
            {
                if (tier.MaxLight is < 0 or > 100)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: dungeon_layer.revisit.tiers[].max_light = {tier.MaxLight} 越界（须 0..100；D-2）。");
                }

                if (tier.MaxLight <= lastMaxLight)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: dungeon_layer.revisit.tiers **必须按暗 → 亮排列**（max_light 严格递增；" +
                        $"发现 {lastMaxLight} → {tier.MaxLight}）—— RevisitSpawner 取首个命中档，**不排序**（D-2）。");
                }

                if (tier.Percent is <= 0 or > 100)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: dungeon_layer.revisit.tiers[max_light={tier.MaxLight}].percent = {tier.Percent} 越界（须 (0,100]；D-2）。");
                }

                lastMaxLight = tier.MaxLight;
            }

            if (rev.BattleWeight < 0 || rev.TrapWeight < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.revisit 的 battle_weight / trap_weight 必须 ≥ 0（D-2）。");
            }

            if (rev.BattleWeight == 0 && rev.TrapWeight == 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.revisit 的 battle_weight / trap_weight **不得同时为 0**" +
                    " —— 否则判定触发后无种类可选，会白掷一次骰（D-2）。");
            }

            // 🔴 末档必须覆盖到「满光照」（= 最亮档的上界，本项目 `light.tiers` 里是 100）：
            //    否则**亮着走一趟 ⇒ 一次都不掷** ⇒ D-2 在最常见的情形下静默失效（**我在初版数据里真踩了这个坑** ⚠️）。
            //    这里用 `light.enter_value`（进地牢时的光照 = 实际可能出现的最大值）作为上界判据，不写死 100 ✓
            int lightMax = t.Light?.EnterValue ?? 100;
            if (rev.Tiers[^1].MaxLight < lightMax)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.revisit.tiers 的**末档** `max_light` = {rev.Tiers[^1].MaxLight} " +
                    $"< 满光照 {lightMax} —— 光照高于该值时**一档都不命中 ⇒ 完全不掷骰**，D-2 会在最常见的情形下静默失效。" +
                    "请把末档上界提到 ≥ 满光照（D-2）。");
            }
        }

        // 🔴 D-5（M8 地牢层）：**饥饿**档位 —— 与 D-2 **同构同纪律**（数组顺序即语义：暗 → 亮，取首个命中、不排序）。
        //    ① tiers 为空 ⇒ 视为**数据写错**（要关就整段删 `hunger`，与 D-2 的 P21 同族纪律一致）；
        //    ② max_light 严格递增且 ∈ [0,100]；③ percent ∈ (0,100]；
        //    ④ food_per_hero ≥ 1（0 ⇒ 饥饿=纯白送治疗，必是写错）；⑤ 三个效果数值须为正；
        //    ⑥ 缓冲 ≥ 0；⑦ 🔴 **末档必须覆盖满光照**（否则"亮着走永远不饿" ⇒ 与 D-2 同一个坑）。
        if (t.DungeonLayer?.Hunger is { } hunger)
        {
            if (hunger.Tiers is null || hunger.Tiers.Count == 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.hunger.tiers 为空 —— 要「不启用饥饿」请**整段删掉** `hunger`，不要写空数组（D-5，与 D-2 同纪律）。");
            }

            int lastHungerMaxLight = int.MinValue;
            foreach (HungerTier tier in hunger.Tiers)
            {
                if (tier.MaxLight is < 0 or > 100)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: dungeon_layer.hunger.tiers[].max_light = {tier.MaxLight} 越界（须 0..100；D-5）。");
                }

                if (tier.MaxLight <= lastHungerMaxLight)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: dungeon_layer.hunger.tiers **必须按暗 → 亮排列**（max_light 严格递增；" +
                        $"发现 {lastHungerMaxLight} → {tier.MaxLight}）—— HungerSpawner 取首个命中档，**不排序**（D-5）。");
                }

                if (tier.Percent is <= 0 or > 100)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: dungeon_layer.hunger.tiers[max_light={tier.MaxLight}].percent = {tier.Percent} 越界（须 (0,100]；D-5）。");
                }

                lastHungerMaxLight = tier.MaxLight;
            }

            int hungerLightMax = t.Light?.EnterValue ?? 100;
            if (hunger.Tiers[^1].MaxLight < hungerLightMax)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.hunger.tiers 的**末档** `max_light` = {hunger.Tiers[^1].MaxLight} " +
                    $"< 满光照 {hungerLightMax} —— 光照高于该值时**一档都不命中 ⇒ 永远不会饿**（DD 里满光照同样会饿）。" +
                    "请把末档上界提到 ≥ 满光照（D-5）。");
            }

            if (hunger.FoodPerHero < 1)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.hunger.food_per_hero = {hunger.FoodPerHero} 必须 ≥ 1 —— " +
                    "0 会让「吃」变成**零成本白送治疗**；🔴 也可能是**该键漏配**（本字段刻意无默认值 ⇒ 缺失即 0）。" +
                    "要关就删掉整段 hunger（D-5）。");
            }

            if (hunger.EatHealPercent <= 0 || hunger.StarveHpPercent <= 0 || hunger.StarveMorale < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.hunger 的 eat_heal_percent / starve_hp_percent 必须 > 0、starve_morale 必须 ≥ 0（D-5）；" +
                    "🔴 这几个字段**刻意无默认值** ⇒ 报 0 往往就是 `tuning.json` **漏配了该键**（不是「要用 0」，0 无意义）✓");
            }

            if (hunger.BufferAtStart < 0 || hunger.BufferAfterTrigger < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.hunger 的 buffer_at_start / buffer_after_trigger 必须 ≥ 0（D-5）。");
            }
        }

        // 🔴 D-3（M8 地牢层）：**格视野（GridVision）** —— 揭示三态的**格级**通道。
        //    ① radius ≥ 0（负半径 ⇒ "看不清任何东西"，必是写错；要关就删 `vision`）；
        //    ② radius < 满光照那样的"整图铺开"风险：**此处无法判**（tuning 不知道图尺寸）⇒
        //       关卡级那条上界校验在 `DungeonGridConfig` ⑦（两边各管各的，见 `TuningGridVision` 注释）✓
        //    ③ 🔴 scout_bonus ≥ 0 —— 负加成会让侦察**永远失败**（静默失效，D-2/D-5 同款坑）⚠️
        if (t.DungeonLayer?.Vision is { } vision)
        {
            if (vision.Radius < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.vision.radius = {vision.Radius} 必须 ≥ 0 —— " +
                    "负半径等于「什么都看不清」；要「不启用视野」请**整段删掉** `vision`（D-3）。");
            }

            if (vision.ScoutBonus < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.vision.scout_bonus = {vision.ScoutBonus} 必须 ≥ 0 —— " +
                    "负加成在 base_pct 低时会让侦察**永远失败**（静默失效）；D-3 的判据是「侦察照配置来」（D-3）。");
            }
        }

        // 🔴 D-4（M8 地牢层）：**陷阱撒布** —— `DungeonTileKind.Trap` 的**唯一**产出源。
        //    ① 概率必须 ∈ [0,100]（>100 无意义；<0 会让 `ScatterTraps` 静默不撒）；
        //    ② 🔴 **>100 不是"必然撒"的合法写法**：要必然就在数值上给 100（且 `NextPercent()` 是 [0,100)
        //       ⇒ 100 仍是"几乎必然"）—— 我们**不为此造一个"必然"分支**（那是第二套语义）⚠️
        if (t.DungeonLayer?.Traps is { } traps)
        {
            if (traps.CorridorChancePercent is < 0 or > 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.traps.corridor_chance_percent = {traps.CorridorChancePercent} 越界 [0,100] —— " +
                    "要「不撒陷阱」请**整段删掉** `traps`（不是写 0，虽然 0 也被 `ScatterTraps` 当关闭处理）（D-4）。");
            }
        }

        // 🔴 D-6（M8 地牢层）：**隐藏房** —— `DungeonTileKind.Secret` 的**唯一**产出源（与 D-4 同族）。
        //    ① 概率必须 ∈ [0,100]（同 D-4 口径，不造"必然"分支）；
        //    ② 🔴 **reward_gold 必须 > 0** —— 这是本刀**最关键**的一条：
        //       隐藏房的设计意图就是「给侦察一个**非信息类**回报」。若回报为 0，
        //       玩家花光照侦察只换来"看见一个空房间" ⇒ **理性地永不侦察** ⇒
        //       整条侦察链路（含 D-3 三态 / D-4 拆除）**静默沦为装饰** ⚠️（红線 21 家族）
        //       ⇒ 不设默认值，漏配即 0 ⇒ 当场拒绝（fail-fast，不给静默兜底）。
        if (t.DungeonLayer?.Secrets is { } secrets)
        {
            if (secrets.CorridorChancePercent is < 0 or > 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.secrets.corridor_chance_percent = {secrets.CorridorChancePercent} 越界 [0,100] —— " +
                    "要「不撒隐藏房」请**整段删掉** `secrets`（不是写 0）（D-6）。");
            }

            if (secrets.RewardGold <= 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.secrets.reward_gold = {secrets.RewardGold} 必须 > 0 —— " +
                    "🔴 隐藏房是「给侦察的**非信息类**回报」（`dungeon_layer_design.md §F3d`）：" +
                    "回报为 0 ⇒ 揭示了但没东西 ⇒ 玩家**理性地永不侦察** ⇒ D-3 / D-4 整条链路静默沦为装饰（D-6）⚠️");
            }

            if (secrets.MaxRewardsPerRun < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.secrets.max_rewards_per_run = {secrets.MaxRewardsPerRun} 必须 ≥ 0" +
                    "（0 = 不设上限，是合法结构值）（D-6）。");
            }
        }

        // 🔴 D-7（M8 地牢层）：**探索层 act-out** —— 带折磨者拒绝摸奇物 / 拒绝进食。
        //    ① 阈值必须 ∈ [0,100]（士气刻度）；
        //    ② 🔴🔴 **阈值必须 == `morale.start`** —— 这是本刀**最关键**的一条：
        //       折磨的**挂上点**（士气 == 0）与**解除点**（士气 == `morale.start`）都是 `MoraleLedger` 的硬口径
        //       ⇒ 「士气 < 阈值」只有在**阈值 == `morale.start`** 时才与"带折磨"等价。若策划把阈值调成 30，
        //       就会出现「士气 40 时折磨**实际已挂上**（崩溃过），但探索层判他**不带折磨**」⇒ 规则**自相矛盾** ⚠️
        //       ⇒ 两者必须同值，改一个就改另一个（fail-fast，不给静默分歧）✓
        //    ③ 两条概率必须 ∈ (0,100]：0 = "配了但永远不生效"（静默失效家族）⇒ 要关闭请**整段删掉** `exploration` ✓
        if (t.DungeonLayer?.Exploration is { } actOut)
        {
            if (actOut.MoraleAfflictionThreshold is < 0 or > 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.exploration.morale_affliction_threshold = " +
                    $"{actOut.MoraleAfflictionThreshold} 越界 [0,100]（士气刻度）（D-7）。");
            }

            if (actOut.MoraleAfflictionThreshold != t.Morale.Start)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.exploration.morale_affliction_threshold = " +
                    $"{actOut.MoraleAfflictionThreshold} 必须 == tuning.morale.start = {t.Morale.Start} —— " +
                    "🔴 折磨**只**在士气 0 挂上、**只有**回到 `morale.start` 才解除（`MoraleLedger`）⇒ " +
                    "阈值就是那条解除线；不同值会让「探索层判他不带折磨」与「战斗内他确实带着折磨」**同时成立**（D-7）⚠️");
            }

            if (actOut.CurioRefusePercent is <= 0 or > 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.exploration.curio_refuse_percent = {actOut.CurioRefusePercent} " +
                    "必须 ∈ (0,100] —— 0 等于「配了但永远不生效」；要关闭请**整段删掉** `exploration`（D-7）。");
            }

            if (actOut.EatRefusePercent is <= 0 or > 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: dungeon_layer.exploration.eat_refuse_percent = {actOut.EatRefusePercent} " +
                    "必须 ∈ (0,100]（同上）（D-7）。");
            }
        }
    }
}
