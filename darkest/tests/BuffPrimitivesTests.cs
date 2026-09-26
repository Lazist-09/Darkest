using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **A1 · buff 原语层**（`darkest/data/buff_primitives.json`，来源 = 本地参考项目）的验收 ✓
///
/// 背景（**实测的病灶**）：我方 `quirks.json` + `trinkets.json` 引用了 **556 个去重的 buff id**，
///   而参考项目的定义池**一条都没落库** ⇒ 引用链 **482/556 解析不到**（另 74 条参考项目未覆盖/已改名）
///   ⇒ 怪癖/饰品的效果**一条都不会生效** ✓ 本用例把"池子真的落了、落对了"钉住 ✓
///
/// 🔴 本用例是**读数 + 断言**两种东西分开写的（纪律 BJ）：
///   · `Assert` = 若数据变了就该红的**硬事实**（条数/唯一性/结构不变量）
///   · `Console.WriteLine` = 供人判读的**读数**（解析率、去向分布）✓
///
/// 判据源：**本地参考项目**（用户指令 2026-09-25"数值采用该项目的来源"）——
///   此前判据源是【一手 E 盘】的 48 组合 / 27 个 `stat_type`（见 `BuffPrimitiveTranslationTests` 的旧注释）✓
/// </summary>
[TestClass]
public sealed class BuffPrimitivesTests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static BuffPrimitivesConfig Pool() => BuffPrimitivesConfig.Parse(ReadData("buff_primitives.json"));

    /// <summary>
    /// 🔴 **防火墙的负向证明**：池子**缺**清单里的名字时，`ValidateAgainst` **必须抛**。
    /// WHY 非做不可：这条校验是本表的**唯一生产消费点** —— 如果它只会"总是通过"，
    ///   那么它在生产路径上等于空转（"接了线但不会响" = 红线 21 的另一种形态）⚠️
    /// </summary>
    [TestMethod]
    public void ValidateAgainst_Throws_WhenThePoolLacksAChecklistName()
    {
        // 一个"小而合法"的池：只有 1 条，且它的 stat_type 是清单里有的那个
        var tiny = new BuffPrimitivesConfig
        {
            Primitives = new[]
            {
                new BuffPrimitiveConfig("X", "hp_heal_percent", "", 0.1, false, "always", false,
                    new BuffPrimitiveRuleData(0, "")),
            },
        };

        Assert.IsTrue(tiny.StatTypes.Contains("hp_heal_percent"), "前提：这一个名字在池子里 ✓");

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => BuffPrimitiveTranslation.ValidateAgainst(tiny),
            "清单里 24 条待接的名字都不在这个池子里 ⇒ 必须抛（不许静默通过）✓");
        StringAssert.Contains(ex.Message, "红线 21", "错误信息要点出纪律号（供人判读）✓");
        StringAssert.Contains(ex.Message, "combat_stat_add", "错误信息要点名缺了哪个（可复算）✓");

        Console.WriteLine($"[A1·防火墙] 池子只有 1 条 ⇒ 加载即抛 ✓（{ex.Message.Length} 字）");
    }

    /// <summary>
    /// 🔴 **解析器的 fail-fast 负向证明**：结构坏了必须**当场抛**，不许靠"缺省当 0"糊过去 ✓
    /// </summary>
    [TestMethod]
    public void Parse_RejectsBrokenStructure()
    {
        static string Row(string id, string durType = "", string dur = "")
            => $"{{\"id\":\"{id}\",\"stat_type\":\"resistance\",\"stat_sub_type\":\"bleed\",\"amount\":0.1,"
             + "\"remove_if_not_active\":false,\"rule_type\":\"always\",\"is_false_rule\":false,"
             + "\"rule_data\":{\"float\":0,\"string\":\"\"}"
             + (durType.Length > 0 ? $",\"duration_type\":\"{durType}\"" : "")
             + (dur.Length > 0 ? $",\"duration\":{dur}" : "") + "}";

        string dup = "{\"primitives\":[" + Row("a") + "," + Row("a") + "]}";
        string empty = "{\"primitives\":[]}";
        string onlyType = "{\"primitives\":[" + Row("a", "combat_end") + "]}";
        string onlyDur = "{\"primitives\":[" + Row("a", "", "4") + "]}";
        string zeroDur = "{\"primitives\":[" + Row("a", "combat_end", "0") + "]}";
        string good = "{\"primitives\":[" + Row("a", "combat_end", "4") + "]}";

        Assert.ThrowsException<InvalidDataException>(
            () => BuffPrimitivesConfig.Parse(dup), "id 重复 ⇒ 抛（引用会歧义）✓");
        Assert.ThrowsException<InvalidDataException>(
            () => BuffPrimitivesConfig.Parse(empty), "空表 ⇒ 抛 ✓");
        Assert.ThrowsException<InvalidDataException>(
            () => BuffPrimitivesConfig.Parse(onlyType), "只给 duration_type 不给 duration ⇒ 抛（实测两者同生同死）✓");
        Assert.ThrowsException<InvalidDataException>(
            () => BuffPrimitivesConfig.Parse(onlyDur), "只给 duration 不给 duration_type ⇒ 抛 ✓");
        Assert.ThrowsException<InvalidDataException>(
            () => BuffPrimitivesConfig.Parse(zeroDur), "duration = 0 ⇒ 抛（必须 > 0）✓");

        // 反向：合法的一行必须**能**通过（否则上面的断言可能只是"一律抛"）
        BuffPrimitivesConfig ok = BuffPrimitivesConfig.Parse(good);
        Assert.AreEqual(1, ok.Primitives.Count, "合法的一行必须能过 ✓");

        Console.WriteLine("[A1·fail-fast] 5 种坏结构全部当场抛 · 合法的一行能过 ✓");
    }

    /// <summary>池子的规模与结构不变量（改了就该红 —— 这就是 A1 的验收定义 ✓）</summary>
    [TestMethod]
    public void Pool_LandedWithTheMeasuredShape()
    {
        BuffPrimitivesConfig cfg = Pool();

        Assert.AreEqual(1801, cfg.Primitives.Count, "参考项目 JsonBuffs 全量 = 1801 条 ✓");
        Assert.AreEqual(1801, cfg.Primitives.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count(),
            "id 必须 1801 个互不相同（重复会让引用歧义）✓");
        Assert.AreEqual(25, cfg.StatTypes.Count, "实测 `stat_type` 闭集 = 25 个 ✓");
        Assert.AreEqual(23, cfg.Primitives.Select(p => p.RuleType).Distinct(StringComparer.Ordinal).Count(),
            "实测 `rule_type` 闭集 = 23 个 ✓");
        Assert.AreEqual(41, cfg.Primitives.Select(p => (p.StatType, p.StatSubType)).Distinct().Count(),
            "实测 (stat_type, stat_sub_type) 组合 = 41 ✓");

        // 🔴 结构不变量：`duration_type` 与 `duration` **同生同死**（63 条都有 / 1738 条都没有）✓
        Assert.AreEqual(63, cfg.Primitives.Count(p => p.DurationType is not null), "带 duration_type 的 = 63 ✓");
        Assert.AreEqual(63, cfg.Primitives.Count(p => p.Duration is not null), "带 duration 的 = 63 ✓");
        Assert.AreEqual(0, cfg.Primitives.Count(p => (p.DurationType is null) != (p.Duration is null)),
            "只出现一半的 = 0（解析器已 fail-fast 拦）✓");

        // 🔴 `remove_if_not_active` = **未接线字段**：1801 条里只有 1 条 true
        //    ⇒ 把条数钉住：改了会红 ⇒ 不会静默变成"没人读的真行为" ✓
        BuffPrimitiveConfig only = cfg.Primitives.Single(p => p.RemoveIfNotActive);
        Assert.AreEqual("skill_transform", only.Id, "唯一 remove_if_not_active=true 的是 skill_transform ✓");
        Assert.AreEqual(1, cfg.Primitives.Count(p => p.RemoveIfNotActive), "true 的条数 = 1 ✓");

        // `is_false_rule` = 通用的"取反"开关（实测 60 条在用）✓
        Assert.AreEqual(60, cfg.Primitives.Count(p => p.IsFalseRule), "is_false_rule=true 的 = 60 ✓");

        Console.WriteLine($"[A1·原语池] {cfg.Primitives.Count} 条 · stat_type {cfg.StatTypes.Count} · "
            + $"rule_type {cfg.Primitives.Select(p => p.RuleType).Distinct().Count()} · 组合 "
            + $"{cfg.Primitives.Select(p => (p.StatType, p.StatSubType)).Distinct().Count()} · "
            + $"duration {cfg.Primitives.Count(p => p.Duration is not null)} · "
            + $"false_rule {cfg.Primitives.Count(p => p.IsFalseRule)} · "
            + $"remove_if_not_active {cfg.Primitives.Count(p => p.RemoveIfNotActive)} ✓");
    }

    /// <summary>
    /// 🔴 **量纲**：参考项目的 `amount` 是**分数**（0.04 = 4%），我方是**整数百分比**
    ///   ⇒ 采用时必须 ×100。这条断言保证"分数性质"没被某次改动悄悄改成整数 ✓
    /// </summary>
    [TestMethod]
    public void Amount_IsAFraction_NotAnIntegerPercent()
    {
        BuffPrimitivesConfig cfg = Pool();

        int fractional = cfg.Primitives.Count(p => p.Amount != Math.Floor(p.Amount));
        Assert.AreEqual(1676, fractional, "非整数 amount = 1676 条（1801 − 125）⇒ 量纲是分数 ✓");
        Assert.IsTrue(cfg.Primitives.Any(p => Math.Abs(p.Amount - 0.04) < 1e-9), "确实存在 0.04 这种值 ✓");

        Console.WriteLine($"[A1·量纲] amount 非整数 {fractional}/1801 ⇒ 参考项目用【分数】，我方用【整数百分比】"
            + "（例 0.04 = 4%）⇒ 采用时 **×100** 且须先定舍入口径 ✓");
    }

    /// <summary>
    /// 🔴 **引用解析率（实测）**：我方 556 个去重 buff id 里，参考项目能定义多少条 ✓
    ///   ⚠️ 这是**读数**不是"通过了"：未解析的 74 条是 A7（把怪癖/饰品改用参考项目来源）的**待改清单** ✓
    /// </summary>
    [TestMethod]
    public void OurBuffReferences_ResolutionRate_IsMeasuredAndFrozen()
    {
        BuffPrimitivesConfig cfg = Pool();
        var known = new HashSet<string>(cfg.Primitives.Select(p => p.Id), StringComparer.Ordinal);

        var refs = new List<(string File, string Owner, string Buff)>();
        foreach (string file in new[] { "quirks.json", "trinkets.json" })
        {
            string json = ReadData(file);
            string root = file == "quirks.json" ? "quirks" : "trinkets";
            foreach (string owner in ExtractOwnerBuffPairs(json, root))
            {
                string[] parts = owner.Split('\u0001');
                refs.Add((file, parts[0], parts[1]));
            }
        }

        int distinct = refs.Select(r => r.Buff).Distinct(StringComparer.Ordinal).Count();
        int resolved = refs.Select(r => r.Buff).Distinct(StringComparer.Ordinal).Count(b => known.Contains(b));
        int unresolved = distinct - resolved;

        Assert.AreEqual(556, distinct, "我方引用的去重 buff id = 556（怪癖 182 + 饰品 374）✓");
        Assert.AreEqual(482, resolved, "参考项目能定义 = 482 ⇒ 引用链从【0 条】到【482 条】✓");
        Assert.AreEqual(74, unresolved, "解析不到 = 74 ⇒ A7 待改清单（报告里逐条列出）✓");

        Console.WriteLine($"[A1·解析率] 我方引用去重 {distinct} ⇒ 参考项目可解析 **{resolved}** / 未解析 **{unresolved}**"
            + "（详情：reports/ref_buff_primitives_source.md）✓");
    }

    /// <summary>
    /// 🔴 **M2 清单必须与池子的 `stat_type` 闭集**逐一相等（双向）——
    ///   少一条 = 上游新增了原语而我们没登记；多一条 = 清单里有上游不存在的名字（红线 21）✓
    /// </summary>
    [TestMethod]
    public void M2Checklist_CoversExactlyThePoolVocabulary()
    {
        BuffPrimitivesConfig cfg = Pool();

        var checklist = new HashSet<string>(BuffPrimitiveTranslation.Checklist, StringComparer.Ordinal);
        string[] missingFromChecklist = cfg.StatTypes.Except(checklist).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] notUpstream = checklist.Except(cfg.StatTypes).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.AreEqual(0, missingFromChecklist.Length,
            $"上游有而清单没有：{string.Join(", ", missingFromChecklist)}（必须显式登记）✓");
        Assert.AreEqual(0, notUpstream.Length,
            $"清单有而上游没有：{string.Join(", ", notUpstream)}（红线 21：写了但接不上）✓");

        Assert.AreEqual(1, BuffPrimitiveTranslation.Activated.Count, "已激活 = 1（hp_heal_percent）✓");
        Assert.AreEqual(24, BuffPrimitiveTranslation.Pending.Count, "待接 = 24（25 − 1）✓");

        // 生产消费点确实调了它（加载即报）⇒ 这里再断言一次"不抛异常" ✓
        BuffPrimitiveTranslation.ValidateAgainst(cfg);

        Console.WriteLine($"[A1·M2清单] 已激活 {BuffPrimitiveTranslation.Activated.Count} · "
            + $"待接 {BuffPrimitiveTranslation.Pending.Count} · 合计 = 上游闭集 {cfg.StatTypes.Count} ✓");
    }

    /// <summary>
    /// 🔴 **每一条原语都必须有去向**（不许静默跳过）：
    ///   `Frozen` = 0 —— 实测 25 个 `stat_type` 全部落到了我们某个层（战斗/士气/治疗/远征/城镇）✓
    /// </summary>
    [TestMethod]
    public void EveryPrimitive_HasADestination_AndNoDestinationIsSilent()
    {
        BuffPrimitivesConfig cfg = Pool();
        var shapes = cfg.Primitives.Select(p => (p.StatType, (string?)p.StatSubType)).ToArray();

        IReadOnlyDictionary<BuffPrimitiveTranslation.Target, int> tally = BuffPrimitiveTranslation.Tally(shapes);
        int frozen = tally.TryGetValue(BuffPrimitiveTranslation.Target.Frozen, out int f) ? f : 0;

        Assert.AreEqual(0, frozen, "无去向（Frozen）的条目 = 0 ✓");
        Assert.AreEqual(shapes.Length, tally.Values.Sum(), "每条恰好落一处（没有第三种去向）✓");

        foreach (string st in cfg.StatTypes)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(BuffPrimitiveTranslation.DestinationNote(st)),
                $"\"{st}\" 必须给出去向说明（不许静默）✓");
        }

        Console.WriteLine("[A1·去向] 原语 1801 条 / 25 种 stat_type 的分布：");
        foreach (var kv in tally.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"[A1·去向]   {kv.Key,-15} {kv.Value,4}");
        }
    }

    /// <summary>
    /// 🔴 **待接条件列**（架构 `M2-ROUTE-B-20260921` 裁定 ②）—— 防它被改坏：
    ///   ① 每条 `Pending` 都必须有非空条件（不许静默）✓
    ///   ② **「缺载体」= 7 / 「未接线」= 17**（与 `ByStatType` 的去向一致）✓
    ///   ③ **"缺载体"必须恰好是 `ExpeditionLayer` 的那些**（不许两处口径漂移）✓
    /// </summary>
    [TestMethod]
    public void PendingConditions_DistinguishMissingCarrierFromUnwired()
    {
        var conditions = BuffPrimitiveTranslation.PendingWithConditions;

        Assert.AreEqual(BuffPrimitiveTranslation.Pending.Count, conditions.Count,
            "条件数与 Pending 条数一致 ✓");

        foreach ((string name, string condition) in conditions)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(condition),
                $"\"{name}\" 必须给出待接条件（不许静默）✓");
        }

        string[] lackCarrier = conditions.Where(c => c.Condition.Contains("缺载体"))
            .Select(c => c.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] unwired = conditions.Where(c => c.Condition.Contains("未接线"))
            .Select(c => c.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.AreEqual(24, lackCarrier.Length + unwired.Length,
            "每条要么缺载体要么未接线（没有第三种）✓");
        Assert.AreEqual(7, lackCarrier.Length,
            $"缺载体 = 7（全 ExpeditionLayer）：{string.Join(", ", lackCarrier)} ✓");
        Assert.AreEqual(17, unwired.Length, "未接线 = 17（战斗级 + 1 条 units.json 抗性）✓");

        // ③ 口径一致：缺载体集合 == ByStatType 里标 ExpeditionLayer 的那些
        string[] expeditionLayer = BuffPrimitiveTranslation.Pending
            .Where(n => BuffPrimitiveTranslation.Classify(n, null)
                        == BuffPrimitiveTranslation.Target.ExpeditionLayer)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expeditionLayer, lackCarrier,
            "「缺载体」必须恰好等于 ExpeditionLayer 的那些（两处口径不许漂移）✓");

        Console.WriteLine($"[A1·待接条件] 缺载体 {lackCarrier.Length} · 未接线 {unwired.Length} ✓");
        Console.WriteLine($"[A1·待接条件] 缺载体逐条：{string.Join(", ", lackCarrier)} ✓");
    }

    /// <summary>
    /// 从我们的怪癖/饰品 JSON 里取出 `(属主id, buff id)` 对（用 `JsonDocument` 手走，避免为读数据再建一个模型 ✓）。
    /// 返回 `"属主\u0001buffId"` 形式的字符串（MSTest 的断言消息里可读 ✓）。
    /// </summary>
    private static IEnumerable<string> ExtractOwnerBuffPairs(string json, string root)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(root, out System.Text.Json.JsonElement rows))
        {
            yield break;
        }

        foreach (System.Text.Json.JsonElement row in rows.EnumerateArray())
        {
            string owner = row.TryGetProperty("id", out System.Text.Json.JsonElement id) ? id.GetString() ?? "" : "";
            if (!row.TryGetProperty("buffs", out System.Text.Json.JsonElement buffs))
            {
                continue;
            }

            foreach (System.Text.Json.JsonElement b in buffs.EnumerateArray())
            {
                if (b.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    yield return owner + "\u0001" + b.GetString()!;
                }
            }
        }
    }
}
