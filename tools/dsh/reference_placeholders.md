# 参考项目顶替 · **替换清单（单一入口 · 末次全面同步）**

> 🔴 **用途**：用户指示「先用 `F:\GithubPro\Darkest-Dungeon-Unity` 的数值顶替，后续再改」⇒
>   本文件记录**每一项的当前状态、出处等级、值从哪来、后续怎么改、读数与提交号** ✓
> 🔴 **出处等级**（`dd1_baseline` §32.1）：**一手 E 盘 > 二手 wiki > 第三方参考项目** ✓
> 🔴 **GPL 边界**（§32.2 四条）：只读结构与数值 · 不逐行誊写代码 · **明显衍生要登记 `assets_credits`** ✓

## §1 待定项 · 现状总表
| # | 项 | 状态 | **出处等级** | 值 / 来源 | 后续怎么改 |
|---|---|---|---|---|---|
| ① | **技能 `dmg%`**（44 个） | 🟡 **提案就绪 · 未落库** | —（无值） | `reports/skill_dmg_mapping_proposal.md`：名字匹配 **12** ⇒ **✅ 可用 7 · ⚠️ 可疑（类型不符）5** · **🔴 需点名 32** | 🔴 **等策划确认/改写**（或给 44 行 `our_id = dmg%`）⇒ 我一次落库 + 前后读数 |
| ② | **顶层 `prot`**（4 原型） | 🔴 **未顶替（有意）** | 参考=**0** ✓ **且**一手=**0** ✓（**两源一致**） | 现值仍是旧的 8/12/4/5（原 `phys_def`） | 🔴 单独落 0 会打穿 **M6 判据 A1（死门 1.30/场 > 0.4）** ✗ ⇒ 二选一：(a) 维持现状（我推荐）／(b) 成套落地 + 给新 band |
| ③ | **5 阶武器表**（4 原型） | ✅ **已顶替 · 出处=一手** | **一手 E 盘** ✓ | `land_edrive_hero_tables.py --apply`（`_align` 已写明一手 + 与第三方差异处数 ✓） | 改 `units.json` 的 `weapon[]`（**该表今天零消费** ⇒ 不影响战斗 ✓） |
| ④ | **5 阶护甲表**（4 原型） | ✅ **已顶替 · 出处=一手** | **一手 E 盘** ✓ | 同 ③（`armour[]`：`def_pct/prot/hp/spd` ✓） | 同 ③ |
| ⑤ | **4v4 名单 4 人** | 🔴 **未定** | — | 可用池：参考项目 **15 英雄**（`dd1_hero_tables_from_unity_ref.json` ✓） | 🔴 等策划点名（我的建议：hellion / man_at_arms / plague_doctor / highwayman ✓） |
| ⑥ | **`O-95` SP 剂量** | 🔴 **无法顶替** | — | **SP 不是原版概念** ⇒ 参考项目**无对应字段** ✗（实测） | 🔴 只能策划给数 ✓ |
| ⑤b | 🆕 **「当前阶从哪来」** | 🔴 **未定（③④ 接线的唯一前置）** | — | 机制已备（见 §3 ✓） | 🔴 三选项：**(A) 升级树等级驱动（我推荐）** /(B) 远征进度 /(C) 固定 0 ⇒ 见 `reports/tier_source_options.md` |

## §2 为什么 ③④ 用**一手**而不是参考项目（现场证据）
```
`tools/dsh/reconcile_hero_tables_edrive_vs_ref.py`（可复跑 · **不写游戏数据** ✓）实测：
   一手可读英雄 **15** · 与参考配对 **11** · 逐字段比较 **495** · 一致 **402** · **冲突 97（19.6%）**
   ⇒ 📌 **参考项目与一手不是一回事**（差异集中在 `crit_pct` / `dmg_max` / `def_pct` / `hp` ✓）
   ⇒ 按 §32.3「**一手 > 第三方；冲突时一手为准并记录**」⇒ ③④ 改从**一手**落 ✓
   📄 冲突逐条：`reports/edrive_vs_reference_hero_tables.md` ✓
🔴 我方解析器的更正（如实）：第一版正则只认整数 ⇒ 漏读 `abomination_armour_0 .def 7.5%` ⇒ 误报"阶数不同" ✗
   ⇒ 修好重跑：比较 **475→495** · 冲突 **89→97**（数字更实 ✓）
```

