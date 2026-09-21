using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Darkest.Core.Events;
using Darkest.Core.Rng;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **一条陷阱定值**（`D-4`，2026-09-20）—— DD wiki `Trap` 的地区表。
///
/// **DD 原文实据**（`darkestdungeon.fandom.com/wiki/Trap`）：
/// <list type="bullet">
///   <item><description>踏中 ⇒ **随机一名**队员掉 **% 最大 HP** 的血 + 压力 **恒定 +15** ✓</description></item>
///   <item><description>各地区**额外效果**不同（Ruins 25/28/30% · Weald 0% HP 但 100% Blight · Warrens 10% + Bleed ·
///     Cove 10% + Debuff …）⇒ 本项目**只落 `hp_percent` 与权重**，额外效果按"只登记不结算"处理 ⚠️</description></item>
/// </list>
/// </summary>
public sealed record TrapDef(
    [property: JsonPropertyName("id")] string Id,

    /// <summary>地区（`ruins` / `weald` / `warrens` / `cove`）—— 🔴 **必须是已登记地区**，
    /// 未登记 ⇒ 加载期拒绝（**不静默当成 ruins**：那是"谁替策划选了一个地区"）⚠️</summary>
    [property: JsonPropertyName("region")] string Region,

    /// <summary>踏中时对**随机一人**造成的伤害（**% 其最大 HP**）—— 必须 ∈ (0,100] ✓</summary>
    [property: JsonPropertyName("hp_percent")] double HpPercent,

    /// <summary>同地区内的抽取权重；**权重 0 = 该条不参与抽取**（不是"抽到 0 次"）✓</summary>
    [property: JsonPropertyName("weight")] int Weight,

    /// <summary>人类可读说明（**不参与行为**；`#408` 说明字段纪律）✓</summary>
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>
/// 🔴🔴 **`D-4` 陷阱定值表**（`res://data/trap_defs.json`）—— 「陷阱转真」的数据侧。
///
/// **为什么必须新开一个文件**（而不是塞进 `tuning.json`）：
/// 这是一张**内容表**（逐条陷阱、逐地区），与 `curios.json` / `buff_defs.json` 同族；
/// `tuning.json` 是**数值表**（平衡数字）⇒ 混进去会让"内容 vs 平衡"的边界模糊（本项目的三扫纪律按后者判）✓
///
/// 🔴🔴 **与 `D-2`（`RevisitSpawner.ThreatTrap`）共用一份真值**（`#325` D6「两份真值」禁令）：
/// DD 原文 —— 「Traps **can sometimes be encountered along corridors that have already been explored**」
/// ⇒ "重访刷新的陷阱"与"图上的陷阱"**是同一件事** ⇒ 本类就是**唯一**的陷阱表，
/// `D-2` 若要结算 `ThreatTrap`，**必须**回到这里抽（`Pick`），**不得**自建第二份 ⚠️
/// </summary>
public sealed record TrapDefs(
    [property: JsonPropertyName("traps")] IReadOnlyList<TrapDef> Traps,

    /// <summary>🔴 **未被侦察**的陷阱的闪避基准（%）：按**该队员**的 `Trap Resist` 掷骰（**不是 DODGE**）✓</summary>
    [property: JsonPropertyName("unscouted_dodge_percent")] double UnscoutedDodgePercent,

    /// <summary>🔴 **已被侦察**的陷阱的拆除加成（%）：DD 原文「拆除概率 = **Trap Resist + 40%**」✓</summary>
    [property: JsonPropertyName("disarm_bonus_percent")] double DisarmBonusPercent,

    /// <summary>🔴 压力伤害（**定值**，不是百分比）：DD 原文「**stress will always be +15**」⚠️</summary>
    [property: JsonPropertyName("stress_damage")] int StressDamage,

    /// <summary>拆除成功时**回多少压力**：DD 原文「Disarming a trap renders it harmless and **heals 8 stress**」✓</summary>
    [property: JsonPropertyName("disarm_stress_heal")] int DisarmStressHeal,

    /// <summary>人类可读说明（**不参与行为**）✓</summary>
    [property: JsonPropertyName("note")] string? Note = null)
{
    public const string ResPath = "res://data/trap_defs.json";

    /// <summary>🔴 已登记地区（**唯一的地区白名单**；未登记 ⇒ 加载期拒绝）✓</summary>
    public static readonly IReadOnlyList<string> KnownRegions = new[] { "ruins", "weald", "warrens", "cove" };

    /// <summary>按 id 取一条（取不到 ⇒ null；**不抛** —— 调用方负责判 null 并留痕）✓</summary>
    public TrapDef? Find(string id) => Traps.FirstOrDefault(t => t.Id == id);

    /// <summary>某地区里**权重 > 0** 的条目（`Pick` 的输入面；顺序 = 表内顺序 ⇒ 确定性）✓</summary>
    public IReadOnlyList<TrapDef> CandidatesFor(string region)
        => Traps.Where(t => t.Region == region && t.Weight > 0).ToArray();

    /// <summary>某地区**权重和**（0 ⇒ 该地区永远无陷阱 ⇒ 加载期就该拒）✓</summary>
    public int WeightSumFor(string region) => Traps.Where(t => t.Region == region).Sum(t => t.Weight);

    /// <summary>
    /// 解析 + **加载期逐条校验**（与 `CuriosConfig.Parse` / `BuffDefsConfig.Validate` 同纪律：
    /// 任何一条不满足 ⇒ 抛 `InvalidDataException`，**绝不静默修数据**）✓
    ///
    /// 校验清单：
    /// ① `traps` 不能为空（要关就**整段删 `trap_defs`**，与 `P21`/`D-2`/`D-5` 同族）；
    /// ② `id` 非空且**不重复**（重复 ⇒ `Find` 取第一条 ⇒ 第二条永远拿不到 = 静默死数据）；
    /// ③ `region` 必须在**已登记地区**里（未登记 ⇒ 拒绝，不静默兜底）；
    /// ④ `hp_percent ∈ (0,100]`（0 ⇒ 只有压力、无伤害，必是漏配键；>100 ⇒ 一击必杀，非设计意图）；
    /// ⑤ `weight ≥ 0` 且**每个已登记地区的权重和 > 0**（全 0 ⇒ 该地区永远无陷阱 = **静默失效**）；
    /// ⑥ `unscouted_dodge_percent ∈ [0,100]`；⑦ `disarm_bonus_percent ≥ 0`（负 ⇒ 拆除反而更难）；
    /// ⑧ `stress_damage ≥ 1`（0 ⇒ 压力伤害整条静默失效，DD 原文恒 +15）；
    /// ⑨ `disarm_stress_heal ≥ 0`（DD 是回压，不是加压）✓
    /// </summary>
    /// <param name="requireRegions">🔴 **必须自带权重 > 0 条目的地区**。不给 ⇒ 用
    /// <see cref="KnownRegions"/> 全集（出货数据必须四地区齐备）；测试若要只用一部分地区，
    /// 就**显式列出**它真正覆盖的那些 —— 而不是"把某个地区的权重调成 0 蒙过去"（那是静默失效）⚠️</param>
    public static TrapDefs Parse(string json, IReadOnlyList<string>? requireRegions = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        TrapDefs defs;
        try
        {
            defs = JsonSerializer.Deserialize<TrapDefs>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException($"{ResPath}: 反序列化得到 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 解析失败（{ex.Message}）。", ex);
        }

        // ① 非空
        if (defs.Traps is null || defs.Traps.Count == 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: `traps` 为空 —— 要「不启用陷阱」请**整段删掉** `trap_defs`，不要写空数组（`D-4`，与 `P21`/`D-2`/`D-5` 同纪律）。");
        }

        // ② ~ ⑤ 逐条
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TrapDef t in defs.Traps)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
            {
                throw new InvalidDataException($"{ResPath}: 有条目 `id` 为空 ⇒ 拒绝加载（`D-4`）。");
            }

            if (!seen.Add(t.Id))
            {
                throw new InvalidDataException(
                    $"{ResPath}: `id` 重复（`{t.Id}`）⇒ `Find` 只会取第一条，第二条**永远拿不到**（静默死数据）⚠️");
            }

            if (string.IsNullOrWhiteSpace(t.Region) || !KnownRegions.Contains(t.Region))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 陷阱 `{t.Id}` 的 `region` = `{t.Region}` **不在已登记地区**里" +
                    $"（{string.Join(" / ", KnownRegions)}）⇒ 拒绝加载 —— 🔴 **不静默当成 ruins**" +
                    "（那等于「谁替策划选了一个地区」，红线 21 / `P29` ①）⚠️");
            }

            if (t.HpPercent is <= 0 or > 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 陷阱 `{t.Id}` 的 `hp_percent` = {t.HpPercent} 越界（须 (0,100]；`D-4`）—— " +
                    "0 ⇒ 只有压力没有伤害（必是漏配键）；>100 ⇒ 一击必杀（非设计意图）⚠️");
            }

            if (t.Weight < 0)
            {
                throw new InvalidDataException($"{ResPath}: 陷阱 `{t.Id}` 的 `weight` = {t.Weight} 不能为负（`D-4`）。");
            }
        }

        // ⑤ 每个**被要求覆盖**的地区：权重和必须 > 0（全 0 ⇒ 该地区**永远无陷阱** = 静默失效）
        //    🔴 判据是"有要求"而不是"有条目"：出货数据必须四地区齐备（默认 = KnownRegions 全集）✓
        foreach (string region in requireRegions ?? KnownRegions)
        {
            int sum = defs.WeightSumFor(region);
            if (sum <= 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 地区 `{region}` 的陷阱**权重和为 0** ⇒ 该地区**永远抽不到陷阱**（静默失效）⚠️ " +
                    "要么给权重，要么**别把它列为要求覆盖的地区**（`D-4`）。");
            }
        }

        // ⑥ ~ ⑨ 全局数值
        if (defs.UnscoutedDodgePercent is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: `unscouted_dodge_percent` = {defs.UnscoutedDodgePercent} 越界（须 [0,100]；`D-4`）—— " +
                ">100 的骰子无意义，<0 等于必然踏中⚠️");
        }

        if (defs.DisarmBonusPercent < 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: `disarm_bonus_percent` = {defs.DisarmBonusPercent} 必须 ≥ 0 —— " +
                "负加成让**拆除反而更难**，等于「侦察」这个机制白做（静默失效）⚠️");
        }

        if (defs.StressDamage < 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: `stress_damage` = {defs.StressDamage} 必须 ≥ 1 —— " +
                "0 会让「压力伤害」整条**静默失效**（DD 原文：stress **always** +15）⚠️ " +
                "🔴 也可能是**该键漏配**（本字段刻意无默认值）⇒ 要关就删掉整段 `trap_defs` ✓");
        }

        if (defs.DisarmStressHeal < 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: `disarm_stress_heal` = {defs.DisarmStressHeal} 必须 ≥ 0 —— DD 是**回压**（「heals 8 stress」），不是加压 ✓");
        }

        return defs;
    }
}

