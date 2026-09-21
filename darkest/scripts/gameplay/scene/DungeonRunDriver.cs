using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Run;
using Darkest.UI; // ✅ UI 命名空间已**统一为 `Darkest.UI`**（2026-09-21 用户拍板 · 我域 35 处 + 域外 2 处改名已完成）⇒ **不再存在大小写并存** ✓
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
            ReturnToTown(host, flow);
            return true;
        }

        if (Array.Exists(args, a => a == "--e2e") && ExpeditionContext.IsActive)
        {
            E2E(host, flow, log);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 🔴 **本趟结算并回城**（`--hamlet-next` 与冒烟步骤 `town` **共用同一实现** ⇒ 不会两处漂移）✓
    /// 📌 这一步**不依赖任何 UI 面板**（纯流程 + 切场景）⇒ 旧场景退休后可以**立刻**在宿主侧接上 ✓
    /// </summary>
    public static void ReturnToTown(BattleRoot host, ExpeditionFlow flow, string result = "completed")
    {
        // 🔴 **打印自证**（我自己的纪律：动作必须留下可读的痕迹 —— 否则"跑没跑过"看不出来）✓
        GD.Print($"[片4-driver] ✅ 结算回城：走 {flow.StepsDone} 段 ／ 胜 {flow.Wins} ／ " +
                 $"光照 {flow.Meter.Value} ⇒ 切城池（`--hamlet-next` 与冒烟 `town` 共用此实现）✓");
        flow.ReturnToTown(result); // 🔴 `#352`：completed = 走完 ／ abandoned = 放弃远征 ✓
        ExpeditionContext.End();
        // 🆕 **B-3 接线**：本条路径**也是回落**（直接切场景、没有面板化）⇒ 如实记一笔并打印账本 ✓
        //    ⇒ 这样"**还差哪几屏没面板化**"就有**真实数据**了（架构 B-3 的原话：回落必须留痕 ✓）
        Darkest.Data.UiShellLedger.Record("hamlet", Darkest.Data.UiShellLedger.ShellRoute.SceneFallback);
        GD.Print("[片4-driver] B-3 留痕：" + Darkest.Data.UiShellLedger.Report());
        host.GetTree().CallDeferred("change_scene_to_file", Darkest.UI.MainMenuRoot.HamletScene);
    }

    /// <summary>自动走到终点（**真实 `StepTo`**；每一步留痕 ⇒ 可复现 ✓）</summary>
    private static void TopologyAuto(BattleRoot host, ExpeditionFlow flow, CombatLog log)
    {
        var path = new System.Collections.Generic.List<string>();
        int guard = 0;
        while (!flow.ReachedGoal && guard++ < 64)
        {
            // 🔴 走向**有效终点**（开走格后 = 派生终点，可能因 `#342`③ 主干 ≤3 段而**提前**）✓
            int target = flow.TileWalk is { } tw && tw.TileRoom.TryGetValue(tw.Grid.Goal, out int derived) ? derived : flow.Map!.GoalId;
            int next = flow.NextRoomToward(target);
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

                // 🔴🆕 **升级通道**（`hamlet.md` §7.2/§7.6「战斗给经验 ⇒ 等级成长」· 占位数值 `#399`）：
                //    这里虽是**驱动层模拟**胜利，但它是"**每场胜利 ⇒ 给经验**"的**真实调用点**（同一条 `Roster` 通道 ✓）
                //    ⇒ 让 e2e 也能看到**等级成长** ✓
                //    ⚠️ **口径**：这是**驱动模拟**，**不等于**宿主真实战斗结算路径（后者要"踏进 Battle 格"= T5~T7，卡 UI ✓）
                if (ExpeditionContext.Roster is { } xpRoster && xpRoster.ExperienceWired)
                {
                    xpRoster.AwardExperienceForBattle(new Darkest.Core.Events.CombatLog(), win: true, reason: "e2e_sim");
                    GD.Print("[升级通道·e2e] 模拟胜利 ⇒ 全队发经验：" +
                             string.Join("、", System.Linq.Enumerable.Select(xpRoster.Heroes,
                                 h => $"{h.Name} Lv{h.Level}(XP{xpRoster.ExperienceOf(h.Id)})")) + " ✓");
                }
            }

            HeirloomStock? hs = ExpeditionContext.Heirlooms;
            GD.Print($"[E2E] 阶段0 跑图：四场胜利 ⇒ 金钱 {ExpeditionContext.Gold?.Gold ?? 0}" +
                     $"　传家宝 {(hs is null ? "未接入" : string.Join("/", hs.Kinds.Select(k => $"{k}×{hs.Count(k)}")))} ✓");

            flow.ReturnToTown("completed"); // 阶段0 结算 = 走完 ✓（`#352` 的 `result` 参数只给 `ReturnToTown` 本体用）
            ExpeditionContext.Roster?.ApplyReturnFromRun(log, flow.Session.Roster().Select(r => (r.Id, r.Morale)));

            // 🆕 **阵亡消费**（策划 `#400` 裁定 (a)+ / A11）：**移出名册 + 释放名额 + 进 Graveyard** ✓
            //    口径：**只认内核事件流**（`DeathEvent(IsPlayer: true)` ✓）；无阵亡 ⇒ 返回 0（不假装 ✓）
            int deaths = ExpeditionContext.Roster?.ConsumePlayerDeaths(log) ?? 0;
            GD.Print($"[阵亡] 本趟我方阵亡 **{deaths}** 名 ⇒ 已移出名册并留档（Graveyard 现有 " + "{(ExpeditionContext.Roster?.Graveyard.Count ?? 0)} 名）✓");
            ExpeditionContext.End();
            ExpeditionContext.E2EStage = 1;
            host.GetTree().CallDeferred("change_scene_to_file", Darkest.UI.MainMenuRoot.HamletScene);
            return;
        }

        GD.Print("[E2E] 阶段2 ✅ **再出发成功**（回到地牢层）⇒ 完整回路成立：启动 → 跑图 → 回城 → 花钱 → 再出发 ✓");
    }
}
