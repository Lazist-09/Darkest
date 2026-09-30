# 一手（E 盘）vs 第三方（参考项目）· 英雄武器/护甲表 **对账报告**

> 🔴 依据：`doc/architecture/source_priority.md`（**来源口径唯一真值**）——
>   **一手 E 盘 = 第一来源；参考项目 = 兜底（仅当 E 盘读不到）** ✓
>   **冲突时以一手为准，并记录冲突** ✓
> 🔴🔴 **2026-09-26 更正**：本工具曾写「主程序 `#473` 已改判据（数值一律采用参考项目）
>   ⇒ 本表不再决定取值 / 下表"以一手为准"作废」—— ⚠️ **那句已作废**：
>   **用户 2026-09-26 裁定「选1,如果E盘读不到数值就用参考」⇒ `#473` 的「一律采用参考」【已取代】。**
>   ⇒ 本表重新**以一手为准**（并仍如实记录冲突）✓
> 工具：`tools/dsh/reconcile_hero_tables_edrive_vs_ref.py`（可复跑；**不写游戏数据** ✓）

## 读数

| 项 | 值 |
|---|---|
| 一手可读英雄 | **15** |
| 与参考项目配对上的英雄 | **15** |
| 🔴 **没配上 ⇒ 未对账的英雄** | **0** |
| 逐字段比较次数 | **675** |
| **一致** | **545** |
| **冲突** | **130** |
| 🔴 **未比较的字段数** | **0** |
| 一致率 | **80.7%** |

🔴 **配对规则更正（本轮）**：旧版用 `k.lower() == hlower` 配对 ⇒ 一手的目录是蛇形
  （`man_at_arms`）而参考的键是驼峰（`ManAtArms`）⇒ **4 个英雄静默漏比**（180 字段）⚠️
  现改为**归一化**（小写 + 去掉所有非字母数字）⇒ 上表"没配上的英雄"**必须为 0** ✓

## 🔴 冲突逐条（**以一手为准** —— 口径见 `doc/architecture/source_priority.md`）

