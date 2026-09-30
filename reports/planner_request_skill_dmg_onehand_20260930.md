# 策划请求 · 技能 `dmg%`【一手口径】复核 —— 14 条有出处的逐条对账 · 23 条一手↔参考冲突 · **只请裁 2 条**

> 🕒 2026-09-30 · 来自**主程序**（推送收口 · P0 数据请单）
> 🔴 **判据**：`doc/architecture/source_priority.md`（**你 2026-09-26 的裁定**「**选1,如果E盘读不到数值就用参考**」）
>    ⇒ **一手 E 盘 = 第一来源** · **参考项目 = 兜底（仅当 E 盘读不到）** —— 而 `#473`「**一律采用参考**」**已被你取代** ✓
> 🧰 **全部读数可复跑**（**只读，不写游戏数据**）：
>    `python tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py --check`
>    `python tools/dsh/extract_dd1_hero_skills.py`（一手读取器 · **两张表共用同一个解析器** ⇒ 不会是两套解析结果 ✓）
> 📄 完整对账报告：`reports/edrive_vs_ref_skill_dmg.md` · 一手原始表：`reports/dd1_hero_skills_from_edrive.md` / `.json`
> 📌 **送达判据**：`Select-String -Path doc\windows\策划窗口.txt -Pattern 'DELIVERY-LEAD-SKILL-DMG-ONEHAND-20260930' -SimpleMatch` 命中即送达。

---

## 0. 一句话

🔴 **本件不是「请你补值」—— 44 条技能现在【全部有 `dmg_pct`】** ⇒ 它是**请你复核**：
   **其中【真正需要你裁的只有 2 条】**（两条都是「我们把**参考值**落库了，而**一手**另有一个值」）✓

| # | 我方技能 | 现落库 | 🔴 **一手（E 盘原版）** | 一手证据 |
|---|---|---:|---:|---|
| **①** | `warrior_lunge` | **−55** | **−50** | `heroes/hellion/hellion.info.darkest:34`（`breakthru`）|
| **②** | `commissar_burst_fire` | **−60** | **−50** | `heroes/highwayman/highwayman.info.darkest:28`（`grape_shot_blast`）|

其余的账**已经在本次全部结清**，都不用你花时间：

```
✅ 12 条有出处的   ⇒ 与一手【逐条相符】（含 5 条非零）⇒ 不动 ✓
🔴 23 条一手↔参考冲突 ⇒ 【只有上面那 2 条打到我们落库值上】；其余 21 条只登记（§5）✓
✅ 30 条我们自加的 ⇒ 取 0 —— 架构已在 dd1_baseline §61.2 【确认】
                     （理由：Σ段倍率 在承担技能倍率 ⇒ 这 30 条取 0 是对的）⇒ 不用裁 ✓
```

⏱️ **时效（重要）**：这 2 条**现在改是【零行为】的** —— 伤害路径**还没读** `dmg_pct`
   （守卫测试 `WeaponDamageModelStage1Tests.NoProductionCodeCallsTheNewModel_SoOldReadingsCannotChange` 钉着「生产代码 0 调用」✓）
   ⇒ **游戏里一点没变** ✓；一旦 **M1c 阶段 3 切默认**，它们才变成**平衡数值改动** ⇒ 那时要等解冻窗口 ✓

> 📎 **与上一封的关系**：`reports/planner_request_skill_dmg_mapping.md`（2026-09-26 · 请裁 30 条）
>    —— 那封**你已裁完**（`#475`）并**落库**；本件是**同一件事在【一手口径】下的复核**，只涉及 2 条 ✓

---

## 1. 现况（实测 · 可复算）

| 项 | 读数 | 判据 |
|---|---:|---|
| 技能总数（`darkest/data/skills.json`）| **44** | `skills[]` 长度 |
| **有 `dmg_pct`** | **44** | 字段直取（**没有一条缺**）✓ |
| 其中 **有出处**（`_dmg_pct_source`）| **14** | **8 = `ref:`**（参考项目）· **6 = `dd1:`**（一手）✓ |
| 其中 **无出处**（我们自加）| **30** | 每条带 `value_source: none` + `origin: ours`（**显式声明，不是"忘了填"**）✓ |
| 14 条里**非零**的 | **7** | `lunge −55` · `warrior_war_cry −100` · `heavy_ram −60` · `medic_anesthetic −100` · `lethal_injection −80` · `command_blade +15` · `burst_fire −60` ✓ |

