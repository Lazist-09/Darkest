# 参考项目战斗逻辑（Darkest-Dungeon-Unity）对齐报告

参考项目根：`F:\GithubPro\Darkest-Dungeon-Unity\`（GPL）。本报告所有路径均相对 `Assets/Scripts/`。
本报告**只读**参考项目；所有结论均给出实现点行号，凡属推断均显式标 `推断`。
行号基于本次阅读时的文件内容。

## 0. 阅读范围与"谁是核心"

| 关注点 | 实现点 |
|---|---|
| 战斗结算主链路（命中/伤害/暴击/治疗） | `Mechanics/Battle/BattleSolver.cs`（全文 499 行，核心 302-424） |
| 属性取值与钳制 | `Character/Character.cs`（66-180）、`Character/Attribute.cs` |
| 抗性判定 | `Mechanics/Skills/Effects/*.cs`（每个 SubEffect 自己 roll） |
| 回合顺序 | `Mechanics/Battle/Round.cs` |
| 死门/心衰/压力 | `Managers/RaidSceneManager.cs`、`Character/Hero.cs` |
| 位置/位移 | `Raid/Party/FormationUnit.cs`、`Mechanics/Battle/FormationSet.cs` |
| buff/状态 | `Character/Buff.cs`、`Character/BuffInfo.cs`、`Character/Statuses/*.cs` |

---

## 1. 伤害公式（从"技能要打人"到"扣 HP"）

### 1.1 结论（逐步骤）

全部发生在一个函数里：`BattleSolver.ExecuteSkill(performerUnit, targetUnit, skill, artInfo)`。
变量名保持原文；`performer` = `performerUnit.Character`，`target` = `targetUnit.Character`。

```text
步骤 0  ApplyConditions(performerUnit, targetUnit, skill)       // 结算前先刷条件 buff（vs marked / vs unholy 等）
步骤 1  命中判定（见 §2）—— 未命中则 ApplyEffects() 后 return，不进伤害
步骤 2  基础伤害 initialDamage （float，不取整）
        英雄:   Lerp(performer.MinDamage, performer.MaxDamage, RandomSolver.NextDouble())
                * (1 + skill.DamageMod)
        怪物:   Lerp(skill.DamageMin, skill.DamageMax, RandomSolver.NextDouble())
                * performer.DamageMod
步骤 3  护甲（prot）在此步、以乘算形式参与：
        damage = CeilToInt(initialDamage * (1 - target.Protection))
        if (damage < 0) damage = 0
        若 target.BattleModifiers.CanBeDamagedDirectly == false → damage = 0
步骤 4  暴击判定（见 §3）：暴击则 target.TakeDamage(damage * 1.5f) 并 return
步骤 5  非暴击：damage = target.TakeDamage(damage)
步骤 6  TakeDamage 内部：damage = RoundToInt(damageAmount); HitPoints.DecreaseValue(damage)
        DecreaseValue: currentValue = Clamp(currentValue - amount, 0, ModifiedBaseValue)
步骤 7  HP 变 0 → PrepareDamage/PrepareDeath 路径（见 §6）
```

**要点**
- 取整时机共 **2 次**：`prot` 之后立即 `CeilToInt`（向上取整，§1.2），`TakeDamage` 里 `RoundToInt`（四舍五入）。
- 爆击倍率作用在**已扣护甲的整数伤害**上（`damage * 1.5f`），再 `RoundToInt`。
- 下限：护甲后 `damage < 0 → 0`。因为用了 `CeilToInt`，只要 `initialDamage > 0` 结果最小就是 1（**没有显式的"最小 1 点"钳制**，是 ceil 的副作用）。`damage == 0` 只在 `initialDamage == 0` 或 `CanBeDamagedDirectly == false` 时出现（UI 里显示 `PopupMessageType.ZeroDamage`，见 `RaidSceneManager.cs:3895`）。
- 上限：伤害本身无上限；`prot` 无"最大减伤 85%"以外的钳制（`Protection` 上限 = `Max(0.85, RawValue)`，见 §1.3）。
- **`DmgReceivedPercent`（damage_received_percent）被解析、被登记为属性，但在伤害结算里从未被使用**（grep 全库仅出现于 `Character.cs:214` 的 Modifiers 数组、`CharacterHelper.cs:35/108/205` 的字符串映射、`Buff.cs:54` 的正负判定）。→ 参考项目里"受到伤害 +X%"是死属性。

### 1.2 依据

- `Mechanics/Battle/BattleSolver.cs:383-392`
```csharp
float initialDamage = performer is Hero ?
    Mathf.Lerp(performer.MinDamage, performer.MaxDamage, (float)RandomSolver.NextDouble()) * (1 + skill.DamageMod) :
    Mathf.Lerp(skill.DamageMin, skill.DamageMax, (float)RandomSolver.NextDouble()) * performer.DamageMod;

int damage = Mathf.CeilToInt(initialDamage * (1 - target.Protection));
if(damage < 0)
    damage = 0;
```
- `Mechanics/Battle/BattleSolver.cs:391-392`（`CanBeDamagedDirectly == false → damage = 0`）
- `Character/Character.cs:1107-1112`
```csharp
public int TakeDamage(float damageAmount)
{
    int damage = Mathf.RoundToInt(damageAmount);
    GetPairedAttribute(AttributeType.HitPoints).DecreaseValue(damage);
    return damage;
}
```
- `Character/Attribute.cs:117-123`（`DecreaseValue` 钳 `[0, ModifiedBaseValue]`）
- 英雄武器基础伤害来源（MinDamage/MaxDamage 的 RawValue）：`Character/HeroClass.cs:107-114`
```csharp
case "weapon:":
    Equipment weapon = new Equipment(data[2], Weapons.Count + 1, HeroEquipmentSlot.Weapon);
    weapon.EquipmentModifiers.Add(new FlatModifier(AttributeType.DamageLow,  float.Parse(data[6]), false));
    weapon.EquipmentModifiers.Add(new FlatModifier(AttributeType.DamageHigh, float.Parse(data[7]), false));
```
→ 英雄的 `.dmg` 是**技能倍率**（`CombatSkill.cs:283-290`，`DamageMod = .dmg / 100`）；怪物的 `.dmg` 是**技能固定区间**（`DamageMin/DamageMax`），怪物自身的伤害倍率来自 `DamageHigh` 的 `Multiplier`（`Character.cs:150-160`）。

### 1.3 🔴 `prot` 与 `def` 分别在哪一步、什么形式？原版只有一个 def 吗？

**结论：只有一个 `def`，且 `def` 不是护甲减伤，而是闪避（dodge）。**

- `def` = `AttributeType.DefenseRating` → 属性名 `Character.Dodge`，**只参与命中判定**（`hitChance = accuracy - Dodge`），**完全不参与伤害数值**。
- `prot` = `AttributeType.ProtectionRating` → 属性名 `Character.Protection`，**只参与伤害数值**（`damage = ceil(initialDamage * (1 - prot))`），**完全不参与命中**。
- 数据层也确实只有这两个字段，没有第三个"护甲减伤"字段：

`Mechanics/MechanicsDefines.cs:27-28`
```csharp
DefenseRating,
ProtectionRating,
```
`Character/Character.cs:205-209`（单值战斗属性白名单）
```csharp
private static readonly AttributeType[] SingleStats = new AttributeType[]
{
    AttributeType.DefenseRating, AttributeType.ProtectionRating, AttributeType.SpeedRating,
    AttributeType.AttackRating, AttributeType.CritChance, AttributeType.DamageLow, AttributeType.DamageHigh,
};
```
字符串映射：`"defense_rating" → DefenseRating`、`"protection_rating" → ProtectionRating`（`Character/Utils/CharacterHelper.cs:11-12`）。
数据解析（⚠️ 注意两边的单位约定不同）：

`Database/DarkestDatabase.cs:2154-2164`
```csharp
case "stats:":
    monsterData.Attributes.Add(AttributeType.HitPoints, float.Parse(data[2]));
    monsterData.Attributes.Add(AttributeType.DefenseRating,    float.Parse(data[4]) / 100);
    monsterData.Attributes.Add(AttributeType.ProtectionRating, float.Parse(data[6]));      // 不除 100
    monsterData.Attributes.Add(AttributeType.SpeedRating,      float.Parse(data[8]));
    monsterData.Attributes.Add(AttributeType.Stun, float.Parse(data[10]) / 100);
    ...
```
英雄侧：护甲 `data[4] / 100 → DefenseRating`（`HeroClass.cs:115-120`）；英雄**没有基础 prot 字段**，prot 只能靠 buff/技能获得（`HeroClass.cs:97-120` 里无 ProtectionRating）。
→ 结论：怪物数据里 `def` 写成整数百分比、`prot` 写成小数；英雄 `def` 写成整数百分比、`prot` 无基础值。（推断：这解释了为什么同一份 DD1 数据里两个字段的写法不一致。）

**钳制（很重要，DD1 的封顶是在这里做的）**：`Character/Character.cs:102-124`
```csharp
public float Dodge       { get { var d = GetSingleAttribute(AttributeType.DefenseRating);   return d != null ? Mathf.Clamp(d.ModifiedValue, 0, Mathf.Max(3, d.RawValue)) : 0; } }
public float Protection  { get { var p = GetSingleAttribute(AttributeType.ProtectionRating); return p != null ? Mathf.Clamp(p.ModifiedValue, -1, Mathf.Max(0.85f, p.RawValue)) : 0; } }
```
- `prot` ∈ [-1, max(0.85, raw)]：默认最多减伤 85%；`prot < 0` 会**放大**伤害（`1 - (-0.5) = 1.5`）。
- `dodge` ∈ [0, max(3, raw)]：闪避可以被 buff 堆到很大（钳制上限 3.0 而不是 0.95）。
- `Accuracy` ∈ [-1, 2]（`Character.cs:90-100`）。
- `MinDamage` ∈ [0, +∞)，`MaxDamage` ∈ [MinDamage, +∞)（`Character.cs:126-148`）。

---

## 2. 命中判定

### 2.1 结论

```text
accuracy  = skill.Accuracy + performer.Accuracy        // 都是 0~1 小数；performer.Accuracy 已钳 [-1,2]
hitChance = Clamp(accuracy - target.Dodge, 0, 0.95)     // 命中率上限 95%，下限 0
roll      = RandomSolver.NextDouble()                   // [0,1)
若 target.CanBeHit == false  → roll = float.MaxValue（强制不命中）

未命中分支（roll > hitChance）：
  若 (skill.CanMiss == false) 或 (target.CanBeMissed == false) → 不判未命中，直接继续走伤害
  否则 roll > Min(accuracy, 0.95) ? Miss : Dodge
  两种都走 ApplyEffects() 然后 return（没有伤害）
```

- **判定顺序：先命中，后暴击。** 命中失败直接 return，暴击**不会**被 roll（`BattleSolver.cs:369-381` 在 `394-412` 之前）。
- `Miss` 与 `Dodge` 的区别：若未命中的"缺口"能归因于闪避（`roll ≤ accuracy`）则记 `Dodge`，否则记 `Miss`。→ 当 `accuracy ≥ 0.95` 时（上限情形）所有未命中都会显示为 `Dodge`；只有在 `accuracy < roll` 时才会出现 `Miss`。
- 命中率**不受暴击影响**（`CritDoesntApplyToRoll` 参数被解析但未使用，见 §10）。
- 未命中时**效果仍会执行**（`ApplyEffects` 被调用）；是否真的落地由每个 Effect 的 `on_miss` 决定（默认**会**落地，见 §10）。

### 2.2 依据

`Mechanics/Battle/BattleSolver.cs:363-381`
```csharp
float accuracy = skill.Accuracy + performer.Accuracy;
float hitChance = Mathf.Clamp(accuracy - target.Dodge, 0, 0.95f);
float roll = (float)RandomSolver.NextDouble();
if (target.BattleModifiers != null && target.BattleModifiers.CanBeHit == false)
    roll = float.MaxValue;

if (roll > hitChance)
{
    if (!(skill.CanMiss == false || (target.BattleModifiers != null && target.BattleModifiers.CanBeMissed == false)))
    {
        if (roll > Mathf.Min(accuracy, 0.95f))
            SkillResult.AddResultEntry(new SkillResultEntry(targetUnit, SkillResultType.Miss));
        else
            SkillResult.AddResultEntry(new SkillResultEntry(targetUnit, SkillResultType.Dodge));
        ApplyEffects(performerUnit, targetUnit, skill);
        return;
    }
}
```
`Mechanics/RandomSolver.cs:83-89`（`CheckSuccess` 语义 = `chance >= 1 恒真`，否则 `NextDouble() < chance`）
```csharp
public static bool CheckSuccess(float chance)
{
    if (chance >= 1)
        return true;
    return random.NextDouble() < chance;
}
```
技能精度来源：`Mechanics/Skills/CombatSkill.cs:280-282`（`.atk` / 100）。注意 `CombatSkill.cs:399-405`：**`Accuracy == 0` 的技能会被归为 `Support`/`Heal` 类别**，从而完全不走命中判定（`BattleSolver.cs:320` 分支）。

---

## 3. 暴击

### 3.1 结论

- 判定：`critChance = performer.CritChance.ModifiedValue + skill.CritMod`，**没有 clamp**（`CritChance.ModifiedValue` 本身是 raw+flat 再乘 Multiplier；`skill.CritMod = .crit / 100`）。`CheckSuccess` 保证 `≥1` 必暴。
- 倍率：**1.5 倍，作用在已扣护甲的伤害上**，`damage * 1.5f` 之后由 `TakeDamage` → `RoundToInt` 取整。→ **暴击伤害同样被护甲削减**（护甲先算，暴击后乘）。
- 治疗暴击：`initialHeal * 1.5f` → `Heal()` → `CeilToInt(healAmount * (1 + HpHealReceivedPercent))`。
- 附：**治疗的基础量是"整数均匀 roll"**，与伤害的连续 `Lerp` 不同：`initialHeal = RandomSolver.Next(skill.Heal.MinAmount, skill.Heal.MaxAmount + 1) * (1 + performer.HpHealPercent)`（`BattleSolver.cs:326-327`），且治疗不判命中、不受 prot/dodge 影响。
- 附加效果：暴击命中英雄目标时额外施加数据表里的 `"Stress 2"` 效果；暴击治疗时对非怪物目标额外施加 `"crit_heal_stress_heal"`。
- 暴击**不改变**命中率、不改变效果触发概率。
- 暴击只在**命中之后** roll（与 §2 一致）。
- `skill.IsCritValid == false` 时完全不 roll 暴击。

### 3.2 依据

`Mechanics/Battle/BattleSolver.cs:394-412`
```csharp
if (skill.IsCritValid)
{
    float critChance = performer.GetSingleAttribute(AttributeType.CritChance).ModifiedValue + skill.CritMod;
    if (RandomSolver.CheckSuccess(critChance))
    {
        int critDamage = target.TakeDamage(damage * 1.5f);   // damage 已含 prot
        ...
        if (targetUnit.Character.IsMonster == false)
            DarkestDungeonManager.Data.Effects["Stress 2"].ApplyIndependent(targetUnit);
        return;
    }
}
```
`Mechanics/Skills/CombatSkill.cs:292-294`（`.crit` → `CritMod = value / 100`）
`Mechanics/Battle/BattleSolver.cs:329-342`（治疗暴击 1.5 倍 + `crit_heal_stress_heal`）
`Character/Character.cs:1092-1100`（`Heal` 用 `CeilToInt` 且乘 `HpHealReceivedPercent`）

---

## 4. 抗性

### 4.1 清单（原版 8 种，怪物只有 5 种）

`Character/Character.cs:227-238`
```csharp
private static readonly AttributeType[] HeroResistances = new AttributeType[]
{
    AttributeType.Stun, AttributeType.Poison, AttributeType.Disease,
    AttributeType.DeathBlow, AttributeType.Move, AttributeType.Bleed,
    AttributeType.Debuff, AttributeType.Trap,
};

private static readonly AttributeType[] MonsterResistances = new AttributeType[]
{
    AttributeType.Stun, AttributeType.Poison, AttributeType.Move,
    AttributeType.Bleed, AttributeType.Debuff,
};
```
→ **英雄 8 种**：Stun / Poison / Disease / DeathBlow / Move / Bleed / Debuff / Trap。
→ **怪物 5 种**：Stun / Poison / Move / Bleed / Debuff（没有 Disease / DeathBlow / Trap；怪物不生病、不检定死门、不拆陷阱）。
怪物抗性直接取 `monsterData.Attributes[...]`（`Character.cs:303-306`），英雄抗性 = 职业基础值 + `level * 0.1`（**死门抗性不加等级成长**，`Character.cs:279-286`、`1203-1210`）。
英雄职业数据：`HeroClass.cs:97-106`（顺序：stun poison bleed disease move debuff death_blow trap，全部 `/100`）。

### 4.2 统一判定形态

除 Disease / DeathBlow / Trap 外，全部是同一模板（**线性相减，无递减、无非线性**）：

```text
chance = (effect.IntegerParams[Chance].HasValue ? Chance/100 : 1)
       - target.GetSingleAttribute(<对应抗性>).ModifiedValue
       + (performer 是英雄 ? performer.<对应>Chance : 0)      // 只有英雄有进攻方加成
chance = Clamp(chance, 0, 0.95)
成功 = RandomSolver.CheckSuccess(chance)
```
- 注意：**只有英雄作为施法者时才有 `XChance` 加成**（`performer.Character is Hero`）；怪物没有 `StunChance/BleedChance/...` 加成，哪怕数据里有。
- 概率上限一律 **0.95**（不存在 100% 上状态，除非 `CheckSuccess(≥1)` 的旁路）。
- 自身对自己（`performer == target`）时 debuff 概率被强制为 1（`BuffEffect.cs:64`、`CombatStatBuffEffect.cs:135`、`ControlEffect.cs:28`）。

| 抗性 | 实现点 | 公式差异 |
|---|---|---|
| Stun | `Effects/StunEffect.cs:16-24` | `-Stun`，`+StunChance`，Clamp[0,0.95] |
| Bleed | `Effects/BleedEffect.cs:18-25` | `-Bleed`，`+BleedChance`，Clamp[0,0.95] |
| Poison | `Effects/PoisonEffect.cs:18-25` | `-Poison`，`+PoisonChance`，Clamp[0,0.95] |
| Move | `Effects/PushEffect.cs:18-25`、`Effects/PullEffect.cs` | `-Move`，`+MoveChance`，Clamp[0,0.95] |
| Debuff | `Effects/BuffEffect.cs:57-64`、`Effects/CombatStatBuffEffect.cs:127-138` | `-Debuff`，`+DebuffChance`，Clamp[0,0.95] |
| Disease | `Effects/DiseaseEffect.cs:18-25` | **两段 roll**；`chance2 = 1 - Disease`（**没有 Clamp[0,0.95]，没有进攻方加成**） |
| DeathBlow | `Character/Hero.cs:32-39` | 见 §6，`Clamp(DeathBlow, 0, 0.87)` |
| Trap | `Managers/RaidSceneManager.cs:5587` | 非战斗（拆陷阱），`GetSingleAttribute(Trap).ModifiedValue` |

`Effects/StunEffect.cs:16-26`
```csharp
float stunChance = effect.IntegerParams[EffectIntParams.Chance].HasValue ?
    (float)effect.IntegerParams[EffectIntParams.Chance].Value / 100 : 1;
stunChance -= target.Character.GetSingleAttribute(AttributeType.Stun).ModifiedValue;
if (performer != null && performer.Character is Hero)
    stunChance += performer.Character.GetSingleAttribute(AttributeType.StunChance).ModifiedValue;
stunChance = Mathf.Clamp(stunChance, 0, 0.95f);
if (RandomSolver.CheckSuccess(stunChance)) { stunStatus.StunApplied = true; ... }
```
`Effects/DiseaseEffect.cs:18-25`
```csharp
float diseaseTriggerChance = effect.IntegerParams[EffectIntParams.Chance].HasValue ?
    (float)effect.IntegerParams[EffectIntParams.Chance].Value / 100 : 1;
if (!RandomSolver.CheckSuccess(diseaseTriggerChance)) return false;
float diseaseChance = 1 - target.Character.GetSingleAttribute(AttributeType.Disease).ModifiedValue;
```

### 4.3 是否有非线性/递减？

- **抗性数值本身线性相减**，没有递减公式。
- 唯一的"递减/叠加"机制是 **眩晕恢复**：每次被眩晕后，单位会获得 N 层 `STUNRECOVERYBUFF`（每层提升眩晕抗性），层数随已被眩晕次数累加（`Character/Character.cs:348-363`）：
```csharp
public void ApplyStunRecovery()
{
    var recoveryBuff = DarkestDungeonManager.Data.Buffs["STUNRECOVERYBUFF"];
    int recoveryStackCount = 0;
    for (int i = 0; i < BuffInfo.Count; i++)
        if (BuffInfo[i].Buff == recoveryBuff) recoveryStackCount++;
    recoveryStackCount++;
    for (int i = 0; i < recoveryStackCount; i++)
        AddBuff(new BuffInfo(recoveryBuff, BuffDurationType.Round, BuffSourceType.Adventure, 2));
}
```
调用点：单位回合开始处理眩晕时（`RaidSceneManager.cs:2860`、`3499`、`2773`）。
- DoT（Bleed/Poison）不参与抗性递减，每个实例独立计时（§9）。

---

## 5. 回合顺序 / 速度

### 5.1 结论

每个**大轮**开始时一次性排序（`Round.NextRound`）：

```text
pool = 全部英雄（每个 1 条）+ 全部怪物（每个重复 Initiative.NumberOfTurns 次，NumberOfTurns 默认 0 → 该怪整轮不行动）
score(unit) = unit.Character.Speed + RandomSolver.Next(0, 3) + RandomSolver.NextDouble()
OrderedUnits = pool.OrderByDescending(score)          // 降序
```
- **速度参与方式**：`Speed` 是加数（`Character.Speed`，钳 `[0, +∞)`，`Character.cs:66-76`）；随机项 = 整数 `[0,2]` 加上一个连续 `[0,1)` 双精度小数。
- **有随机项**，且随机项幅度（0~3）相对速度值（通常 1~10）**非常大**——速度主要是概率优势而不是硬序。
- **平手**：靠连续 `NextDouble()` 几乎不可能完全相等；若完全相等，`OrderByDescending` 是稳定排序，保持插入顺序（先英雄列表顺序、后怪物列表顺序）。
- **首轮惊喜（Surprise）**：`RoundNumber == 0` 时按 `BattleGround.SurpriseStatus` 处理——`HeroesSurprised` → 英雄分数 `-100`；`MonstersSurprised` → 可被惊喜的怪物分数 `-100`；否则正常。
- 排序后**本轮内不再重排**：单位开始回合时把自己从 `OrderedUnits` 里 `Remove`（`Round.cs:34/62`）。速度 buff 在同一轮内不会改变顺序（下一轮生效）。
- 中途加入（召唤）：`InsertInitiativeRoll` 把单位插到"第一个速度比他低 2 以上"的单位之前（`Round.cs:156-170`）。

### 5.2 依据

`Mechanics/Battle/Round.cs:133-153`
```csharp
if(RoundNumber == 0)
{
    if (RaidSceneManager.BattleGround.SurpriseStatus == SurpriseStatus.HeroesSurprised)
        OrderedUnits = new List<FormationUnit>(OrderedUnits.OrderByDescending(unit => unit.Character.IsMonster ?
            unit.Character.Speed + RandomSolver.Next(0, 3) + RandomSolver.NextDouble() :
            unit.Character.Speed + RandomSolver.Next(0, 3) + RandomSolver.NextDouble() - 100));
    ...
    else
        OrderedUnits = new List<FormationUnit>(OrderedUnits.OrderByDescending(unit =>
        unit.Character.Speed + RandomSolver.Next(0, 3) + RandomSolver.NextDouble()));
}
else
    OrderedUnits = new List<FormationUnit>(OrderedUnits.OrderByDescending(unit =>
        unit.Character.Speed + RandomSolver.Next(0, 3) + RandomSolver.NextDouble()));
```
`Mechanics/Battle/Round.cs:119-131`（怪物按 `Initiative.NumberOfTurns` 重复入池）
```csharp
foreach (var unit in battleground.MonsterParty.Units)
{
    unit.CombatInfo.UpdateNextRound();
    if (unit.Character.IsMonster)
        for (int i = 0; i < unit.Character.Initiative.NumberOfTurns; i++)
            OrderedUnits.Add(unit);
    else
        OrderedUnits.Add(unit);
```
`Character/Components/Initiative.cs:13-30`（`.number_of_turns_per_round`，默认 0）
`Mechanics/Battle/Round.cs:156-170`（`InsertInitiativeRoll`，`Speed < unit.Speed - 2` 处插入）

---

## 6. 死门 / 濒死

### 6.1 结论

**英雄路径**（`PrepareDeath` 的 else 分支）：

```text
当英雄 HP 被扣到 0：
  若 (hero.AtDeathsDoor == false && MarkedForDeath == false)
      → DeathDoorEnterQueue.Add(unit)，本次不死
  若 (hero.AtDeathsDoor == true || MarkedForDeath == true)
      → 若 (CheckSuccess(hero.DeathResist) && !MarkedForDeath) → 存活（本次豁免死亡）
      → 否则 → 死亡
```
- `DeathResist = Clamp(DeathBlow.ModifiedValue, 0.0, 0.87)`；**缺属性时默认 0.5**（`Hero.cs:32-39`）。
- **死门本身不减伤**：进入死门后 HP 保持 0，后续每一次把 HP 打到 0 的伤害都会再 roll 一次 `DeathResist`（这就是"每次受击的后果"：独立概率判定，不是累加/递减）。
- 进死门时（下一批事件处理时统一执行，`RaidSceneManager.cs:4821-4838`）：
  - `Hero.ApplyDeathDoor()` → 标记 `DeathsDoorStatusEffect.AtDeathsDoor = true`，`RevertMortality()`，并挂上职业 `deaths_door.buffs` 里的永久 buff（`BuffSourceType.DeathsDoor`）。
  - 对该单位施加数据效果 `"BarkStress"`。
- 离开死门：任何治疗（`Hero.Heal` 覆写）会先 `RevertDeathsDoor()` 再治疗；`RevertDeathsDoor` 移除死门 buff 并 `ApplyMortality()` → 挂 `deaths_door.recovery_buffs`（`BuffSourceType.Mortality`，"死亡恢复/虚弱"状态）。
- `MarkedForDeath`（心衰）：无视 `DeathResist`，直接死。
- **英雄死亡代价**：全队每人施加效果 `"Stress 2"`（`RaidSceneManager.cs:1815-1816`）；从作战队列/待处理队列中移除。
- 英雄**被治疗**、`TownReset` 等都会清状态；战斗结束只清 Stun/Guard/Guarded 与 Combat buff（`RaidSceneManager.cs:2120-2132`）。

**怪物路径**：HP 0 即死（除 `DeathClass.CanDieFromDamage == false` 的"打不死"怪，直接 return false）。死亡后按 `DeathClass` 决定"替换成另一种怪 / 留尸体"，`FullCaptor` 死亡会释放俘虏，`LifeLink` 有联动死亡（`RaidSceneManager.cs:1576-1629`、`1669-1766`）。

### 6.2 依据

`Managers/RaidSceneManager.cs:1630-1665`
```csharp
Hero hero = targetUnit.Character as Hero;
if (hero.AtDeathsDoor || targetUnit.CombatInfo.MarkedForDeath)
{
    if (RandomSolver.CheckSuccess(hero.DeathResist) && !targetUnit.CombatInfo.MarkedForDeath)
        return false;                     // 死门豁免
    targetUnit.SetDeathAnimation(true);
    ...
    targetUnit.CombatInfo.IsDead = true;
    return true;
}
else
{
    DeathDoorEnterQueue.Add(targetUnit);   // 进死门
    return false;
}
```
`Character/Hero.cs:32-39`、`353-401`（`ApplyDeathDoor` / `RevertDeathsDoor` / `ApplyMortality` / `RevertMortality`）
`Character/Components/DeathDoor.cs`（`.buffs` / `.recovery_buffs` 解析）
`Managers/RaidSceneManager.cs:4829-4838`（进死门统一处理 + `BarkStress`）
`Character/Hero.cs:672-677`（治疗先退死门）
`Managers/RaidSceneManager.cs:1576-1584`（怪物 `CanDieFromDamage == false` 免死）
> 附注（联机专用实现 `Networking/RaidSceneMultiplayerManager.cs:1995-2006`）：多人模式里死亡抗性会按人数差减 `0.3`（`resistIgnoreBonus`），单机版**没有**这段。

---

## 7. 士气 / 压力 / 折磨 / 美德

### 7.1 阈值（结论）

| 阈值 | 语义 | 依据 |
|---|---|---|
| `Stress` 上限 **200** | `PairedAttribute(stress, 200, true)` | `Hero.cs:244` |
| `>= 50` | `IsStressed`（"有压力"，UI/行为用） | `Hero.cs:44` |
| `>= 100` | `IsOverstressed`（过压 → 触发 resolve check） | `Hero.cs:45` |
| `== 200` | 心衰检定 `AddHeartAttackCheck` | `RaidSceneManager.cs:1856-1857`、`StressEffect.cs:42-43` |
| `== 0` 且处于 Afflicted | 解除折磨 `RevertTrait()` | `StressHealEffect.cs:37-38`、`66-67` |

- **resolve check 触发条件**：压力增加后 `IsOverstressed && !IsVirtued && !IsAfflicted`（即"首次过 100"），入队一次（`AddResolveCheck` 去重）。`Character.ReadyForAfflictionCheck` 同义（`Character.cs:49-52`）。
- **美德/折磨判定**：`virtueChance = Clamp(0.25 + ResolveCheckPercent.ModifiedValue, 0.01, 0.6)`；否则折磨。从对应 `OverstressType` 的 Trait 池随机取一个（`RaidSceneManager.cs:4578-4583`）。
- **美德结果**：压力被设为 `RandomSolver.Next(20, 40)`（`0-2` 之间的整数 20~39）。
- **折磨结果**：对**其他所有队友**施加效果 `"AfflictedAllyStress"`（同伴压力）；折磨是 Trait（`OverstressType.Affliction`），挂永久 buff。
- 对已有美德的英雄继续加压会被**硬钳到 ≤100**（`StressEffect.cs:37-38` 等三处）。

### 7.2 压力数值公式

```text
受压 StressEffect:
  initialDamage = StressAmount
                * (1 + performer.StressDmgPercent)              // 施法者加成
  damage = RoundToInt(initialDamage * (1 + target.StressDmgReceivedPercent))
  if (damage < 1) damage = 1                                    // 最小 1
  Stress.IncreaseValue(damage)                                  // 钳 [0,200]

减压 StressHealEffect: 同型
  heal = RoundToInt(StressHealAmount * (1 + healer.StressHealPercent)
                                      * (1 + target.StressHealReceivedPercent))
  if (heal < 1) heal = 1
```
- 心衰（`RaidSceneManager.cs:4925-4951`）：
  - 已在死门 → `MarkedForDeath = true` → `PrepareDeath` → 直接死亡（显示心衰 + 死亡一击）。
  - 未在死门 → 施加 `TakeDamagePercent(1.0f)`（当前最大 HP 的 100% → HP 归 0），再把压力设成 `ValueRatio = 0.75f`（即 150/200），然后入死门队列。
- 英雄死亡 → 全队 `"Stress 2"`（`RaidSceneManager.cs:1815-1816`）；被暴击命中 → 英雄目标额外 `"Stress 2"`（`BattleSolver.cs:407-409`）。
- 光照（TorchMeter）通过 `StressDmgReceivedPercent` buff 间接影响受压（`Raid/TorchMeter.cs:135/141/155/169`）。
- 战斗/地牢中还有大量数据表效果（`"Stress 2"`、`"AfflictedAllyStress"`、`"BarkStress"`），实际数值在数据文件里，不在代码里。

### 7.3 依据

`Managers/RaidSceneManager.cs:4578-4583`
```csharp
float virtueChance = 0.25f + resolveUnit.Character[AttributeType.ResolveCheckPercent].ModifiedValue;
virtueChance = Mathf.Clamp(virtueChance, 0.01f, 0.6f);
bool isVirtue = RandomSolver.CheckSuccess(virtueChance);
var availableTraits = isVirtue ? DarkestDungeonManager.Data.Traits.FindAll(trait => trait.Type == OverstressType.Virtue) :
    DarkestDungeonManager.Data.Traits.FindAll(trait => trait.Type == OverstressType.Affliction);
```
`Managers/RaidSceneManager.cs:4643-4644`
```csharp
if (isVirtue)
    resolveUnit.Character.Stress.CurrentValue = RandomSolver.Next(20, 40);
```
`Managers/RaidSceneManager.cs:1841-1858`（`ProcessStress`：受压 + 触发 resolve/heart attack）
```csharp
int damage = Mathf.RoundToInt(stress * (1 + unit.Character[AttributeType.StressDmgReceivedPercent].ModifiedValue));
if (damage < 1) damage = 1;
unit.Character.Stress.IncreaseValue(damage);
if (unit.Character.IsOverstressed)
{
    if (unit.Character.IsVirtued) unit.Character.Stress.CurrentValue = Mathf.Clamp(unit.Character.Stress.CurrentValue, 0, 100);
    else if (!unit.Character.IsAfflicted && unit.Character.IsOverstressed) Instanse.AddResolveCheck(unit);
    if (Mathf.RoundToInt(unit.Character.Stress.CurrentValue) == 200) Instanse.AddHeartAttackCheck(unit);
}
```
`Mechanics/Skills/Effects/StressEffect.cs:26-32`、`Effects/StressHealEffect.cs:28-34`
`Managers/RaidSceneManager.cs:4925-4951`（心衰两种分支）
`Character/Hero.cs:244`、`44-47`、`403-410`（Trait 挂载）

---

## 8. 位移 / 位置

### 8.1 位置表示

- **1~4，不是 0~3**。前排 = 1，后排 = 4（英雄与怪物都是 1~4；怪物侧只是渲染镜像）。
- 依据：`Mechanics/Battle/FormationSet.cs:65-68` 把 `"1234"` 这类字符串逐字符解析成 `Ranks` 列表：
```csharp
for (int i = 0; i < formationString.Length; i++)
    Ranks.Add(int.Parse(formationString[i].ToString()));
Ranks.Sort();
```
`Raid/Party/FormationRanksSlot.cs:31-34/68-71`（`SetSiblingIndex(4 - unit.Rank)` / `unit.Rank - 1`）确认取值域是 1..4。
- 站位生成：
  - 英雄 `unit.Initialize(hero, 4 - i, Team.Heroes)`（`Raid/Party/FormationParty.cs:69`）→ `HeroInfo[0]` 落在 **rank 4（最后排）**，列表顺序是"后→前"。⚠️ 这一点容易被我们搞反。
  - 怪物 `summonRank = 1; ... summonRank += monster.Size`（`FormationParty.cs:105-119`）→ 从 rank 1 起依次向后。
- 体型（`Size`）占多格：单位占 `[Rank, Rank + Size - 1]`。
- 判定 API（`FormationSet.cs:23-31`）：
```csharp
public bool IsLaunchableFrom(int rank, int size) { return Ranks.Exists(r => r >= rank && r <= rank + size - 1); }
public bool IsTargetableUnit(FormationUnit unit) { return Ranks.Exists(r => r >= unit.Rank && r <= unit.Rank + unit.Size - 1); }
```
- 技能字符串前缀语义（`FormationSet.cs:53-63`）：`@` = 己方阵容（self formation）、`~` = 多目标、`?` = 随机目标、空串 = 自身（self target）。

### 8.2 位移结算

**技能自带位移（施法者自己动）**，在伤害/效果之前发生（`BattleSolver.cs:312-318`）：
```csharp
if (skill.Move != null && !performerUnit.CombatInfo.IsImmobilized)
{
    if (skill.Move.Pullforward > 0) performerUnit.Pull(skill.Move.Pullforward, false);
    else if (skill.Move.Pushback > 0) performerUnit.Push(skill.Move.Pushback, false);
}
```
`MoveComponent(Pullforward, Pushback)` 在技能数据 `.move` 处解析（`CombatSkill.cs:389-391`）。

**对目标的推/拉（PushEffect / PullEffect）**：先过抗性 roll（§4），成功后调用 `target.Push(n)` / `target.Pull(n)`。`Raid/Party/FormationUnit.cs:208-252`：
```csharp
public void Push(int strength, bool changeUnitOrder = true)
{
    if (CombatInfo.IsImmobilized) return;
    int pushed = 0;
    foreach (var backUnit in Party.Units.FindAll(unit => unit.Rank > Rank).OrderBy(unit => unit.Rank))
    {
        if (backUnit.CombatInfo.IsImmobilized) break;
        int backUnitTargetRank = Rank;
        int pushedTargetRank = Rank + backUnit.Size;
        RankSlot.Relocate(pushedTargetRank, changeUnitOrder);
        backUnit.RankSlot.Relocate(backUnitTargetRank);
        pushed += backUnit.Size;
        if (pushed >= strength) break;
    }
    Party.Units.Sort(...);
}
```
语义：施法者与被推者**交换位置**，沿链条逐个向后挪，直到累计搬运量 `>= strength`；**遇到被定身的单位就停止**（连锁中断）；`Pull` 是镜像方向（`Rank < Rank` 向前）。
- 定身 `CombatInfo.IsImmobilized`（`Raid/Party/FormationUnitInfo.cs:12`）由 `ImmobilizeEffect` / `UnimmobilizeEffect` 设置，同时**禁止自身位移与阻挡推拉链**。
- 洗牌：`ShuffleTargetEffect`（`.shuffletarget` / `.shuffleparty`）。
- 选中目标时若被守护，会在 `ExecuteGuardRedirection` 阶段把目标替换成守护者（`RaidSceneManager.cs:3803-3813`）——**位移/伤害都作用于替换后的目标**。

---

## 9. buff / 状态

### 9.1 两条独立系统

1. **Buff（属性修改器）**：`Buff { BuffType(StatAdd|StatMultiply), AttributeType, ModifierValue, RuleType, IsFalseRule, SingleParam, StringParam }` + `BuffInfo { Buff, DurationType, SourceType, Duration, IsApplied }`。
2. **StatusEffect（开关/计时器状态）**：`StatusType = {None, Stun, Bleeding, Poison, Marked, Riposte, Guard, Guarded, DeathsDoor, DeathRecovery}`（`Mechanics/MechanicsDefines.cs:167-179`）。

### 9.2 挂载与生效方式

`Character/Character.cs:539-561`
```csharp
protected void ApplyBuff(BuffInfo buffEntry)
{
    if (buffEntry.IsApplied) return;
    buffEntry.IsApplied = true;
    if(buffEntry.Buff.Type == BuffType.StatAdd)
        GetAttribute(buffEntry.Buff.AttributeType).FlatAddition += buffEntry.ModifierValue;
    else if(buffEntry.Buff.Type == BuffType.StatMultiply)
        GetAttribute(buffEntry.Buff.AttributeType).Multiplier += buffEntry.ModifierValue;
}
```
最终值公式（`Character/Attribute.cs:5-18`）：
```text
ModifiedValue = (RawValue + FlatAddition) * Multiplier
两者都"带缓存"，任一 RawValue/FlatAddition/Multiplier 赋值时置脏重算 -> IsModificationCurrent
```

### 9.3 叠加规则

- **不去重、不刷新**：每次 `AddBuff` 都 `BuffInfo.Add(new BuffInfo(...))`（`Character.cs:392-397`），同属性同规则的多个 buff 各自独立存在，效果**加法叠加**（`FlatAddition` 累加；`StatMultiply` 的 `Multiplier` 也累加，即 `1 + Σ x`，不是连乘）。
- UI 只是显示时按 `IsSameBuff`（同 AttributeType + RuleType + IsFalseRule）分组求和、取最大剩余回合（`Character.cs:495-537`、`Buff.cs:65-68`）。
- 条件 buff（`BuffRule` ≠ `Always`）**不会自动每帧结算**，而是在特定时机被"重算"：技能执行前后 `ApplyConditions` / `RemoveConditions`（`BattleSolver.cs:483-498`）、以及各处 `ApplyAllBuffRules(Rules.GetIdleUnitRules(unit))`。规则枚举见 `Character/Buff.cs:6-17`（Always/Size/LightBelow/LightAbove/HpBelow/HpAbove/InRank/StressAbove/StressBelow/Skill/Afflicted/Virtued/Melee/Ranged/FirstRound/Status/EnemyType/DeathsDoor/InCamp/InDungeon/WalkBack/InActivity/InCorridor/Riposting/InMode）。
- 「对已标记目标增伤」这类效果是**条件属性 buff**：数据里用 `.keyStatus tagged` + `.monsterType` 设定条件（`Effect.cs:451-496`、`CombatStatBuffEffect.cs:57-79`），命中判定时若**主目标**满足条件，则给 buff 的落点（`target` = 施法者，因为 `.target performer`）加上该 buff，来源标记 `BuffSourceType.Condition`，技能结束后由 `RemoveConditionalBuffs` 清掉（`Character.cs:365-372`、`BattleSolver.cs:494-498`）。

### 9.4 持续时间

- 默认：效果没有 `.duration` 时，buff 时长为 **3** 回合（`Effects/BuffEffect.cs:174-179`、`Effects/CombatStatBuffEffect.cs:310-318`）。
- `.duration -1` → `BuffDurationType.Camp`（持续到扎营结束）；`.curio` 存在 → 也是 Camp 类型（`BuffEffect.cs:161-168`）。
- `BuffDurationType.Round` 的递减点 = `Character.UpdateRound()`（`Character.cs:316-331`）：
```csharp
public void UpdateRound()
{
    foreach (var effect in StatusEffects) effect.Value.UpdateNextTurn();
    UpdateDurations(BuffDurationType.Round);
}
public void UpdateDurations(BuffDurationType durationType)
{
    foreach (var buffEntry in BuffInfo.FindAll(roundBuff => roundBuff.DurationType == durationType))
        if (--buffEntry.Duration <= 0) RemoveBuff(buffEntry);
}
```
调用点：**单位自己回合开始时**（`RaidSceneManager.cs:2871` 英雄、`3509` 怪物；被眩晕时走 `2862/3501` 也会调用一次），以及地牢推进时对全体（`4749`）。→ "回合数"实际是**该单位自己的回合计数**，不是全局轮数。
- `Combat` 类型：战斗结束时 `UpdateDurations(BuffDurationType.Combat)` + 清 Stun/Guard/Guarded（`RaidSceneManager.cs:2120-2132`）。
- `Camp` 类型：扎营结束时 `RemoveCampingBuffs()`（`Raid/CampController.cs:27`）。

### 9.5 各 StatusEffect 的具体语义

| 状态 | 持续/计时 | 依据 |
|---|---|---|
| Stun | **布尔值，无 duration**；在被眩晕单位自己的回合开始时消费掉（`StunApplied=false`），随后 `ApplyStunRecovery()` 叠眩晕恢复 buff，本回合被跳过 | `Statuses/StunStatusEffect.cs:8-17`；`RaidSceneManager.cs:2851-2868`、`3490-3507`、`2768-2776` |
| Marked（标记） | `MarkDuration`，每个自己回合 `--`；`DurationType.Combat` 则不递减 | `Statuses/MarkStatusEffect.cs:11-18`；`Effects/TagEffect.cs:11-12`（默认 3） |
| Riposte | `RiposteDuration`，自己回合 `--`；`DurationType.Combat` 不递减（`.duration -1`） | `Statuses/RiposteStatusEffect.cs:11-18`；`Effects/RiposteEffect.cs:22-30` |
| Guard / Guarded | `GuardedStatusEffect.GuardDuration`，在**守护者**的回合递减（`GuardStatusEffect.UpdateNextTurn`） | `Statuses/GuardStatusEffect.cs:16-28`、`GuardedStatusEffect.cs:5-9` |
| Bleeding / Poison | `List<DamageOverTimeInstanse{TickDamage,TicksAmount,TicksLeft}>`；`CurrentTickDamage = Σ TickDamage`；`UpdateNextTurn` 逐个 `--TicksLeft`，≤0 移除 | `Statuses/DamageOverTimeStatusEffect.cs:11-20`、`DamageOverTimeInstanse.cs:7-10` |
| DeathsDoor / DeathRecovery | 纯布尔标记 | `Statuses/DeathsDoorStatusEffect.cs`、`DeathRecoveryStatusEffect.cs` |

**DoT 结算**（重要）：
- 触发点 = **受影响单位自己回合开始时**（英雄 `RaidSceneManager.cs:2812-2846`；怪物 `3443-3485`），顺序 **Bleed → Poison**。
- 另有地牢推进（每走一格）对**英雄**再结算一次全部 DoT（`ExecuteRoundAdvance`，`RaidSceneManager.cs:4653-4741`，由 `AdvanceThroughDungeon()` 触发，`1166-1169`）。
- 对 `CombatInfo.TotalInitiatives == 0`（= `Initiative.NumberOfTurns == 0`，即数据里没写 `.number_of_turns_per_round` 的怪，或某种不参与轮转的单位）在轮末以 **1.5 倍** 结算 DoT（`RaidSceneManager.cs:2721-2763`，`CeilToInt(CurrentTickDamage * 1.5f)`）。⚠️ 这条与"整轮不行动"是同一条件的两个后果（`Round.cs:119-124` + `FormationUnitInfo.cs:46`），实际数据通常有 1 回合，所以主要是边界行为 —— `推断`：这更像实现副作用而非原版设计。
- **DoT 伤害绕过 prot 与命中判定**：直接 `ProcessDamage(unit, CurrentTickDamage)` → `TakeDamage`（`RaidSceneManager.cs:1821-1839`）。
- DoT 实例**每次施加都新增一条**（`BleedingStatusEffect`/`PoisonStatusEffect` 的 `AddInstanse`），互相叠加，各自计时（`Effects/BleedEffect.cs:28-35`、`PoisonEffect.cs:28-35`）。

---

## 10. 参考实现的"坑"与未实现项（对齐时务必决定跟不跟）

用 grep 逐个确认过（全库无使用点）：

1. `AttributeType.DmgReceivedPercent`（受到伤害 %）：**从未参与伤害计算**。
2. `EffectBoolParams.OnHit`、`ApplyWithResult`、`CritDoesntApplyToRoll`：**只解析，没有任何使用点**。唯一被使用的 bool 参数是 `OnMiss`、`ApplyOnce`、`CanApplyAfterDeath`、`Queue`、`CurioResult`。
3. `Effect.Apply` 的 `on_miss` 逻辑：`if (OnMiss == false) return;` —— **未声明 `on_miss` 的效果在未命中时依然会施加**（`Effect.cs:35-37`）。与 DD1 "未命中不施加 debuff" 的直觉相反，数据表必须显式写 `.on_miss false`。
4. `BuffEffect.ApplyQueued` 里负向 buff 的抵抗用的是 **`AttributeType.Move`**（`BuffEffect.cs:128`），而 `ApplyInstant` 和 `CombatStatBuffEffect` 用的是 `Debuff`（`BuffEffect.cs:34/60/97`、`CombatStatBuffEffect.cs:101/130/170/201`）——**疑似 bug/不一致**。我们按 `Debuff` 实现更合理。
5. Stun **没有持续时间**（忽略 `.duration`），一通眩晕只跳过一回合。
6. `skill.Accuracy == 0` 会把技能类别改为 `Support`/`Heal`（`CombatSkill.cs:399-405`），从而完全不进命中/伤害分支 —— 数据里漏写 `.atk` 会造成"技能不造成伤害"。
7. 英雄攻击力只有武器 Min/Max 与技能倍率；**没有"等级/决心带来的伤害成长"**（武器升级带来的 min/max 是在武器数据里）。
8. `Crit` 属性（`Character.Crit`，钳 [0,1]）存在但 BattleSolver **不用它**，用的是未钳制的 `GetSingleAttribute(CritChance).ModifiedValue + skill.CritMod`。
9. `RaidSceneMultiplayerManager` 才是联机真正跑的那份逻辑，与单机的 `RaidSceneManager` 有细微差别（如 `PrepareDeath` 有 `resistIgnoreBonus = 0.3`）。若我们只对齐单机，忽略多人分支。

---

## 11. 与我们（Godot/C# 复刻）最可能不一致的 5 点提醒

1. **`def` 的语义**：参考项目里 `def`/`DefenseRating` 是**闪避**，只进命中公式；护甲减伤是 `prot`/`ProtectionRating`，只进伤害公式。若我们把 `def` 当成护甲减伤，或让 `prot` 参与命中，会系统性错位。
2. **取整与钳制顺序**：`ceil(初始伤害 × (1 - prot))`，然后暴击 `×1.5` 再 `RoundToInt`；没有独立"最小 1 点伤害"钳制（是 ceil 的副作用）；护甲封顶是 `max(0.85, 原始 prot)`；命中率封顶 **0.95**，暴击率**无封顶**。
3. **回合顺序的随机幅度**：`Speed + rand[0,2] + rand[0,1)` 降序，随机项与速度同量级；一轮只排一次、轮内速度变化不重排；怪物按 `number_of_turns_per_round` 多次入池（默认 0 → 整轮不动，且轮末吃 1.5 倍 DoT）。
4. **DoT 触发时机**：在**受影响者自己回合开始时** Bleed→Poison 依次结算，绕开 prot 与命中；地牢推进时对英雄再结算一次；DoT 实例叠加而非覆盖。若我们按"每轮末统一结算"，节奏会完全不同。
5. **未命中仍会施加效果**（除非显式 `on_miss false`）+ 死门是"每次 HP 归零独立 roll `DeathResist`（≤0.87）"，而不是累加/递减；心衰未在死门时是 `当前最大HP × 100%` 伤害 + 压力设为 75%。

---

## 附：核心公式速查（一行版）

```text
命中:      roll >= Skill.Acc; hit = (Skill.Acc + Perf.Acc - Tgt.Dodge) 限 [0, 0.95]; 未中→先判 Miss/Dodge 后 return
伤害:      dmg = ceil( Lerp(min,max,rnd) * (1+Skill.DmgMod) * (1 - Tgt.Prot) )   // 英雄 min/max=武器, 怪物=技能区间, ×Perf.DamageMod
暴击:      if rnd < (Perf.CritChance + Skill.CritMod) → dmg = round(dmg * 1.5)     // 护甲先算
dodge:     只进命中公式    prot: 只进伤害公式    （没有第三个字段）
抗性:      chance = clamp(效果chance - Tgt.<Res> + (英雄? Perf.<Res>Chance:0), 0, 0.95)   // Disease 例外: 1-Res, 无夹取
回合:      OrderByDescending(Speed + rand[0,2] + rand[0,1))
死门:      HP 0 且未在死门 → 进死门; 已在死门 → rnd < clamp(DeathResist,0,0.87) 则免死, 否则死
压力:      50=有压力 100=过压(首次触发检定) 200=心衰; 美德率 = clamp(0.25 + ResolveCheckPercent, 0.01, 0.6)
位置:      rank 1..4, size 占 [rank, rank+size-1]; Push/Pull = 与目标交换 + 沿链挪动, 定身阻断
状态:      Stun 布尔(跳一回合); Marked/Riposte 各自回合递减; Guard 在守护者回合递减; DoT 实例叠加
buff:      ModifiedValue = (Raw + ΣFlat) * (1 + ΣMult), 不去重不刷新, 默认 3 回合, 每到自己回合 -1
```
