// 🔴 **从自身拆出两片**（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **判据主流程**（`MotionLayerName` 原 :20-27 ＋ `Check` 原 :29-179）⇒
//    树遍历与绘制口径 ⇒ `LayoutAudit.Traversal.cs`；覆盖层口径／例外留痕／读数格式化 ⇒ `LayoutAudit.Caliber.cs`（M11 ① 预警面第二十六件 · 2026-10-02 · 只搬家 · 零行为改动）✓

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.UI;

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
public static partial class LayoutAudit
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
        var scopeSkips = new SkipLog();
        Collect(scope, scope, labels, panels, scopeSkips);

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
            // 🔴 跨视口口径（见 `SameViewport`）：子窗口（引擎 tooltip 的 `PopupPanel` ／ `PopupMenu`）内
            //    控件的坐标系是【子窗口内容原点】⇒ 与主视口**不可比**，不参与相机判据 ✓
            // 🔴 滚动口径（与 tooBig 同源）：ScrollContainer 内部的子项「超出即可滚动」是设计如此 ⇒ 不算越界 ✓
            if (n is Control oc && SameViewport(root, oc) && oc.IsVisibleInTree() && oc.Size.X > 0 && oc.Size.Y > 0 && !InsideScroll(oc))
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

        // 🆕 🔴 **内容需求口径**（规则①的正确量法）：headless 的**实际视口是 1280×1280**（`--resolution` 无效），
        //    拿"实际矩形 vs 项目 720"比会把"填满视口"误判成越界 ⚠️ ⇒ 真正有意义的是【**内容最小尺寸需求**】：
        //    某控件的最小尺寸超过相机 ⇒ 它在真实 720 下**必然溢出**（这才是要修的东西）✓
        var tooBig = new System.Collections.Generic.List<string>();
        foreach (Node n in Walk(root))
        {
            if (n is Control tc && SameViewport(root, tc) && tc.IsVisibleInTree() && !InsideScroll(tc))
            {
                Vector2 need = tc.GetCombinedMinimumSize();
                if (need.X > cam.X + 0.5f || need.Y > cam.Y + 0.5f)
                {
                    if (tooBig.Count < 6)
                    {
                        tooBig.Add($"{Path(root, tc)} 需 {need.X:0}×{need.Y:0}");
                    }
                    else
                    {
                        tooBig.Add("…");
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
        var allSkips = new SkipLog();
        Collect(root, root, allLabels, allPanels, allSkips);
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
                         // ⚠️ 读数更正（2026-10-02）：原式 = 覆盖层跳过数 ＋ 全场景跳过数 ⇒ **把覆盖层那一份数了两遍**（同子树被数两次）✓
                         $"　跳过瞬态元素 {allSkips.Transient} 个（`{MotionLayerName}` 口径例外，按设计会短暂叠放）" +
                         (overlay is null ? string.Empty : $"（覆盖层内 {scopeSkips.Transient} 个）") +
                         $"　跳过子窗口 {allSkips.Windows} 个（{SkipNote(allSkips)}；`Window` 系自成视口 ⇒ 矩形不可比，见 SameViewport）" +
                        $"　相机 {cam.X:0}×{cam.Y:0}（**项目真实视口**）越界控件 {outsideList.Count} 个（实际矩形口径；滚动容器内部按设计可超出 ⇒ 不计，见 InsideScroll）" +
                        // 🔴 口径自证（红线 17／21）：被 clip_contents 裁到空的 Label **没画在屏幕上** ⇒ 不进重叠判据，但**必须留痕** ✓
                        $"　跳过裁剪外元素 {allSkips.Clipped} 个（clip_contents 口径：被祖先容器裁到空的像素没画在屏幕上 ⇒ 不参与重叠判据，见 ClippedRect；上方 Label 计数已扣除）" +
                        $"　帧={Engine.GetProcessFrames()}　🔴 **内容需求超出相机 {tooBig.Count} 个**" + (tooBig.Count == 0 ? "（全部装得下 ✅）" : "：" + string.Join(" ／ ", tooBig)) +
                         (outsideList.Count == 0 ? "（全部落在可视区内 ✅）" : "：" + string.Join(" ／ ", outsideList));

        string report = $"布局判据（{root.Name}）{scopeNote}：可见 Label {labels.Count} 个 ／ Panel+PC {panels.Count} 个　" +
                        $"重叠对 {overlaps} ／ 透明框 {transparent}　=> {(ok ? "✅ 通过" : "🔴 未通过")}" +
                        (ok ? string.Empty : problems.ToString()) +
                        caliber;
        return (ok, report);
    }
}
