using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;

using System.Collections.Generic;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// M7.5 **跨场景远征上下文**（场景层最小持有者）：
/// 战斗场景（`Battle.tscn`）与远征场景（`Expedition.tscn`）之间切换时，**流程状态必须活着**，
/// 否则每次切回来都会重开一趟（违反"一趟 = N 场"的语义）。
///
/// 🔴 归口：**场景层**（`scripts/gameplay/scene/`）—— 它是 Godot 侧粘合，**不进内核**；
/// 内核的 `ExpeditionFlow` 仍然零 Godot 引用，只被这里"取用/放回"。
/// · 进战斗前：`Begin(flow)`（把远征流程交给战斗场景）
/// · 战斗结束：`BattleRoot` 调 `flow.OnBattleFinished(...)` 再切回远征场景
/// · 远征结束（回城/完成/全灭）：`End()` 清空引用
/// </summary>
public static class ExpeditionContext
{
    /// <summary>当前进行中的远征流程（null = 不在远征中）。</summary>
    public static ExpeditionFlow? Flow { get; private set; }

    /// <summary>
    /// 🔴 **片 4：一次性"请求进入地牢"标记**（主菜单/Hamlet 的"出发远征"入口置它 ⇒ 宿主 `_Ready` 认领并进地图模式）
    /// ⚠️ 用**一次性认领**（`ConsumeRequestDungeon`）而不是布尔查询 ⇒ 不会出现"两次进入都当地牢"的串味 ✓
    /// </summary>
    private static bool _requestDungeon;

    /// <summary>片 4：入口置位（在**切场景之前**调用）✓</summary>
    public static void RequestDungeon() => _requestDungeon = true;

    /// <summary>片 4：宿主认领（**读到即清**）⇒ 只生效一次 ✓</summary>
    public static bool ConsumeRequestDungeon()
    {
        bool v = _requestDungeon;
        _requestDungeon = false;
        return v;
    }

    /// <summary>🆕 **片 4**：地牢面板要用的配置（由 `ExpeditionComposition` 组装时一并放入 ⇒ 表现层直接读，**不必自己再解析一份**）✓</summary>
    public static Darkest.Data.CampSkillsConfig? CampSkills { get; private set; }

    /// <summary>🆕 同上：房间内容表 ✓</summary>
    public static Darkest.Data.RoomContentsConfig? RoomContents { get; private set; }

    /// <summary>🆕 同上：Curio 目录 ✓</summary>
    public static Darkest.Data.CuriosConfig? Curios { get; private set; }

    /// <summary>🆕 片 4：由组合根（`ExpeditionComposition`）放入面板配置 ✓</summary>
    /// <summary>🆕 P0 养成闭环：**上一趟出发前的快照**（用于"本次 vs 上次"对比 ⇒ 让成长可读）✓</summary>
    public static Darkest.Gameplay.Sim.Run.RunStartSnapshot? LastRunStart { get; private set; }

    /// <summary>
    /// 🔴🆕 `D-4`（2026-09-20）：**本趟的目的地地区 id**（`ruins`/`weald`/`warrens`/`cove`）——
    /// 决定抽哪张陷阱表（`trap_defs.json` 按地区分条）。
    ///
    /// <para>🔴 **默认 `null`**：主城/地图层目前**还没有"选地区"的入口** ⇒ 未设 ⇒ `D-4` 的陷阱抽取**显式不做**
    /// （`TrapResolver.Pick` 拿不到地区 ⇒ 不抽不掷）—— 这是**可自证的有界缺口**，不是静默失效：
    /// `BattleRoot` 会打印"地区 = 未给（不抽）"，而 `TrapResistSourceDeclared` 同族自证 ✓</para>
    /// <para>⚠️ **为什么不在这里编一个默认地区**：那等于"谁替策划选了一个地区"（`TrapDefs.Parse` 的
    /// `region` 校验正是为拦这个而存在）⇒ 宁可 `null` + 打印，也不静默挑一个 ⚠️</para>
    /// </summary>
    public static string? RegionId { get; private set; }

    /// <summary>🔴 `D-4`：设置本趟地区（**由地图层/主城的"选地区"入口调**；`null` ⇒ 陷阱抽取关闭）✓</summary>
    public static void SetRegion(string? region) => RegionId = region;

    /// <summary>🆕 P0：当前是第几趟（从 1 起；每次进地牢 +1）✓</summary>
    public static int RunIndex { get; private set; }

