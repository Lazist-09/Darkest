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

    // ------------------------------------------------------------------
    // 业务入口（唯一一条：点条目 ／ 调试直招 都走它）✓
    // ------------------------------------------------------------------

    /// <summary>🔴 **点条目 ⇒ 招募**：职业 / 等级 / 是否高级都取自**当前名单**；越界 ⇒ 如实拒绝（不静默换人）✓</summary>
    public void RecruitOfferAt(int index)
    {
        if (index < 0 || index >= _recruitOffers.Count)
        {
            GD.Print($"[HamletRoot] 招募条目 #{index}：今日新兵只有 {_recruitOffers.Count} 个 ⇒ **如实拒绝**（不静默）✓");
            return;
        }

        string archetype = index < _recruitOfferArchetypes.Length ? _recruitOfferArchetypes[index] : "?";
        StagecoachRecruits.Offering offering = _recruitOffers[index];
        RecruitOne(archetype, RecruitLevelFor(offering), $"·条目{index}", offering.Upgraded);
    }

    /// <summary>
    /// 🔴 **招募一个人**（唯一业务入口；UI 只转发）：转发内核 `Roster.Recruit`
    /// （进名册 ／ 写 `HeroRecruitedEvent` 全在内核）✓
    /// 🔴 满员的理由由内核 `Roster.CanRecruit` 回答（红线 21 (b)）—— 本方法不自造理由 ✓
    /// 🆕 M5u：`upgraded = true`（高级新兵）⇒ 招到后按内核纯函数**掷一条怪癖**（见 `GrantRecruitQuirk`）✓
    /// </summary>
    public void RecruitOne(string archetype, int? rookieLevel, string tag, bool upgraded = false)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            GD.Print($"[HamletRoot] 招募{tag}：名册未加载 ⇒ **如实拒绝**（不静默）✓");
            return;
        }

        int before = roster.Heroes.Count;
        HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{before + 1}", rookieLevel);
        if (rookie is not null && upgraded)
        {
            GrantRecruitQuirk(roster, rookie.Id);
        }

        GD.Print(rookie is null
            ? $"[HamletRoot] 招募{tag}：**名册已满**（{before}/{EffectiveCapNow(roster)}）—— 拒绝（不悄悄顶替）✓"
            : $"[HamletRoot] 招募{tag}：{rookie.Name}（{rookie.Archetype} Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{EffectiveCapNow(roster)}　事件原文 {LastRecruitEventText()} ✓");
        Refresh();
        AutoSave("招募");
    }

    /// <summary>
    /// 🔴 **名册可用上限重算**（**单一来源 = 马车曲线**）：按 `RunProgress.CurrentRosterCap` 写 `Roster.CurrentCap`；
    /// 调用点 = 回城装配（`Build`）＋ 马车升级（`UpgradeBuilding`）✓
    /// ⚠️ 名册 ／ 解锁表未加载 ⇒ **如实拒绝**（不静默写一个数）✓
    /// </summary>
    public void RecomputeRosterCap(string reason)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null || _unlockCfg is null)
        {
            GD.Print($"[HamletRoot] 名册可用上限重算（{reason}）：{(roster is null ? "名册" : "解锁表")}未加载 ⇒ 如实拒绝（不静默写数）✓");
            return;
        }

        int before = roster.CurrentCap;
        int level = ExpeditionContext.Heirlooms?.LevelOf("stagecoach") ?? 0;
        IReadOnlyList<int> curve = ExpeditionContext.StagecoachCapCurve ?? Array.Empty<int>();
        roster.CurrentCap = ExpeditionContext.Progress.CurrentRosterCap(
            _unlockCfg,
            roster.Cap,
            stagecoachCapByLevel: ExpeditionContext.StagecoachCapCurve,
            stagecoachLevel: level);
        GD.Print($"[HamletRoot] 名册可用上限重算（{reason}）：{before} ⇒ {roster.CurrentCap}" +
                 $"（马车 Lv{level} · 曲线 {string.Join("/", curve)} · 硬上限 {roster.Cap}）✓");
    }

    /// <summary>可用上限的一行读法（**真值 = 内核**；曲线未重算时如实标注，不假装有值）✓</summary>
    private static string EffectiveCapNow(Roster? r)
    {
        if (r is null)
        {
            return "—";
        }

        return r.CurrentCap > 0
            ? $"{Math.Min(r.CurrentCap, r.Cap)}（硬上限 {r.Cap}）"
            : $"{r.Cap}（硬上限 {r.Cap}；⚠ 曲线未重算 ⇒ 暂用硬上限）";
    }

    /// <summary>
    /// 🔴 驿站弹窗的**功能行文案**（`RefreshBuildingPopup` 与 `ShowBuildingInfo` **共用同一份** ⇒ 不抄第二份口径）✓
    /// ⚠️ 数字全部**从数据现算**（马车等级 ／ 曲线人数 ／ 高级概率）—— 本方法不写死任何常量（红线 21）✓
    /// </summary>
    private string RecruitFunctionLine()
    {
        int level = ExpeditionContext.Heirlooms?.LevelOf("stagecoach") ?? 0;
        return $"招募新兵（今日新兵列表：马车 Lv{level} ⇒ {StagecoachRecruits.CountAt(_cfg.Coach, level)} 人 · " +
               $"高级概率 {StagecoachRecruits.UpgradedChancePctAt(_cfg.Coach, level):0.##}% · 起始等级 " +
               $"{StagecoachRecruits.StartingLevel(_cfg.Coach, new StagecoachRecruits.Offering(false))} ⇒ 点条目即招）";
    }

    /// <summary>🔴 **刷新今日新兵**（刷新按钮的真实入口）：重掷名单 + 重画（幂等）✓</summary>
    public void RefreshRecruitOffers(string reason)
    {
        RollRecruitOffers(reason);
        RefreshRecruitRow();
        GD.Print($"[HamletRoot] 今日新兵已刷新（{reason}）：{_recruitOffers.Count} 人（高级 {RecruitOfferUpgradedCount} 人）" +
                 $"　原型 {string.Join("、", _recruitOfferArchetypes)} ✓");
    }

    /// <summary>回读最近一条招募事件原文（`HeroRecruitedEvent`；没有 ⇒ "（无）"）✓</summary>
    private string LastRecruitEventText()
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            if (_log.Events[i] is Darkest.Core.Events.HeroRecruitedEvent e)
            {
                return $"hero_recruited: {e.HeroId} {e.Name}（{e.Archetype} Lv{e.Level} 士气{e.Morale} 花费{e.Cost}）";
            }
        }

        return "（无）";
    }

    /// <summary>
    /// 🆕 **M5u · M7③：高级新兵带 Quirk** —— 掷签走内核纯函数 `StagecoachRecruits.RollQuirk`
    /// （疾病不进池 · 与现持互斥的不进池 ⇒ 候选池口径**只有一处**），落状态走 `Roster.AddQuirk`（必写事件）✓
    /// ⚠️ 库未加载 ／ 池为空 ⇒ **如实打印「本次不发」**（不编一条怪癖 —— 红线 21：不留不可解释状态）✓
    /// </summary>
    private void GrantRecruitQuirk(Roster roster, string heroId)
    {
        if (_quirksCfg is null)
        {
            GD.Print($"[HamletRoot] 高级新兵 {heroId}：怪癖库未加载 ⇒ **本次不发怪癖**（如实上报，不编）✓");
            return;
        }

        string? quirkId = StagecoachRecruits.RollQuirk(_quirksCfg, roster.QuirksOf(heroId), _recruitRng);
        if (quirkId is null)
        {
            GD.Print($"[HamletRoot] 高级新兵 {heroId}：候选池为空（库 {_quirksCfg.Quirks.Count} 条全部被过滤）" +
                     "⇒ **本次不发怪癖** ✓");
            return;
        }

        bool added = roster.AddQuirk(_log, heroId, quirkId, "stagecoach_upgraded");
        GD.Print($"[HamletRoot] 高级新兵 {heroId} ⇒ 掷得怪癖 `{quirkId}`（{QuirkKindLabel(_quirksCfg.Get(quirkId))}" +
                 $" · 新增={added}）　事件原文 {LastQuirkEventText()} ✓");
    }

    /// <summary>回读最近一条怪癖事件原文（`HeroQuirkGainedEvent`；没有 ⇒ "（无）"）✓</summary>
    private string LastQuirkEventText()
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            if (_log.Events[i] is Darkest.Core.Events.HeroQuirkGainedEvent e)
            {
                return $"hero_quirk_gained: {e.HeroId} {e.QuirkId}（{e.Reason}）";
            }
        }

        return "（无）";
    }

    /// <summary>
    /// 🆕 **M5u · 怪癖冒烟播种**（`--hamlet-quirk-seed=&lt;英雄&gt;:&lt;怪癖&gt;`，逗号可多条）——
    /// 走**公开 API** `Roster.AddQuirk`（必写 `HeroQuirkGainedEvent`），**不直接改私有字典**（红线 26）✓
    /// ⚠️ 库里没有的 id ⇒ `QuirksConfig.Get` **fail-fast**（如实炸，不静默跳过）；
    /// 英雄不存在 ⇒ `AddQuirk` 内 `MoraleOf` 当场抛 ✓（数值本身**一个都不改** —— `#307` 冻结）✓
    /// </summary>
    private void SeedQuirksForSmoke(string spec)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null || _quirksCfg is null)
        {
            GD.Print($"[HamletRoot] 怪癖播种：名册={roster is not null} 库={_quirksCfg is not null} ⇒ 跳过（如实上报，不假装种上）");
            return;
        }

        foreach (string item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int sep = item.IndexOf(':');
            if (sep <= 0 || sep == item.Length - 1)
            {
                GD.Print($"[HamletRoot] 怪癖播种：`{item}` 不是「英雄:怪癖」⇒ 拒绝（不猜）✓");
                continue;
            }

            string heroId = item[..sep];
            string quirkId = item[(sep + 1)..];
            QuirkConfig q = _quirksCfg.Get(quirkId);   // 不存在 ⇒ fail-fast ✓
            bool ok = roster.AddQuirk(_log, heroId, quirkId, "smoke-seed");
            GD.Print($"[HamletRoot] 怪癖播种：{heroId} ⇒ {quirkId}（{QuirkKindLabel(q)} · 新增={ok}" +
                     $" · 现持 {roster.QuirksOf(heroId).Count} 条）✓");
        }

        Refresh();   // 🔴 名册行重建 ⇒ 播种立刻可见（不等到下一次刷新）✓
    }

    // ------------------------------------------------------------------
    // 冒烟族（全部走真实入口；⚠️ 必须挂在 `HandleGearSmokeFlags` **之前**）✓
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 招募冒烟族（**全部走真实入口**）：
    ///   ① `--hamlet-recruit-list`        ⇒ 打开驿站弹窗并打印名单读数（条目数 ／ 高级数 ／ 名册 a/b）
    ///   ② `--hamlet-recruit-refresh`     ⇒ 再掷一次并打印**前后两份**名单（证明刷新真的换了人）
    ///   ③ `--hamlet-recruit-press=<idx>` ⇒ 真实 `EmitSignal(Pressed)` 招第 idx 个（红线 26）✓
    /// ⚠️ 位置：`HandleGearSmokeFlags` 之前 —— 后者可能把弹窗切到铁匠铺（那会把名单行隐藏）✓
    /// </summary>
    private void HandleRecruitSmokeFlags(string[] args)
    {
        // 🆕 2026-10-01 M5u：`--hamlet-recruit-seed=N` ⇒ 重播招募掷骰（**必须先于** `OpenBuildingPopup` —— 名单在开窗时掷）
        //    🔴 为什么需要：**高级新兵带怪癖**（M7③）只在 `Upgraded` 条目上发生，而固定种子 `20261001`
        //    在马车 Lv0 实测掷不出高级条目 ⇒ 不重播就**验不到那条分支**（只能「假定它对」）✓
        if (FindSmokeArg(args, "--hamlet-recruit-seed=") is { } rSeedArg && int.TryParse(rSeedArg, out int rSeed))
        {
            _recruitRng = new Darkest.Core.Rng.RngProvider(rSeed);
            GD.Print($"[HamletRoot] 招募冒烟：掷骰种子 ⇒ {rSeed}（同 seed 同名单 · 供复验高级分支）✓");
        }

        bool wantList = Array.Exists(args, a => a == "--hamlet-recruit-list" || a == "--hamlet-recruit-refresh")
            || FindSmokeArg(args, "--hamlet-recruit-press=") is not null;
        if (!wantList)
        {
            return;
        }

        OpenBuildingPopup("stagecoach");
        GD.Print($"[HamletRoot] 招募冒烟：RecruitRowMounted={RecruitRowMounted}　RecruitRowVisible={RecruitRowVisible}" +
                 $"　今日新兵 {RecruitOfferCount} 人（高级 {RecruitOfferUpgradedCount} 人）" +
                 $"　名册 {ExpeditionContext.Roster?.Heroes.Count ?? -1} / {EffectiveCapNow(ExpeditionContext.Roster)}" +
                 $"　列表条目节点 {_recruitOfferButtons.Count} 个 ✓");

        if (Array.Exists(args, a => a == "--hamlet-recruit-refresh"))
        {
            string before = DescribeRecruitOffers();
            RefreshRecruitOffers("冒烟·刷新");
            GD.Print($"[HamletRoot] 招募冒烟·刷新：前 [{before}] ⇒ 后 [{DescribeRecruitOffers()}]" +
                     $"　条目 {_recruitOfferButtons.Count} 个（应 = 曲线值）✓");
        }

        // ⚠️ 支持**逗号分隔的多个下标**（`--hamlet-recruit-press=0,1`）—— 只为一条验收：
        //    先招到满员、再按下一个条目 ⇒ 必须走「置灰 + 原样显示理由」分支（红线 21 (b)）✓
        if (FindSmokeArg(args, "--hamlet-recruit-press=") is { } pressArg)
        {
            foreach (string token in pressArg.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(token, out int idx)) { continue; }
                PressRecruitOffer(idx);
                GD.Print($"[HamletRoot] 招募冒烟·按下 #{idx}：名册 {ExpeditionContext.Roster?.Heroes.Count ?? -1} 人" +
                         $" / {EffectiveCapNow(ExpeditionContext.Roster)}　事件原文 {LastRecruitEventText()} ✓");
            }
        }
    }

    /// <summary>名单一行读法（冒烟对照用：职业 + 高级标记）✓</summary>
    private string DescribeRecruitOffers()
        => string.Join("、", _recruitOfferArchetypes.Select((a, i) =>
            $"{a}{(_recruitOffers[i].Upgraded ? "+" : "")}"));

    /// <summary>
    /// 🔴 红线 26：**功能级验收走玩家路径** —— 本方法发出**真实 `Pressed` 信号**（不直接调业务方法）✓
    /// ⚠️ 条目置灰 ⇒ **不改名册**：打印"UI 原样显示的理由"后返回 ✓
    /// </summary>
    public void PressRecruitOffer(int index)
    {
        if (index < 0 || index >= _recruitOfferButtons.Count)
        {
            GD.Print($"[HamletRoot] PressRecruitOffer(#{index})：找不到条目（列表 {_recruitOfferButtons.Count} 个；红线 21：按钮没挂上）");
            return;
        }

        Button btn = _recruitOfferButtons[index];
        if (btn.Disabled)
        {
            GD.Print($"[HamletRoot] PressRecruitOffer(#{index})：条目**置灰** ⇒ 不改名册；" +
                     $"UI 原样显示的理由 =「{btn.TooltipText}」（红线 21）✓");
            return;
        }

        GD.Print($"[HamletRoot] PressRecruitOffer(#{index})：发出真实 Pressed（条目「{btn.Text}」）⇒ 招募");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
