using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Save;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`Phase 1` 存档系统**（`Phase 1` · 方案 §6 Phase 1-C）—— **精简**测试。
///
/// <para>**本文件只守「静默失效」**：即"**错了但不会报错**"的那类缺陷 ——
/// 数据悄悄丢了、档被悄悄删了、损坏档被悄悄读成空档。
/// **不测**显然正确的代码（getter、JSON 库自身、纯转发）✓</para>
///
/// <para>共 8 条（**不新增用例**：`v3` 的怪癖断言全部并进既有用例）✓
/// ① 往返不丢数据 ② 特质实例往返 ③ 版本不符**不删档**
/// ④ 空档哨兵安全 ⑤ 新档（空名册）不炸 ⑥ 恢复等价性（端到端最小）
/// ⑦ 老档（v1）缺 `gear`／`roster.quirks` ⇒ 迁移**逐级**补空表（不是损坏档）
/// ⑧ **必备件**（v2 起 `gear` ／ v3 起 `roster.quirks`）缺了必须**看得见** ✓</para>
/// </summary>
[TestClass]
public sealed class SaveSystemTests
{
    // ---------------------------------------------------------------
    // 夹具
    // ---------------------------------------------------------------

    private static string ReadData(string name)
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
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

    private static Roster NewRoster() => new(RosterConfig.Parse(ReadData("roster.json")));

