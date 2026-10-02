using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// 🆕 **M7u（`dd1_workstreams.md §3` · 策划 `#423`）· 驿站【今日新兵】列表**（2026-10-01）
///  —— 从 `HamletRoot.cs` 拆出（每文件 ≤600 行）✓
///
/// 四条玩家路径（全部**真读内核**：UI 只渲染 + 转发）：
///   ① 打开驿站（nav「驿站 Stage Coach」）⇒ 弹窗里出现【今日新兵 N 人 · 马车 LvX】＋ N 个条目；
///   ② 点条目 ⇒ 内核 `Roster.Recruit` **真进名册 / 真写 `HeroRecruitedEvent`**（UI 不自己算账）✓
///   ③ 点【🔄 刷新今日新兵】⇒ 重掷名单（确定性：专用 rng + 固定种子 ⇒ 同一条命令行同结果）✓
///   ④ 满员 ⇒ 条目**置灰 + 悬停给理由**（理由 = 内核 `Roster.CanRecruit` 的回答，UI 不自造 ✓ 红线 21）✓
///
/// 🔴 三条纪律：
///   · **零数值**：人数 ／ 高级概率 ／ 起始等级全部来自 `economy.json` 的马车三条曲线与 `StagecoachRecruits`（本文件不写数字）✓
///   · **上限单一来源 = 马车曲线**：`RecomputeRosterCap` 按 `RunProgress.CurrentRosterCap` 写 `Roster.CurrentCap`；
///     调用点 = 回城装配（`Build`）＋ 马车升级（`UpgradeBuilding`）⇒ 「改马车第 3 级，只有一处跟着变」✓
///   · **不新增第二份原型表**：候选原型 = `units.json` 派生（`UnitsConfig.PlayerArchetypes`）✓
///
/// ⚠️ 假设（报告如实标注）：
///   · 内核 `StagecoachRecruits.Roll` 只回答「高级与否」⇒ **职业由本层从数据池里抽**（DD 式：名单逐位给一个职业）；
///     抽取顺序固定 ⇒ 同 seed 同名单 ✓
///   · 「高级新兵带 Quirk」**已落地（M5u · 2026-10-01）**：掷签 = 内核纯函数 `StagecoachRecruits.RollQuirk`
///     （疾病不进池 · 与现持互斥的不进池）＋ `Roster.AddQuirk`（必写 `HeroQuirkGainedEvent`）✓
/// </summary>
public partial class HamletRoot : Control
{
    // ------------------------------------------------------------------
    // 今日新兵列表（**懒建**：只有真打开驿站弹窗才建 ⇒ 城池主屏零新增节点）✓
    // ------------------------------------------------------------------
    private VBoxContainer? _recruitRow;         // 复用行（`MountServiceRow` 认它；`Build` 不再预建）✓
    private Label? _recruitStatus;              // 标题行（今日新兵 N 人 · 马车 LvX · 名册 a/b）
    private VBoxContainer? _recruitOfferList;   // 条目容器（刷新时重建；条目数 = 曲线[马车等级]）✓
    private readonly List<Button> _recruitOfferButtons = new();
    private IReadOnlyList<StagecoachRecruits.Offering> _recruitOffers = Array.Empty<StagecoachRecruits.Offering>();
    private string[] _recruitOfferArchetypes = Array.Empty<string>();
    private int _recruitRolledLevel = -1;

    /// <summary>
    /// 🔴 今日新兵**专用**随机源：种子固定 ⇒ **同一条命令行 ⇒ 同一份名单**（可复现 ✓）；
    /// 与减压/疗养共用的 `_rng` **隔离** ⇒ 刷新名单**不扰动**别人的掷骰序列 ✓
    /// </summary>
    private Darkest.Core.Rng.RngProvider _recruitRng = new(20261001);   // ⚠️ 非 readonly：冒烟可用 `--hamlet-recruit-seed=N` 重播（同 seed 同名单 ⇒ 可复现）✓

    /// <summary>列表行是否已建且挂进树（供冒烟断言）。</summary>
    public bool RecruitRowMounted => _recruitRow is not null && _recruitRow.IsInsideTree();

    /// <summary>列表行是否真的可见（弹窗打开且未被隐藏）。</summary>
    public bool RecruitRowVisible => RecruitRowMounted && _recruitRow!.IsVisibleInTree();

    /// <summary>今日新兵条目数（供冒烟断言；与 `StagecoachRecruits.CountAt` 同源）✓</summary>
    public int RecruitOfferCount => _recruitOffers.Count;

