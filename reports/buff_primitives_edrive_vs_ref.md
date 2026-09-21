# buff 原语词汇 · **一手（E 盘）vs 第三方（参考项目）对账**（2026-09-22 · 轮 3）

> 🔴 依据 dd1_baseline §32.3：**一手 > 二手 > 第三方**；**冲突时以一手为准并记录** ✓
> 工具：	ools/dsh/extract_dd1_buff_primitives.py --ref <源> --out-suffix <后缀>（**同一抽取器同时抽两份** ⇒ 可比 ✓）

## 读数（当场实测）

| 项 | 一手 E 盘 | 第三方参考项目 |
|---|---|---|
| 源文件 | `shared/buffs/base.buffs.json`（769 KB） | `Assets/Resources/Data/JsonBuffs.json`（668.5 KB） |
| buff 条数 | **2020** | **1801** |
| 原语种数 | **48** | **41** |
| stat_type 种数 | **27** | **25** |
| rule_type 种数 | **27** | **23** |

## 集合差异

| 类别 | 数量 |
|---|---|
| 两边都有 | **40** |
| **只在一手** | **8** |
| **只在第三方** | **1** |
| 都有但使用次数不同 | **28** |

### 只在一手的原语（⇒ **按 §32.3 应补进我们的分类器**）

```
crit_received_chance / , ignore_stealth / , activity_side_effect_chance / remove_currency, activity_side_effect_chance / add_currency, activity_side_effect_chance / remove_trinket, activity_side_effect_chance / add_trinket, stress_dmg_received_percent / hunger, stress_dmg_received_percent / camping_eat
```

### 只在第三方的原语（⇒ **一手没有 ⇒ 不得作为顶替依据**）

```
hp_heal_amount / 
```

### 都有但次数不同（前 20）

```
combat_stat_multiply / damage_low : 一手 198 vs 第三方 177
combat_stat_multiply / damage_high : 一手 198 vs 第三方 177
combat_stat_add / crit_chance : 一手 155 vs 第三方 126
combat_stat_add / attack_rating : 一手 145 vs 第三方 134
stress_dmg_received_percent /  : 一手 134 vs 第三方 126
combat_stat_add / speed_rating : 一手 116 vs 第三方 102
combat_stat_add / defense_rating : 一手 95 vs 第三方 90
combat_stat_add / protection_rating : 一手 94 vs 第三方 90
resistance / poison : 一手 65 vs 第三方 58
resistance / bleed : 一手 60 vs 第三方 51
hp_heal_received_percent /  : 一手 54 vs 第三方 38
hp_heal_percent /  : 一手 51 vs 第三方 44
debuff_chance /  : 一手 50 vs 第三方 46
resolve_check_percent /  : 一手 48 vs 第三方 44
resistance / move : 一手 45 vs 第三方 42
scouting_chance /  : 一手 43 vs 第三方 41
resistance / disease : 一手 42 vs 第三方 37
resistance / debuff : 一手 40 vs 第三方 38
combat_stat_multiply / max_hp : 一手 39 vs 第三方 36
resistance / stun : 一手 38 vs 第三方 36
```

## 结论（对本次顶替的意义）

```
· 分类器（41 条 ⇒ 26 有去向 / 15 显式冻结）**原先是按第三方的词汇表建的** ⇒ 本对账给出**一手词汇表** ✓
· 若一手多出原语 ⇒ **它们应进入分类器**（否则「要么被引用、要么进清单」的规则会漏项 ✗）✓
· 若某原语**只在第三方** ⇒ 它**不是原版的东西** ⇒ 不能作为顶替依据 ✓ 应从我方清单里剔除或标注 ✓
· ⚠️ 本报告**不改数据**：下一步（等架构/策划点头）才是按此修正分类器 ✓
```