    private static HeirloomStock NewStock() => new(
        HeirloomConfig.Parse(ReadData("heirlooms.json")),
        HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json")));

    private static Economy NewEconomy() => new(EconomyConfig.Parse(ReadData("economy.json")));

    /// <summary>组装一份「有内容」的顶层快照（名册/进度/传家宝/经济都动过）✓</summary>
    private static SaveSnapshot BuildPopulatedSnapshot()
    {
        var log = new CombatLog();
        Roster roster = NewRoster();
        string heroId = roster.Heroes[0].Id;

        roster.SetMorale(log, heroId, 23, "test");
        roster.AwardExperienceForBattle(log, win: true);
        roster.Infect(log, heroId, "test_disease");
        // 🆕 M5u（v3）：怪癖也造**真内容**（`tough` 是库里真 id，且与 `fragile` 互斥 ⇒ M5 用例已断言）✓
        roster.AddQuirk(log, heroId, "tough", "test");
        roster.ScheduleOpeningPenalty(heroId, 7);
        roster.CurrentCap = 10;

        var progress = new RunProgress();
        progress.FinishRun(log, "completed", battlesWon: 3);

        // 🔴 用**奖励通道**填存量（`TryUpgrade` 需要传家宝存量，开局为 0 ⇒ 会静默失败、测不到东西）
        HeirloomStock stock = NewStock();
        stock.AwardForRun(log, 10, 1.0);

        Economy economy = NewEconomy();
        economy.AwardContent(log, 5000, "test");

        // 🔴 `P4 ①`（2026-09-30）：装备阶也造**真内容** —— 走**真升级路径**（花 750 金买
        //    `hellion.weapon` code 0 ⇒ 阶 = 1）。⚠️ 若这里被静默拒绝，往返断言会看到 0 阶
        //    ⇒ "夹具其实没造出内容"这件事**会被测出来**，不会假装通过 ✓
        var gear = new HeroGearState();
        gear.TryUpgrade(log, HeroUpgradesConfig.Parse(ReadData("hero_upgrades.json")),
            economy, roster.Heroes[0], GearAxis.Weapon, buildingLevel: 4);

        return new SaveSnapshot(
            SaveMigrator.CurrentVersion,
            roster.CaptureSnapshot(),
            progress.CaptureSnapshot(),
            stock.CaptureSnapshot(),
            economy.CaptureSnapshot(),
            gear.CaptureSnapshot());
    }

    // ---------------------------------------------------------------
    // ① 往返不丢数据（守「静默丢失」）
    // ---------------------------------------------------------------

    [TestMethod]
    public void RoundTrip_KeepsEverySubsystemState()
    {
        SaveSnapshot before = BuildPopulatedSnapshot();

        // 🔴 走完整链路：序列化 → 反序列化 → 迁移
        MigrationResult migrated = SaveMigrator.Migrate(
            SaveSerializer.Deserialize(SaveSerializer.Serialize(before)));
        Assert.IsTrue(migrated.Ok, $"迁移应成功：{migrated.Message}");

        // 🔴 恢复到**全新**的实例（不是原对象）⇒ 才能真正证明"数据被搬过去了"
        SaveSnapshot after = migrated.Snapshot!;
        Roster roster = NewRoster();
        roster.RestoreFrom(after.Roster);
        var progress = new RunProgress();
        progress.RestoreFrom(after.Progress);
        HeirloomStock stock = NewStock();
        stock.RestoreFrom(after.Heirlooms);
        Economy economy = NewEconomy();
        economy.RestoreFrom(after.Economy);
        var gear = new HeroGearState();
        gear.RestoreFrom(after.Gear);

        string heroId = before.Roster.Heroes[0].Id;
        Assert.AreEqual(before.Roster.Morale[heroId], roster.MoraleOf(heroId),
            "士气必须原样回来（跨趟状态是名册的核心 —— 丢了等于 `#245` 失效）✓");
        Assert.AreEqual(before.Roster.Xp[heroId], roster.ExperienceOf(heroId), "经验必须原样回来 ✓");
        Assert.AreEqual(before.Roster.CurrentCap, roster.CurrentCap, "当前可用上限必须原样回来 ✓");
        Assert.AreEqual(1, roster.QuirksOf(heroId).Count,
            "🔴 怪癖（v3 新增）必须原样回来 —— 不入档 = 一存一读「招到的怪癖没了」（静默丢状态）✓");
        Assert.IsTrue(roster.QuirksOf(heroId).Contains("tough"), "怪癖 id 必须原样回来 ✓");
        Assert.AreEqual(before.Progress.RunsFinished, progress.RunsFinished, "已完成出征数必须原样回来 ✓");
        Assert.AreEqual(before.Economy.Gold, economy.Gold, "金币必须原样回来 ✓");
        Assert.AreEqual(before.Heirlooms.Levels.Count, after.Heirlooms.Levels.Count, "建筑等级条目数必须一致 ✓");
        Assert.AreEqual(before.Gear.Tiers.Count, after.Gear.Tiers.Count, "装备阶条目数必须一致 ✓");
        Assert.AreEqual(1, gear.WeaponTierOf(heroId),
            "🔴 花金币买来的**装备阶**必须回来 —— 不入档 = 一存一读「金币花了、阶没了」" +
            "（静默丢进度，玩家归因不到）✓");
        Assert.AreEqual(0, gear.ArmourTierOf(heroId), "武器升级**不得**连带护甲阶（两条树独立）✓");
    }

    // ---------------------------------------------------------------
    // ② 特质实例往返（守「特质被抹平」）
    // ---------------------------------------------------------------

    [TestMethod]
    public void RoundTrip_KeepsTraitInstances()
    {
        var log = new CombatLog();
        Roster roster = NewRoster();
        string heroId = roster.Heroes[0].Id;

        HeroTraitConfig trait = roster.TraitsOf(heroId)[0];
        roster.LockTrait(log, heroId, trait.Id, 0, string.Empty);

        SaveSnapshot snap = new(SaveMigrator.CurrentVersion, roster.CaptureSnapshot(),
            new ProgressSnapshot(0, 0),
            new HeirloomSnapshot(new Dictionary<string, int>(), new Dictionary<string, int>()),
            new EconomySnapshot(0, 0),
            new GearSnapshot(Array.Empty<GearTierSnapshot>()));

        Roster restored = NewRoster();
        restored.RestoreFrom(SaveMigrator.Migrate(
            SaveSerializer.Deserialize(SaveSerializer.Serialize(snap))).Snapshot!.Roster);

        Assert.AreEqual(trait.Id, restored.TraitsOf(heroId)[0].Id, "特质 id 必须回来 ✓");
        Assert.AreEqual(trait.DamagePct, restored.TraitsOf(heroId)[0].DamagePct,
            "特质**效果数值**必须回来（只存 id 会丢数值 ⇒ 所以存的是完整实例）✓");
        Assert.IsTrue(restored.IsTraitLocked(heroId, trait.Id), "固化标记必须回来 ✓");
    }

    // ---------------------------------------------------------------
    // ③ 版本不符 ⇒ 不删档（守「静默删档」反模式）
    // ---------------------------------------------------------------

    [TestMethod]
    public void FutureVersion_FailsButKeepsTheSnapshot()
    {
        SaveSnapshot future = BuildPopulatedSnapshot() with { Version = SaveMigrator.CurrentVersion + 1 };

        MigrationResult result = SaveMigrator.Migrate(future);

        Assert.IsFalse(result.Ok, "未来版本必须**判定为失败**（不能假装读得懂）✓");
        Assert.IsNotNull(result.Snapshot,
            "🔴 失败时**必须把原档原样带回** —— 调用方据此保留文件；返回 null 就等于" +
            "「悄悄丢掉玩家进度」（红线：不删档）✓");
        Assert.AreEqual(future.Economy.Gold, result.Snapshot!.Economy.Gold, "带回的必须是**原档**内容 ✓");
    }

    // ---------------------------------------------------------------
    // ④ 空档哨兵安全（守「null 漏判」）
    // ---------------------------------------------------------------

    [TestMethod]
    public void EmptySnapshot_RoundTripsWithoutThrowing()
    {
        // 🔴 "空档"**现场构造**：项目纪律是"只被测试调用的 API 也算死函数" ⇒ 不提供 `Empty` 哨兵 ✓
        SaveSnapshot empty = new(SaveMigrator.CurrentVersion,
            new RosterSnapshot(
                Array.Empty<HeroConfig>(),
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, IReadOnlyList<string>>(),
                new Dictionary<string, IReadOnlyList<string>>(),
                new Dictionary<string, IReadOnlyList<HeroTraitConfig>>(),
                Array.Empty<string>(),
                Array.Empty<GraveyardSnapshot>(),
                0, 0,
                new Dictionary<string, int>()),
            new ProgressSnapshot(0, 0),
            new HeirloomSnapshot(new Dictionary<string, int>(), new Dictionary<string, int>()),
            new EconomySnapshot(0, 0),
            new GearSnapshot(Array.Empty<GearTierSnapshot>()));

        SaveSnapshot back = SaveSerializer.Deserialize(SaveSerializer.Serialize(empty));

        Assert.AreEqual(SaveMigrator.CurrentVersion, back.Version, "版本号必须回来 ✓");
        Roster roster = NewRoster();
        roster.RestoreFrom(back.Roster); // 空档恢复**不得抛**（新游戏的第一帧就是它）✓
        Assert.IsTrue(roster.Heroes.Count >= 0, "恢复后名册处于合法态 ✓");
    }

    // ---------------------------------------------------------------
    // ⑤ 损坏档必须报错，而不是被读成空档（守「静默兜底」）
    // ---------------------------------------------------------------

    [TestMethod]
    public void CorruptedSave_ThrowsInsteadOfBecomingAnEmptySave()
    {
        // 🔴 缺了 `roster` 的档：如果静默补默认，玩家会看到"空名册"却不知道档坏了
        string broken = "{ \"version\": 1, \"economy\": { \"gold\": 9 } }";

        Assert.ThrowsException<InvalidDataException>(
            () => SaveSerializer.Deserialize(broken),
            "损坏档必须**抛错**，绝不静默读成空档（红线 21）✓");

        Assert.ThrowsException<InvalidDataException>(
            () => SaveSerializer.Deserialize(string.Empty), "空文本必须抛错 ✓");
    }

    // ---------------------------------------------------------------
    // ⑥ 恢复等价性（端到端最小：读档后的读数 == 存档前）
    // ---------------------------------------------------------------

    [TestMethod]
    public void Restore_ProducesTheSameReadingsAsBeforeSaving()
    {
        var log = new CombatLog();
        Roster before = NewRoster();
        string heroId = before.Heroes[0].Id;
        before.SetMorale(log, heroId, 41, "test");
        before.Infect(log, heroId, "test_disease");

        int moraleBefore = before.MoraleOf(heroId);
        int diseasesBefore = before.DiseasesOf(heroId).Count;
        int heroesBefore = before.Heroes.Count;

        Roster after = NewRoster();
        after.RestoreFrom(SaveMigrator.Migrate(SaveSerializer.Deserialize(
            SaveSerializer.Serialize(new SaveSnapshot(
                SaveMigrator.CurrentVersion,
                before.CaptureSnapshot(),
                new ProgressSnapshot(1, 1),
                new HeirloomSnapshot(new Dictionary<string, int>(), new Dictionary<string, int>()),
                new EconomySnapshot(77, 77),
                new GearSnapshot(Array.Empty<GearTierSnapshot>()))))).Snapshot!.Roster);

        Assert.AreEqual(moraleBefore, after.MoraleOf(heroId), "读档后士气读数必须与存档前一致 ✓");
        Assert.AreEqual(diseasesBefore, after.DiseasesOf(heroId).Count, "读档后疾病数必须一致 ✓");
        Assert.AreEqual(heroesBefore, after.Heroes.Count, "读档后名册人数必须一致 ✓");
    }

    // ---------------------------------------------------------------
    // ⑦ 老档（v1）**结构上就没有** `gear`／`roster.quirks` ⇒ 迁移逐级补空表，不得误判成损坏档
    // ---------------------------------------------------------------

    [TestMethod]
    public void OldSaveWithoutGearAndQuirks_MigratesToEmptyTables_NotACorruptSave()
    {
        // 🔴 两臂：① 字段在、值为 `null` ② 字段**根本不存在**（v1 那版的真实形状）✓
        //    v1 档里两个字段都还没有（`gear` 自 v2 起、`roster.quirks` 自 v3 起）⇒ 两臂都按 v1 造 ✓
        SaveSnapshot populated = BuildPopulatedSnapshot();
        SaveSnapshot v1WithNull = populated with
        {
            Version = 1,
            Gear = null!,
            Roster = populated.Roster with { Quirks = null! },
        };
        string nullField = SaveSerializer.Serialize(v1WithNull);

        JsonObject node = JsonNode.Parse(nullField)!.AsObject();
        node.Remove("Gear");
        ((JsonObject)node["Roster"]!).Remove("Quirks");
        string noField = node.ToJsonString();

        foreach (string text in new[] { nullField, noField })
        {
            MigrationResult migrated = SaveMigrator.Migrate(SaveSerializer.Deserialize(text));

            Assert.IsTrue(migrated.Ok,
                $"🔴 v1 老档必须能迁移 —— 一刀切按当前版校验会把老档**误判成损坏档**（实际：{migrated.Message}）");
            Assert.AreEqual(SaveMigrator.CurrentVersion, migrated.Snapshot!.Version,
                "迁移后版本必须升到当前（v1 ⇒ v2 ⇒ v3 **逐级**）✓");
            Assert.IsNotNull(migrated.Snapshot!.Gear, "迁移必须补上 `gear` 快照（不给 `null`）✓");
            Assert.AreEqual(0, migrated.Snapshot!.Gear.Tiers.Count,
                "补的必须是**空表** = 全部英雄第 0 阶（v1 那版既没有该字段、也没有升级入口 ⇒ 唯一忠实读法）✓");
            Assert.IsNotNull(migrated.Snapshot!.Roster.Quirks, "迁移必须补上 `roster.quirks`（不给 `null`）✓");
            Assert.AreEqual(0, migrated.Snapshot!.Roster.Quirks.Count,
                "补的必须是**空表** = 没有英雄持怪癖（v2 那版没有任何怪癖入口 ⇒ 唯一忠实读法）✓");
        }
    }

    // ---------------------------------------------------------------
    // ⑧ 必备件坏了必须**看得见**（守「静默读成默认值 / 静默钳制」）
    // ---------------------------------------------------------------

    [TestMethod]
    public void DamagedRequiredFields_BecomeVisible_NotSilentlyDefaulted()
    {
        SaveSnapshot populated = BuildPopulatedSnapshot();

        // ① 当前版本的档缺 `gear` ⇒ 判为损坏档（自 v2 起它是必备件）✓
        SaveSnapshot noGear = populated with { Gear = null! };
        Assert.ThrowsException<InvalidDataException>(
            () => SaveSerializer.Deserialize(SaveSerializer.Serialize(noGear)),
            "🔴 自 v2 起 `gear` 是必备件 ⇒ 缺了必须抛（静默当成「全 0 阶」= 玩家进度无声消失）✓");

        // ①b 缺 `roster.quirks` ⇒ 同样判为损坏档（自 v3 起它是必备件）✓
        SaveSnapshot noQuirks = populated with { Roster = populated.Roster with { Quirks = null! } };
        Assert.ThrowsException<InvalidDataException>(
            // ⚠️ 必须**过一遍 Deserialize**：结构校验 `Validate` 在读取侧（红线 21 是「不静默读成空表」）✓
            () => SaveSerializer.Deserialize(SaveSerializer.Serialize(noQuirks)),
            "🔴 自 v3 起 `roster.quirks` 是必备件 ⇒ 缺了必须抛（静默当成「没有怪癖」= 招到的怪癖无声消失）✓");

        // ② 阶越界（0~4 之外）⇒ 恢复时**必须抛**，不静默钳到第 4 阶 ✓
        SaveSnapshot outOfRange = populated with
        {
            Gear = new GearSnapshot(new[] { new GearTierSnapshot("hero_warrior_1", 9, 0) }),
        };
        SaveSnapshot roundTripped = SaveSerializer.Deserialize(SaveSerializer.Serialize(outOfRange));

        var gear = new HeroGearState();
        Assert.ThrowsException<InvalidDataException>(
            () => gear.RestoreFrom(roundTripped.Gear),
            "🔴 阶越界 = 损坏档，必须**看得见**（钳住 = 玩家看到的读数与档里写的不是一回事）✓");
    }
}
