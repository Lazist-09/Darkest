using System;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🆕 **M6u（P4 ②）· 方块【孔】**（DD 式拖放**目标**）—— 玩家把方块拖进来 ⇒ `Dropped(载荷 id)` ✓
///
/// 用户 2026-10-01 口径原话：「所有建筑详情界面的选人都是有一个**方块空洞**，玩家把角色对应的方块头像
/// **放入孔中**表示使用这个人」⇒ 本件就是那个"空洞"（DD 的 `hero_slot` 位）✓
/// 🆕 2026-10-02 M4u：**同一件孔也服务饰品** —— 载荷带族 tag（见 `DragPayload`），`AcceptTag` 决定这个孔收哪一族
///   ⇒ 英雄方块投进饰品格**当场拒绝**（不会拿英雄 id 去查饰品库而炸）✓ **拖放协议仍然只有一份**（红线 21）✓
///
/// 🔴 **复用引擎内建拖放，不造轮子**：只重写 `_CanDropData` / `_DropData`（Godot 官方 drag-and-drop）✓
/// 🔴 孔**必须** `MouseFilter = Stop`：`Ignore` 的控件收不到 `_DropData`（实测坑 ⇒ 显式设置 + 注释钉住）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class GearHeroSlot : PanelContainer
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/gear_hero_slot.tscn";

    /// <summary>落孔回调（参数 = 载荷 id，族 tag 已剥）；英雄孔 ⇒ 选人 ／ 饰品孔 ⇒ 装上 ✓</summary>
    public Action<string>? Dropped { get; set; }

    /// <summary>🔴 本孔收哪一族载荷（默认英雄族）；族不匹配 ⇒ `_CanDropData` 当场 false（不炸、不静默收下）✓</summary>
    public string AcceptTag { get; set; } = DragPayload.HeroTag;

    /// <summary>孔里的字形 Label（空孔 = ＋ ／ 已选 = 首字）✓</summary>
    public Label? Glyph => GetNodeOrNull<Label>("SlotGlyph");

    /// <summary>编辑器预览字形 ✓</summary>
    [Export]
    public string PreviewGlyph { get; set; } = "＋";

    /// <summary>造一个孔；**场景缺失/类型不符 ⇒ 返回 null**（调用方如实上报"拖放不可用"，不崩不静默）✓</summary>
    public static GearHeroSlot? TryCreate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        return packed?.Instantiate<GearHeroSlot>();
    }

    /// <summary>换字形与悬停说明（＋ = 空孔；首字 = 已选该英雄）✓</summary>
    public void SetGlyph(string text, string tooltip)
    {
        if (Glyph is { } g)
        {
            g.Text = text;
            g.Visible = true;   // 方块放进孔后字形被隐藏过 ⇒ 换回空孔要能恢复（孔会被复用）✓
        }

        TooltipText = tooltip;   // 悬停在孔边缘（字形之外）也能看到同一句 ✓
    }

    /// <summary>
    /// 🆕 M4u：**把一件方块放进孔**（隐藏 ＋ 字形 ＋ 换悬停）—— 方块自己仍是拖放源（可再拖出）✓
    /// ⚠️ 本方法只负责"放"：装载 ／ 卸载的业务判据在调用方（本件一句判据都不写 · 红线 21 (b)）✓
    /// </summary>
    public void SetItem(Control item, string tooltip)
    {
        if (Glyph is { } g)
        {
            g.Visible = false;
        }

        TooltipText = tooltip;
        AddChild(item);
    }

    /// <summary>🔴 引擎内建拖放**目标**：只收**本族**载荷（非空字符串 ＋ tag 逐字相等）✓</summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => data.VariantType == Variant.Type.String && DragPayload.TryDecode(data.AsString(), AcceptTag, out _);

    /// <summary>🔴 落孔 ⇒ 转发载荷 id（真实玩家路径 = 引擎拖动；冒烟用 `TryDropPayload` 走**同一入口**）✓</summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!DragPayload.TryDecode(data.AsString(), AcceptTag, out string id))
        {
            return;   // `_CanDropData` 已挡过；真到这里说明引擎没先问 ⇒ 如实返回，不猜 ✓
        }

        GD.Print($"[GearHeroSlot] 方块落孔（族 {AcceptTag}）：{id}（引擎 _DropData ⇒ Dropped）✓");
        Dropped?.Invoke(id);
    }

    /// <summary>
    /// 🔴 冒烟用**同一条入口**（先 `_CanDropData` 再 `_DropData`，Variant 真造）——
    /// 拖动阈值本身无法 headless 复验（报告如实标注），但**族校验 ＋ 落孔回调**走的正是玩家那条路 ✓
    /// </summary>
    public bool TryDropPayload(string payload)
    {
        Variant v = payload;
        if (!_CanDropData(Vector2.Zero, v))
        {
            GD.Print($"[GearHeroSlot] 载荷「{payload}」被拒（本孔收族 {AcceptTag}）⇒ 不落孔 ✓");
            return false;
        }

        _DropData(Vector2.Zero, v);
        return true;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;   // 🔴 孔必须收鼠标事件（Ignore ⇒ 永远收不到 _DropData）✓
        if (Engine.IsEditorHint() && Glyph is { } g)
        {
            g.Text = PreviewGlyph;
        }
    }
}
