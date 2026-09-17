using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;

/// <summary>
/// M7.5 **背包格子交互界面**（`ui_spec` 必显 **12 背包格子** + P21 ⑬「**包满禁止静默丢弃**」的界面端）。
///
/// 🔴 分工：
/// · **规则全在 `Inventory`**（12 格、口粮不堆叠、整备仅出发前、包满 = `full_choose_discard`）；
/// · 本类只做两件事：**把格子画出来** + **把点击转发成 `TryDiscardAt` / `TryAdd`**；
/// · **不含任何数值判断**（格数、是否满、能否支付都由内核回答）。
///
/// 交互流（P21 ⑬）：
///   拾取时若 `TryAdd` 返回 `full_choose_discard` ⇒ 进入 **「选择丢弃哪一格」** 状态（格子高亮可点），
///   玩家点某一格 ⇒ `TryDiscardAt` 腾格 ⇒ **自动重试拾取**（不静默丢、也不丢玩家的东西而不告知）。
/// </summary>
/// 🔴 `#319` **改类（实机取证）**：本类**原来是 `CanvasLayer`**，而 `Expedition.tscn` 把它声明为 `PanelContainer`
/// ⇒ 引擎报 `Script inherits from native type 'CanvasLayer' …` ⇒ **脚本没挂上 ⇒ 背包格子面板是死的**（P21 ⑬ 的界面端失效）
/// ⇒ 正解 = 本类自己就是面板（`PanelContainer`）+ 内部 `VBox`（提示 + 格子）堆叠（不再手写 `Position`）✓
public partial class InventoryPanel : PanelContainer
{
    private Inventory? _bag;
    private Action? _onChanged;
    private InventoryItem? _pendingPickup;

    private GridContainer _grid = null!;
    private Button? _consumeFood; // 包满时的显式"消耗口粮腾格"动作（内核回答可用性）
    private string _lastBagSnapshot = string.Empty; // 🔴 (b) 背包内容读数：只在变化时打一行 ✓
    private Label _hint = null!;
    private readonly List<Button> _slotButtons = new();

    /// <summary>是否处于"包满 → 选择丢弃"状态（供自检/冒烟）。</summary>
    public bool IsChoosingDiscard => _pendingPickup is not null;

    public override void _Ready()
    {
        var col = new VBoxContainer { Name = "InventoryCol" };
        col.AddThemeConstantOverride("separation", 6);
        AddChild(col);

        _hint = new Label
        {
            Name = "InventoryHint",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 24), // §14.2④：给最小尺寸，防塌陷
        };
        col.AddChild(_hint);