⚠️ **口径警告（写给你，也写给后人）**：**这 14 条不许用「0」代替** ——
   **一手在其中 7 条是非零**（其中 `barbaric_yawp`/`blinding_gas` = **−100**，最极端）✓

📌 **范围**：本件只覆盖 **4 个主职池内**的技能（`warrior_` / `tank_` / `medic_` / `commissar_`）——
   **这 14 条全在池内** ✓；NPC 单位（`melee_*` / `ranged_*` / `caster_*`）与通用 `move` 共 8 条**都在那 30 条自加里**，不在本件 ✓

---

## 2. 一手口径：它是怎么读出来的 · 读数

```
📁 根：E:\SteamLibrary\steamapps\common\DarkestDungeon
🧰 读取器：tools/dsh/extract_dd1_hero_skills.py
   ⇒ 15 个英雄 .info.darkest + 28 个共享文件 = 43 文件 ⇒ 525 行记录（105 技能 × 5 档）✓
🎯 取的是每行的 .dmg 字段（原版的「技能伤害修正」）
```

#### 2.1 一手 `.dmg` 的全分布（**105 条 · L0** —— 一张表说明"它不是默认 0"）

```
0 ×36 · −100 ×12 · −50 ×10 · −75 ×6 · −80 ×4 · −90 ×3 · −60 ×3 · −33 ×3 · −15 ×3 · −25 ×2 · −40 ×2 · −10 ×2 · +50 ×2
· −95 / −85 / −67 / −65 / −35 / −20 / +15 / +20 / +40 各 ×1
· 🔴 整行【没有 .dmg 字段】×8
⇒ 加法自检：36+12+10+6+4+3+3+3+3+2+2+2+2+9+8 = 105 ✓（**划分表相加必须等于总数**）
```

**8 条两侧都没有 `.dmg` 字段**（写法一致，**不是冲突**）：
`battle_heal` · `battlefield_bandage` · `battlefield_medicine` · `divine_grace` · `fortifying_vapours` · `gods_comfort` · `inspiring_cry` · `wyrd_reconstruction`

#### 2.2 🔴 等级错位**已排除**（这是本件能"一比一"的前提）

```
📊 冲突的 23 条：一手侧 22/23 在 L0..L4 【全同值】 · 参考侧 23/23 全同值
   ⇒ 唯一逐档变化的是 intimidate（一手 L0..2 = −85 · L3/4 = −80）
⇒ ✅ 两手比的是【同一个东西】，两条不符**不是**因为拿错了档位 ✓
📊 而我们落库的那 14 条一一对应的 14 个一手技能：L0..L4 【全部恒定】✓（可复跑 p0 探针 / 见 §3）
```

---

## 3. 14 条有出处的：逐条对账（**一手 = 判据**）