    /// <summary>今日新兵里「高级」的条数（供冒烟断言）✓</summary>
    public int RecruitOfferUpgradedCount => _recruitOffers.Count(o => o.Upgraded);

    /// <summary>
    /// 候选原型池 = **数据派生**（`units.json` 里 `side == "player"` ⇒ `UnitsConfig.PlayerArchetypes`）——
    /// 🔴 不抄第二份原型表（P3 纪律：真值只有一处）✓
    /// </summary>
    private string[] RecruitArchetypePool()
        => _unitsCfg?.PlayerArchetypes.ToArray() ?? Array.Empty<string>();

    // ------------------------------------------------------------------
    // 接线：懒建列表 ／ 掷名单 ／ 重画（幂等）✓
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 **懒建今日新兵列表**（只在真打开驿站弹窗时建；建好挂进弹窗正文区）：
    ///   标题行（今日新兵 N 人 · 马车 LvX · 名册 a/b）＋ 刷新按钮 ＋ N 个条目（点条目 = 招募）✓
    /// ⚠️ 原型目录缺失 ⇒ **列表仍建，但不编条目**（如实上报，不静默造人）✓
    /// </summary>
    private void EnsureRecruitRow()
    {
        if (_recruitRow is not null || _buildingPopupBody is null)
        {
            return;
        }

        var row = new VBoxContainer { Name = "RecruitRow" };
        row.AddThemeConstantOverride("separation", 6);

        var head = new HBoxContainer { Name = "RecruitHead" };
        head.AddThemeConstantOverride("separation", 8);
        _recruitStatus = PopupLine("今日新兵：—");
        _recruitStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(_recruitStatus);

        var refresh = new Button
        {
            Name = "RecruitRefresh",
            Text = "🔄 刷新今日新兵",
            CustomMinimumSize = new Vector2(180, 32),
            TooltipText = "重新掷一份名单（同一条命令行 ⇒ 同结果；刷新 = 再掷一次）",
        };
        refresh.Pressed += () => RefreshRecruitOffers("刷新按钮");
        head.AddChild(refresh);
        row.AddChild(head);

        _recruitOfferList = new VBoxContainer { Name = "RecruitOfferList" };
        _recruitOfferList.AddThemeConstantOverride("separation", 4);
        row.AddChild(_recruitOfferList);

        _buildingPopupBody.AddChild(row);
        _recruitRow = row;
        RollRecruitOffers("首次打开驿站");
        RefreshRecruitRow();
        GD.Print($"[HamletRoot] 今日新兵列表已挂：条目 {_recruitOffers.Count} 个（马车 Lv{_recruitRolledLevel}）" +
                 $"　原型池 {RecruitArchetypePool().Length} 个（units.json 派生）✓");
    }

    /// <summary>
    /// 🔴 **掷今日新兵**：数量 / 高级概率 = `StagecoachRecruits.Roll`（读马车三条曲线；每次掷骰写
    /// `StagecoachRecruitRolledEvent` ⇒ 可复现可审计）＋ 逐位从**数据池**抽职业（顺序固定 ⇒ 同 seed 同名单）✓
    /// </summary>
    private void RollRecruitOffers(string reason)
    {
        StagecoachConfig coach = _cfg.Coach;
        _recruitRolledLevel = ExpeditionContext.Heirlooms?.LevelOf("stagecoach") ?? 0;
        _recruitOffers = StagecoachRecruits.Roll(coach, _recruitRolledLevel, _recruitRng, _log);

        string[] pool = RecruitArchetypePool();
        var archetypes = new string[_recruitOffers.Count];
        for (int i = 0; i < archetypes.Length; i++)
        {
            archetypes[i] = pool.Length > 0 ? pool[_recruitRng.NextInt(0, pool.Length)] : "?";
        }

        _recruitOfferArchetypes = archetypes;
        GD.Print($"[HamletRoot] 今日新兵（{reason}）：马车 Lv{_recruitRolledLevel} ⇒ {_recruitOffers.Count} 人" +
                 $"（高级 {RecruitOfferUpgradedCount} 人 · 概率 {StagecoachRecruits.UpgradedChancePctAt(coach, _recruitRolledLevel):0.##}%）" +
                 $"　原型池 {pool.Length} 个　掷骰已写 `StagecoachRecruitRolledEvent` ✓");
    }