- `abomination.weapon[0].crit`: 一手 **2.0** vs 参考 2.5
- `abomination.weapon[2].crit`: 一手 **4.0** vs 参考 3.5
- `abomination.weapon[3].crit`: 一手 **5.0** vs 参考 4.0
- `abomination.weapon[4].crit`: 一手 **6.0** vs 参考 4.5
- `abomination.armour[0].def`: 一手 **7.5** vs 参考 0
- `abomination.armour[1].def`: 一手 **12.5** vs 参考 0
- `abomination.armour[1].hp`: 一手 **31** vs 参考 33
- `abomination.armour[2].def`: 一手 **17.5** vs 参考 0
- `abomination.armour[2].hp`: 一手 **36** vs 参考 40
- `abomination.armour[3].def`: 一手 **22.5** vs 参考 0
- `abomination.armour[3].hp`: 一手 **41** vs 参考 47
- `abomination.armour[4].def`: 一手 **27.5** vs 参考 0
- `abomination.armour[4].hp`: 一手 **46** vs 参考 54
- `antiquarian.weapon[0].crit`: 一手 **1.0** vs 参考 2.5
- `antiquarian.weapon[1].crit`: 一手 **2.0** vs 参考 3.0
- `antiquarian.weapon[1].spd`: 一手 **5** vs 参考 7
- `antiquarian.weapon[2].crit`: 一手 **3.0** vs 参考 3.5
- `antiquarian.weapon[2].spd`: 一手 **6** vs 参考 8
- `antiquarian.weapon[3].spd`: 一手 **6** vs 参考 8
- `antiquarian.weapon[4].crit`: 一手 **5.0** vs 参考 4.5
- `antiquarian.weapon[4].spd`: 一手 **7** vs 参考 9
- `antiquarian.armour[1].hp`: 一手 **20** vs 参考 24
- `antiquarian.armour[2].hp`: 一手 **23** vs 参考 31
- `antiquarian.armour[3].hp`: 一手 **26** vs 参考 38
- `antiquarian.armour[4].hp`: 一手 **29** vs 参考 45
- `arbalest.weapon[0].dmg_max`: 一手 **8** vs 参考 9
- `arbalest.weapon[0].crit`: 一手 **6.0** vs 参考 7.5
- `arbalest.weapon[1].dmg_max`: 一手 **10** vs 参考 11
- `arbalest.weapon[1].crit`: 一手 **7.0** vs 参考 8.0
- `arbalest.weapon[2].dmg_max`: 一手 **11** vs 参考 13
- `arbalest.weapon[2].crit`: 一手 **8.0** vs 参考 8.5
- `arbalest.weapon[3].dmg_max`: 一手 **13** vs 参考 14
- `arbalest.weapon[4].dmg_max`: 一手 **14** vs 参考 16
- `arbalest.weapon[4].crit`: 一手 **10.0** vs 参考 9.5
- `arbalest.armour[1].hp`: 一手 **32** vs 参考 34
- `arbalest.armour[2].hp`: 一手 **37** vs 参考 41
- `arbalest.armour[3].hp`: 一手 **42** vs 参考 48
- `arbalest.armour[4].hp`: 一手 **47** vs 参考 55
- `bounty_hunter.weapon[0].crit`: 一手 **4.0** vs 参考 5.0
- `bounty_hunter.weapon[1].crit`: 一手 **5.0** vs 参考 5.5
- `bounty_hunter.weapon[3].crit`: 一手 **7.0** vs 参考 6.5
- `bounty_hunter.weapon[4].crit`: 一手 **8.0** vs 参考 7.0
- `crusader.weapon[0].crit`: 一手 **3.0** vs 参考 5.0
- `crusader.weapon[1].crit`: 一手 **4.0** vs 参考 5.5
- `crusader.weapon[2].crit`: 一手 **5.0** vs 参考 6.0
- `crusader.weapon[3].crit`: 一手 **6.0** vs 参考 6.5
- `grave_robber.weapon[0].dmg_max`: 一手 **8** vs 参考 9
- `grave_robber.weapon[0].crit`: 一手 **6.0** vs 参考 5.0
- `grave_robber.weapon[1].dmg_max`: 一手 **10** vs 参考 11
- `grave_robber.weapon[1].crit`: 一手 **7.0** vs 参考 5.5
- `grave_robber.weapon[2].dmg_max`: 一手 **11** vs 参考 13
- `grave_robber.weapon[2].crit`: 一手 **8.0** vs 参考 6.0
- `grave_robber.weapon[3].dmg_max`: 一手 **13** vs 参考 14
- `grave_robber.weapon[3].crit`: 一手 **9.0** vs 参考 6.5
- `grave_robber.weapon[4].dmg_max`: 一手 **14** vs 参考 16
- `grave_robber.weapon[4].crit`: 一手 **10.0** vs 参考 7.0
- `hellion.weapon[0].crit`: 一手 **5.0** vs 参考 2.5
- `hellion.weapon[1].crit`: 一手 **6.0** vs 参考 3.0
- `hellion.weapon[2].crit`: 一手 **7.0** vs 参考 3.5
- `hellion.weapon[3].crit`: 一手 **8.0** vs 参考 4.0
- `hellion.weapon[4].crit`: 一手 **9.0** vs 参考 4.5
- `highwayman.weapon[1].crit`: 一手 **6.0** vs 参考 5.5
- `highwayman.weapon[2].crit`: 一手 **7.0** vs 参考 6.0
- `highwayman.weapon[3].dmg_min`: 一手 **8** vs 参考 7
- `highwayman.weapon[3].crit`: 一手 **8.0** vs 参考 6.5
- `highwayman.weapon[4].dmg_min`: 一手 **9** vs 参考 8
- `highwayman.weapon[4].crit`: 一手 **9.0** vs 参考 7.0
- `highwayman.armour[0].hp`: 一手 **23** vs 参考 22
- `highwayman.armour[1].hp`: 一手 **28** vs 参考 26
- `highwayman.armour[2].hp`: 一手 **33** vs 参考 30
- `highwayman.armour[3].hp`: 一手 **38** vs 参考 34
- `highwayman.armour[4].hp`: 一手 **43** vs 参考 38
- `houndmaster.weapon[0].crit`: 一手 **4.0** vs 参考 5.0
- `houndmaster.weapon[0].spd`: 一手 **5** vs 参考 6
- `houndmaster.weapon[1].crit`: 一手 **5.0** vs 参考 5.5
- `houndmaster.weapon[1].spd`: 一手 **5** vs 参考 6
- `houndmaster.weapon[2].spd`: 一手 **6** vs 参考 7
- `houndmaster.weapon[3].crit`: 一手 **7.0** vs 参考 6.5
- `houndmaster.weapon[3].spd`: 一手 **6** vs 参考 7
- `houndmaster.weapon[4].crit`: 一手 **8.0** vs 参考 7.0
- `houndmaster.weapon[4].spd`: 一手 **7** vs 参考 8
- `houndmaster.armour[1].hp`: 一手 **25** vs 参考 28
- `houndmaster.armour[2].hp`: 一手 **29** vs 参考 35
- `houndmaster.armour[3].hp`: 一手 **33** vs 参考 42
- `houndmaster.armour[4].hp`: 一手 **37** vs 参考 49
- `jester.weapon[0].crit`: 一手 **4.0** vs 参考 7.5
- `jester.weapon[1].crit`: 一手 **5.0** vs 参考 8.0
- `jester.weapon[2].crit`: 一手 **6.0** vs 参考 8.5
- `jester.weapon[3].crit`: 一手 **7.0** vs 参考 9.0
- `jester.weapon[4].crit`: 一手 **8.0** vs 参考 9.5
- `leper.weapon[0].crit`: 一手 **1.0** vs 参考 2.5
- `leper.weapon[1].crit`: 一手 **2.0** vs 参考 3.0
- `leper.weapon[2].crit`: 一手 **3.0** vs 参考 3.5
- `leper.weapon[4].crit`: 一手 **5.0** vs 参考 4.5
- `man_at_arms.weapon[0].dmg_max`: 一手 **9** vs 参考 10
- `man_at_arms.weapon[0].crit`: 一手 **2.0** vs 参考 3.75
- `man_at_arms.weapon[1].dmg_max`: 一手 **10** vs 参考 12
- `man_at_arms.weapon[1].crit`: 一手 **3.0** vs 参考 4.25
- `man_at_arms.weapon[2].dmg_max`: 一手 **12** vs 参考 13
- `man_at_arms.weapon[2].crit`: 一手 **4.0** vs 参考 4.75
- `man_at_arms.weapon[3].dmg_max`: 一手 **13** vs 参考 15
- `man_at_arms.weapon[3].crit`: 一手 **5.0** vs 参考 5.25
- `man_at_arms.weapon[4].dmg_max`: 一手 **14** vs 参考 16
- `man_at_arms.weapon[4].crit`: 一手 **6.0** vs 参考 5.75
- `man_at_arms.armour[1].hp`: 一手 **37** vs 参考 38
- `man_at_arms.armour[2].hp`: 一手 **43** vs 参考 45
- `man_at_arms.armour[3].hp`: 一手 **49** vs 参考 52
- `man_at_arms.armour[4].hp`: 一手 **55** vs 参考 59
- `occultist.weapon[0].crit`: 一手 **6.0** vs 参考 7.5
- `occultist.weapon[1].crit`: 一手 **7.0** vs 参考 8.0
- `occultist.weapon[2].crit`: 一手 **8.0** vs 参考 8.5
- `occultist.weapon[4].crit`: 一手 **10.0** vs 参考 9.5
- `plague_doctor.weapon[0].crit`: 一手 **2.0** vs 参考 2.5
- `plague_doctor.weapon[2].crit`: 一手 **4.0** vs 参考 3.5
- `plague_doctor.weapon[3].crit`: 一手 **5.0** vs 参考 4.0
- `plague_doctor.weapon[4].crit`: 一手 **6.0** vs 参考 4.5
- `plague_doctor.armour[0].def`: 一手 **0.0** vs 参考 5
- `plague_doctor.armour[1].def`: 一手 **5.0** vs 参考 10
- `plague_doctor.armour[2].def`: 一手 **10.0** vs 参考 15
- `plague_doctor.armour[3].def`: 一手 **15.0** vs 参考 20
- `plague_doctor.armour[4].def`: 一手 **20.0** vs 参考 25
- `vestal.weapon[0].dmg_max`: 一手 **8** vs 参考 9
- `vestal.weapon[0].crit`: 一手 **1.0** vs 参考 2.5
- `vestal.weapon[1].dmg_max`: 一手 **10** vs 参考 11
- `vestal.weapon[1].crit`: 一手 **2.0** vs 参考 3.0
- `vestal.weapon[2].dmg_max`: 一手 **11** vs 参考 13
- `vestal.weapon[2].crit`: 一手 **3.0** vs 参考 3.5
- `vestal.weapon[3].dmg_max`: 一手 **13** vs 参考 14
- `vestal.weapon[4].dmg_max`: 一手 **14** vs 参考 16
- `vestal.weapon[4].crit`: 一手 **5.0** vs 参考 4.5

## 结论（对本次顶替的意义）

```
· 若一致 ⇒ 已落库的表**不需要**因"换一手"而改动 ⇒ 顶替的出处等级可标为【一手=第三方同值】✓
· 若有冲突 ⇒ **冲突字段逐条以一手为准**（本报告已列），并写进替换清单与 assets_credits ✓
· 🔴🔴 **2026-09-26 更正**：本工具曾写「本轮判据已改（主程序 `#473`：数值一律采用参考项目）
  ⇒ 上表"以一手为准"作废；冲突字段改为以参考为准」—— ⚠️ **那句已作废**：
  **用户 2026-09-26 裁定「选1,如果E盘读不到数值就用参考」⇒ `#473`【已取代】** ⇒
  ✅ **冲突字段仍【以一手为准】**（口径见 `doc/architecture/source_priority.md`）✓
  ⚠️ 另注：本表只比**一手 vs 参考**，**不代表我方已落库值** ——
  实测我方 `weapon.crit_pct` 与【两者都不符】（等差序列）⇒ 见 `O-104` ✓
```