# A6 后续：参考的 29 种战斗 act-out vs 我方 —— **是【整层缺失】**，且与 A6 的判定交叉印证

> 🕒 2026-09-26 · 承接 `18_curio_three_stage_limits.md` §5 与 A6 的"act-out 落点"待裁 ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 参考的两张 act-out 表：**29 种，语义完全清楚**

### ① 回合开始 **14 种** —— **角色"自作主张"**

```
`nothing` · `bark_stress`（喊话+压力）· `change_pos`（乱跑位）· `ignore_command`（无视命令）·
`random_command`（随机行动）· `retreat_from_combat`（脱离战斗）· `attack_friendly`（**打友方**）·
`attack_self`（**打自己**）· `mark_self`（自我标记）· `stress_heal_self`（自我减压）·
`stress_heal_party`（全队减压）· `buff_random_party_member`（随机给友方增益）·
`buff_party`（全队增益）· `heal_self`（自我治疗）✓
⇒ 🎖️ **即：这正是「折磨/美德」在战斗里的【行为表现】** ✓
   · **折磨侧**（`attack_self` · `attack_friendly` · `ignore_command` · `retreat_from_combat`）⇒ **作妖** ✓
   · **美德侧**（`stress_heal_party` · `buff_party` · `heal_self`）⇒ **撑场** ✓
   📌 而这与 A6 量出的 `chance` 权重**方向一致**：
      **7 个折磨的 `nothing` 权重 4~10（常作妖）· 5 个美德一律 3（少见）** ✓
```

### ② 反应 **15 种** —— 分成**两类**（我实测出来的）

```
**`block_*` 5 种**（**真的拦**）：
   `block_move` · `block_heal` · `block_buff` · `block_item` · `block_combat_retreat` ✓
**`comment_*` 9 种**（**只是台词**）：
   `comment_self_hit` · `comment_self_missed` · `comment_ally_hit` · `comment_ally_missed` ·
   `comment_ally_attack_hit` · `comment_ally_attack_missed` · `comment_move` ·
   `comment_curio_interaction` · `comment_trap_triggered` ✓
**`block_effect` 1 种** —— 🎖️ **归 `block` 类还是 `comment` 类，本件未判** ⚠️（如实记）✓
⇒ 🎖️ **即：反应表一半是【真机制】、一半是【氛围台词】** ✓
   📌 而"台词"那 9 种**与战斗数值无关** ⇒ 采用时**可以后做** ✓
```

## 2. 🔴 而我方的 `ActOut` —— **不是同一层**

```
🔴 我方也有 `ActOut` 这个名字（36 处置），但实测它的内容是：
   `ExplorationActOut.RollCurioRefuse`（`ExpeditionFlow.RoomInteractions.cs:173`）
   ⇒ 📌 **即：我方的 `ActOut` 是【探索层的"拒绝摸奇物"】** ✓
   `LastActOutRefused` · `LastActOutText` · `ActOutCurioRefuseCount` ✓
⇒ ⚠️ **两者【不是同一层】**：
   · **我方** = **探索层**（摸奇物时的拒绝）✓
   · **参考** = **战斗层**（回合开始 + 反应）✓
⇒ ✅ **结论：我方【没有】参考那 29 种战斗 act-out** ⇒ **是【整层缺失】** ⚠️
   🎖️ **注意**：这**不是**"我方的 ActOut 写得不对" —— 而是**名字撞车、层次不同** ⚠️
      📌 **判据**：**"这两个同名概念，是【同一个】还是【名字撞了】？"** ——
         **看挂点（哪一层/什么事件）⇒ 不同 ⇒ 就不是同一个** ✓
```

## 3. 🎖️🎖️ 与 A6 的判定**交叉印证**（两个独立读数指向同一结论）

```
📊 **A6 的读数**（`08_traits_from_ref.md`）：用 `buff_ids` 签名比对 ⇒
   **我方那 7 条【一条都不是】参考 12 条的对等物** ✓
📊 **本件的读数**（读代码 + 读表）：**参考的 trait = 属性 buff 包 + 战斗 act-out（29 种）**，
   **而我方 = 概率修正（`prob_mod`）+ hooks** ✓
⇒ 🔴 **两条独立读数指向同一结论**：
   🎖️ **两者是【不同的机制】，不是"同一套的不同实现"** ✓
   ⇒ 📌 **即：A6 的真缺口 = 6 折磨 + 4 美德 + 29 种 act-out**（**三块都要新建**）✓
```

## 4. 🎖️ 而我方现有 hooks 的"时机"与参考**部分对得上**

```
我方 `traits.json` 的 hooks（实测只有 **4 条**有 hooks）：
   `affliction_fear`         `{timing: "before_use_skill", effect: "33% refuse to act…"}` ✓
   `affliction_selfish`      `{timing: "before_heal",      effect: "33% refuse this heal"}` ✓
   `affliction_uncontrolled` `{timing: "before_attack",    effect: "33% randomize target…"}` ✓
   `virtue_inspired`         `{timing: "round_start",      effect: "team morale regen…"}` ✓
🎖️ **对照参考的 act-out 语义**：
   · 我方的 `before_use_skill` / `before_attack` / `before_heal` **在参考里【没有同名】** ⇒
     📌 因为参考的机制是**"回合开始时决定做什么"**，而我方是**"做事前掷一次骰"** ⚠️
   · 我方的 `round_start` **与参考的 `combat_start_turn_act_outs`【时机相同】** ✓
     ⇒ ✅ **即：只有 `round_start` 这一个时机是对得上的** ✓
🔴 而 `virtue_inspired` 的 hook 值写着 **"VALUE MISSING -> O-27"** ⇒ 我方**自己记的未接线** ✓
⇒ 📌 **即：我方 hooks 的时机集（4 种）与参考的（2 类事件）【不是同一套切法】** ⚠️
   ⇒ 🎖️ **与 A6 的"两套不同机制"结论一致** ✓
```

## 5. 诚实边界

```
✅ **能验**：参考 14 + 15 种 act-out 的**完整 id 列表与语义**（读表）·
   **反应表分 `block_*`(5) / `comment_*`(9)** · 我方 `ActOut` 的**真实内容**（读代码）·
   我方 hooks 的**时机集** · **两层不同** —— **全部当场跑出** ✓
🎖️ 并**与 A6 的判定交叉印证**（两条独立读数同一结论）✓
🔴 **不能验**：`block_effect` 归 `block` 还是 `comment` 类 ⇒ 记**未判** ✓
🔴 **不能验**：那 29 种 act-out 的**逐种行为语义未读代码**（我是从 id 名推断的）⇒ 记**推断** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ "接上后士气流会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：我方 `ActOut`（探索层）与参考的（战斗层）**要不要合并命名空间** ⇒ 记**待裁** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/actout_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **A6 这条线【核完】**：真缺口 = **6 折磨 + 4 美德 + 29 种战斗 act-out**（三块新建）✓
   ⇒ 🎖️ **而落库仍被"buff `amount` 是分数"的舍入口径阻塞**（A7 也一样）✓
🆕 **可做**：① 读代码核 `block_effect` 与 29 种的逐种语义
   ② 抽 `heirloom_exchange` / `enemy_ai` 的真实 id 键（补 `27_*.md` 的覆盖缺口）✓
⏸️ **等策划**：七张单已投递 ✓
```
