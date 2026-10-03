// 🔴 从 BattleRoot.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3：**只抽流程驱动口**）——只搬家、零行为改动 ✓
//    本文件 = 流程驱动口（进地牢 EnterDungeonInScene / 回城 GoToHamlet）

using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Skill;
using Darkest.UI;
using Godot;

namespace Darkest.Gameplay.Scene;

public sealed partial class BattleRoot
{
    public bool EnterDungeonInScene()
    {
        Darkest.Core.Events.CombatLog log = ExpeditionContext.Log ?? new Darkest.Core.Events.CombatLog();
        ExpeditionComposition.Built built = ExpeditionComposition.BuildInScene(this, log);
        if (_ui is null)
        {
            _ui = GetNode<BattleUI>("UILayer/BattleUI");
        }

        // 🔴🔴 2026-10-03 修（真缺陷 · 玩家路径）：**进地牢必须先建 UI** ——
        //    实测（`.tmp_tile_smoke.txt`）：进地牢后只有两条「地图模式请求：**UI 尚未构建** ⇒ 延后到 Bind() 之后」，
        //    **没有** `[BattleUI] 暗黑地牢式排布就绪` ／ `[UI 模式] 进入【地图模式】` ／ `[UI 瓷砖]` ⇒ **玩家看不到地图** ⚠️
        //    根因链：本方法**不调 `Bind()`**；而 `EnterMapMode()`（`BattleUI.Dungeon.cs`）在 `_bottomRow` ／ `_uiRoot`
        //    为空时只置 `_pendingMapMode = true` 就返回，**唯一解药 `Bind()` 只有 `BindUi()` 会调** ⇒ 纯进地牢路径永远不建 UI ✓
        //    · `Bind()` 只建 UI、**不起战斗**（起战斗仍在 `StartExpeditionBattleInScene` ／ `NewGame`）⇒ 不改任何规则 ✓
        //    · 必须 **deferred**：本方法在 `_Ready()` 期间被调，此时 `AddChild` 会撞
        //      「Parent node is busy adding/removing children」（实测踩过）⚠️
        //    · `Bind()` **不接 `_view`**（视图由战斗开始时接：`SetBattleView`）⇒ 地图模式里卡片 ／ 技能栏保持空转（预期）✓
        Callable.From(BindUi).CallDeferred();
        GD.Print("[片4] ✅ 已排入 `BindUi()`（延后一帧）⇒ 进地牢**会建 UI**（此前只置 `_pendingMapMode` 而无人 `Bind()` ⇒ 地图不显示）✓");

        _dungeonHostedInScene = true; // 🔴 片 4 过渡标记（战后据此回地图模式）✓
        // 🔴 片 4 收口：**把走格真正开起来**（UI 报"全项目无人调 `EnableTileWalk`" ⇒ 走格接口一直没人用 ⚠️）
        //    · 段消耗**从数据取**（`tuning.light.node_step` = −30，负值 = 消耗 ⇒ 取反得 30）⇒ **代码不写死** ✓
        //    · 幂等 + opt-in：不改变任何既有规则；表现层据此渲染"格子主画面" ✓
        // 🔴 D-1（2026-09-20）：回头代价同样**从数据取**（`tuning.dungeon_layer.revisit_light_cost`）；
        //    未配置 ⇒ 传 null ⇒ 回头代价关闭（既有行为不变）✓
        int segmentCost = -built.Flow.Tuning.Light!.NodeStep;
        int? backtrackCost = built.Flow.Tuning.DungeonLayer?.RevisitLightCost;
        built.Flow.EnableTileWalk(segmentCost, backtrackCost);

        // 🔴🔴 `D-4`（2026-09-20）：**接线陷阱机制**（否则 `TrapResolver` 永远没人调 = 库写完了没人用 ⚠️）
        //    · 陷阱表由组合根加载并绑定（`ExpeditionComposition.Traps`）⇒ 此处**只读**，不重复解析 ✓
        //    · 地区 id **必须显式给**（`SetRegion`）：未给 ⇒ 不抽（**不静默挑一个地区** —— 那等于"谁替策划选了"）⚠️
        //    · `trap_resist` 目前在 `units.json` 里**不存在** ⇒ 不传抗性查询口 ⇒ 退化解（全员 0），
        //      由 `TrapResistSourceDeclared` **自证**（表现层/验收能判断"退化解是否在用"）✓
        TrapDefs? trapDefs = ExpeditionComposition.Traps;
        built.Flow.BindTraps(trapDefs);
        built.Flow.SetRegion(ExpeditionContext.RegionId);
        GD.Print($"[片4] ✅ 走格已开启（段消耗 = −`light.node_step` = {segmentCost}，取自已解析数据）" +
                 $"　回头代价 = {(backtrackCost is { } bc ? bc.ToString() : "未配置（关闭）")}" +
                 $"　网格 {built.Flow.TileWalk!.Grid.Width}×{built.Flow.TileWalk.Grid.Height}" +
                 $"　房间块 {built.Flow.TileWalk.Segments.Count} 段走廊　队伍在 ({built.Flow.TilePosition.X},{built.Flow.TilePosition.Y}) ✓");
        GD.Print($"[片4·D-4] ✅ 陷阱已接线：{(trapDefs is null ? "未配置（机制关闭）" : $"陷阱表 {trapDefs.Traps.Count} 条")}　" +
                 $"撒布 = {(built.Flow.Tuning.DungeonLayer?.Traps is { } ts ? $"{ts.CorridorChancePercent}%/走廊格" : "未配置（一格不撒）")}　" +
                 $"本轮派生陷阱格 = {built.Flow.TileWalk.TrapTiles}　" +
                 $"地区 = {ExpeditionContext.RegionId ?? "**未给（不抽）**"}　" +
                 $"抗性来源 = {(built.Flow.TrapResistSourceDeclared ? "已声明" : "**未声明**（退化解：全员按 0 算）")} ✓");

        // 🔴🔴 `D-6`（2026-09-20）：**接线隐藏房** —— 打印自证（否则"撒了但没人揭示"= 静默失效 ⚠️）
        //    · 揭示**不需要新接线**：它挂在既有侦察路径（Curio 的 `scout` 效果）上，见
        //      `ExpeditionFlow.RoomInteractions.ApplyCurioEffect("scout")` ✓
        //    · 这里**只报状态**：配了多少 / 撒了几格 / 回报多少 —— 让"机制真的开着"可被肉眼核对 ✓
        //    · 🔴 **`Unexplored` 的 `Secret` 格不会出现在地图上**（`WalkMapView.FromTileWalk` 显式跳过）✓
        TuningSecretScatter? secretsCfg = built.Flow.Tuning.DungeonLayer?.Secrets;
        GD.Print($"[片4·D-6] ✅ 隐藏房已接线：{(secretsCfg is null ? "未配置（机制关闭：不撒、不揭示、不给）" : $"撒布 {secretsCfg.CorridorChancePercent}%/走廊格 · 回报 {secretsCfg.RewardGold} 金币/处 · 配额 {(secretsCfg.MaxRewardsPerRun <= 0 ? "不限" : secretsCfg.MaxRewardsPerRun.ToString())}")}　" +
                 $"本轮派生隐藏房格 = {built.Flow.TileWalk.SecretTiles}（🔴 这些格**未揭示前不会出现在地图上**）　" +
                 $"已揭示 = {built.Flow.SecretRevealedCount} 处 / {built.Flow.SecretGoldGranted} 金币 ✓");

        // 🔴🆕 **P0 养成闭环**（用户指令：把重心转到 Hamlet/养成）：进地牢时抓一份"出发前快照"并打印
        //    **本次 vs 上次** ⇒ 让"这趟比上趟强在哪"变成**可读**（不碰任何数值，纯只读）✓
        if (ExpeditionContext.Roster is { } snapRoster)
        {
            // 🔴🆕 **M15-P0（2026-10-02）**：把两个"只有这里有"的值传给快照 ——
            //    · `sortieIds` = 本趟出征名单（**英雄 id**；来自组合根产物，非 `Retained` 的战斗单位 id）✓
            //    · `hpPercentLastRunEnd` = 上一趟收尾的队伍 HP%（`ExpeditionContext.End()` 已缓存；
            //      此时 `PreviousSession` 已被组合根 `ConsumePreviousSession()` 认领清空 ⇒ 只能从这里读）✓
            foreach (string line in ExpeditionContext.CaptureRunStartAndDiff(
                snapRoster, ExpeditionContext.Heirlooms, ExpeditionContext.Gold,
                sortieIds: built.SortieIds,
                hpPercentLastRunEnd: ExpeditionContext.LastRunEndHpPercent))
            {
                GD.Print(line);
            }
        }
        // 🔴 `#352`：**把【放弃远征】入口注入 UI**（UI 已实现 `SetAbandonAction` + 二次确认，**但无人调用 ⇒ 按钮永不显示** ⚠️）
        //    · 语义：**放弃远征 = 【一趟】的选择**（结束本趟 ⇒ 回城·未完成）
        //    · 与"撤退"（【一场】的选择）**分开**（红线 19：措辞不得混淆）✓
        _ui.SetAbandonAction(() =>
        {
            if (ExpeditionContext.Flow is { } abandonFlow)
            {
                GD.Print("[片4] 🚪 **放弃远征**（地图层动作：结束本趟 ⇒ 回城·未完成）✓");
                abandonFlow.Abandon("player_map_action");
                DungeonRunDriver.ReturnToTown(this, abandonFlow, result: "abandoned");
            }
        });
        GD.Print("[片4] ✅ 【放弃远征】入口已注入 UI（`SetAbandonAction`）⇒ 按钮现在会显示 ✓");
        _ui.EnterMapMode();
        GD.Print($"[片4] ✅ **场景内进入地牢**（地图模式）：Phase={built.Flow.Session.Phase}　" +
                 $"段数={built.Flow.StepsDone}　光照={built.Flow.Meter.Value}　" +
                 // 🔴 修正一句"会撒谎的打印"（实测抓到）：**复用一趟时可能停在 `Battle` 相位**（战斗还没打完）
                 //    ⇒ 那时 `CanShowPathChoice=False` **本来就是对的**，而原句却硬写"应为 True" ⚠️
                 //    ⇒ 改成按相位给期望：`Walking/Camp` 才期望 True ✓（不再用一句固定期望覆盖两种真相）
                 $"CanShowPathChoice={built.Flow.Session.CanShowPathChoice}" +
                 $"（相位 {built.Flow.Session.Phase} ⇒ 期望 {(built.Flow.Session.Phase is Darkest.Gameplay.Sim.Run.FlowPhase.Walking
                     or Darkest.Gameplay.Sim.Run.FlowPhase.Camp ? "True" : "False")}）✓");
        return true;
    }

    private void GoToHamlet()
    {
        const string path = "res://scenes/hamlet/Hamlet.tscn";
        Darkest.UI.UIRoot? shell = Darkest.UI.UIRoot.Instance;
        if (shell is not null && shell.ShowPanel<Darkest.UI.HamletRoot>(path) is not null)
        {
            // 🆕 **B-3 接线**：成功面板化 ⇒ 记一笔 `Panel`（账本因此能回答"还差哪几屏"✓）
            Darkest.Data.UiShellLedger.Record("hamlet", Darkest.Data.UiShellLedger.ShellRoute.Panel);
            GD.Print("[BattleRoot] 回城 ⇒ 走 UIRoot 单外壳（ShowPanel<HamletRoot>）✓　"
                + Darkest.Data.UiShellLedger.Report());
            return;
        }

        // 🆕 **B-3 接线**：**回落必须留痕**（架构原话）—— 记 `SceneFallback` 并打印账本 ✓
        Darkest.Data.UiShellLedger.Record("hamlet", Darkest.Data.UiShellLedger.ShellRoute.SceneFallback);
        GD.Print("[BattleRoot] 回城 ⇒ 无 UIRoot 外壳（回落：ChangeSceneToFile）✓　"
            + Darkest.Data.UiShellLedger.Report());
        GetTree().CallDeferred("change_scene_to_file", path);
    }
}