        _grid = new GridContainer
        {
            Name = "InventoryGrid",
            Columns = 2 // 🔴 相机口径：4 列网格最小宽过大 ⇒ 2 列,
        };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 6);
        col.AddChild(_grid);

        // 🔴 主程序清单第 4 组之一：**就地消耗口粮腾格**（`Inventory.TryConsumeFoodToFreeSlot`）
        //    —— 原先这里只有一句注释/HINT"备选路径之一"，**并没有动作**（红线 21：不留"看起来能点"的假动作）⚠️
        //    ⇒ 现在做成**显式按钮**：仅在**包满**时出现；可用性由内核回答（无口粮 ⇒ 置灰 + tooltip 说明）✓
        _consumeFood = new Button
        {
            Name = "ConsumeFoodToFreeSlot",
            Text = "消耗口粮腾出一格",
            CustomMinimumSize = new Vector2(0, 30),
            Visible = false,
        };
        _consumeFood.Pressed += () =>
        {
            if (_bag is null)
            {
                return;
            }

            if (!CanOperate())
            {
                _hint.Text = "当前相位不可操作背包（消耗口粮属相位动作）";
                GD.Print("[背包] 消耗口粮被相位谓词拒绝 ⇒ 不改背包（如实报）");
                return;
            }

            bool ok = _bag.TryConsumeFoodToFreeSlot(out InventoryItem? consumed);
            _hint.Text = ok
                ? $"已消耗 {Describe(consumed!)} 腾出一格"
                : "没有可消耗的口粮（内核拒绝，不部分扣）";
            GD.Print($"[背包] 消耗口粮腾格：内核受理={ok}（剩余 {_bag.Slots.Count}/{_bag.SlotCap}）");
            Rebuild();
            _onChanged?.Invoke();
        };
        col.AddChild(_consumeFood);
        Visible = false;
    }

    /// <summary>注入内核背包（数据与规则都在内核）。</summary>
    public void Initialize(Inventory bag, Action onChanged)
    {
        _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        _onChanged = onChanged;
        Rebuild();
    }

    /// <summary>
    /// 🔴 `#327` 相位裁定（架构 `…PHASE-RULING…` ③）：**"查看"是 A 类（战斗期成立），"操作"是相位动作** ——
    /// 背包里混了两种动作 ⇒ 操作（`消耗口粮腾格` / `满则丢弃`）必须**按相位禁用 + 说明理由**（不留不可解释的禁用）✓
    /// 🔴 UI **绝不自己推断相位**（`#325` D6 家族）⇒ 由**宿主**传入谓词；未传 ⇒ 旧行为（`true`，兼容既有调用点）✓
    /// </summary>
    public Func<bool> CanOperate { get; set; } = () => true;

    /// <summary>拾取一格：**满则进入"选择丢弃"状态**（P21 ⑬：禁止静默丢弃）。</summary>
    public bool TryPickup(InventoryItem item)
    {
        if (_bag is null)
        {
            return false;
        }

        if (_bag.TryAdd(item, out string reason))
        {
            Rebuild();
            _onChanged?.Invoke();
            return true;
        }

        if (reason == "full_choose_discard")
        {
            _pendingPickup = item;
            _hint.Text = "背包已满：请选择要丢弃的一格（不会自动丢弃）";
            Rebuild();
            return false;
        }

        return false;
    }

    /// <summary>点某一格：若在"选择丢弃"状态 ⇒ 丢弃该格并**自动重试拾取**；否则无操作。</summary>
    public void ClickSlot(int index)
    {
        if (_bag is null)
        {
            return;
        }

        if (_pendingPickup is { } item)
        {
            if (!CanOperate())
            {
                // 🔴 相位不允许"操作" ⇒ **禁用 + 说明理由**（红线 21：不留不可解释的禁用）✓
                _hint.Text = "当前相位不可操作背包（丢弃/消耗属相位动作；相位谓词由内核给 ⇒ 待 `CanShow…` 落地）";
                GD.Print("[背包] 丢弃被相位谓词拒绝 ⇒ 不改背包（如实报）");
                return;
            }

            if (!_bag.TryDiscardAt(index, out _))
            {
                return;
            }

            _pendingPickup = null;
            _hint.Text = string.Empty;
            _bag.TryAdd(item, out _); // 腾格后重试拾取
            Rebuild();
            _onChanged?.Invoke();
            return;
        }

        // 非丢弃状态：只是**选中**该格并说明（真正"腾格"走上面那个显式按钮；文案不得承诺未实现的动作 ✓）
        if (_bag.Slots.Count > index)
        {
            _hint.Text = $"已选中第 {index + 1} 格：{Describe(_bag.Slots[index])}";
        }
    }

    /// <summary>包满时刷新显式"消耗口粮腾格"按钮的可见性/可用性（**由内核回答**，红线 21）✓</summary>
    private void RefreshConsumeFoodButton()
    {
        if (_bag is null || _consumeFood is null)
        {
            return;
        }

        bool full = _bag.Slots.Count >= _bag.SlotCap;
        _consumeFood.Visible = full;
        bool phaseOk = CanOperate();
        _consumeFood.Disabled = !full || !phaseOk || _bag.CountOf(ItemKind.Food) <= 0;
        _consumeFood.TooltipText = !full
            ? "背包未满 ⇒ 不需要腾格"
            : !phaseOk ? "当前相位不可操作背包（操作属相位动作，相位谓词待内核落地）"
            : _bag.CountOf(ItemKind.Food) <= 0 ? "没有口粮可消耗" : "消耗 1 个口粮腾出一格（内核决定规则）";
    }

    /// <summary>重建格子（纯渲染；格数来自内核 `SlotCap`）。</summary>
    public void Rebuild()
    {
        if (_bag is null)
        {
            return;
        }

        foreach (Node child in _grid.GetChildren())
        {
            child.QueueFree();
        }

        RefreshConsumeFoodButton(); // 🔴 包满时才出现"消耗口粮腾格"（可见性/可用性由内核回答）✓

        // 🔴 主程序裁定（准我的 (b)）：**背包内容只读读数** —— 让"到底有没有 `support_pack`"可判、可复核 ✓
        //    形态：内容**变化时**打一行（不刷屏）；**这不是调试开关，是"不是看起来像"的取证口**（红线 25）✓
        string snapshot = $"{_bag.Slots.Count}/{_bag.SlotCap}　" + string.Join("　", _bag.Slots.Select(Describe));
        if (snapshot != _lastBagSnapshot)
        {
            _lastBagSnapshot = snapshot;
            GD.Print($"[背包] 内容={snapshot}");
        }

        _slotButtons.Clear();
        for (int i = 0; i < _bag.SlotCap; i++)
        {
            bool occupied = i < _bag.Slots.Count;
            int index = i; // 闭包捕获
            var button = new Button
            {
                Name = $"Slot{i + 1}",
                Text = occupied ? Describe(_bag.Slots[i]) : $"{i + 1}. （空）",
                CustomMinimumSize = new Vector2(140, 40),
            };
            button.Pressed += () => ClickSlot(index);
            _grid.AddChild(button);
            _slotButtons.Add(button);
        }
    }

    private static string Describe(InventoryItem item) => item.Kind switch
    {
        ItemKind.Firewood => "柴火",
        ItemKind.Food => "口粮",
        ItemKind.SupportCrate => "支援箱",
        _ => "支援包",
    };
}
