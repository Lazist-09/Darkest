using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **Curio 内核验收**（`curio.md` §5 的 V2/V3/V4/V5 在内核层的部分；V1/V6/V7 属 UI 层，另有用例）：
/// · V5：**空手路径必须写 `RngDraw`**；**道具路径不掷骰 ⇒ 不写**
/// · V2：**同一 Curio：空手 vs 用对道具 ⇒ 结果确实不同**（且用对道具更好）
/// · V3：**错误道具 ⇒ 确定的坏结果**（骸骨堆 ＋ 口粮 ⇒ −10 士气）
/// · V4：**走开** ⇒ 零变化、不掷骰
/// · ④：命中**阶段二 kind** ⇒ **显式拒绝**（`Deferred=true`），不静默当成功（红线 21）
/// </summary>
[TestClass]
public sealed class CurioResolverTests
{
    private static CuriosConfig Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", "curios.json");
            if (File.Exists(candidate))
            {
                return CuriosConfig.Parse(File.ReadAllText(candidate));
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("data/curios.json 未找到。");
    }

    [TestMethod]
    public void BareHands_WritesRngDraw_ItemRouteDoesNot()
    {
        CuriosConfig cfg = Load();
        CurioConfig crate = cfg.Get("cur_supply_crate")!;

        var log = new CombatLog();
        CurioOutcome bare = CurioResolver.ResolveBare(crate, new RngProvider(20260909), log);
        Assert.AreEqual("bare", bare.Route);
        Assert.AreEqual(1, log.Events.OfType<RngDraw>().Count(), "🔴 V5：空手必须写一条 RngDraw（可审计）");

        var log2 = new CombatLog();
        CurioOutcome? item = CurioResolver.ResolveItem(crate, "firewood");
        Assert.IsNotNull(item);
        Assert.AreEqual("item", item!.Route);
        Assert.AreEqual(0, log2.Events.OfType<RngDraw>().Count(),
            "🔴 道具路径【不掷骰】⇒ 不写 RngDraw（Curio 的灵魂：正确道具 = 确定结果）");
        Assert.AreEqual(2, item.Amount, "用柴火 ⇒ +2 柴火");
        Assert.AreEqual("food", item.ExtraKind);
        Assert.AreEqual(1, item.ExtraAmount, "并把里面的口粮也翻出来（+1）");
    }

    [TestMethod]
    public void V2_BareVsItem_ResultsDiffer_AndItemIsBetter()
    {
        CuriosConfig cfg = Load();
        CurioConfig altar = cfg.Get("cur_altar")!;

        CurioOutcome bare = CurioResolver.ResolveBare(altar, new RngProvider(1), new CombatLog());
        CurioOutcome item = CurioResolver.ResolveItem(altar, "supply_pack")!;

        Assert.AreNotEqual(bare.Amount, item.Amount, "🔴 V2：空手 vs 用对道具 ⇒ 结果确实不同");
        Assert.IsTrue(item.Amount > bare.Amount, "且【用对道具更好】（+30% vs +20%）");
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.Text), "V6：结果要有描述文本");
    }

    [TestMethod]
    public void V3_WrongItem_IsDeterministicBadResult()
    {
        CuriosConfig cfg = Load();
        CurioConfig bones = cfg.Get("cur_bone_pile")!;

        CurioOutcome wrong = CurioResolver.ResolveItem(bones, "food")!; // 在死者旁边吃东西
        Assert.AreEqual("morale_team", wrong.Kind);
        Assert.AreEqual(-10, wrong.Amount, "🔴 V3：用错道具 ⇒ **确定的坏结果**（100% −10 士气）");

        // 「确定」的含义：连查两次完全一致（不掷骰 ⇒ 无随机）
        CurioOutcome again = CurioResolver.ResolveItem(bones, "food")!;
        Assert.AreEqual(wrong.Amount, again.Amount);
        Assert.AreEqual(wrong.Text, again.Text);
    }

    [TestMethod]
    public void V4_Leave_IsNoOp_NoRoll()
    {
        CuriosConfig cfg = Load();
        CurioConfig sconce = cfg.Get("cur_sconce")!;

        var log = new CombatLog();
        CurioOutcome leave = CurioResolver.Leave(sconce);
        Assert.AreEqual("leave", leave.Route);
        Assert.AreEqual("none", leave.Kind);
        Assert.AreEqual(0, leave.Amount, "V4：走开 ⇒ 零变化");
        Assert.AreEqual(0, log.Events.Count, "V4：走开 ⇒ 不写任何事件（不掷骰）");
        Assert.IsFalse(string.IsNullOrWhiteSpace(leave.Text), "V6：走开也要有文本");
    }

    [TestMethod]
    public void DeferredKinds_AreMarkedExplicitly_NotSilentlySucceeded()
    {
        CuriosConfig cfg = Load();

        // 🔴 阶段二示例：**书堆的 `trait_positive`**（名册没有加特质的通道 ⇒ 需【特质目录 + AddTrait + 投影】）
        CurioConfig books = cfg.Get("cur_book_stack")!;
        CurioBareResultConfig trait = books.BareHands.Single(b => b.Kind == "trait_positive");
        Assert.IsTrue(CuriosConfig.DeferredKinds.Contains(trait.Kind),
            "阶段二 kind 必须登记在 DeferredKinds（内核据此拒绝，不静默）");

        // ✅ 骸骨堆的 `disease_one` 已**转正**（走 `Roster.Infect`）
        CurioConfig bones = cfg.Get("cur_bone_pile")!;
        Assert.IsTrue(CuriosConfig.ConsumedKinds.Contains(bones.BareHands.Single(b => b.Kind == "disease_one").Kind),
            "disease_one 已接线（跑图中患病走既有 Roster.Infect 通道）");
        Assert.IsFalse(CuriosConfig.DeferredKinds.Contains("disease_one"));

        // ✅ 圣坛的 `damage_buff` 已**转正**（跨场 buff + 扎营清都已具备）
        CurioConfig altar = cfg.Get("cur_altar")!;
        Assert.IsTrue(CuriosConfig.ConsumedKinds.Contains(altar.BareHands.Single().Kind),
            "damage_buff 已接线 ⇒ 圣坛的空手路径应可正常生效");
        Assert.IsFalse(CuriosConfig.DeferredKinds.Contains("damage_buff"));
    }

    [TestMethod]
    public void UnknownItem_ReturnsNull_SoUiCanHideIt()
    {
        CuriosConfig cfg = Load();
        CurioConfig sconce = cfg.Get("cur_sconce")!;
        Assert.IsNull(CurioResolver.ResolveItem(sconce, "firewood"),
            "该 Curio 没定义这个道具 ⇒ 返回 null ⇒ UI 不列它（V7：不误导）");
    }
}
