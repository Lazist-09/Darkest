using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **P0 · 养成闭环的"再出发对比"读数**（用户指令：把重心从战斗内核转到 Hamlet/养成闭环）——
/// **纯只读快照**：在"一趟出发之前"抓一次，与**上一趟的**对比 ⇒ 让"**这趟比上趟强在哪**"变成**可读** ✓
///
/// 📌 为什么需要它（问题的形状）：
///   现在回城能花钱、能升级，但**玩家（和我们自己）都看不到成长** ⇒ "养成有没有意义"无法回答 ⚠️
///   而它**不需要任何新机制**：所有量都已经存在（名册 / 传家宝 / 建筑 / 金钱）⇒ 只是**没人把它们放在一起比** ✓
///
/// 🔴 纪律：
///   · **零数值改动**（`#307`）：本类**只读**，不写任何状态 ✓
///   · **不造旁路账本**：全部读既有对象（`Roster` / `HeirloomStock` / `Economy`）✓
///   · **HP 不进快照**：契约是"每场满血开局"（`#245`）⇒ 出发时 HP 恒满，记它没有信息量（在 `Describe` 里注明）✓
/// </summary>
public sealed record RunStartSnapshot(
    int RunIndex,
    int Gold,
    int RosterCount,
    int RosterCap,
    int MoraleAvg,
    int LevelAvg,
    int LevelMax,
    int Diseases,
    int TraitsPositive,
    int TraitsNegative,
    int TraitsLocked,
    IReadOnlyDictionary<string, int> Heirlooms,
    IReadOnlyDictionary<int, int> LevelHistogram,   // 🆕 等级分布（Lv → 人数）：让"队伍在长"看得见形状 ✓
    IReadOnlyList<string> SortieIds,               // 🆕 本趟出征名单（为 null/空 ⇒ 未提供）：轮换读数的基础 ✓
    IReadOnlyDictionary<string, int> BuildingLevels,
    // 🆕 **M15-P0（架构裁定取 (b)）**：**上一次收尾时的队伍 HP%**（0~100；null = 未接线/首趟 ⇒ 不假装）✓
    //   口径（架构裁定）：对比的是"**上次收尾 vs 本次出发**" —— 而**出发按契约恒满血**（`#245`）✓
    //   ⇒ 所以它记的是【上一趟结束时】的队伍平均 HP%（**不是**出发时的 100%）✓
    int? HpPercentLastRunEnd = null)
{
    /// <summary>
    /// 默认关注的建筑（`heirlooms.json` 的 building id；名字不是数字 ⇒ 写在这里不触 `#307`）✓
    /// 🔴 **主程序踩过**：我曾把 `sanitarium` 也写进来 —— 而它是**服务**不是**可升级建筑** ⇒
    ///    `HeirloomStock.LevelOf` 按 `P23` ④ **抛"未知建筑"** ⇒ 🔴 **一个读数把整条"进地牢"流程打断了** ⚠️
    ///    ⇒ 教训：**读数绝不许能打断主流程**；且 id 只许用**真实登记过的**（见下方 `Capture` 的兜底）✓
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultBuildings = new[] { "tavern", "abbey", "stagecoach" };

    /// <summary>
    /// 抓一份"出发前"快照 ✓（`heirlooms` / `economy` 允许为 null ⇒ 未接线时不假装有值，如实记 0 并在 `Describe` 标注）✓
    /// </summary>
    public static RunStartSnapshot Capture(
        int runIndex,
        Roster roster,
        HeirloomStock? heirlooms = null,
        Economy? economy = null,
        IReadOnlyList<string>? buildings = null,
        IReadOnlyList<string>? sortieIds = null,   // 🆕 出征名单（可选；给了才能看"换人"）✓
        int? hpPercentLastRunEnd = null)           // 🆕 M15-P0 (b)：上次收尾的队伍 HP%（可选；缺省 = 不记）✓
    {
        if (roster is null)
        {
            throw new ArgumentNullException(nameof(roster));
        }

        var heroes = roster.Heroes;
        int count = heroes.Count;
        int moraleAvg = count == 0 ? 0 : (int)Math.Round(heroes.Average(h => roster.MoraleOf(h.Id)));
        // 🆕 英雄**等级**（用户重心＝养成 ⇒ 这是"队伍在长"的最直接一面；我第一版漏了 ⚠️）✓
        int levelAvg = count == 0 ? 0 : (int)Math.Round(heroes.Average(h => h.Level));
        int levelMax = count == 0 ? 0 : heroes.Max(h => h.Level);

        int diseases = 0;
        int pos = 0;
        int neg = 0;
        int locked = 0;
        foreach (var h in heroes)
        {
            diseases += roster.DiseasesOf(h.Id).Count;
            foreach (var t in roster.TraitsOf(h.Id))
            {
                // 🔴 正/负口径**照数据既有语义**（`roster.json`）：`damage_pct > 0` 或 `morale_damage_pct < 0` ⇒ 正面
                //    （例：`trait_brutal` +5 伤害 = 正；`trait_steady` 士气伤害 −10 = 正）✓
                //    ⚠️ **两者皆 0 ⇒ 中性 ⇒ 两边都不计**（不猜、不硬塞）✓
                bool isPositive = t.DamagePct > 0 || t.MoraleDamagePct < 0;
                bool isNegative = t.DamagePct < 0 || t.MoraleDamagePct > 0;
                if (isPositive)
                {
                    pos++;
                }
                else if (isNegative)
                {
                    neg++;
                }

                if (roster.IsTraitLocked(h.Id, t.Id))
                {
                    locked++;
                }
            }
        }

        var heirloomCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (heirlooms is not null)
        {
            foreach (string k in heirlooms.Kinds)
            {
                heirloomCounts[k] = heirlooms.Count(k);
            }
        }

        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string b in buildings ?? DefaultBuildings)
        {
            // 🔴 **读数不许打断主流程**（我踩过：未知 id ⇒ `LevelOf` 抛异常 ⇒ 整条进地牢流程断掉 ⚠️）
            //    ⇒ 未登记的建筑**如实记 −1**（= "未登记"，不是 0 级 —— 两者含义不同）✓
            try
            {
                levels[b] = heirlooms?.LevelOf(b) ?? 0;
            }
            catch (InvalidOperationException)
            {
                levels[b] = -1;
            }
        }

        var levelHist = new Dictionary<int, int>();
        foreach (var h in heroes)
        {
            levelHist[h.Level] = levelHist.GetValueOrDefault(h.Level) + 1;
        }

        // 🔴 参数顺序必须与 record 声明一致（我第一版插错位置 ⇒ 编译当场抓到 ✓）
        return new RunStartSnapshot(runIndex, economy?.Gold ?? 0, count, roster.CurrentCap, moraleAvg, levelAvg, levelMax,
            diseases, pos, neg, locked, heirloomCounts, levelHist,
            sortieIds?.ToList() ?? new List<string>(), levels, hpPercentLastRunEnd);   // 🆕 M15-P0 (b)：**真的传进 record**（我第一版只加参数没传 ⇒ 用例当场抓到 ✓）
    }

    /// <summary>一行读数（存档/日志用）✓</summary>
    /// <summary>等级分布的可读形式（`[Lv1×3 Lv2×5]`）✓</summary>
    private static string Hist(IReadOnlyDictionary<int, int> h)
        => "[" + string.Join(" ", h.OrderBy(k => k.Key).Select(k => $"Lv{k.Key}×{k.Value}")) + "]";

    public string Describe()
        => $"第 {RunIndex} 趟出发前：金钱 {Gold}　名册 {RosterCount}/{RosterCap}　平均士气 {MoraleAvg}　等级均 {LevelAvg}（最高 {LevelMax}）　" +
           $"等级分布 {Hist(LevelHistogram)}　{(SortieIds.Count > 0 ? $"出征 {string.Join("/", SortieIds)}　" : "")}" +
           $"疾病 {Diseases}　特质 正 {TraitsPositive}／负 {TraitsNegative}（锁定 {TraitsLocked}）　" +
           $"传家宝 [{string.Join(" ", Heirlooms.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}"))}]　" +
           $"建筑 [{string.Join(" ", BuildingLevels.OrderBy(k => k.Key).Select(k => $"{k.Key}:Lv{k.Value}"))}]　" +
           (HpPercentLastRunEnd is { } hp
               ? $"队伍 HP%：**上次收尾 {hp}%** → 本次出发 100%（契约满血 `#245`）✓"
               : "队伍 HP%：**未接线**（架构 (b) 口径要的是「上次收尾」⇒ 等宿主把该值传进来；**不假装** ✓）✓");

    /// <summary>
    /// 🔴 **"本次 vs 上次"对比行**（只列**真的变了**的项）—— 这就是"养成有没有意义"的可读形式 ✓
    /// `prev == null`（第 1 趟）⇒ 只报"首趟基线" ✓
    /// </summary>
    public IReadOnlyList<string> DiffLines(RunStartSnapshot? prev)
    {
        if (prev is null)
        {
            return new[] { $"[养成] 首趟基线：{Describe()}" };
        }

        var diffs = new List<string>();
        void Cmp(string label, int now, int before)
        {
            if (now != before)
            {
                diffs.Add($"{label} {before} → {now}（{(now > before ? "+" : "")}{now - before}）");
            }
        }

        Cmp("金钱", Gold, prev.Gold);
        Cmp("名册", RosterCount, prev.RosterCount);
        Cmp("名册上限", RosterCap, prev.RosterCap);
        Cmp("平均士气", MoraleAvg, prev.MoraleAvg);

        // 🆕 **M15-P0 (b)**：跨趟看"队伍收尾 HP%"的走向（两边都有值才比 ⇒ 不与"未接线"混淆 ✓）
        if (prev.HpPercentLastRunEnd is { } ph && HpPercentLastRunEnd is { } nh)
        {
            Cmp("上趟收尾队伍 HP%", nh, ph);
        }
        Cmp("平均等级", LevelAvg, prev.LevelAvg);

        // 🆕 **等级分布**（形状变化也要看得见：例 `[Lv1×3 Lv2×5] → [Lv1×1 Lv2×6 Lv3×1]`）✓
        string nowHist = Hist(LevelHistogram);
        string prevHist = Hist(prev.LevelHistogram);
        if (nowHist != prevHist)
        {
            diffs.Add($"等级分布 {prevHist} → {nowHist}");
        }

        // 🆕 **轮换**（换了几个人；两边都没给名单 ⇒ 如实不报，不假装 0 ✓）✓
        if (SortieIds.Count > 0 && prev.SortieIds.Count > 0)
        {
            int kept = SortieIds.Count(id => prev.SortieIds.Contains(id));
            int swapped = SortieIds.Count - kept;
            if (swapped > 0)
            {
                diffs.Add($"出征名单**换人 {swapped} 名**（留 {kept}）");
            }
        }
        Cmp("疾病", Diseases, prev.Diseases);
        Cmp("正面特质", TraitsPositive, prev.TraitsPositive);
        Cmp("负面特质", TraitsNegative, prev.TraitsNegative);
        Cmp("锁定特质", TraitsLocked, prev.TraitsLocked);

        foreach (var kv in Heirlooms.OrderBy(k => k.Key))
        {
            Cmp($"传家宝[{kv.Key}]", kv.Value, prev.Heirlooms.TryGetValue(kv.Key, out int b) ? b : 0);
        }

        foreach (var kv in BuildingLevels.OrderBy(k => k.Key))
        {
            Cmp($"建筑[{kv.Key}]等级", kv.Value, prev.BuildingLevels.TryGetValue(kv.Key, out int b) ? b : 0);
        }

        if (diffs.Count == 0)
        {
            // 🔴 策划 `#394` 的 **A4**（「第二次去，队伍不一样」）在这里落到实处：起点没变 ⇒ A4 **不成立** ⚠️
            return new[]
            {
                $"[养成] 第 {RunIndex} 趟 vs 第 {prev.RunIndex} 趟：**起点未变**（无成长也无损耗）",
                $"[A4] ❌ **不成立**：本趟与上趟起点一致 ⇒ 「第二次去，队伍不一样」没有落点" +
                "（若这是第 5 趟之后，需按 `#394` 给解释；若是首几趟，属正常）✓",
            };
        }

        return new[]
        {
            $"[养成] 第 {RunIndex} 趟 vs 第 {prev.RunIndex} 趟（变了 {diffs.Count} 项）：{string.Join("　", diffs)} ✓",
            $"[A4] ✅ **成立**：起点确实变了（{diffs.Count} 项）⇒ 「第二次去，队伍不一样」有落点 ✓",
        };
    }
}
