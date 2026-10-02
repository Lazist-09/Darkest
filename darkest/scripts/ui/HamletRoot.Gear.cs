using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// 🆕 **M6u（`P4 ②`）· 铁匠铺【装备阶】UI**（2026-10-01 解冻窗口）—— 从 `HamletRoot.cs` 拆出（每文件 ≤600 行）✓
///
/// 施工单 `doc/architecture/tasks/p4_gear_ui_m6u.md` 的四条玩家路径：
///   ① 打开铁匠铺（nav「铁匠铺·武器／护甲」）⇒ 弹窗里出现【孔 ＋ 英雄方块 ＋ 升阶按钮】；
///   ② 把英雄方块**拖进孔**（或点方块）⇒ 选中该英雄（用户 2026-10-01 口径：DD 式「方块空洞」）✓
///   ③ 点升阶 ⇒ 内核 `HeroGearState.TryUpgrade` **真扣金币 / 真涨阶 / 真写事件**（UI 只转发，不自己算账）✓
///   ④ 升不动 ⇒ 按钮**置灰 + 悬停给理由**（理由 = `HeroGear.Why` 的**原文**，UI 不自造 ✓ 红线 21）
///
/// 🔴 三条纪律：
///   · **零数值**：花费 / 门槛 / 理由全部来自 `hero_upgrades.json` 与 `HeroGear`（本文件不写任何金币数字）✓
///   · **选人不整屏重画**：装备阶的选中住 `_gearHero`，**不调** `SelectHero`（那条会 `Refresh()` 全屏）✓
///   · 缺数据 ⇒ **机制关闭 + 打印**（opt-in），不静默当成"全第 0 阶"（与 `ExpeditionComposition` 同口径）✓
///
/// 🔴 拖放 = **引擎内建**（`Control._GetDragData / _CanDropData / _DropData` + `SetDragPreview`）
///    ⇒ 不引第三方库、不自研拖放协议（用户 2026-10-01：「不要自己造轮子」）✓
/// </summary>
// ⚠️ 2026-10-02 拆分（M11 ① 预警面第十七件）：原 485 行 ⇒ 3 片 —— 本片 = **呈现与接线**（字段/词表/接线/建行/刷新）
//    ＋ 共享冒烟助手 `FindSmokeArg`；动作族 ⇒ `.Gear.Actions.cs` · 冒烟族 ⇒ `.Gear.Smoke.cs`（旧「冒烟族」段注随族**逐字节**迁移）✓
public partial class HamletRoot : Control
{
    // ------------------------------------------------------------------
    // 装备阶行（**懒建**：只有真打开铁匠铺弹窗才建 ⇒ 城池主屏零新增节点）✓
    // ------------------------------------------------------------------
    private VBoxContainer? _gearRow;
    private GearHeroSlot? _gearSlot;
    private Label? _gearStatus;
    private readonly Dictionary<string, Button> _gearUpgradeButtons = new(StringComparer.Ordinal);   // 键 = weapon / armour
    private string? _gearHero;   // 装备阶选中者（与减压的 `_selectedHero` **分开**：两条路径互不干扰）✓

    /// <summary>装备阶行是否已建且挂进树（供冒烟断言）。</summary>
    public bool GearRowMounted => _gearRow is not null && _gearRow.IsInsideTree();

    /// <summary>装备阶行是否真的可见（弹窗打开且未被隐藏）。</summary>
    public bool GearRowVisible => GearRowMounted && _gearRow!.IsVisibleInTree();

    /// <summary>装备阶当前选中的英雄 id（供冒烟断言）。</summary>
    public string? GearHeroId => _gearHero;

    // ------------------------------------------------------------------
    // 轴 ↔ 建筑树 id（键 = `heirlooms.json` 的 building，逐字同 `hero_upgrades.json` 的 prerequisites.tree_id）✓
    // ------------------------------------------------------------------

