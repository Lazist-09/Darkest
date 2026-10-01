using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 一次买/卖的**结果**（🔴 **不抛**：余额不足 / 库存不足 / 零价都是**正常拒绝** ⇒ 必须能渲染成一行）✓
/// </summary>
public readonly record struct ProvisionTradeResult(
    bool Ok,
    string Reason,
    string Id,
    int Count,
    int UnitGold,
    int TotalGold,
    int StockAfter)
{
    /// <summary>给日志 / 状态行 / 冒烟断言共用的一行（**单一落点**：不各处再拼一遍）✓</summary>
    public string Describe()
        => Ok
            ? $"[供应] {Id} ×{Count}（单价 {UnitGold} ⇒ 共 {TotalGold}）⇒ 库存 {StockAfter} ✓"
            : $"[供应] {Id} ×{Count} **被拒**（{Reason}）⇒ 不变 ✓";
}

/// <summary>
/// 🔴 **M12 · 城内【供应】库存**（买 / 卖 / 出征时注入本趟背包）。
///
/// <para>口径：① 买 = `Economy.TrySpend`（不足 ⇒ 拒绝且不扣）；② 卖 = `Economy.AwardContent`（正数才入账）；
/// ③ **零价行**（一手 `firewood` / `dog_treats` = 0/0）⇒ 两边都拒绝（`zero_price`，**不发明免费**）；
/// ④ **未知 id / 数量 ≤ 0 / 库存不足** ⇒ 同样**拒绝**（返回理由，不抛 —— 商店 UI 要把它渲染成一行）✓</para>
/// <para>🔴 **不入存档**（用户 2026-10-02 裁定：本包只登记 `O-113`，退出即丢）—— 因此本类**不持有**
/// `SaveSnapshot` 相关接口：它活在本进程的 `ExpeditionContext.Supplies` 里 ✓</para>
/// <para>🔴 **本类零 Godot 引用**（内核纪律）：注入读数以 `IReadOnlyList&lt;string&gt;` 交回调用方打印 ✓</para>
/// </summary>
public sealed class ProvisionStock
{
    private readonly ProvisionsConfig _cfg;
    private readonly Dictionary<string, int> _stock = new(StringComparer.Ordinal);

    public ProvisionStock(ProvisionsConfig config)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>全部商品行（顺序 = 数据文件顺序；UI **不重排、不筛选**，只如实呈现）✓</summary>
    public IReadOnlyList<ProvisionConfig> Lines => _cfg.Provisions;

    /// <summary>某件商品**城内库存**（未知 id ⇒ 0）✓</summary>
    public int CountOf(string id) => _stock.TryGetValue(id, out int n) ? n : 0;

    /// <summary>库存总件数（状态行读数）✓</summary>
    public int TotalCount => _stock.Values.Sum();

    /// <summary>买入 `count` 件：扣金成功才入库存（**单一真值 = 本类的 _stock**）✓</summary>
    public ProvisionTradeResult Buy(CombatLog log, Economy economy, string id, int count = 1)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (economy is null)
        {
            throw new ArgumentNullException(nameof(economy));
        }

        ProvisionConfig? p = _cfg.Find(id);
        if (p is null)
        {
            return new ProvisionTradeResult(false, "unknown_id", id, count, 0, 0, CountOf(id));
        }

        if (count <= 0)
        {
            return new ProvisionTradeResult(false, "bad_count", p.Id, count, p.BuyGold, 0, CountOf(p.Id));
        }

        if (p.BuyGold <= 0)
        {
            return new ProvisionTradeResult(false, "zero_price", p.Id, count, p.BuyGold, 0, CountOf(p.Id));
        }

        int total = p.BuyGold * count;
        if (!economy.TrySpend(log, total, "provision:buy:" + p.Id))
        {
            return new ProvisionTradeResult(false, "not_enough_gold", p.Id, count, p.BuyGold, total, CountOf(p.Id));
        }

        int after = CountOf(p.Id) + count;
        _stock[p.Id] = after;
        return new ProvisionTradeResult(true, "ok", p.Id, count, p.BuyGold, total, after);
    }

    /// <summary>卖出 `count` 件：扣库存成功才入账（**先查库存再动钱** ⇒ 不会出现「给了钱没扣货」）✓</summary>
    public ProvisionTradeResult Sell(CombatLog log, Economy economy, string id, int count = 1)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (economy is null)
        {
            throw new ArgumentNullException(nameof(economy));
        }

        ProvisionConfig? p = _cfg.Find(id);
        if (p is null)
        {
            return new ProvisionTradeResult(false, "unknown_id", id, count, 0, 0, CountOf(id));
        }

        int have = CountOf(p.Id);
        if (count <= 0)
        {
            return new ProvisionTradeResult(false, "bad_count", p.Id, count, p.SellGold, 0, have);
        }

        if (have < count)
        {
            return new ProvisionTradeResult(false, "not_enough_stock", p.Id, count, p.SellGold, 0, have);
        }

        if (p.SellGold <= 0)
        {
            return new ProvisionTradeResult(false, "zero_price", p.Id, count, p.SellGold, 0, have);
        }

        int total = p.SellGold * count;
        if (economy.AwardContent(log, total, "provision:sell:" + p.Id) <= 0)
        {
            return new ProvisionTradeResult(false, "rejected_by_economy", p.Id, count, p.SellGold, total, have);
        }

        int after = have - count;
        if (after == 0)
        {
            _stock.Remove(p.Id);
        }
        else
        {
            _stock[p.Id] = after;
        }

        return new ProvisionTradeResult(true, "ok", p.Id, count, p.SellGold, total, after);
    }

    /// <summary>
    /// 🔴 把城内库存**注入本趟背包**（`ExpeditionComposition` 在 `bag.LockForRun()` **之前**调用）：
    /// · 无货 ⇒ **零操作零打印**（空列表）；
    /// · 逐件 `bag.TryAdd`，**成功才从城内扣**；包满 ⇒ 余量**留在城内**并如实打印（不静默丢弃）✓
    /// · raid_item = null 的行**不进背包**（本包只做三种落点：food ⇒ Food / firewood ⇒ Firewood）✓
    /// </summary>
    /// <returns>逐行读数（调用方打印；本类零 Godot 引用）✓</returns>
    public IReadOnlyList<string> InjectInto(Inventory bag)
    {
        if (bag is null)
        {
            throw new ArgumentNullException(nameof(bag));
        }

        var lines = new List<string>();
        if (TotalCount == 0)
        {
            return lines;
        }

        foreach (ProvisionConfig p in _cfg.Provisions)
        {
            int have = CountOf(p.Id);
            if (have <= 0 || p.RaidItem is null)
            {
                continue;
            }

            ItemKind kind = p.RaidItem == ProvisionsConfig.RaidItemFood ? ItemKind.Food : ItemKind.Firewood;
            int injected = 0;
            for (int i = 0; i < have; i++)
            {
                if (!bag.TryAdd(new InventoryItem(kind, p.Id + "_" + (i + 1)), out string reason))
                {
                    lines.Add($"[供应注入] {p.Id} 第 {i + 1}/{have} 件未落包（{reason}）⇒ 余量留城内（不静默丢弃）✓");
                    break;
                }

                injected++;
            }

            if (injected > 0)
            {
                int left = have - injected;
                if (left == 0)
                {
                    _stock.Remove(p.Id);
                }
                else
                {
                    _stock[p.Id] = left;
                }

                lines.Add($"[供应注入] {p.Id} ×{injected}（{kind}）⇒ 背包里 {kind} ×{bag.CountOf(kind)}　库存余 {left} ✓");
            }
        }

        return lines;
    }
}
