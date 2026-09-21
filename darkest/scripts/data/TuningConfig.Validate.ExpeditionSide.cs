// 🔴 从 TuningConfig.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.2 的 **③.Expedition**）
//    ✅ **按域重切已完成**（2026-09-21）：本片现在 = **远征 + 地牢层**（`retreat_formula` · `dungeon_layer` 的
//       回头代价/hunger/vision/traps/secrets/exploration · 光照/侦察/背包/扎营/口粮）✓
//       片首的**小 record 横切校验**（`Morale`/`Weak`/`VirtueRate`/`DeathsDoor` 等）保留在此（它们**跨域共用** ✓）
//    🔴 只搬家、零行为（调用顺序未变）· 复核用 `tools/dsh/audit_split_integrity.py` ✓
//    📄 边界与清单：`reports/tuningconfig_domain_recheck.md` ✓

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

public sealed partial record TuningConfig
{
    private static void ValidateExpedition(TuningConfig t, string rawJson)
    {
        IReadOnlyList<TuningDifficultyTier>? tiers = t.Expedition.DifficultyTiers;
        if (tiers is null || tiers.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: expedition.difficulty_tiers 必填（P20 ⑭）。");
        }

        int expectedFrom = 1;
        double lastMultiplier = 0;
        int lastResistPp = 0;
        foreach (TuningDifficultyTier tier in tiers.OrderBy(x => x.BattleFrom).ToArray())
        {
            if (tier.BattleFrom != expectedFrom)
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers 必须从第 1 场起**无缝隙无重叠**（期望 from={expectedFrom}，实际 {tier.BattleFrom}；P20 ⑭）。");
            }

            if (tier.BattleTo < tier.BattleFrom)
            {
                throw new InvalidDataException($"{ResPath}: difficulty_tiers battle_to < battle_from（P20 ⑭）。");
            }

            if (tier.Multiplier < lastMultiplier)
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers 乘数必须**单调不减**（{lastMultiplier} → {tier.Multiplier}；P20 ⑭）。");
            }

            lastMultiplier = tier.Multiplier;
            if (tier.StunResistPp < 0)
            {
                throw new InvalidDataException($"{ResPath}: difficulty_tiers stun_resist_pp 必须 ≥ 0（P20 ⑭ / #253）。");
            }

