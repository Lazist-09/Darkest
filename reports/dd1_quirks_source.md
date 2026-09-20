# M5 Quirk 表：来源与实测（一手 E 盘）

> 源：`E:\SteamLibrary\steamapps\common\DarkestDungeon\shared\quirk\quirk_library.json`（**一手**）
> 契约（卡 M5）：schema = `is_positive`/`is_disease`/`classification`/`incompatible_quirks`/`curio_tag` · **库 170 条** · **互斥必须可断言** ✓

## 实测（每次运行重测）

- 条目数 = **170**（契约定义：170）
- 字段数 = **16**：`buffs`, `can_be_replaced_by_new_quirk`, `can_modify_in_activity`, `classification`, `curio_tag`, `curio_tag_chance`, `id`, `incompatible_quirks`, `is_disease`, `is_positive`, `keep_loot`, `origin`, `random_chance`, `show_explicit_buff_description`, `show_explicit_curio_tag_description`, `show_flavor_description`
- `is_positive`：正 **68** / 负 **102**
- `is_disease`：疾病 **23** / 非疾病 **147**
- `classification`：`mental`=108, `physical`=56, `(空)`=6
- 声明互斥的条目 = **95** · 互斥边总数 = **130**
- 🔴 **悬空互斥引用**（指向不存在的 id）= **0** ✓
- 🔴 **非对称互斥**（`a→b` 但无 `b→a`）= **0** 对 ✓（全对称）

## 互斥可断言的口径（给实现用）

```
① 引用完整性：`incompatible_quirks` 里每个 id 必须在库中（悬空即红）
② 对称性：本次实测给出**真实结果**（上面那行）—— 若不为 0 ⇒ 校验**不能假设对称**，必须按【边】判
③ 判定口径：互斥 = 存在边（单向即算）；`QuirksConfig.AreIncompatible(a, b)` 单点实现 ✓
```
