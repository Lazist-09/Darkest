using System;
using System.Collections.Generic;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **C4 终态判据**（架构原话）：
///   "🔴 **恢复 autoload** + **终态一起做**：S4 后 **`main_scene` 改为 `ui_root.tscn` 并【撤掉 autoload】**
///    （否则两份外壳实例）⇒ **判据「外壳现在有几个实例？」必须是 `1`**"
///
/// WHY 需要它：这条判据**没法靠"冒烟绿"回答** ✗ —— 两份外壳实例也能跑绿 ✓
///   ⇒ 所以要**数实例** + **看配置**（`main_scene` / `autoload`）⇒ 三者一起才说明问题 ✓
///
/// 🔴 **零行为**：只在显式 `--shell-count` 时运行（我域旗标 ✓）
/// </summary>
public static class ShellInstanceAudit
{
    /// <summary>从树根数一遍 `UIRoot` 实例（**含 autoload 与场景两份都会数到** ✓）</summary>
    public static List<string> CollectShellPaths(Node anyNode)
    {
        var found = new List<string>();
        Node? root = anyNode.GetTree()?.Root;
        if (root is null)
        {
            return found;
        }

        Walk(root, found);
        return found;

        static void Walk(Node n, List<string> acc)
        {
            if (n is Darkest.UI.UIRoot)
            {
                acc.Add(n.GetPath().ToString());
            }

            foreach (Node child in n.GetChildren())
            {
                Walk(child, acc);
            }
        }
    }

    /// <summary>跑一次判据：**实例数 + `main_scene` + autoload 开关** ⇒ 打印判定与修法顺序 ✓</summary>
    public static bool Run(Node anyNode)
    {
        List<string> shells = CollectShellPaths(anyNode);
        Variant mainScene = ProjectSettings.GetSetting("application/run/main_scene", "");
        Variant autoload = ProjectSettings.GetSetting("autoload/UIRoot", "");

        GD.Print($"[C4 判据] 外壳实例数 = **{shells.Count}**（判据要求 **1**）");
        foreach (string p in shells)
        {
            GD.Print($"[C4 判据]   · {p}");
        }

        GD.Print($"[C4 判据] `main_scene` = {mainScene}　·　`autoload/UIRoot` = {(string.IsNullOrEmpty(autoload.AsString()) ? "（未注册）" : autoload.AsString())}");

        bool one = shells.Count == 1;
        bool mainIsUiRoot = mainScene.AsString().EndsWith("ui_root.tscn", StringComparison.OrdinalIgnoreCase);
        bool noAutoload = string.IsNullOrEmpty(autoload.AsString());

        if (one && mainIsUiRoot && noAutoload)
        {
            GD.Print("[C4 判据] ✅ **终态达成**：1 份外壳 · `main_scene` = `ui_root.tscn` · autoload 已撤 ✓");
            return true;
        }

        GD.Print($"[C4 判据] 🔴 **未达终态**（实例 {shells.Count} / main_scene 是 ui_root={mainIsUiRoot} / 无 autoload={noAutoload}）");
        GD.Print("[C4 判据] ⇒ 修法顺序（架构给的口径）：① S4 落地（战斗成面板）② `main_scene` → `ui_root.tscn` "
            + "③ **撤掉 autoload** ④ 回来跑本条 ⇒ 应看到「实例数 = 1 · autoload 未注册」✓");
        return false;
    }
}
