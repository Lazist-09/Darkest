using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **布局基建的【自动判据】**（`ui_spec §14.5` / `#319`⑤ / 架构 `next_round §4.1`）——
/// 把"没有重叠、框不透明"从**"看起来还行"**变成**可断言** ✓
///
/// 两条判据（**必须遍历每个界面**：战斗 / 城池 / 角色详情 / 地图）：
/// 🔴 判据 1：**所有可见 `Label` 两两不相交**（`Rect2.Intersects` 全为假）；
/// 🔴 判据 2：**每个 `Panel` 的 `BgColor.a == 1.0`**（用户原话"框不能是透明的"直接可断言）。
///
/// 用法：`LayoutAudit.Check(rootNode)` ⇒ 返回 `(bool ok, string report)`；冒烟钩子 `--ui-audit` 打印它 ✓
/// </summary>
public static class LayoutAudit
{
    /// <summary>跑两条判据（递归遍历整棵子树）。</summary>
    public static (bool Ok, string Report) Check(Node root)
    {
        // 🔴 **覆盖层口径**：若存在"**满屏不透明**"的面板（模态浮层，如角色详情），
        //    则**只审该浮层内部** —— 否则浮层【下面】那些被遮住的 Label 会被算成"重叠"
        //    （实测：详情打开时报 16 对"重叠"，全都是 Hamlet 的 Label ⟷ 详情 Label ⇒ 那是**口径问题**，不是布局问题）✓
        Control? overlay = FindOpaqueFullScreenOverlay(root);
        Node scope = overlay ?? root;

        var labels = new List<(string Path, Rect2 Rect)>();
        var panels = new List<(string Path, Control Panel)>();
        Collect(scope, scope, labels, panels);

        var problems = new StringBuilder();

        // 判据 1：可见 Label 两两不相交
        int overlaps = 0;
        for (int i = 0; i < labels.Count; i++)
        {
            for (int j = i + 1; j < labels.Count; j++)
            {
                if (labels[i].Rect.Intersects(labels[j].Rect))
                {
                    overlaps++;
                    if (overlaps <= 6) // 报告前几条即可（防刷屏）
                    {
                        problems.Append($"\n  🔴 重叠：{labels[i].Path} ⟷ {labels[j].Path}");
                    }
                }
            }
        }

        // 判据 2：Panel **与 `PanelContainer`** 都必须不透明
        // ⚠️ 我第一版只收 `Panel` ⇒ **`PanelContainer` 完全没被检查** ⇒ §14 的容器化之后判据 2 **假通过** ⚠️
        //    （`PanelContainer` 继承自 `Container` 而不是 `Panel`）—— 这是"判据口径漏了一类"的典型
        int transparent = 0;
        foreach ((string path, Control panel) in panels)
        {
            StyleBox? box = panel.GetThemeStylebox("panel");
            float alpha = box is StyleBoxFlat flat ? flat.BgColor.A : -1f;
            if (box is not StyleBoxFlat || alpha < 1.0f)
            {
                transparent++;
                if (transparent <= 6)
                {
                    problems.Append($"\n  🔴 框透明/无样式：{path}（a={alpha:0.##}）");
                }
            }
        }

        bool ok = overlaps == 0 && transparent == 0;
        string scopeNote = overlay is null ? "（全界面）" : $"（**只审覆盖层 {overlay.Name}**）";
        string report = $"布局判据（{root.Name}）{scopeNote}：可见 Label {labels.Count} 个 ／ Panel+PC {panels.Count} 个　" +
                        $"重叠对 {overlaps} ／ 透明框 {transparent}　=> {(ok ? "✅ 通过" : "🔴 未通过")}" +
                        (ok ? string.Empty : problems.ToString());
        return (ok, report);
    }

    /// <summary>找"满屏且不透明"的面板（= 模态覆盖层）；没有则返回 `null`。</summary>
    private static Control? FindOpaqueFullScreenOverlay(Node root)
    {
        Control? found = null;
        foreach (Node child in Walk(root))
        {
            if (child is not Control ctl || !ctl.Visible)
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

    private static IEnumerable<Node> Walk(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node grand in Walk(child))
            {
                yield return grand;
            }
        }
    }

    private static void Collect(Node node, Node root, List<(string, Rect2)> labels, List<(string, Control)> panels)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Label label && label.Visible && !string.IsNullOrWhiteSpace(label.Text))
            {
                // Label 的可视矩形：全局坐标（跨父容器一致口径）✓
                labels.Add((Path(root, label), new Rect2(label.GlobalPosition, label.Size)));
            }

            // 🔴 `Panel` **与** `PanelContainer` 两类都要查（后者继承自 Container，不是 Panel）✓
            if (child is Panel or PanelContainer && child is Control ctl && ctl.Visible)
            {
                panels.Add((Path(root, ctl), ctl));
            }

            Collect(child, root, labels, panels);
        }
    }

    private static string Path(Node root, Node node)
    {
        var names = new List<string>();
        Node? cur = node;
        while (cur is not null && cur != root)
        {
            names.Insert(0, cur.Name.ToString());
            cur = cur.GetParent();
        }

        return string.Join("/", names.TakeLast(3)); // 只留末尾三段，够定位
    }
}