## §3 已落地的**前后读数**（零行为证明）
```
③④ 的**值**：`python tools/dsh/land_edrive_hero_tables.py --apply`
   改动量：warrior(hellion) weapon 5 阶 · tank(man_at_arms) weapon 5+armour 4 ·
           medic(plague_doctor) weapon 3+armour 5 · commissar(highwayman) weapon 3+armour 5 ✓
   📊 应用后：构建 **0 错** · 全量 **809/809** · **减伤基线逐位不变** · 🆕 **冒烟 13/13 全绿**（R7 ✓）
   依据：`WeaponAt`/`ArmourAt` **无生产调用点** ⇒ 该表**今天零消费** ⇒ 换源不改战斗数值 ✓
   备份：`…expflow\units.before-edrive-tables.json` ✓
③④ 的**机制**（阶数接线用 · 两者**都无生产调用方** ⇒ 被"无人消费"守卫钉住 ✓）：
   · 伤害侧 `WeaponBaseDamage`（一手 5 阶区间 × (1+dmg%) ✓）
   · 🆕 减伤侧 `TierDefence`（`ProtAt`/`DefAt`/`ProtFractionAt`/`ArmourViewAt` ⇒ 复用 `UnitStats.ArmourAt` **单一出处** ✓）
     实测：warrior 逐阶 prot 全 0 · def 逐阶递增；敌人（无 5 阶）**回退顶层**；越界**钳制**（-3⇒0 · 99⇒4）✓
```

## §4 GPL 登记（`#447 §32.2 ④`）· ✅ **已闭环**
```
架构已把登记原文贴入 **`doc/assets_credits.md` §10**（L131 起 · +23 行 ✓ 我核过）
   （原文出自我 `reports/reference_substitution_findings.md` §4 ⇒ 已被采纳 ✓）
```

## §5 数据出处等级与对账状态（全量审计见 `reports/data_provenance_audit.md`）
| 数据集 | 源 | 出处等级 | 对账状态 |
|---|---|---|---|
| `units.json` **5 阶武器/护甲** | E 盘 `<h>.info.darkest` | ✅ **一手** | ✅ 轮 1 已落一手（一手 vs 第三方 495/97 ✓） |
| `units.json` **顶层基础属性** | 非 `<h>.info.darkest`（实测该文件只有 weapon/armour ✗） | ⚠️ **我们的设计（含手调）** | ✅ 已核对（**不是**第 0 阶投影 ✓）⇒ 不列为待回一手，列为「阶机制差异清单」✓ |
| `data/trinkets.json`(196) | E 盘 `trinkets/base.entries.trinkets.json` | ✅ 一手 | ✅ 无需动 |
| `data/quirks.json`(170) | E 盘 `shared/quirk/quirk_library.json` | ✅ 一手 | ✅ 无需动 |
| `data/buildings.json`(8/20/99) | E 盘 `upgrades/building/*` | ✅ 一手 | ✅ 无需动 |
| `data/hero_upgrades.json`(15/135/645) | E 盘 `upgrades/heroes/*` | ✅ 一手 | ✅ 轮 2 补上 `_note`/`_source` ✓ |
| **Buff 原语词汇**（分类器） | 一手 `shared/buffs/base.buffs.json`(2020/48) vs 第三方 `JsonBuffs.json`(1801/41) | ✅ **一手已取到** | ✅ **轮 3-4 已修正**：41→**48** 种 · 有去向 26→**27** · 冻结 15→**21** ✓ |
| **技能 dmg% 候选池** | 参考项目 `Heroes/Info/*.bytes` | ⚠️ 第三方（**仅提案**） | ⏳ 等策划确认（提案未落库 ✓） |
| `data/traits.json`(7) | 我们的 `buff_defs.json` | ✅ 自有 | ✅ |
| `data/skills.json`(44) | 我们自研 | ✅ 自有 | ✅ |

## §6 本目标（轮 1~8）的**提交索引**（硬要求 (d)：每项有读数 + 提交号）
| 轮 | 内容 | 提交 |
|---|---|---|
| 1 | 一手对账器 + ③④ 一手落库 + 顶替可行性报告（含 GPL 原文） | `d87af63` |
| 1 | 投策划 + 架构 | `3ac0c7a` |
| 2 | 数据出处等级审计（+ `hero_upgrades` 来源标记） | `da2ba96` |
| 2 | 顶层属性 vs 第 0 阶一致性核对 | `f21cfbc` |
| 3 | buff 原语一手对账（+ 抽取器 `--out-suffix`） | `31aa237` |
| 3 | 替换清单补出处等级一节 | `dafca86` |
| 4 | 分类器按一手修正（41→48 · 26→27 · 15→21） | `06ca079` |
| 4 | 清单该行改为"已修正" | `9950bcc` |
| 5 | 修我自己引入的门禁回红（`_source` 元数据键约定） | `5338fa2` |
| 5 | `--deadkeys` 判据升级（架构批准取甲 · 双向自检 PASS · 删 15 行白名单） | `d4fbce2` |
| 5 | 投架构（升级完成） | `7b66658` |
| 6 | 减伤侧机制 `TierDefence` + 守卫扩展 + 用例 + 当前阶三选项提案 | `fd8dee7` |
| 6 | 新件带来的 3 处门禁项按机制登记（纪律回 0） | `59f873c` |
| 7 | 技能映射提案升级（加类型一致性判据） | `5e9247b` |
| 7 | 冒烟重跑 13/13 全绿（换源未伤运行时） | `1c8239f` |

