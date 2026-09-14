# 真机架构参考：Darkest Dungeon（**只学形态，不抄素材**）

> **参考路径（只读）**：`E:\SteamLibrary\steamapps\common\DarkestDungeon`
> **用户指令**：「这是真实的 DD 的路径，可以参考他的架构」⇒ 本文件是**架构侧的真机侦察结论**。
> 🔴🔴 **版权与纪律（先写在最前）**：
> ```
> ① 只看【格式 / 目录结构 / 数值组织方式】，**不抄任何文本、数值、美术、音频、动画资源**
> ② 真机使用 Spine（`.atlas`/`.skel` 1886×2）与 FMOD（`.bank` 64）等**商业中间件** ⇒ 素材**一律不碰**
> ③ 与既有红线一致：**禁止出现 DD 的美术资源**（V11 红线）；**wiki 正文为 CC BY-NC-SA、游戏本体为专有**
> ④ "学"的判据是三问：**它解决什么问题？我们有同一个问题吗？照抄会破坏我们的分层吗？**（见 §4）
> ```

---

## 1. 真机全景（实测计数）

```
内容根 = 40 个域目录（不是"一个巨大的 data 文件"）
载荷扩展名：.darkest 1180（自有数据格式）· .json 326 · .loc2 306（本地化）· .loc 12
             .png 6486 · .atlas/.skel 1886×2（Spine 动画）· .bank 64（FMOD）· .fnt 30（位图字体）
             .hlsl/.glsl/.xbc/.wbc（着色器，含平台变体）· .csv 16 · .xml 26 · .times 25
规模分布（文件数）：monsters 2794 · heroes 1950 · dlc 4248 · panels 634 · campaign 442 · dungeons 331
                     props 275 · shared 273 · fx 361 · localization 198 · mods 137 · raid 81 · upgrades 23
```

**三条一眼可见的架构事实**：
| # | 事实 | 意义 |
|---|---|---|
| **①** | **内容 = 数据 + 目录**（`heroes/` `monsters/` `dungeons/` `raid/` `campaign/` `upgrades/` `panels/` `shared/`） | 🔴 **没有"把内容编译进代码"这一步** ⇒ 平衡/内容**零代码改动** |
| **②** | **`dlc/`（4248 文件）与主内容【完全同构】** | 🔴 **DLC 不改 schema、只加内容** ⇒ 这正是"数据驱动"的收益兑现 |
| **③** | **`mods/<name>/` = 同名域目录的【纯文件覆盖】，且【没有清单文件】**（实测：mods 下无 `*.xml`/`*.json`/`*.txt`） | 🔴 **mod = 叠加层**，**无需 API、无需注册** ⇒ 内容包机制的成本几乎为零 |

---

## 2. 六条可借鉴的形态（与我们的现状逐条对照）

### (a) **"效果项数组 + 加载级白名单"—— 与我们同构 ✅（印证我们做对了）**
真机 `raid/camping/default.camping_skills.json`：
```
skills[] = { id, level, cost, use_limit,
             effects[] = { selection, requirements[], chance{code,amount}, type, sub_type, amount },
             hero_classes[], upgrade_requirements[] = { code, currency_cost[{type,amount}], prerequisite_requirements[] } }
```
⇒ **和我们的 `camp_skills.json` + `CampSkillsConfig`（未登记 effect ⇒ 启动报错）是同一个形态** ✅
⇒ **`#302` 的"消费点白名单"方向被真机印证**（DD 也是"数据里写 effect 名、代码里按名分发"）。
🔴 **真机比我们多的两项（候选）**：**`use_limit`（每趟使用次数上限）** · **`chance{code,amount}`（统一概率结构）**。

### (b) **按建筑分文件的【升级树】+ 每级 requirement（供 M8.3）**
真机 `upgrades/building/{abbey,blacksmith,guild,sanitarium,stage_coach,tavern,camping_trainer,nomad_wagon}.upgrades.json`（**一栋一个文件**）：
```
{ "trees": [ { "id": "stage_coach.numrecruits", "is_instanced": false,
               "tags": ["building","stage_coach"],
               "requirements": [ { "code":"a",
                                   "currency_cost":[{type:"gold",amount:0},{type:"deed",amount:3},{type:"crest",amount:4}],
                                   "prerequisite_requirements":[] }, … 后续级 ] } ] }
```
**对照我们**：M8.1 是**单文件 `heirlooms.json` + 三轴（降费/增强/解锁）+ 只有首级**。
🔴 **候选优化（供 M8.3，不是必须改）**：**① 按建筑分文件**（内容包友好）· **② 每级 = requirement 对象（**多货币成本 + 前置**）** ⇒ 把"先升哪个"从**三选一**升级为**树 + 前置**；**③ `id` 用命名空间**（`building.upgrade`）· **④ `tags` 用于筛选/联动**。
⚠️ **同时注意**：**M8.1 的三轴设计是策划拍的**（`#292`）⇒ 本项只作**形态参考**，不得据此擅自改已定设计。

