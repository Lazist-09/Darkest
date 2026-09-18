using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Darkest.Data;

/// <summary>
/// M8.0 ①(c)（`#286` / `blueprint §9.15 归属写死`）：**`formation.json` 从【编成来源】降为【阵型模板】**
/// —— 槽位布局 / 敌方编成 / 障碍仍由它给，**我方出征 6 人改由名册提供**。
///
/// 🔴 约定：**出征 6 人由名册经远征会话提供**（本类只是"把名册选出的 6 人快照套进阵型模板"的工具），
/// **`BattleDirector` 仍单场纯** —— 它只看到"套好的阵型"，不读名册、不持 Hamlet。
/// </summary>
public static class FormationSortie
{
    /// <summary>
    /// 按**模板槽位的原型**从名册选人（每位只用一次）：槽位 1..6 的原型序列 = formation 模板的 `unit`；
    /// 找不到同原型时退化为"名册里下一个没用过的人"。⇒ **保住经典编成，同时实现个体化**（等级/特质各不相同）。
    /// </summary>
    public static IReadOnlyList<HeroConfig> SelectForTemplate(FormationConfig template, RosterConfig roster,
        IReadOnlyList<string>? chosenIds = null)   // 🔴 轮换口子（契约：cap12=出征6+替补6 ⇒ 轮换成为策略）✓
    {
        if (template is null || roster is null)
        {
            throw new ArgumentNullException(template is null ? nameof(template) : nameof(roster));
        }
        // 🔴 **轮换路径**（`chosenIds` 非空）：**按给定名单取人** ⇒ **选人规则由调用方决定**（玩家点选 / 策略 / …）✓
        //    ⚠️ **默认（null）= 行为与以前完全一致** ⇒ 不发明规则、不改变既有路径 ✓
        //    ⚠️ 不合法**如实炸**（不在名册 / 重复 / 人数不等于槽位）⇒ 不静默兜底 ✓
        if (chosenIds is not null)
        {
            var listed = new List<HeroConfig>();
            foreach (string id in chosenIds)
            {
                HeroConfig? one = roster.Heroes.FirstOrDefault(h => h.Id == id);
                if (one is null)
                {
                    throw new InvalidDataException($"{RosterConfig.ResPath}: 指定的出征英雄 \"{id}\" 不在名册里（轮换名单不合法）✓");
                }

                if (listed.Any(x => x.Id == id))
                {
                    throw new InvalidDataException($"{RosterConfig.ResPath}: 出征名单里 \"{id}\" 重复 ✓");
                }

                listed.Add(one);
            }

            if (listed.Count != template.InitialRoster.Player.Count)
            {
                throw new InvalidDataException($"{RosterConfig.ResPath}: 出征名单人数 {listed.Count} ≠ 阵型槽位 {template.InitialRoster.Player.Count} ✓");
            }

            return listed;
        }

        var used = new HashSet<string>();
        var picked = new List<HeroConfig>();
        foreach (RosterEntryConfig slot in template.InitialRoster.Player)
        {
            HeroConfig? hero = roster.Heroes.FirstOrDefault(h => h.Archetype == slot.Unit && !used.Contains(h.Id))
                               ?? roster.Heroes.FirstOrDefault(h => !used.Contains(h.Id));
            if (hero is null)
            {
                throw new InvalidDataException($"{RosterConfig.ResPath}: 名册人数不足以填满阵型模板（M8.0 ①(c) / #286）。");
            }

            used.Add(hero.Id);
            picked.Add(hero);
        }

        return picked;
    }

    /// <summary>
    /// 🔴 **英雄 id → 阵型槽位**（`O-83` 探针口径裁定的产物，`#327`①）：`ExpeditionSession` 的
    /// "按槽位映射"（扎营加成 / 跨场 buff / HP 结转）**只有绑定了阵型才会生效** ——
    /// 而此前**探针从未绑定** ⇒ 那些效果在 V10/A1/A2 读数里全部不生效（架构已把这条落成
    /// `m7_6_verification §1.8` 探针口径清单 + README 红线 17 ⑪）⚠️
    ///
    /// **单一来源**：与 `SelectForTemplate` 同一套选人/槽位顺序 ⇒ 场景与探针**用同一个映射** ✓
    /// </summary>
    public static Dictionary<string, int> HeroSlotMap(FormationConfig template, RosterConfig roster)
    {
        IReadOnlyList<HeroConfig> picked = SelectForTemplate(template, roster);
        IReadOnlyList<RosterEntryConfig> slots = template.InitialRoster.Player;
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < picked.Count && i < slots.Count; i++)
        {
            map[picked[i].Id] = slots[i].Slot;
        }

        return map;
    }

    /// <summary>把阵型模板的我方 6 槽换成给定原型序列（**按槽位序号 1..N 对应列表下标**）。</summary>
    public static FormationConfig WithPlayerSortie(FormationConfig template, IReadOnlyList<string> archetypesBySlot)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (archetypesBySlot is null || archetypesBySlot.Count == 0)
        {
            return template; // 未指定 ⇒ 保持模板原样（向后兼容）
        }

        IReadOnlyList<RosterEntryConfig> slots = template.InitialRoster.Player;
        if (archetypesBySlot.Count != slots.Count)
        {
            throw new InvalidDataException(
                $"{RosterConfig.ResPath}: 出征人数 {archetypesBySlot.Count} 与阵型模板我方槽位 {slots.Count} 不一致（M8.0 ①(c) / #286）。");
        }

        RosterEntryConfig[] entries = slots
            .Select((e, i) => e with { Unit = archetypesBySlot[i] })
            .ToArray();

        return template with
        {
            InitialRoster = template.InitialRoster with { Player = entries },
        };
    }
}
