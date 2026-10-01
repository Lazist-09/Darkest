using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
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

        // 🔴🆕 **H-1 装备阶的【唯一容器】**（`P4 ①` · 2026-09-30 · **本处故意在会话构造之前**）：
        //    **先建出来**，再交给**投影 / 存档 / 升级机制**三处**共用同一实例** ✓
        //    ⚠️ 此前是在下面那个分支里就地 `BindGear(new HeroGearState())` ⇒ 存档**拿不到它**
        //       ⇒ 一存一读「**金币花了、阶没了**」（静默丢进度家族：玩家归因不到，日志里也没有一行）
        //    🔴 容器**与 `hero_upgrades.json` 在不在无关**：空表 == 全部英雄第 0 阶 ⇒
        //       文件缺失时**升级机制照样是关的**（下面那个分支会打印"关闭"），只是状态有了落点 ✓
        //    🔴 2026-09-30 架构裁定①（取「（甲）直读」）⇒ **护甲阶的投影来源就是这份容器**：
        //       组合根取阶（`ArmourTierOf`）→ 传参进桥 ⇒ 阶的**真值只此一处**（`HeroConfig.ArmourTier` 那条读点已换源删除 ✓）
        // 🔴🆕 2026-10-01 M6u 组合根修复（用户已授权）：`ExpeditionContext.Gear` **已有就用它** ——
        //    旧写法无条件 `new` ⇒ 玩家在 Hamlet 买了阶、一出征就被**覆盖清零**，回城自动存档再把空表写盘
        //    （静默丢进度家族：玩家归因不到、日志里也没有一行）✓
        //    ⚠️ 空表 ⇒ 仍是"全员第 0 阶"，与"未接线"同读数；数据在不在与容器无关（见上）✓
        var gear = ExpeditionContext.Gear ?? new HeroGearState();
        ExpeditionContext.BindGear(gear);

        // 🔴 按**出征槽位顺序**取阶（与 `sortie` 一一对应）——
        //    ⚠️ 空表 ⇒ 全第 0 阶 ⇒ **投影读数与旧口径逐字节相同**（旧来源 `HeroConfig.ArmourTier` 全仓无数据写入 ⇒ 恒 0 ✓）
        int[] armourTierBySlot = sortie.Select(h => gear.ArmourTierOf(h.Id)).ToArray();

        var session = new ExpeditionSession(
            _ => DirectorBridge.BuildFromRes(host, sortie, roster.LevelGrowth, openingMorale, diseasePenalties, traitEffects, armourTierBySlot).Core,
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

        // 🔴🔴 **`#245` 跨趟携带的生产接线**（此前**只被测试调用** ⇒ `CarryOverFrom` 是死函数、
        //    "回城 → 再出发"在真实路径上拿不到上一趟会话 ⇒ 士气**跨趟累积**落不了地）⚠️
        //    口径（`ExpeditionSession.CarryOverFrom` 文档 + M7 ⑱ 3 趟士气曲线用例）：
        //    · **HP 完全恢复**（回城口径）· **士气保留**（`#245`：回城完全不恢复）· 虚弱/死门后遗症清除 ✓
        //    · 首次出征（`PreviousSession` 为 null）⇒ **不做任何事**（新会话自带初始名册值）✓
        //    ⚠️ 认领即清（`ConsumePreviousSession`）⇒ 不会把同一趟会话携带两次 ✓
        if (ExpeditionContext.ConsumePreviousSession() is { } previousRun)
        {
            session.CarryOverFrom(previousRun);
            GD.Print($"[片4] #245 跨趟携带：上一趟 {previousRun.Roster().Count} 人 ⇒ **HP 全恢复**、" +
                     $"**士气保留**（回城不恢复）、虚弱/死门后遗症清除 ✓");
        }

        var meter = new LightMeter(tuning.Light!);

        EconomyConfig econCfg = EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath));
        Economy economy = ExpeditionContext.EnsureEconomy(econCfg);
        HeirloomConfig heirloomCfg = HeirloomConfig.Parse(FileAccess.GetFileAsString(HeirloomConfig.ResPath));

        // 🆕 **步骤 ②（接线）**：任务奖励通道 = **同一个 `heirlooms.json` 的 `quest_reward` 段** ✓
        //    🔴 必须在这里**补绑**：`HamletRoot.Build` 会**先**用不带通道的那次调用把跨趟库存建出来
        //       （那是 UI 域文件 ⇒ 我不动它）⇒ 靠 `BindQuestReward`（幂等 · 只补不换）把通道补上 ✓
        HeirloomQuestRewardConfig questRewardCfg =
            HeirloomQuestRewardConfig.Parse(FileAccess.GetFileAsString(HeirloomConfig.ResPath));
        questRewardCfg.Validate(); // 🔴 一手形状校验（4 地牢全 4 种 / 6 档只有 1·3·5 有值 / 首项 0 ✓）
        HeirloomStock heirlooms = ExpeditionContext.EnsureHeirlooms(heirloomCfg, questRewardCfg);

        // 🆕 步骤 ② 的**难度档代理输入**：队伍平均等级（⚠️ 我推的 ⇒ placeholder + O11 ✓）
        //    · 用 `sortie`（**实际出征的人**）而不是整本名册 —— 一手说的是"队伍"的 resolve level ✓
        //    · 等级实测来自 `HeroConfig.Level`（组合根后面就是用它做 `ApplyLevelGrowth` 投影的 ✓）
        double partyAverageLevel = sortie.Count == 0 ? 0.0 : sortie.Average(h => h.Level);
        GD.Print($"[片4] 传家宝步骤②接线：通道={(heirlooms.HasRunReward ? "已挂" : "缺失")} · " +
                 $"队伍平均等级 {partyAverageLevel:0.##} ⇒ 难度档 " +
                 $"{HeirloomQuestRewardConfig.ProxyDifficultyFromAverageLevel(partyAverageLevel)} ✓");

        // 🔴 `2b34478` 真缺陷修复的注入点：解锁表必须在**构造前**备好（`Unlocks` 是 `init` 属性）✓
        CuriosConfig curiosCfg = CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath));
        UnlocksConfig unlocksCfg = UnlocksConfig.Parse(
            FileAccess.GetFileAsString(UnlocksConfig.ResPath),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curiosCfg.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            shared.Cap);

        // 🔴🔴 `D-4`（2026-09-20）：**陷阱掷骰源** —— 与主流程**共用同一条随机流**（可复现）；
        //    ⚠️ 此前 `session.BindTrapRng` **无生产调用点**（只有测试调）⇒ 三扫判死函数 ⇒ 真实路径上
        //    "踩中陷阱"会**抛错**（`ResolveTrapByResist` 会话未注入 ⇒ 明确抛，不静默）⇒ 故必须在此绑定 ✓
        var runRng = new Darkest.Core.Rng.RngProvider(20260909);
        session.BindTrapRng(runRng);

        var flow = new ExpeditionFlow(session, meter, bag, new Scouting(tuning.Scouting!, tuning.Light!),
            handle.Nodes, tuning, log, runRng, economy, heirlooms, heirloomCfg, partyAverageLevel)
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

        // 🔴🆕 **H-1 装备阶的【唯一容器】**：**已在会话构造之前建好并 `BindGear`**（见本函数上方 ✓）——
        //    那里是**投影**的取阶处，往下依次是**存档**与**升级机制** ⇒ 三处共用同一实例 ✓

        // 🔴🔴 **`Phase 1`（2026-09-27）：存档系统接线** —— 到这一行时五个跨趟持有者
        //    （名册 / 进度 / 传家宝 / 经济 / **装备阶**）**已全部备齐** ⇒ 组装 `SaveController` 挂进上下文，
        //    供 UI 与流程在"一趟结束 / 建筑升级 / 回城"等时机触发存读档 ✓
        //    🔴 **opt-in 纪律**：`save.json` 缺失 ⇒ 机制**显式关闭并打印**（绝不半生效）✓
        if (FileAccess.FileExists(SaveConfig.ResPath))
        {
            SaveConfig saveCfg = SaveConfig.Parse(FileAccess.GetFileAsString(SaveConfig.ResPath));
            var saves = new SaveController(saveCfg, new SaveFileGateway(saveCfg),
                ExpeditionContext.EnsureRoster(rosterCfg), ExpeditionContext.Progress,
                heirlooms, economy, gear);
            ExpeditionContext.BindSaves(saves);
            GD.Print($"[片4·存档] 存档系统已接线：{saveCfg.SlotCount} 个槽位 · 目录 user://saves · " +
                     $"格式 v{Darkest.Gameplay.Sim.Save.SaveMigrator.CurrentVersion} ✓");
        }
        else
        {
            GD.Print("[片4·存档] 未找到 `save.json` ⇒ 存档机制**关闭**（opt-in；不静默）✓");
        }

        // 🔴🆕 **H-1（2026-09-27）：英雄装备阶接线** —— `hero_upgrades.json` 是它的**数值来源**（一手 15 职业树 ✓）
        //    🔴 **opt-in 纪律**：文件缺失 ⇒ 机制**显式关闭并打印**（绝不半生效）✓
        if (FileAccess.FileExists(HeroUpgradesConfig.ResPath))
        {
            HeroUpgradesConfig upCfg = HeroUpgradesConfig.Parse(
                FileAccess.GetFileAsString(HeroUpgradesConfig.ResPath));
            // 外部树目录（供 `Validate` 判定「前置悬空」）= **全部建筑树**（`buildings.json` 唯一真值）：
            //   `hero_upgrades.json` 的先决里有 `guild.skill_levels`（公会树，M8.3 未落地）⇒ 只列铁匠铺两条会把整表判失败 ⇒ 机制关死 ✓
            upCfg.Validate(BuildingsConfig.Parse(FileAccess.GetFileAsString(BuildingsConfig.ResPath)).TreeIds);
            ExpeditionContext.BindUpgrades(upCfg);
            GD.Print($"[片5·H-1] 装备阶已接线：{upCfg.TreeCount} 棵树 / {upCfg.LevelCount} 级 ／ " +
                     $"铁匠铺「武器」Lv{heirlooms.LevelOf(HeroGear.BuildingTreeId(GearAxis.Weapon))} ／ " +
                     $"「护甲」Lv{heirlooms.LevelOf(HeroGear.BuildingTreeId(GearAxis.Armour))} ／ " +
                     $"阶域 0~{HeroGear.MaxTier}（**按护甲阶取 prot/dodge** ✓）· " +
                     $"阶容器与存档**共用同一实例** ✓ · 护甲阶**投影已接线**（{armourTierBySlot.Length} 人已取阶；" +
                     "来源 = ExpeditionContext.Gear ⇒ 空表 = 全第 0 阶，**≠ 未接线** ✓）");
        }
        else
        {
            GD.Print($"[片5·H-1] 未找到 `{HeroUpgradesConfig.ResPath}` ⇒ 装备阶**升级机制关闭**（opt-in；不静默）· " +
                     "阶容器已挂（空表 = 全第 0 阶）⇒ 存档仍带 `gear` ✓ · **护甲阶投影仍已接线**" +
                     $"（{armourTierBySlot.Length} 人已取阶；来源 = ExpeditionContext.Gear；空表 = 全第 0 阶 ≠ 未接线 ✓）");
        }

        // 🔴🆕 `D-4`（2026-09-20）：**陷阱内容表** —— 与 `curiosCfg` 同族（内容表），**同处组装、同处绑定** ✓
        //    ⚠️ 文件**可以不存在**（陷阱是 opt-in 机制）⇒ 缺失 ⇒ `null` ⇒ `D-4` 显式关闭（不静默半生效）✓
        if (Godot.FileAccess.FileExists(TrapDefs.ResPath))
        {
            TrapDefs trapCfg = TrapDefs.Parse(Godot.FileAccess.GetFileAsString(TrapDefs.ResPath));
            builtTraps = trapCfg;
            GD.Print($"[片4·D-4] 陷阱表已加载：{trapCfg.Traps.Count} 条 ／ " +
                     $"闪避基准 {trapCfg.UnscoutedDodgePercent}% ／ 拆除加成 +{trapCfg.DisarmBonusPercent}% ／ " +
                     $"压力 {trapCfg.StressDamage} ／ 拆回压 {trapCfg.DisarmStressHeal} ✓");
        }
        else
        {
            GD.Print($"[片4·D-4] 未找到 `{TrapDefs.ResPath}` ⇒ 陷阱机制**关闭**（opt-in；不静默）✓");
        }

        // 🔴🆕 `D-7`（2026-09-20）：**探索层 act-out** 自证 —— 带折磨者拒绝摸奇物 / 拒绝进食。
        //    ⚠️ 与 D-4 不同：**没有独立内容表**（判据用 `tuning.dungeon_layer.exploration` + 士气读数）
        //    ⇒ 此处只打印"机制是否启用 + 两条概率 + 阈值"，供冒烟/日志核对 ✓
        if (tuning.DungeonLayer?.Exploration is { } actOut)
        {
            GD.Print($"[片4·D-7] 探索层 act-out **已启用**：折磨阈值 士气<{actOut.MoraleAfflictionThreshold}（= `morale.start`）・" +
                     $"拒绝摸奇物 {actOut.CurioRefusePercent}% ・拒绝进食 {actOut.EatRefusePercent}% " +
                     $"⇒ 判据 = **队内最低士气**（只算存活者；`Retained` 战后落账 ⇒ 首战前不生效，与 D-4/D-5 同源）✓");
        }
        else
        {
            GD.Print("[片4·D-7] 未配置 `tuning.dungeon_layer.exploration` ⇒ 探索层 act-out **关闭**（opt-in；不静默）✓");
        }

        return new Built(flow, campSkills, rosterCfg, roomContents, curiosCfg, heroSlots);
    }

    /// <summary>🔴 `D-4`：本趟的陷阱内容表（`null` = 未配置 ⇒ 机制关闭）✓</summary>
    private static TrapDefs? builtTraps;

    /// <summary>🔴 `D-4`：供宿主读的陷阱表（`null` ⇒ 未配置）✓</summary>
    public static TrapDefs? Traps => builtTraps;
}
