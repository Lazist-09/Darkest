using System;

namespace Darkest.Data;

/// <summary>
/// 🔴 **C4（形态 B）· 冒烟步骤的分类器**（**零 Godot** ⇒ 可被 `Darkest.Tests` 直接单测 ✓）。
///
/// WHY 它必须在内核/数据层（这条是编译器教我的）：
///   我第一版把它写在 `Gameplay.Scene.SmokeScript` 里 ⇒ `Darkest.Tests` 报 `CS0234: 命名空间 Darkest.Gameplay 中不存在 Scene`
///   ⇒ 即：**测试工程引用不到 Godot 侧** ✓ ⇒ 凡要"可测"的逻辑，就必须待在**零 Godot** 的层里 ✓
///   （这与项目"内核零 Godot"的纪律同源 ✓）
///
/// WHY 需要分类：
///   现在冒烟步骤是【**场景根**】导向（`main`→`MainMenuRoot`、`map`→`BattleRoot`、`town`→`HamletRoot`）✓
///   形态 B 下**战斗不再是场景** ⇒ 步骤要能指向**面板** ✓ ⇒ 所以需要"这一步属于哪一类" ✓
///   🔴 本件**只分类**（不执行、不改导航）⇒ 冒烟行为零改动 ✓
/// </summary>
public static class SmokeStepSpec
{
    /// <summary>步骤类型：场景导航 / 面板导航（S4 后用）/ 动作 / 未知 ✓</summary>
    public enum Kind
    {
        /// <summary>落在**场景根**上（`main:*` / `map:*` / `town` / `hover:*` / `row:*`）✓</summary>
        SceneNav,

        /// <summary>落在**面板**上（`panel:*` / `show:*`）—— 🔴 **S4 之后才接线**（当前无步骤用）✓</summary>
        PanelNav,

        /// <summary>场景内**动作**（`auto` / `camp` / `skill:*` / `finish` / `run-full` / `curio:*` …）✓</summary>
        Action,

        /// <summary>未知 ⇒ 由执行侧报错退出（**不许静默跳过** ✓）</summary>
        Unknown,
    }

    /// <summary>纯分类（判据顺序：面板前缀 → 动作集 → 场景导航集 → 未知 ✓）</summary>
    public static Kind Classify(string step)
    {
        string s = (step ?? "").Trim();
        if (s.Length == 0)
        {
            return Kind.Unknown;
        }

        if (s.StartsWith("panel:", StringComparison.Ordinal) || s.StartsWith("show:", StringComparison.Ordinal))
        {
            return Kind.PanelNav;
        }

        if (s is "auto" or "camp" or "finish" or "run-full" or "quit" or "back" or "embark"
            || s.StartsWith("skill:", StringComparison.Ordinal)
            || s.StartsWith("curio:", StringComparison.Ordinal))
        {
            return Kind.Action;
        }

        if (s.StartsWith("main:", StringComparison.Ordinal)
            || s.StartsWith("map:", StringComparison.Ordinal)
            || s.StartsWith("hover:", StringComparison.Ordinal)
            || s.StartsWith("row:", StringComparison.Ordinal)
            || s == "town")
        {
            return Kind.SceneNav;
        }

        return Kind.Unknown;
    }

    /// <summary>一行的可读描述（给冒烟仪表打印用 ✓；**不改变任何执行路径** ✓）</summary>
    public static string Describe(string step) => $"{step}（{Classify(step)}）";
}
