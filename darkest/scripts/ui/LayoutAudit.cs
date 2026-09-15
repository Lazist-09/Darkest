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
    /// <summary>
    /// 🔴 **瞬态特效层**（`ui_spec §12.1` 动效）的名字：判据**按口径跳过它**。
    /// 理由（口径说明，不是放水）：消散中的伤害数字/闪白/暗角**按设计**会短暂叠在卡片上 ——
    /// 那是**特效**，不是"布局重叠"；把它们算进判据只会让判据变噪声。
    /// ⚠️ 所以该层里的东西**必须真的是瞬态**（`UiMotion` 只写 `Modulate/Position` 且用完即 `QueueFree`），
    ///    且**必须 `MouseFilter = Ignore`**（否则它会吞输入 —— `#321`⑤）✓
    /// </summary>
    public const string MotionLayerName = "MotionLayer";

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
        // 🔴 架构裁定（`next_round §4.1.1`）：判据第 7 条**批准**，但**例外必须可审计**
        //    ⇒ 报告里必须打印"**跳过 N 个瞬态元素**"（红线 17 口径写清 + 红线 21 不留黑箱）✓
        int skippedTransient = Collect(scope, scope, labels, panels);

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
                        // 🔴 带**矩形坐标**：光有"谁压谁"不够 —— 实测踩过"整列溢出 ⇒ 子控件拿到负尺寸 ⇒ 假/真重叠"
                        //    ⇒ 有坐标才能一眼看出是"放错位置"还是"容器被挤爆"（取证 > 猜）
                        problems.Append($"\n  🔴 重叠：{labels[i].Path} {Fmt(labels[i].Rect)} ⟷ {labels[j].Path} {Fmt(labels[j].Rect)}");
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

        // 🆕 🔴 用户规则 ①（2026-09-15）：**一切 UI 都要考虑【相机大小】** ⇒ 任何可见控件的外接矩形
        //    必须落在**相机矩形**内。**越界 = 用户说的"UI 看不全"** ⇒ 做成可读读数（首个越界控件带 pos/size）✓
        //    ⚠️ 本轮先作**独立读数**（不并入 `ok`）：并入后全屏立刻红，而"全屏重新按 1280×720 收敛"是下一轮的工作 ✓
        // 🔴 **相机口径校准**（用户规则①）：必须用【项目设置的真实视口】——
        //    headless 下 `GetViewport().GetVisibleRect()` 会给**方形 1280×1280**，那不是玩家的相机 ⚠️
        //    项目：`display/window/size/viewport_width=1280 / viewport_height=720`（stretch=canvas_items ⇒ UI 坐标即此尺寸）✓
        int camW = (int)ProjectSettings.GetSetting("display/window/size/viewport_width", 1280);
        int camH = (int)ProjectSettings.GetSetting("display/window/size/viewport_height", 720);
        Vector2 cam = new Vector2(camW, camH);
        var outsideList = new System.Collections.Generic.List<string>();
        foreach (Node n in Walk(root))
        {
            if (n is Control oc && oc.IsVisibleInTree() && oc.Size.X > 0 && oc.Size.Y > 0)
            {
                Vector2 op = oc.GlobalPosition;
                if (op.X < -0.5f || op.Y < -0.5f || op.X + oc.Size.X > cam.X + 0.5f || op.Y + oc.Size.Y > cam.Y + 0.5f)
                {
                    if (outsideList.Count < 20)
                    {
                        outsideList.Add($"{Path(root, oc)} {Fmt(new Rect2(op, oc.Size))}");
                    }
                    else
                    {
                        outsideList.Add("…");
                        break;
                    }
                }
            }
        }

        bool ok = overlaps == 0 && transparent == 0;
        string scopeNote = overlay is null ? "（全界面）" : $"（**只审覆盖层 {overlay.Name}**）";

        // 🆕 🔴 **口径自证**（红线 17 / 红线 25：「通过了」之前先问「它到底检查了什么」）——
        //   两个真实教训：
        //   ① 曾经**误把普通容器当"模态覆盖层"** ⇒ 审计范围被缩到只剩一个小子树 ⇒ 报"✅ 通过"（**假通过**）
        //   ② 曾经只审覆盖层 ⇒ **范围外的控件完全没被检查**，而报告里看不出来 ⚠️
        //   ⇒ 所以每次都要把【覆盖层是谁/什么类/样式读数】+【全场景计数】一起打出来 ✓
        var allLabels = new List<(string Path, Rect2 Rect)>();
        var allPanels = new List<(string Path, Control Panel)>();
        Collect(root, root, allLabels, allPanels);
        int outsideLabels = allLabels.Count - labels.Count;
        int outsidePanels = allPanels.Count - panels.Count;
        string overlayInfo = overlay is null
            ? "无覆盖层 ⇒ 审全场景"
            : $"覆盖层 {overlay.Name}（类 {overlay.GetType().Name}，panel 样式 a={AlphaOf(overlay):0.##}" +
              $"{((overlay.HasThemeStyleboxOverride("panel")) ? "，来源=节点 override" : "，来源=主题链")}）";
        string caliber = $"\n  ｜口径：{overlayInfo}　全场景：可见 Label {allLabels.Count} ／ Panel+PC {allPanels.Count}" +
                         (outsideLabels > 0 || outsidePanels > 0
                             ? $"　⚠️ 在审范围外还有 {outsideLabels} 个 Label ／ {outsidePanels} 个 Panel（须判定：真被遮住 还是 漏审）"
                             : "　（范围外无控件）") +
                         // 🔴 架构裁定（`§4.1.1`）：第 7 条例外**必须可审计** ⇒ 打印"跳过的瞬态元素数"（含覆盖层子树内的）✓
                         $"　跳过瞬态元素 {skippedTransient + Collect(root, root, new List<(string, Rect2)>(), new List<(string, Control)>())} 个（`{MotionLayerName}` 口径例外，按设计会短暂叠放）" +
                         $"　相机 {cam.X:0}×{cam.Y:0} 越界控件 {outsideList.Count} 个" +
                         (outsideList.Count == 0 ? "（全部落在可视区内 ✅）" : "：" + string.Join(" ／ ", outsideList));

        string report = $"布局判据（{root.Name}）{scopeNote}：可见 Label {labels.Count} 个 ／ Panel+PC {panels.Count} 个　" +
                        $"重叠对 {overlaps} ／ 透明框 {transparent}　=> {(ok ? "✅ 通过" : "🔴 未通过")}" +
                        (ok ? string.Empty : problems.ToString()) +
                        caliber;
        return (ok, report);
    }

    /// <summary>数某子树里的节点数（用于"跳过了几个瞬态元素"的留痕）✓</summary>
    private static int CountDescendants(Node node)
    {
        int n = 0;
        foreach (Node child in node.GetChildren())
        {
            n += 1 + CountDescendants(child);
        }

        return n;
    }

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

    /// <summary>递归收集；返回**被跳过的瞬态元素个数**（`MotionLayer` 口径例外 ⇒ 必须留痕）✓</summary>
    private static int Collect(Node node, Node root, List<(string, Rect2)> labels, List<(string, Control)> panels)
    {
        int skipped = 0;
        foreach (Node child in node.GetChildren())
        {
            if (child.Name == MotionLayerName)
            {
                skipped += 1 + CountDescendants(child); // 🔴 跳过（口径见常量注释）—— **计数并上报**，不做黑箱 ✓
                continue;
            }

            if (child is Label label && label.IsVisibleInTree() && !string.IsNullOrWhiteSpace(label.Text))
            {
                // Label 的可视矩形：全局坐标（跨父容器一致口径）✓
                labels.Add((Path(root, label), new Rect2(label.GlobalPosition, label.Size)));
            }

            // 🔴 `Panel` **与** `PanelContainer` 两类都要查（后者继承自 Container，不是 Panel）✓
            // ⚠️ 可见性用 `IsVisibleInTree()`（**有效可见性**）：`Visible` 只看自己的标记 ——
            //    实测踩过：面板被 `Hide()` 收起后，其内部 Label 的 `Visible` 仍是 true ⇒ 被算成"重叠"（判据假红）
            if (child is Panel or PanelContainer && child is Control ctl && ctl.IsVisibleInTree())
            {
                panels.Add((Path(root, ctl), ctl));
            }

            skipped += Collect(child, root, labels, panels); // 递归（并累加子树的跳过数）
        }

        return skipped;
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
