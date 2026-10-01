using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;   // ExpeditionContext（名册/节杖）与 `HamletRoot.HeroDetail.cs` 同源 ✓
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// 🆕 **M4u · 角色详情 · 饰品 2 格**（2026-10-02 · `dd1_workstreams.md §3` · 契约 `doc/modules/trinkets.md §5`）✓
///
/// <para>本片 = `HamletRoot.HeroDetail.cs` 的**饰品分片**（该文件已 509 行 ⇒ 按红线「程序文件 ≤600 行」另立一片）✓</para>
///
/// <para>🔴 **槽位是孔，不是按钮**（用户 2026-10-02 原话：建筑详情选人 = 把方块头像**放进孔里**）——
/// 2 格都是 `GearHeroSlot`（**引擎内建** drag-and-drop 的落点）；**已装件**自己变成可拖出的方块
/// （`GearHeroSquare`）⇒ **拖进空孔 = 装上 ／ 拖出孔外松手（或点一下方块）= 卸下**，
/// 两条路都走 `Roster` 的真入口（红线 26：屏上读数与真状态同一份）✓</para>
///
/// <para>🔴 **置灰不靠透明度**（LayoutAudit 判据 2：面板样式 `BgColor.a` 必须 1.0）：
/// 「装不上」用**全不透明灰字 + 内核原样理由整行**表达 ⇒ 可断言、可读、不骗人 ✓</para>
///
/// <para>🔴 **理由的生产点在 `Roster.CanEquipTrinket`**（红线 21 (b)：判据只有一个落点）——
/// 本片**一句判据都不写**，只把内核返回值原样显示 ✓</para>
///
/// <para>⚠️ **装的方块从哪来（库存 ／ 商店）未接线** —— 属 M4u 后续；屏上如实标注，
/// 冒烟走 `--hamlet-trinket-seed` 的**公开 API 播种**（`Roster.EquipTrinket`）喂方块 ✓</para>
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>🆕 M4u：饰品库（`trinkets.json` 196 条）—— 详情 2 格的**名字/职业要求/悬停全文**都真读它（UI 不写死 id）✓</summary>
    private TrinketsConfig? _trinketsCfg;

    /// <summary>详情右栏的 2 个饰品格（`GridContainer` 2 列 —— DD `hero_trinket_grid_layout.number_of_columns 2`）✓</summary>
    private GridContainer? _detailTrinketGrid;

    /// <summary>饰品格下方的读数行（已装 N/2 ／ 不可装备理由 —— 冒烟断言就断它）✓</summary>
    private Label? _detailTrinketNote;

    // 🔴 冒烟播种**被拒**的候选件（T4 读数）：职业不符/重复/槽满 ⇒ 在详情里以「不可装备」整行呈现（理由 = 内核原样返回）✓
    private string? _trinketRefusedHeroId;
    private string? _trinketRefusedTrinketId;
    private string? _trinketRefusedReason;

    /// <summary>供冒烟断言：详情里饰品读数行的**真文本**（详情未开 ⇒ 空串）✓</summary>
    public string TrinketNoteText => _detailTrinketNote?.Text ?? string.Empty;

    /// <summary>
    /// **懒建 2 个饰品格 + 读数行**（只建一次）—— 🔴 骨架给了 `HeroTrinketGrid` 就用骨架的格（编辑器里可改），
    /// 没给则代码建；两条路都收敛到同一个 `_detailTrinketGrid`（刷新入口唯一）✓
    /// </summary>
    private void EnsureTrinketSlots(Control parent)
    {
        if (_detailTrinketGrid is null)
        {
            var grid = new GridContainer { Name = "HeroTrinketGrid", Columns = Roster.MaxTrinketSlots };
            grid.AddThemeConstantOverride("h_separation", 92);    // DD offset 92 ×0.667 ≈ 61 ✓
            grid.AddThemeConstantOverride("v_separation", 160);   // DD offset 160 ×0.667 ≈ 107 ✓
            parent.AddChild(grid);
            parent.MoveChild(grid, Math.Min(1, parent.GetChildCount() - 1));   // DD：饰品格紧随装备位（装备 0 ⇒ 饰品 1）✓
            _detailTrinketGrid = grid;
        }

        if (_detailTrinketNote is null)
        {
            var note = new Label
            {
                Name = "DetailTrinketNote",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                // 🔴 `MouseFilter` 必须显式给：Godot 的 `Label` 默认不吃鼠标（不给 ⇒ tooltip 不弹）；用 Pass（不是 Stop）
                //    ⇒ 悬停可读全文，且滚轮事件继续上抛到外层 `ScrollContainer`（规则①：正文必须可滚）✓
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            parent.AddChild(note);
            parent.MoveChild(note, Math.Min(2, parent.GetChildCount() - 1));
            _detailTrinketNote = note;
        }

        GD.Print($"[UI 英雄面板] ✅ 饰品 {Roster.MaxTrinketSlots} 孔就位（真读数 · 同源 Roster.TrinketsOf；" +
                 "孔 = GearHeroSlot，只收 trinket 族载荷）✓");
    }

    /// <summary>
    /// 🔴 **刷新饰品格（每次开详情 ＋ 每次装卸都现读）** —— 红线 26：屏上读数与 `Roster` 是**同一份**，
    /// 不缓存、不写死 id；库未加载 ⇒ 如实标注（不假装有名字）✓
    /// <para>⚠️ 装卸回调里**不能立刻重建**发信号的那个孔（引擎还在它身上派发 `NOTIFICATION_DRAG_END`）
    /// ⇒ 装卸后的刷新走**延迟一帧**（见 `AfterTrinketChange`）；本方法本身是同步的 ✓</para>
    /// </summary>
    private void RefreshTrinketSlots(string heroId)
    {
        if (_detailTrinketGrid is null || _detailTrinketNote is null)
        {
            GD.Print("[HamletRoot] 饰品读数：格未建 ⇒ 跳过（不静默假装刷过）");
            return;
        }

        foreach (Node c in _detailTrinketGrid.GetChildren().ToArray())
        {
            _detailTrinketGrid.RemoveChild(c);
            c.QueueFree();
        }

        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            _detailTrinketNote.Text = "【饰品】🔴 名册未加载 ⇒ 读数不可用";
            return;
        }

        IReadOnlyList<string> held = roster.TrinketsOf(heroId);   // 🔴 现读（红线 26）✓
        var readings = new List<string>();
        for (int slot = 1; slot <= Roster.MaxTrinketSlots; slot++)
        {
            bool filled = slot <= held.Count;
            string tid = filled ? held[slot - 1] : string.Empty;
            _detailTrinketGrid.AddChild(filled
                ? BuildFilledTrinketHole(heroId, tid, slot)
                : BuildEmptyTrinketHole(heroId, slot));
            readings.Add(filled ? $"槽{slot}={tid}" : $"槽{slot}=空");
        }

        // 🔴 T4 读数：被拒的候选件以**整行理由**呈现（置灰 = 全不透明灰字，绝不调透明度）✓
        bool showRefusal = _trinketRefusedHeroId == heroId && _trinketRefusedReason is not null;
        if (showRefusal)
        {
            _detailTrinketGrid.AddChild(BuildRefusedCandidatePanel());
            readings.Add($"候选「{_trinketRefusedTrinketId}」=不可装备");
        }

        string summary = $"【饰品】{held.Count}/{Roster.MaxTrinketSlots} 格已装";
        if (showRefusal)
        {
            summary += $"\n🔴 不可装备：「{_trinketRefusedTrinketId}」—— {_trinketRefusedReason}";
            _detailTrinketNote.TooltipText = $"不可装备理由（内核原样）\n{_trinketRefusedReason}\n" +
                "（来源 Roster.CanEquipTrinket —— 唯一判据落点）✓";
        }
        else
        {
            summary += "　（装卸已接线：方块拖进空孔 = 装上 · 拖出孔外松手 ／ 点方块 = 卸下）";
            _detailTrinketNote.TooltipText =
                "饰品格：同源 Roster.TrinketsOf；装上走 Roster.EquipTrinket ／ 卸下走 Roster.UnequipTrinket" +
                "（两者必写 HeroTrinketEquippedEvent ／ HeroTrinketUnequippedEvent）\n" +
                "⚠️ 装的方块从哪来（库存 ／ 商店）未接线 ⇒ 本屏只做装卸；冒烟用 --hamlet-trinket-seed 喂方块";
        }

        _detailTrinketNote.Text = summary;
        GD.Print($"[M4u·饰品] {heroId}：{string.Join("｜", readings)}｜{summary.Replace('\n', '｜')}" +
                 $"（库 {(_trinketsCfg is null ? "未加载" : $"{_trinketsCfg.Trinkets.Count} 条")}）✓");
    }

    /// <summary>空孔（`＋` 字形；悬停说清「为什么现在装不上」 —— 红线 21：不留不可解释的空框）✓</summary>
    private Control BuildEmptyTrinketHole(string heroId, int slot)
    {
        string tip = EmptySlotTooltip(slot);
        GearHeroSlot? hole = GearHeroSlot.TryCreate();
        if (hole is null)
        {
            GD.Print("[M4u·饰品] 🔴 孔模板 gear_hero_slot.tscn 不可用 ⇒ 本格回落纯读数框（拖放不可用，如实上报不静默）");
            return FallbackHole(slot, "空槽", tip);
        }

        hole.Name = $"HeroTrinketSlot{slot}";     // 🔴 冒烟锚点：节点名与接线前**一字不改**（落孔冒烟按它找孔）✓
        hole.AcceptTag = DragPayload.TrinketTag;  // 🔴 只收饰品族 ⇒ 英雄方块拖进来当场拒绝（不拿英雄 id 去查饰品库而炸）✓
        hole.Dropped += id => EquipTrinketFromSlot(heroId, id);
        hole.SetGlyph("＋", tip);
        return hole;
    }

    /// <summary>
    /// 已装格：孔里放**可拖出的方块**（`GearHeroSquare`，载荷族 = trinket）——
    /// **拖出孔外松手**（引擎 `NOTIFICATION_DRAG_END` ＋ `IsDragSuccessful() == false`）或**点一下方块** = 卸下 ✓
    /// </summary>
    private Control BuildFilledTrinketHole(string heroId, string trinketId, int slot)
    {
        string tip = TrinketTooltip(trinketId, slot);
        GearHeroSlot? hole = GearHeroSlot.TryCreate();
        if (hole is null)
        {
            GD.Print("[M4u·饰品] 🔴 孔模板不可用 ⇒ 本格回落纯读数框（拖放不可用，如实上报不静默）");
            return FallbackHole(slot, trinketId, tip);
        }

        hole.Name = $"HeroTrinketSlot{slot}";
        hole.AcceptTag = DragPayload.TrinketTag;
        hole.Dropped += id => EquipTrinketFromSlot(heroId, id);   // 同族件落进已装格 ⇒ 判据仍在内核（重复 ／ 槽满）✓

        GearHeroSquare? square = GearHeroSquare.TryCreate(trinketId, trinketId, tip, DragPayload.TrinketTag,
            nodeName: $"HeroTrinketSquare{slot}");
        if (square is null)
        {
            GD.Print("[M4u·饰品] 🔴 方块模板 gear_hero_square.tscn 不可用 ⇒ 本格只显示 id（**不能拖出卸下**，如实上报）");
            hole.SetGlyph(trinketId, tip);
            return hole;
        }

        square.Pressed += () => UnequipTrinketFromUi(heroId, trinketId, "click");        // 点一下 = 卸下 ✓
        square.DraggedOut = () => UnequipTrinketFromUi(heroId, trinketId, "drag-out");   // 拖出孔外松手 = 卸下 ✓
        hole.SetItem(square, tip);
        return hole;
    }

    /// <summary>孔/方块模板缺失时的**纯读数框**（保留 `HeroTrinketSlot{n}` 节点名 ⇒ 冒烟锚点不因模板缺失而消失）✓</summary>
    private static Control FallbackHole(int slot, string text, string tooltip)
    {
        var hole = new PanelContainer
        {
            Name = $"HeroTrinketSlot{slot}",
            CustomMinimumSize = new Vector2(120, 44),
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = tooltip,
        };
        hole.AddChild(new Label
        {
            Name = $"TrinketSlotText{slot}",
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        return hole;
    }

    /// <summary>T4：被拒候选件 = **全不透明灰字** ＋ 整行理由（理由来自内核，UI 不自己判第二遍）✓</summary>
    private Control BuildRefusedCandidatePanel()
    {
        var bad = new PanelContainer
        {
            Name = "HeroTrinketCandidate",
            CustomMinimumSize = new Vector2(120, 44),
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = $"不可装备（内核原样理由）：{_trinketRefusedReason}\n" +
                "（理由生产点 = Roster.CanEquipTrinket —— 唯一判据落点；UI 不自己判第二遍）✓",
        };
        var badLabel = new Label
        {
            Name = "TrinketCandidateText",
            Text = $"✕ {_trinketRefusedTrinketId}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        badLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f, 1.0f));   // 🔴 置灰=灰字 α=1 ✓
        bad.AddChild(badLabel);
        return bad;
    }

    /// <summary>右栏文本区的饰品读数行（与格上真读数同源 ⇒ 不是第二份真值）✓</summary>
    private string DescribeTrinketSlots(Roster roster, string heroId)
    {
        IReadOnlyList<string> held = roster.TrinketsOf(heroId);
        string slots = string.Join("｜", Enumerable.Range(1, Roster.MaxTrinketSlots)
            .Select(i => i <= held.Count ? $"槽{i}「{held[i - 1]}」" : $"槽{i} 空"));
        string refusal = _trinketRefusedHeroId == heroId && _trinketRefusedReason is not null
            ? $"　🔴 不可装备：「{_trinketRefusedTrinketId}」—— {_trinketRefusedReason}"
            : string.Empty;
        return $"【饰品 {held.Count}/{Roster.MaxTrinketSlots}】{slots}{refusal}" +
               "（装卸已接线：方块拖进空孔 = 装上 · 拖出孔外松手 ／ 点方块 = 卸下 · buff 原语未接线 #307）";
    }

    /// <summary>悬停全文（Godot 内建 `TooltipText`）—— 逐字段真读，不写死；buff 原语未接线如实标注 ✓</summary>
    private string TrinketTooltip(string trinketId, int slot)
    {
        if (_trinketsCfg is null)
        {
            return $"饰品格 {slot}/{Roster.MaxTrinketSlots}：「{trinketId}」（🔴 饰品库未加载 ⇒ 字段不可读）";
        }

        TrinketConfig t = _trinketsCfg.Get(trinketId);   // 库里没有 ⇒ fail-fast（不静默）✓
        string classes = t.HeroClassRequirements.Count == 0
            ? "无（全职业可装）"
            : string.Join(" / ", t.HeroClassRequirements);
        return $"饰品格 {slot}/{Roster.MaxTrinketSlots}：{t.Id}\n" +
               $"稀有度 {t.Rarity}（award_category={_trinketsCfg.AwardCategoryOf(t.Rarity)}" +
               $" · 可购买={_trinketsCfg.IsPurchasable(t)}）\n" +
               $"价格 {t.Price}　limit {t.Limit}（库存概念未接线 ⇒ limit 本件不判，登记待策划）\n" +
               $"职业要求 {classes}\n" +
               $"origin_dungeon {t.OriginDungeon}　origin {t.Origin}\n" +
               $"buffs {t.Buffs.Count} 条：{(t.Buffs.Count == 0 ? "无" : string.Join("、", t.Buffs))}\n" +
               "（buff 原语层未接线 ⇒ 装上不改变属性 · #307 数值冻结）";
    }

    /// <summary>空槽悬停 —— 说清「怎么装」（红线 21：不留不可解释的空框）✓</summary>
    private static string EmptySlotTooltip(int slot)
        => $"饰品格 {slot}/{Roster.MaxTrinketSlots}：空\n" +
           "（把饰品方块拖进本孔 = 装上；方块来源（库存 ／ 商店）未接线 ⇒ 冒烟用 --hamlet-trinket-seed 播种，" +
           "落孔走 --hamlet-trinket-drop）✓";

    /// <summary>
    /// 🆕 **M4u · 饰品冒烟播种**（`--hamlet-trinket-seed=&lt;英雄&gt;:&lt;饰品&gt;[,…]`）——
    /// 走**公开 API** `Roster.EquipTrinket`（必写 `HeroTrinketEquippedEvent`），**不直接改私有字典**（红线 26）✓
    /// <para>⚠️ 库里没有的 id ⇒ `TrinketsConfig.Get` **fail-fast**（如实炸，不静默跳过）；
    /// 英雄不存在 ⇒ `Roster` 内 `HeroOf` 当场抛 ✓</para>
    /// <para>🔴 被拒（职业不符/重复/槽满）**不是失败**：记下**内核原样理由** ⇒ 详情页整行呈现（T4 的读数）✓</para>
    /// </summary>
    private void SeedTrinketsForSmoke(string spec)
    {
        Roster? roster = ExpeditionContext.Roster;
        string? lastHero = null;   // 🔴 「英雄只写一次」：`hero:a,b,c` 三件同属一个英雄（文档契约）✓
        if (roster is null || _trinketsCfg is null)
        {
            GD.Print($"[HamletRoot] 饰品播种：名册={roster is not null} 库={_trinketsCfg is not null} ⇒ 跳过（如实上报，不假装种上）");
            return;
        }

        foreach (string item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int sep = item.IndexOf(':');
            string heroId;
            string trinketId;
            if (sep > 0 && sep < item.Length - 1)
            {
                heroId = item[..sep];
                trinketId = item[(sep + 1)..];
                lastHero = heroId;
            }
            else if (sep < 0 && lastHero is not null)
            {
                heroId = lastHero;
                trinketId = item;   // 🔴 省略英雄 ⇒ 沿用前一件的（`<英雄>:a,b,c` = 同一英雄三件，T3 就靠这条）✓
            }
            else
            {
                GD.Print($"[HamletRoot] 饰品播种：`{item}` 既不是「英雄:饰品」也没有前序英雄 ⇒ 拒绝（不猜）✓");
                continue;
            }

            TrinketConfig t = _trinketsCfg.Get(trinketId);   // 不存在 ⇒ fail-fast ✓
            string? refused = roster.EquipTrinket(_log, _trinketsCfg, heroId, trinketId, "smoke-seed");
            if (refused is null)
            {
                if (_trinketRefusedHeroId == heroId)
                {
                    _trinketRefusedHeroId = null;   // 同一英雄后一次装上 ⇒ 旧候选读数作废（不留过期读数）✓
                    _trinketRefusedTrinketId = null;
                    _trinketRefusedReason = null;
                }

                GD.Print($"[HamletRoot] 饰品播种：{heroId} ⇒ 「{t.Id}」装上" +
                         $"（现持 {roster.TrinketsOf(heroId).Count}/{Roster.MaxTrinketSlots}）✓");
            }
            else
            {
                _trinketRefusedHeroId = heroId;
                _trinketRefusedTrinketId = trinketId;
                _trinketRefusedReason = refused;
                GD.Print($"[HamletRoot] 饰品播种：{heroId} ⇒ 「{t.Id}」**被拒**：{refused}" +
                         "（理由原样存下 ⇒ 详情页整行呈现，T4）✓");
            }
        }

        Refresh();   // 🔴 名册行立刻可见（不等到下一次刷新）✓
    }
}
