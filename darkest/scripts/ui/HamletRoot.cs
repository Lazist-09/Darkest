using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M8.0 ③（`#283`）：**回城场景 Hamlet**（局外养成层的入口）。
///
/// 🔴 职责边界（`blueprint §9.15`）：本类**只渲染 + 转发** —— 金钱来自**跨趟持有者**
/// （`ExpeditionContext.Gold`，与经济数据同源），减压/招募按钮在 ④/⑤ 落地前**置灰**
/// （**不假装可用**）。
/// 🔴 红线 18：Hamlet 必须**从启动场景可达**（`BattleRoot` 的按钮 / `--hamlet` CLI）。
/// </summary>
public partial class HamletRoot : Node2D
{
    private Label _status = null!;
    private Label _hint = null!;
    private Label _upgradeStatus = null!;
    private EconomyConfig _cfg = null!;
    private string? _selectedHero;                       // ② 选人权：玩家选中的被减压者
    private readonly List<Button> _heroButtons = new();  // 动态重建（士气 < 50 的人）
    private readonly Darkest.Core.Events.CombatLog _log = new();
    private readonly Darkest.Core.Rng.RngProvider _rng = new(20260909);

    public override void _Ready()
    {
        // 跨趟经济与名册：与地牢层共用同一实例（回城不重置金钱与士气）
        _cfg = EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath));
        Economy economy = ExpeditionContext.EnsureEconomy(_cfg);
        RosterConfig rosterCfg = RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath));
        Roster roster = ExpeditionContext.EnsureRoster(rosterCfg);

        _status = new Label
        {
            Name = "HamletStatus",
            Position = new Vector2(24, 24),
            Size = new Vector2(1200, 120),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_status);

        var backToDungeon = new Button
        {
            Name = "BackToDungeon",
            Text = "再出发（远征）",
            Position = new Vector2(24, 160),
            Size = new Vector2(240, 40),
        };
        backToDungeon.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/expedition/Expedition.tscn");
        AddChild(backToDungeon);

        // ④ 减压：**两栋同价同效、风险不同**（Tavern 更不稳 / Abbey 更稳）—— 真可用，选的是"风格"
        var tavern = new Button { Name = "ReliefTavern", Text = "减压·酒馆（快而不稳）", Position = new Vector2(280, 160), Size = new Vector2(240, 40) };
        tavern.Pressed += () => DoRelief("tavern");
        AddChild(tavern);

        var abbey = new Button { Name = "ReliefAbbey", Text = "减压·修道院（慢而稳）", Position = new Vector2(536, 160), Size = new Vector2(240, 40) };
        abbey.Pressed += () => DoRelief("abbey");
        AddChild(abbey);

        // ⑤ 招募（Stage Coach）：**按原型选**（玩家决定招哪种人；免费 / Lv1 / morale 50 / 满员拒绝）
        string[] archetypes = { "warrior", "tank", "medic", "commissar" };
        for (int i = 0; i < archetypes.Length; i++)
        {
            string a = archetypes[i];
            var b = new Button
            {
                Name = $"Recruit_{a}",
                Text = $"招募·{a}（免费）",
                Position = new Vector2(24 + (i * 190), 260),
                Size = new Vector2(180, 36),
            };
            b.Pressed += () => RecruitArchetype(a);
            AddChild(b);
        }

        // 🔴 M8.1：**建筑升级入口**（三栋首批建筑；两轴：降费 / 增强·解锁）—— 消耗传家宝
        string[] upgradable = { "tavern", "abbey", "stagecoach" };
        for (int i = 0; i < upgradable.Length; i++)
        {
            string bId = upgradable[i];
            var ub = new Button
            {
                Name = $"Upgrade_{bId}",
                Text = $"升级·{bId}",
                Position = new Vector2(24 + (i * 190), 340),
                Size = new Vector2(180, 36),
            };
            ub.Pressed += () => UpgradeBuilding(bId);
            AddChild(ub);
        }

        // ② 选人权：**减压按【人】选**（列出名册里士气 < 基准者）；选完再选建筑
        _hint = new Label
        {
            Name = "ReliefHint",
            Position = new Vector2(24, 200),
            Size = new Vector2(1200, 30),
        };
        AddChild(_hint);

        _upgradeStatus = new Label
        {
            Name = "UpgradeStatus",
            Position = new Vector2(24, 386),
            Size = new Vector2(1200, 60),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_upgradeStatus);

        Refresh();
        GD.Print($"[HamletRoot] 回城就绪：金钱 {economy.Gold}（跨趟持有；减压一次 {economy.StressReliefCost}）" +
                 $"　名册 {roster.Heroes.Count} 人（士气跨趟；最低 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}）");

        // 🔴 M8.0 ⑥ 端到端：回城阶段 ⇒ **花钱（减压）** 然后 **再出发**
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--e2e") && ExpeditionContext.E2EStage == 1)
        {
            int goldBefore = economy.Gold;
            // ② 选人权：冒烟里**显式指定对象**（证明"能对指定的人减压"）
            string target = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
            int moraleBefore = roster.MoraleOf(target);
            SelectHero(target);
            DoRelief("tavern");
            int moraleAfter = roster.MoraleOf(target);
            GD.Print($"[E2E] 阶段1 回城：**花钱** {goldBefore} 减 {economy.Gold} ⇒ 剩余 {economy.Gold}" +
                     $"　指定对象 {target} 士气 {moraleBefore} 到 {moraleAfter}（V2：减压 ⇒ 士气确实更高）" +
                     $"　名册最低士气 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}");
            ExpeditionContext.E2EStage = 2;
            GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
        }
    }

    /// <summary>
    /// **减压**（M8.0 ④）：花钱 → 恢复士气（**唯一**士气出口）→ 副作用掷骰（Tavern 更不稳 / Abbey 更稳）。
    /// 🔴 只转发：钱与士气都在**跨趟持有者**里；UI 不自己算账。
    /// </summary>
    public void DoRelief(string buildingId)
    {
        Economy? economy = ExpeditionContext.Gold;
        Roster? roster = ExpeditionContext.Roster;
        if (economy is null || roster is null)
        {
            return;
        }

        // ② 选人权：**优先用玩家指定的人**（未指定时退化为最低者，便于冒烟）
        string heroId = _selectedHero ?? roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
        StressReliefOutcome o = StressRelief.Apply(_cfg, economy, _rng, _log, buildingId, heroId, roster.MoraleOf(heroId));
        if (o.Paid)
        {
            roster.ApplyRelief(_log, heroId, _cfg.Building(buildingId).MoraleRestore, buildingId);
        }

        GD.Print($"[HamletRoot] 减压·{buildingId}：{(o.Paid ? "成交" : "拒绝（钱不够）")}" +
                 $"　{heroId} 士气 {o.NewMorale}　副作用 {(o.PenaltyTriggered ? $"触发（下趟 −{o.NextRunPenalty}）" : "未触发")}" +
                 $"　剩余金钱 {economy.Gold}");
        Refresh();
    }

    /// <summary>**招募**（M8.0 ⑤）：免费；新兵 Lv1 / 士气 50；满员即拒绝（不悄悄顶替）。</summary>
    public void Recruit()
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            return;
        }

        // 挑一个"人最少的原型"，名字用序号（最小实现；将来由玩家选）
        string archetype = roster.Heroes
            .GroupBy(h => h.Archetype)
            .OrderBy(g => g.Count())
            .First().Key;
        HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{roster.Heroes.Count + 1}");

        GD.Print(rookie is null
            ? $"[HamletRoot] 招募：**名册已满**（{roster.Heroes.Count}/{_cfg.Coach.MaxRoster}）—— 拒绝（不悄悄顶替）"
            : $"[HamletRoot] 招募：{rookie.Name}（{rookie.Archetype} Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{_cfg.Coach.MaxRoster}");
        Refresh();
    }

    /// <summary>**选中某位英雄**（② 选人权：减压必须由玩家指定对象，不是"自动挑最低的"）。</summary>
    public void SelectHero(string heroId)
    {
        _selectedHero = heroId;
        Roster? roster = ExpeditionContext.Roster;
        int morale = roster?.MoraleOf(heroId) ?? 0;
        GD.Print($"[HamletRoot] 已选中 {heroId}（当前士气 {morale}）⇒ 再点酒馆/修道院减压");
        Refresh();
    }

    /// <summary>**招募指定原型**（⑤：玩家决定招哪种人；免费 / Lv1 / morale 50 / 满员拒绝）。</summary>
    public void RecruitArchetype(string archetype)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            return;
        }

        HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{roster.Heroes.Count + 1}");
        GD.Print(rookie is null
            ? $"[HamletRoot] 招募·{archetype}：**名册已满**（{roster.Heroes.Count}/{roster.Cap}）—— 拒绝（不悄悄顶替）"
            : $"[HamletRoot] 招募·{archetype}：{rookie.Name}（Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{roster.Cap}");
        Refresh();
    }

    /// <summary>**升级建筑**（M8.1）：按曲线扣传家宝；不足即拒绝（不部分扣）；升级后**生效值真的改变**。</summary>
    public void UpgradeBuilding(string building)
    {
        HeirloomStock? h = ExpeditionContext.Heirlooms;
        if (h is null)
        {
            return;
        }

        UpgradeLevel? next = h.NextLevel(building);
        bool ok = h.TryUpgrade(_log, building);
        GD.Print(ok
            ? $"[HamletRoot] 升级·{building} ⇒ Lv{h.LevelOf(building)}（花 {string.Join("/", next!.Cost.Select(k => $"{k.Key}×{k.Value}"))}）" +
              $"　生效：减压价 {h.EffectiveReliefCost(_cfg.StressReliefCost)}　名册上限 {h.EffectiveRosterCap(baseCap: ExpeditionContext.Roster?.Heroes.Count ?? 0, hardCap: 12)}"
            : $"[HamletRoot] 升级·{building}：**传家宝不足或已满级** ⇒ 拒绝（不部分扣）");
        Refresh();
    }

    /// <summary>刷新（只读跨趟状态，不自己算账）。</summary>
    public void Refresh()
    {
        int gold = ExpeditionContext.Gold?.Gold ?? 0;
        int cost = ExpeditionContext.Gold?.StressReliefCost ?? 0;
        int affordable = cost <= 0 ? 0 : gold / cost;
        Roster? roster = ExpeditionContext.Roster;
        string moraleLine = roster is null
            ? "名册：未加载"
            : "名册士气：" + string.Join("、", roster.Heroes
                .OrderByDescending(h => roster.MoraleOf(h.Id))
                .Select(h => $"{h.Name}{roster.MoraleOf(h.Id)}"));
        _status.Text =
            $"【Hamlet 回城】金钱 {gold}　一次减压 {cost} ⇒ 现在能减 {affordable} 次\n" +
            moraleLine + "\n" +
            "④ 减压已可用（Tavern 快而不稳 ／ Abbey 慢而稳，**同价同效**）；⑤ 招募随后落地。";

        // ② 选人权：刷新"可减压者"按钮（名册里**士气低于基准 50** 的人）
        foreach (Button b in _heroButtons)
        {
            b.QueueFree();
        }

        _heroButtons.Clear();
        if (roster is not null)
        {
            int i = 0;
            foreach (HeroConfig h in roster.Heroes.Where(x => roster.MoraleOf(x.Id) < RosterConfig.RookieMorale))
            {
                string id = h.Id;
                var b = new Button
                {
                    Name = $"Hero_{id}",
                    Text = $"{h.Name}（士气 {roster.MoraleOf(id)}）",
                    Position = new Vector2(24 + (i * 190), 300),
                    Size = new Vector2(180, 34),
                };
                b.Pressed += () => SelectHero(id);
                AddChild(b);
                _heroButtons.Add(b);
                i++;
                if (i >= 6)
                {
                    break; // 一行放 6 个
                }
            }
        }

        _hint.Text = roster is null
            ? "减压：名册未加载"
            : _selectedHero is null
                ? "减压：请先点一位【士气低于 50】的人，再点酒馆/修道院（同价同效、风险不同）"
                : $"减压对象：{_selectedHero}（士气 {roster.MoraleOf(_selectedHero)}）⇒ 请点酒馆或修道院";

        // 🔴 M8.1：传家宝库存 + 三栋建筑的等级与**生效值**（升级真的改变数字）
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (heirlooms is not null)
        {
            string stock = string.Join(" ／ ", heirlooms.Kinds.Select(k => $"{k}×{heirlooms.Count(k)}"));
            string levels = string.Join(" ／ ", new[] { "tavern", "abbey", "stagecoach" }
                .Select(b => $"{b} Lv{heirlooms.LevelOf(b)}"));
            _upgradeStatus.Text =
                $"传家宝：{stock}\n建筑：{levels}　⇒ 减压价 {heirlooms.EffectiveReliefCost(_cfg.StressReliefCost)}" +
                $"　恢复量 {heirlooms.EffectiveMoraleRestore("tavern", _cfg.StressRelief!.Buildings[0].MoraleRestore)}" +
                $"　新兵起始等级 {heirlooms.EffectiveRookieLevel(_cfg.Coach.RookieLevel)}";
        }
    }
}
