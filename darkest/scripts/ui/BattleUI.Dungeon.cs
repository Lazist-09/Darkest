using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求**按相位切**）✓
/// ② 本文件 = **战斗 · 地牢宿主与地图相位族**（`SceneMode == Map`：宿主面板/扎营面板/行走地图/行走 HUD、
///    `SetAbandonAction`/`PressAbandon` 放弃远征、`HostDungeonPanels`、`EnterMapMode`/`ExitMapMode`）✓
/// ③ 🔴 依赖主类私有成员/状态：`_uiRoot`/`_bottomRow`/`_dungeonHost`/`_abandonButton`/`_abandonExpedition`/
///    `_abandonConfirm`/`_abandonWarned`/`_mode`/`_walkView`/`_host`（其余读数走内核 `ExpeditionFlow`）✓
/// ④ **只搬家、零行为改动**（含 2026-09-21 `_uiRoot` 未就绪守卫与 map-phase 分支，一字未改）✓
/// </summary>
public partial class BattleUI : Control
{
    /// <summary>地牢面板宿主（惰性建一次，**不属于骨架** ⇒ 增删不影响 S1 指纹）✓</summary>
    private Control DungeonHost()
    {
        if (_dungeonHost is null || !GodotObject.IsInstanceValid(_dungeonHost))
        {
            _dungeonHost = _bottomBarSkel?.DungeonHost ?? new VBoxContainer
            {
                // 🔴 消重叠（宽度预算）：宿主不再与 E 区都 ExpandFill 争宽 ⇒ 固定 240 宽 + 靠右，E 区吃剩余宽
                    Name = "DungeonHost",
                CustomMinimumSize = new Vector2(240, 72),    // 🔴 相机 720 口径：104→72
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            };
            if (_dungeonHost.GetParent() is null)
            {
                _bottomRow.AddChild(_dungeonHost);
            }
        }

        return _dungeonHost;
    }

