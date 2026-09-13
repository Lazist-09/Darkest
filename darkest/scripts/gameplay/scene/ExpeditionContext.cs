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

    public static bool IsActive => Flow is not null;

    /// <summary>进入战斗前绑定流程；第二次调用（返程）不覆盖。</summary>
    public static void Begin(ExpeditionFlow flow, CombatLog log)
    {
        Flow ??= flow;
        Log ??= log;
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
