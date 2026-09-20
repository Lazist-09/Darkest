using System;
using System.Collections.Generic;

namespace Darkest.Core.Contracts;

/// <summary>
/// 单位静态属性模板（data_schema §3.1 units.json 逐字段映射；百分比存整数百分数）。
/// 运行期实例从模板复制基础值（blueprint §7：运行时模板【只读】，绝不在模板上写回）。
/// </summary>
public sealed record UnitStats(
    int Hp,
    int Attack,
    int PhysDef,
    int Speed,
    int Dodge,
    int Crit,
    int Resilience,
    int StunResist,
    int BleedResist,
    int StatDebuffResist,
    int DisplaceResist,
    int? DeathsDoorResist,
    // 🆕 **M1a · 抗性 5→8（加字段级）**：null = **未配**（不假装 0）✓
    //   🔴 判定路径照 `EffectsStep` 既有 switch 同形接；**数值由策划给**（原版口径），我不发明 ✓
    //   ⚠️ 陷阱那条已有自证（`TrapResistSourceDeclared`：未声明 ⇒ 退化解 0 且**会打印**）✓
    int? PoisonResist = null,
    int? DiseaseResist = null,
    int? TrapResist = null,
    // 🆕 **M1a · 补 `prot`（护甲值）**：**百分比整数（0~85）**，null = 未配（不假装 0）✓
    //   🔴 语义来源（参考项目 `Character.cs`，一手）：
    //      `Protection = Mathf.Clamp(prot.ModifiedValue, -1, Mathf.Max(0.85f, prot.RawValue))`
    //      ⇒ **prot 是 0~0.85 的【比例/减伤百分比】，封顶 85%**（**不是固定减伤**）✓
    int? Prot = null,
    int MoveDistance = 0,
    // 🆕 **M1a 阶段 1**：原版 weapon/armour 各 5 阶（**只承载数据，零消费点**）✓
    IReadOnlyList<WeaponTier>? WeaponTiers = null,
    IReadOnlyList<ArmourTier>? ArmourTiers = null)
{
    public bool HasDeathsDoor => DeathsDoorResist is not null;

    /// <summary>池外「移动」的射程（自身 ±N 格换位；F1/#191 从单位读，不再写在技能上）。</summary>
    public int MovementRange => MoveDistance;

    /// <summary>
    /// 🆕 **M1a 阶段 1 的"按阶取"**：`tier` ∈ 0..4（原版 0~4）⇒ 返回该阶；越界或未配 tier ⇒ **null**（如实不假装）✓
    /// 🔴 **本阶段无人消费它**（切换公式属 M1c 阶段 3）✓
    /// </summary>
    public WeaponTier? WeaponAt(int tier)
        => WeaponTiers is not null && tier >= 0 && tier < WeaponTiers.Count ? WeaponTiers[tier] : null;

    /// <summary>🆕 同上（护甲阶）✓</summary>
    /// <summary>
    /// 🆕 **M1a · 护甲减伤（比例）**：`Prot` 是百分比整数（0~85）⇒ 返回 0~0.85 的比例；未配 ⇒ **null**（不假装 0）✓
    /// 🔴 上限 0.85 的来源 = 参考项目 `Character.cs` 的 `Mathf.Max(0.85f, raw)` 钳制 ✓
    /// ⚠️ **本阶段没有消费点**（接结算属后续）⇒ 加它不改变任何读数 ✓
    /// </summary>
    public double? ProtFraction => Prot is { } p ? System.Math.Clamp(p / 100.0, 0.0, 0.85) : null;

    public ArmourTier? ArmourAt(int tier)
        => ArmourTiers is not null && tier >= 0 && tier < ArmourTiers.Count ? ArmourTiers[tier] : null;
}