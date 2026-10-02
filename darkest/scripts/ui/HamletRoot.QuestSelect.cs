using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 ②：任务选择屏控制器（开屏 + 按地牢设 DD 坐标 + **任务列表真接线** + 关闭）—— 与 HamletRoot 同 partial ✓
/// 🆕 M14：列表改读 `quests.json`（此前是「内核未提供接口」占位句 ⇒ 已删）✓
/// </summary>
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
            PopulateQuestList(skel, dungeon);

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

    /// <summary>
    /// 🔴 **M14 · 任务列表真接线**（读 `quests.json` ⇒ 按地牢筛 `plot_quests` + 显示该难度的 resolve 上限）✓
    /// 三条纪律：① 读取/校验失败或该地牢无条目 ⇒ **如实回落**（红线 21，不伪造内容）
    ///          ② 上限语义 = 「该难度允许的**最高** resolve level」（`null` = 无上限 —— `04b` §5.4 实测）
    ///          ③ 词表归一：坐标表用 `darkest_dungeon`（带下划线）· 数据用 `darkestdungeon`（无下划线）✓
    /// </summary>
    private void PopulateQuestList(QuestSelectSkeleton skel, string dungeon)
    {
        if (skel.QuestList is not VBoxContainer list)
        {
            GD.Print("[UI 任务选择] 骨架缺 QuestList 锚点 ⇒ 列表未接线（如实上报，不静默）");
            return;
        }

        QuestsConfig cfg;
        try
        {
            cfg = QuestsConfig.Parse(FileAccess.GetFileAsString(QuestsConfig.ResPath));
        }
        catch (Exception ex)
        {
            list.AddChild(PopupLine($"任务表未加载 ⇒ 列表为空（{ex.Message}）"));
            GD.Print($"[UI 任务选择] `{QuestsConfig.ResPath}` 读取/校验失败 ⇒ 列表为空：{ex.Message}");
            return;
        }

        string dataKey = dungeon == "darkest_dungeon" ? "darkestdungeon" : dungeon;
        IReadOnlyList<PlotQuestConfig> quests = cfg.PlotQuestsFor(dataKey);
        list.AddChild(PopupLine($"当前地牢：{dungeon}（坐标已按 DD 原文切换；任务表键 = {dataKey}）"));
        if (quests.Count == 0)
        {
            list.AddChild(PopupLine($"该地牢无任务条目（表内共 {cfg.PlotQuests.Count} 条）⇒ 如实为空，不伪造内容"));
            GD.Print($"[UI 任务选择] 地牢 {dataKey} ⇒ 0 条（表内 {cfg.PlotQuests.Count} 条 / {QuestsConfig.KnownDungeons.Count} 个地牢）");
            return;
        }

        foreach (PlotQuestConfig q in quests)
        {
            int? cap = cfg.ResolveLevelCapFor(q.Quest.Difficulty);
            string capText = cap is null ? "无等级上限" : $"允许 resolve ≤ {cap}";
            string goalText = string.Join("、", q.Quest.GoalIds.Select(id =>
                cfg.GoalById(id) is { } g ? $"{id}（{g.Type}）" : id));
            list.AddChild(PopupLine($"📜 {q.Id}　{q.Quest.Type} · 难度{q.Quest.Difficulty} · 长度{q.Quest.Length}"));
            list.AddChild(PopupLine($"　目标：{goalText}　{capText}"));
        }

        GD.Print($"[UI 任务选择] 列表接线：地牢 {dataKey} ⇒ {quests.Count} 条（表内共 {cfg.PlotQuests.Count} 条）✓");
    }
}