    /// <summary>建筑 id ⇒ 装备轴；非铁匠铺 ⇒ null（本行只服务两条铁匠铺树）✓</summary>
    private static GearAxis? GearAxisOfBuilding(string building) => building switch
    {
        "blacksmith.weapon" => GearAxis.Weapon,
        "blacksmith.armour" => GearAxis.Armour,
        _ => null,
    };

    /// <summary>轴 ⇒ 后缀（weapon / armour；与 `HeroGear.BuildingTreeId` 同词表）✓</summary>
    private static string GearAxisSuffix(GearAxis axis) => axis == GearAxis.Weapon ? "weapon" : "armour";

    /// <summary>轴 ⇒ 玩家可读词（武器 / 护甲）✓</summary>
    private static string GearAxisWord(GearAxis axis) => axis == GearAxis.Weapon ? "武器" : "护甲";

    // ------------------------------------------------------------------
    // 接线（容器 + 升级树数据；照 `ExpeditionComposition` 的口径：缺一即"机制关闭"，不静默）✓
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 补齐 `ExpeditionContext` 的两件前置（**只在 null 时建/绑**，已有则不覆盖）：
    ///   ① `Gear` = `HeroGearState`（**容器与数据在不在无关**：空表 = 全员第 0 阶 ≠ 未接线）；
    ///   ② `Upgrades` = `hero_upgrades.json`（**文件缺 / 校验失败 ⇒ 打印 + 机制关闭**）✓
    /// ⚠️ Hamlet 直达路径（`--hamlet`）此前**无任何绑定** ⇒ 不补这一步，铁匠铺升阶按钮永远"机制关闭"✓
    /// </summary>
    private void EnsureGearWiring()
    {
        if (ExpeditionContext.Gear is null)
        {
            ExpeditionContext.BindGear(new HeroGearState());
            GD.Print("[HamletRoot] 装备阶容器已挂（Hamlet 直达路径；空表 = 全员第 0 阶 ≠ 未接线）✓");
        }

        if (ExpeditionContext.Upgrades is not null)
        {
            return;
        }

        if (!FileAccess.FileExists(HeroUpgradesConfig.ResPath))
        {
            GD.Print($"[HamletRoot] 未找到 {HeroUpgradesConfig.ResPath} ⇒ 装备阶**升级机制关闭**（opt-in；不静默）");
            return;
        }

        try
        {
            HeroUpgradesConfig up = HeroUpgradesConfig.Parse(FileAccess.GetFileAsString(HeroUpgradesConfig.ResPath));
            // 外部树目录 = **全部建筑树**（`buildings.json` 是唯一真值；只列铁匠铺两条 ⇒ `guild.skill_levels` 判悬空 ⇒ 整表失败）✓
            BuildingsConfig bld = BuildingsConfig.Parse(FileAccess.GetFileAsString(BuildingsConfig.ResPath));
            up.Validate(bld.TreeIds);
            ExpeditionContext.BindUpgrades(up);
            GD.Print($"[HamletRoot] 装备升级树已加载并校验通过：{up.TreeCount} 树 / {up.LevelCount} 等级 ／ 外部树目录 {bld.TreeIds.Count} 条 ✓");
        }
        catch (Exception ex)
        {
            GD.Print($"[HamletRoot] hero_upgrades 校验失败 ⇒ 装备阶升级机制关闭（opt-in；如实上报）：{ex.Message}");
        }
    }

