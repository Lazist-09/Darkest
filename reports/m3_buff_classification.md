# M3 · **22 条 buff 的三类处置：逐条归属表**（2026-09-21 实测 · 可执行版）

> 🔴 **卡 M3 的三类**：① 折磨/美德 **7** ⇒ `traits.json`（`TraitsConfig` · 无 Ledger）② 机制态 **10** ⇒ 换原版对应态
>   ③ 自加 **5** ⇒ 废掉或按红线 30 移层 ✓ 验收：**"搬家必须带行为"** + 前后读数（士气/折磨）✓
> 🔴 **本表用【时长形态】做判别器** —— 这是我三轮侦察里最重要的一条方法修正 ✓（见 §1）

## 1. 🔴 判别器（方法修正）：**按时长形态分，不按 id 名分**
```
折磨：`duration.type = until_morale_50`（士气回到 50 才结束 ✓）
美德：`duration.type = until_battle_end_or_morale_zero`（本场战斗内有效 ✓）
⇒ 实测这两族**恰好 7 条**（3 折磨 + 4 美德）⇒ **与卡里的 ① = 7 完全一致** ✓✓
（我上一轮按 id 名匹配得出"8/14"是**错的** ⇒ 名字不作数、**行为形态**才作数 ✓ 这条已写进方法记录 ✓）
```

## 2. 逐条归属（**22 行 · 全部实测**）
| # | buff | kind | duration | 我的归属 | 依据（可核） |
|---|---|---|---|---|---|
| 1 | `affliction_fear` | prob_mod | `until_morale_50` | **① 折磨** | 时长 = 折磨族 ✓ |
| 2 | `affliction_selfish` | prob_mod | `until_morale_50` | **① 折磨** | 同上 ✓ |
| 3 | `affliction_uncontrolled` | prob_mod | `until_morale_50` | **① 折磨** | 同上 ✓ |
| 4 | `virtue_brave` | damage_mod+state_flag | `until_battle_end_or_morale_zero` | **① 美德** | 时长 = 美德族 ✓ |
| 5 | `virtue_resolute` | prob_mod | `until_battle_end_or_morale_zero` | **① 美德** | 同上 ✓ |
| 6 | `virtue_inspired` | （无 modifier） | `until_battle_end_or_morale_zero` | **① 美德** | 同上 ✓（且**无 modifier** ⇒ 得看它的 effects ✓） |
| 7 | `virtue_focused` | prob_mod | `until_battle_end_or_morale_zero` | **① 美德** | 同上 ✓ |
| 8 | `stun` | state_flag | `action_skip` | **② 机制态** | 原版有 `STUN*`（64 条）✓ |
| 9 | `mark` | （无 modifier） | `rounds 3` | **② 机制态** | 原版 `MARK` 22 条 ✓ |
| 10 | `bleed` | damage_mod | `rounds 2` | **② 机制态** | 原版 `BLEED` 80 条 ✓ |
| 11 | `shield` | （无 modifier） | `charges` | **② 机制态** | 原版 `SHIELD` 2 条 ✓ |
| 12 | `guard_attach` | （无 modifier） | `rounds 3` | **② 机制态** | 原版 `GUARD` 10 条 ✓（护卫/守护 ✓） |
| 13 | `stealth` | damage_mod+prob_mod+state_flag | `rounds 2` | **② 机制态** | 原版 `STEALTH` 1 条 ✓ |
| 14 | `taunt` | （无 modifier） | `rounds 2` | **② 机制态**? | 🔴 原版 **0 命中** ⇒ 按"名字/语义"归 ② 但**证据薄弱** ⇒ 归 ③ 更诚实 ✓ |
| 15 | `bound` | （无 modifier） | `until_morale_50` | **🔴 待定** | 原版 **0 命中** ⇒ 但时长像折磨族 ⇒ **两条判据冲突** ⇒ 请看 §4 ✓ |
| 16 | `next_attack_boost` | damage_mod | `next_attack_within_rounds` | **③ 自加** | 原版无此"下一击"记账口径 ✓ |
| 17 | `deaths_door_recovery` | damage_mod+prob_mod | `until_next_recovery` | **③ 自加** | 我方自建的"死门恢复"记账 ✓ |
| 18 | `next_battle_sharpen` | damage_mod | `next_battle` | **③ 自加** | "下一场"记账 ✓ |
| 19 | `next_battle_armor` | stat_mod | `next_battle` | **③ 自加** | 同上 ✓ |
| 20 | `pep_talk` | damage_mod | `until_battle_end_or_morale_zero` | **③ 自加**? | 时长像美德族 ⚠️ 但语义是"鼓舞"（技能给的 ✓）⇒ §4 |
| 21 | `curio_altar_blessing_20` | damage_mod | `until_next_recovery` | **③ 自加** | Curio 给的祝福（我方自建口径 ✓） |
| 22 | `curio_altar_blessing_30` | damage_mod | `until_next_recovery` | **③ 自加** | 同上 ✓ |

## 3. 与卡里数字的对账（**如实报差异**）
```
卡的 ① **7** ⇒ 我实测 **7** ✓ **完全一致**（判别器 = 时长形态 ✓）
卡的 ② **10** ⇒ 我实测 **6~7**（`stun/mark/bleed/shield/guard_attach/stealth` + `taunt` 待定）
卡的 ③ **5** ⇒ 我实测 **7~8**（`next_attack_boost/deaths_door_recovery/next_battle_sharpen/next_battle_armor/
   curio_altar_blessing_20/30` = 6 明确 + `pep_talk`/`taunt` 待定）
⇒ 📌 **总数为 22 不变**；**① 对得严丝合缝**，②③ 的边界差 1~3 条（差在 `taunt`/`bound`/`pep_talk`）✓
   ⇒ 请按你的口径裁（**我不擅自宣称卡写错** ✓）
```

## 4. 🔴 三条"边界模糊"的（**请裁** · 这就是 M3 动手前唯一的信息缺口）
```
① `bound`：**原版 0 命中**（像"自加"）但**时长 = `until_morale_50`**（像"折磨"）⇒ 两条判据冲突
② `taunt`：**原版 0 命中** ⇒ 归 ③ 还是"原版有等价态但没叫 taunt"（DD1 有"protect/guard"系？）
③ `pep_talk`：时长像**美德族**但它是**技能给的效果**（不是决心判定产物）⇒ 归 ② 还是 ③
```
## 5. 动手时每条要做什么（**卡里 ①②③ 的落地动作**）
```
① 折磨/美德 7 条 ⇒ 搬到 `traits.json`（`TraitsConfig` ✓ 卡明写"**无 Ledger**"）
   ⇒ 🔴 但 `MoraleLedger` 现在**消费**这 7 条（我实测过：8 文件 56 处里含它 ✓）⇒ **搬家必须带行为** ✓
② 机制态 ⇒ **换原版对应态** ⇒ 🔴 需要"按语义匹配"（用我 M2 的 `BuffPrimitiveTranslation` 分类器 ✓）
③ 自加 ⇒ **废掉** 或 按**红线 30 移层** ✓
🔴 全部动作的硬前置：**M1/M2 到位** + 那 9 个在飞消费点（现已全部入库 ✓ 所以只剩 M1/M2 ✓）
```

## 6. 一句话
```
**① 已钉死（7 条 = 时长形态族 ✓）· ② 与 ③ 只差 3 条边界（`taunt`/`bound`/`pep_talk`）⇒ 你一句话即可开工** ✓
```