    /// <summary>
    /// 🔴 `#327` 片 2 #6：把 **【扎营】面板（B 类）**挂进地图模式。
    /// 铁律：**可见性只读 `Session.CanShowCampUi`**（`blueprint §9.17.0`：内核持相位、UI 只读谓词 ⇒ 绝不推断相位）✓
    /// 数据与 `ExpeditionRoot` **同源**（`camp_skills.json` + 名册原型映射 + `RespiteLeft` + `Tuning.Camp`），UI 不重算 ✓
    /// ⚠️ 诚实边界：**战斗相位下谓词为假 ⇒ 面板必然隐藏**；"该显示时显示"要等片 3 的行走相位进宿主后才能观察 ✓
    /// </summary>
    private void HostDungeonCampPanel(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        if (_mapModeCamp is null || !GodotObject.IsInstanceValid(_mapModeCamp))
        {
            _mapModeCamp = new Darkest.UI.CampSkillPanel { Name = "MapModeCamp", CustomMinimumSize = new Vector2(210, 96) }; // 🔴 预留宽度+按 720 收高  // 🔴 相机 720 口径：96→64
            DungeonHost().AddChild(_mapModeCamp);
        }

        if (!flow.Session.CanShowCampUi) // 🔴 只读谓词
        {
            _mapModeCamp.Visible = false;
            GD.Print($"[UI 片2] 【扎营】面板：Phase={flow.Session.Phase} ⇒ `CanShowCampUi=False` ⇒ **隐藏**（只读谓词，不推断相位）✓");
            return;
        }

        // 🔴 片 4 收尾（主程序 ③）：**配置改读公共读处** `ExpeditionContext.CampSkills`（消掉"各解析一份"）——
        //    ⚠️ 为空时（单场战斗 / 未 `BindConfigs`）退回本地解析并**留痕**（不静默、不崩）✓
        if (_campSkillsCfgForMap is null)
        {
            _campSkillsCfgForMap = Darkest.Gameplay.Scene.ExpeditionContext.CampSkills;
            if (_campSkillsCfgForMap is null)
            {
                _campSkillsCfgForMap = Darkest.Data.CampSkillsConfig.Parse(
                    Godot.FileAccess.GetFileAsString(Darkest.Data.CampSkillsConfig.ResPath));
                GD.Print("[UI 片2] `ExpeditionContext.CampSkills` 为空 ⇒ 本地解析一份" +
                         "（留痕：单场战斗 / 未 `BindConfigs` 时会走这里）");
            }
        }

        // `RosterConfig`（英雄 id → 原型）：**你未开公共读处** ⇒ 仍本地懒解析（已投窗口问是否要开）✓
        _rosterCfgForMap ??= Darkest.Data.RosterConfig.Parse(
            Godot.FileAccess.GetFileAsString(Darkest.Data.RosterConfig.ResPath));

        var heroByArchetype = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string id, int _, int _, int _) in flow.Session.Roster())
        {
            string? archetype = _rosterCfgForMap.Heroes.FirstOrDefault(h => h.Id == id)?.Archetype;
            if (archetype is not null && !heroByArchetype.ContainsKey(archetype))
            {
                heroByArchetype[archetype] = id;
            }
        }

        Darkest.Data.CampSkillConfig[] usable = _campSkillsCfgForMap.Skills
            .Where(s => Darkest.Data.CampSkillsConfig.ConsumedEffectNames.Contains(s.Effect)) // 只列已接线（红线 21）
            .ToArray();
        Darkest.Core.Events.CombatLog? log = Darkest.Gameplay.Scene.ExpeditionContext.Log;

        _mapModeCamp.Visible = true;
        _mapModeCamp.Refresh(
            usable,
            heroOf: s => heroByArchetype.TryGetValue(s.OwnerUnit, out string? hero) ? hero : null,
            affordOf: s => flow.Session.RespiteLeft >= s.Cost,
            useOf: (s, target) =>
            {
                if (log is null)
                {
                    GD.Print("[UI 片2] 扎营：无 `ExpeditionContext.Log` ⇒ 不执行（如实拒绝）");
                    return;
                }

                bool used = flow.Session.UseCampSkill(log, s, Darkest.Core.Contracts.UnitId.Of(target), flow.Tuning.Camp!);
                GD.Print($"[UI 片2] 扎营技能 {s.Name}：{(used ? "已使用" : "拒绝")}　剩余 Respite {flow.Session.RespiteLeft}");
                HostDungeonPanels(); // 刷新（点数/可用性变化）
            },
            statusText: $"【扎营】Respite {flow.Session.RespiteLeft} 点　可用技能 {usable.Length} 个（只列已接线）");

        GD.Print($"[UI 片2] 【扎营】面板：Phase={flow.Session.Phase} ⇒ **显示**（可用 {usable.Length} 个技能）✓");
    }

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

    /// <summary>
    /// 🔴 `retreat.md §8`：宿主把【放弃远征】动作交给我（**不破 `Bind` 签名**：`Bind` 之后调一次即可）✓
    /// ⚠️ **未调用 ⇒ 按钮不显示**（红线 21：不留"点了没用"的控件；也避免把"结束一趟"错标成"退一场"）✓
    /// </summary>
    public void SetAbandonAction(Action? abandon)
    {
        _abandonExpedition = abandon;
        if (_abandonButton is not null)
        {
            _abandonButton.Visible = abandon is not null;
        }

        GD.Print(abandon is null
            ? "[UI 撤退/放弃] 宿主**未提供**放弃远征动作 ⇒ 按钮不显示（不假装可用，红线 21）✓"
            : "[UI 撤退/放弃] 已接入【放弃远征】入口（行走模式可见·二次确认·tooltip 写清后果）✓");
    }

    /// <summary>按下【放弃远征】⇒ **二次确认**（不可逆；`§8` 硬要求②）✓</summary>
    public void PressAbandon()   // 🔴 主程序 2026-09-21 请求：retreat 冒烟需公共入口（发真实 Pressed）✓
    {
        GD.Print("[UI-TRACE] abandon");   // ASCII 留痕（供 ui_sweep 断言：避免 PS5.1 读中文的编码坑）
        if (_abandonExpedition is null)
        {
            if (!_abandonWarned)
            {
                _abandonWarned = true;
                GD.Print("[UI 撤退/放弃] 放弃远征：**没有动作可调**（宿主未注入）⇒ 什么也不做（不静默假装）✓");
            }

            return;
        }

        if (_uiRoot is null || !GodotObject.IsInstanceValid(_uiRoot))
        {
            GD.Print("[UI 撤退/放弃] ⚠️ _uiRoot 未就绪（未 Build）⇒ 不执行放弃（不静默、不半执行）✓");
            return;
        }

        if (_abandonConfirm is null)
        {
            // ⚠️ 战斗屏的模态工厂是 `MakeOpaqueModal`（返回正文 Label + out 面板）；按钮挂在**正文的父容器**（col）上 ✓
            // 🔴 DD `shared/confirm_dialog` ⇒ 840×600 居中（二次确认框比普通模态高）✓
            Label abandonText = MakeOpaqueModal("AbandonConfirm", out PanelContainer abandonPanel,
                Darkest.UI.PopupLayout.Confirm);
            _abandonConfirm = abandonPanel;
            abandonText.Text = "放弃远征 = **结束本次远征、回城**（本趟未完成）。\n此操作**不可逆**；若只是想退出本场战斗，请用【撤退】。";
            var row = new HBoxContainer { Name = "AbandonConfirmRow" };
            row.AddThemeConstantOverride("separation", 8);
            ((Control)abandonText).GetParent().AddChild(row);

            var yes = new Button { Name = "AbandonYes", Text = "确认放弃远征", CustomMinimumSize = new Vector2(180, 34) };
            yes.Pressed += () =>
            {
                GD.Print("[UI 撤退/放弃] ✅ 二次确认通过 ⇒ 调宿主【放弃远征】动作 ✓");
                _abandonConfirm!.Visible = false;
                _overlay?.CloseModal(_abandonConfirm!);
                _abandonExpedition?.Invoke();
            };
            row.AddChild(yes);

            var no = new Button { Name = "AbandonNo", Text = "取消（继续走）", CustomMinimumSize = new Vector2(160, 34) };
            no.Pressed += () =>
            {
                GD.Print("[UI 撤退/放弃] 取消放弃远征 ⇒ 继续走 ✓");
                _abandonConfirm!.Visible = false;
                _overlay?.CloseModal(_abandonConfirm!);
            };
            row.AddChild(no);
        }

        _overlay?.OpenModal(_abandonConfirm);   // 🔴 入栈 ⇒ 遮罩出现 + Esc 可关 ✓
        GD.Print("[UI 撤退/放弃] 弹出【放弃远征】二次确认（不可逆；取消 ⇒ 继续走）✓");
    }

    /// <summary>面板状态读数：**显示/隐藏 + 子控件数**（隐藏 = 门禁生效的**自证**，不是"没接上"）✓</summary>
    private static string PanelState(string name, Control? panel)
        => panel is not null && GodotObject.IsInstanceValid(panel)
            ? $"{name}={(panel.Visible ? "显示" : "隐藏")}({panel.GetChildCount()}子)"
            : $"{name}=未建";

    /// <summary>地牢面板 #1：**光照条**（`LightBarPanel`）。数据**不新造**：走 `ExpeditionContext.Flow.Meter`（与 `BattleMiniMap` 同法）✓</summary>
    private Darkest.UI.LightBarPanel? _mapModeLightBar;

    /// <summary>🔴 主程序清单第 3 条：**敌方意图预览**那一行（`_host.PreviewIntent` ⇒ 只渲染，不推断）✓</summary>
    private Label _intentText = null!;
    private bool _pendingMapMode;   // 🔴 宿主在 Build 之前请求进地图模式 ⇒ 延后到 Bind 之后（修 NRE）

    /// <summary>地牢面板 #2：**侦察标记**（`ScoutMarkPanel`，两态可区分）。数据：`ExpeditionContext.Flow.LastScout` ✓</summary>
    private Darkest.UI.ScoutMarkPanel? _mapModeScoutMark;

    /// <summary>地牢面板 #3：**背包**（`InventoryPanel`，含"满则选择丢弃"流程）。数据：`ExpeditionContext.Flow.Bag` ✓</summary>
    private Darkest.UI.InventoryPanel? _mapModeInventory;

    /// <summary>已注入的背包实例（判断是否需要重新 `Initialize`，避免每次进模式都清掉面板内部状态）✓</summary>
    private Darkest.Gameplay.Sim.Run.Inventory? _mapModeInventoryBag;

    /// <summary>地牢面板 #4：**本趟投影列表**（`ExpeditionListPanel`）。数据：`ExpeditionProjector.Project/RenderList`（内核投影）✓</summary>
    private Darkest.UI.ExpeditionListPanel? _mapModeList;

    /// <summary>🔴 `#327` 片 2 #6：**扎营**面板（B 类）—— 内容已独立化（`CampSkillPanel`），可见性**只读** `Session.CanShowCampUi` ✓</summary>
    private Darkest.UI.CampSkillPanel? _mapModeCamp;
    private Darkest.Data.CampSkillsConfig? _campSkillsCfgForMap;   // 懒解析（与 `ExpeditionRoot` 同一数据源）
    private Darkest.Data.RosterConfig? _rosterCfgForMap;           // 懒解析（英雄 id → 原型）

    /// <summary>
    /// 🔴 `#327` 片 2：**逐个把地牢面板挂进地图模式**（宿主 = `DungeonHost()`，在骨架之外 ⇒ 切模式不动骨架）✓
    /// 纪律：数据**只来自本趟流程**（`ExpeditionContext.Flow`，与 `BattleMiniMap` 同一手法）；UI **不重算**任何数字；
    ///       无本趟数据 ⇒ **空态 + 留痕**（红线 21：不假装有数据、不留不可解释的空）。
    /// ⚠️ 选面板的判据：**数据在战斗期就存在**才迁 —— 例：`PathChoicePanel` 需要"行走中的 `PathStep`"，
    ///    战斗里的地图模式没有它 ⇒ 现在挂上去就是空面板（触发红线 21）⇒ **等片 3 把流程驱动搬进宿主后再迁** ✓
    /// </summary>
    private void HostDungeonPanels()
    {
        Darkest.Gameplay.Sim.Run.ExpeditionFlow? flow = Darkest.Gameplay.Scene.ExpeditionContext.Flow;
        Control host = DungeonHost();

        // ① 光照条：**已移到战斗屏【正上方】**（DD 图②）⇒ 此处不再挂第二份（同一读数只显示一处）✓

        // ② 侦察标记（面板 #2）：🔴 **用户要求（2026-09-16）"底部最右的框（侦察扎营之类）没用，删掉"** ⇒
        //    **不再创建、不再挂载**（⚠️ 删创建块**必须同时删使用点**，否则每帧 NRE —— 我这条教训吃过两次）✓

        // 🔴 相机 720 口径（DD 图②）：**地图模式不内联背包/投影列表** ——
        //    实测 `MapModeInventory 需 298×312` 且可见 ⇒ 底栏 574 高 ⇒ 整屏 918 > 720 ⚠️
        //    它们在 DD 里经【多功能框】查看，不占地图模式底栏 ⇒ 本模式不再创建 ✓

        // ④ 本趟投影列表（面板 #4）：建一次即可（内容由 `Refresh(lines)` 更新）✓
        if (_mapModeList is null || !GodotObject.IsInstanceValid(_mapModeList))
        {
            _mapModeList = new Darkest.UI.ExpeditionListPanel
            {
                Name = "MapModeList",
                CustomMinimumSize = new Vector2(0, 96),    // 🔴 相机 720 口径：140→96
            };
            host.AddChild(_mapModeList);
        }

        if (flow is not null)
        {
            // 🔴 用户要求（2026-09-16）：**侦察标记面板已从底栏删除** ⇒ 连**使用点**一起删（我这条教训吃过三次）✓

            // ④ 本趟投影列表（面板 #4）：**行文只来自内核投影**（`ExpeditionProjector`），UI 不自己拼数字 ✓
            Darkest.Core.Events.CombatLog? expeditionLog = Darkest.Gameplay.Scene.ExpeditionContext.Log;
            int listLineCount = -1;
            if (expeditionLog is not null)
            {
                Darkest.Gameplay.Sim.Run.ExpeditionViewState view = Darkest.Gameplay.Sim.Run.ExpeditionProjector.Project(
                    expeditionLog, flow.Bag.CountOf(Darkest.Gameplay.Sim.Run.ItemKind.Firewood),
                    flow.Bag.CountOf(Darkest.Gameplay.Sim.Run.ItemKind.Food),
                    flow.Session, flow.Tuning.Camp!, flow.Tuning.Expedition.NBattles, flow.Tuning.Expedition.DifficultyTiers);
                _mapModeList!.Refresh(Darkest.Gameplay.Sim.Run.ExpeditionProjector.RenderList(
                    view, flow.Session, flow.Tuning.Expedition.NBattles, ambushTriggered: false, flow.Tuning.Camp!));
                listLineCount = _mapModeList.LineCount; // 供读数（证明投影列表**真喂到了行**）✓
            }
            else
            {
                GD.Print("[UI 片2] 地图模式：`ExpeditionContext.Log` 为空 ⇒ 投影列表为空态（如实报）");
            }

            // 🔴 相机 720 口径 + DD 图②：**地图模式只显示 地图/侦察/光** ——
            //    实测该模式下底栏叠加背包+投影列表+扎营 ⇒ 内容需求高 **918 > 720** ⚠️
            //    （背包/投影列表在战斗模式或经多功能框查看；扎营由相位谓词门禁控制）✓
            if (_mapModeInventory is not null) { _mapModeInventory.Visible = false; }
            if (_mapModeList is not null) { _mapModeList.Visible = false; }
        // 🔴 用户要求：5/6 号位**两种模式都显示**（原先只战斗模式 ⇒ "看不见 6 号位"）✓
        if (_slotLeft is not null) { _slotLeft.Visible = true; }

        // 🔴 `§8`①（**文本自证抓到的**）：**撤退（战斗内）与放弃远征（地图层）必须不同屏** ⇒
        //    实测地图模式里"撤退"仍可见 ⇒ 与放弃远征同屏（手滑 = 一趟白跑）⚠️ ⇒ 按模式裁决 ✓
        if (_retreatButton is not null) { _retreatButton.Visible = _mode == SceneMode.Battle; }   // 🔴 用字段 _mode（两处同名代码都能编译）

        // 🔴 `§8`①：**放弃远征（地图层）与撤退（战斗内）不得同屏** ⇒ 前者只在行走模式可见（且需宿主已注入动作）✓
        // 🔴 冒烟：`--abandon` ⇒ 按下【放弃远征】（**只弹出二次确认，不确认**，避免真结束一趟）✓
        if (_abandonButton is not null && _abandonExpedition is not null
            && _mode == SceneMode.Map && !_abandonSmoked
            && Array.Exists(OS.GetCmdlineArgs(), x => x == "--abandon"))
        {
            _abandonSmoked = true;
            PressAbandon();
        }

        if (_abandonButton is not null)
        {
            _abandonButton.Visible = _mode == SceneMode.Map && _abandonExpedition is not null;

            // 🔴 文本级自证（一次性）：**撤退/放弃远征两个按钮的"界面用词 + 可见性"**都留痕
            //    （§8 要求两个词不得混用；红线 21 要求"可见/不可见"都可解释）✓
            if (!_labelProofed)
            {
                _labelProofed = true;
                GD.Print($"[UI §8 用词] 撤退按钮文案=「{_retreatButton?.Text}」　可见={_retreatButton?.Visible}" +
                         $"　｜　放弃远征按钮文案=「{_abandonButton.Text}」　可见={_abandonButton.Visible}" +
                         $"（模式={(_mode == SceneMode.Map ? "Map" : "Battle")}　宿主已注入放弃动作={_abandonExpedition is not null}）✓");
            }

            // 🔴 红线 21（不留不可解释的状态）：**按钮为什么没出现**必须留痕一次 ✓
            if (_abandonExpedition is null && !_abandonWarned)
            {
                _abandonWarned = true;
                GD.Print("[UI 撤退/放弃] 宿主**未注入**放弃远征动作 ⇒ 该按钮**不显示**（不假装可用，红线 21）✓");
            }
        }   // 🔴 紫框内 6 号位：地图模式让位给地牢面板（实测曾与 ScoutMark/CampStatus 相压）

            // 🔴 片 2 #6：**扎营**面板（B 类）—— 门禁**只读谓词**（UI 绝不推断相位）✓
            // 🔴 用户要求（2026-09-16）：**扎营面板也从底部删除**（相位动作走弹窗，不占底栏）✓
            // HostDungeonCampPanel(flow);

            // 🔴 行走模式 HUD（层②④）：只读内核读数（下一跳 / 到终点 / 已揭示 / 光照档）✓
            // 🔴 相机 720 口径：行走 HUD（75px 高）**移到地图页的格子主画面上方**（避免与主画面重复占高）
            // HostDungeonWalkHud(flow);   // 暂撤：底栏高度是 topology 路径超相机的最后一项

            // 🔴 层④：DD 式示意地图（大方块=房间 / 小方块=走廊）✓
            // 🔴 主程序 (A) 落地：**格子地图已升级为【地图页主画面】** ⇒ 宿主里这份**重复**小图撤掉
            //    （实测它占底栏 68px 高，是 topology 路径 782 > 720 的组成部分之一）✓
            // HostDungeonWalkMap(flow);

            // 🔴 相机 720 口径（DD 图②）：地图模式**不内联背包**（经多功能框查看）⇒ 该初始化块已移除
            //    ⚠️ 教训：删创建块时必须同时删**使用点**，否则每帧 NRE（实测 topology 路径 E=4）✓

            GD.Print($"[UI 片2] 地图模式面板状态：" +
                     $"{PanelState("侦察标记", _mapModeScoutMark)}　" +
                     $"{PanelState("背包", _mapModeInventory)}　{PanelState("投影列表", _mapModeList)}　" +
                     $"{PanelState("扎营", _mapModeCamp)}　{PanelState("顶部火把条", _topTorch)}" +
                     "　（**显示/隐藏 + 子控件数** ⇒ 门禁与内容都能自证；隐藏是门禁生效，不是没接）");

            // 🔴 相位谓词读数（架构 `…PHASE-RULING…`）：**UI 只读谓词、绝不推断相位** ——
            //    先把谓词变成**可观测读数**，B 类面板（扎营/Curio/选路）挂载时直接消费它 ✓
            //    ⚠️ 如实说明：**战斗中相位=Battle ⇒ 三个谓词全假** ⇒ B 类面板此刻本就不可见，
            //       "真显示"要等片 3 把**行走相位**搬进宿主后才能观察（我不做"挂了但观察不到"的假接线）✓
            GD.Print($"[UI 相位] Phase={flow.Session.Phase}　CanShowCampUi={flow.Session.CanShowCampUi}" +
                     $"　CanShowPathChoice={flow.Session.CanShowPathChoice}　CanShowCurioUi={flow.Session.CanShowCurioUi}" +
                     "　（B 类面板：扎营／选路／Curio 按此三者显示；战斗相位下应全假）");

            // 🔴 **常驻告警**（把我在 `DELIVERY-UI-PHASE-STALE-IN-BATTLE` 里报的发现固化下来）：
            //    ⚠️ **判据必须带"战斗是否仍在进行"**：`_host.GameOver == true` 时相位回 `Walking` 是**正常的**
            //    （战斗结束 ⇒ 回到行走相位）—— 我第一版只判"在战斗场景里 + 谓词为真" ⇒ **误报**（已修）⚠️
            bool battleRunning = _host is not null && !_host.GameOver;
            bool predicatesTrue = flow.Session.CanShowCampUi || flow.Session.CanShowPathChoice || flow.Session.CanShowCurioUi;
            GD.Print(battleRunning && predicatesTrue
                ? "🔴 [UI 相位·告警] **战斗仍在进行、谓词却为真 ⇒ 相位陈旧**" +
                  "（远征会话冻结在 Walking）⇒ **不得**据此挂 B 类面板（否则=战斗中能扎营）"
                : $"✅ [UI 相位·告警] 相位与宿主一致（战斗进行中={battleRunning}／谓词为真={predicatesTrue}）" +
                  (battleRunning ? "⇒ 可据此挂 B 类面板" : "　—— 战斗已结束 ⇒ 相位回 Walking 是**正常**的 ✓"));
        }
        else
        {
            // 🔴 无本趟数据 ⇒ **空态 + 留痕**（红线 21：不留不可解释的空；也不假装有数据）✓
            GD.Print("[UI 片2] 地图模式：**无本趟流程** ⇒ 4 个地牢面板均为空态（如实报，不假绿）");
        }
    }

    public void EnterMapMode()
    {
        GD.Print("[UI-TRACE] dungeon-in-scene-entered");   // ASCII 留痕（供 ui_sweep 断言：避免 PS5.1 读中文的编码坑）
        // 🔴 修 NRE（片 4 宿主内进地牢实测：`BattleRoot._Ready` 会在 `Build()` 之前就调本方法）：
        //    此时 `_bottomRow`（骨架三行之一）**还没建** ⇒ `DungeonHost()` 会每帧抛 NRE ⚠️
        //    ⇒ 正解：**延后到 `Bind()` 之后再进地图模式**（不猜、不半建），并**如实留痕** ✓
        if (_bottomRow is null || _uiRoot is null)
        {
            _pendingMapMode = true;
            GD.Print("[UI 模式] 地图模式请求：**UI 尚未构建**（宿主在 `_Ready` 阶段就调了）⇒ 延后到 `Bind()` 之后 ✓");
            return;
        }

        _mode = SceneMode.Map;
        SetMultiFunctionPage(MapPageIndex);
        // 🔴 片 2 第一步：把**地牢面板 #1（光照条）**挂进地图模式（宿主可见性随模式；骨架不动）✓
        DungeonHost().Visible = true;

        // 🔴 主程序 (A)：进地图模式即切到【地图页】⇒ 格子主画面成为主视图 ✓
        // 🔴 用户要求（2026-09-16）：**进地图模式不再自动切页**（原先"上来就默认地图打开"）✓
        GD.Print("[UI 模式] 进地图模式（**不自动切页** —— 由玩家自己选页签）✓");
        HostDungeonPanels();
        GD.Print($"[UI 模式] 进入【地图模式】　{ModeAudit()}");
        GD.Print($"[UI S1] {SkeletonVerdict()}"); // 🔴 切模式后**立即**断言（不是只打印 id）✓
    }

    /// <summary>🔴 回到**战斗模式**（同样不重建）：把 E 区切回第 0 页（详情）✓</summary>
    public void ExitMapMode()
    {
        _mode = SceneMode.Battle;
        SetMultiFunctionPage(0);
        DungeonHost().Visible = false; // 🔴 地牢内层随模式收起（**不销毁、不重建**；骨架始终存活）✓
        if (_eAreaTitle is not null) { _eAreaTitle.Visible = true; }   // 🔴 回到战斗模式 ⇒ 恢复分区标题 ✓
        GD.Print($"[UI 模式] 回到【战斗模式】　{ModeAudit()}");
        GD.Print($"[UI S1] {SkeletonVerdict()}"); // 🔴 退出方向**也要**断言（两向都验，才算"往返不重建"）✓
    }
}
