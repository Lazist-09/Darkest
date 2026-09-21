using Godot;

namespace Darkest.UI;

/// <summary>DD 1:1 ②：任务选择屏控制器（开屏 + 按地牢设 DD 坐标 + 关闭）—— 与 HamletRoot 同 partial ✓</summary>
public partial class HamletRoot : Control
{
    /// <summary>DD：按地牢切换 任务地图/全图/覆盖层 的坐标（/1920、/1080 折比例）✓</summary>
    public void ApplyQuestMapAnchors(string dungeon)
    {
        QuestSelectSkeleton? skel = _questSkel;
        if (skel is null)
        {
            return;
        }

        (float mx, float my, float ax, float ay, float ox, float oy) = dungeon switch
        {
            "cove" => (1260f, 400f, 850f, 380f, 1270f, 480f),
            "darkest_dungeon" => (1260f, 100f, 850f, 540f, 1230f, 70f),
            _ => (940f, 220f, 850f, 80f, 1050f, 260f),   // crypts（默认）
        };

        SetQuestBox(skel.QuestMap, mx, my, 0.30f, 0.28f);
        SetQuestBox(skel.AllQuestMap, ax, ay, 0.30f, 0.28f);
        SetQuestBox(skel.DungeonEffectOverlay, ox, oy, 0.30f, 0.24f);
        GD.Print($"[UI 任务选择] 坐标按地牢 {dungeon}：quest_map=({mx},{my}) all=({ax},{ay}) overlay=({ox},{oy})（DD 原文）");
    }

    private static void SetQuestBox(Control? c, float x, float y, float w, float h)
    {
        if (c is null)
        {
            return;
        }

        c.AnchorLeft = x / 1920f;
        c.AnchorTop = y / 1080f;
        c.AnchorRight = x / 1920f + w;
        c.AnchorBottom = y / 1080f + h;
    }

    private QuestSelectSkeleton? _questSkel;

    /// <summary>开【任务选择】屏（骨架优先；缺失 ⇒ 回落一行说明）✓</summary>
    public void OpenQuestSelect(string dungeon = "crypts")
    {
        (PanelContainer self, _, VBoxContainer body) = MakePopup("QuestSelectPopup", "📜 【任务选择】", Darkest.UI.PopupLayout.FullScreen);
        QuestSelectSkeleton? skel = QuestSelectSkeleton.TryInstantiate();
        if (skel is not null)
        {
            _questSkel = skel;
            body.AddChild(skel);
            ApplyQuestMapAnchors(dungeon);
            if (skel.QuestList is VBoxContainer list)
            {
                list.AddChild(PopupLine($"当前地牢：{dungeon}（坐标已按 DD 原文切换）"));
                list.AddChild(PopupLine("任务列表：内核未提供接口 ⇒ 本屏当前只还原 DD 布局（不伪造内容）"));
            }

            if (skel.Close is Button close)
            {
                // 🔴 2026-09-20：`self` 由 `MakePopup` 直接返回（此前 `body.GetParent()?.GetParent() as PanelContainer`
                //    在代码回落路径下取到的是 MarginContainer ⇒ cast 失败 ⇒ **关不掉**）✓
                close.Pressed += () => ClosePopup(self, "QuestSelectPopup");
            }
        }
        else
        {
            body.AddChild(PopupLine("任务选择：骨架不可用（回落文本，不静默）"));
        }

        GD.Print($"[HamletRoot] OpenQuestSelect：已开屏（骨架{(skel is not null ? "采用" : "回落")}）");
    }
}