| # | 我方技能 | 落库 | 我方出处（现状）| 一手 id | 一手 L0 | 一手证据 `file:line` | 参考 L0 | 判定 |
|---:|---|---:|---|---|---:|---|---:|---|
| 1 | `warrior_cleave` | 0 | `ref:Hellion/wicked_hack` | `wicked_hack` | 0 | `heroes/hellion/hellion.info.darkest:13` | 0 | ✅ |
| 2 | `warrior_sweep` | 0 | `dd1:iron_swan` | `iron_swan` | 0 | `hellion:18` | 0 | ✅ |
| 3 | **`warrior_lunge`** | **−55** | `ref:Hellion/breakthru` | `breakthru` | **−50** | `hellion:34` | −55 | 🔴 **§4 请裁** |
| 4 | `warrior_war_cry` | **−100** | `dd1:barbaric_yawp` | `barbaric_yawp` | −100 | `hellion:23` | −100 | ✅ |
| 5 | `warrior_battle_fury` | 0 | `ref:Hellion/adrenaline_rush` | `adrenaline_rush` | 0 | `hellion:39` | 0 | ✅ |
| 6 | `tank_guard_wall` | 0 | `ref:ManAtArms/defender` | `defender` | 0 | `man_at_arms/man_at_arms.info.darkest:28` | 0 | ✅ |
| 7 | `tank_shield_bash` | 0 | `dd1:crush` | `crush` | 0 | `man_at_arms:13` | 0 | ✅ |
| 8 | `tank_heavy_ram` | **−60** | `dd1:rampart` | `rampart` | −60 | `man_at_arms:18` | −60 | ✅ |
| 9 | `tank_war_cry` | 0 | `ref:ManAtArms/command` | `command` | 0 | `man_at_arms:39` | 0 | ✅ |
| 10 | `medic_scalpel` | 0 | `dd1:incision` | `incision` | 0 | `plague_doctor/plague_doctor.info.darkest:28` | 0 | ✅ |
| 11 | `medic_anesthetic` | **−100** | `ref:PlagueDoctor/blinding_gas` | `blinding_gas` | −100 | `plague_doctor:23` | −100 | ✅ |
| 12 | `medic_lethal_injection` | **−80** | `dd1:noxious_blast` | `noxious_blast` | −80 | `plague_doctor:13` | −80 | ✅ |
| 13 | `commissar_command_blade` | **+15** | `ref:Highwayman/wicked_slice` | `wicked_slice` | +15 | `highwayman/highwayman.info.darkest:13` | +15 | ✅ |
| 14 | **`commissar_burst_fire`** | **−60** | `ref:Highwayman/grape_shot_blast` | `grape_shot_blast` | **−50** | `highwayman:28` | −60 | 🔴 **§4 请裁** |

⇒ **12 条相符 · 2 条不符**（不符的两条**恰好就是上一轮"按参考纠正"过的那两条** —— 见 §4）✓

---

## 4. 🔴 请你裁的 2 条（**每条只需一个数字**）

#### 4.1 事实（两条同型）

```
🔴 上一轮（2026-09-26）执行的是 #473「数值一律采用参考项目」⇒ 这两条【按参考改了】：
   · warrior_lunge        −50 ⇒ −55   （出处写 ref:Hellion/breakthru (纠正:旧 -50)）
   · commissar_burst_fire −50 ⇒ −60   （出处写 ref:Highwayman/grape_shot_blast (纠正:旧 -50)）
🔴 而 #473 已被你 2026-09-26 的裁定【取代】（source_priority.md §1b：一手优先、读不到才用参考）
   ⇒ 同一件事在【一手口径】下的读数：两条都是 −50（E 盘原版 .dmg 实测，证据见 §3 第 3/14 行）✓
📌 即：**我们改过的两个值，一手不认** —— 这不是"两个源各有说法"，而是【改错了口径】✓
```

#### 4.2 两个选项（**都是零行为** —— 现在改，游戏里一点没变）

| 选 | 含义 | 我要做的 | 登记 |
|---|---|---|---|
| **①（我的建议）** | **两条都回一手 `−50`** | 改 2 个 `dmg_pct` + 更新 `_dmg_pct_source` 为 `dd1:` + 写前后读数 | 撤销一次口径错，**不是**新偏离 ⇒ 仍入 `§39`（**留痕**）✓ |
| ② | **保持 −55 / −60** | 一个数都不改 | 🔴 必须记为「**刻意偏离一手**」⇒ 入 `§39`，并在数据里写明「此处不用一手」的理由 ✓ |

🔴 **我不替你选**：②也可能是有意的（例如你已经知道这一版平衡要它更强/更弱）⇒ 但**若你不说，默认值就是 ①** ✓

---

## 5. 🔴 一手 ↔ 参考的 23 条冲突（**登记，不请裁**）

```
📊 读数（L0）：一手 skill id 105 · 参考 skill id 105 · 两侧都有 105 ⇒ 【逐条可比】
   · 相同 82 · 🔴 不同 23 · 两侧都无字段 0 · 只有一手 0 · 只有参考 0 ✓
⇒ 其中【只有 2 条】打到我们落库值上（§4）⇒ **其余 21 条与我们的数据无关，故只登记** ✓
```