## §7 🔴 等策划/架构的 **5 件**（给了我就一次做完：每项附前后读数 + 提交号）
```
① 技能 `dmg%`：在 `reports/skill_dmg_mapping_proposal.md` 上确认/改写（7 可用 / 5 可疑 / 32 需点名）
② `prot`/减伤：(a) 维持现状（我推荐）／(b) 成套落地 + 给 M6 新 band
③ 4v4 名单：点名 4 个（建议 hellion / man_at_arms / plague_doctor / highwayman）
④ SP 剂量：参考项目无此概念 ⇒ 只能你给
⑤ **当前阶从哪来**：(A) 升级树等级驱动（我推荐）／(B) 远征进度／(C) 固定 0 ⇒ 给了就能把 ③④ 接线 ✓
```

---

## §8 🆕 策划 5 件答案落地后的状态（2026-09-22 · 轮 12-13）
| # | 策划裁定 | 我做了什么 | 读数 / 提交 |
|---|---|---|---|
| ① | **技能 dmg%**：36 行表（明确 **11** / 候选 12 / 无对应 13） | ✅ **明确 11 条已落库**（+接近明确的 `warrior_lunge` ⇒ 共 11 条 ✓）· 每条带 `_dmg_pct_source`（原版技能名 + 依据 ✓） | 🔴 **零行为**（伤害路径未读 `dmg_pct` ✓）· 全量 **818/818** ✓ · 守卫断言改成"11 填/33 未填" ✓ · **`3e2e640`** |
| ② | **prot/减伤 ⇒ 取 (a) 维持现状** | ✅ **什么都不用做**（判据不动 ⇒ M6 band 保持 ✓） | 实测对照仍在 `reports/prot_zero_impact_measurement.md`（prot=0 ⇒ 死门 0.33→**1.30** ✓） |
| ③④ | **当前阶 ⇒ 采纳 O-101**（= 装备/升级等级 · 由 Roster/Hero 持有 `weaponTier`/`armourTier`） | ✅ 机制已备（伤害侧 `WeaponBaseDamage` + 减伤侧 `TierDefence` ✓ 都零行为、被"无人消费"守卫钉住 ✓） | 🔴 **接线未做**：需要 `Roster.cs`/`Hero` 加两个 tier 字段（**`Roster.cs` 是别人在飞的 M ✗** ⇒ 我不动 ✓）⇒ **等它落地或授权我改** ✓ |
| ⑤ | **4v4 ⇒ 是【验收夹具】** = **warrior + tank + medic + commissar**（不是规则 ✓） | ✅ 夹具已钉住（`M9FourVFourFixtureTests` 2 条 ✓）+ 记录纪律 BG 的三种 4 人 ✓ | 全量 **820/820** ✓（+2 ✓） |
| ⑥ | **SP 剂量 ⇒ 占位 + 口径**（`Skill.SupportPointCost ?? 0` ⇒ 未声明不扣） | ✅ 载体**已存在**（`SkillsConfig` 里已有 `SupportPointCost` ✓ 实测）⇒ **无需改** ✓ | 只待结算路径接线（后续卡 ✓） |

### §8.1 由此**剩下的活**（都已写明前置）
```
① 技能 dmg%：**候选 12 + 无对应 13** 未落（策划只给了三档分类 ✓ ⇒ 未落是**按你的表**做的 ✓）
② prot：**已闭合**（取 (a) ✓ 无需动 ✓）
③④ 接线：**前置 = `Roster.cs`/`Hero` 的 tier 字段**（该文件在飞 ✗ ⇒ 我等它，或你授权我改 ✓）
⑤ 4v4 夹具：**已钉住** ✓（若要在战斗里跑出 4v4 读数 ⇒ 那是"夹具 + 战斗读数"的下一步 ✓）
⑥ SP：载体已存在 ⇒ **只等结算接线** ✓
＋ 祖产：策划取 **顺序 (A) = 先接线任务奖励再删 `tier_drop`** ⇒ **接线**需要三个口径（难度档/任务长度/每趟给哪几种）
   ⇒ 已在 `DELIVERY-LEAD-HEIRLOOM-STEP2-ORDER` 里问过 ✓（若你已在那封里答了，我下一轮读并执行 ✓）
```
