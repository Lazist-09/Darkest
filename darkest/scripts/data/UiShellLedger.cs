using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Data;

/// <summary>
/// 🔴 **B-3「回落必须留痕」的可数一半**（架构 B-3）：
///   架构原话：「**回落必须留痕**（每次回落打印 ⇒ "**还差哪几屏没面板化**"可数）」✓
///
/// 本件做的就是那个"**可数**"：
///   每块屏在形态 B 下的去向 = ① `Panel`（已面板化 ✓）② `SceneFallback`（回落成场景切换 ⚠️）③ `NotWired`（还没接 ✗）
///   ⇒ 于是"还差哪几屏" = `SceneFallback` + `NotWired` 的那几屏 ✓ 一句话答得出来 ✓
///
/// 🔴 **为什么放在数据层（零 Godot）**：同 `SmokeStepSpec` —— 测试工程引用不到 Godot 侧，
///   凡要可测的逻辑必须零 Godot ✓（我上一轮被编译器教过这条 ✓）
///
/// ⚠️ **激活条件（我按自查表的承诺，写明谁会用、什么时候）**：
///   本账本目前**只有测试在用** ⇒ 真正的数据来自两处调用点：
///     ① `BattleRoot.GoToHamlet`（**在飞 `M`**）⇒ 面板成功/回落失败时各记一笔 ✓
///     ② `UIRoot.ShowPanel` 的失败分支（**UI 域**）⇒ 装载失败时记 `NotWired` ✓
///   ⇒ 这两处接线后，本账本才会产出**真实读数** ✓（我不擅自去改那两处：一个在飞、一个属 UI ✓）
/// </summary>
public static class UiShellLedger
{
    /// <summary>一块屏在形态 B 下的去向 ✓</summary>
    public enum ShellRoute
    {
        /// <summary>已面板化（走外壳 `ShowPanel` 成功）✓</summary>
        Panel,

        /// <summary>⚠️ **回落**成场景切换（外壳缺失/不可用）—— 这就是"还差哪几屏"里的一类 ✓</summary>
        SceneFallback,

        /// <summary>✗ **还没接线**（既没面板、也没回落）—— 另一类"还差" ✓</summary>
        NotWired,
    }

    private static readonly Dictionary<string, ShellRoute> Routes = new(StringComparer.Ordinal);

    /// <summary>记一笔（同一屏**后来居上**：后面测到更准确的状态会覆盖前一条 ✓）</summary>
    public static void Record(string screen, ShellRoute route)
    {
        if (!string.IsNullOrWhiteSpace(screen))
        {
            Routes[screen] = route;
        }
    }

    /// <summary>清空（**给测试隔离用** ✓；生产路径不该调它 ✓）</summary>
    public static void Reset() => Routes.Clear();

    /// <summary>已记录屏数 ✓</summary>
    public static int Count => Routes.Count;

    /// <summary>按去向计数 ✓</summary>
    public static int CountOf(ShellRoute route) => Routes.Values.Count(v => v == route);

    /// <summary>
    /// 🔴 **架构要的那句话**："还差哪几屏没面板化" = 回落 + 未接线的那几屏 ✓
    /// </summary>
    public static IReadOnlyList<string> NotPanelizedScreens =>
        Routes.Where(kv => kv.Value != ShellRoute.Panel).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    /// <summary>一行可读汇总（给冒烟/日志用 ✓）</summary>
    public static string Report()
    {
        int panel = CountOf(ShellRoute.Panel);
        int fallback = CountOf(ShellRoute.SceneFallback);
        int missing = CountOf(ShellRoute.NotWired);
        string tail = NotPanelizedScreens.Count == 0
            ? "还差 0 屏 ✓"
            : "还差 " + NotPanelizedScreens.Count + " 屏：" + string.Join(", ", NotPanelizedScreens);
        return $"[外壳账本] 已记 {Count} 屏 ⇒ 面板 {panel} · 回落 {fallback} · 未接线 {missing}　{tail} ✓";
    }
}
