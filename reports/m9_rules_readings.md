# M9 三处规则的**现状读数**（供裁定 · 2026-09-21 读真实代码）

> 🔴 **为什么这份重要**：这三处**不是名字问题**（改名已在 `5368aa6` 完成 ✓）—— 它们决定
>   **4v4 之后哪些功能会消失/变化** ✓ 下面每一处都写了【真实语义 + 4v4 的后果 + 我的建议】✓

## ① `BattleDirector.cs:261-278` —— 其实是**「增援」规则**（不是普通目标规则）
```csharp
int aPos = _player.UnitAtPosition(a) ?? -1;
if (aPos is < 1 or > 4) return false;              // 发起者必须站在【战斗位】
UnitRuntime? b = _player.UnitRuntimeAt(bSlot);
if (b is null || !_player.Layout.ExtensionSlots.Contains(bSlot)) return false;   // 目标必须是【扩展位】
if (x is < 1 or > 4 || _player.GetSlot(x) == SlotState.Empty && aPos == x) return false;
if (!TrySpendSupportPoints(_balance.Tuning.SupportPoints.CostReinforce, "reinforce")) …
```
```
📌 **真实语义**：**扩展位（5/6）= 待命/替补席**；"增援" = 把**待命位的人**换进**战斗位** ✓
🔴 **4v4 的后果**：若我方 = 4 槽且**没有扩展位** ⇒ 这个功能**整条不可用**（目标条件永远不成立）⚠️
   ⇒ 即：**"4v4 + 无名册待命位" 会让"增援"消失** ⇒ 这是**玩法决定**，必须你/策划点头 ✓
✅ 我的建议（供参考）：**保留扩展位**（我方 `slot_count 6 / combat 4 / extension [5,6]` 不动）⇒ 那 M9 只剩
   **SP 维度（C）** 与 **UI 4v4 布局（M9u）**；若真要去掉扩展位 ⇒ 必须**同时决定"增援"与"虚弱回升"怎么办** ✓
```

## ② `MoraleLedger.SupportSlotRegen` —— **虚弱回升**公式（`#59` / T-M4-07）
```csharp
/// 回合回升 = 10 + 健康支援位队友×5 + 韧性÷20，上限 25；只来自【健康】支援位（虚弱互不提供）。
public int SupportSlotRegen(UnitRuntime unit, FormationBoard player)
{
    int healthySupport = 0;
    foreach (int slot in player.Layout.ExtensionSlots) { … healthySupport++; }
    return RecoveryAmount(unit.EffectiveResilience, healthySupport, _balance);
}
```
```
📌 **真实语义**：**待命位上的健康队友**每人给虚弱者 **+5** 回升（上限 25）✓
🔴 **4v4 的后果**：无扩展位 ⇒ 该项**恒为 0** ⇒ 虚弱回升只剩 `10 + 韧性/20`（**平衡变化，不是 bug** ✓）
   ⇒ ⚠️ 需要策划确认：这是**想要的**（4v4 更苦）还是**要补**（比如把系数挪到战斗位 ✓）
```

## ③ `FormationConfig.cs:107` —— 敌方校验 + 越界校验
```csharp
if (cfg.Enemy.ExtensionSlots is { Count: > 0 })
    throw new InvalidDataException($"{ResPath}: 敌方无扩展位（enemy.md §1）…");
foreach (int slot in layout.ExtensionSlots) { /* 越界 [1, SlotCount] */ }
```
```
📌 **真实语义**：① 敌方**不许有**扩展位（enemy.md §1 ✓）② 扩展位下标必须落在 `[1, SlotCount]` ✓
🔴 **4v4 的后果**：敌方本来就是 `support_slots: []` ⇒ 这条**自动成立**（空 ✓）⇒ **不阻塞** ✓
✅ 已顺手把文案从"支援位"改成"**扩展位**"（纯文案 · 零行为 · 用例数仍 797 ✓）
```

## 总结：**真正需要一句话的只有两件事**
```
① **要不要保留"待命/扩展位"？**
   保留 ⇒ 增援与虚弱回升都照旧（我什么都不用改）· 不保留 ⇒ **增援消失 + 虚弱回升少一档**（两处行为改动，我会给前后读数）
② 若保留 ⇒ 那么 M9 的剩余工作只有：**C（SP 去掉支援位维度）** + **M9u（UI 4v4 布局 · 属 UI 域）** ✓
🔴 我**不代裁**，也不碰数值（SP 剂量在 `O-95` 解冻清单里 ✓）
```
