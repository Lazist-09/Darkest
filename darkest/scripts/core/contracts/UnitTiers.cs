using System.Text.Json.Serialization;

namespace Darkest.Core.Contracts;

/// <summary>
/// 🔴 **武器阶（原版 `weapon` 0~4 阶）** —— M1a 阶段 1：**只建结构、不切公式** ✓
///
/// 来源（读到的一手结构，`doc/modules/dd1_baseline.md` §27）：
///   `weapon × 5 阶`，字段 = `atk%`（命中修正）· `dmg min max`（**区间**）· `crit%` · `spd`
///   ⚠️ 基准同时标了两处 **待核**：① 我们的 `Attack` 究竟是**命中**还是**伤害** ② `weapon.dmg` 我们**没有对应**
///   ⇒ 所以本类型**只承载数据**：**没有任何消费点**（阶段 1 的铁律：旧读数必须不变）✓
/// </summary>
public sealed record WeaponTier(
    [property: JsonPropertyName("atk_pct")] int AtkPct,
    [property: JsonPropertyName("dmg_min")] int DmgMin,
    [property: JsonPropertyName("dmg_max")] int DmgMax,
    [property: JsonPropertyName("crit_pct")] int CritPct,
    [property: JsonPropertyName("spd")] int Spd);

/// <summary>
/// 🔴 **护甲阶（原版 `armour` 0~4 阶）** —— M1a 阶段 1：**只建结构、不切公式** ✓
///
/// 来源（`dd1_baseline.md` §27）：`armour × 5 阶`，字段 = `def%` · `prot` · `hp` · `spd`
///   ⚠️ 基准标了两处 **待核**：① 我们可能把原版**一个 `def` 拆成了 `PhysDef`＋`Dodge`**（合并**前**要先留命中率基线）
///   ② `armour.prot` 我们**完全没有** ⇒ 阶段 1 只登记字段，**不参与判定** ✓
/// </summary>
public sealed record ArmourTier(
    [property: JsonPropertyName("def_pct")] int DefPct,
    [property: JsonPropertyName("prot")] int Prot,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("spd")] int Spd);
