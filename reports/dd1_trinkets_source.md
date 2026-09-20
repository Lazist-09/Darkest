# M4 Trinket 表：来源与实测结构（由提取器生成）

> 来源：`F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonTrinkets.json`（本地参考项目，用户指定可参考）
> 契约：`doc/modules/trinkets.md`（策划 `#437`）⇒ T1 196 条 / T2 校验 / T5 `price<=1` 不可购买 ✓

## 实测

- 条目数 = **488**（契约 T1 期望 **196**）
- 参考件 `rarities` 声明 **12** 种：`darkest_dungeon`, `ancestral_shambler`, `ancestral`, `collector`, `madman`, `very_rare`, `rare`, `uncommon`, `common`, `very_common`, `trophy`, `kickstarter`
- 条目里**实际用到**的 rarity **12** 种：`ancestral`, `ancestral_shambler`, `collector`, `common`, `darkest_dungeon`, `kickstarter`, `madman`, `rare`, `trophy`, `uncommon`, `very_common`, `very_rare`
- `price <= 1` 的条目 = **304** 条（契约 T5 期望 **26**）
- 带 `hero_class_requirements` 的条目 = **84** 条
- 重复 id = **0**（T2 要求 0）
- 缺字段 = **0** 处

## 与契约的差异（如实列出，不静默）

- ⚠️ 条目数 488 ≠ 契约 196 ⇒ **请策划裁**：是参考件少/多，还是契约数字需更新 ✓
- ⚠️ `rarities` 声明 **12** 种 ≠ 契约 T2 的 **13** ⇒ 请裁（多/少的那一种是什么）✓
- ⚠️ `price<=1` **304** 条 ≠ 契约 T5 的 **26** ⇒ 请裁 ✓