            if (tier.StunResistPp < lastResistPp)
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers stun_resist_pp 必须**单调不减**（{lastResistPp} → {tier.StunResistPp}；P20 ⑭ / #253）。");
            }

            lastResistPp = tier.StunResistPp;
            if (tier.Target is not null && tier.Target is not ("enemy_hp" or "enemy_resist"))
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers target 只能是 enemy_hp / enemy_resist（或 null=待裁定；P20 ⑭）。");
            }

            expectedFrom = tier.BattleTo + 1;
        }

        if (expectedFrom != t.Expedition.NBattles + 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: difficulty_tiers 必须**恰好覆盖第 1~{t.Expedition.NBattles} 场**（实际覆盖到 {expectedFrom - 1}；P20 ⑭）。");
        }

        // 🔴 P21（M7.5 地牢层，加载级 fail-fast）：① 五档覆盖 0~100 无缝隙无重叠 + 边界取档；③ 效果表**不得出现 HP 字段**；
        // ④ 掉落概率 ∈ [0,1] 且随变暗**单调不减**；⑤ 侦察 base ≥ 0；⑥ 背包 slot_cap = 12（**不许调到 15 来"修好"取舍**）。
        if (t.Light is null || t.Scouting is null || t.Inventory is null)
        {
            throw new InvalidDataException($"{ResPath}: light / scouting / inventory 三段必填（P21）。");
        }

        if (t.Light.Tiers.Count != 5)
        {
            throw new InvalidDataException($"{ResPath}: light.tiers 必须**五档**（P21 ①）。");
        }

        int expectMax = 100;
        string[] order = { "radiant", "dim", "shadowy", "dark", "black" };
        for (int i = 0; i < order.Length; i++)
        {
            TuningLightTier tier = t.Light.Tiers.FirstOrDefault(x => x.Id == order[i])
                ?? throw new InvalidDataException($"{ResPath}: light.tiers 缺档 \"{order[i]}\"（P21 ①）。");
            if (tier.Max != expectMax)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.tiers \"{tier.Id}\" max 应为 {expectMax}（实际 {tier.Max}；P21 ① 无缝隙无重叠）。");
            }

            if (tier.Min < 0 || tier.Min > tier.Max + (tier.Id == "black" ? 0 : 1))
            {
                throw new InvalidDataException($"{ResPath}: light.tiers \"{tier.Id}\" 区间非法（P21 ①）。");
            }

            expectMax = tier.Min - 1;
        }

        if (expectMax != -1)
        {
            throw new InvalidDataException($"{ResPath}: light.tiers 必须覆盖到 0（P21 ①）。");
        }

        // 🔴 P21 ④（#276 改）：掉落 = **类型 + 份数**；校验对象 = **柴火份数按档单调不减**（0/0/1/1/2）
        // ＋ 🔴 **下限：`shadowy` 起必须 ≥ 1 柴火**（否则"摸黑换续航"的链又会断：补给再多也换不来扎营）。
        foreach ((string id, TuningLootSpec spec) in t.Light.Loot)
        {
            if (spec.Firewood < 0 || spec.Food < 0)
            {
                throw new InvalidDataException($"{ResPath}: light.loot[\"{id}\"] 份数必须 ≥ 0（P21 ④）。");
            }
        }

        int lastFirewood = -1;
        bool darkZone = false;
        foreach (string id in order)
        {
            if (!t.Light.Loot.TryGetValue(id, out TuningLootSpec? spec) || spec is null)
            {
                throw new InvalidDataException($"{ResPath}: light.loot 缺档 \"{id}\"（P21 ④）。");
            }

            if (spec.Firewood < lastFirewood)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.loot 的**柴火份数**必须按档单调不减（{id}；P21 ④ / #276）。");
            }

            if (id == "shadowy")
            {
                darkZone = true;
            }

            if (darkZone && spec.Firewood < 1)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.loot 从 shadowy 起**必须 ≥ 1 柴火**（{id}；P21 ④ / #276：否则摸黑换不来续航）。");
            }

            lastFirewood = spec.Firewood;
            if (!t.Light.Effects.ContainsKey(id))
            {
                throw new InvalidDataException($"{ResPath}: light.effects 缺档 \"{id}\"（P21 ③）。");
            }
        }

        // ③ 效果表不得出现 HP 字段（用原始 JSON 文本兜底检查，防将来加字段）
        ValidateLightHasNoHpFields(rawJson);

        if (t.Scouting.BasePct < 0 || t.Scouting.Reveal != "next_node_type_only")
        {
            throw new InvalidDataException($"{ResPath}: scouting 必须 base_pct ≥ 0 且 reveal=next_node_type_only（P21 ⑤）。");
        }

        if (t.Inventory.SlotCap != 12)
        {
            throw new InvalidDataException(
                $"{ResPath}: inventory.slot_cap 必须 = 12（P21 ⑥：**不许调到 15 来「修好」设计取舍**）。");
        }

        // P21 ⑥/⑬（v1.04）：推荐配置必须 ≤ slot_cap 且**恰满**（作默认）；包满策略必须是"选择丢弃"（禁止静默丢）
        if (t.Inventory.RecommendedLoadout is null)
        {
            throw new InvalidDataException($"{ResPath}: inventory.recommended_loadout 必填（P21 ⑥「整备」默认配置）。");
        }

        int recommended = t.Inventory.RecommendedLoadout.Values.Sum();
        if (recommended > t.Inventory.SlotCap)
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout 合计 {recommended} > slot_cap {t.Inventory.SlotCap}（P21 ⑥）。");
        }

        // 🔴 P21 ⑥（#275 改）：推荐配置**必须留 ≥1 格余量** —— 否则"摸黑搏到的补给"必须先丢东西，
        // **收益端在入口就被堵住**（推荐配置 = 1/9/1 = 11 格，留 1 格）。
        if (t.Inventory.SlotCap - recommended < 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout 必须留 ≥1 格余量（{recommended}/{t.Inventory.SlotCap}；P21 ⑥ / #275）。");
        }

        // 🔴 P20 ② / P21 ⑥（#274）：推荐配置的柴火必须**可实现**（≤ 起手柴火）——
        // 起手柴火降到 1 后，`firewood:2` 就是不可实现的配置。
        if (t.Inventory.RecommendedLoadout.TryGetValue("firewood", out int recFirewood)
            && recFirewood > t.Resources.Firewood)
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout.firewood({recFirewood}) 必须 ≤ 起手 firewood({t.Resources.Firewood})（P20 ② / #274）。");
        }

        if (t.Inventory.FullPolicy != "choose_what_to_discard")
        {
            throw new InvalidDataException(
                $"{ResPath}: inventory.full_policy 必须 = choose_what_to_discard（P21 ⑬：**禁止静默丢弃**，否则光照计收益端闭环会漏）。");
        }

        // 🔴 P21 ⑭（#265 命名裁定）：推荐配置**必须含 `support_crate`（支援箱）** —— 缺它 ⇒ 默认配置下
        // SP 完全不恢复 ⇒ **支援位废**（玩家看不见的陷阱）。`support_crate`（while_carried，每回合 +1 SP，必需）
        // 与 `support_pack`（一次性 +2 SP，可选）是**两个不同 id**，不得混用。
        if (!t.Inventory.RecommendedLoadout.ContainsKey("support_crate"))
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout **必须包含 support_crate（支援箱）**（P21 ⑭ / #265：缺它 ⇒ 默认配置下 SP 完全不恢复）。");
        }

        // 🔴 P20（M7 远征层）：①②③ —— 键齐备与值域，**加载级 fail-fast**
        if (t.Expedition is null || t.Resources is null || t.Camp is null)
        {
            throw new InvalidDataException($"{ResPath}: expedition / resources / camp 三段必填（P20）。");
        }

        if (t.Expedition.NBattles < 1)
        {
            throw new InvalidDataException($"{ResPath}: expedition.n_battles 必须 ≥ 1（P20 ①）。");
        }

        // 🔴 P20 ①（#273）：完成目标 —— `battle_goal ≥ 1` 且 `≤ n_battles`（"打赢 ≥ N 场"是完成的一部分）
        if (t.Expedition.BattleGoal < 1 || t.Expedition.BattleGoal > t.Expedition.NBattles)
        {
            throw new InvalidDataException(
                $"{ResPath}: expedition.battle_goal 必须 ∈ [1, n_battles={t.Expedition.NBattles}]（P20 ① / #273）。");
        }

        if (t.Expedition.AmbushChance is < 0 or > 1 || t.Camp.AmbushChance is < 0 or > 1)
        {
            throw new InvalidDataException($"{ResPath}: ambush_chance 必须 ∈ [0,1]（P20 ①）。");
        }

        if (t.Expedition.RetreatPenalty.NoDeath < 0 || t.Expedition.RetreatPenalty.WithDeath < t.Expedition.RetreatPenalty.NoDeath)
        {
            throw new InvalidDataException($"{ResPath}: retreat_penalty 必须 ≥0 且 with_death ≥ no_death（P20 ①）。");
        }

        if (t.Resources.Firewood < 0 || t.Resources.Food < 0)
        {
            throw new InvalidDataException($"{ResPath}: resources.firewood / food 必须 ≥ 0（P20 ②）。");
        }

        TuningFoodTiers ft = t.Camp.FoodTiers;
        if (!(ft.Starve < ft.Half && ft.Half < ft.Full && ft.Full < ft.Feast))
        {
            throw new InvalidDataException($"{ResPath}: camp.food_tiers 四档必须单调 0 < half < full < feast（P20 ③）。");
        }

        if (t.Camp.RespiteBase < 1 || t.Camp.PepTalkBattles < 1)
        {
            throw new InvalidDataException($"{ResPath}: camp.respite_base ≥ 1 且 pep_talk_battles ≥ 1（P20 ③）。");
        }

        // ⑥ 撤退对账：morale_events 两档必须与 tuning.retreat 一致、且 scope = survivors
        if (t.Retreat.Scope != "survivors"
            || t.Retreat.SuccessMorale != -t.Expedition.RetreatPenalty.NoDeath
            || t.Retreat.WithDeathMorale != -t.Expedition.RetreatPenalty.WithDeath)
        {
            throw new InvalidDataException(
                $"{ResPath}: retreat 与 retreat_penalty 对账失败（P20 ⑥）：scope=survivors、" +
                $"success_morale=-no_death、with_death_morale=-with_death。");
        }

        if (t.Morale.Min >= t.Morale.Max)
        {
            throw new InvalidDataException($"{ResPath}: morale.min 必须 < morale.max。");
        }

        // 🔴 P16（#196/#198 + v0.68）：`m_value` 必须与公式一致，**写错即启动报错**。
        //    M = ceil(敌方满编总HP ÷ (实测 D × safety_factor))；护栏 M ≥ 3。
        TuningOvertimeReinforcement o = t.OvertimeReinforcement;
        if (o.MValue is not { } mValue)
        {
            throw new InvalidDataException($"{ResPath}: overtime_reinforcement.m_value 必填（不得为 null；P16）。");
        }

        if (o.MeasuredD <= 0 || o.EnemyFullHp <= 0 || o.SafetyFactor <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: overtime_reinforcement 非法（measured_d / enemy_full_hp / safety_factor 必须 > 0）" +
                " —— 🔴 这几项**必须来自数据**，不许靠 C# 默认值兜底（数字外置纪律）。");
        }

        if (o.WaveIntervalRounds <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: overtime_reinforcement.wave_interval_rounds 必须 > 0（数字外置：data 里没有它就算缺键）。");
        }

        int expectedM = Math.Max(3, (int)Math.Ceiling(o.EnemyFullHp / (o.MeasuredD * o.SafetyFactor)));
        if (mValue != expectedM)
        {
            throw new InvalidDataException(
                $"{ResPath}: m_value={mValue} 与公式不符（P16）：ceil({o.EnemyFullHp} ÷ ({o.MeasuredD} × {o.SafetyFactor})) = {expectedM}。" +
                "改数值后必须重算 m_value（或同步 measured_d / enemy_full_hp）。");
        }

        if (t.MentalReduction.CapPercent is <= 0 or > 100 || t.MentalReduction.ResilienceDivisor <= 0)
        {
            throw new InvalidDataException($"{ResPath}: mental_reduction 取值非法。");
        }

        // 🔴 物理减免除数（数字外置，用户 2026-09-14）：**必填且 > 0** ⇒ 不许回落到代码里的默认值 ✓

        // ═══════════════════════════════════════════════════════════════════════════════
        // 🆕 **按域重切（架构 §1.2 的 ③.Expedition）**：以下整段由 `.CombatSide` 搬来（原 `ValidateCombat` L79-334）
        //    · 内容 = `retreat_formula` + `dungeon_layer`（回头代价/hunger/vision/traps/secrets/exploration）✓
        //    · 🔴 **只搬家、零行为**：`Validate` 的调用顺序**未变**（先 Expedition 后 Combat ✓）⇒
        //      唯一可能的可观察差异 =「同时写坏两类数据时报哪条错」，由全量用例兜底 ✓
        // ═══════════════════════════════════════════════════════════════════════════════
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