    /// <summary>🆕 P0：记一次"出发"（返回"本次 vs 上次"的对比行；首次 ⇒ 只报基线）✓</summary>
    public static IReadOnlyList<string> CaptureRunStartAndDiff(
        Darkest.Gameplay.Sim.Run.Roster roster,
        Darkest.Gameplay.Sim.Run.HeirloomStock? heirlooms,
        Darkest.Gameplay.Sim.Run.Economy? economy)
    {
        Darkest.Gameplay.Sim.Run.RunStartSnapshot? prev = LastRunStart;
        RunIndex++;
        LastRunStart = Darkest.Gameplay.Sim.Run.RunStartSnapshot.Capture(RunIndex, roster, heirlooms, economy);
        // 🆕 **名册构成读数（㉝）也一起返回** ⇒ 宿主的"再出发"一行就同时有：
        //    **本次 vs 上次对比** ＋ **名册构成**（在册/等级分布/特质/疾病）⇒ 养成读数成体系 ✓
        var lines = new List<string>(LastRunStart.DiffLines(prev));
        lines.Add(Darkest.Gameplay.Sim.Run.RosterComposition.Describe(roster));

        // 🆕 **进度可见性**（A4 同族）：把"下一个解锁还差几趟/几胜"也带进"再出发"读数 ✓
        // 🔴 **纪律 Q**：读数**不得打断主流程** ⇒ 整段包 try/catch（解析失败就只少一行，绝不影响流程 ✓）
        try
        {
            Darkest.Data.UnlocksConfig? unlocksCfg = LooksUnlocks();
            if (unlocksCfg is not null)
            {
                if (Progress.NextUnlock(unlocksCfg) is { } next)
                {
                    lines.Add($"[下一解锁] {string.Join("/", next.Entry.Unlocks)}（还差 **{next.RunsRemaining} 趟** ／ {next.BattlesRemaining} 胜）✓");
                }
                else
                {
                    lines.Add("[下一解锁] **全部已解锁** ✓");
                }
            }
        }
        catch (System.Exception ex)
        {
            lines.Add($"[下一解锁] （读数不可用，不影响流程：{ex.GetType().Name}）✓");
        }
        return lines;
    }

    public static void BindConfigs(Darkest.Data.CampSkillsConfig campSkills,
        Darkest.Data.RoomContentsConfig roomContents, Darkest.Data.CuriosConfig curios)
    {
        CampSkills = campSkills;
        RoomContents = roomContents;
        Curios = curios;
    }

    /// <summary>本趟远征共用的日志（事件流是唯一事实来源）。</summary>
    public static CombatLog? Log { get; private set; }

    /// <summary>
    /// 🔴 M8.0 ②（`blueprint §9.15`）：**金钱是跨会话状态** —— 它**跨场景、跨趟存活**，
    /// 因此**不随 `End()` 清空**（与 `Flow`/`Log` 的"一趟"生命周期不同）。
    /// </summary>
    public static Economy? Gold { get; private set; }

    /// <summary>🆕 **M7②（策划 `#423`）**：**名册上限曲线的唯一来源** = 马车的 `roster_cap_by_level`
    /// （`9→12→16→20→24→28`，索引 = 马车等级）✓
    /// 由 `EnsureEconomy` 在拿到已解析数据时缓存 ⇒ UI 只需把它传给 `CurrentRosterCap(...)` ✓
    /// （🔴 这是"上限单一来源 = 马车"的落点：名字册上限**只由它**决定 ✓）</summary>
    public static IReadOnlyList<int>? StagecoachCapCurve { get; private set; }

    /// <summary>确保跨趟经济存在（首次进入地牢层或回城时创建；已存在则复用同一实例）。</summary>
    public static Economy EnsureEconomy(EconomyConfig config, int gold = 0)
    {
        // 🆕 M7②：顺手缓存"上限曲线"（配置在手时最省事 ⇒ 调用方无需自己找 EconomyConfig ✓）
        StagecoachCapCurve ??= config.Coach.RosterCapByLevel;
        Gold ??= new Economy(config, gold);
        return Gold;
    }

    /// <summary>
    /// 🔴 M8.0（`#287` = **`#245` 的落地**）：**名册（含士气）也是跨会话状态** ——
    /// 与 `Gold` 同层、**不随 `End()` 清空**（士气跨趟累积才满足 `#245`「回城完全不恢复」）。
    /// </summary>
    public static Roster? Roster { get; private set; }

    /// <summary>确保跨趟名册存在（复用同一实例；已存在不重建 ⇒ 士气不会被重置）。</summary>
    public static Roster EnsureRoster(RosterConfig config)
    {
        Roster ??= new Roster(config);
        return Roster;
    }

    /// <summary>
    /// 🔴 M8.1：**传家宝（第三种资源）与建筑升级也是跨会话状态** —— 与 `Gold` 同层、**不随 `End()` 清空**。
    /// </summary>
    public static HeirloomStock? Heirlooms { get; private set; }

    /// <summary>确保跨趟传家宝库存存在（复用同一实例 ⇒ 库存与升级等级不被重置）。</summary>
    public static HeirloomStock EnsureHeirlooms(HeirloomConfig config)
    {
        Heirlooms ??= new HeirloomStock(config);
        return Heirlooms;
    }

    /// <summary>
    /// 🔴 **跨趟进度 + 解锁评估**（`O-86` / `next_round` ③）：与 `Gold`/`Roster`/`Heirlooms` **同层**、
    /// **不随 `End()` 清空** ✓（此前**不存在**任何跨趟出征计数器 ⇒ 解锁阈值表没有输入 ⚠️；本属性补上这一层）
    /// </summary>
    public static Darkest.Gameplay.Sim.Run.RunProgress Progress { get; } = new();

