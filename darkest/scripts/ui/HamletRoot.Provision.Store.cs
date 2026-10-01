using System;
using System.Collections.Generic;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// 🆕 **M12 · 供应屏 ③：商店（买 ／ 卖 ／ 城内库存）** —— DD `provision_store_background_layout` 位内的 11 行商品。
///
/// <para>🔴 分工（红线 26：UI 不算账）：本文件只做**呈现 + 转发**；真值全在内核 `ProvisionStock`
/// （买 = `Economy.TrySpend` ／ 卖 = `Economy.AwardContent`；未知 id ／ 数量 ≤ 0 ／ 零价 ／ 钱不够 ／ 货不够
/// 一律**返回拒绝理由**而不是抛 ⇒ 本屏把它渲染成一行）✓</para>
/// <para>🔴 **零价行**（一手 `firewood` ／ `dog_treats` = 0/0）⇒ **置灰 + tooltip 说明**（不发明免费商品，红线 21）✓</para>
/// <para>🔴 **城内库存不入档**（用户 2026-10-02 裁定 ⇒ 登记 `O-113`，退出即丢）⇒ 买卖成功后**不调 `AutoSave`**
/// （回城自动存档会把这张空表当真相写盘；本包只登记，不改存档版本）✓</para>
/// <para>🔴 选人 = **方块空洞 + 拖头像进孔**（DD 式；见 `ProvisionSkeleton.PartyGrid` 与 `GearHeroSquare`），
/// 本文件只管**商店列**，不碰队伍格 ✓</para>
/// </summary>
public partial class HamletRoot : Control
{
    private Label? _provStatus;
    private string _provLastTrade = "（本进程尚无买卖）";
    private readonly Dictionary<string, (Button Buy, Button Sell, Label Stock)> _provCells = new(StringComparer.Ordinal);

    /// <summary>城内库存总件数（供冒烟断言；机制关闭 ⇒ -1，**不是 0**：0 是「开着但没货」）✓</summary>
    public int ProvisionStockCount => ExpeditionContext.Supplies?.TotalCount ?? -1;

    /// <summary>状态行标签（弹窗重开／关闭后旧引用会被释放 ⇒ 用前必须验活）✓</summary>
    private Label? LiveProvStatus
        => _provStatus is not null && GodotObject.IsInstanceValid(_provStatus) ? _provStatus : null;

    /// <summary>
    /// 惰性加载 `provisions.json` ⇒ `ExpeditionContext.Supplies`（**幂等**：已有实例直接复用 ⇒ 买卖读数不会被重置）✓
    /// 缺表 ／ 解析失败 ⇒ **机制关闭 + 打印**（不静默编一份表，红线 21）✓
    /// </summary>
    private void EnsureSupplies()
    {
        if (ExpeditionContext.Supplies is not null)
        {
            return;
        }

        if (!FileAccess.FileExists(ProvisionsConfig.ResPath))
        {
            GD.Print($"[供应] `{ProvisionsConfig.ResPath}` 缺失 ⇒ 买卖机制关闭（opt-in；不静默编一份表）✓");
            return;
        }

        try
        {
            ProvisionsConfig cfg = ProvisionsConfig.Parse(FileAccess.GetFileAsString(ProvisionsConfig.ResPath));
            ExpeditionContext.EnsureSupplies(cfg);
            GD.Print($"[供应] 已加载 `{ProvisionsConfig.ResPath}`：{cfg.Count} 行（单一真值 = ExpeditionContext.Supplies）✓");
        }
        catch (Exception ex)
        {
            GD.Print($"[供应] 表解析失败（{ex.GetType().Name}）⇒ 买卖机制关闭（如实上报，不静默置灰）：{ex.Message}");
        }
    }

    /// <summary>
    /// 把 11 行商品挂进 `ProvStoreAnchor/.../StoreGrid`（7 列，住在 `ScrollContainer` 之内 ⇒ 需求判据豁免）✓
    /// ⚠️ **不走 `TakeAnchor`**：锚点里住着骨架自带的 `StoreCol/StoreMargin/StoreScroll/StoreGrid/StoreStatus`，
    /// 清锚点会把网格一起清掉 ✓
    /// </summary>
    private void MountProvisionStore(ProvisionSkeleton skel)
    {
        _provCells.Clear();
        if (skel.StoreGrid is not GridContainer grid)
        {
            GD.Print("[UI 供应] 缺 `StoreGrid` 锚点 ⇒ 商店行无处可挂（红线 21 如实上报）");
            return;
        }

        _provStatus = skel.StoreStatus;
        ProvisionStock? stock = ExpeditionContext.Supplies;
        if (stock is null)
        {
            if (LiveProvStatus is { } off)
            {
                off.Text = $"商店：机制关闭（{ProvisionsConfig.ResPath} 未加载）⇒ 不可买卖（不假装可用）✓";
            }

            return;
        }

        foreach (ProvisionConfig p in stock.Lines)
        {
            grid.AddChild(BuildProvisionCell(p));
        }

        RefreshProvisionStore();
        GD.Print($"[UI 供应] 商店已挂 {stock.Lines.Count} 行（网格 = `ProvStoreAnchor/StoreCol/StoreMargin/StoreScroll/StoreGrid`，7 列）✓");
    }