### (c) **进度驱动解锁曲线（供 M8 解锁轴）**
真机 `campaign/quest/quest.generation.json`：
```
generation.dungeon = { generated_quests_max_threshold, generated_dungeons[{ id, required_number_of_quests_finished }] }
```
⇒ **"哪个地牢在第几个任务完成后解锁"是纯数据** ⇒ 对我们 **M8 的解锁轴（`stagecoach = unlock`）与内容解锁**是直接可借鉴的形态：**解锁 = `required_number_of_*` 的阈值表**（而不是代码里的 if）。

### (d) 🔴 **房间内容表 vs 拓扑：两层分离 —— 印证我们 M7.6 的分法是对的**
真机 `dungeons/<region>/<region>.<n>.mash.darkest` 的形态（**不是拓扑！**）：
```
hall: .chance 2 .types <怪物A> <怪物A> <怪物A>
room: .chance 1 .types <怪物A> <怪物C> …
```
⇒ 🔴 **`*.mash` = 【按房间类型 → 可能的编成池 + 权重】的内容表**，与"地图怎么长"（拓扑生成）**完全分离**。
⇒ **对照我们**：**拓扑** = `ExpeditionMapGenerator`（内核自研）· **内容** = `expedition_nodes.json` ⇒ **我们也是两层** ✅
🔴🔴 **但它指出我们一处该改的地方**：我们现在用**全局旋钮 `branch_battle_weight`** 去调"支路含不含战斗房"（`O-79`/`O-80` 那段调参史）。
**真机形态 = 【房间类型 → 编成池 + 权重】的表** ⇒ **建议把该旋钮演进为"按房间类型的内容表"**（**数据化**，且**权重与编成同表**）；这同时解释了 `O-79` 为什么难调：**我们缺的不是权重，是"类型 → 内容池"这一层**。⇒ 登记 **`O-85`**。

### (e) **本地化 = 双层（供 i18n，我们目前 0）**
真机：**每语言一个 `.loc2`**（12 种）+ **每域一个 `*.string_table.xml`** ⇒ **数据里存 key、语言文件存文本**。
⇒ 直接对应我们审计 §4 的 **⑩ i18n**（`godot_builtins_audit.md`）：🔴 **方案形态 = "key 间接层"**（`data/*.json` 里的文本字段改为 key，语言表提供文本）⇒ 挂 **Godot `TranslationServer`/`.csv`**（红线 26 已列）。

### (f) **mod = 纯目录覆盖（长线候选）**
真机 **无清单**：把同名路径文件放进 `mods/<name>/` 即生效 ⇒ **"base + overlay"**。
⇒ 对我们：**数据加载器支持"基础包 + 覆盖层"（按 id 合并）** 即可获得同能力 ⇒ ⚠️ **长线**（内核加载器要加"合并语义" + 冲突策略），**不建议现在做**，登记在 §3。

---

## 3. 候选清单（**登记，不自动开工**）

| # | 候选 | 借鉴形态 | 适用里程碑 | 备注 |
|---|---|---|---|---|
| **1** | **房间内容表**（类型 → 编成池 + 权重） | (d) | **M7.6**（`O-85`） | 🔴 **优先级最高**（它解释并替代 `branch_battle_weight` 那段调参） |
| **2** | **`use_limit` + 统一 `chance{code,amount}`** | (a) | M7.5 收口（营地技能） | 小；但**`chance` 结构统一**利于 `ProbMod` 的"按名分发"一致性 |
| **3** | **升级树 + 每级 requirement（多货币 + 前置）** | (b) | **M8.3** | ⚠️ **不得据此改 M8.1 已定三轴** |
| **4** | **解锁曲线 = 阈值表** | (c) | **M8**（解锁轴） | 形态便宜、收益直接 |
| **5** | **本地化 key 间接层** | (e) | i18n 立项时 | 与红线 26 ⑩ 合流；**它反过来影响"锚点/容器"排期** |
| **6** | **数据 overlay（mod 能力）** | (f) | 长线 | 需要加载器"合并语义 + 冲突策略" |

