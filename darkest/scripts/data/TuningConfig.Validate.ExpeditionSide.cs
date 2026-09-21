// 🔴 从 TuningConfig.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.2）
//    ⚠️ **如实说明**：本片的边界是【**行数边界**】（切在 L495/496，因为那一刀两侧的**局部变量互不越界** ✓），
//       我随后按**主要内容**命名 —— 现在文件名是 `.Validate.ExpeditionSide`（**Side = 不假装纯域** ✓）：
//       主体 = 远征/光照/侦察/背包/扎营/口粮 ✓，**也含少数战斗项**（`t.Morale` / `t.MentalReduction` 等 ✓）
//    🔴 若要**严格按域**重切 ⇒ 见 `reports/tuningconfig_domain_recheck.md`（我列了精确的跨域清单，等你定 ✓）
//    🔴 只搬家、零行为：校验**顺序不变**（主文件的 `Validate` 在同一位置依次调用两支 ✓）

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
    }
}

