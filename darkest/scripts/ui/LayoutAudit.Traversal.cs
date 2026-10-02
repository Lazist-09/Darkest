// ① 来源：从 `LayoutAudit.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **树遍历与绘制口径**（`InsideScroll` 原 :181-193 ＋ `CountDescendants` 原 :195-205 ＋ `Walk` 原 :258-268 ＋ `SameViewport` 原 :270-293 ＋
//    `Collect` 原 :324-370 ＋ `Path` 原 :372-383 ＋ `ClippedRect` 原 :385-401；M11 ① 预警面第二十六件·第一片）✓
// ② 职责：给主片判据提供「遍历哪些节点／哪些按口径跳过／矩形按引擎 clip_contents 怎么裁」的唯一入口（跳过必留痕 ⇒ `SkipLog` 计数）✓
// ③ 🔴 依赖（实测扫描本片）：`Godot`（`Node`／`Control`／`Rect2`／`ScrollContainer`／`Window`）＋ `System.Collections.Generic`（`List`）＋ `System.Linq`（`TakeLast`）；
//    跨片仅**引用类型** `SkipLog`（`Caliber` 片）；`Caliber` 片反向**调用**本片 `Walk`／`CountDescendants` ⇒
//    两片互引但**无递归调用环**（`Walk` 不回调 `Caliber` 片）✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 3 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Darkest.UI;

public static partial class LayoutAudit
{

    /// <summary>🔴 口径：**滚动容器内部的内容不算越界**（它本就是"超出即可滚动"的设计）✓</summary>
    private static bool InsideScroll(Node node)
    {
        for (Node? p = node.GetParent(); p is not null; p = p.GetParent())
        {
            if (p is ScrollContainer)
            {
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// 🔴 **跨视口口径**（2026-10-02 实证）：`Window` 系节点（引擎 tooltip 的 `PopupPanel` ／ `PopupMenu`）**自成一个 `Viewport`**
    /// ⇒ 它内部 `Control.GlobalPosition` 是【相对该子窗口内容原点】的坐标，与主视口**不可比**。
    /// 实测：`ProvStoreAnchor`（带 `tooltip_text`）被引擎挂上 tooltip 弹窗（`@PopupPanel@10/@Label@9` pos=(6,6)），
    /// 与主视口的 `DialogTitle` pos=(6,10) 被判成"重叠" —— 那是**坐标系错误**，不是布局重叠（`hamlet-quest-select` 屏同样复现）✓
    /// ⚠️ 口径代价（如实登记 `O-113`）：子窗口**内部**的布局本判据不覆盖（tooltip 内容随光标、由引擎托管）✓
    /// </summary>
    private static bool SameViewport(Node root, Node node)
    {
        for (Node? p = node; p is not null; p = p.GetParent())
        {
            if (p == root)
            {
                return true;
            }

            if (p is Window)
            {
                return false;
            }
        }

        return true;   // 走到树根也没碰到 `Window`（游离节点 ⇒ 不该发生，按同视口处理）✓
    }

    /// <summary>递归收集（跳过项写进 `SkipLog`；红线 21：例外必须留痕）✓</summary>
    private static void Collect(Node node, Node root, List<(string, Rect2)> labels, List<(string, Control)> panels, SkipLog log)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.Name == MotionLayerName)
            {
                log.Transient += 1 + CountDescendants(child); // 🔴 跳过（口径见常量注释）—— **计数并上报**，不做黑箱 ✓
                continue;
            }

            // 🔴 跨视口口径（见 `SameViewport`）：`Window` 子树自成视口 ⇒ 里面 Label 的 GlobalPosition 与主视口**不可比** ✓
            if (!SameViewport(root, child))
            {
                log.AddWindow(child);
                continue;
            }

            if (child is Label label && label.IsVisibleInTree() && !string.IsNullOrWhiteSpace(label.Text))
            {
                // 🔴 **绘制口径**（2026-10-02 实证）：Godot 的 clip_contents ⇒ 子树被裁到该容器矩形内。
                //    实测教训：左列 nav 装进 ScrollContainer（内容 912 > 宿主 ≈792）⇒ 滚出可视区的那几条
                //    Locked_blacksmith_* 文案（y≈962／1006）**根本没画在屏幕上**，却被判成「与下方 ReliefHint
                //    ／ LastRunStartLines 重叠」—— 那是**裁剪口径错误**，不是布局重叠（Hamlet 实测 9 对里 8 对属此类）⚠️
                //    ⇒ 判据必须用【**裁剪后**的矩形】：裁到空 ⇒ 玩家看不到 ⇒ 不参与判据（**计数并上报**，红线 21）✓
                Rect2 rect = ClippedRect(label);
                if (rect.Size.X <= 0.5f || rect.Size.Y <= 0.5f)
                {
                    log.Clipped += 1;
                }
                else
                {
                    labels.Add((Path(root, label), rect));
                }
            }

            // 🔴 `Panel` **与** `PanelContainer` 两类都要查（后者继承自 Container，不是 Panel）✓
            // ⚠️ 可见性用 `IsVisibleInTree()`（**有效可见性**）：`Visible` 只看自己的标记 ——
            //    实测踩过：面板被 `Hide()` 收起后，其内部 Label 的 `Visible` 仍是 true ⇒ 被算成"重叠"（判据假红）
            if (child is Panel or PanelContainer && child is Control ctl && ctl.IsVisibleInTree())
            {
                panels.Add((Path(root, ctl), ctl));
            }

            Collect(child, root, labels, panels, log); // 递归（跳过数写进同一份 `log`）✓
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

    /// <summary>
    /// 🔴 **绘制口径**：把控件矩形裁到**祖先里所有** clip_contents = true 的容器之内 —— 这就是 Godot 真实的绘制规则。
    /// 用引擎自己的属性（而不是「猜哪一类是滚动容器」）⇒ 一处规则覆盖 ScrollContainer ／ 面板 ／ 画布，无自造判据 ✓
    /// </summary>
    private static Rect2 ClippedRect(Control ctl)
    {
        Rect2 rect = new Rect2(ctl.GlobalPosition, ctl.Size);
        for (Node? p = ctl.GetParent(); p is not null; p = p.GetParent())
        {
            if (p is Control pc && pc.ClipContents)
            {
                rect = rect.Intersection(new Rect2(pc.GlobalPosition, pc.Size));
            }
        }

        return rect;
    }
}
