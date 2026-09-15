using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **英雄资产接口加载器**用例（`P31` · `hero_asset_interface.md` · 我认领的分工）。
///
/// ⚠️ **本文件刻意不用"多行原始字符串"**：上一轮我连续三次败在 C# `"""` 的缩进规则（`CS8999`）
///   ⇒ 改为 **结构化序列化**（`Dictionary`/数组 → `JsonSerializer.Serialize`）⇒ **样本是对象，不是字符串** ✓
///   好处：测的是【数据形状】，而不是我手写 JSON 的拼写 ✓
/// 📌 文件名带 `Loader` 是为了与上一版彻底分开（上一版已删）✓
/// </summary>
[TestClass]
public sealed class HeroAssetsLoaderTests
{
    private static Dictionary<string, object?> Slot(params string[] frames)
        => new() { ["frames"] = frames, ["anchor"] = new Dictionary<string, object?> { ["x"] = 0.5, ["y"] = 1.0 } };

    private static Dictionary<string, object?> AllFive() => new()
    {
        ["idle"] = Slot("sprite/idle.png"),
        ["combat"] = Slot("sprite/combat.png"),
        ["attack"] = Slot("sprite/attack.png"),
        ["defend"] = Slot("sprite/defend.png"),
        ["walk"] = Slot("sprite/walk.png"),
    };

    private static string Json(Dictionary<string, object?> actions, bool placeholder = false, string? source = null)
    {
        var root = new Dictionary<string, object?>
        {
            ["archetype"] = "warrior",
            ["actions"] = actions,
        };
        if (placeholder)
        {
            root["placeholder"] = true;
        }

        if (source is not null)
        {
            root["source"] = source;
        }

        return JsonSerializer.Serialize(root);
    }

    [TestMethod]
    public void H1_RequiredFiveSlots_WithExplicitAnchors_Parse()
    {
        HeroAssetsConfig cfg = HeroAssets.Parse(Json(AllFive()));
        Assert.AreEqual("warrior", cfg.Archetype);
        CollectionAssert.AreEqual(new[] { "sprite/idle.png" }, HeroAssets.FramesOf(cfg, "idle").ToArray(),
            "表现层取帧（已保证是引用）✓");
        Assert.IsFalse(cfg.Placeholder, "非占位 ⇒ 走正式根 ✓");
        Assert.AreEqual("res://assets/heroes/warrior", HeroAssets.RootFor(cfg), "正式资产根 = HeroesRoot/<archetype> ✓");
    }

    [TestMethod]
    public void H2_MissingRequiredSlot_Throws_AndNamesIt()
    {
        Dictionary<string, object?> actions = AllFive();
        actions.Remove("walk"); // 🔴 删掉一个动作 ⇒ 必须【报错并点名】（不是静默不画）
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => HeroAssets.Parse(Json(actions)), "缺 `walk` ⇒ 拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("walk"), "🔴 必须点名缺的那个槽（H2）✓");
    }

    [TestMethod]
    public void UnknownSlotName_IsRejected_UnlessItIsASkillSlot()
    {
        Dictionary<string, object?> bad = AllFive();
        bad["dance"] = Slot("sprite/dance.png");
        Assert.ThrowsException<InvalidDataException>(() => HeroAssets.Parse(Json(bad)),
            "槽名不在固定 12 槽表 ⇒ 拒绝（`P31` ②）✓");

        Dictionary<string, object?> ok = AllFive();
        ok["skill.grape"] = Slot("sprite/grape.png");
        Assert.IsTrue(HeroAssets.Parse(Json(ok)).Actions.ContainsKey("skill.grape"),
            "`skill.<名>`（技能类按该英雄技能表命名）⇒ 允许 ✓");
    }

    [TestMethod]
    public void FramesMustBeReferences_NotInlineData()
    {
        Dictionary<string, object?> b64 = AllFive();
        b64["battlefield"] = Slot("data:image/png;base64,iVBORw0KGgoAAAANSUhEUg");
        Assert.ThrowsException<InvalidDataException>(() => HeroAssets.Parse(Json(b64)),
            "内联 base64 ⇒ 拒绝（`P31` ②：`frames` 必须是引用）✓");

        Dictionary<string, object?> inline = AllFive();
        inline["camp"] = Slot("{\"w\": 64, \"h\": 64}");
        Assert.ThrowsException<InvalidDataException>(() => HeroAssets.Parse(Json(inline)),
            "把尺寸/帧数据拷进 hero.json ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void Anchor_MustBeExplicit()
    {
        Dictionary<string, object?> actions = AllFive();
        actions["defend"] = new Dictionary<string, object?> { ["frames"] = new[] { "sprite/defend.png" } }; // 无 anchor
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => HeroAssets.Parse(Json(actions)), "有帧但无锚点 ⇒ 拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("defend") && ex.Message.Contains("anchor"), "报错同时点名槽与 `anchor` ✓");
    }

    [TestMethod]
    public void DeclaredMissing_NeedsAReason_ButIsAllowedWhenGiven()
    {
        Dictionary<string, object?> noReason = AllFive();
        noReason["heroic"] = new Dictionary<string, object?> { ["frames"] = Array.Empty<string>() };
        Assert.ThrowsException<InvalidDataException>(() => HeroAssets.Parse(Json(noReason)),
            "空帧又没 `missing_reason` ⇒ 拒绝（不许静默留空）✓");

        Dictionary<string, object?> withReason = AllFive();
        withReason["heroic"] = new Dictionary<string, object?>
        {
            ["frames"] = Array.Empty<string>(),
            ["missing_reason"] = "本英雄无美德姿态（接口允许显式声明缺失）",
        };
        Assert.AreEqual(0, HeroAssets.FramesOf(HeroAssets.Parse(Json(withReason)), "heroic").Count,
            "显式声明缺失 ⇒ 取帧为空（表现层据此不画，但【原因在数据里】）✓");
    }

    [TestMethod]
    public void Placeholder_LoadsOnlyFromPlaceholderRoot_AndMustDeclareSource()
    {
        HeroAssetsConfig ok = HeroAssets.Parse(Json(AllFive(), placeholder: true, source: "workshop-mod（无授权 · 不发布）"));
        Assert.AreEqual(HeroAssets.PlaceholderRoot, HeroAssets.RootFor(ok),
            "🔴 占位 ⇒ **只从 `PlaceholderRoot` 加载**（「放对地方」是唯一能跑的路径 ✓）");

        Assert.ThrowsException<InvalidDataException>(
            () => HeroAssets.Parse(Json(AllFive(), placeholder: true)),
            "`placeholder = true` 却没写 `source` ⇒ 拒绝（合规：不发布 + 显式标注）✓");
    }
}
