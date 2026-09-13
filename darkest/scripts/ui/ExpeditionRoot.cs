using System;
using System.Collections.Generic;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 远征场景层**骨架**（M7.5 接线）：持有面板与内核对象的**引用**，把玩家操作转发给内核，再刷新面板。
///
/// 🔴 分工红线（与 `ExpeditionListPanel` 一致）：
/// · **所有状态与判定在内核**（`ExpeditionSession` / `LightMeter` / `Inventory` / `Scouting`）；
///   本类**不做规则判断、不算数字、不掷骰**；
/// · 面板文本**一律**来自 `ExpeditionProjector.RenderList(...)`（数字来自事件流）；
/// · 数据（tuning / nodes）由外部注入（与 `BattleRoot` 同源加载），本类不自己读文件。
///
/// 已接线的交互：**事件二选一（无跳过）** · 扎营入口（柴火不足则按钮禁用） · 回城结算面板 · 面板刷新。
/// 未接线（下一件事）：地图/选路界面切换、背包格子拖放、光照条可视化控件、与战斗场景的往返。
/// </summary>
public partial class ExpeditionRoot : Node
{
    private ExpeditionListPanel _panel = null!;
    private Button _choiceA = null!;
    private Button _choiceB = null!;
    private Button _campButton = null!;
    private readonly List<Button> _buttons = new();

    /// <summary>内核对象（由外部注入；本类只转发，不构造、不改写）。</summary>
    public ExpeditionSession? Session { get; private set; }

    public LightMeter? Meter { get; private set; }

    public Inventory? Bag { get; private set; }

    public TuningConfig? Tuning { get; private set; }

    public ExpeditionNodesConfig? Nodes { get; private set; }

    public CombatLog Log { get; } = new();

    /// <summary>面板最后渲染的行（供自检/冒烟）。</summary>
    public IReadOnlyList<string> LastLines { get; private set; } = Array.Empty<string>();

    public override void _Ready()
    {
        _panel = new ExpeditionListPanel { Name = "ExpeditionListPanel" };
        AddChild(_panel);

        // 事件二选一（**不允许跳过** ⇒ 只有两个选项按钮）
        _choiceA = MakeButton("选项 A", new Vector2(24, 660), () => ChooseEventOption(0));
        _choiceB = MakeButton("选项 B", new Vector2(280, 660), () => ChooseEventOption(1));

        // 扎营入口（柴火不足 ⇒ 禁用 = 灰显）
        _campButton = MakeButton("扎营（1 柴火）", new Vector2(536, 660), Camp);

        _panel.ShowPanel();
    }

    /// <summary>注入内核对象（数据由调用方按与 `BattleRoot` 同源的方式加载）。</summary>
    public void Initialize(ExpeditionSession session, LightMeter meter, Inventory bag,
        TuningConfig tuning, ExpeditionNodesConfig nodes)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Meter = meter ?? throw new ArgumentNullException(nameof(meter));
        Bag = bag ?? throw new ArgumentNullException(nameof(bag));
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));

        Meter.EmitStart(Log); // 光照起点（reason = start；㉑ 曲线需要）
        RefreshPanel();
    }

    /// <summary>刷新面板：**行文本只来自内核投影**（UI 不自己算数）。</summary>
    public void RefreshPanel()
    {
        if (Session is null || Tuning is null)
        {
            return;
        }

        LastLines = ExpeditionProjector.RenderList(
            ExpeditionProjector.Project(Log, Bag!.CountOf(ItemKind.Firewood), Bag.CountOf(ItemKind.Food),
                Session, Tuning.Camp!, Tuning.Expedition.NBattles, Tuning.Expedition.DifficultyTiers),
            Session, Tuning.Expedition.NBattles, ambushTriggered: false, Tuning.Camp!);

        _panel.Refresh(LastLines);
        _campButton.Disabled = !Session.CanCamp; // 灰显依据来自内核（不是 UI 自算）
    }

    /// <summary>事件二选一（**无跳过**；越界由内核拒绝并抛错）。</summary>
    public void ChooseEventOption(int index)
    {
        if (Session is null || Nodes is null || _pendingEventNodeId is null)
        {
            return;
        }

        Session.ResolveEventNode(Log, Nodes.Get(_pendingEventNodeId), index);
        _pendingEventNodeId = null;
        RefreshPanel();
    }

    private string? _pendingEventNodeId;

    /// <summary>设置当前待决策的事件节点（由流程层设置；本类不选择节点）。</summary>
    public void SetPendingEvent(string nodeId)
    {
        _pendingEventNodeId = nodeId;
        ExpeditionNodeConfig node = Nodes!.Get(nodeId);
        _choiceA.Text = node.Options[0].Label;
        _choiceB.Text = node.Options[1].Label;
        RefreshPanel();
    }

    /// <summary>扎营入口（转发给内核；柴火不足时内核拒绝且不扣）。</summary>
    public void Camp()
    {
        if (Session is null || Tuning is null || Meter is null)
        {
            return;
        }

        int campIndex = Session.BattlesPlayed + 1;
        if (!Session.StartCamp(Log, campIndex, Tuning.Camp!.RespiteBase))
        {
            RefreshPanel();
            return;
        }

        Meter.OnCamp(Log); // D0.2：扎营回满 100
        RefreshPanel();
    }

    /// <summary>回城结算（转发；士气完全不恢复由内核保证 #245）。</summary>
    public void ReturnToTown(string outcome)
    {
        Session?.ReturnToTown(Log, outcome);
        RefreshPanel();
    }

    private Button MakeButton(string text, Vector2 position, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = new Vector2(240, 40),
        };
        button.Pressed += onPressed;
        AddChild(button);
        _buttons.Add(button);
        return button;
    }
}
