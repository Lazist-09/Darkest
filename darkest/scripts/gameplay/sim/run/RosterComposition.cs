using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **名册构成读数**（`m8_verification.md` ㉝）：**在册人数 / 等级分布 / 特质分布 / 疾病 / 锁定特质** ✓
/// 用途：把"养成有没有让**队伍构成**变化"变成**可读**（与等级均值、轮换频率配套 ✓）
/// 纪律：**纯只读**（不改状态、不消耗 RNG、不抛异常打断主流程 —— 纪律 Q）✓
/// </summary>
public static class RosterComposition
{
    /// <summary>一行读数（`[(id)]` 形式便于 grep）✓</summary>
    public static string Describe(Roster roster)
    {
        if (roster is null)
        {
            return "[名册构成] N/A（无名册）";
        }

        var heroes = roster.Heroes;
        var byLevel = heroes.GroupBy(h => h.Level).OrderBy(g => g.Key)
            .Select(g => $"Lv{g.Key}×{g.Count()}");
        int pos = 0, neg = 0, locked = 0, diseases = 0;
        foreach (var h in heroes)
        {
            diseases += roster.DiseasesOf(h.Id).Count;
            foreach (var t in roster.TraitsOf(h.Id))
            {
                if (t.DamagePct > 0 || t.MoraleDamagePct < 0)
                {
                    pos++;
                }
                else if (t.DamagePct < 0 || t.MoraleDamagePct > 0)
                {
                    neg++;
                }

                if (roster.IsTraitLocked(h.Id, t.Id))
                {
                    locked++;
                }
            }
        }

        // 🔴 读数不许误导：headless 下 `CurrentCap` 可能**未设置（= 0）** ⇒ 那就**显示硬上限**并标注 ✓
        string capText = roster.CurrentCap > 0
            ? $"{heroes.Count}/{roster.CurrentCap}（硬上限 {roster.Cap}）"
            : $"{heroes.Count}/{roster.Cap}（硬上限；当前可用上限**未设置**=0 ⇒ 不装作已解锁）";
        return $"[名册构成] 在册 {capText}　等级分布 [{string.Join(" ", byLevel)}]　" +
               $"特质 正 {pos}／负 {neg}（锁定 {locked}）　疾病 {diseases}　" +
               $"经验通道 {(roster.ExperienceWired ? "已接线" : "未接线（不假装生效）")} ✓";
    }
}