/// <summary>
/// 🔴🔴 **`D-4` 陷阱的「三态门禁」** —— 本刀与 `D-3` 的**直接协同点**。
///
/// DD 原文把陷阱分成**三种可玩状态**（不是"有/没有"二态）：
/// <list type="bullet">
///   <item><description><see cref="Hidden"/>（**未探索**）：玩家**看不见** ⇒ 无从拆除；
///     但🔴 **照样会踏中**（能不能躲取决于 `Trap Resist`）—— 绝不能把"没侦察到"当成"不会触发"，
///     那是把 DD 的紧张感整个删掉 ⚠️</description></item>
///   <item><description><see cref="Disarmable"/>（**只被侦察到** = `D-3` 的 `Scouted`）：地图上**紫色图标**、
///     走近能看到 ⇒ 给一次**拆除**机会（概率 = Trap Resist + `disarm_bonus_percent`）✓</description></item>
///   <item><description><see cref="Consumed"/>（**已走过** = `D-3` 的 `Visited`）：该格内容已消费
///     （陷阱要么被拆、要么已触发）⇒ **不再触发** ✓</description></item>
/// </list>
///
/// 🔴 这就是 `D-3` 中间态 `Scouted` **唯一的用武之地** —— 没有 `D-4`，`Scouted` 只是个视觉差异。
/// </summary>
public enum TrapGate
{
    /// <summary>未探索 ⇒ 看不见（但仍可能踏中）✓</summary>
    Hidden = 0,

