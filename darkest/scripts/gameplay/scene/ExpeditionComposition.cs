using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **`#327` 片 4：把【远征流程的组装】搬进宿主**（原在 `scripts/ui/ExpeditionRoot.cs` 的 `_Ready` 里）。
///
/// 为什么要有这个文件：片 4 的终态是"**唯一场景 `Battle.tscn` + 地图模式 = 地牢**" ⇒
/// 那么"谁来组装 `ExpeditionFlow`"就必须从**远征场景**（即将退休）搬到**宿主**这边 ✓
/// ⚠️ 面板（扎营/Curio/选路…）的迁移是 **UI 设计师** 的片 2（他逐个挂进 `BattleUI.DungeonHost()`）✓
/// ⚠️ 组装**只此一处**：`ExpeditionRoot` 退休后，它的那份必须删掉（否则又是"两处真值"⇒ 会各自漂移）⚠️
///
/// 忠实照搬（逐行对照 `ExpeditionRoot` 304~405 行；含两处**真缺陷修复**的注入点）：
///   · `BindSortie(heroSlots)`：跨场 buff / 扎营加成 / **O-83 血结转**都靠它（按槽位映射）✓
///   · `Unlocks = unlocksCfg`：`2b34478` 修的真缺陷（未解锁 Curio 抽不到）——**注入点必须在这里** ✓
///   · `Progress = ExpeditionContext.Progress`：跨趟进度（next_round ③）✓
/// </summary>
public static class ExpeditionComposition
{
    /// <summary>组装产物（宿主与面板都需要的那几样；面板侧配置一并带出，避免二次解析）✓</summary>
    public sealed record Built(
        ExpeditionFlow Flow,
        CampSkillsConfig CampSkills,
        RosterConfig RosterCfg,
        RoomContentsConfig RoomContents,
        CuriosConfig Curios,
        IReadOnlyDictionary<string, int> HeroSlots);