| skill id | 一手 L0 | 参考 L0 | 一手证据 `file:line` |
|---|---:|---:|---|
| `abyssal_artillery` | **−33** | −25 | `heroes/occultist/occultist.info.darkest:18` |
| `bellow` | **−100** | −90 | `heroes/man_at_arms/man_at_arms.info.darkest:23` |
| `blackjack` | **−65** | −60 | `heroes/houndmaster/houndmaster.info.darkest:44` |
| `bleed_out` | **+20** | +15 | `heroes/hellion/hellion.info.darkest:44` |
| 🔴 `breakthru` | **−50** | −55 | `heroes/hellion/hellion.info.darkest:34` |
| `come_hither` | **−80** | −67 | `heroes/bounty_hunter/bounty_hunter.info.darkest:23` |
| 🔴 `flare` | **−100** | **0** | `heroes/arbalest/arbalest.info.darkest:44` |
| 🔴 `focus` | **−40** | **−90** | `heroes/leper/leper.info.darkest:23` |
| `gods_illumination` | **−75** | −50 | `heroes/vestal/vestal.info.darkest:39` |
| 🔴 `grape_shot_blast` | **−50** | −60 | `heroes/highwayman/highwayman.info.darkest:28` |
| 🔴 `heroic_end` | **+50** | **+150** | `heroes/jester/jester.info.darkest:23` |
| `hew` | **−50** | −40 | `heroes/leper/leper.info.darkest:18` |
| `hook_and_slice` | **−95** | −85 | `heroes/bounty_hunter/bounty_hunter.info.darkest:44` |
| `hounds_harry` | **−75** | −80 | `heroes/houndmaster/houndmaster.info.darkest:18` |
| `intimidate` | **−85**（L3/4 = −80）| −75 | `heroes/leper/leper.info.darkest:44` |
| `judgement` | **−25** | −20 | `heroes/vestal/vestal.info.darkest:18` |
| `pick` | **−15** | 0 | `heroes/grave_robber/grave_robber.info.darkest:13` |
| `pistol_shot` | **−15** | −25 | `heroes/highwayman/highwayman.info.darkest:18` |
| `poison_dart` | **−60** | −90 | `heroes/grave_robber/grave_robber.info.darkest:39` |
| `rake` | **−50** | −40 | `heroes/abomination/abomination.info.darkest:34` |
| `stunning_blow` | **−50** | −75 | `heroes/crusader/crusader.info.darkest:23` |
| `thrown_dagger` | **−10** | 0 | `heroes/grave_robber/grave_robber.info.darkest:34` |
| `vomit` | **−90** | −100 | `heroes/abomination/abomination.info.darkest:23` |

🎖️ **差得最远的三条**（说明"参考 ≠ 一手"不是一句空话）：`flare`（**−100 vs 0**）· `heroic_end`（**+50 vs +150**）· `focus`（**−40 vs −90**）✓
   ⇒ ⚠️ 它们**不是我们的技能**（我方没有对应用法），所以**不影响落库**；但它们证明：
      **凡"从参考抄来的数"，都值得回一手核一次** ✓（同族：`source_priority.md §1b` 点名的「A2 技能 `dmg%` 11→14 条」）

---

## 6. ✅ 那 30 条自加的：**不用裁**（架构已确认）

```
🔴 架构裁定（dd1_baseline §61.2，原话照录）：
   「⇒ ✅ 而这也解释了另一件事：**为什么 30 条自加技能的 dmg% 取 0 是对的** ——
      **因为 Σ 本来就在承担倍率** ✓（📌 策划"30 条 dmg%=0"的裁定**架构确认** ✓）」
⇒ 📌 即：技能之间的强弱【由 Σ段倍率 表达】（0.5 / 0.6 / 0.9 / 1.5 / 1.8 / 2.0 …）·
      `dmg%` 只承担【相对原版技能的那一层对齐】⇒ 自加技能没有原版对应 ⇒ 取 0 **是中性值，不是缺失** ✓
```

⚠️ **但有一条同日到期的事**（只登记，等你解冻）：`Σ段倍率` 的**语义**本身（§61.3 第二条路）
   ⇒ 若将来**把 Σ 归一 + 把差额搬进 `dmg%`**，那 **44 条的 `dmg%` 会整体重算** ⇒ 那是一次**重做平衡**，
      与本件的 2 条无关（**不要混批**）✓

---

## 7. ⚠️ 两处注记文本是**旧口径**（只登记，不改）

