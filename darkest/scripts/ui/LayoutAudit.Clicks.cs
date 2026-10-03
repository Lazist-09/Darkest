// ① 来源：`#319`⑤ 布局判据的**第三条**（用户 2026-10-03 报「主菜单点不动」的根因面）——
//    前两条（Label 相交 / 框不透明）**查不到本类缺陷**：制作人员骨架的满屏 `ColorRect` 既不重叠任何 Label、
//    也不是 Panel ⇒ 两条判据全绿，而玩家**一个按钮都点不动**（实测：`MainMenuRoot` 把 `CreditsSkeleton`／`FeFlowSkeleton`
//    无条件当叠加层加进主菜单 ⇒ 满屏占位块盖住三选一按钮）⚠️
// ② 职责：回答「**屏幕上这个按钮，真的点得到吗**」——用引擎自己的命中测试规则（绘制顺序 + `MouseFilter`），
//    不写"哪一类算装饰"的白名单式猜测（红线 21 (b)：判据要能被引擎读数推翻）✓
// ③ 口径（三条，缺一条就会假红／假绿）：
//    · **吞点击的只有两种**（2026-10-03 引擎实测 `.tmp_ui_probe/probe.gd`／`probe4.gd`，`SceneTree` 里真 `push_input`）：
//      `Stop` ⇒ 事件**停在它身上**（下方兄弟**与**它的祖先都收不到，实测 parent_hits=0）；
//      `Pass` ⇒ 事件**往父链送**（吞掉「画在它下面的**兄弟**」，但被盖的是它的**祖先**时照样收得到，实测 parent_hits=1）⇒ 后者不报；
//      `IGNORE` ⇒ 完全不参与命中 ⇒ 不报 ✓
//      ⚠️ `mouse_filter` 是**逐控件**的：父节点 `Ignore` **不**豁免子节点（credits 满屏吞点击正是这个形态 ——
//         外层 `Control` 写了 `2`、内层满屏 `Fill` 没写 ⇒ 引擎默认 `Stop` ⇒ 照吞）✓
//    · **装饰性** = 自身非交互控件、且子树内也没有交互控件（`PanelContainer` + 按钮的模态框**不算**装饰 ⇒ 不报）；
//    · **绘制顺序**：Godot 命中测试取"最上面那个非 IGNORE 的控件" ⇒ 绘制序 = **(所属 `CanvasLayer.layer`, 有效 `z_index`, 先序序号)** 三元组 ✓
//      ⚠️ **两个真实误报（2026-10-03 实测）**，缺任一条就会报出"假红"：
//      ① **跨 `CanvasLayer` 不可比**：`BattleBg` 在世界层（`Node2D` + 相机 ⇒ 画布坐标）、按钮在 `UILayer`
//         （`CanvasLayer` ⇒ 屏幕坐标）⇒ 矩形压根不在同一坐标系里（实测 `BattleBg` 被算成压住 12 个按钮）；
//      ② **带 `gui_input` 处理器的控件是交互控件**：`PortraitFrame`（右键开详情）不是 `BaseButton`，
//         按"类型白名单"判会被当成装饰 ⇒ 实测误报 8 条。两条都改成**读引擎状态**（层号/`z_index`/信号连接表）✓
// ④ 例外必须可审计（红线 21）：**模态层内部**的遮挡是设计（遮罩 + 自带按钮）⇒ 跳过但**计数上报**，不做黑箱 ✓
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Godot;

namespace Darkest.UI;

