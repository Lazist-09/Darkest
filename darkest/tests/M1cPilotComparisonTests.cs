using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Data;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M1c · 阶段 2 的对照夹具**（架构三步走第 2 步："新路径默认关 + **1~2 技能试点并跑对照**"）。
///
/// 本夹具**不做任何切换**：它把**旧模型**与**新模型（武器区间 × (1+技能 dmg%)）**在同一输入下并排算出来，
/// 给出**差值表** ⇒ 这就是阶段 3"切换默认"时要用的**前后读数对照**的现成工具 ✓
///
/// 输入来源（可核）：
///   · 武器区间 = `units.json` 的 `weapon[i].dmg_min/dmg_max`（策划 `#448` 已对齐到 4 原型 ✓）
///   · 技能 `dmg%` = 参考项目示例（`smite 0%` · `zealous_accusation -40%`）——
///     ⚠️ 它们是**夹具输入**，**不是**我们技能表的值（我们的值归策划 ✓）
///   · 旧模型的"中性帧"= `attack × 1.0`（不吃段倍率/暴击/增益/减伤 ⇒ 只比较**形状**差异 ✓）
/// </summary>
[TestClass]
public sealed class M1cPilotComparisonTests
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

    /// <summary>夹具的输出：同一输入下，旧模型 / 新模型 / 差值（比率）。</summary>
    private readonly record struct Row(string Unit, int Tier, int SkillDmgPct, int OldRaw, double NewRaw)
    {
        public double Delta => NewRaw - OldRaw;

        public double Ratio => OldRaw == 0 ? 0 : NewRaw / OldRaw;
    }

    private static List<Row> Build(IEnumerable<UnitConfig> players, IReadOnlyList<int> tiers, IReadOnlyList<int> skills)
    {
        var rows = new List<Row>();
        foreach (UnitConfig u in players.Where(x => x.Weapon is not null && x.Side == "player"))
        {
            foreach (int tier in tiers)
            {
                var w = u.Weapon![tier];
                foreach (int pct in skills)
                {
                    // 旧模型：中性帧（attack 平推）· 新模型：武器区间中点 × (1 + dmg%)
                    // 🔴 两侧都**只算原始伤害**，不掺减伤/暴击/增益 ⇒ 比较的是模型**形状**而非平衡 ✓
                    double neu = BattleMath.WeaponRoll(w.DmgMin, w.DmgMax, 0.5);
                    rows.Add(new Row(u.Id, tier, pct, u.Attack,
                        BattleMath.WeaponRawDamage(w.DmgMin, w.DmgMax, pct, 0.5) is var r && r >= 0 ? r : 0));
                }
            }
        }

        return rows;
    }

    [TestMethod]
    public void PilotComparison_PrintsTheSideBySideTable_ForTheTwoPilotSkills()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        int[] pilots = { 0, -40 };          // 参考项目的两个技能 dmg%（smite / zealous_accusation）✓
        int[] tiers = { 0, 4 };             // 最低阶与最高阶（形状差异最明显）✓

        List<Row> rows = Build(units.Units, tiers, pilots);

        foreach (Row r in rows)
        {
            Console.WriteLine($"[M1c·对照] {r.Unit} tier{r.Tier} dmg{r.SkillDmgPct,4}% ｜ "
                + $"旧(中性帧) {r.OldRaw,3} ｜ 新(武器区间) {r.NewRaw,6:0.##} ｜ Δ {r.Delta,+7:0.##}（×{r.Ratio:0.00}）");
        }

        // 🔴 夹具的**自证**：新模型必须随 tier 单调不减（武器阶越高区间越大 ⇒ 同 dmg% 下不降 ✓）
        foreach (UnitConfig u in units.Units.Where(x => x.Side == "player" && x.Weapon is not null))
        {
            var w0 = u.Weapon![0];
            var w4 = u.Weapon[4];
            double n0 = BattleMath.WeaponRawDamage(w0.DmgMin, w0.DmgMax, 0, 0.5);
            double n4 = BattleMath.WeaponRawDamage(w4.DmgMin, w4.DmgMax, 0, 0.5);
            Assert.IsTrue(n4 >= n0, $"{u.Id}: 5 阶原始伤害不应低于 0 阶（{n4} vs {n0}）—— 若失败说明对齐数据有问题 ✓");
        }

        Assert.AreEqual(16, rows.Count, "4 原型 × 2 阶 × 2 技能 = 16 行对照 ✓");
        Console.WriteLine("[M1c·对照] 16 行并排读数已输出 ⇒ 阶段 3 切换时用它做**前后对照**（本夹具不切默认 ✓）");
        TestContext.WriteLine("[M1c·阶段2] 对照夹具 ✓");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // 🆕 **P1（2026-09-30）：阶段 2 的第二面** —— 把架构要的三维对照补齐：
    //    **伤害分布 · 回合数 · 胜率** ✓（上一版只给了「同一输入下的静态并排表」）
    //    🔴 **两条铁律不变**：① **零行为**（生产路径不许调用 WeaponBaseDamage —— 守卫测试钉着它 ✓）
    //                      ② **不切默认**（只把新模型的**期望值**喂进**旧**管线做对照 ⇒ 生产一字未动 ✓）
    //    🔴 **口径（必须连着读数一起读，否则会误读）**：本 A/B 用 unitsTweak 把 attack 换成
    //       新模型的**期望值** = tier0 区间中点 × (1 + dmg%) ⇒ 比的是**基础值的水平** ✓
    //       ⚠️ **区间自身的离散**（新模型每击从区间取随机值）**没有**进本读数 ——
    //          它要等阶段 3 真正接线才谈得上（那时 WeaponBaseDamage 被生产消费 ✓）
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>3 个对照臂：**A = 现状（旧模型，不改）** · **B = 新 · 全技能 0%** · **C = 新 · 全技能 −40%**。</summary>
    private sealed record Arm(string Name, int? DmgPct);

    /// <summary>一臂的读数（全部从**事件流**聚合 ⇒ 与表现零耦合 ✓）。</summary>
    private sealed class ArmReading
    {
        public string Name { get; init; } = "";

        public List<int> Rounds { get; } = new();

        public List<int> Hits { get; } = new();

        public Dictionary<string, int> DamageBySkill { get; } = new();

        public Dictionary<string, int> HitCountBySkill { get; } = new();

        public int Win { get; set; }

        public int RoundLimit { get; set; }

        public int Deaths { get; set; }

        public long TotalDamage { get; set; }

        public double WinRate => Win / (double)Rounds.Count;
    }

    /// <summary>
    /// 新模型的**期望基础值**（给旧管线的可插替身）：武器 tier0 中点 × (1 + dmg%/100)，取整到 int
    ///（旧路径的 attack 是 int ⇒ 取整与 §2.3 的「四舍五入」同口径 ✓）。
    /// 🔴 **取 tier0（起始阶）**：游戏现在**没有「当前阶」概念** ⇒ 最低阶 = 最接近现状的对照 ✓
    /// </summary>
    private static int MidAttack(UnitConfig u, int dmgPct)
    {
        double mid = (u.Weapon![0].DmgMin + u.Weapon![0].DmgMax) / 2.0;
        return (int)System.Math.Round(mid * (1.0 + dmgPct / 100.0), System.MidpointRounding.AwayFromZero);
    }

    /// <summary>把「新模型」翻译成旧管线的入参：**只改我方带武器原型的 attack**，其余字段一律不动 ✓</summary>
    private static Func<UnitsConfig, UnitsConfig>? PilotTweak(int? dmgPct)
        => dmgPct is null ? null : cfg => cfg with
        {
            Units = cfg.Units.Select(u => u.IsPlayer && u.Weapon is not null
                ? u with { Attack = MidAttack(u, dmgPct.Value) }
                : u).ToArray(),
        };

    /// <summary>确定性伪随机（SplitMix64）：只为把「区间分布」的分位数打出来 ⇒ 不引入任何新依赖 ✓</summary>
    private static double[] Rolls(int samples)
    {
        var result = new double[samples];
        ulong x = 0x9E3779B97F4A7C15UL;
        for (int i = 0; i < samples; i++)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            result[i] = (z >> 11) * (1.0 / 9007199254740992.0);   // 高 53 位 ⇒ [0,1) ✓
        }

        return result;
    }

    private static int Pct(IReadOnlyList<int> sorted, double p)
        => sorted[System.Math.Clamp((int)(p * sorted.Count), 0, sorted.Count - 1)];

    private static double Sigma(IReadOnlyList<int> xs)
    {
        double mean = xs.Average();
        double sum = 0;
        foreach (int x in xs)
        {
            sum += (x - mean) * (x - mean);
        }

        return System.Math.Sqrt(sum / xs.Count);
    }

    /// <summary>攻击者是不是我方（含支援位复制体 warrior_2 / medic_2 ⇒ 前缀匹配 ✓）</summary>
    private static bool IsPlayerSide(string unitId, UnitsConfig units)
        => units.Units.Where(x => x.IsPlayer)
            .Any(x => unitId == x.Id || unitId.StartsWith(x.Id + "_", StringComparison.Ordinal));

    /// <summary>
    /// 🔴 **维度 ①：伤害分布**（纯函数 · 零模拟）—— 旧模型是**点值**（每击同一数 ⇒ σ = 0），
    /// 新模型是**区间均匀分布**（每击不同）。本方法把两者的分位数并排打出来 ✓
    /// </summary>
    [TestMethod]
    public void DamageDistribution_WeaponRangeVsPointBase_PrintsPercentiles()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        const int samples = 20000;
        double[] rolls = Rolls(samples);
        int[] pilots = { 0, -40 };

        Console.WriteLine($"[M1c·分布] 逐击基础值分布（{samples} 次确定性取样 · tier0 · 取整后）—— 旧模型是【点值】⇒ 分布退化成一个数 ✓");
        foreach (UnitConfig u in units.Units.Where(x => x.IsPlayer && x.Weapon is not null))
        {
            WeaponTier w = u.Weapon![0];
            Console.WriteLine($"[M1c·分布] {u.Id,-10} 旧（点值）attack {u.Attack,3} ⇒ σ = 0（无区间 ⇒ 每击同一数）");
            foreach (int pct in pilots)
            {
                var hit = new int[samples];
                for (int i = 0; i < samples; i++)
                {
                    hit[i] = (int)System.Math.Round(
                        BattleMath.WeaponRawDamage(w.DmgMin, w.DmgMax, pct, rolls[i]), System.MidpointRounding.AwayFromZero);
                }

                int[] sorted = hit.OrderBy(v => v).ToArray();
                Console.WriteLine($"[M1c·分布] {u.Id,-10} 新 dmg{pct,4}% · 区间 {w.DmgMin}-{w.DmgMax} ⇒ "
                    + $"min {sorted[0]} / P5 {Pct(sorted, 0.05)} / P25 {Pct(sorted, 0.25)} / P50 {Pct(sorted, 0.5)} "
                    + $"/ P75 {Pct(sorted, 0.75)} / P95 {Pct(sorted, 0.95)} / max {sorted[^1]}"
                    + $" · 均值 {hit.Average():0.00} · σ {Sigma(hit):0.00} ⇒ 相对旧 {hit.Average() / u.Attack:0.00}×");
            }
        }

        // 🔴 **分布的三条硬性质自证**（否则这张表就是画出来的）：① 两端 = 区间端点 ② 单调不减 ③ 均值 = 中点
        UnitConfig warrior = units.Get("warrior");
        WeaponTier w0 = warrior.Weapon![0];
        var probe = new int[2001];
        for (int i = 0; i <= 2000; i++)
        {
            probe[i] = (int)System.Math.Round(
                BattleMath.WeaponRawDamage(w0.DmgMin, w0.DmgMax, 0, i / 2000.0), System.MidpointRounding.AwayFromZero);
        }

        Assert.AreEqual(w0.DmgMin, probe[0], "roll 0 ⇒ 区间左值 ✓");
        Assert.AreEqual(w0.DmgMax, probe[2000], "roll 1 ⇒ 区间右值 ✓");
        for (int i = 1; i <= 2000; i++)
        {
            Assert.IsTrue(probe[i] >= probe[i - 1], "区间取值必须单调不减 ✓");
        }

        Assert.AreEqual((w0.DmgMin + w0.DmgMax) / 2.0, probe.Average(), 0.05, "均值 ≈ 区间中点 ✓");
        Console.WriteLine("[M1c·分布] 自证：区间的端点 / 单调 / 均值三条都过 ⇒ 分布不是画的 ✓");
        TestContext.WriteLine("[M1c·阶段2] 伤害分布已输出 ✓");
    }

    /// <summary>
    /// 🔴 **维度 ②③：回合数 + 胜率**（headless 直驱 100 场 × 3 臂 · **三臂同 seed** ⇒ 配对对照，消 seed 方差 ✓）
    /// 🔴 **刻意不做平衡判红**（项目惯例：探针只报数 —— 见 `doc/state.md` O-82 探针惯例 ✓）：
    ///    B/C 两臂用的是**夹具值**而不是策划值 ⇒ 给它们设门槛 = 给「假前提」设闸 ✓
    /// </summary>
    [TestMethod]
    public void SimLevel_AB_PrintsDamageRoundsAndWinRate_ForThreeArms()
    {
        const int runs = 100;
        const long seedBase = 20260909;   // 与 M6Acceptance 同族 ⇒ 三臂**逐 seed 配对** ✓
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));

        // 0. 变更映射表：先让读者看清「A/B 到底改了什么」✓
        foreach (UnitConfig u in units.Units.Where(x => x.IsPlayer && x.Weapon is not null))
        {
            WeaponTier w = u.Weapon![0];
            Console.WriteLine($"[M1c·A/B] 变更映射 {u.Id,-10} 现状 attack {u.Attack,3} ⇒ 0% 臂 {MidAttack(u, 0),3} · −40% 臂 {MidAttack(u, -40),3}（tier0 区间 {w.DmgMin}-{w.DmgMax}）");
        }

        // 1. 前置自证：**tweak 真的生效**（否则 A/B 是假对照 ⇒ 这三条必须断言 ✓）
        UnitConfig before = units.Get("warrior");
        UnitConfig after = PilotTweak(0)!(units).Get("warrior");
        Assert.AreEqual(MidAttack(before, 0), after.Attack, "🔴 0% 臂必须真的替换了 attack ✓");
        Assert.AreEqual(before.Hp, after.Hp, "🔴 只许动 attack ⇒ HP 等一律不变（单变量对照）✓");
        Assert.IsTrue(units.Units.Any(u => u.IsPlayer && u.Weapon is not null && MidAttack(u, 0) != u.Attack),
            "🔴 若 0% 臂与现状完全相同 ⇒ 这个 A/B 是空的（读数没有意义）✓");

        // 2. 三臂跑同一批 seed
        var arms = new[]
        {
            new Arm("A·现状（旧模型）", null),
            new Arm("B·新 · 全技能 0%", 0),
            new Arm("C·新 · 全技能 −40%", -40),
        };
        var readings = new List<ArmReading>();
        foreach (Arm arm in arms)
        {
            var m = new ArmReading { Name = arm.Name };
            for (int i = 0; i < runs; i++)
            {
                (GameOutcome o, CombatLog log) = HeadlessDriver.Run(
                    seedBase + i, PolicyKind.SemiRandom, unitsTweak: PilotTweak(arm.DmgPct));
                if (o.Result is GameResult.PlayerVictory or GameResult.DrawRetreat)
                {
                    m.Win++;
                }

                if (o.Result == GameResult.RoundLimit)
                {
                    m.RoundLimit++;
                }

                m.Rounds.Add(o.Rounds);
                m.Deaths += log.Events.OfType<DeathEvent>().Count(e => e.IsPlayer);

                foreach (DamageEvent d in log.Events.OfType<DamageEvent>())
                {
                    if (d.Attacker is not UnitId a || !IsPlayerSide(a.Value, units))
                    {
                        continue;
                    }

                    m.TotalDamage += d.Amount;
                    m.Hits.Add(d.Amount);
                    if (d.SkillId is string sid)
                    {
                        m.DamageBySkill[sid] = m.DamageBySkill.GetValueOrDefault(sid) + d.Amount;
                        m.HitCountBySkill[sid] = m.HitCountBySkill.GetValueOrDefault(sid) + 1;
                    }
                }
            }

            Assert.AreEqual(runs, m.Rounds.Count, "每臂都必须跑满场次 ✓");
            Assert.IsTrue(m.Hits.Count > 0, $"🔴 {arm.Name}：我方 0 次命中 ⇒ 事件流聚合或单位识别有一处错 ✓");
            readings.Add(m);
        }

        // 3. 逐臂读数
        foreach (ArmReading m in readings)
        {
            int[] rs = m.Rounds.OrderBy(v => v).ToArray();
            int[] hs = m.Hits.OrderBy(v => v).ToArray();
            Console.WriteLine($"[M1c·A/B] ==== {m.Name} ==== 场次 {m.Rounds.Count} · **胜率 {m.WinRate:P1}** · **平均回合 {m.Rounds.Average():0.00}**"
                + $"（P10 {Pct(rs, 0.10)} / P50 {Pct(rs, 0.50)} / P90 {Pct(rs, 0.90)} · 最短 {rs[0]} / 最长 {rs[^1]}）"
                + $" · 打满 100 回合 {m.RoundLimit} 场 · 我方阵亡 {m.Deaths} 人次");
            Console.WriteLine($"[M1c·A/B] {m.Name} · **总伤害 {m.TotalDamage}** · 每回合均伤 {m.TotalDamage / (double)m.Rounds.Sum():0.0}"
                + $" · 命中 {m.Hits.Count} 次 · **每命中均伤 {m.TotalDamage / (double)m.Hits.Count:0.00}**"
                + $"（P10 {Pct(hs, 0.10)} / P50 {Pct(hs, 0.50)} / P90 {Pct(hs, 0.90)} · max {hs[^1]}）");
            foreach (KeyValuePair<string, int> kv in m.DamageBySkill.OrderByDescending(k => k.Value).Take(6))
            {
                Console.WriteLine($"[M1c·A/B] {m.Name} · 伤害榜 {kv.Key,-26} 合计 {kv.Value,6} · n {m.HitCountBySkill[kv.Key],4} · 均 {kv.Value / (double)m.HitCountBySkill[kv.Key]:0.00}");
            }
        }

        // 4. 对照小结（配对 ⇒ 可直接相减 ✓）
        long baseDamage = readings[0].TotalDamage;
        Console.WriteLine("[M1c·A/B] 🔴 对照小结（同 seed 配对 ⇒ 各臂可与 A 直接相减 ✓）：");
        foreach (ArmReading m in readings)
        {
            Console.WriteLine($"[M1c·A/B]   {m.Name,-18} 胜率 **{m.WinRate:P1}**（Δ{100 * (m.WinRate - readings[0].WinRate):+0.0;-0.0}pp）· "
                + $"平均回合 **{m.Rounds.Average():0.00}**（Δ{m.Rounds.Average() - readings[0].Rounds.Average():+0.00;-0.00}）· "
                + $"总伤害 {m.TotalDamage}（×{m.TotalDamage / (double)baseDamage:0.00}）");
        }

        // 5. 口径三条 + 结构性断言（只钉「机制没坏」，不钉平衡 ✓）
        Console.WriteLine("[M1c·A/B] ⚠️ 口径三条（读数必须连着它们读）：");
        Console.WriteLine("[M1c·A/B]   ① 本对照只替换【基础值的期望】⇒ 新模型**每击的区间离散**未进读数（等阶段 3 接线）✓");
        Console.WriteLine("[M1c·A/B]   ② 用 tier0（起始阶）：游戏**没有「当前阶」概念**（阶段 3 硬前置 ①）✓");
        Console.WriteLine("[M1c·A/B]   ③ B/C 是**全技能一刀切**的 dmg% ⇒ 它们答的是「若一刀切会怎样」，**不是**策划的最终值 ✓");
        Assert.IsTrue(readings.All(m => m.Rounds.All(r => r <= 100)), "回合数必须钳在 100 以内（软锁审计口径）✓");
        TestContext.WriteLine("[M1c·A/B] 三维对照（分布 / 回合 / 胜率）已输出 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
