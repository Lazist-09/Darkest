using System;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **B-2 的【判据】**（架构原话）：
///   "绘层必须解决（`ui_root.tscn` 根是 `Control`、autoload 早于场景 ⇒ 当前场景画在其上 ⇒ 屏幕可能错）
///    ⇒ 外壳上 **`CanvasLayer`**（或层号）+ **补判据"外壳三层都在当前场景之上"**（**否则冒烟绿=假绿**）"
///
/// WHY 需要它：冒烟"全绿"**不能**说明"外壳画在场景之上" ✗ —— 那是**另一件事** ✓
///   本判据把这件事变成**可读的数字**：外壳的有效绘层 vs 当前场景的有效绘层 ✓
///
/// 判据（**fail-closed**）：
///   ① 外壳存在（`UIRoot.Instance`）⇒ 否则报 `未接线` ✓
///   ② 外壳的**有效绘层号** > 当前场景的**有效绘层号** ⇒ ✅ 在上；否则 🔴 **在场景之下/同层**
/// 🔴 **零行为**：只在显式 `--shell-layer` 时运行（我域旗标 ✓）⇒ 默认路径一字不变 ✓
/// </summary>
public static class ShellLayerAudit
{
    /// <summary>节点（或其祖先）所在画布的有效层号：`CanvasLayer.Layer`；普通 `Node` ⇒ 0 ✓</summary>
    public static int EffectiveLayer(Node? node)
    {
        for (Node? cur = node; cur is not null; cur = cur.GetParent())
        {
            if (cur is CanvasLayer cl)
            {
                return cl.Layer;
            }
        }

        return 0;   // 默认画布 ✓
    }

    /// <summary>
    /// 跑一次判据并打印**可核对的读数**；返回是否通过（true = 外壳确实在场景之上 ✓）。
    /// </summary>
    public static bool Run(Node sceneNode, bool pushErrorWhenFail = false)
    {
        Darkest.UI.UIRoot? shell = Darkest.UI.UIRoot.Instance;
        if (shell is null)
        {
            GD.Print("[B-2 判据] 🔴 外壳不存在（`UIRoot.Instance` = null）⇒ **未接线**（不算通过 ✓）");
            return false;
        }

        int shellLayer = EffectiveLayer(shell);
        int sceneLayer = EffectiveLayer(sceneNode);

        // 外壳三层各自的层号（一并打出来，方便 UI 侧一眼看到"该改哪一层"✓）
        string L(string name, Node? n) => n is null ? $"{name}=（缺失）" : $"{name}={EffectiveLayer(n)}";

        GD.Print($"[B-2 判据] 外壳三层：{L("Base", shell.BaseLayer)} · {L("Screen", shell.ScreenLayer)} · "
            + $"{L("Overlay", shell.GetNodeOrNull<Control>("OverlayLayer"))}");
        GD.Print($"[B-2 判据] **外壳有效绘层 = {shellLayer}** vs **当前场景（{sceneNode.GetType().Name}）有效绘层 = {sceneLayer}**");

        bool above = shellLayer > sceneLayer;
        if (above)
        {
            GD.Print($"[B-2 判据] ✅ 外壳在场景**之上**（{shellLayer} > {sceneLayer}）✓");
        }
        else
        {
            string why = shellLayer == sceneLayer
                ? "同层 ⇒ 谁在上取决于树序（autoload 早于场景 ⇒ **场景会盖住外壳** ⚠️）"
                : "外壳层号更小 ⇒ **外壳被场景盖住** ⚠️";
            GD.Print($"[B-2 判据] 🔴 **外壳不在场景之上**（{shellLayer} vs {sceneLayer}）：{why}"
                + " ⇒ 这正是架构说的『**屏幕可能错**』✓（修法：给外壳加 `CanvasLayer` 抬层 = UI 域 ✓）");

            if (pushErrorWhenFail)
            {
                GD.PushError($"[B-2 判据] 外壳绘层 {shellLayer} 未高于场景 {sceneLayer}（屏幕可能错）");
            }
        }

        return above;
    }
}