```
🔴 ① darkest/data/skills.json:2  _dmg_pct_note
   现文：「A2+A3 之后：技能 dmg% 的来源 = 【本地参考项目】`Heroes/Info/*.bytes` 的 `.dmg` 字段」
   ⇒ ⚠️ 这是 #473 口径（已取代）⇒ 现行应为「**一手 E 盘 .info.darkest 的 .dmg；读不到才用参考**」
🔴 ② darkest/data/skills.json:1800  _sigma_note
   现文：「🆕 策划 #475 裁定：**Σ段倍率 归一为 1** …… 纪律 BS：原版没有 ⇒ 必须中性化」
   ⇒ 🔴 而【该裁定已被架构 §61 否决】（§61 标题即「Σ段倍率 归一【不能那样做】」）
       ⇒ **数据文件里留的偏偏是被否的那一条** ⚠️
⇒ ✅ 处置：**数值冻结期只登记不动**（纪律 AU：读到不一致先记，不改）⇒ 解冻窗口改**文本**（零行为）✓
```

---

## 8. 诚实边界（**我能验 / 不能验**）

```
✅ 能验（已实测 · 可复跑）：
   · 一手 105 条的 .dmg / 5 档 —— reports/dd1_hero_skills_from_edrive.{md,json}（逐条带 file:line）
   · 一手 ↔ 参考 105↔105 逐条对账（82 同 / 23 异）—— reports/edrive_vs_ref_skill_dmg.md
   · 我们 14 条出处的逐条复核（12 相符 / 2 不符）—— 同上
   · 「生产代码 0 调用新伤害模型」—— darkest/tests/WeaponDamageModelStage1Tests.cs（守卫）
⚠️ 不能验（我没做的，别当我做了）：
   · 我没读参考项目的 C# 运行逻辑去反推「原版为什么给 breakthru −50」⇒ .dmg 的【设计意图】是推的
   · 本件只覆盖【英雄技能】的 .dmg ⇒ 怪物技能 / 效果表 / 词缀表 的 .dmg 类字段不在本件
   · 改这 2 条的【平衡位移】没测（现在测也没有意义 —— 机制还没接线，读数不会动）✓
   · 一手读取器只解析 .dmg/.atk_pct/.type/.effect 等字段，**没有做全文校验**（别的字段不保证）
```

---

## 9. 谁消费这张表（落地证据）

| 消费点 | 怎么用它 |
|---|---|
| `darkest/data/skills.json` 的 `dmg_pct` | **就是本表的落点**（14 条有出处 + 30 条自加）✓ |
| `WeaponBaseDamage` / `BattleMath.WeaponRawDamage` | 公式 `区间 × Σ段倍率 × (1 + dmg%/100)` —— **纯机制，无生产调用方**（守卫钉住）✓ |
| **M1c 阶段 3（P2）** | **切默认那一步**：这 2 条的值会进平衡读数（前后对照 A1 / A2 / V10）⇒ **本件是它的硬前置** ✓ |
| `dd1_baseline §39`（解冻清单） | ②选项（保持 −55/−60）= 「**刻意偏离一手**」⇒ **必须登记在这里** ✓ |
| `reports/edrive_vs_ref_skill_dmg.md` | 对账的**留存面**（入 git）· 复跑一条命令 ✓ |

---

## 10. 我要你回什么（**可执行的三行**）

```
① warrior_lunge        = −50（回一手 · 我的建议） 还是 −55（保持参考）？
② commissar_burst_fire = −50（回一手 · 我的建议） 还是 −60（保持参考）？
③（可留空）§5 那 23 条冲突，要不要我在【解冻窗口】按一手一次性重转？（默认：只登记，不动）
```

⇒ ✅ 你回完，我**当场落库 + 写前后读数 + 报提交号**（这 2 条是零行为 ⇒ 不需要等解冻窗口）✓
⇒ 🔴 本件**不阻塞**其它一切工作：`P1`（M1c 阶段 2 夹具）已完成 · `P2`（切默认）等的是你**这一回**✓

---

```
DELIVERY-LEAD-SKILL-DMG-ONEHAND-20260930
```

- **阻塞 / 待裁定**：**只有上面 2 个数**（其余全部结清）✓
- **权威在哪**：`doc/architecture/source_priority.md`（源优先级唯一真值）· `reports/edrive_vs_ref_skill_dmg.md`（对账留存面）·
  `reports/planner_request_skill_dmg_mapping.md`（上一封 · 已被 `#475` 裁完）✓
