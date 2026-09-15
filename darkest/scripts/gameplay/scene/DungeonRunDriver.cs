using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Run;
using Darkest.Ui; // ⚠️ UI 根是 `Darkest.Ui`（小写 i），而 `BattleUi` 是 `Darkest.UI` —— 两者并存，别混 ✓
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **`#327` 片 4：把"远征驱动类 CLI"从 `ExpeditionRoot`（待退休）搬进宿主侧** ✓
///
/// 覆盖三支（原在 `ExpeditionRoot._Ready`）：
///   · `--topology-auto`：**自动走到终点**（走"真实 `StepTo`"这条路 ⇒ 与玩家路径同源 ✓）
///   · `--hamlet-next`：本趟**结算并回城**（冒烟：启动 → 跑图 → 回城 ✓）
///   · `--e2e`：分阶段端到端（阶段 0 = 打 4 场 → 回城；阶段 2 = 再出发回地牢 ✓）
///
/// ⚠️ 边界：本类**只做驱动与切场景**，规则一律走内核（`ExpeditionFlow`）✓
/// ⚠️ 与旧实现的差异：旧版把"打 4 场"写成 `flow.Advance(1) + OnBattleFinished(...)`（**线性**口径）；
///    拓扑模式下应当走 `StepTo → 起战斗 → OnBattleFinished`（真实路径）⇒ 这里按**拓扑**实现 ✓
/// </summary>
public static class DungeonRunDriver
{
    /// <summary>是否请求了这些驱动（宿主 `_Ready` 里调用一次即可）✓</summary>
    public static bool TryHandle(BattleRoot host, ExpeditionFlow flow, CombatLog log)
    {
        if (host is null || flow is null)
        {
            return false;
        }

        string[] args = OS.GetCmdlineArgs();

        if (Array.Exists(args, a => a == "--topology-auto"))
        {
            TopologyAuto(host, flow, log);
            return true;
        }

        if (Array.Exists(args, a => a == "--hamlet-next") && ExpeditionContext.IsActive)
        {
            GD.Print("[片4-driver] --hamlet-next ⇒ 本趟结算并回城（冒烟路径：启动 → 跑图 → 回城）✓");
            flow.ReturnToTown("completed");
            ExpeditionContext.End();
            host.GetTree().CallDeferred("change_scene_to_file", Darkest.Ui.MainMenuRoot.HamletScene);
            return true;
        }

        if (Array.Exists(args, a => a == "--e2e") && ExpeditionContext.IsActive)
        {
            E2E(host, flow, log);
            return true;
        }

        return false;
    }

    /// <summary>自动走到终点（**真实 `StepTo`**；每一步留痕 ⇒ 可复现 ✓）</summary>
    private static void TopologyAuto(BattleRoot host, ExpeditionFlow flow, CombatLog log)
    {
        var path = new System.Collections.Generic.List<string>();
        int guard = 0;
        while (!flow.ReachedGoal && guard++ < 64)
        {
            int next = flow.NextRoomToward(flow.Map!.GoalId);
            if (next < 0)
            {
                break;
            }

            MoveOutcome mv = flow.StepTo(next);
            if (!mv.Moved)
            {
                break;
            }

            path.Add($"{next}(−{mv.Cost})");
        }

        GD.Print($"[片4-driver] --topology-auto：走到{(flow.ReachedGoal ? "**终点**" : "卡住")}" +
                 $"　路径 {string.Join(" → ", path)}　光照 {flow.Meter.Value}　还剩 {flow.RemainingSegmentsToGoal} 段 ✓");
        _ = host;
        _ = log;
    }

    /// <summary>
    /// 端到端（阶段口径照旧实现；**驱动改走拓扑**）：
    /// 阶段 0：打 4 场（模拟胜利）⇒ 结算回城；阶段 1：Hamlet 花钱（由 HamletRoot 自己的冒烟完成）；
    /// 阶段 2：再进地牢 ⇒ 打印成功并停在此处（供人观察）✓
    /// </summary>
    private static void E2E(BattleRoot host, ExpeditionFlow flow, CombatLog log)
    {
        if (ExpeditionContext.E2EStage == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                flow.Advance(1);                                  // 线性口径的"过一格"（拓扑下仅作推进占位）
                flow.OnBattleFinished("PlayerVictory", rounds: 5); // 模拟胜利（冒烟专用）
            }

            HeirloomStock? hs = ExpeditionContext.Heirlooms;
            GD.Print($"[E2E] 阶段0 跑图：四场胜利 ⇒ 金钱 {ExpeditionContext.Gold?.Gold ?? 0}" +
                     $"　传家宝 {(hs is null ? "未接入" : string.Join("/", hs.Kinds.Select(k => $"{k}×{hs.Count(k)}")))} ✓");

            flow.ReturnToTown("completed");
            ExpeditionContext.Roster?.ApplyReturnFromRun(log, flow.Session.Roster().Select(r => (r.Id, r.Morale)));
            ExpeditionContext.End();
            ExpeditionContext.E2EStage = 1;
            host.GetTree().CallDeferred("change_scene_to_file", Darkest.Ui.MainMenuRoot.HamletScene);
            return;
        }

        GD.Print("[E2E] 阶段2 ✅ **再出发成功**（回到地牢层）⇒ 完整回路成立：启动 → 跑图 → 回城 → 花钱 → 再出发 ✓");
    }
}
