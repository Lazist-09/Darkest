using System;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// E3 扎营数学（纯函数，供流程与门禁共用；不持有状态、不掷骰）：
/// · **口粮需求按存活人数等比缩放**（照抄 DD"每减员 −1/4"精神）：`ceil(档位基准 × 存活人数 ÷ 满编 6)`
/// · **Respite = respite_base + 存活人数**（满编 6 人 = 12；减员后变少 → 天然惩罚减员）
/// · 四档效果：Starve 全队 −20% HP / −15 士气；Half 无；Full 全队 +10% HP；Feast 全队 +25% HP / +10 士气
/// </summary>
public static class ExpeditionCampMath
{
    /// <summary>满编人数（口粮缩放的基准；与 formation 的 6 人编成一致）。</summary>
    public const int FullRoster = 6;

    /// <summary>该档位在**当前存活人数**下实际需要的口粮。</summary>
    public static int FoodRequired(TuningFoodTiers tiers, string tier, int survivors)
    {
        int baseValue = BaseFood(tiers, tier);
        int alive = Math.Clamp(survivors, 0, FullRoster);
        return baseValue == 0 ? 0 : (int)Math.Ceiling(baseValue * alive / (double)FullRoster);
    }

    /// <summary>档位基准口粮（starve 0 / half 3 / full 6 / feast 12）。</summary>
    public static int BaseFood(TuningFoodTiers tiers, string tier) => tier switch
    {
        "half" => tiers.Half,
        "full" => tiers.Full,
        "feast" => tiers.Feast,
        "starve" => tiers.Starve,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), $"未知食物档位 \"{tier}\"（E3）。"),
    };

    /// <summary>
    /// 档位效果：HP 百分比增量（正负）与士气增量。
    /// 🔴 数字外置（P29）：四档效果**来自 `tuning.camp.food_effects`**（原先硬写在本方法的 switch 里）✓
    /// </summary>
    public static (double HpPercent, int Morale) FoodEffect(Darkest.Data.TuningFoodEffects effects, string tier)
    {
        Darkest.Data.TuningFoodEffect e = tier switch
        {
            "starve" => effects.Starve,
            "half" => effects.Half,
            "full" => effects.Full,
            "feast" => effects.Feast,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), $"未知食物档位 \"{tier}\"（E3）。"),
        };
        return (e.HpPercent, e.Morale);
    }

    /// <summary>Respite 点数池 = 基准 + 存活人数（满编 12；死 2 人 → 10）。</summary>
    public static int RespitePool(int respiteBase, int survivors) => respiteBase + Math.Max(0, survivors);
}