    /// <summary>只被侦察到（`RevealState.Scouted`）⇒ 可拆除 ✓</summary>
    Disarmable = 1,

    /// <summary>已走过（`RevealState.Visited`）⇒ 内容已消费，不再触发 ✓</summary>
    Consumed = 2,
}

/// <summary>一次陷阱结算的结果（`D-4`）✓</summary>
public sealed record TrapOutcome(
    /// <summary>是否**踏中**（造成了伤害/压力）✓</summary>
    bool Triggered,

    /// <summary>是否**拆除成功**（`Triggered == false` 且确实是"拆掉了"而不是"已消费"）✓</summary>
    bool Disarmed,

    /// <summary>掷骰值（`< 0` = 没掷；**展示值 == 消费值**，纪律 V）✓</summary>
    double Roll,

    /// <summary>判定用的阈值（`< 0` = 没掷）✓</summary>
    double Threshold);

/// <summary>
/// 🔴🔴 **`D-4` 陷阱结算器**（**零 Godot**；纯规则）—— 全仓**唯一**的陷阱判定/抽取真值。
///
/// **DD 口径**（wiki `Trap` 原文）：
/// · 未侦察 ⇒ 按 **`Trap Resist`**（不是 DODGE）掷闪避；
/// · 已侦察 ⇒ 概率 = **`Trap Resist + 40`**（**允许 &gt; 100%**，原文 "can go above 100%"）；
/// · 拆除成功 ⇒ **完全无害**（"renders it harmless"）+ **回 8 压力**；
/// · 伤害 = **% 最大 HP**；压力 = **定值**。
///
/// 🔴 **随机必写 `RngDraw`**（红线：所有抽取必写日志）；**已消费的格 ⇒ 不掷、不写** ✓
/// </summary>
public static class TrapResolver
{
    /// <summary>🔴 `D-3` → 门禁：**三态**映射（枚举同构，故直接按值映射）✓</summary>
    public static TrapGate GateFor(RevealState state) => state switch
    {
        RevealState.Unexplored => TrapGate.Hidden,
        RevealState.Scouted => TrapGate.Disarmable,
        RevealState.Visited => TrapGate.Consumed,
        _ => TrapGate.Hidden,
    };

