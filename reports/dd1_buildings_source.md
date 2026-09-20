# M6 建筑结构：来源与实测（一手 E 盘）

> 源：`E:\SteamLibrary\steamapps\common\DarkestDungeon\upgrades\building`（**一手** · 每建筑一个文件 · 8 个）
> 目标形状（策划 `#421`）：`buildings[] { id, trees[] { id, levels[] { code, currency_cost[], prerequisites[] } } }` ✓
> 契约要求：`code` / `prerequisites` **各出一条 P 校验** ✓

## 实测（每次运行重测）

- 建筑 = **8** 个：`abbey`, `blacksmith`, `camping_trainer`, `guild`, `nomad_wagon`, `sanitarium`, `stage_coach`, `tavern`
- 升级树 = **20** 棵 · 等级条目 = **99** 条
- `code` 在树内重复 = **0** ✓
- 🔴 **悬空前置引用**（`tree_id`/`requirement_code` 不存在）= **0** ✓
- 🔴 **前置环**（永远到不了的等级）= **0** ✓（无环）
- `currency_cost` 用到的资源类型：`gold`=99, `crest`=99, `bust`=35, `portrait`=27, `deed`=23

## P 校验的口径（给实现用，全部来自上面实测）

```
① code：同一棵树内唯一（重复即红）；跨树可重名（`a`/`b`/`c` 是每棵树的档位名）✓
② prerequisites：引用必须存在（悬空即红）；**无环**（有环即红）✓
③ currency_cost：type 必须在本表实测集合内（不发明新资源）✓
```
