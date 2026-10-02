// ① 来源：从 `LayoutAudit.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **覆盖层口径／例外留痕／读数格式化**（`AlphaOf` 原 :207-212 ＋ `Fmt` 原 :214-216 ＋ `FindOpaqueFullScreenOverlay` 原 :218-256 ＋
//    `SkipNote` 原 :295-297 ＋ `SkipLog` 原 :299-322；M11 ① 预警面第二十六件·第二片）✓
// ② 职责：回答「审计范围是谁」（模态覆盖层判定 · 只用能画底的 `Panel`／`PanelContainer`）与「跳过了什么」（红线 21：例外必须可审计）✓
// ③ 🔴 依赖（实测扫描本片）：`Godot`（`Control`／`StyleBox`／`StyleBoxFlat`／`Rect2`／`Node`）＋ `System.Collections.Generic`（`List<string>`）；
//    跨片仅**调用** `Walk`／`CountDescendants`（`Traversal` 片）；反向仅**类型引用** ⇒ 两片互引但**无递归调用环** ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 2 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Godot;

namespace Darkest.UI;

public static partial class LayoutAudit
{

    /// <summary>数某控件的 `panel` 样式不透明度（诊断用；无样式 ⇒ -1）。</summary>
    private static float AlphaOf(Control ctl)
    {
        StyleBox? box = ctl.GetThemeStylebox("panel");
        return box is StyleBoxFlat flat ? flat.BgColor.A : -1f;
    }

    /// <summary>矩形格式化（诊断用）：`pos=(x,y) size=(w,h)`。</summary>
    private static string Fmt(Rect2 r)
        => $"pos=({r.Position.X:0},{r.Position.Y:0}) size=({r.Size.X:0},{r.Size.Y:0})";

    /// <summary>找"满屏且不透明"的面板（= 模态覆盖层）；没有则返回 `null`。</summary>
    /// <remarks>
    /// 🔴 **必须是【能画底的 Panel 系】节点**（`Panel` / `PanelContainer` / `PopupPanel`）：
    /// 实测教训 —— `MarginContainer` / `VBoxContainer` 这类**纯布局容器不画背景**，
    /// 它们**遮不住任何东西**；若把它们当"模态覆盖层"，审计范围会被悄悄缩到一个小子树
    /// ⇒ 报"✅ 通过"却是**假通过**（战斗屏实测：只审 10 个 Label，全场景 59 个，49 个在范围外）⚠️
    /// </remarks>
    private static Control? FindOpaqueFullScreenOverlay(Node root)
    {
        Control? found = null;
        foreach (Node child in Walk(root))
        {
            if (child is not Control ctl || !ctl.IsVisibleInTree())
            {
                continue;
            }

            // 🔴 只有"能画不透明底"的类才可能是模态浮层（**纯布局容器一律排除**）
            //    ⚠️ `PopupPanel` 是 `Window` 系、不是 `Control`（Walk 里根本走不到它）⇒ 这里只判 `Panel` / `PanelContainer`
            if (ctl is not (Panel or PanelContainer))
            {
                continue;
            }

            bool fullScreen = ctl.AnchorLeft == 0 && ctl.AnchorTop == 0 && ctl.AnchorRight == 1 && ctl.AnchorBottom == 1;
            if (!fullScreen)
            {
                continue;
            }

            StyleBox? box = ctl.GetThemeStylebox("panel");
            if (box is StyleBoxFlat { BgColor.A: >= 1.0f })
            {
                found = ctl; // 取最后一个（最上层）
            }
        }

        return found;
    }

    /// <summary>子窗口样本（诊断用；空 ⇒ 如实写「无」）✓</summary>
    private static string SkipNote(SkipLog log)
        => log.WindowNames.Count == 0 ? "无" : string.Join(" ／ ", log.WindowNames);

    /// <summary>跳过留痕（红线 21：**例外必须可审计**）✓</summary>
    private sealed class SkipLog
    {
        /// <summary>跳过的瞬态元素个数（含其子树）✓</summary>
        public int Transient;

        /// <summary>🔴 **被裁剪掉的 Label 个数**（clip_contents 口径：裁到空的像素没画在屏幕上 ⇒ 不参与重叠判据）✓</summary>
        public int Clipped;

        /// <summary>跳过的子窗口个数（含其子树）✓</summary>
        public int Windows;

        /// <summary>子窗口名字样本（最多 3 个 ⇒ 报告里能看出到底跳过了谁）✓</summary>
        public readonly List<string> WindowNames = new();

        public void AddWindow(Node window)
        {
            Windows += 1 + CountDescendants(window);
            if (WindowNames.Count < 3)
            {
                WindowNames.Add($"{window.Name}({window.GetType().Name})");
            }
        }
    }
}
