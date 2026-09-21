# 原版数据契约(E盘) ↔ 模仿实现(F盘) 对照与偏差地图

> 🔴🔴 **可信度标注（`#416` · 2026-09-18 · 复核前必读）**
>
> ```
> 🔴 **F5：本表【已部分过期】，且【自称抽样】—— 只当线索，不当基准** ⚠️
>    · **过期实例**：本表 M1 与"士气/压力"一节说 `sim/morale/` **为空** ——
>      🔴 **实测（`#416`）：`darkest/scripts/gameplay/sim/morale/` 有 3 个文件** ⚠️
>      （`sim/` 下：`run` 68 · `board` 11 · `pipeline` 11 · `director` 10 · `buffs` 7 · `skill` 7 ·
>        `survival` 5 · **`morale` 3** · `enemy` 3 · `turn` 3）
>    · **抽样声明（原文）**：「本表为**代表性抽样**，非 300+ 文件的逐行 diff」✅
> ⇒ ✅ **基准改以 🆕 `doc/modules/dd1_baseline.md` 为准**（**E 盘原版数据 = 一手**）✅
> ⇒ ✅ **本表的用途收窄为【线索清单】**：它列的偏差项（尤其 §4 的 6 条）**仍值得逐条核**，
>    但**每条都必须回到 E 盘原文复核后才能采信** ⚠️
> ```


> 目的:以 E 盘原版随游戏发布的**数据文件**为「原版接口契约真值」,对照 F 盘模仿项目的**实际建模**,标出模仿在哪些地方偏离了原版。
> 用法:把它当成你**学原版架构的偏差地图**——每一条「偏差」都指向一个原版有、但模仿没忠实还原(或没做)的机制,正是该去原版行为里求证的点。
>
> 方法说明:本表为**代表性抽样**,非 300+ 文件的逐行 diff。原版取 heroes/abomination、monsters/bloated_corpse、trinkets、curios、dungeons/_shared、scripts/map_generator 等真实文件;模仿端取 `core/contracts/*` 与 `sim/*` 真实代码。括号内为原文证据。

---

## 先看三个宏观架构差异

| # | 维度 | 原版(E盘) | 模仿端(F盘) | 含义 |
|---|------|-----------|------------|------|
| M1 | **引擎是否可读** | 安装内**无 exe / 无 Assembly-CSharp.dll / 无 *_Data**(已全盘确认) | 全部 C# 源码可读 | 原版架构在本机**读不到**,只能读其数据契约;F 是「某团队的 DD-like 解读」,≠ 原版 |
| M2 | **数据载体** | 自定义 `.darkest` 文本 + `.json` + `.csv`,游戏自带宽松解析器 | 标准 JSON(`System.Text.Json`)+ C# `record`/`config` 类 + **加载即炸 fail-fast** | F 用强类型 schema 取代原版「策划直接改文本」的工作流,工程更稳,但偏离原版数据格式 |
| M3 | **效果解析方式** | 技能引用**具名效果字符串**查全局库(`combat_skill .effect "Manacles Stun 1"`) | 同样**具名查表**(`IBuffLedger.Add(u,"TRINKET_...")` + `BuffDefsConfig.Get(id)` 不存在即抛异常) | 高度一致,且 F 加了「行为/约束/说明/空」四分类纪律——**这是学原版 buff 系统的好入口** |

---

## 逐系统对照表

| 系统 | 原版(E盘)接口契约(实测字段) | 模仿端(F盘)建模 | 偏差标记 |
|------|------------------------------|----------------|----------|
| **单位属性 UnitStats** | `weapon`(atk/dmg/crit/spd,分 upgrade 0–4 阶) + `armour`(def/prot/hp/spd,分阶) + `resistances`(.stun/.poison/.bleed/.disease/.move/.debuff/.death_blow/.trap) | `UnitStats` 扁平 record:Hp/Attack/PhysDef/Speed/Dodge/Crit/Resilience/StunResist/BleedResist/StatDebuffResist/DisplaceResist/DeathsDoorResist?/MoveDistance | ⚠️ **装备分阶缺失**:原版 weapon/armour 是 0–4 可升级阶,塞在数据中;F 压平成「已结算终值」,UnitStats 无阶概念。<br>⚠️ **抗性集缩水**:原版有 poison/disease/death_blow/trap 抗性;F 只有 Stun/Bleed/StatDebuff/Displace(+Resilience),未单列疾病/中毒/死门/陷阱抗性(或合并)。 |
| **阵型 Formation** | rank 数字(1–4)+ **位掩码**(`combat_skill .launch 4321 .target 123`,数字即 rank 位掩码) | `IFormation`:我方 **6 槽**/敌方 **4 槽**,`TrySwapChain`(逐级交换链,撞障碍照常交换,不靠齐)、`CloseUp`(向中靠齐) | ✅ 机制还原度高(交换链/靠齐是 DD 招牌)。<br>⚠️ **槽位数**:F 用「我方 6 / 敌方 4」;原版战斗为 **4v4**(每侧 4 rank)。需确认 F 的 6 槽是否为战斗阵型还是道路/镇上阵型——若是战斗阵型则偏离原版。 |
| **技能 Skill** | `combat_skill`:id/level/type(ranged·melee·move)/atk/dmg/crit/launch/target/.effect(具名)/valid_modes(human·beast)/per_turn_limit/per_battle_limit/is_continue_turn/refresh_after_each_wave/is_stall_invalidating | `ISkillUseResolver.Resolve(caster,skillId,snapshot)`→`Availability`(BadStance/NoTarget/OnCooldown/UsesExhausted/RequiresUnmet/SupportPointsNotEnough)+ `SkillExecutor`/`SkillTargetResolver` | ✅ 可用性判定/冷却/每场次数与原版对应。<br>⚠️ **支援位 SP**:F 引入 `SupportPointsNotEnough`(战斗位/支援位);原版用 rank 前后排表达,F 的「支援位」概念需确认是否与原版 rank 模型一致。 |
| **Buff/增益** | 纯**具名引用**:`trinkets.buffs:["TRINKET_CROW_...","TRINKET_ANCESTOR_STRESSDMG"]`、`deaths_door .buffs ...recovery_buffs ...heart_attack_buffs` | `IBuffLedger`(Add/AddCharged/Remove/StatModTotal/PercentMod/HasStateFlag/TickRounds)+ `BuffDefConfig`(modifiers+hooks+duration+stack,四分类纪律) | ✅ **最忠实的系统**:具名查表 + 修改器+钩子+生命周期,且 F 比原版更严(引用不存在 buff 即炸)。学原版 buff 体系直接看这里。 |
| **饰品 Trinket** | `base.entries.trinkets.json`:id / buffs[] / hero_class_requirements[] / rarity / price / limit / origin_dungeon(= 一组 buff 引用 + 约束) | 应在 `data/` 经 `BuffDefsConfig` 间接承载(未单独读 trinket 配置类) | ⚠️ **未直接对照**:原版饰品 = buff 列表 + 职业/稀有度/来源约束;F 似乎把饰品 buff 走 `BuffDefsConfig`,饰品自身配置类未在本次抽样内,需补读确认约束字段是否齐全。 |
| **奇物 Curio** | `curio_type_library.csv`:加权结果表(RESULT TYPES/WEIGHT/%CHANCE,多结果列)+ ITEM 交互列 | `CurioConfig`:`bare_hands`(chance 和=100 的加权表)+ `item_results`(**正确道具=100% 确定好结果**,不掷骰) | ✅ 概念一致,且 F 显式落实「用错道具=确定坏结果」(#272/#313)。<br>ℹ️ 结构更紧(原版是扁平 CSV 多列)。 |
| **怪物 Monster** | `*.info.darkest`:display.size / enemy_type(可多值:eldritch+unholy)/ stats(hp/def/prot/spd+各类 resist)/ skill / personality.prefskill / initiative.number_of_turns_per_round / **monster_brain.id** / life_link / battle_modifier(能否先手) / **wave_spawning.prefers_front** | `sim/enemy/EnemyAi.cs` + `sim/director/BattleDirector.*` | ✅ 框架对应(怪物 AI + 战斗导演)。<br>⚠️ 原版 **monster_brain id / wave_spawning 偏好 / initiative 每轮行动次数** 是否在 F 建模需补读 `EnemyAi` 确认——这些是原版怪物差异化的核心。 |
| **士气/压力 Morale** | 庞大系统:stress、affliction、virtue、heart_attack、deaths_door 多 buff、beast 形态 `stress_damage_per_turn 6` | `sim/morale/` 目录**为空**;相关逻辑散落 `buffs/AfflictionProcs.cs` 与 `pipeline/MoraleLedger.cs` | 🔴 **重大偏差/未整合**:原版灵魂机制(压力→折磨/美德)在 F 尚未成体系模块。学原版务必亲自跑游戏观察压力曲线,因为代码里这一块还稀薄。 |
| **回合/行动序列** | speed 排序 + 眩晕跳过 + 死亡实时剔除 + 高 speed 可能多回合 | `ITurnSequencer.BuildRoundOrder`(每回合重掷 speed 浮动 0–10%)/`NextActor`(跳过眩晕)/`OnRemoved` | ✅ 结构一致。<br>⚠️ 「每回合重掷速度浮动」是 F 的设计选择(#163);原版实际为固定 speed+小随机?需对照原版行为确认是否一致。 |
| **战斗结算管线** | 命中→暴击→伤害→具名效果(逐段) | `sim/pipeline`:HitStep→DamageStep→EffectsStep→DisplaceStep + `MoraleLedger` | ✅ 逐段管线与原版一致。 |
| **地牢生成** | `scripts/map_generator.darkest`:size/quest_type/dungeon_type/base_room_number/base_corridor_number/gridsize/spacing/connectivity/min_final_distance/hallway_* 与 room_* 概率范围 + `dungeons/_shared/shared.json`(出生难度表) | `sim/run`:DungeonGrid + DungeonGridDeriver + DungeonGridConfig + ExpeditionMap + CurioResolver + ExpeditionFlow | ✅ 数据驱动程序生成,结构更清晰,对应度高。 |
| **平衡/数值** | 部分外置(shared.json 出生表等),核心公式在引擎(黑箱) | `BalanceTable`(tuning.json 只读快照,所有公式常量外置)+ 加载即炸 | ℹ️ F 更工程化(全外置 + 只读);原版更黑箱。这是 F 的纪律,非原版特征。 |
| **生存(光照/饥饿/死门)** | `survival`:light/hunger(trap/food)、`deaths_door` 多 buff、`beast.stress_damage_per_turn` | `sim/survival` 仅 `WeakDeathsDoor.cs`;光照在 `run/DungeonWalkLight.cs`,饥饿在 ExpeditionFlow | ⚠️ 生存系统**分散未整合**:原版光照/饥饿/死门是连贯的 survival 层,F 拆到 survival/run 多处,且薄弱。 |

---

## 偏差分级速览

- 🔴 **重大 / 未做**:士气·压力·折磨系统(`morale/` 空);生存层分散薄弱。
- ⚠️ **需补读确认**:怪物 brain/wave_spawning、饰品配置类、支援位 vs rank、6 槽 vs 4v4、速度重掷。
- ℹ️ **结构/工程差异(非逻辑错)**:数据格式(JSON vs .darkest)、平衡外置(tuning.json)、buff 四分类纪律。
- ✅ **高度忠实(可直接当原版教材)**:Buff 具名查表、阵型交换链/靠齐、战斗结算管线、地牢程序生成、奇物确定结果。

---

## 给你的学习建议

1. **接口层**:直接精读 E 盘 `heroes/*/*.info.darkest` 与 `monsters/*/*.info.darkest`——它们是原版单位契约的「真值」,字段最密。
2. **效果系统**:用 F 盘 `IBuffLedger` + `BuffDefsConfig` 当「带注释版」学原版 buff 模型(具名+修改器+钩子+生命周期),再去 E 盘 `trinkets/*.json` / `deaths_door` 找原版用法。
3. **找原版真身**:本机无原版 DLL,若要读**真实架构**,需拿一份完整安装(`DarkestDungeon_Data/Managed/Assembly-CSharp.dll`)用 dnSpy/ILSpy 反编译——那是唯一 faithful 源。
4. **用偏差倒逼学习**:本表里每条 🔴/⚠️ 都是「原版有、模仿薄/偏」的点,优先去原版游戏里观察这些行为(尤其压力曲线、怪物 brain 差异、4v4 站位),再回 F 盘看它怎么补——补得对不对,正是你判断自己懂没懂原版的标准。
