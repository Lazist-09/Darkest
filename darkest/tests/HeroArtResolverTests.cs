using System;
using System.Collections.Generic;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **英雄美术资产的运行时解析**（用户 2026-09-19：把 mod 英雄接进来**替换现有内容**）。
/// 本用例钉住三条路径（正式优先 / 只占位 / 都没有），并**证明"占位不会被误当正式"** ✓
///
/// 背景（实测）：`warrior`/`tank`/`medic`/`commissar` 此前**共用同一张占位图** ——
///   因为 `HeroArt`（UI）硬编码 `PlaceholderRoot` 且 `RootFor`/`FramesOf` 只有用例在调 ⚠️
/// </summary>
[TestClass]
public sealed class HeroArtResolverTests
{
    /// <summary>假文件系统：只认给定的路径集合（内核可测、无需 Godot ✓）</summary>
    private static Func<string, bool> Fs(params string[] files)
    {
        var set = new HashSet<string>(files, StringComparer.Ordinal);
        return p => set.Contains(p);
    }

    [TestMethod]
    public void FormalAssetWins_WhenItExists()
    {
        HeroArtResolution r = HeroArtResolver.Resolve("warrior", Fs(
            $"{HeroArtResolver.FormalRoot}/warrior/hero.json",
            $"{HeroArtResolver.PlaceholderRoot}/warrior/hero.json",
            $"{HeroArtResolver.PlaceholderRoot}/hero.json"));

        Assert.AreEqual(HeroArtSource.Formal, r.Source, "正式资产存在 ⇒ 必须优先用它 ✓");
        Assert.IsFalse(r.IsPlaceholder, "正式 ⇒ **不得**标成占位 ✓");
        Assert.AreEqual($"{HeroArtResolver.FormalRoot}/warrior", r.Root, "根 = 描述符所在目录（帧引用相对它解析）✓");
        Console.WriteLine($"[英雄资产] warrior ⇒ 正式（{r.ConfigPath}）✓");
        TestContext.WriteLine($"[英雄资产] 正式优先：{r.Source} / root={r.Root} ✓");
    }

    [TestMethod]
    public void FallsBackToPerArchetypePlaceholder_AndSaysSo()
    {
        HeroArtResolution r = HeroArtResolver.Resolve("tank", Fs(
            $"{HeroArtResolver.PlaceholderRoot}/tank/hero.json",
            $"{HeroArtResolver.PlaceholderRoot}/hero.json"));

        Assert.AreEqual(HeroArtSource.Placeholder, r.Source, "正式不存在 ⇒ 回落【按原型】占位 ✓");
        Assert.IsTrue(r.IsPlaceholder, "必须被识别为占位 ✓");
        Assert.AreEqual($"{HeroArtResolver.PlaceholderRoot}/tank", r.Root);
        StringAssert.Contains(r.Note, "不是", "🔴 占位必须**显式说明「这不是该原型的正式资产」**（不静默 ✓）");
        StringAssert.Contains(r.Note, "不发布", "🔴 占位必须带「不发布」口径 ✓");
        Console.WriteLine($"[英雄资产] tank ⇒ 占位（按原型）：{r.Note}");
        TestContext.WriteLine($"[英雄资产] 按原型占位：{r.Source} ✓");
    }

    [TestMethod]
    public void FallsBackToLegacySinglePack_ThenToNone()
    {
        HeroArtResolution legacy = HeroArtResolver.Resolve("medic", Fs(
            $"{HeroArtResolver.PlaceholderRoot}/hero.json"));
        Assert.AreEqual(HeroArtSource.PlaceholderLegacy, legacy.Source, "只有旧单包 ⇒ 用它（兼容早期「1 个英雄验证接口」✓）");
        StringAssert.Contains(legacy.Note, "多原型共用", "旧单包必须点名「多原型共用一份」这个事实 ✓");

        HeroArtResolution none = HeroArtResolver.Resolve("commissar", Fs());
        Assert.AreEqual(HeroArtSource.None, none.Source, "都没有 ⇒ None（调用方回落色块+首字 ✓）");
        Assert.IsFalse(none.Found);
        StringAssert.Contains(none.Note, "回落色块", "None 必须给「回落」口径（不静默 ✓）");
        Console.WriteLine($"[英雄资产] 旧单包 ⇒ {legacy.Source}；都没有 ⇒ {none.Source}（{none.Note}）✓");
        TestContext.WriteLine($"[英雄资产] 旧单包/都没有两条路径 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