    private static Darkest.Data.UnlocksConfig? _unlocks;

    /// <summary>🆕 惰性读取解锁表（**只读**；失败就返回 null ⇒ 由调用方如实少打一行 ✓）</summary>
    private static Darkest.Data.UnlocksConfig? LooksUnlocks()
    {
        if (_unlocks is not null)
        {
            return _unlocks;
        }

        if (!Godot.FileAccess.FileExists(Darkest.Data.UnlocksConfig.ResPath))
        {
            return null;
        }

        // 🔴 **必须同时给 Curio 目录**（P27 ④：解锁引用了不存在的 curio ⇒ `Parse` 会抛）——
        //    我第一版传 `curioIds: null` ⇒ 每次读数都抛 ⚠️（**幸好纪律 Q 兜住，流程没断** ✓）
        //    ⇒ 这里把 `curios.json` 的 id 读出来一起传 ✓
        var curioIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        if (Godot.FileAccess.FileExists(Darkest.Data.CuriosConfig.ResPath))
        {
            Darkest.Data.CuriosConfig curios = Darkest.Data.CuriosConfig.Parse(
                Godot.FileAccess.GetFileAsString(Darkest.Data.CuriosConfig.ResPath));
            foreach (Darkest.Data.CurioConfig c in curios.RealCurios)
            {
                curioIds.Add(c.Id);
            }
        }

        _unlocks = Darkest.Data.UnlocksConfig.Parse(
            Godot.FileAccess.GetFileAsString(Darkest.Data.UnlocksConfig.ResPath),
            new System.Collections.Generic.HashSet<string>(Darkest.Data.HeirloomConfig.AllowedBuildings, System.StringComparer.Ordinal),
            curioIds, rosterHardCap: 12);
        return _unlocks;
    }

    /// <summary>
    /// **端到端冒烟阶段计数**（M8.0 ⑥）：`0` 未开始 ／ `1` 已跑完一趟回城 ／ `2` 已再出发。
    /// 只服务 `--e2e` 冒烟（**不参与游戏逻辑**），用于把"启动 到 跑图 到 回城 到 花钱 到 再出发"串成一次运行。
    /// </summary>
    public static int E2EStage { get; set; }

    public static bool IsActive => Flow is not null;

    /// <summary>进入战斗前绑定流程；第二次调用（返程）不覆盖。</summary>
    public static void Begin(ExpeditionFlow flow, CombatLog log)
    {
        Flow ??= flow;
        Log ??= log;
    }

    /// <summary>
    /// 🔴 `#307`③：**下一场战斗是不是"夜袭战斗"**（扎营后插进来的额外战斗）——
    /// 它跨**场景切换**传递：`ExpeditionRoot` 置位 ⇒ 玩家进 `Battle.tscn` 真打 ⇒ `BattleRoot` 结算时消费。
    /// 依据契约 `m7_expedition.md:35`：**夜袭战斗计入 6 场皆胜**（结算入口需要知道它不是节点步骤）。
    /// </summary>
    public static bool PendingAmbush { get; set; }

    /// <summary>消费"夜袭标记"（读到即清除 ⇒ 只影响这一场）。</summary>
    public static bool ConsumePendingAmbush()
    {
        bool v = PendingAmbush;
        PendingAmbush = false;
        return v;
    }

    /// <summary>更新引用（返程后由远征场景设置，保证同一个 flow 实例）。</summary>
    public static void Bind(ExpeditionFlow flow, CombatLog log)
    {
        Flow = flow;
        Log = log;
    }

    /// <summary>
    /// 🔴 **上一趟的会话**（`#245` **跨趟携带**的唯一输入）—— 与 `Gold`/`Roster`/`Heirlooms` **不同层**：
    /// 它**只在相邻两趟之间**有效（本趟开始后即被新会话取代）⇒ 由 `End()` 时从 `Flow` 里**摘出来**留存，
    /// 供下一趟开趟时走 `ExpeditionSession.CarryOverFrom`（HP 全恢复 / **士气不回** / 虚弱与死门后遗症清除）✓
    /// ⚠️ 若无此持有者，则"回城 → 再出发"在**生产路径**上永远拿不到上一趟的会话 ⇒ `CarryOverFrom`
    ///    只被测试调用（= 死函数）且 `#245` 的跨趟语义**落不了地** ⚠️
    /// </summary>
    public static ExpeditionSession? PreviousSession { get; private set; }

    /// <summary>本趟开始后认领上一趟的会话（**读到即清** ⇒ 不会跨两趟重复携带）✓</summary>
    public static ExpeditionSession? ConsumePreviousSession()
    {
        ExpeditionSession? p = PreviousSession;
        PreviousSession = null;
        return p;
    }

    /// <summary>本趟结束：清空（下一趟重新 Begin）；**同时把本趟会话留给下一趟**（`#245` 跨趟携带）✓</summary>
    public static void End()
    {
        PreviousSession = Flow?.Session; // 🔴 先摘会话，再清 Flow（顺序不可换）✓
        Flow = null;
        Log = null;
    }
}