### 🔴 §3.1 策划裁定（`#315` · 2026-09-14）

| # | 形态 | 裁定 |
|---|---|---|
| **1** | **升级树 + 每级 requirement** | ✅ **M8.3 采纳该形态**（**一栋一文件 + 每级多货币成本 + 前置**）—— 理由：**M8.1 是【首级】最小版，M8.3 要做"建筑升级" ⇒ 真机形态是现成答案**；⚠️ **但【不动 M8.1 已定的三轴】**（`tavern=cost_down` / `abbey=effect_up` / `stagecoach=unlock`） |
| **2** | **解锁 = 阈值表** | ✅ **采纳** —— 🔴 **我们现在的"解锁轴"只有"抬高名册上限"一种，太薄** ⚠️ ⇒ **M8.3 加"阈值表"**（按【已完成出征数】解锁内容，如"第 3 趟解锁饰品槽 / 第 6 趟解锁精英战"）；🔴 **而"解锁什么"属【内容】⇒ 与 M8.3 / M7.6 一起定** ⏸ |
| **3** | **本地化（`.loc2` + `string_table`）** | ✅ **同审计 ⑩**：**M8.3 后做**，但 🔴 **现在就必须按【可 i18n】写布局**（容器 + 锚点） |
| **(d)** | 🔴 **`O-85`（房间类型 → 编成池 + 权重）** | ✅ **采纳演进，且【不改 `#294` 的定性】** —— `#294`① 的「**支路 = 额外机会**」**保留**；`O-85` 改的是**实现形态**（**全局粗旋钮 → 按房间类型的细表**）⇒ **两者不冲突**；🔴 **且它与 `Curio` 【同层】**（都是"房间内容"）⇒ 📌 **建议与 Curio 一起设计**（同层的东西一起定，省一轮） |
| (a) | **营地技能形态同构** | ✅ 确认（`effects[{type,sub_type,amount,chance}]` + `hero_classes[]` ⇄ 我方 `camp_skills.json` + `ConsumedEffectNames`） |
| (d) | **"拓扑生成"×"房间内容表"分两层** | ✅ 确认 —— 🔴 **并正好支持 `O-85` 的演进**（**我们缺的正是那"房间内容表"这一层**） |

---

## 4. 🔴 三条【不照抄】的判定（附理由 —— 学形态 ≠ 抄结构）

| 真机做法 | 我们**不照抄**的理由 |
|---|---|
| **① 技能定义里混表现绑定**：`combat_skill: .id "smite" .icon … .anim … .fx … .targchestfx … .sfx …` | 真机是**自定义引擎**、且接受"一个英雄 = 一个内容包"；**我们是 Godot + 明确分层（内核零 Godot）** ⇒ 🔴 **规则数据里不得出现 `anim/fx/sfx` 路径** ⇒ 表现绑定应走 **`id → 表现资源` 的映射**（表现层拥有），否则**内核会被表现污染、headless 直接跑不动** |
| **② UI 布局数据化**（`shared/controls/controls.layout.darkest`、`fe_flow/dlc.layout.darkest`） | 真机没有编辑器内 UI 体系，只能自己写布局格式；**Godot 的正解是 `Control` 树 + 容器 + 锚点 + `Theme`**（红线 26）⇒ **布局数据化对我们 = 倒退**；**可借鉴的只是"文案/图标 key 化"**（= i18n (e)） |
| **③ Spine 动画 / FMOD 音频 / 位图字体** | **版权 + 商业中间件**（红线：禁止 DD 素材）⇒ 我们**用自己的资源**；真机给我们的唯一启示是**"动效与音频值得投入"**（红线 26 的 ④⑤） |

---

## 5. 与既有文档的对账

| 本文件结论 | 落点 |
|---|---|
| 真机形态 (a) 印证"消费点白名单" | `O-82` / 红线 21 / `data_schema` P22 ⑧ |
| 候选 1（房间内容表） | **`O-85`** + `tasks/m7_6_verification.md`（V3 邻域） |
| 候选 5（本地化 key 层） | `godot_builtins_audit.md` §4 ⑩ + 红线 26 |
| 不照抄 ①（表现绑定不得进规则数据） | **红线 27**（参考真机纪律）+ `blueprint` §9.16 |
| 真机只读路径 | `skills/架构师.md` **§7.6** |
