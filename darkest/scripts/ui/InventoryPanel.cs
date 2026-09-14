using System;
using System.Collections.Generic;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

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
            Columns = 4,
        };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 6);
        col.AddChild(_grid);
        Visible = false;
    }

    /// <summary>注入内核背包（数据与规则都在内核）。</summary>
    public void Initialize(Inventory bag, Action onChanged)
    {
        _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        _onChanged = onChanged;
        Rebuild();
    }

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

        // 非丢弃状态：就地消耗口粮腾格（策划给的备选路径之一）
        if (_bag.Slots.Count > index)
        {
            _hint.Text = $"已选中第 {index + 1} 格：{Describe(_bag.Slots[index])}";
        }
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