public static partial class LayoutAudit
{
    /// <summary>
    /// 🔴 判据 3：**吞点击**（`#319`⑤ 第三条）。
    /// 返回 `(处数, 明细, 按模态口径跳过数)`；明细每行形如 `被盖的按钮 ⟸ 吞它的装饰控件`。
    /// </summary>
    /// <param name="scope">审计范围（有模态覆盖层时 = 该层，口径同判据 1／2）✓</param>
    private static (int Count, List<string> Items, int ModalSkips) FindClickBlockers(Node scope)
    {
        List<ClickNode> nodes = CollectClickNodes(scope);

        // 🔴 反向传播"子树里有交互控件"（先序序号 ⇒ 子节点序号必大于父 ⇒ 倒着扫一遍即可）✓
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i].SubtreeInteractive && nodes[i].Parent >= 0)
            {
                nodes[nodes[i].Parent].SubtreeInteractive = true;
            }
        }
        var items = new List<string>();
        int count = 0;
        int modalSkips = 0;
        for (int bi = 0; bi < nodes.Count; bi++)
        {
            ClickNode target = nodes[bi];
            if (!target.Interactive)
            {
                continue;
            }

            for (int ci = 0; ci < nodes.Count; ci++)
            {
                ClickNode cover = nodes[ci];
                if (cover.Interactive || cover.SubtreeInteractive)
                {
                    continue; // 模态框/复合组件（自带按钮）不算装饰 ⇒ 见口径 ③
                }

                if (cover.Ctl.MouseFilter == Control.MouseFilterEnum.Ignore)
                {
                    continue; // 只有 Ignore 不参与命中 ⇒ 见口径 ③（`Pass` 一样吞下方兄弟）
                }

                // 🔴 2026-10-03 引擎实测（`.tmp_ui_probe/probe4.gd`）：子控件 `Pass` ⇒ 事件**继续往父链送**
                //    ⇒ 被盖住的是它的**祖先**时照样收得到（实测 parent_hits=1）⇒ 不算吞；
                //    子控件 `Stop` ⇒ 事件停在它身上（实测 parent_hits=0）⇒ 祖先也收不到 ⇒ 照报 ✓
                if (cover.Ctl.MouseFilter == Control.MouseFilterEnum.Pass && IsAncestorOf(nodes, bi, ci))
                {
                    continue;
                }

                if (!IsAbove(cover, target) || !cover.Rect.Intersects(target.Rect))
                {
                    continue; // 只判"画在它上面"的（命中测试取最上面那个）＋ **同层**才比（见口径 ③）✓
                }

                if (IsIntentionalModalLayer(nodes, ci, bi))
                {
                    modalSkips++;
                    continue;
                }

                count++;
                if (PrintAll || count <= 6)
                {
                    items.Add($"\n  🔴 点不动：{target.Path} {Fmt(target.Rect)} ⟸ 被 {cover.Path} {Fmt(cover.Rect)}" +
                              $"（{cover.Ctl.GetType().Name}，mouse_filter={cover.Ctl.MouseFilter}）盖住");
                }
            }
        }

        return (count, items, modalSkips);
    }

    /// <summary>`ancestorIdx` 是否为 `nodeIdx` 的祖先（沿 `Parent` 链上溯）——`Pass` 冒泡口径用 ✓</summary>
    private static bool IsAncestorOf(List<ClickNode> nodes, int ancestorIdx, int nodeIdx)
    {
        for (int i = nodes[nodeIdx].Parent; i >= 0; i = nodes[i].Parent)
        {
            if (i == ancestorIdx)
            {
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// 🔴 **模态层口径**（例外必须可审计）：装饰控件与被盖按钮**不在同一层**，且装饰所在层**自带交互控件**
    /// ⇒ 那是"遮罩 + 对话框"的设计（模态打开时下方本就不该可点）⇒ 跳过 ✓
    /// ⚠️ 同层则**一律报**（同一层里装饰压住同层按钮 = 真缺陷，不能被"这一层还有别的按钮"洗掉）✓
    /// </summary>
    private static bool IsIntentionalModalLayer(List<ClickNode> nodes, int coverIdx, int targetIdx)
    {
        int coverTop = TopIndexOf(nodes, coverIdx);
        if (coverTop == TopIndexOf(nodes, targetIdx))
        {
            return false;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (i != targetIdx && nodes[i].Interactive && TopIndexOf(nodes, i) == coverTop)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>沿父链走到最外层（= `scope` 的直接子节点）——"层"的判定单位 ✓</summary>
    private static int TopIndexOf(List<ClickNode> nodes, int index)
    {
        while (nodes[index].Parent >= 0)
        {
            index = nodes[index].Parent;
        }

        return index;
    }

    /// <summary>
    /// 🔴 `cover` 是否**画在** `target` 上面（Godot 命中测试的取法）：
    /// 先比所属 `CanvasLayer.layer`（高者在上 ⇒ 跨层直接不是"盖住"），再比有效 `z_index`，最后比先序序号 ✓
    /// ⚠️ 跨层**只判序、不比矩形**：`CanvasLayer`（默认不跟相机）用屏幕坐标、世界层用画布坐标 ⇒
    ///    矩形不可比，报出来就是假红（见口径 ③ 误报①）✓
    /// </summary>
    private static bool IsAbove(ClickNode cover, ClickNode target)
    {
        if (cover.Layer != target.Layer)
        {
            return cover.Layer > target.Layer;
        }

        return cover.Z != target.Z ? cover.Z > target.Z : cover.Order > target.Order;
    }

    /// <summary>所属 `CanvasLayer.layer`（无 `CanvasLayer` 祖先 ⇒ 世界层 = 0；`CanvasLayer` 默认 `layer` 也是 1 ⇒ 与引擎一致）✓</summary>
    private static int CanvasLayerOf(Control ctl)
    {
        for (Node? p = ctl.GetParent(); p is not null; p = p.GetParent())
        {
            if (p is CanvasLayer cl)
            {
                return cl.Layer;
            }
        }

        return 0;
    }

    /// <summary>有效 `z_index`（`CanvasItem.ZAsRelative` 默认 true ⇒ 沿父链累加；遇到非 relative 就停）✓</summary>
    private static int EffectiveZ(Node node)
    {
        int z = 0;
        for (Node? n = node; n is CanvasItem ci; n = ci.GetParent())
        {
            z += ci.ZIndex;
            if (!ci.ZAsRelative)
            {
                break;
            }
        }

        return z;
    }

    /// <summary>交互控件 = 引擎里"玩家能操作"的那几类（不含 `ProgressBar` 这类只显示的 `Range`）✓</summary>
    private static bool IsInteractive(Control ctl) => ctl switch
    {
        BaseButton => true,
        LineEdit => true,
        TextEdit => true,
        Slider => true,
        ScrollBar => true,
        SpinBox => true,
        ItemList => true,
        Tree => true,
        TabBar => true,
        _ => HasGuiInputHandler(ctl),   // 🔴 见口径 ③ 误报②：`PanelContainer` 挂 `GuiInput` 也是交互控件（如右键头像）✓
    };

    /// <summary>读**引擎的信号连接表**（不猜类型）：C# 的 `GuiInput +=` 与 GDScript 的 `connect` 都落在这里 ✓</summary>
    private static bool HasGuiInputHandler(Control ctl)
    {
        Godot.Collections.Array<Godot.Collections.Dictionary> conns =
            ctl.GetSignalConnectionList(Control.SignalName.GuiInput);
        return conns.Count > 0;
    }
    /// <summary>先序收集（序号 = 绘制顺序：越大越靠上）；跨视口（子窗口）与零尺寸按口径跳过 ✓</summary>
    private static List<ClickNode> CollectClickNodes(Node scope)
    {
        var nodes = new List<ClickNode>();

        void Visit(Node node, int parentIdx)
        {
            foreach (Node child in node.GetChildren())
            {
                if (!SameViewport(scope, child))
                {
                    continue; // `Window` 系自成视口（见 `SameViewport`）✓
                }

                int myIdx = parentIdx;
                if (child is Control ctl && ctl.IsVisibleInTree() && ctl.Size.X > 0.5f && ctl.Size.Y > 0.5f)
                {
                    bool interactive = IsInteractive(ctl);
                    nodes.Add(new ClickNode
                    {
                        Ctl = ctl,
                        Path = Path(scope, ctl),
                        Rect = ClippedRect(ctl),
                        Order = nodes.Count,
                        Layer = CanvasLayerOf(ctl),
                        Z = EffectiveZ(ctl),
                        Parent = parentIdx,
                        Interactive = interactive,
                        SubtreeInteractive = interactive,
                    });
                    myIdx = nodes.Count - 1;
                }

                Visit(child, myIdx);
            }
        }

        Visit(scope, -1);
        return nodes;
    }

    /// <summary>判据 3 的节点读数（绘制顺序 + 矩形 + 是否交互 + 子树内是否有交互）✓</summary>
    private sealed class ClickNode
    {
        public Control Ctl = null!;
        public string Path = string.Empty;
        public Rect2 Rect;
        public int Order;
        public int Layer;
        public int Z;
        public int Parent = -1;
        public bool Interactive;
        public bool SubtreeInteractive;
    }
}
//    · **只有 `Ignore` 不参与命中**（`Pass` 一样吞掉**画在它下面的兄弟**控件）—— 2026-10-03 引擎实测
//      （`.tmp_ui_probe/probe.gd` · `SceneTree` 里真 `push_input`）：`lower=Stop / upper=Pass` ⇒ 下层**收不到**；
//      把 `upper` 改 `Ignore` ⇒ 下层立刻收到 ⚠️ 旧口径「PASS 是本判据推荐的写法」**与引擎不符**，已作废：
//      `Pass` 只是把事件**继续往父链**送（祖先挂 `GuiInput` 时才用得上），**不**送给被盖住的兄弟 ✗