    /// <summary>
    /// 组装一趟远征（**幂等**：若 `ExpeditionContext` 已有活动流程 ⇒ 直接复用，不重开一趟）✓
    /// </summary>
    public static Built BuildInScene(Node host, CombatLog log)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (ExpeditionContext.IsActive && ExpeditionContext.Flow is { } existing)
        {
            GD.Print("[片4] 返程/复用：已有活动远征流程 ⇒ **不重开一趟**（沿用同一张地图与进度）✓");
            return new Built(existing, CampSkillsConfig.Parse(FileAccess.GetFileAsString(CampSkillsConfig.ResPath)),
                RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath)),
                RoomContentsConfig.Parse(FileAccess.GetFileAsString(RoomContentsConfig.ResPath),
                    CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath))),
                CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath)),
                new Dictionary<string, int>(StringComparer.Ordinal));
        }

        DirectorBridge.DirectorHandle handle = DirectorBridge.BuildFromRes(host);
        TuningConfig tuning = handle.Tuning;

        // 🔴 M8.0 ①(c)（#286）：出征 6 人由【名册】提供（阵型模板只给槽位/敌方）✓
        RosterConfig roster = RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath));
        FormationConfig template = FormationConfig.Parse(FileAccess.GetFileAsString(FormationConfig.ResPath));
        IReadOnlyList<HeroConfig> sortie = FormationSortie.SelectForTemplate(template, roster);

        // 🆕 **英雄美术解析读数**（用户 2026-09-19：把 mod 英雄接进来**替换现有内容**）——
        //    在真实构建里把"每个原型 ⇒ 用哪份美术"打一次：正式优先 → 按原型占位 → 旧单包 → 点名 ✓
        //    🔴 纪律 Q：**只打印、不改行为**；解析失败也**不抛**（留痕即可）✓
        try
        {
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (HeroConfig h in sortie)
            {
                if (!seen.Add(h.Archetype))
                {
                    continue;
                }

                HeroArtResolution art = HeroArtResolver.Resolve(h.Archetype, Godot.FileAccess.FileExists);
                string kind = art.Source switch
                {
                    HeroArtSource.Formal => "正式",
                    HeroArtSource.Placeholder => "占位(按原型)",
                    HeroArtSource.PlaceholderLegacy => "占位(旧单包)",
                    _ => "无(回落色块)",
                };
                GD.Print($"[英雄美术] {h.Archetype} ⇒ {kind}　{art.Note}");
            }
        }
        catch (System.Exception ex)
        {
            GD.Print($"[英雄美术] 读数不可用（不影响流程）：{ex.GetType().Name}");
        }
        Roster shared = ExpeditionContext.EnsureRoster(roster);

        var openingMorale = new List<int>();
        var diseasePenalties = new List<DiseasePenalty>();
        var traitEffects = new List<TraitEffects>();
        SanitariumConfig saniCfg = SanitariumConfig.Parse(FileAccess.GetFileAsString(SanitariumConfig.ResPath));
        foreach (HeroConfig h in sortie)
        {
            openingMorale.Add(shared.MoraleOf(h.Id));
            diseasePenalties.Add(Sanitarium.TotalPenalty(saniCfg, shared, h.Id));
            traitEffects.Add(shared.TraitEffectsOf(h.Id));
        }

        GD.Print($"[片4] 名册出征 6 人（按模板槽位原型配人）：" +
                 string.Join("、", sortie.Select((h, i) => $"{h.Name}({h.Archetype} Lv{h.Level} 士气{openingMorale[i]})")));

        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _); // 整备默认 = 推荐配置（2/9/support_crate）
        bag.LockForRun();

        var session = new ExpeditionSession(
            _ => DirectorBridge.BuildFromRes(host, sortie, roster.LevelGrowth, openingMorale, diseasePenalties, traitEffects).Core,
            tuning.Expedition.NBattles,
            firewood: bag.CountOf(ItemKind.Firewood),
            food: bag.CountOf(ItemKind.Food),
            ambushChance: tuning.Expedition.AmbushChance);

        // 🔴 跨场 buff 的【hero → 战斗单位】映射：按【阵型槽位】挂（两套 id 体系的唯一正确接法）✓
        var heroSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<RosterEntryConfig> playerSlots = template.InitialRoster.Player;
        for (int i = 0; i < sortie.Count && i < playerSlots.Count; i++)
        {
            heroSlots[sortie[i].Id] = playerSlots[i].Slot;
        }

        session.BindSortie(heroSlots);
        session.BindCampHeroes(roster.Heroes);

        var meter = new LightMeter(tuning.Light!);

        EconomyConfig econCfg = EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath));
        Economy economy = ExpeditionContext.EnsureEconomy(econCfg);
        HeirloomConfig heirloomCfg = HeirloomConfig.Parse(FileAccess.GetFileAsString(HeirloomConfig.ResPath));
        HeirloomStock heirlooms = ExpeditionContext.EnsureHeirlooms(heirloomCfg);

        // 🔴 `2b34478` 真缺陷修复的注入点：解锁表必须在**构造前**备好（`Unlocks` 是 `init` 属性）✓
        CuriosConfig curiosCfg = CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath));
        UnlocksConfig unlocksCfg = UnlocksConfig.Parse(
            FileAccess.GetFileAsString(UnlocksConfig.ResPath),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curiosCfg.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            shared.Cap);

        var flow = new ExpeditionFlow(session, meter, bag, new Scouting(tuning.Scouting!, tuning.Light!),
            handle.Nodes, tuning, log, new Darkest.Core.Rng.RngProvider(20260909), economy, heirlooms, heirloomCfg)
        {
            Progress = ExpeditionContext.Progress,
            Unlocks = unlocksCfg,
        };
        ExpeditionContext.Bind(flow, log);

        // 🔴🔴 **必须是拓扑模式**（表现层"地图模式"的唯一形态）—— 原 `ExpeditionRoot` 也在此处 `BeginTopology` ✓
        //    ⚠️ 我第一版**漏了这一句** ⇒ 宿主在线性模式下跑（无地图、`Current` 步存在）⇒ 步骤 ↔ 战斗**对不上** ⇒ 冒烟跑飞 ⚠️
        ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(FileAccess.GetFileAsString(ExpeditionMapConfig.ResPath));
        ExpeditionMap map = flow.BeginTopology(mapCfg);
        GD.Print($"[片4] 拓扑模式已开启：{map.RoomCount} 间 ／ 支路 {map.BranchCount} ／ 分叉 {map.ForkCount} ／ " +
                 $"起点 {map.StartId} ⇒ 终点 {map.GoalId}（还剩 {flow.RemainingSegmentsToGoal} 段）✓");

        CampSkillsConfig campSkills = CampSkillsConfig.Parse(FileAccess.GetFileAsString(CampSkillsConfig.ResPath));
        RosterConfig rosterCfg = RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath));
        RoomContentsConfig roomContents = RoomContentsConfig.Parse(
            FileAccess.GetFileAsString(RoomContentsConfig.ResPath), curiosCfg);

        ExpeditionContext.BindConfigs(campSkills, roomContents, curiosCfg); // 🔴 片 4：面板配置进上下文 ⇒ 表现层读一处 ✓
        return new Built(flow, campSkills, rosterCfg, roomContents, curiosCfg, heroSlots);
    }
}
