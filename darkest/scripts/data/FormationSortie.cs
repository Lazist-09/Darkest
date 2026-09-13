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