    /// <summary>
    /// 🔴 重画今日新兵列表（**真读**名册 / 马车等级 / 曲线；可用性由内核 `Roster.CanRecruit` 回答）✓
    /// ⚠️ 行未建 ／ 弹窗不是驿站 ⇒ 空操作或隐藏（幂等，随便调）✓
    /// </summary>
    private void RefreshRecruitRow()
    {
        if (_recruitRow is null || _recruitOfferList is null)
        {
            return;
        }

        if (_buildingPopupId != "stagecoach")
        {
            _recruitRow.Visible = false;   // 只服务驿站；别的建筑不显示（不静默显示别人的名单）✓
            return;
        }

        _recruitRow.Visible = true;
        Roster? roster = ExpeditionContext.Roster;
        bool canRecruit = roster?.CanRecruit ?? false;

        // 🔴 **马车等级变了 ⇒ 名单随之重掷**（人数/概率 = 曲线[新等级]）：
        //    不补这一条，玩家升完马车看到的仍是**旧等级的名单**（数量与曲线对不上）⇒
        //    「人数只有一处跟着变」在界面上就不成立（升了马车却还是 2 人）✓
        int levelNow = ExpeditionContext.Heirlooms?.LevelOf("stagecoach") ?? 0;
        if (levelNow != _recruitRolledLevel)
        {
            RollRecruitOffers($"马车 Lv{_recruitRolledLevel} ⇒ Lv{levelNow}（等级变 ⇒ 名单重掷）");
        }

        // 条目重建：先 RemoveChild 再 QueueFree（同帧不留旧条目 ⇒ 判据不误报重叠）✓
        foreach (Node child in _recruitOfferList.GetChildren().ToArray())
        {
            _recruitOfferList.RemoveChild(child);
            child.QueueFree();
        }

        _recruitOfferButtons.Clear();
        for (int i = 0; i < _recruitOffers.Count; i++)
        {
            StagecoachRecruits.Offering offering = _recruitOffers[i];
            string archetype = i < _recruitOfferArchetypes.Length ? _recruitOfferArchetypes[i] : "?";
            int level = RecruitLevelFor(offering);
            string kind = offering.Upgraded ? "高级新兵" : "新兵";
            var btn = new Button
            {
                Name = $"RecruitOffer_{i}",
                Text = $"{kind}·{archetype}　Lv{level}（点它招募）",
                CustomMinimumSize = new Vector2(0, 32),
                Disabled = !canRecruit,
                TooltipText = canRecruit
                    ? $"招募 {kind}·{archetype}（Lv{level} 士气{_cfg.Coach.RookieMorale} 免费）"
                    : $"名册已满（{roster!.Heroes.Count}/{EffectiveCapNow(roster)}）⇒ 先出征或升马车抬高上限" +
                      "（理由来自内核 `Roster.CanRecruit`）",
            };
            int index = i;
            btn.Pressed += () => RecruitOfferAt(index);
            _recruitOfferList.AddChild(btn);
            _recruitOfferButtons.Add(btn);
        }

        if (_recruitStatus is not null)
        {
            _recruitStatus.Text = roster is null
                ? "今日新兵：名册未加载"
                : $"今日新兵 {_recruitOffers.Count} 人（高级 {RecruitOfferUpgradedCount} 人 · 概率 " +
                  $"{StagecoachRecruits.UpgradedChancePctAt(_cfg.Coach, _recruitRolledLevel):0.##}%）" +
                  $"　马车 Lv{_recruitRolledLevel}　名册 {roster.Heroes.Count} / {EffectiveCapNow(roster)}" +
                  (roster.CanRecruit ? "　⇒ 点条目即可招募（免费）" : "　⛔ 名册已满");
        }
    }

    /// <summary>
    /// 条目的**起始等级**：内核 `StagecoachRecruits.StartingLevel`（基础 ＋ 高级 +1）＋ 马车升级的起点提升
    /// （`HeirloomStock.EffectiveRookieLevel`）—— 🔴 「+1」这条规则只住内核，UI 不抄 ✓
    /// </summary>
    private int RecruitLevelFor(StagecoachRecruits.Offering offering)
    {
        int baseLevel = ExpeditionContext.Heirlooms?.EffectiveRookieLevel(_cfg.Coach.RookieLevel) ?? _cfg.Coach.RookieLevel;
        return StagecoachRecruits.StartingLevel(_cfg.Coach, offering) + (baseLevel - _cfg.Coach.RookieLevel);
    }
}
