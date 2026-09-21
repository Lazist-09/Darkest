# DD1 数据 · **出处等级审计**（2026-09-22 · 主程序 · 轮 2）

> 🔴 **为什么要这份**：用户指示「用参考项目顶替」+ 目标硬要求 (a)「**能取一手 E 盘则优先一手**，冲突时一手为准并记录」✓
>   ⇒ 所以第一步不是"抄"，而是**逐数据集问一句：它现在是哪一级来源？**（`dd1_baseline` §32.1 的判据：
>   **"这条结论，我能指出它出自哪一级吗？"**）✓
> 📌 **本审计的触发**：轮 1 对账发现英雄 5 阶表**原先是第三方**（参考项目）⇒ 已改一手 ✓
>   ⇒ 那么**别的数据集呢？** 这就是本报告 ✓

## §1 逐数据集（**当场实测**：读抽取工具的源路径 + 读数据文件的标记）
| 数据集 | 抽取工具 | **源** | **出处等级** | 文件内标记 | 行动 |
|---|---|---|---|---|---|
| `units.json` **5 阶武器/护甲**（4 原型） | `land_edrive_hero_tables.py` | **E 盘** `heroes/<h>/<h>.info.darkest` | ✅ **一手** | `_align` 写明一手 + 与第三方差异处数 ✓ | ✅ **轮 1 已完成** |
| `units.json` **顶层基础属性/抗性** | （M1a 期：取自**参考项目**对应职业 ⚠️） | 参考项目 | ⚠️ **第三方** | `_align` 只记 5 阶来源 ⇒ **顶层来源未区分** ✗ | 🔴 **待对账**（见 §2） |
| `data/trinkets.json`（196） | `extract_dd1_trinkets.py` | E 盘 `trinkets/base.entries.trinkets.json` | ✅ **一手** | `_note` + `_source` ✓ | ✅ 无需动 |
| `data/quirks.json`（170） | `extract_dd1_quirks.py` | E 盘 `shared/quirk/quirk_library.json` | ✅ **一手** | ✓ | ✅ 无需动 |
| `data/buildings.json`（8/20/99） | `extract_dd1_buildings.py` | E 盘 `upgrades/building/*` | ✅ **一手** | ✓ | ✅ 无需动 |
| `data/hero_upgrades.json`（15/135/645） | `extract_dd1_hero_upgrades.py` | E 盘 `upgrades/heroes/*` | ✅ **一手** | 🔴 **原先无标记** ⇒ 🆕 **本轮已补** `_note`+`_source` ✓ | ✅ **本轮闭环** |
| **Buff 原语词汇**（41 条分类器） | `extract_dd1_buff_primitives.py` | ⚠️ **参考项目** `Assets/Resources/Data/JsonBuffs.json` | ⚠️ **第三方** | `BuffPrimitiveTranslation.cs` 注释里说明 ✓ | 🔴 **待对账**（见 §3） |
| **技能候选池**（提案用） | `make_skill_mapping_proposal.py` | ⚠️ 参考项目 `Heroes/Info/*.bytes` | ⚠️ **第三方** | 报告里写明 ✓（**仅提案、未落库** ✓） | ⏳ 等策划确认 |
| `data/traits.json`（7 条） | `extract_ours_traits.py` | 我们自己的 `buff_defs.json` | ✅ **自有** | `_note` + `origin: ours` ✓ | ✅ |
| `data/skills.json`（44） | 我们自研 | 自有 ✓ | ✅ **自有** | —（自有数据无需标 ✓） | ✅ |

## §2 🔴 顶层基础属性的**一手出处还没找到**（如实报，不猜）
```
实测：一手 `heroes/hellion/hellion.info.darkest`（57 行）里**只有** `weapon:` ×5 与 `armour:` ×5
   ⇒ 🔴 **没有**独立的 `hp:` / `attack:` / `dodge:` / `resistance:` 字段 ✗
⇒ 所以"顶层基础属性/抗性回一手"这件事**现在还做不了** —— 不是我不做，是**一手出处未定位** ✓
📌 下一步（尚未做）：在 E 盘找它们真正的住处（候选：`shared/hero/…`、`heroes/<h>/<h>.darkest` 等），
   找到后再做**和轮 1 一样的对账**（逐字段 diff ⇒ 冲突以一手为准 ⇒ 附读数）✓
   ⚠️ **注意**：顶层属性**是战斗消费的**（`EffectiveAttack`/hp/def/dodge/抗性 ✓）⇒ 与 5 阶不同（5 阶今天零消费 ✓）
      ⇒ 所以回一手必然**改行为** ✗ ⇒ 必须**先给前后读数**、并按解冻口径处理 ✓（不能像 5 阶那样"零行为"过关 ✓）
```

## §3 🔴 Buff 原语词汇来自**第三方**（待对账）
```
`extract_dd1_buff_primitives.py` 的源 = 参考项目 `JsonBuffs.json`（DEFAULT_REF ✓ 实测）⚠️
⇒ 那份"41 条原语 ⇒ 26 有去向 / 15 显式冻结"的分类，**出处是第三方** ✓
⇒ 按 (a) 应回一手：E 盘 `shared/buffs/*.buffs.json` ✓（尚未做）⇒ 找到后同样做逐条对账 ✓
⚠️ 但注意：这 41 条是**词汇/去向**，不是数值 ⇒ 影响面比"数值"小 ✓（仍按一手优先处理 ✓）
```

## §4 本轮**做了什么**（读数 + 提交）
```
🆕 `hero_upgrades.json` 补上 `_note` + `_source`（外科式插入 ⇒ **不重排版** ✓ 避开 `json.dump` 重排陷阱）
📊 前后读数：构建 **0 错** ✓ · 全量 **809/809** ✓ · M8 落库读数**不变**（**15 职业 / 135 树 / 645 等级** ✓ 无悬空 ✓）
   ⇒ 说明新键**不影响解析**（解析器忽略未知键 ✓）
```

## §5 审计结论（一句话）
```
✅ **5 个数据集已是一手**（trinkets/quirks/buildings/hero_upgrades + units 的 5 阶 = 轮 1 已升一手）
⚠️ **2 个仍是第三方**（units 顶层属性 · Buff 原语词汇）⇒ **都已列为待对账**，且**各写清了"为什么现在做不了/怎么做"** ✓
✅ **2 个自有**（traits / skills）✓
```
