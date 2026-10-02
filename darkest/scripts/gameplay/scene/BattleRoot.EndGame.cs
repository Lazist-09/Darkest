// 🔴 从 BattleRoot.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3：只抽【战斗结算】）——只搬家、零行为改动 ✓
//    本文件 = 一场战斗结束时的落账与呈现：远征回灌 · 升级通道 · 撤退/继续按钮 · 结算面板数据
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_gameOver x1 · _awaitingPlayer x1 · _ui x2 · _xpLog x1
//      · _rosterCfgForXp x2 · _dungeonHostedInScene x1 · _autoContinue x1（冒烟片声明）
//    【依赖主类公有面】Director · ResultText / ResultRound / ResultCounts（主文件声明）

using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Board;
using Godot;

namespace Darkest.Gameplay.Scene;

public partial class BattleRoot
{
    private void EndGame(string what)
    {
        _gameOver = true;
        _awaitingPlayer = false;
        ResultText = what;
        ResultRound = Director.Round;

        // 🔴 M7.5：远征模式 ⇒ 回灌结果 + 显示【继续（回远征）】按钮（**结算面板照常显示**，不再直接切场景）
        if (ExpeditionContext.IsActive)
        {
            // 🔴 `O-83`：**战后落账**（本场结束血量 → 跨趟台账）—— 必须在回灌/切场景**之前**：
            //    下一场开局要读它；切场景后本场景（及 `Director`）就可能被释放 ✓
            ExpeditionContext.Flow!.CaptureBattleEndHp(Director);

            string result = what.Contains("撤退", System.StringComparison.Ordinal)
                ? "DrawRetreat"
                : Director.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";

            // 🔴🆕 **升级通道接线**（契约 `hamlet.md` §7.2/§7.6「**战斗给经验 ⇒ 等级成长**」·
            //    策划 `#399` 给了**占位数值**）⇒ 战斗结算就给全队发经验 ✓
            //    纪律：**数值全部来自 `roster.json`**（缺省 ⇒ 显式不生效 ✓ 不假装）· **事件留在常驻日志里**（可审计 ✓）
            if (ExpeditionContext.Roster is { } xpRoster && xpRoster.ExperienceWired)
            {
                bool won = result == "PlayerVictory";
                _xpLog ??= new Darkest.Core.Events.CombatLog();
                xpRoster.AwardExperienceForBattle(_xpLog, won, "battle");
                GD.Print("[升级通道] 本场" + (won ? "胜" : "负") + " ⇒ 发经验：" +
                         string.Join("、", System.Linq.Enumerable.Select(xpRoster.Heroes,
                             h => $"{h.Name} Lv{h.Level}(XP{xpRoster.ExperienceOf(h.Id)})")) + " ✓");

                // 🔴 **A10 可读性读数**（策划 `#399`；`RosterConfig.BattlesToNextLevel` 的**生产消费点**，2026-09-20 接线）——
                //    玩家感受到的是"**我打了 N 场，升了 1 级**" ⇒ 这句把"还差几场"直接说出来 ✓
                //    此前该读法**只被测试调用** ⇒ "升级通道有没有意义"这个判据**在游戏里看不见** ⚠️
                // 🔴 读法归属：`BattlesToNextLevel` / `CostToNextLevel` 定义在 **`RosterExperience`** 上
                //    （`RosterConfig` 只有 `LevelGrowth` / `Experience` 两个成员）⇒ 必须经 `cfg.Experience?.` 进入 ✓
                Darkest.Data.RosterConfig cfg = _rosterCfgForXp ??= Darkest.Data.RosterConfig.Parse(
                    Godot.FileAccess.GetFileAsString(Darkest.Data.RosterConfig.ResPath));
                Darkest.Data.RosterExperience? xp = cfg.Experience;
                GD.Print("　[A10 升级读数] " + string.Join("、", System.Linq.Enumerable.Select(xpRoster.Heroes, h =>
                {
                    int? battles = xp?.BattlesToNextLevel(h.Level, cfg.LevelMin, cfg.LevelMax);
                    return battles is { } n ? $"{h.Name} 再 {n} 场升 Lv{h.Level + 1}" : $"{h.Name} 已满级";
                })) + " ✓");
            }

            // 🔴 `#352` 打印自证（`retreat.md §11` 要的"撤退 ⇒ 回地图当前格"）：这一行让"撤没撤对"**可读** ✓
            if (result == "DrawRetreat")
            {
                GD.Print($"[片4] ↩️ **撤退**（【一场】的选择）⇒ 回地图当前格**继续走**（本趟**不**结束）✓" +
                         $"　IsFinished={ExpeditionContext.Flow?.IsFinished}（应为 False）" +
                         $"　Outcome={ExpeditionContext.Flow?.Outcome}（应为 InProgress）✓");
            }
            // 🔴 修 UI 报的 4 条：**流程层的 `OnBattleFinished` 只接受【当前步骤是战斗节点】时调用** ——
            //    冒烟的 `auto` 可能在"非战斗步骤"上触发战斗结束（宿主内进地牢的步骤对齐还没做完）⇒ 那会被流程**如实拒绝** ✓
            //    ⇒ 这里**先判**，不把非法调用递进去（红线 21：不静默、也不假装）✓
            // 🔴 判据修正（实测教训）：**拓扑模式下 `flow.Current` 恒为 null**（`BeginTopology` 只设 `_currentRoomId`）
            //    ⇒ 原先按 `Current.Kind` 判 ⇒ 恒假 ⇒ 战斗结算被静默跳过 ⚠️
            //    ⇒ 正解：**按当前房间类型**判（拓扑）＋ 兼容线性步（`Current.Kind`）✓
            bool flowExpectsBattle =
                ExpeditionContext.Flow!.Current?.Kind == Darkest.Gameplay.Sim.Run.FlowStepKind.Battle
                || ExpeditionContext.Flow.CurrentRoomType == "battle";
            if (!flowExpectsBattle)
            {
                GD.Print($"[片4] ⚠️ 战斗结束但**当前步骤不是战斗节点**（{ExpeditionContext.Flow.Current?.Kind}）"
                         + "⇒ 不调用 `OnBattleFinished`（否则流程如实抛错）；这属【宿主内进地牢的步骤对齐未完成】✓");
            }

            if (flowExpectsBattle)
            {
            ExpeditionContext.Flow!.OnBattleFinished(result, Director.Round,
                isAmbush: ExpeditionContext.ConsumePendingAmbush()); // 🔴 #307③：夜袭战斗不是节点步骤

            var toExpedition = new Button
            {
                Name = "ReturnToExpedition",
                Text = "继续（回地图）", // 🔴 片 4②：不再是"回远征**场景**"，而是**回地图模式**（同一场景）✓
                Position = new Vector2(540, 660),
                Size = new Vector2(200, 40),
            };
            // 🔴 片 4②：战后**留在本场景、切回地图模式**（由宿主继续驱动流程；不再切场景、也不再重跑组装）✓
            toExpedition.Pressed += () =>
            {
                if (_dungeonHostedInScene)
                {
                    // 🔴 片 4②：**宿主内进的**地牢 ⇒ 战后**留在本场景、切回地图模式**（不切场景、不重跑组装）✓
                    _ui.EnterMapMode();
                    GD.Print($"[片4] ✅ 战后回地图模式（同一场景）：Phase={ExpeditionContext.Flow!.Session.Phase}　" +
                             $"CanShowPathChoice={ExpeditionContext.Flow.Session.CanShowPathChoice}（应为 True）✓");
                    // 🔴 片 4 收口修：**同场景内**的模式切换**没有新的 `_Ready`** ⇒ 冒烟待办步骤要在此**续跑**
                    //    （实测：`--smoke=main:1,town` 的第 2 步 `town` 曾永远停着 ⚠️）
                    if (SmokeScript.HasPending)
                    {
                        SmokeScript.Step(this);
                    }
                }
                // 🔴 片 4 收尾：**旧场景已退休** ⇒ 两条路都回**同一场景的地图模式**（过渡分支删除 ✓）
                _ui.EnterMapMode();
            };

            AddChild(toExpedition);
            GD.Print($"[BattleRoot] 远征模式：本场结果 {result}，点【继续（回地图）】回地图模式继续走图");

            // 🔴 冒烟：自动点【继续（回远征）】（**真实 `Pressed`** ⇒ 走玩家路径）
            //    ⚠️ 两条入口都要认：① 命令行旗标（旧路径）② `PressAutoFinish()`（步进冒烟的新路径）
            //    —— 此前只认旗标 ⇒ 步进冒烟不带旗标时，"战斗 ⇒ 返回"这一段**走不完**（我实测踩过）。
            if (_autoContinue || System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-auto-finish"))
            {
                GD.Print("[BattleRoot] 自动点【继续（回远征）】（真实 Pressed）");
                toExpedition.EmitSignal(BaseButton.SignalName.Pressed);
            }

            } // ← 收 `if (flowExpectsBattle)`（🔴 修 UI 报的 4 条：非战斗步骤**不递非法调用**给流程）✓
        }
        // T-M6-07 系统触发日志 + P0④ 结算面板数据（同一批计数）
        var events = Director.Log.Events;
        int collapse = events.OfType<CollapseResultEvent>().Count();
        int weak = events.OfType<WeakEnterEvent>().Count();
        int dd = events.OfType<DeathDoorEvent>().Count();
        int retreat = events.OfType<RetreatEvent>().Count();
        int virtue = events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Virtue");
        int aff = events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Affliction");
        int disp = events.OfType<DisplaceEvent>().Count();
        ResultCounts = new[] { collapse, weak, dd, retreat, virtue, aff, disp };
        GD.Print($"[BattleRoot] 战斗结束：{what}（第 {Director.Round} 回合）。系统触发：士气触底 {collapse} / 虚弱 {weak} / 死门 {dd} / " +
                 $"撤退出现 {retreat} / 美德 {virtue} / 折磨 {aff} / 位移 {disp}。按 R 重开。");
    }

    private UnitRuntime? FindPlayerUnit(UnitId id)
        => Director.Player.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id);
}
