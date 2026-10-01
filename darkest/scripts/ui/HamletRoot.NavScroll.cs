using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 2026-10-02 M12 拆片：**左列建筑 nav 的滚动宿主**（用 Godot 内建 `ScrollContainer`，不自造滚动逻辑）。
///
/// 起因（实测读数，`reports/m12_provision_20261002.md`）：DD `building_navigation.base_size` = **128×1000**，
/// 我方按 DD 尺子铺了 10 个 nav 槽（各 56 高）+ 4 个未解锁文案（各 32 高）+ 5 个入口按钮 ⇒
/// 内容最小高 **912**；而 1080 相机里已被 TopBar 39 ／ StatusBar 87 ／ BottomBar 56 ／ 间距 24 ／ 外边距 20 分走
/// ⇒ 可分配只剩 **874** ⇒ `HamletRootCol.GetCombinedMinimumSize()` = **1159 > 1080**
/// ⇒ `LayoutAudit` 判据「内容需求超出相机」demand=2（`ui_sweep` 该入口 FAIL）⚠️
///
/// 为什么用 `ScrollContainer` 而不是删槽位：滚动轴的**最小尺寸不向上传播**
/// （4.6 实测：内容 896 ⇒ `ScrollContainer.GetCombinedMinimumSize().Y == 0`）⇒ 父容器不再被内容撑高，
/// 高度不够时玩家**滚动**看后面的入口，DD 槽位信息**零丢失**；横轴保持 `Disabled`（宽度照 DD 128 定死）✓
/// </summary>
public partial class HamletRoot
{
    /// <summary>
    /// 🔴 2026-10-02 M15-P0：左列**上留白垫高**（px）—— 把建筑 nav 推到 DD 真位。
    /// 依据：DD `town.layout [town_screen_layout].button_navigation_pos` = **70,230** ⇒ nav 应从 **y=230** 起；
    /// 我方左列首行实测 **y=158**（冒烟 `hamlet_next_audit2.txt`：nav 项顶 = PurposeLabel 177 − (56−18)/2）⇒ 差 72 ✓
    /// 顺带解决与 `Overlay/ActivityLogAnchor`（DD `activity_log_pos 144,132`，盒底 0.1922×1080 = **207.6**）的同屏相撞 ✓
    /// </summary>
    private const int NavTopPadHeight = 72;

    /// <summary>
    /// 🔴 2026-10-02 M15-P0：左列**下留白垫高**（px）—— 让开屏幕级地产占位。
    /// 依据：`Overlay/EstateSummary`（DD `estate_summary_pos` **0,975** ⇒ 盒顶 975.24）压着左列底部；
    /// 实测成长对比行 y=977..1000（冒烟同源）⇒ 与「地产总览／地产费用」标签相交 ⇒ 垫 30 把左列内容抬到 975 以上 ✓
    /// </summary>
    private const int NavBottomPadHeight = 30;

    /// <summary>
    /// 🔴 2026-10-02 M15-P0：两块垫片 = 引擎内建 `Control` 留白（**无绘制、`MouseFilter=Ignore`** ⇒ 不吞点击、不是新控件）。
    /// ⚠️ 72／30 是**读数常量**（不是猜的）：TopBar／StatusBar／BottomBar 高度一变就要**重测**——
    ///    护栏就是判据本身：重跑 `--hamlet-next --ui-audit`，重叠对不为 0 ⇒ 常量过期 ✓
    /// </summary>
    private static void EnsureColumnPads(Container parent)
    {
        EnsurePad(parent, "NavTopPad", NavTopPadHeight, 0);                            // 首行之上 ✓
        EnsurePad(parent, "NavBottomPad", NavBottomPadHeight, parent.GetChildCount());  // 末行之下 ✓
    }

    /// <summary>幂等建／改一块留白垫，并把它摆到目标序号（Godot 内建 `MoveChild`）✓</summary>
    private static void EnsurePad(Container parent, string name, int height, int index)
    {
        if (parent.GetNodeOrNull<Control>(name) is not { } pad)
        {
            pad = new Control
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Ignore,   // 🔴 垫片绝不吞输入（否则它盖住的区域点不动）⚠️
            };
            parent.AddChild(pad);
        }

        pad.CustomMinimumSize = new Vector2(0, height);
        parent.MoveChild(pad, Mathf.Clamp(index, 0, parent.GetChildCount() - 1));
    }

    /// <summary>
    /// 把 nav 竖列挂进（或复用）滚动宿主。幂等：已挂过就只改 Parent，不叠第二层 ⇒ 重进 Hamlet 不会越套越深 ✓
    /// </summary>
    private static ScrollContainer EnsureNavScrollHost(Container parent, VBoxContainer nav)
    {
        EnsureColumnPads(parent);   // 🔴 先摆垫片（幂等）：nav 的槽位索引取垫片之后的**真实**索引 ✓

        if (nav.GetParent() is ScrollContainer existing)
        {
            if (existing.GetParent() != parent)
            {
                existing.Reparent(parent);
            }

            return existing;
        }

        var host = new ScrollContainer
        {
            Name = "BuildingNavScroll",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,   // 宽度照 DD 128 定死，不横滚 ✓
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,         // 装不下才出滚动条 ✓
        };
        int slot = nav.GetParent() is null ? -1 : nav.GetIndex();   // 骨架里的摆放位（左列第几个）要保住 ✓
        if (nav.GetParent() is null)
        {
            host.AddChild(nav);
        }
        else
        {
            nav.Reparent(host);   // Reparent 自带 RemoveChild ⇒ 先记 index 再 Reparent（顺序颠倒会拿到 -1）⚠️
        }

        parent.AddChild(host);
        if (slot >= 0 && slot < parent.GetChildCount())
        {
            parent.MoveChild(host, slot);
        }

        GD.Print($"[HamletRoot] 建筑 nav 套滚动宿主（内容最小高 {nav.GetCombinedMinimumSize().Y:0} > 可分配 ⇒ 纵向滚动；横向 Disabled）✓");
        return host;
    }
}
