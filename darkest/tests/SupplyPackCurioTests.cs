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
/// 🔴 **`support_pack` 数据落地**（策划 `#329`／催办 `#332`）：
/// 补给箱（`cur_supply_crate`）空手 ⇒ `50% +2 口粮 ／ 15% +1 support_pack ／ 35% 空` ✓
/// **为什么值得**：那是一条【两端已就绪、只差中间一格数据】的机制 ——
///   UI 端（入口 + 可用性 + 置灰 + tooltip）＋ 内核端（`TryUseSupportPack` / `TryUseSupportPackForSp`）
///   ⇒ 缺的就是 `curios.json` 这一档；没有它那个按钮**永远置灰** ⚠️
/// ⚠️ **权重属数值**（`#307` 冻结中）⇒ 由策划裁定后落为**占位**（数据里标 `placeholder: true`）✓
/// 本用例锁：**改了它会出 support_pack**（P26 "改它就变" 的同一形状）✓
/// </summary>
[TestClass]
public sealed class SupplyPackCurioTests
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

    private static CuriosConfig Curios() => CuriosConfig.Parse(ReadData("curios.json"));

    [TestMethod]
    public void SupplyCrate_BareHands_HasTheSupportPackRow_AndSumsTo100()
    {
        CurioConfig crate = Curios().Get("cur_supply_crate")!;
        Assert.AreEqual(100, crate.BareHands.Sum(b => b.Chance), "空手概率和必须恰 100（P26 / #272）✓");

        CurioBareResultConfig pack = crate.BareHands.First(b => b.Kind == "support_pack");
        Assert.AreEqual(15, pack.Chance, "15% 支援包（策划 #329）✓");
        Assert.AreEqual(1, pack.Amount, "一只 ✓");
        Assert.IsTrue(pack.Placeholder, "权重属数值（#307）⇒ 已标 placeholder 以便追溯 ✓");
        Assert.IsTrue(CuriosConfig.ConsumedKinds.Contains("support_pack"),
            "`support_pack` 必须在【已实现 kind】白名单里（否则解析直接拒绝加载）✓");
        Assert.IsFalse(CuriosConfig.DeferredKinds.Contains("support_pack"), "它**不是**阶段二的未接线 kind ✓");
    }

    [TestMethod]
    public void ResolveBare_ReturnsSupportPack_ForTheMiddleRollBand()
    {
        CurioConfig crate = Curios().Get("cur_supply_crate")!;
        var log = new CombatLog();

        // 50/15/35 的累计带（🔴 `NextPercent()` 是 **0..100** 刻度，不是 0..1）：roll < 50 ⇒ food；50 ≤ roll < 65 ⇒ support_pack；≥ 65 ⇒ none ✓
        CurioOutcome pack = CurioResolver.ResolveBare(crate, new LocalRng(55.0), log);
        Assert.AreEqual("support_pack", pack.Kind, "🔴 改了它会出 support_pack ✓");
        Assert.AreEqual(1, pack.Amount);
        Assert.IsFalse(pack.Deferred, "已接线 ⇒ 不是 Deferred ✓");

        Assert.AreEqual("food", CurioResolver.ResolveBare(crate, new LocalRng(10.0), log).Kind, "低 band 仍是口粮 ✓");
        Assert.AreEqual("none", CurioResolver.ResolveBare(crate, new LocalRng(90.0), log).Kind, "高 band 仍是空 ✓");
        Assert.IsTrue(log.Events.OfType<RngDraw>().Any(), "空手必须写 RngDraw（#313 ⑤ / V5）✓");
    }

    /// <summary>本地固定值 RNG（本用例专用；🔴 `DrawCount` 是 **`ulong`** —— 我第一版写成 `int`，编译不过 ✓）</summary>
    private sealed class LocalRng(double value) : IRngProvider
    {
        public ulong DrawCount { get; private set; }

        public double NextPercent()
        {
            DrawCount++;
            return value;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            DrawCount++;
            return minInclusive;
        }

        public double NextDouble()
        {
            DrawCount++;
            return value;
        }
    }
}
