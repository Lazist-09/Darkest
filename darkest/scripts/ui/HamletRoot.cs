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
    private Label _saniStatus = null!;
    private SanitariumConfig? _saniCfg;
    private readonly Dictionary<string, Button> _saniButtons = new(); // M8.2：三项服务按钮（用于置灰）
    private EconomyConfig _cfg = null!;
    private string? _selectedHero;                       // ② 选人权：玩家选中的被减压者
    private readonly List<Button> _heroButtons = new();  // 动态重建（士气 < 50 的人）
    private readonly Dictionary<string, Button> _upgradeButtons = new(); // M8.1：三栋升级按钮（用于置灰）
    private readonly Darkest.Core.Events.CombatLog _log = new();
    private readonly Darkest.Core.Rng.RngProvider _rng = new(20260909);

    public override void _Ready()
    {
        // 跨趟经济与名册：与地牢层共用同一实例（回城不重置金钱与士气）
        _cfg = EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath));
        _saniCfg = SanitariumConfig.Parse(FileAccess.GetFileAsString(SanitariumConfig.ResPath));
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
            _upgradeButtons[bId] = ub;
        }

        // ② 选人权：**减压按【人】选**（列出名册里士气 < 基准者）；选完再选建筑
        _hint = new Label
        {
            Name = "ReliefHint",
            Position = new Vector2(24, 200),
            Size = new Vector2(1200, 30),
        };
        AddChild(_hint);

        // 🔴 M8.1：升级状态区（传家宝库存 / 各级等级 / 生效值）
        _upgradeStatus = new Label
        {
            Name = "UpgradeStatus",
            Position = new Vector2(24, 386),
            Size = new Vector2(1200, 40),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_upgradeStatus);

        // 🔴 M8.2：**Sanitarium 三项服务**（治病 ／ 除负面特质 ／ 锁正面特质）—— 消耗金钱 + 传家宝
        string[] services = { "cure_disease", "remove_negative_trait", "lock_positive_trait" };
        for (int i = 0; i < services.Length; i++)
        {
            string sName = services[i];
            var sb = new Button
            {
                Name = $"Sani_{sName}",
                Text = $"Sanitarium·{sName}",
                Position = new Vector2(24 + (i * 260), 430),
                Size = new Vector2(250, 36),
            };
            sb.Pressed += () => DoService(sName);
            AddChild(sb);
            _saniButtons[sName] = sb;
        }

        _saniStatus = new Label
        {
            Name = "SaniStatus",
            Position = new Vector2(24, 474),
            Size = new Vector2(1200, 60),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_saniStatus);

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
            // 🔴 M8.1：**真实点击路径**升级（发 `Pressed` 信号）⇒ 证明"升级入口从启动场景可达、且点得动"
            int costBefore = ExpeditionContext.Heirlooms?.EffectiveReliefCost(_cfg.StressReliefCost) ?? -1;
            PressUpgrade("tavern");
            int costAfter = ExpeditionContext.Heirlooms?.EffectiveReliefCost(_cfg.StressReliefCost) ?? -1;
            GD.Print($"[E2E] 阶段1 升级：减压价 {costBefore} 到 {costAfter}");

            // 🔴 M8.2 / V16：**患病 → 治病**（冒烟用：对**全队**按概率掷骰使其患病，再走**真实点击路径**治愈）
            if (_saniCfg is not null)
            {
                Sanitarium.RollContract(_log, _rng, _saniCfg, roster, roster.Heroes.Select(h => h.Id).ToArray());
                int sickTotal = roster.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0);
                string? sickHero = roster.Heroes.FirstOrDefault(h => roster.DiseasesOf(h.Id).Count > 0)?.Id;
                GD.Print($"[E2E] 阶段1 患病：全队 {roster.Heroes.Count} 人掷骰 ⇒ 患病 {sickTotal} 人（概率 0.15/0.12/0.10 ×3 病）");

                if (sickHero is not null)
                {
                    _selectedHero = sickHero; // 指定治疗对象（走"按人选"的入口）
                    int before = roster.DiseasesOf(sickHero).Count;
                    PressService("cure_disease");
                    int after = roster.DiseasesOf(sickHero).Count;
                    GD.Print($"[E2E] 阶段1 治病：{sickHero} 患病 {before} 到 {after}（V16：患病 → 治病 回路成立）");
                }
            }

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

    /// <summary>
    /// 🔴 M8.2 / V16：**Sanitarium 服务的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    public void PressService(string serviceName)
    {
        if (!_saniButtons.TryGetValue(serviceName, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressService({serviceName})：找不到按钮（红线 21）");
            return;
        }

        GD.Print($"[HamletRoot] PressService({serviceName})：发出真实 Pressed 信号（按钮「{btn.Text}」，置灰={btn.Disabled}）");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>**执行一项 Sanitarium 服务**：挑对象（优先玩家选中的、否则找有病/可改的人）→ 调用内核 → 打印结果。</summary>
    public void DoService(string serviceName)
    {
        Roster? roster = ExpeditionContext.Roster;
        Economy? economy = ExpeditionContext.Gold;
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (roster is null || economy is null || heirlooms is null || _saniCfg is null)
        {
            return;
        }

        // 选对象：优先玩家选中的人；否则按服务挑一个"有事可做"的（有病 / 有负面特质 / 有正面特质）
        string? hero = _selectedHero;
        hero ??= serviceName switch
        {
            "cure_disease" => roster.Heroes.FirstOrDefault(h => roster.DiseasesOf(h.Id).Count > 0)?.Id,
            "remove_negative_trait" => roster.Heroes.FirstOrDefault(h => roster.FindRemovableNegativeTrait(h.Id) is not null)?.Id,
            _ => roster.Heroes.FirstOrDefault(h => roster.FindLockablePositiveTrait(h.Id) is not null)?.Id,
        };

        if (hero is null)
        {
            GD.Print($"[HamletRoot] Sanitarium·{serviceName}：**没有可用对象**（拒绝对空做事）");
            Refresh();
            return;
        }

        CureOutcome o = serviceName switch
        {
            "cure_disease" => CureFirstDisease(roster, economy, heirlooms, hero),
            "remove_negative_trait" => Sanitarium.RemoveNegativeTrait(_log, _saniCfg, economy, heirlooms, roster, hero),
            _ => Sanitarium.LockPositiveTrait(_log, _saniCfg, economy, heirlooms, roster, hero),
        };

        GD.Print($"[HamletRoot] Sanitarium·{serviceName}：{(o.Paid ? "成交" : "拒绝（钱/传家宝不足，或无事可做）")}" +
                 $"　对象 {hero}　花 金钱{o.GoldSpent} 加 传家宝[{o.HeirloomSpent}]");
        Refresh();
    }

    private CureOutcome CureFirstDisease(Roster roster, Economy economy, HeirloomStock heirlooms, string heroId)
    {
        foreach (string d in roster.DiseasesOf(heroId).ToArray())
        {
            return Sanitarium.CureDisease(_log, _saniCfg!, economy, heirlooms, roster, heroId, d);
        }

        return new CureOutcome(false, 0, string.Empty);
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

        // 🔴 M8.2 / V15：Sanitarium 三服务的**成本显示 + 可用性置灰**（红线 21 (b)：由内核回答）
        HeirloomStock? saniHeirlooms = ExpeditionContext.Heirlooms;
        if (_saniCfg is not null && saniHeirlooms is not null && ExpeditionContext.Gold is not null)
        {
            int saniAffordable = 0;
            foreach ((string s, Button btn) in _saniButtons)
            {
                SanitariumService svc = _saniCfg.Service(s);
                string svcCost = $"{svc.Gold}金＋{string.Join("/", svc.Heirlooms.Select(k => $"{k.Key}×{k.Value}"))}";
                bool can = Sanitarium.CanAfford(_saniCfg, s, ExpeditionContext.Gold, saniHeirlooms);
                btn.Disabled = !can;
                btn.Text = $"Sanitarium·{s}（{svcCost}）";
                saniAffordable += can ? 1 : 0;
            }

            int sick = roster?.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0) ?? 0;
            _saniStatus.Text = $"Sanitarium：可支付 {saniAffordable}/3 项服务（不足即置灰）　患病英雄 {sick} 人" +
                               $"　负面特质可除 {roster?.Heroes.Count(h => roster.FindRemovableNegativeTrait(h.Id) is not null) ?? 0} 人";
        }

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

            // 🔴 红线 21 (b)：**按钮可用性由内核回答**（传家宝不足或已满级 ⇒ 置灰；不假装可用）
            foreach ((string b, Button btn) in _upgradeButtons)
            {
                bool can = heirlooms.CanUpgrade(b);
                btn.Disabled = !can;
                UpgradeLevel? next = heirlooms.NextLevel(b);
                btn.Text = next is null ? $"升级·{b}（已满级）" : $"升级·{b}（需 {string.Join("/", next.Cost.Select(k => $"{k.Key}×{k.Value}"))}）";
            }
        }
    }

    /// <summary>
    /// 🔴 M8.1 / 红线 21 (b)：**升级按钮的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    public void PressUpgrade(string building)
    {
        if (!_upgradeButtons.TryGetValue(building, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：找不到按钮（红线 21：按钮没挂上）");
            return;
        }

        GD.Print($"[HamletRoot] PressUpgrade({building})：发出真实 Pressed 信号（按钮「{btn.Text}」，置灰={btn.Disabled}）");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
