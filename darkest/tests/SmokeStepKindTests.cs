using System;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **C4（形态 B）· 冒烟步骤类型化**的纯分类器用例。
///
/// 为什么需要它：现在步骤是【**场景根**】导向（`main`→`MainMenuRoot`、`map`→`BattleRoot`、`town`→`HamletRoot`）；
///   形态 B 下**战斗不再是场景** ⇒ 步骤要能指向**面板** ⇒ 所以需要"这一步属于哪一类"这个信息 ✓
///
/// 🔴 本件**只测分类器**（纯函数 · 不碰 Godot）✓；**`Applies` 一字未改** ⇒ 导航行为零改动 ✓
/// </summary>
[TestClass]
public sealed class SmokeStepKindTests
{
    [TestMethod]
    public void ClassifiesSceneNavigationActionsPanelsAndUnknown()
    {
        // 场景导航（Applies 里被派发到某个场景根的步骤 ✓）
        foreach (string s in new[] { "main:0", "main:1", "map:2", "town", "hover:tavern", "row:0" })
        {
            Assert.AreEqual(SmokeStepSpec.Kind.SceneNav, SmokeStepSpec.Classify(s), $"{s} 应为场景导航 ✓");
        }

        // 场景内动作 ✓
        foreach (string s in new[] { "auto", "camp", "finish", "run-full", "quit", "back", "embark", "skill:1", "curio:leave" })
        {
            Assert.AreEqual(SmokeStepSpec.Kind.Action, SmokeStepSpec.Classify(s), $"{s} 应为动作 ✓");
        }

        // 🔴 面板步骤（**S4 之后才接线**；现在只识别 ✓）
        foreach (string s in new[] { "panel:hamlet", "show:roster" })
        {
            Assert.AreEqual(SmokeStepSpec.Kind.PanelNav, SmokeStepSpec.Classify(s), $"{s} 应为面板导航（S4 后接线）✓");
        }

        // 未知 ⇒ 交给 Apply 报错退出（**不许静默跳过** ✓）
        foreach (string s in new[] { "", "  ", "nonsense", "main" })
        {
            Assert.AreEqual(SmokeStepSpec.Kind.Unknown, SmokeStepSpec.Classify(s), $"\"{s}\" 应为未知 ✓");
        }

        Console.WriteLine("[C4] 步骤分类器：场景导航 / 动作 / 面板（S4 后用）/ 未知 四类都判对 ✓");
        Console.WriteLine($"[C4] 例：{SmokeStepSpec.Describe("map:1")} · {SmokeStepSpec.Describe("panel:hamlet")} ✓");
        TestContext.WriteLine("[C4] KindOf 纯分类通过 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