    /// <summary>
    /// 🔴 **懒建装备阶行**（只在真打开铁匠铺弹窗时建；建好挂进弹窗正文区）：
    ///   孔（拖放目标）＋ 英雄方块网格（拖放源 ／ 点击兜底）＋ 两颗升阶按钮 ＋ 一行状态 ✓
    /// ⚠️ 场景模板缺失 ⇒ **回落代码构建**（不崩、不静默）✓
    /// </summary>
    private void EnsureGearRow()
    {
        if (_gearRow is not null || _buildingPopupBody is null)
        {
            return;
        }

        EnsureGearWiring();

        var row = new VBoxContainer { Name = "GearRow" };
        row.AddThemeConstantOverride("separation", 6);

        var head = new HBoxContainer { Name = "GearHead" };
        head.AddThemeConstantOverride("separation", 8);
        _gearSlot = GearHeroSlot.TryCreate();
        if (_gearSlot is not null)
        {
            _gearSlot.Dropped += SelectGearHero;   // 🔴 引擎落孔回调 ⇒ 选人（与点击**同一条**入口）✓
            head.AddChild(_gearSlot);
        }
        else
        {
            GD.Print("[HamletRoot] 装备阶：孔模板不可用（scenes/ui/gear_hero_slot.tscn）⇒ **拖放不可用**（点方块仍可用）");
        }

        var grid = new GridContainer { Name = "GearHeroGrid", Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        IReadOnlyList<HeroConfig> heroes = ExpeditionContext.Roster?.Heroes ?? Array.Empty<HeroConfig>();
        foreach (HeroConfig h in heroes)
        {
            string heroId = h.Id;
            string abbrev = h.Name.Length > 0 ? h.Name[..1] : "?";
            string tip = $"{h.Name}　{h.Archetype} Lv{h.Level}（拖进孔中，或点它）";
            Button square = (Button?)GearHeroSquare.TryCreate(heroId, abbrev, tip) ?? new Button
            {
                Name = $"GearHero_{heroId}",
                Text = abbrev,
                CustomMinimumSize = new Vector2(34, 34),
                TooltipText = tip,
            };
            square.Pressed += () => SelectGearHero(heroId);
            grid.AddChild(square);
        }

        head.AddChild(grid);
        row.AddChild(head);

        var bar = new HBoxContainer { Name = "GearUpgradeBar" };
        bar.AddThemeConstantOverride("separation", 8);
        foreach (GearAxis axis in new[] { GearAxis.Weapon, GearAxis.Armour })
        {
            var btn = new Button
            {
                Name = $"GearUpgrade_{GearAxisSuffix(axis)}",
                Text = $"升{GearAxisWord(axis)}阶",
                CustomMinimumSize = new Vector2(210, 36),
            };
            btn.Pressed += () => UpgradeGear(axis);
            bar.AddChild(btn);
            _gearUpgradeButtons[GearAxisSuffix(axis)] = btn;
        }

        row.AddChild(bar);
        _gearStatus = PopupLine("装备阶：请先把英雄方块拖进孔中（或点方块）");
        row.AddChild(_gearStatus);

        _buildingPopupBody.AddChild(row);
        _gearRow = row;
        GD.Print($"[HamletRoot] 装备阶行已挂：孔={(_gearSlot is not null ? "有" : "无（回落）")}" +
                 $"　英雄方块 {heroes.Count} 个　升阶按钮 {_gearUpgradeButtons.Count} 颗 ✓");
        RefreshGearRow();
    }

    /// <summary>
    /// 🔴 重画装备阶行（**真读** `HeroGearState` / `HeirloomStock` / `Economy`；可用性由 `HeroGear.Why` 回答）✓
    /// ⚠️ 行未建 / 弹窗不是铁匠铺 ⇒ 空操作或隐藏（幂等，随便调）✓
    /// </summary>
    private void RefreshGearRow()
    {
        if (_gearRow is null)
        {
            return;
        }

        GearAxis? axisOfBuilding = _buildingPopupId is null ? null : GearAxisOfBuilding(_buildingPopupId);
        if (axisOfBuilding is not { } axis)
        {
            _gearRow.Visible = false;   // 只服务铁匠铺两条树；别的建筑不显示（不静默显示别的轴）✓
            return;
        }

        _gearRow.Visible = true;
        HeroConfig? hero = _gearHero is null
            ? null
            : ExpeditionContext.Roster?.Heroes.FirstOrDefault(h => h.Id == _gearHero);
        HeroGearState? gear = ExpeditionContext.Gear;
        HeroUpgradesConfig? cfg = ExpeditionContext.Upgrades;
        Economy? economy = ExpeditionContext.Gold;
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        int buildingLevel = heirlooms?.LevelOf(HeroGear.BuildingTreeId(axis)) ?? 0;

        // 孔：空 ⇒ ＋；有选中 ⇒ 英雄首字（悬停给全名 ⇒ 孔自己也能解释"这是谁"）✓
        if (_gearSlot is not null)
        {
            string glyph = hero is null ? "＋" : (hero.Name.Length > 0 ? hero.Name[..1] : "?");
            _gearSlot.SetGlyph(glyph, hero is null
                ? "把英雄方块拖进这个孔（或点方块）⇒ 表示用这个人"
                : $"{hero.Name}　{hero.Archetype} Lv{hero.Level}（再拖一个方块进来 = 换人）");
        }

        int tier = hero is null || gear is null ? 0 : gear.TierOf(hero.Id, axis);
        string costText = "—";
        if (hero is not null && cfg is not null)
        {
            HeroUpgradeTreeConfig? tree = HeroGear.TreeOf(cfg, hero.Archetype, axis);
            HeroUpgradeLevelConfig? lv = tree?.Levels.FirstOrDefault(
                l => l.Code == tier.ToString(CultureInfo.InvariantCulture));
            if (lv is not null)
            {
                costText = $"{HeroGear.GoldCostOf(lv)} 金";
            }
        }

        // 🔴 置灰的理由：**只用内核原文**（`HeroGear.Why` ／ "机制关闭" ／ "先选人"）—— UI 不自造 ✓
        string? why = hero is null
            ? "先把英雄方块拖进孔中（或点方块）"
            : cfg is null || gear is null || economy is null
                ? "hero_upgrades.json 未加载 ⇒ 装备阶机制关闭（opt-in）"
                : HeroGear.Why(cfg, hero.Archetype, axis, tier, buildingLevel, hero.Level, economy.Gold);

        foreach ((string suffix, Button btn) in _gearUpgradeButtons)
        {
            btn.Visible = suffix == GearAxisSuffix(axis);
            if (!btn.Visible)
            {
                continue;
            }

            btn.Disabled = why is not null;
            btn.Text = hero is null
                ? $"升{GearAxisWord(axis)}阶（先选人）"
                : tier >= HeroGear.MaxTier
                    ? $"已满级（第 {HeroGear.MaxTier} 阶）"
                    : $"升{GearAxisWord(axis)}阶 ⇒ 第 {tier + 1} 阶（{costText}）";
            btn.TooltipText = why ?? $"花金币升{GearAxisWord(axis)}阶（可用性由 HeroGear.Why 回答；当前 {tier} / {HeroGear.MaxTier}）";
        }

        if (_gearStatus is not null)
        {
            string who = hero is null ? "（未选人）" : $"{hero.Name}（{hero.Id}）";
            _gearStatus.Text = $"装备阶 · {GearAxisWord(axis)}轴：{who}　当前 {tier} / {HeroGear.MaxTier}" +
                               $"　铁匠铺「{GearAxisSuffix(axis)}」Lv{buildingLevel}　" +
                               (why is null ? "⇒ 可以升 ✓" : $"⛔ {why}");
        }
    }

    // ------------------------------------------------------------------
    // 共享冒烟助手（`FindSmokeArg` —— 5 个 partial 共用：Recruit ／ Trinkets ／ Provision ／ Build ／ Gear 冒烟族；本片只留这一件）✓
    // ------------------------------------------------------------------

    /// <summary>取 `--xxx=` 后面的值（没有 ⇒ null）✓</summary>
    private static string? FindSmokeArg(string[] args, string prefix)
        => Array.Find(args, a => a.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
