# M4 Trinket 表：来源与实测（由提取器生成 · 一手 E 盘）

> 源：`E:\SteamLibrary\steamapps\common\DarkestDungeon\trinkets\base.entries.trinkets.json`（**一手**）· rarity 表：`base.rarities.trinkets.json`
> 裁定：策划 `#452` —— 参考件是**第三方**（漏 7 / 多 5 / 缺 2 种 rarity）⇒ **回一手** ✓
> 口径：**排除 `rarity == kickstarter`** ⇒ **196 条 / 13 种 rarity** ✓

## 实测（每次运行重新测，不信任历史数字）

- E 盘全量条目 = **490**（裁定定义：490）
- E 盘 rarity 条数 = **14**（裁定定义：14）
- 排除 `kickstarter` 后条目 = **196**（裁定定义：196）
- 排除后 rarity 种数 = **13**（裁定定义：13）：`ancestral`, `ancestral_shambler`, `collector`, `common`, `courtier`, `crow`, `darkest_dungeon`, `madman`, `rare`, `trophy`, `uncommon`, `very_common`, `very_rare`
- 排除后**非 universal** = **26**（裁定定义：26）⇒ 这才是**不可购买**的判据 ✓
- 排除后 `price <= 1` = **15**（裁定定义：15）⇒ ⚠️ **不是**购买判据（策划已更正）✓
- 重复 id = **0**（T2 要求 0）· 缺字段 = 0（提取时已补显式空值）

## rarity 表（含 `award_category` —— 购买判据的唯一来源）

| rarity | award_category |
|---|---|
| `darkest_dungeon` | `dd` |
| `trophy` | `trophy` |
| `ancestral_shambler` | `battle` |
| `ancestral` | `universal` |
| `crow` | `quest` |
| `courtier` | `battle` |
| `collector` | `battle` |
| `madman` | `battle` |
| `very_rare` | `universal` |
| `rare` | `universal` |
| `uncommon` | `universal` |
| `common` | `universal` |
| `very_common` | `universal` |
| `kickstarter` | `kickstarter` |