    /// <summary>一行商品（名 ／ 买 ／ 卖 ／ 库存；**竖排** ⇒ 两个 Label 不相交，LayoutAudit 判据 1 绿）✓</summary>
    private Control BuildProvisionCell(ProvisionConfig p)
    {
        var cell = new VBoxContainer
        {
            Name = $"ProvCell_{p.Id}",
            CustomMinimumSize = new Vector2(58, 0),
            TooltipText = ProvisionRowTooltip(p),
        };
        cell.AddThemeConstantOverride("separation", 2);

        var caption = new Label
        {
            Name = "ProvName",
            Text = p.NameZh,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        caption.AddThemeFontSizeOverride("font_size", 11);
        cell.AddChild(caption);

        var buy = new Button
        {
            Name = $"ProvBuy_{p.Id}",
            Text = $"买 {p.BuyGold}",
            CustomMinimumSize = new Vector2(0, 26),
        };
        buy.Pressed += () => BuyProvision(p.Id);
        cell.AddChild(buy);

        var sell = new Button
        {
            Name = $"ProvSell_{p.Id}",
            Text = $"卖 {p.SellGold}",
            CustomMinimumSize = new Vector2(0, 26),
        };
        sell.Pressed += () => SellProvision(p.Id);
        cell.AddChild(sell);

        var have = new Label
        {
            Name = "ProvStock",
            Text = "库存 0",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        have.AddThemeFontSizeOverride("font_size", 11);
        cell.AddChild(have);

        _provCells[p.Id] = (buy, sell, have);
        return cell;
    }

    /// <summary>行 tooltip：价格 + 堆叠上限 + **出征落点**（raid_item 为 null ⇒ 如实说「不进背包」）✓</summary>
    private static string ProvisionRowTooltip(ProvisionConfig p)
    {
        string raid = p.RaidItem is null
            ? "　· 出征**不进背包**（本包只做 food ／ firewood 两种落点）"
            : $"　· 出征时进背包（{p.RaidItem}）";
        return $"{p.NameZh}（{p.Id}）　买 {p.BuyGold} ／ 卖 {p.SellGold} 金　堆叠上限 {p.StackLimit}{raid}";
    }

    /// <summary>按真值刷三处：按钮可用性（置灰 + tooltip 三分支）／ 每行库存 ／ 状态行 ✓</summary>
    private void RefreshProvisionStore()
    {
        ProvisionStock? stock = ExpeditionContext.Supplies;
        if (stock is null)
        {
            return;
        }

        foreach (ProvisionConfig p in stock.Lines)
        {
            if (!_provCells.TryGetValue(p.Id, out (Button Buy, Button Sell, Label Stock) cell))
            {
                continue;
            }

            int have = stock.CountOf(p.Id);
            cell.Buy.Disabled = p.BuyGold <= 0;
            cell.Buy.TooltipText = p.BuyGold <= 0
                ? $"「{p.NameZh}」买价 = 0 ⇒ 置灰（不发明免费商品；红线 21）✓"
                : $"买入 1 件「{p.NameZh}」：{p.BuyGold} 金（堆叠上限 {p.StackLimit}）⇒ 扣金走 Economy.TrySpend ✓";
            cell.Sell.Disabled = p.SellGold <= 0 || have <= 0;
            cell.Sell.TooltipText = p.SellGold <= 0
                ? $"「{p.NameZh}」卖价 = 0 ⇒ 置灰（不发明白送的金币；红线 21）✓"
                : have <= 0
                    ? $"卖出 1 件「{p.NameZh}」：{p.SellGold} 金 —— 城内库存 0 ⇒ 置灰（先买才有得卖）✓"
                    : $"卖出 1 件「{p.NameZh}」：{p.SellGold} 金（城内 {have} 件）⇒ 入账走 Economy.AwardContent ✓";
            cell.Stock.Text = $"库存 {have}";
        }

        if (LiveProvStatus is { } status)
        {
            int gold = ExpeditionContext.Gold?.Gold ?? -1;
            status.Text = $"商店：{stock.Lines.Count} 行 ／ 城内共 {stock.TotalCount} 件 ／ 金币 {gold}　{_provLastTrade}"
                          + "　（城内库存不入档 · 退出即丢 · O-113）";
        }
    }

    /// <summary>买入（**真实入口**：按钮与冒烟都走这里；扣金 = `Economy.TrySpend`，扣不动 ⇒ 拒绝）✓</summary>
    public void BuyProvision(string id, int count = 1) => TradeProvision(id, count, sell: false);

    /// <summary>卖出（**真实入口**；入账 = `Economy.AwardContent`）✓</summary>
    public void SellProvision(string id, int count = 1) => TradeProvision(id, count, sell: true);

    private void TradeProvision(string id, int count, bool sell)
    {
        EnsureSupplies();
        ProvisionStock? stock = ExpeditionContext.Supplies;
        Economy? economy = ExpeditionContext.Gold;
        if (stock is null || economy is null)
        {
            GD.Print($"[供应] 买卖被拒：{(stock is null ? "城内库存未加载" : "经济未就绪")}（不静默）✓");
            return;
        }

        ProvisionTradeResult r = sell
            ? stock.Sell(_log, economy, id, count)
            : stock.Buy(_log, economy, id, count);
        _provLastTrade = r.Describe();
        GD.Print($"[供应] {(sell ? "卖" : "买")}：{_provLastTrade}");
        RefreshProvisionStore();
        Refresh();   // 🔴 不 AutoSave（城内库存不入档 · 退出即丢 · O-113）✓
    }

    /// <summary>冒烟：按 id 触发**真实按钮**的 `Pressed`（置灰 ⇒ 打印 tooltip 原文并拒绝，不绕过置灰）✓</summary>
    public void PressProvisionBuy(string id, int count = 1) => PressProvisionMany(id, count, buy: true);

    /// <summary>冒烟：卖出同上 ✓</summary>
    public void PressProvisionSell(string id, int count = 1) => PressProvisionMany(id, count, buy: false);

    private void PressProvisionMany(string id, int count, bool buy)
    {
        for (int i = 0; i < count; i++)
        {
            if (!PressProvisionCell(id, buy))
            {
                return;   // 缺行 ／ 置灰 ⇒ 停（同一条拒绝不重复打印）✓
            }
        }
    }

    private bool PressProvisionCell(string id, bool buy)
    {
        if (!_provCells.TryGetValue(id, out (Button Buy, Button Sell, Label Stock) cell))
        {
            GD.Print($"[供应] 冒烟按键：没有「{id}」这一行（如实拒绝，不静默）✓");
            return false;
        }

        Button btn = buy ? cell.Buy : cell.Sell;
        if (btn.Disabled)
        {
            GD.Print($"[供应] 冒烟按键：{btn.Name} 已置灰 ⇒ 拒绝（tooltip 原文：{btn.TooltipText}）✓");
            return false;
        }

        btn.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>
    /// 🔴 M12 冒烟族（**全部走真实入口**）：`--hamlet-provision-buy=<id>[:n]` ／ `--hamlet-provision-sell=<id>[:n]`
    /// ⇒ 先 `OpenProvision()`（骨架优先），再逐条按真实按钮 ✓
    /// ⚠️ 位置：`--hamlet-gold=` 播种**之后**（状态行按播种后余额现读）⇒ 挂在 `HandleGearSmokeFlags` 之后 ✓
    /// </summary>
    private void HandleProvisionSmokeFlags(string[] args)
    {
        string? buyArg = FindSmokeArg(args, "--hamlet-provision-buy=");
        string? sellArg = FindSmokeArg(args, "--hamlet-provision-sell=");
        if (buyArg is null && sellArg is null)
        {
            return;
        }

        OpenProvision();
        if (buyArg is not null)
        {
            (string id, int count) = SplitProvisionSpec(buyArg);
            PressProvisionBuy(id, count);
        }

        if (sellArg is not null)
        {
            (string id, int count) = SplitProvisionSpec(sellArg);
            PressProvisionSell(id, count);
        }

        GD.Print($"[供应] 冒烟读数：ProvisionOpen={ProvisionOpen} ／ 城内库存 {ProvisionStockCount} 件 ／ {ProvisionStatusText}");
    }

    /// <summary>`<id>` 或 `<id>:<n>`（冒号后不是正数 ⇒ 按 1 件；不抛）✓</summary>
    private static (string Id, int Count) SplitProvisionSpec(string spec)
    {
        int colon = spec.IndexOf(':');
        if (colon < 0)
        {
            return (spec, 1);
        }

        string id = spec[..colon];
        return int.TryParse(spec[(colon + 1)..], out int n) && n > 0 ? (id, n) : (id, 1);
    }
}

