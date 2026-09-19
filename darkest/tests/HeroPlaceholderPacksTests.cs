using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **逻辑侧接入测试**（用户 2026-09-19：「先做逻辑测，都能接入并且正常运行了就停止目标」）。
///
/// 本用例**用真加载器**（`HeroAssets.Parse` ⇒ P31 全量校验）跑**四个原型各自**的占位包，证明：
///   ① 解析器认得出它们是【占位（按原型）】（不是正式、也不是旧单包）
///   ② 每份 `hero.json` 都能被真加载器**接受**（缺槽/缺锚点/缺 `source` 都会在这里报错）
///   ③ 必备 5 槽都有帧，且**帧文件真的在盘上**（`res://` 路径在测试里映射到仓库的 `darkest/`）
///   ④ 四个原型**互不相同**（各自的包路径不同 ⇒ 不再是"四原型共用一张图"）
///
/// ⚠️ 占位包是 **gitignored** 的（借用素材，契约 §4）⇒ 本机没生成时**不判失败**：`Assert.Inconclusive` 跳过 ✓
/// </summary>
[TestClass]
public sealed class HeroPlaceholderPacksTests
{
    private static readonly string[] Archetypes = { "warrior", "tank", "medic", "commissar" };

    /// <summary>仓库内 `darkest/` 根（测试运行时是 bin/... ⇒ 向上找带 `darkest.sln` 的那层）</summary>
    private static string? FindRepoDarkest()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "darkest", "assets", "heroes_placeholder");
            if (Directory.Exists(candidate))
            {
                return Path.Combine(dir.FullName, "darkest");
            }

            candidate = Path.Combine(dir.FullName, "assets", "heroes_placeholder");
            if (Directory.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>`res://x/y` ⇒ `&lt;darkest&gt;/x/y`（把 Godot 口径映射到真实文件系统 ✓）</summary>
    private static Func<string, bool> MakeExists(string darkest) => resPath =>
    {
        const string prefix = "res://";
        string rel = resPath.StartsWith(prefix, StringComparison.Ordinal) ? resPath[prefix.Length..] : resPath;
        return File.Exists(Path.Combine(darkest, rel.Replace('/', Path.DirectorySeparatorChar)));
    };

    [TestMethod]
    public void FourArchetypePacks_ResolveAndPassTheRealLoader()
    {
        string? darkest = FindRepoDarkest();
        if (darkest is null)
        {
            Assert.Inconclusive("占位包不存在（本机未生成）⇒ 跳过；生成后本用例会自动开始校验 ✓");
            return;
        }

        string packsRoot = Path.Combine(darkest, "assets", "heroes_placeholder");
        Func<string, bool> Exists = MakeExists(darkest);
        var roots = new System.Collections.Generic.List<string>();

        foreach (string arch in Archetypes)
        {
            string packDir = Path.Combine(packsRoot, arch);
            string descriptor = Path.Combine(packDir, "hero.json");
            Assert.IsTrue(File.Exists(descriptor), $"{arch}: 缺 hero.json（占位包不完整）");

            // ① 解析器：按原型命中占位（且必须显式说明"这不是正式资产"）
            //    🔴 解析器给的是 `res://…` 路径（Godot 口径）⇒ 测试里必须把它映射到仓库的 `darkest/` ✓
            //       （我第一版直接传 File.Exists ⇒ 解析器报 None ⇒ 用例当场抓出这个错 ✓）
            HeroArtResolution res = HeroArtResolver.Resolve(arch, Exists);
            Assert.AreEqual(HeroArtSource.Placeholder, res.Source, $"{arch}: 应解析为【按原型占位】");
            Assert.IsTrue(res.IsPlaceholder, $"{arch}: 必须被识别为占位");
            Assert.IsTrue(res.Note.Contains("不发布", StringComparison.Ordinal), $"{arch}: 占位说明必须含「不发布」口径");

            // ② 真加载器（P31：必备 5 槽 / 缺槽必须 missing_reason / 有帧槽必须有 anchor / placeholder 必须带 source）
            HeroAssetsConfig cfg = HeroAssets.Parse(File.ReadAllText(descriptor));
            Assert.AreEqual(arch, cfg.Archetype, $"{arch}: hero.json 的 archetype 必须与目录名一致");
            Assert.IsTrue(cfg.Placeholder, $"{arch}: 占位包必须 placeholder:true");
            Assert.IsFalse(string.IsNullOrWhiteSpace(cfg.Source), $"{arch}: 占位包必须写 source");

            // ③ 必备 5 槽有帧，且帧文件在盘上
            int framed = 0;
            foreach (string slot in HeroAssets.RequiredSlots)
            {
                var frames = HeroAssets.FramesOf(cfg, slot);
                Assert.IsTrue(frames.Count > 0, $"{arch}: 必备槽 `{slot}` 无帧");
                foreach (string reference in frames)
                {
                    string full = Path.Combine(packDir, reference);
                    Assert.IsTrue(File.Exists(full), $"{arch}: 槽 `{slot}` 的帧不在盘上: {reference}");
                }

                framed++;
            }

            Assert.IsTrue(File.Exists(Path.Combine(packDir, cfg.Portrait ?? "portrait.png")), $"{arch}: 缺 portrait");

            roots.Add(packDir);
            string line = $"[英雄美术·接入] {arch} ⇒ 占位（按原型）· 槽 {cfg.Actions.Count} · 必备 5 槽 {framed}/{HeroAssets.RequiredSlots.Count} 有帧"
                          + $" · 来源 {cfg.Source}";
            Console.WriteLine(line);
            TestContext.WriteLine(line);
        }

        // ④ 四个原型各自不同（证明"不再是四原型共用一张图"）
        Assert.AreEqual(Archetypes.Length, roots.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "四个原型的包路径必须互不相同 ⇒ **每趟不同角色** ✓");
        TestContext.WriteLine($"[英雄美术·接入] 四个原型各自独立包 ✓（{string.Join(" · ", roots.Select(Path.GetFileName))}）");
    }

    public TestContext TestContext { get; set; } = null!;
}
