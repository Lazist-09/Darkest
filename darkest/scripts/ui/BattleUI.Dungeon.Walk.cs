using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Godot;


namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓


/// <summary>
/// ① 从 `BattleUI.Dungeon.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求**按相位切**）✓
/// ② 本文件 = **地图模式 · 行走相位 HUD 与示意地图**（`HostDungeonWalkMap`/`HostDungeonWalkHud`：全部只读内核
///    读数 —— 下一跳／到终点／已揭示／光照档／路线光照预算 ＋ 展示值==事件流复算的自证）✓
/// ③ 🔴 依赖主类私有成员/状态：本片持有 `_walkHud`/`_lastWalkHud`/`_walkMap`/`_lastWalkMapSketch`；
///    调核心片 `DungeonHost()`（宿主）；读数走内核 `ExpeditionFlow` 与 `LightMeter.Recompute` ✓
/// ④ **只搬家、零行为改动**（一字未改；两方法现为**暂撤**状态，调用点仍注释在核心片 `HostDungeonPanels`）✓
/// </summary>
public partial class BattleUI : Control
{

    /// <summary>🔴 行走模式 HUD（`#327` 层②④）：下一跳 / 到终点距离 / 已揭示 / 光照档 —— **全部只读内核读数** ✓</summary>
    private Label? _walkHud;
    private string _lastWalkHud = string.Empty;

    /// <summary>🔴 层④：**DD 式示意地图**（大方块=房间 / 小方块=走廊）—— 数据全只读内核 ✓</summary>
    private Darkest.UI.WalkMapView? _walkMap;
    private string _lastWalkMapSketch = string.Empty;

    /// <summary>层④：把示意地图挂进地图模式（非地图模式 ⇒ 不建/隐藏，如实空态）✓</summary>
    private void HostDungeonWalkMap(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        var map = flow.Map;
        if (!flow.IsTopologyMode || map is null)
        {
            if (_walkMap is not null && GodotObject.IsInstanceValid(_walkMap))
            {
                _walkMap.Visible = false;
            }

            return;
        }

        if (_walkMap is null || !GodotObject.IsInstanceValid(_walkMap))
        {
            _walkMap = new Darkest.UI.WalkMapView { Name = "MapModeWalkMap" };
            DungeonHost().AddChild(_walkMap);
        }

        _walkMap.Visible = true;
        _walkMap.Refresh(map, flow.CurrentRoomId, flow.RevealedRoomIds);

        // 布局自证：headless 看不到画面 ⇒ 用**文字速写**证明位置与标记（红线 25：不是"看起来像"）✓
        if (_walkMap.LastSketch != _lastWalkMapSketch)
        {
            _lastWalkMapSketch = _walkMap.LastSketch;
            GD.Print($"[UI 行走地图] {_lastWalkMapSketch}");
        }
    }

    /// <summary>
    /// 🔴 **行走模式 HUD**（DD 式层②④ 的最小可用版）：**只读**内核读数，不新增机制、不自己算规则：
    /// `flow.CurrentRoomId` / `Map.Rooms`（Id/Depth/Type）· `flow.NextRoomToward(GoalId)` + `MapTraversal.IsAdjacent`
    /// · `MapTraversal.ShortestPathLength`（**内核算，不是我算**）· `flow.RevealedRoomIds` · `flow.Meter.Value/.Tier` ✓
    /// ⚠️ 非地图模式 / 无地图 ⇒ **如实空态**（红线 21：不留不可解释的空）；内容变化时**打印一行自证** ✓
    /// </summary>
    private void HostDungeonWalkHud(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        if (_walkHud is null || !GodotObject.IsInstanceValid(_walkHud))
        {
            _walkHud = new Label
            {
                Name = "MapModeWalkHud",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _walkHud.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextInfo);
            DungeonHost().AddChild(_walkHud);
        }

        var map = flow.Map;
        if (!flow.IsTopologyMode || map is null)
        {
            _walkHud.Text = "行走 HUD：非地图模式（`IsTopologyMode=false` 或无 Map）⇒ 不适用";
        }
        else
        {
            int cur = flow.CurrentRoomId;
            string curType = map.Rooms.FirstOrDefault(r => r.Id == cur)?.Type ?? "?";
            int next = flow.NextRoomToward(map.GoalId); // -1 = 不可达 / 已到终点
            string nextText = next < 0
                ? "（不可达 / 已到终点）"
                : $"→ 房间 {next}（{map.Rooms.FirstOrDefault(r => r.Id == next)?.Type ?? "?"}" +
                  $"／相邻={Darkest.Gameplay.Sim.Run.MapTraversal.IsAdjacent(map, cur, next)}）";
            int remain = cur == map.GoalId ? 0 : Darkest.Gameplay.Sim.Run.MapTraversal.ShortestPathLength(map, cur, map.GoalId);
            // 🔴 **路线光照预算**（`DungeonWalkLight.PlanPath` 的生产消费点，2026-09-20 接线）——
            //    显示"按当前路线走到终点要花掉多少光"；走格未开启 / 不可达 ⇒ 如实标不可预测 ✓
            int target = map.GoalId;
            int? forecast = flow.ForecastLightTo(target);
            string forecastText = forecast is { } f
                ? $"需扣 {f} 光（现 {flow.Meter.Value} ⇒ 预计剩 {System.Math.Max(0, flow.Meter.Value - f)}）"
                : "（不可预测：走格未开启 / 不可达）";
            _walkHud.Text = $"[行走] 当前 房间 {cur}（{curType}）　下一跳 {nextText}　到终点 {remain} 间" +
                            $"　已揭示 {flow.RevealedRoomIds.Count}/{map.Rooms.Count}　光照 {flow.Meter.Value}（{flow.Meter.Tier}）" +
                            $"　路线预算：{forecastText}";
        }

        // 🔴 **展示值 == 事件流复算值**（纪律 V）—— `LightMeter.Recompute` 的**生产调用点**：
        //    光照条显示的是 `Meter.Value`（内存值），而它**必须**能从 `LightChangedEvent` 流复算出来；
        //    两者不等 ⇒ 说明"有变化没写事件"（真缺陷）⇒ **当场如实报**（不静默、不自动修正）⚠️
        Darkest.Core.Events.CombatLog? meterLog = Darkest.Gameplay.Scene.ExpeditionContext.Log;
        if (meterLog is not null)
        {
            int recomputed = Darkest.Gameplay.Sim.Survival.LightMeter.Recompute(meterLog, flow.Meter.Value);
            // ⚠️ 口径：`Recompute(log, enterValue)` 取**最后一条** `LightChangedEvent.To`；
            //    无事件 ⇒ 返回 `enterValue`（此处传当前值 ⇒ 恒等）。因此本检查的语义 = "**若事件流非空，
            //    其末值必须等于内存值**"（不是"必然相等" —— 无事件时按设计返回入参）✓
            bool hasEvent = meterLog.Events.OfType<Darkest.Core.Events.LightChangedEvent>().Any();
            if (hasEvent && recomputed != flow.Meter.Value)
            {
                GD.Print($"🔴 [UI 光照校验] **展示值 {flow.Meter.Value} ≠ 事件流复算 {recomputed}** ⇒ " +
                         "有光照变化**没写 `LightChangedEvent`**（纪律 V 违反：数字必须能从事件流复算）⚠️");
            }
        }

        if (_walkHud.Text != _lastWalkHud)
        {
            _lastWalkHud = _walkHud.Text;
            GD.Print($"[UI 行走HUD] {_walkHud.Text}");
        }
    }
}