    /// <summary>踏中时对**最大 HP** 为 `maxHp` 的队员造成的伤害 —— 🔴 **至少 1**（不能取整成 0）⚠️</summary>
    public static int DamageFor(TrapDef def, int maxHp)
        => Math.Max(1, (int)Math.Round(maxHp * def.HpPercent / 100.0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// **拆除概率**（%）= `Trap Resist + disarm_bonus_percent` ⇒ 🔴 **不钳到 100**
    /// （DD 原文：「Disarm Chance **can go above 100%**」；钳位会静默削掉高抗性队员的优势）⚠️
    /// </summary>
    public static double DisarmChancePercent(TrapDefs defs, int trapResistPercent)
        => trapResistPercent + defs.DisarmBonusPercent;

    /// <summary>**未侦察时的闪避概率**（%）= 该队员 `Trap Resist` × ... 🔴 **不加** disarm 加成 ⚠️</summary>
    public static double DodgeChancePercent(TrapDefs defs, int trapResistPercent)
    {
        _ = defs; // 闪避**只有** Trap Resist 这一项（DD 原文：闪避"depends on Trap Resist, not their DODGE"）
        return trapResistPercent;
    }

    /// <summary>
    /// 🔴 **结算一次陷阱踏中**（三种门禁各自一条路径）✓
    ///
    /// <list type="bullet">
    ///   <item><description><see cref="TrapGate.Consumed"/> ⇒ **不掷、不写、不触发**（行为逐字不变）✓</description></item>
    ///   <item><description><see cref="TrapGate.Hidden"/> ⇒ 掷**闪避**（`Trap Resist`）⇒ 失败则踏中 ✓</description></item>
    ///   <item><description><see cref="TrapGate.Disarmable"/> ⇒ 掷**拆除**（`Trap Resist + bonus`）⇒ 成功则**无害 + 回压** ✓</description></item>
    /// </list>
    /// </summary>
    public static TrapOutcome Resolve(CombatLog log, IRngProvider rng, TrapDefs defs, TrapDef trap,
        TrapGate gate, int trapResistPercent)
    {
        if (log is null || rng is null || defs is null || trap is null)
        {
            throw new ArgumentNullException(nameof(defs));
        }

        if (gate == TrapGate.Consumed)
        {
            return new TrapOutcome(Triggered: false, Disarmed: false, Roll: -1, Threshold: -1);
        }

        bool disarmAttempt = gate == TrapGate.Disarmable;
        double threshold = disarmAttempt
            ? DisarmChancePercent(defs, trapResistPercent)
            : DodgeChancePercent(defs, trapResistPercent);

        // 🔴 `>100` ⇒ **必然成功**：不掷骰（零随机不留痕）——
        //    与 `RevisitSpawner` 的"单侧权重 ⇒ 种类已定，不必掷"**同款纪律** ✓
        if (threshold > 100)
        {
            log.Append(new EffectEvent(default, $"trap_{(disarmAttempt ? "disarm" : "dodge")}_certain", threshold, true));
            return new TrapOutcome(Triggered: false, Disarmed: disarmAttempt, Roll: -1, Threshold: threshold);
        }

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));

        bool success = roll < threshold;
        bool triggered = !success;

        log.Append(new EffectEvent(default,
            $"trap_{(disarmAttempt ? "disarm" : "dodge")}:{trap.Id}:{(success ? "ok" : "fail")}",
            roll, triggered));

        return new TrapOutcome(Triggered: triggered, Disarmed: success && disarmAttempt, Roll: roll, Threshold: threshold);
    }

