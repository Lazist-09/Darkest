using System;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🆕 **M6u（P4 ②）· 方块**（DD 式拖放**源**）—— 玩家把方块**拖进孔中**表示"用它"；
/// 点击方块 = 同一动作的兜底（无鼠标 / 键盘场景）✓
///
/// 🔴 **复用引擎内建拖放，不造轮子**：Godot 的 `Control._GetDragData` / `SetDragPreview` 就是官方那套
///    （官方文档 "Drag and drop" 一节：源控件重写 `_GetDragData` 并 `SetDragPreview`，
///    目标控件重写 `_CanDropData` / `_DropData`）⇒ **零第三方库、零自研协议** ✓
///    ⇒ 载荷 = `<族 tag>:<id>`，编解码只在 `DragPayload` 一处（红线 21：一条规则一个落点）✓
///
/// 🆕 2026-10-02 M4u：**拖出孔外松手 = 卸下** —— 用引擎 `Node.NotificationDragEnd` ＋
///    `Control.IsDragSuccessful()`（官方 XML 原话："Best used with `Node.NotificationDragEnd`"）
///    ⇒ 失败即"没落在任何孔里" ⇒ 回调 `DraggedOut`（饰品孔据此卸下）✓
///
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class GearHeroSquare : Button
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/gear_hero_square.tscn";

    /// <summary>方块代表的载荷 id（空 ⇒ 不可拖：不静默拖一个空载荷）✓</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>载荷族（默认英雄族）；饰品的方块要显式给饰品族 ⇒ 只有饰品孔收它 ✓</summary>
    public string PayloadTag { get; set; } = DragPayload.HeroTag;

    /// <summary>🔴 拖出后**没落在任何孔里** ⇒ 回调（饰品孔据此卸下；英雄方块不接线 ⇒ 与接线前行为一致）✓</summary>
    public Action? DraggedOut { get; set; }

    /// <summary>编辑器预览用文案（运行时被真实首字覆盖）✓</summary>
    [Export]
    public string PreviewText { get; set; } = "战";

    /// <summary>造一个方块；**场景缺失/类型不符 ⇒ 返回 null**（调用方回落 `new Button`，不崩不静默）✓</summary>
    public static GearHeroSquare? TryCreate(string payload, string text, string tooltip,
        string payloadTag = DragPayload.HeroTag, string? nodeName = null)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        GearHeroSquare? square = packed?.Instantiate<GearHeroSquare>();
        if (square is null)
        {
            return null;
        }

        square.Name = nodeName ?? $"GearHero_{payload}";   // 节点名 = 冒烟锚点（按 id 找方块 ⇒ 真实按下）✓
        square.Payload = payload;
        square.PayloadTag = payloadTag;
        square.Text = text;
        square.TooltipText = tooltip;
        return square;
    }

    /// <summary>🔴 引擎内建拖放**源**：返回 `<族 tag>:<id>` 作为拖动载荷 ✓</summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        string encoded = DragPayload.Encode(PayloadTag, Payload);
        if (encoded.Length == 0)
        {
            return default;   // 没有载荷 ⇒ 不开始拖动（如实拒绝，不静默拖空载荷）✓
        }

        SetDragPreview(new Label
        {
            Text = Text,
            CustomMinimumSize = new Vector2(34, 34),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,   // 预览跟着光标走 ⇒ 绝不能抢鼠标（否则孔收不到落点）✓
            Modulate = DdTheme.Highlight,
        });
        GD.Print($"[GearHeroSquare] 开始拖动方块：{encoded}（载荷 = 「族:id」串；引擎内建 drag-and-drop）✓");
        return encoded;
    }

    /// <summary>🔴 引擎内建拖放**结束**：没落在任何孔里 ⇒ `DraggedOut`（"拖出孔外 = 卸下"的判据只有这一处）✓</summary>
    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || Engine.IsEditorHint())
        {
            return;   // 编辑器里同样会派发通知 ⇒ 明确不接（回调本来就是游戏侧接的）✓
        }

        if (!IsDragSuccessful())
        {
            GD.Print($"[GearHeroSquare] 方块「{Payload}」拖出后未落在任何孔里 ⇒ DraggedOut ✓");
            DraggedOut?.Invoke();
        }
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            Text = PreviewText;
        }
    }
}
