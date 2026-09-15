using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;

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

    /// <summary>🆕 **片 4**：地牢面板要用的配置（由 `ExpeditionComposition` 组装时一并放入 ⇒ 表现层直接读，**不必自己再解析一份**）✓</summary>
    public static Darkest.Data.CampSkillsConfig? CampSkills { get; private set; }

    /// <summary>🆕 同上：房间内容表 ✓</summary>
    public static Darkest.Data.RoomContentsConfig? RoomContents { get; private set; }

    /// <summary>🆕 同上：Curio 目录 ✓</summary>
    public static Darkest.Data.CuriosConfig? Curios { get; private set; }

    /// <summary>🆕 片 4：由组合根（`ExpeditionComposition`）放入面板配置 ✓</summary>
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

    /// <summary>确保跨趟经济存在（首次进入地牢层或回城时创建；已存在则复用同一实例）。</summary>
    public static Economy EnsureEconomy(EconomyConfig config, int gold = 0)
    {
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

    /// <summary>本趟结束：清空（下一趟重新 Begin）。</summary>
    public static void End()
    {
        Flow = null;
        Log = null;
    }
}