    /// <summary>
    /// 🔴 **按地区 + 权重抽一条陷阱** —— 全仓**唯一**的陷阱抽取面
    /// （`D-2` 的 `ThreatTrap` 与"图上的陷阱"**必须**共用它，纪律 `#325` D6）✓
    ///
    /// · 该地区**权重和为 0** ⇒ 返回 `null`（**不静默挑别的地区**的陷阱）⚠️；
    /// · 只有一条候选（或其余权重为 0）⇒ **不掷骰**（种类已定，零随机不留痕）✓；
    /// · 抽取用 `rng.NextInt(0, sum)` ⇒ **必写 `RngDraw`** ✓
    /// </summary>
    public static TrapDef? Pick(CombatLog log, IRngProvider rng, TrapDefs defs, string region)
    {
        if (log is null || rng is null || defs is null)
        {
            throw new ArgumentNullException(nameof(defs));
        }

        IReadOnlyList<TrapDef> pool = defs.CandidatesFor(region);
        if (pool.Count == 0)
        {
            return null; // 该地区无可用陷阱 ⇒ **不掷、不写**（零随机不留痕）✓
        }

        if (pool.Count == 1)
        {
            return pool[0]; // 单条 ⇒ 种类已定，不必掷 ✓
        }

        int sum = pool.Sum(t => t.Weight);
        if (sum <= 0)
        {
            return null; // 防御式兜底：`CandidatesFor` 已保证 `Weight > 0` ⇒ 理论上不可达 ✓
        }

        int draw = rng.NextInt(0, sum);
        log.Append(new RngDraw(rng.DrawCount, draw));

        int acc = 0;
        foreach (TrapDef t in pool)
        {
            acc += t.Weight;
            if (draw < acc)
            {
                return t;
            }
        }

        return pool[^1]; // 浮点/边界防御：理论上不可达 ✓
    }
}
