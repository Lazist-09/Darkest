using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M7.5 D0（光照计，`#258`）：边界取档写死、前进 −15 / 提亮 +30（1 柴火，**不足则拒且不变**）/
/// 扎营回满 / 事件二选一；每次变化写 `LightChangedEvent` 且**可从事件流复算**；
/// P21：五档覆盖 / 掉落概率 / **效果表无 HP 字段** / 背包 12 格 / 侦察口径。
/// </summary>
[TestClass]
public sealed class LightMeterTests
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

    private static TuningConfig Tuning() => TuningConfig.Parse(ReadData("tuning.json"));

    [TestMethod]
    public void D0_TierBoundaries_AreWrittenDown()
    {
        Assert.AreEqual(LightTier.Radiant, LightMeter.TierFor(100));
        Assert.AreEqual(LightTier.Radiant, LightMeter.TierFor(76), ">75 = Radiant");
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(75), "75 = Dim（边界归下档）");
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(51));
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(50), "50 = Shadowy");
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(26));
        Assert.AreEqual(LightTier.Dark, LightMeter.TierFor(25), "25 = Dark");
        Assert.AreEqual(LightTier.Dark, LightMeter.TierFor(1));
        Assert.AreEqual(LightTier.Black, LightMeter.TierFor(0), "0 = Black");
    }

    [TestMethod]
    public void D0_AdvanceBrightenCamp_FlowAndEvents()
    {
        TuningLight cfg = Tuning().Light!;
        var log = new CombatLog();
        var meter = new LightMeter(cfg);

        Assert.AreEqual(100, meter.Value, "进图 100（D0.1）");
        Assert.IsTrue(cfg.NodeStep == -30 && cfg.BrightenGain == 30 && cfg.BrightenFirewoodCost == 1,
            "#278：前进 −30（提亮 +30 恰好抵消 ⇒ 提亮 = 买一个节点）");

        meter.TryAdvanceNode(log);
        Assert.AreEqual(70, meter.Value, "#278：前进 −30（100 → 70）");
        Assert.AreEqual(LightTier.Dim, meter.Tier, "70 属于 Dim（51~75）");

        int firewood = 2;
        Assert.IsTrue(meter.TryBrighten(log, () => firewood-- > 0), "提亮 +30（消耗 1 柴火）");
        Assert.AreEqual(100, meter.Value, "提亮后封顶 100（−30 + 30 ⇒ 提亮 = 买一个节点）");
        Assert.AreEqual(1, firewood);

        meter.TryAdvanceNode(log);
        meter.TryAdvanceNode(log); // 100 → 70 → 40（Shadowy）
        Assert.AreEqual(40, meter.Value);
        Assert.AreEqual(LightTier.Shadowy, meter.Tier);
        Assert.AreEqual(20, meter.Effect.OurMoraleDamagePct, "Shadowy：我方士气伤害 +20%（D0.4）");

        meter.OnCamp(log);
        Assert.AreEqual(100, meter.Value, "扎营 → 回满 100");

        Assert.IsTrue(log.Events.OfType<LightChangedEvent>().Any(e => e.Reason == "advance"));
        Assert.IsTrue(log.Events.OfType<LightChangedEvent>().Any(e => e.Reason == "brighten"));
        Assert.IsTrue(log.Events.OfType<LightChangedEvent>().Any(e => e.Reason == "camp"));
        Assert.AreEqual(meter.Value, LightMeter.Recompute(log, cfg.EnterValue), "**可从事件流复算**（UI 必显 ⑨）");
    }

    [TestMethod]
    public void D0_BrightenWithoutFirewood_RejectedAndUnchanged()
    {
        TuningLight cfg = Tuning().Light!;
        var log = new CombatLog();
        var meter = new LightMeter(cfg, initial: 40);
        int before = meter.Value;

        Assert.IsFalse(meter.TryBrighten(log, () => false), "柴火不足 → 拒绝");
        Assert.AreEqual(before, meter.Value, "拒绝时**值不变**");
        Assert.IsFalse(log.Events.OfType<LightChangedEvent>().Any(), "拒绝不写事件");
    }

    [TestMethod]
    public void D0_EventChoice_TorchOrDark()
    {
        TuningLight cfg = Tuning().Light!;
        var log = new CombatLog();
        var a = new LightMeter(cfg, initial: 40);
        a.ApplyEventChoice(log, torch: true);
        Assert.AreEqual(60, a.Value, "举火把 +20");

        var b = new LightMeter(cfg, initial: 40);
        b.ApplyEventChoice(log, torch: false);
        Assert.AreEqual(20, b.Value, "摸黑 −20（但掉落更好：D0.3）");
    }

    [TestMethod]
    public void P21_LightData_Shipped_WithNoHpFields()
    {
        TuningConfig t = Tuning();
        Assert.AreEqual(5, t.Light!.Tiers.Count, "五档（P21 ①）");
        Assert.AreEqual(0, t.Light.Loot["radiant"].Firewood, "#276：Radiant 无掉落");
        Assert.AreEqual(1, t.Light.Loot["shadowy"].Firewood, "#276：Shadowy 起 ≥1 柴火（摸黑换续航）");
        Assert.AreEqual(2, t.Light.Loot["black"].Firewood, "#276：Black 2 柴火（柴火优先、按档单调不减）");
        Assert.AreEqual(2, t.Light.Loot["black"].Food, "#276：Black 另给 2 口粮");
        Assert.AreEqual(25, t.Scouting!.BasePct, "侦察基础 25%（D1）");
        Assert.AreEqual("next_node_type_only", t.Scouting.Reveal);
        Assert.AreEqual(12, t.Inventory!.SlotCap, "背包 12 格（P21 ⑥：不许调到 15）");
        Assert.IsFalse(t.Inventory.FoodStackable, "口粮不堆叠 ⇒ 默认 15 > 12 真的装不下");
    }

    [TestMethod]
    public void P21_NoHpFieldInLightEffects_ThrowsOnLoad()
    {
        string bad = ReadData("tuning.json").Replace(
            "\"our_morale_damage_pct\": 40, \"enemy_acc\": 12.5",
            "\"our_morale_damage_pct\": 40, \"hp\": 100, \"enemy_acc\": 12.5",
            StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(bad),
            "光照效果表出现 HP 字段 → 启动报错（P21 ③ / #255 已否决该杠杆）");
    }

    [TestMethod]
    public void P21_SlotCapNotTwelve_ThrowsOnLoad()
    {
        string bad = ReadData("tuning.json").Replace("\"slot_cap\": 12", "\"slot_cap\": 15", StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(bad),
            "把 slot_cap 调到 15 来修装不下 → 启动报错（P21 ⑥）");
    }

    [TestMethod]
    public void D6_WhileCarried_IsAFourthUnmergeableDuration()
    {
        BuffDefsConfig defs = BuffDefsConfig.Parse(ReadData("buff_defs.json"));
        _ = defs;
        // 四类不可合并：until_next_recovery（恢复即清）/ battles（计数）/ next_battle（下一场即清）/ while_carried（**携带即生效**）
        Assert.AreNotEqual(BuffDurationType.UntilNextRecovery, BuffDurationType.WhileCarried);
        Assert.AreNotEqual(BuffDurationType.NextBattle, BuffDurationType.WhileCarried);
    }
}
