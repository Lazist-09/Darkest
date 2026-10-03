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
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求**按相位切**）✓
/// ② 本文件 = **战斗 · 地牢宿主与地图相位族**（`SceneMode == Map`：宿主面板/扎营面板/行走地图/行走 HUD、
///    `SetAbandonAction`/`PressAbandon` 放弃远征、`HostDungeonPanels`、`EnterMapMode`/`ExitMapMode`）✓
/// ③ 🔴 依赖主类私有成员/状态：`_uiRoot`/`_bottomRow`/`_dungeonHost`/`_abandonButton`/`_abandonExpedition`/
///    `_abandonConfirm`/`_abandonWarned`/`_mode`/`_walkView`/`_host`（其余读数走内核 `ExpeditionFlow`）✓
/// ④ **只搬家、零行为改动**（含 2026-09-21 `_uiRoot` 未就绪守卫与 map-phase 分支，一字未改）✓
/// </summary>
/// ⑤ 🔴 2026-10-02 第八件：原 520 行**按相位族切 4 片** —— 扎营面板 ⇒ `.Camp.cs`、行走 HUD/示意地图 ⇒ `.Walk.cs`、
///    放弃远征 ⇒ `.Abandon.cs`（本片保留：宿主 · 面板登记 · 模式进出）；**只搬家、零行为改动** ✓
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

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // 🔴🔴 2026-10-03：**地图 → 战斗 ／ 终点** 两处接线（玩家路径的真缺陷修复）——
    //    两条回调（瓷砖主画面 `TryStepTile` ／ 房间图 `StepTo`）**共用这两个方法** ⇒ 判据只写一处、不漂移 ✓
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 🔴 走进**战斗格** ⇒ 在**同一场景内**起一场远征战斗（`#327` 片 4 的玩家入口）。
    /// 返回 true = 这一步已由战斗接管（调用方**不要再**弹饥饿 ／ 刷面板：起战斗会 `ExitMapMode`）✓
    /// · 判据 = **当前房间类型**（拓扑模式下 `flow.Current` 恒 null ⇒ 只能按房间类型判）
    /// · 🔴 **人确实还在那间房里**（瓷砖模式）：站立格必须映射回 `CurrentRoomId` —— 内核只在**踩进房间格**时更新房间 id
    ///   （`ExpeditionFlow.TileWalk.cs`：走廊格**不更新**）⇒ 单看房间类型会**在走出战斗房后的走廊格**上误起一场战斗 ⚠️
    ///   （判据收在 `StandingInsideCurrentRoom`）✓
    /// · **撤退过的那格**（`IsRoomResolved`，`#352`）**不起战斗**：只挪位置 ✓
    /// ⚠️ 已知缺口（已登记）：**打赢不写** `_resolvedRooms`（内核只在 `DrawRetreat` 写）⇒ 走回已打过的战斗格**会再打一场**；
    ///    内核写入口（`MarkRoomResolved`）留作后续件（见 `doc/state.md` 与本轮报告）✓
    /// </summary>
    private bool TryStartBattleAtCurrentRoom(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        if (flow.CurrentRoomType != "battle" || flow.IsRoomResolved(flow.CurrentRoomId))
        {
            return false;
        }

        // 🔴🔴 2026-10-03 二修（真缺陷 · 玩家路径）：**人必须还在那间房里**才起战斗 ——
        //    内核只在**踩进房间格**时更新 `_currentRoomId`（走廊格不更新）⇒ 从战斗房**往外走**时
        //    `CurrentRoomType` 仍是 `"battle"`、`IsRoomResolved` 仍是 false ⇒ 单看类型会**在走廊格上误起一场战斗** ⚠️
        //    （画面里人明明站在走廊 ⇒ 红线 18 家族：显示必须等于真值）✓
        if (!StandingInsideCurrentRoom(flow))
        {
            GD.Print($"[UI 地图] ⚠️ 战斗房 {flow.CurrentRoomId} **人已不在房内**（现在 {flow.TilePosition}）⇒ **不起战斗**（本步只挪位置）✓");
            return false;
        }

        if (_host is not { } host)
        {
            GD.Print("[UI 地图] ⚠️ 走进战斗格但**无宿主**（`_host` 为空）⇒ 如实不起战斗（不假装成功）✓");
            return true;
        }

        GD.Print($"[UI 地图] 🔴 走进**战斗格**（房间 {flow.CurrentRoomId}）⇒ 起战斗" +
                 $"（同场景 · 本趟第 {flow.Session.BattlesPlayed + 1} 场 · 此前胜 {flow.Wins}）✓");
        host.StartExpeditionBattleInScene();
        return true;
    }

    /// <summary>
    /// 🔴 走到**终点** ⇒ 本趟结算并回城（`DungeonRunDriver.ReturnToTown`：**与 `--hamlet-next` ／冒烟 `town` 同一实现** ⇒ 不两处漂移）。
    /// 返回 true = 这一步已由回城接管 ✓
    /// · `result` 由内核 `Completed`（到终点 **且** 胜场 ≥ `tuning.expedition.battle_goal`）定 ✓
    /// · ⚠️ 终点房**若是战斗房**：先打完（上面的方法接管），战后由 `BattleRoot.EndGame` 的【继续（回地图）】补结算 ✓
    /// </summary>
    private bool TryReturnToTownAtGoal(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        if (!flow.ReachedGoal)
        {
            return false;
        }

        // 🔴 同一类缺口：`ReachedGoal` 是**粘性**的（`_currentRoomId` 出房后不更新）⇒ 走出终点房**不再**结算回城，
        //    否则"站在走廊上点一下"会被莫名传回城（显示 ≠ 真值）✓
        if (!StandingInsideCurrentRoom(flow))
        {
            return false;
        }

        if (_host is not { } host)
        {
            GD.Print("[UI 地图] ⚠️ 走到终点但**无宿主**（`_host` 为空）⇒ 如实不结算回城（不假装成功）✓");
            return true;
        }

        string result = flow.Completed ? "completed" : "abandoned";
        GD.Print($"[UI 地图] 🏁 走到**终点**（房间 {flow.CurrentRoomId}）⇒ 结算回城：" +
                 $"走 {flow.StepsDone} 段 ／ 胜 {flow.Wins} ／ 光照 {flow.Meter.Value} ⇒ `{result}` ✓");
        Darkest.Gameplay.Scene.DungeonRunDriver.ReturnToTown(host, flow, result);
        return true;
    }

    /// <summary>
    /// 🔴 **站立格是否确实落在 `CurrentRoomId` 那间房里**（瓷砖模式专用；房间图/线性模式恒 true）——
    /// 内核 `_currentRoomId` **只在踩进房间格时更新**（走廊格不更新）⇒ 出房后它仍是**上一间房**的 id，
    /// 两个判据（起战斗 ／ 到终点）都必须先问这一句，否则会在**走廊格**上触发"房间事件" ⚠️
    /// · 依据（派生器契约，`DungeonGridDeriver`）：房间 3×3 块**全部**登记进 `TileRoom` · 走廊格**永不**登记 ·
    ///   房间格**不被走廊覆盖**（`Carve` 遇房间格直接返回）⇒ "站立格 → 房间 id" 是可靠真值 ✓
    /// </summary>
    private static bool StandingInsideCurrentRoom(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        if (!flow.TileWalkEnabled || flow.TileWalk is not { } tileWalk)
        {
            return true; // 房间图（`StepTo`）只允许走进相邻房间 ⇒ 位置与房间 id 天然一致 ✓
        }

        (int X, int Y) here = flow.TilePosition;
        return tileWalk.TileRoom.TryGetValue(here, out int roomId) && roomId == flow.CurrentRoomId;
    }
}
