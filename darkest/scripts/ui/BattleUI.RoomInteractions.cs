using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **房间交互面**（`#327` 片 2 尾部）：把【事件房 / Curio / 选路】三个交互**接到生产入口**。
///
/// <para>⚠️ 为什么需要本文件（**如实说明**）：`check_data_discipline.py --deadfuncs` 报出内核有 4 个
/// `public` 方法**只有定义处、无生产调用点（连测试都没调）** ⇒ 它们是"写了但没接上"的悬空接口：</para>
/// <list type="bullet">
///   <item><description>`ExpeditionFlow.EventNodeIdForRoom` —— 事件房 ⇒ 事件节点的映射（**房间内容层**）</description></item>
///   <item><description>`ExpeditionFlow.PreviewOptions` —— 选路界面在玩家点击**之前**的预览（**选路 UI**）</description></item>
///   <item><description>`ExpeditionFlow.RetryPendingLoot` —— 背包满 ⇒ 腾格后重收待处理补给（**丢弃流程**）</description></item>
///   <item><description>`ExpeditionFlow.LeaveCurio` —— Curio"走开"（**零变化**出口，V4）</description></item>
/// </list>
/// <para>纪律：本类**只转发**（宿主/UI 只调内核、不重算数字）；**无本趟数据 ⇒ 空态 + 留痕**（红线 21）✓</para>
/// </summary>
public partial class BattleUI : Control
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ① Curio（事件房内容）面板 —— 挂进 `DungeonHost()`（与扎营面板同法：骨架之外 ⇒ 切模式不动骨架）
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>`#327` 片 2 #5 的内容层（说明 + 逐选项按钮）；外框由宿主提供 ✓</summary>
    private Darkest.UI.CurioPanel? _mapModeCurio;

    /// <summary>当前正被交互的 Curio（null = 面板收起）；同一时刻只开一个 ✓</summary>
    private Darkest.Data.CurioConfig? _activeCurio;

    /// <summary>可断言的读数（headless 冒烟用）：当前 Curio / 面板可见性 / 选项数 ✓</summary>
    public string DescribeCurio()
        => _mapModeCurio is null || !GodotObject.IsInstanceValid(_mapModeCurio)
            ? "curio: 未建"
            : $"curio: 当前 {(_activeCurio?.Id ?? "（无）")}　" +
              $"面板={(_mapModeCurio.Visible ? "显示" : "隐藏")}({_mapModeCurio.Buttons.Count} 选项)　" +
              $"空手文案 {(_activeCurio is null ? "—" : (ExpeditionContext.Flow?.LastCurioText ?? "（无）"))}";

    /// <summary>
    /// 🔴 **进入某个房间 ⇒ 按内容表挑一个 Curio 并开面板**（`PickCurioForRoom` 的生产消费点之一）——
    /// `roomType` 由调用方给（内核 `CurrentRoomType`），**支路房**由 `MapRoom.IsBranch` 判 ✓
    /// 无内容（池为空 / 未解锁全部）⇒ **不开面板**（如实返回 false，不静默造一个 Curio）✓
    /// </summary>
    public bool EnterRoomCurio(int roomId, bool isBranch)
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        Darkest.Data.RoomContentsConfig? contents = ExpeditionContext.RoomContents;
        Darkest.Data.CuriosConfig? curios = ExpeditionContext.Curios;
        if (flow is null || contents is null || curios is null)
        {
            GD.Print("[UI 房间交互] 进事件房：缺少流程/内容表/Curio 目录 ⇒ **不开面板**（如实空态）✓");
            return false;
        }

        string roomType = flow.CurrentRoomType ?? "event";
        string? curioId = flow.PickCurioForRoom(contents, roomType, isBranch);
        if (curioId is null)
        {
            GD.Print($"[UI 房间交互] 房间 {roomId}（{roomType}／支路={isBranch}）⇒ 内容池为空 ⇒ **无 Curio 可开** ✓");
            return false;
        }

        Darkest.Data.CurioConfig? curio = curios.RealCurios.FirstOrDefault(c => c.Id == curioId);
        if (curio is null)
        {
            // 🔴 `PickCurioForRoom` 只从 `contents` 的行里挑；若内容表引用了目录里没有的 id ⇒ **如实报**（红线 21）✓
            GD.Print($"[UI 房间交互] 🔴 内容表给出 `{curioId}`，但 `curios.json` 目录里**不存在** ⇒ 不开面板（数据缺口）⚠️");
            return false;
        }

        OpenCurio(curio);
        GD.Print($"[UI 房间交互] 房间 {roomId}（{roomType}／支路={isBranch}）⇒ Curio **{curio.Name}**({curio.Id}) ✓");
        return true;
    }

    /// <summary>
    /// 打开某个 Curio 的面板：**空手 / 用道具 / 走开** 三个出口 + "丢弃腾格后重试"（V4/V7 全给到）。
    /// 口径：选项**由数据决定**（`ItemResults` 有哪些道具就列哪些），UI 不自己编选项 ✓
    /// </summary>
    public void OpenCurio(Darkest.Data.CurioConfig curio)
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null)
        {
            GD.Print("[UI 房间交互] 打开 Curio：无本趟流程 ⇒ 不执行（如实拒绝）✓");
            return;
        }

        if (_mapModeCurio is null || !GodotObject.IsInstanceValid(_mapModeCurio))
        {
            _mapModeCurio = new Darkest.UI.CurioPanel { Name = "MapModeCurio" };
            DungeonHost().AddChild(_mapModeCurio);
        }

        _activeCurio = curio;

        // 🔴 **选项由数据决定**：空手恒有；道具**按 `item_results` 的键**逐条列（不硬编码道具名）✓
        var options = new List<(string Label, Action OnPick)>
        {
            ("空手一试（内核掷骰，结果写在事件流）", () => CurioBare(curio)),
        };

        if (curio.ItemResults is { Count: > 0 } items)
        {
            // 🔴 每个 `item_result` 带自己的 `.Item`（道具名）—— **不硬编码**，逐条按键列 ✓
            foreach (Darkest.Data.CurioItemResultConfig r in items)
            {
                string itemLocal = r.Item;
                options.Add(($"用【{itemLocal}】", () => CurioWithItem(curio, itemLocal)));
            }
        }

        options.Add(("走开（零变化）", () => CurioLeave(curio)));

        _mapModeCurio.Refresh(
            $"【{curio.Name}】\n（空手会掷骰；用对道具可直取更优结果。走开则什么都不会发生）",
            options);
        GD.Print($"[UI 房间交互] Curio 面板：{curio.Name}({curio.Id}) ⇒ {options.Count} 个选项 " +
                 $"（道具 {curio.ItemResults?.Count ?? 0} 种）✓");
    }

    /// <summary>空手一试（内核掷骰 ⇒ 写 `RngDraw`）；结果文本进面板，再用"丢弃腾格后重试"收尾 ✓</summary>
    private void CurioBare(Darkest.Data.CurioConfig curio)
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null)
        {
            return;
        }

        CurioOutcome? o = flow.ResolveCurio(curio, itemUsed: null,
            roster: ExpeditionContext.Roster, diseases: null);
        AfterCurio(flow, curio, o, "空手");
    }

    /// <summary>用指定道具（**直查、不掷骰**）；数据没定义 ⇒ 内核返回 null ⇒ 如实报拒 ✓</summary>
    private void CurioWithItem(Darkest.Data.CurioConfig curio, string item)
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null)
        {
            return;
        }

        CurioOutcome? o = flow.ResolveCurio(curio, itemUsed: item,
            roster: ExpeditionContext.Roster, diseases: null);
        AfterCurio(flow, curio, o, $"道具[{item}]");
    }

    /// <summary>
    /// 🔴 **走开**（V4）：零变化出口 —— 直接转发内核 `LeaveCurio`（本方法就是它的**生产调用点**）✓
    /// </summary>
    private void CurioLeave(Darkest.Data.CurioConfig curio)
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null)
        {
            return;
        }

        CurioOutcome o = flow.LeaveCurio(curio);
        GD.Print($"[UI 房间交互] Curio {curio.Id} ⇒ **走开**（{o.Text}）· 零变化 ✓");
        CloseCurio();
    }

    /// <summary>
    /// 结算后收尾：写读数 + **待处理补给重试**（`RetryPendingLoot` 的生产调用点）。
    /// 🔴 口径：包满时内核把补给入"待处理"队列（**不静默丢**）⇒ 这里腾格后**再收一次**；
    ///    仍失败（还没腾格）⇒ 如实提示"请先丢弃"（不假装收下）✓
    /// </summary>
    private void AfterCurio(ExpeditionFlow flow, Darkest.Data.CurioConfig curio, CurioOutcome? o, string route)
    {
        if (o is null)
        {
            GD.Print($"[UI 房间交互] Curio {curio.Id}：{route} ⇒ 内核**拒绝**（该道具对此 Curio 未定义）✓");
            return;
        }

        GD.Print($"[UI 房间交互] Curio {curio.Id}：{route} ⇒ {o.Text}（kind={o.Kind}:{o.Amount}／route={o.Route}）" +
                 (o.Deferred ? "　🔴 **阶段二 kind：显式不生效**（已写事件留痕）" : string.Empty));

        // 🔴 `D-7`：**被折磨拒绝用道具** ⇒ 必须**显式告知玩家原因** ——
        //    否则玩家看到"我点了道具，结果走了空手"会以为**是 bug**（红线 21：不静默）⚠️
        if (flow.LastActOutRefused)
        {
            GD.Print($"[UI 房间交互] 🔴 **D-7 折磨 act-out：{flow.LastActOutText}** " +
                     $"⇒ 被拒的「{route}」**没用上**，改走**空手**（代价照常承担）✓");
        }

        // 🔴 **待处理补给重试**（唯一生产调用点）—— 包满 ⇒ 队列非空 ⇒ 这里再收一次 ✓
        bool collected = flow.RetryPendingLoot();
        GD.Print(collected
            ? "[UI 房间交互] 待处理补给：**已全部收下**（或本就没有待处理）✓"
            : "[UI 房间交互] 待处理补给：**背包仍满** ⇒ 请先在背包面板【丢弃】一格，再来收 ✓（不静默丢）");

        CloseCurio();
        HostDungeonPanels(); // 背包/光照/投影变化 ⇒ 刷新同屏读数 ✓
    }

    /// <summary>收起 Curio 面板（清按钮 + 隐藏 ⇒ 不留不可解释的空）✓</summary>
    public void CloseCurio()
    {
        _activeCurio = null;
        _mapModeCurio?.Clear();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ② 选路预览 —— `PreviewOptions` 的生产消费点（供选路 UI 在玩家点击**之前**渲染候选）
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 🔴 **选路候选的只读预览**（**不消耗流程状态、不掷骰**）—— 直接转发内核 `PreviewOptions` ✓
    /// 用途：把"下一步能去哪两个地方"显示在**点击之前**（玩家点击后才走 `Advance`）✓
    /// 非流程步骤（拓扑模式 / 已到终点）⇒ 返回空集合（如实，不假装有候选）✓
    /// </summary>
    public IReadOnlyList<string> PreviewPathOptions()
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null)
        {
            return Array.Empty<string>();
        }

        IReadOnlyList<PathOption> options = flow.PreviewOptions();
        if (options.Count == 0)
        {
            return Array.Empty<string>();
        }

        return options.Select((o, i) =>
        {
            // 🔴 走**内核给的类型名**（不自己映射；`PathOption` 自带 `NodeType`/`NodeId`）✓
            string kind = o.NodeType;
            string node = string.IsNullOrEmpty(o.NodeId) ? string.Empty : $"（{o.NodeId}）";
            return $"{i}: {kind}{node}";
        }).ToList();
    }

    /// <summary>可断言的读数：候选预览（headless 冒烟用）✓</summary>
    public string DescribePathPreview()
    {
        IReadOnlyList<string> preview = PreviewPathOptions();
        return preview.Count == 0
            ? "path-preview: （无候选：非流程步骤 / 已到终点）"
            : $"path-preview: {string.Join("　", preview)}";
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ③ 事件房 ⇒ 事件节点映射 —— `EventNodeIdForRoom` 的生产消费点
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 🔴 **走进"事件房"时取该房间对应的事件节点**（`EventNodeIdForRoom` 的生产消费点）——
    /// 供 UI 把"这个房间里是什么事件"显示出来 / 供后续的"事件房 UI"接上 ✓
    /// 无事件内容（战斗房 / 特殊房）⇒ null（如实，不假装有内容）✓
    /// </summary>
    public string? EventNodeIdForRoom(int roomId)
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        return flow?.EventNodeIdForRoom(roomId);
    }

    /// <summary>可断言的读数：当前房间的事件节点（headless 冒烟用）✓</summary>
    public string DescribeRoomEvent()
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null || !flow.IsTopologyMode)
        {
            return "room-event: （非拓扑远征 ⇒ 无房间概念）";
        }

        int roomId = flow.CurrentRoomId;
        string roomType = flow.CurrentRoomType ?? "?";
        string? nodeId = EventNodeIdForRoom(roomId);
        return $"room-event: 房间 {roomId}（{roomType}）⇒ 事件节点 {nodeId ?? "（无：战斗房/特殊房）"}";
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ④ `D-5` 饥饿 —— "队伍饿了"的**吃 / 不吃**二选一（DD wiki "Hunger"）
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>`D-5` 饥饿面板（**复用** `CurioPanel` 的"说明 + 逐选项按钮"形状 ⇒ 不新造一套控件）✓</summary>
    private Darkest.UI.CurioPanel? _mapModeHunger;

    /// <summary>
    /// 🔴 `D-5`：**队伍饿了** ⇒ 弹"吃 / 不吃"二选一（**必须选，不能拖、不能跳** —— DD 铁律）。
    ///
    /// <para>· 口径全部来自内核：<see cref="ExpeditionFlow.HasPendingHunger"/> 是**唯一入口**
    ///   （它会同时确认"名册台账已建" ⇒ 首场战斗之前**不弹**，因为那时无人可结算）✓</para>
    /// <para>· **"吃"按钮**：口粮够 ⇒ 正常；**不够 ⇒ 禁用（灰显）** —— DD 铁律「不能只喂一部分人」，
    ///   凑不齐就是全员挨饿（此时点"不吃"即可，效果完全相同；给个灰按钮是为了**让玩家看见原因**）✓</para>
    /// <para>· 数字**全部来自内核**（`HungerFoodNeeded` / `CanEatForHunger`）—— UI **不重算** ✓</para>
    /// </summary>
    public bool TryOpenHunger()
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null || !flow.HasPendingHunger)
        {
            return false; // 不饿 / 台账未建 / 没测到 ⇒ **不开面板**（如实，不假装有事件）✓
        }

        if (_mapModeHunger is null || !GodotObject.IsInstanceValid(_mapModeHunger))
        {
            _mapModeHunger = new Darkest.UI.CurioPanel { Name = "MapModeHunger" };
            DungeonHost().AddChild(_mapModeHunger);
        }

        int need = flow.HungerFoodNeeded;
        bool canEat = flow.CanEatForHunger;
        int have = ExpeditionContext.Flow.Session.Food;

        var options = new List<(string Label, Action OnPick)>
        {
            (canEat
                ? $"吃（耗 {need} 份口粮 ⇒ 全队回 5% 最大 HP）"
                : $"吃（🔴 口粮不足：需 {need}、只有 {have} ⇒ 只能挨饿）",
                () => HungerResolve(flow, eat: true)),
            ("不吃（全队掉 20% 最大 HP + 涨 20 压力）", () => HungerResolve(flow, eat: false)),
        };

        _mapModeHunger.Refresh(
            canEat
                ? "【队伍饿了】必须二选一：\n吃 —— 每名存活成员 1 份口粮，全队回 5% 自己最大 HP。"
                : $"【队伍饿了】🔴 口粮凑不齐（需 {need} 份、只有 {have} 份）——\n" +
                  "DD 铁律：**不能只喂一部分人** ⇒ 无论选哪个都是全员挨饿，且**一口粮都不消耗**。",
            options);

        // 🔴 "吃"在口粮不足时**禁用（灰显）** —— 点了也是挨饿，灰掉是为了让玩家看见原因（不是隐藏选项）✓
        if (!canEat && _mapModeHunger.Buttons.Count > 0)
        {
            _mapModeHunger.Buttons[0].Disabled = true;
        }

        GD.Print($"[UI 饥饿] 弹出【吃 / 不吃】二选一：需 {need} 份口粮、现有 {have} 份、可吃={canEat} " +
                 $"(光照档 index={flow.LastHunger.TierIndex}、概率 {flow.LastHunger.PercentUsed}%) ✓");
        return true;
    }

    /// <summary>结算饥饿（**只转发内核** ⇒ 数字与效果都在内核）；结算完刷新同屏读数 ✓</summary>
    private void HungerResolve(ExpeditionFlow flow, bool eat)
    {
        string outcome = flow.ResolveHunger(eat);
        // 🔴 `D-7`：**被折磨拒绝进食** ⇒ 必须**显式告知玩家原因**（否则只看到"饿"，玩家以为是自己选错）⚠️
        bool actOutRefused = flow.Session.ActOutEatRefuseCount > 0
            && eat && outcome == "starve";
        GD.Print($"[UI 饥饿] 玩家选「{(eat ? "吃" : "不吃")}」⇒ 内核判定 **{outcome}** " +
                 $"(口粮余 {flow.Session.Food}、存活 {flow.Session.Survivors})" +
                 (actOutRefused ? "　🔴 **D-7 折磨 act-out：有人拒绝进食 ⇒ 强制挨饿**（口粮一点不扣）" : string.Empty) +
                 (eat && outcome == "starve" && !actOutRefused
                     ? "　🔴 口粮凑不齐 ⇒ 强制挨饿且未扣粮（DD 铁律）" : string.Empty));

        CloseHunger();
        HostDungeonPanels(); // 血量/士气/口粮都变了 ⇒ 刷新同屏读数 ✓
    }

    /// <summary>收起饥饿面板（清按钮 + 隐藏）✓</summary>
    public void CloseHunger() => _mapModeHunger?.Clear();

    /// <summary>可断言的读数：饥饿面板状态（headless 冒烟用）✓</summary>
    public string DescribeHunger()
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null)
        {
            return "hunger: （无本趟流程）";
        }

        bool pending = flow.HasPendingHunger;
        string panel = _mapModeHunger is null || !GodotObject.IsInstanceValid(_mapModeHunger)
            ? "未建"
            : $"{( _mapModeHunger.Visible ? "显示" : "隐藏")}({_mapModeHunger.Buttons.Count} 选项)";
        return $"hunger: 待结算={pending}　面板={panel}　需口粮={flow.HungerFoodNeeded}　" +
               $"可吃={flow.CanEatForHunger}　缓冲={flow.HungerBuffer}　已饿={flow.HungerCount}";
    }
}
