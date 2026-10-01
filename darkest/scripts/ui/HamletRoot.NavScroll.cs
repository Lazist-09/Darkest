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
    /// 把 nav 竖列挂进（或复用）滚动宿主。幂等：已挂过就只改 Parent，不叠第二层 ⇒ 重进 Hamlet 不会越套越深 ✓
    /// </summary>
    private static ScrollContainer EnsureNavScrollHost(Container parent, VBoxContainer nav)
    {
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
