# M3 前置侦察：**22 条 buff 的"原版落在哪张表"**（卡里点名的第一步 · 2026-09-21 实测）

> 🔴 **卡 M3 原文**：「① 折磨/美德 7 ⇒ `traits.json` ② **机制态 10 ⇒ 换原版对应态（⚠️ 先量原版落在哪张表）**
>    ③ 自加 5 ⇒ 废掉或按红线 30 移层 · 🔴"**搬家必须带行为**"（纪律 AA）：**8 文件约 20 处消费点一起改** + **前后读数**」
> 🔴 **本轮我只做"量"**（只读 E 盘 ✓ 不碰任何在飞文件 ✓ 不改任何数据 ✓）

## 1. 原版的"表"在哪（实测）
| 表 | 路径（E 盘 · 一手） | 规模 | 里面装什么 |
|---|---|---|---|
| **buff 表** | `shared/buffs/base.buffs.json` | **769 KB · 2020 条** | 原版的 buff/机制态（含大量派生项）✓ |
| **quirk 库** | `shared/quirk/quirk_library.json` | 117.8 KB · **170 条** | 怪癖 + **折磨/美德类精神条目**（`classification=mental` 108 条 ✓） |

关键词计数（`base.buffs.json` 2020 条内）：
```
STUN **64** · BLEED **80** · MARK **22** · GUARD **10** · SHIELD **2** · STEALTH **1**
🔴 **TAUNT 0 · BOUND 0** ⇒ 这两个**原版 buff 表里根本没有** ⇒ 强烈指向"**我们自加**"（对应卡里 ③ 的候选）✓
   ⚠️ 但 `STUN 64` 里绝大多数是**派生项**（`STUNRESIST10` / `STUNACCDEBUFF` / `STUNBLIGHT…`）
   ⇒ 所以"机制态"还要再分【**根态** vs **派生**】两层 ⇒ 我下一步会量这个 ✓
```

## 2. 我们 22 条的**逐条归属**（先说结论：**按 id 名匹配不够用** ✗）
```
按 id 名匹配（大写去下划线）得到的粗糙结果：**8 条有名字对应 / 14 条未找到**
有对应：`stun`→`STUN*` · `mark`→`MARKED*` · `bleed`→`BLEED*` · `shield`→`SHIELD*` ·
        `affliction_fear`→`AFFLICTIONFEARFULBUFF*` · `affliction_selfish`→`AFFLICTIONSELFISHBUFF1` ·
        `virtue_focused`→`VIRTUEFOCUSEDBUFF*` · `stealth`→`IGNORE_STEALTH`（**语义还相反** ⚠️）
未找到（14）：`taunt` · `guard_attach` · `affliction_uncontrolled` · `bound` · `virtue_brave/resolute/inspired` ·
        `next_attack_boost` · `deaths_door_recovery` · `next_battle_sharpen/armor` · `pep_talk` ·
        `curio_altar_blessing_20/30`
🔴 **与卡里预期的"7/10/5"不符** ⇒ 📌 **说明 id 名匹配不是正确判据**：
   · 原版的美德叫 `VIRTUESTALWART` 之类（不是我们的 `virtue_brave`）⇒ 名字不同、**语义相同** ✓
   · `stealth` 的对应项 `IGNORE_STEALTH` 是**对抗方**的 buff ⇒ 名字像、**语义相反** ✗
   · `taunt`/`bound` 原版没有对应 ⇒ 更可能是"自加" ✓
```
## 3. ✅ 正确的下一步（**按语义匹配，而不是按名字**）
```
① 用**我们这侧的语义键**去原版表里找功能对应：
     我们的 `modifiers[].kind`（`damage_mod`/`prob_mod`/`stat_mod`/`state_flag`）+ `hooks[].timing`
     ↔ 原版的 `stat_type` / `stat_sub_type` / `rule_type`（**这正是我 M2 分类器已经建立的映射** ✓ 直接复用）
② 折磨/美德（7 条）⇒ 在 **quirk 库**里按 `is_positive` + `classification=mental` 找功能对应 ✓
   （实测 mental 108 条 ⇒ 空间足够；且"美德"应落在 `is_positive=true` 的那批 ✓）
③ `taunt` / `bound` 这类**原版表里 0 命中**的 ⇒ 归入 **③ 自加**（废掉或按红线 30 移层）✓ —— **但最终仍要策划确认** ✓
④ 🔴 **搬家的硬要求**（纪律 AA）：**消费点一起改** + **前后读数**（士气/折磨）⇒ 我下一步量**那 20 处消费点在哪**（8 文件）
```
## 4. 依赖与阻塞（如实）
```
🔴 **M3 依赖 M1（属性的"态"）与 M2（buff 原语层）** ⇒ 两者均未完全到位（M1c 未切 · M2 映射未裁）
   ⇒ 所以本轮**只交"量"的结果 + 正确方法**，**不动数据、不动消费点** ✓
✅ 我这边可直接续做（不依赖裁定）：② 的"根态 vs 派生"分层 · ④ 的 20 处消费点清单 ✓
```
