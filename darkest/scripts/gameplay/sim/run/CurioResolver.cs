using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 一次 Curio 交互的**结果**（`curio.md` §4/§5）：
/// `Route` ∈ {{bare, item, leave}}；`Text` = **描述文本**（V6）；`Deferred` = 该分支**未接线**（阶段二）。
/// </summary>
public sealed record CurioOutcome(
    string CurioId,
    string Route,
    string Kind,
    int Amount,
    string Text,
    string? ExtraKind = null,
    int ExtraAmount = 0,
    bool Deferred = false,
    string? ItemUsed = null);

/// <summary>
/// 🔴 **Curio 的解析内核**（`doc/modules/curio.md` / `#313`）—— **零 Godot 依赖**（内核层）。
///
/// 三条纪律（契约原文）：
/// ① **空手路径：掷骰并【必须写 `RngDraw`】**（`#313` ⑤ / V5）
/// ② 🔴 **道具路径：按道具【直查】确定结果，【不掷骰】⇒ 不写 `RngDraw`**（Curio 的灵魂：
///    "正确道具 ⇒ 100% 确定的好结果"；"用错道具 ⇒ 确定的坏结果"）
/// ③ **可以走开**（`#313` ③：`#258`「不许跳过」作废）⇒ 走开**零变化、不掷骰、不阻塞通行**（V4）
/// ④ 命中**未接线（阶段二）**的 kind ⇒ **显式拒绝**（`Deferred: true`），绝不静默当作成功（红线 21）
/// </summary>
public static class CurioResolver
{
    /// <summary>🔴 **道具不被接受**（数据里没有该道具的结果项）⇒ 调用方应拒绝该选项（V7：UI 只列已实现道具）。</summary>
    public static CurioOutcome? ResolveItem(CurioConfig curio, string itemUsed)
    {
        CurioItemResultConfig? r = curio.ItemResults?
            .FirstOrDefault(x => string.Equals(x.Item, itemUsed, StringComparison.Ordinal));
        if (r is null)
        {
            return null; // 该 Curio 对这个道具没有定义 ⇒ 无效选项（不掷骰、无变化）
        }

        return new CurioOutcome(curio.Id, "item", r.Kind, r.Amount, r.Text,
            r.ExtraKind, r.ExtraAmount,
            Deferred: CuriosConfig.DeferredKinds.Contains(r.Kind), ItemUsed: itemUsed);
    }

    /// <summary>🔴 **空手**：按 `bare_hands` 累计概率掷骰（**写 `RngDraw`**）。</summary>
    public static CurioOutcome ResolveBare(CurioConfig curio, IRngProvider rng, CombatLog log)
    {
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 V5：空手必须留痕（可审计、可复现）

        int acc = 0;
        foreach (CurioBareResultConfig b in curio.BareHands)
        {
            acc += b.Chance;
            if (roll < acc)
            {
                return new CurioOutcome(curio.Id, "bare", b.Kind, b.Amount, b.Text,
                    Deferred: CuriosConfig.DeferredKinds.Contains(b.Kind));
            }
        }

        // 概率和 = 100 已由加载校验保证（`CuriosConfig.Parse`）⇒ 正常到不了这里；
        // 但**仍不静默**：显式抛出，避免"掷到没定义的分支却拿到空结果"。
        throw new InvalidOperationException(
            $"Curio \"{curio.Id}\" 的空手结果表没覆盖 roll={roll}（概率之和应恰 100，见 CuriosConfig 加载校验）。");
    }

    /// <summary>🔴 **走开**：零变化、不掷骰、不阻塞（V4）。返回一条 `Route=leave` 的结果（供 UI 显示文本）。</summary>
    public static CurioOutcome Leave(CurioConfig curio)
        => new(curio.Id, "leave", "none", 0, "你决定不碰它，继续往前走。");
}
