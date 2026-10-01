using System;
using System.Collections.Generic;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// 🆕 **M12 · 供应屏控制器**（DD 1:1 ② `provision`）—— 开屏 + 骨架优先 + **四个 DD 锚点接真数据** + 关闭 ✓
/// 与 `HamletRoot` 同 partial ⇒ 复用 `MakePopup` / `ClosePopup` / `TakeAnchor` / `PopupLine` ✓
///
/// 🔴 纪律（红线 21）：**没有内核来源的字段一律如实写「待内核接口」**，不编造任务内容 / 售回价 ✓
/// 🔴 数据来源（全部真读，UI 不自己算一份）：
///   · 侦察 = `TuningConfig` 的 `light.enter_value` ＋ `scouting.base_pct` ⇒ `Scouting.ChanceFor`（**内核同一函数**）✓
///   · 售回 = `ProvisionsConfig.sell_gold` × `ProvisionStock.CountOf`（城内库存）✓
///   · 队伍 = `FormationSortie.SelectForTemplate`（**与出征同一份选人** ⇒ 屏上 6 人就是本趟要带的 6 人）✓
///   · 商店 = `HamletRoot.Provision.Store.cs`（买卖只转发 `ProvisionStock`，UI 不算账）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>供应屏是否已开（供冒烟断言）✓</summary>
    public bool ProvisionOpen => _popups.TryGetValue("ProvisionPopup", out (PanelContainer Panel, Label Title, VBoxContainer Body) p) && p.Panel.Visible;

    /// <summary>商店状态行原文（供冒烟断言；未开屏 ⇒ 空串）✓</summary>
    public string ProvisionStatusText => LiveProvStatus?.Text ?? string.Empty;   // 弹窗重开/关闭后旧引用会被释放 ⇒ 验活后再读 ✓

    /// <summary>开【供应】屏（骨架优先；缺失 ⇒ 回落一行说明，不崩不静默）✓</summary>
    public void OpenProvision()
    {
        (PanelContainer self, _, VBoxContainer body) = MakePopup("ProvisionPopup", "🛒 【供应】", Darkest.UI.PopupLayout.FullScreen);
        ProvisionSkeleton? skel = ProvisionSkeleton.TryInstantiate();
        if (skel is null)
        {
            body.AddChild(PopupLine("供应：骨架不可用（回落文本，不静默）"));
            GD.Print("[HamletRoot] OpenProvision：已开屏（骨架回落）");
            return;
        }

        body.AddChild(skel);
        FillProvisionQuestInfo(skel);
        FillProvisionScouting(skel);
        FillProvisionSellBack(skel);
        FillProvisionParty(skel);
        EnsureSupplies();            // 见 `HamletRoot.Provision.Store.cs`（缺数据 ⇒ 机制关闭 + 打印）✓
        MountProvisionStore(skel);   // 11 行商品（名/价/库存）＋ 状态行 ✓

        if (skel.Close is Button close)
        {
            // 🔴 2026-09-20：`self` 由 `MakePopup` 直接返回（此前 `body.GetParent()?.GetParent() as PanelContainer`
            //    在代码回落路径下取到的是 MarginContainer ⇒ cast 失败 ⇒ **关不掉**）✓
            close.Pressed += () => ClosePopup(self, "ProvisionPopup");
        }

        GD.Print("[HamletRoot] OpenProvision：已开屏（骨架采用）");
    }

    /// <summary>
    /// 任务信息位（DD `provision quest_info_pos 1300,96`）—— 🔴 **内核无此接口** ⇒ 盒内只放占位符 "—"，
    /// 全文写进 `InfoCol.QuestInfo`（**不编造任务内容**；红线 21 如实上报）✓
    /// </summary>
    private void FillProvisionQuestInfo(ProvisionSkeleton skel)
    {
        AnchorValue(skel.ProvQuestInfoAnchor, "ProvQuestInfoValue", "—",
            "任务信息位（DD provision quest_info_pos 1300,96）· **内核未提供接口**（ProvisionQuestInfo）⇒ 只放占位符，不编造任务内容 ✓");
        if (skel.QuestInfo is Label info)
        {
            info.Text = "任务信息：待内核接口（ProvisionQuestInfo）—— 本屏只还原 DD 布局，不伪造任务内容";
        }
    }

    /// <summary>
    /// 侦察数值位（DD `provision scouting_stat_pos 1380,96`）—— **真读**：`light.enter_value` 档光照下的
    /// `Scouting.ChanceFor`（与远征里掷骰用的是**同一个函数**）✓ 读不到 ⇒ 如实写「不可用」，不写死数字 ✓
    /// </summary>
    private void FillProvisionScouting(ProvisionSkeleton skel)
    {
        string box;
        string full;
        try
        {
            if (!FileAccess.FileExists(TuningConfig.ResPath))
            {
                (box, full) = ("—", $"侦察：{TuningConfig.ResPath} 缺失 ⇒ 数值不可用（不编造）");
            }
            else
            {
                TuningConfig tuning = TuningConfig.Parse(FileAccess.GetFileAsString(TuningConfig.ResPath));
                if (tuning.Light is null || tuning.Scouting is null)
                {
                    (box, full) = ("—", "侦察：tuning.light ／ tuning.scouting 未配置 ⇒ 数值不可用（不编造）");
                }
                else
                {
                    int lightValue = tuning.Light.EnterValue;
                    double chance = new Scouting(tuning.Scouting, tuning.Light).ChanceFor(lightValue);
                    box = $"{chance:0.#}%";
                    full = $"侦察：光照 {lightValue} ⇒ {chance:0.#}%（tuning.light.enter_value ＋ scouting.base_pct；与远征掷骰同一函数）";
                }
            }
        }
        catch (Exception ex)
        {
            (box, full) = ("—", $"侦察：读数失败（{ex.GetType().Name}）⇒ 不编造");
        }

        AnchorValue(skel.ProvScoutingAnchor, "ProvScoutingValue", box,
            "侦察数值位（DD provision scouting_stat_pos 1380,96）· 数值 = Scouting.ChanceFor(光照档)（内核同一函数）✓");
        if (skel.ScoutingStat is Label stat)
        {
            stat.Text = full;
        }
    }

    /// <summary>
    /// 售回信息位（DD `provision_sell_back_info_pos 1164,510`）—— **真读**城内库存与卖回价；
    /// ⚠️ **背包卖回**（把本趟带回来的东西卖回城）内核无接口 ⇒ 如实写「待内核接口（ProvisionSellFromBag）」✓
    /// </summary>
    private void FillProvisionSellBack(ProvisionSkeleton skel)
    {
        ProvisionStock? stock = ExpeditionContext.Supplies;
        int total = stock?.TotalCount ?? 0;
        AnchorValue(skel.ProvSellBackAnchor, "ProvSellBackValue", $"{total} 件",
            "售回信息位（DD provision_sell_back_info_pos 1164,510）· 读数 = 城内库存总件数（ProvisionStock.TotalCount）✓");
        if (skel.SellBackInfo is Label info)
        {
            info.Text = stock is null
                ? $"售回：供应机制关闭（{ProvisionsConfig.ResPath} 未加载）⇒ 库存不可用 · 背包卖回待内核接口（ProvisionSellFromBag）"
                : $"售回：城内库存 {total} 件（可卖）· 背包卖回待内核接口（ProvisionSellFromBag）";
        }
    }

    /// <summary>
    /// 队伍格（DD `party grid 60,28 格 80x160`，8 列）—— **真读本趟出征 6 人**：
    /// 与出征走**同一份选人** `FormationSortie.SelectForTemplate`（不另算一份，红线 26）✓
    /// 方块用 `GearHeroSquare`（DD 式方块 ＋ 引擎内建拖放源）⇒ **不造轮子** ✓
    /// ⚠️ 拖动换位未接线（DD 里可拖方块换位）⇒ 写进 tooltip，登记 O-113 ✓
    /// </summary>
    private void FillProvisionParty(ProvisionSkeleton skel)
    {
        if (skel.PartyGrid is not GridContainer grid)
        {
            GD.Print("[UI 供应] 缺 `PartyGrid` 锚点 ⇒ 队伍格无处可挂（红线 21 如实上报）");
            return;
        }

        RosterConfig? rosterCfg = _rosterCfgForDetail;
        if (rosterCfg is null)
        {
            grid.AddChild(PopupLine("队伍：名册未加载 ⇒ 读不到出征名单（不伪造 6 个空位）"));
            return;
        }

        IReadOnlyList<HeroConfig> sortie;
        try
        {
            if (!FileAccess.FileExists(FormationConfig.ResPath))
            {
                grid.AddChild(PopupLine($"队伍：{FormationConfig.ResPath} 缺失 ⇒ 读不到阵型模板（不编造）"));
                return;
            }

            FormationConfig template = FormationConfig.Parse(FileAccess.GetFileAsString(FormationConfig.ResPath));
            sortie = FormationSortie.SelectForTemplate(template, rosterCfg);
        }
        catch (Exception ex)
        {
            grid.AddChild(PopupLine($"队伍：阵型读取失败（{ex.GetType().Name}）⇒ 不编造"));
            return;
        }

        int slot = 0;
        foreach (HeroConfig h in sortie)
        {
            slot++;
            string heroId = h.Id;
            string abbrev = h.Name.Length > 0 ? h.Name[..1] : "?";
            string tip = $"队伍位 {slot}：{h.Name}　{h.Archetype} Lv{h.Level}（与出征同一份选人）· 拖动换位待接线（O-113）";
            Button square = (Button?)GearHeroSquare.TryCreate(heroId, abbrev, tip) ?? new Button
            {
                Name = $"GearHero_{heroId}",
                Text = abbrev,
                CustomMinimumSize = new Vector2(34, 34),
                TooltipText = tip,
            };
            grid.AddChild(square);
        }

        GD.Print($"[HamletRoot] 供应屏队伍：{sortie.Count} 人（FormationSortie 与出征同一份选人）✓");
    }

    /// <summary>
    /// DD 锚点里的**短读数**（盒内放得下的一两个字符）：接管锚点（清掉占位 PurposeLabel/BlockPlaceholder）后放一个居中 Label；
    /// 全文写进对应 `InfoCol` 标签（锚点只有 80×43 @1920 ⇒ 长句放不进去，放进去必被裁）✓
    /// ⚠️ 锚点缺失 ⇒ `TakeAnchor` 会打印并返回游离容器（红线 21，不静默丢弃）✓
    /// </summary>
    private static Label AnchorValue(PanelContainer? anchor, string name, string text, string tooltip)
    {
        Label host = TakeAnchor<Label>(anchor, name);
        host.Text = text;
        host.TooltipText = tooltip;
        host.HorizontalAlignment = HorizontalAlignment.Center;
        host.VerticalAlignment = VerticalAlignment.Center;
        host.AddThemeFontSizeOverride("font_size", 12);
        host.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextInfo);
        return host;
    }
}
