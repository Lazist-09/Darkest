using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Godot;

namespace Darkest.UI;   // 命名纪律：一律 Darkest.UI（大写 UI）

/// <summary>
/// M6u · 建筑升级树「树 × 级（code）」全表（任务卡 dd1_workstreams.md:51）：
///   ① 树 × 级：一栋建筑的全部树逐棵列出（tavern ⇒ bar/gambling/brothel 3 棵）；
///   ② code 不是数字 level：每行首列直接打 buildings.json 的 code（a/b/c…）；
///   ③ 前置显示：每行末列打 prerequisites 的 tree_id·requirement_code（首档 ⇒ 无前置）；
///   ④ 多货币：currency_cost 逐项 type×amount（gold/bust/crest/deed/portrait）。
///
/// 为什么住在 ScrollContainer 里（引擎内建，不自造滚动逻辑）：
///   长表（酒馆 18 级 / 驿站 13 级）不向上传播最小尺寸（与 HamletRoot.NavScroll 同款实测结论），
///   且 LayoutAudit 对滚动容器内的内容豁免 ⇒ 不制造新的重叠判据读数。
///
/// 不发明 code 到数字等级的映射（红线 26）：我方升级路径（HeirloomStock）只有
///   blacksmith.weapon/armour 的 a..d 对 Lv1..4 有一手出处；其余树只显示数据本身，
///   当前进度由正文的「当前等级：Lv{n}」回答 ⇒ 表内不写状态。
/// </summary>
public partial class HamletRoot : Control
{
    private static BuildingsConfig? _buildingsCfg;
    private static bool _buildingsTried;

    /// <summary>buildings.json 解析缓存（只解析一次；失败 ⇒ 留痕并返回 null，不静默）</summary>
    private static BuildingsConfig? Buildings()
    {
        if (_buildingsTried)
        {
            return _buildingsCfg;
        }

        _buildingsTried = true;
        try
        {
            _buildingsCfg = BuildingsConfig.Parse(FileAccess.GetFileAsString(BuildingsConfig.ResPath));
        }
        catch (Exception ex)
        {
            GD.Print($"[UI 建筑弹窗] M6u 升级树全表：{BuildingsConfig.ResPath} 不可用 ⇒ 只显示数字等级链（不静默）：{ex.Message}");
        }

        return _buildingsCfg;
    }

    /// <summary>DD 升级树位里的「数字等级链」宿主（懒建一次，住在 _buildingPopupTrees 内）</summary>
    private HBoxContainer ChainHost()
    {
        if (_buildingPopupTrees?.GetNodeOrNull<HBoxContainer>("UpgradeTreeChain") is HBoxContainer chain)
        {
            return chain;
        }

        var made = new HBoxContainer { Name = "UpgradeTreeChain" };
        made.AddThemeConstantOverride("separation", 6);
        _buildingPopupTrees?.AddChild(made);
        return made;
    }

    /// <summary>把「树 × 级（code）」全表挂进 DD 升级树位（幂等：每次刷新重建行，滚动宿主只建一次）</summary>
    private void MountLevelTree(string building)
    {
        if (_buildingPopupTrees is null)
        {
            return;   // 弹窗未建（理论不可达）⇒ 不崩
        }

        // 每次现查（幂等 · 自愈）：宿主已建 ⇒ 复用；被拆 ⇒ 重建 ⇒ 不持野引用 ✓
        VBoxContainer list = EnsureLevelTreeHost(_buildingPopupTrees);
        foreach (Node old in list.GetChildren().ToArray())
        {
            list.RemoveChild(old);
            old.QueueFree();
        }

        BuildingsConfig? cfg = Buildings();
        IReadOnlyList<BuildingTreeConfig> trees = cfg is null
            ? Array.Empty<BuildingTreeConfig>()
            : cfg.TreesFor(building);
        if (trees.Count == 0)
        {
            GD.Print($"[UI 建筑弹窗] M6u 升级树全表：{building} 在 buildings.json 里没有对应树 ⇒ 空表（如实上报，不猜）");
            return;
        }

        int levels = 0;
        foreach (BuildingTreeConfig t in trees)
        {
            list.AddChild(PopupLine($"{t.Id}（{t.Levels.Count} 级）"));
            foreach (BuildingLevelConfig lv in t.Levels)
            {
                levels++;
                string cost = lv.CurrencyCost.Count == 0
                    ? "无"
                    : string.Join(" ＋ ", lv.CurrencyCost.Select(c => $"{c.Type}×{c.Amount}"));
                string pre = lv.Prerequisites.Count == 0
                    ? "无（首档）"
                    : string.Join(" ＋ ", lv.Prerequisites.Select(p => $"{p.TreeId}·{p.RequirementCode}"));
                list.AddChild(PopupLine($"· {lv.Code}｜花费 {cost}｜前置 {pre}"));
            }
        }

        GD.Print($"[UI 建筑弹窗] M6u 升级树全表就位：{building} ⇒ {trees.Count} 树 / {levels} 级（code ＋ 多货币 ＋ 前置，可滚动）");
    }

    /// <summary>懒建滚动宿主（Godot 内建 ScrollContainer；水平禁滚，垂直按需）</summary>
    private static VBoxContainer EnsureLevelTreeHost(VBoxContainer host)
    {
        if (host.GetNodeOrNull<ScrollContainer>("UpgradeLevelScroll") is ScrollContainer existing &&
            existing.GetNodeOrNull<VBoxContainer>("UpgradeLevelList") is VBoxContainer list)
        {
            return list;
        }

        var scroll = new ScrollContainer
        {
            Name = "UpgradeLevelScroll",
            CustomMinimumSize = new Vector2(200, 130),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        var box = new VBoxContainer { Name = "UpgradeLevelList" };
        box.AddThemeConstantOverride("separation", 2);
        // 🔴 必须显式 ExpandFill：内层不展开 ⇒ ScrollContainer 只按【最小宽】安置子节点，
        //    而 PopupLine 模板开了按词换行（最小宽 = 1）⇒ 实测 list=(1,6919)、每行 size=(1,75)（文字被压成 1 列竖条）✓
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(box);
        host.AddChild(scroll);
        return box;
    }
}
