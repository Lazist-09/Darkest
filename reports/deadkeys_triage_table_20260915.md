# deadkeys 17 条 · 三列归类表（键 / 判读 / 归属）—— 2026-09-15 主程序
#
# 判据（策划 `#328`④）：**"策划改了它，游戏会不会不同？"**
#   · 会 ⇒ **接线**（= 写了没接上，红线 21）⚠️
#   · 不会但有**文档价值** ⇒ **登记"未消费"**（工具已按文档键约定 `*_note`/`*_rule` 放行）✓
#   · 两者都不是 ⇒ **删**
# 归属：数据属**策划**（值/内容）· 契约属**架构** · 执行属**主程序** ✓
#
# ══ A. `buff_defs.json` 的 6 个键（都是 `extra_rules` 里的**语义注解**；你点名要逐个报"已实现 vs 打算做"）══
#
# 1) shield.not_consumed_by = ["bleed","displacement","self_damage"]
#    判读：**已实现（以另一种方式表达）** —— 护盾只在 `before_taking_damage` 且**物理**时消耗（`ShieldGuard.TryBlockShield`：
#          `axis != "physical"` 直接返回 false；流血是 `round_end` 固定伤害、位移/自伤根本不走该钩子）
#          ⇒ 这三个"不消耗"是**结构上自然成立**的，无需按名单判断 ✓
#    归属：策划（若将来加"会消耗护盾的新效果" ⇒ 需明确是否列入本名单）· 建议保留为文档并**加一句"当前由实现结构保证"**
#
# 2) shield.aoe_consumes = 1
#    判读：**已实现** —— `IBuffLedger.ConsumeCharge` 注释写明「AOE 算 1 次攻击（combat_math §8.1）」，
#          且 `ShieldGuard._attackBlockedThisAction` 保证**一次动作整攻击挡掉** ✓
#    归属：策划（数值 1 已由实现固定；若要"每目标各消耗一次"就是**机制改动**，属架构判定）
#
# 3) guard_attach.max_per_turn = 1
#    判读：🔴 **【已实现，但数据没被读】= 红线 21 形态** —— `ShieldGuard` 里**硬编码** `_redirectsThisTurn >= 1`；
#          而同一个 1 **同时**写在 `tuning.guard_redirect.max_per_turn` ✓（**两处 data、一处代码硬编码 = 三处真相**）⚠️
#    归属：**主程序执行 + 策划判值** ⇒ 我建议：**接线**（读 `tuning.guard_redirect.max_per_turn`）或**删掉 buff def 里那份**
#
# 4) guard_attach.physical_only = true
#    判读：同上（`ShieldGuard` 硬编码 `axis != "physical"`；`tuning.guard_redirect.physical_only` 也有一份）⚠️
#    归属：同 3 ⇒ **接线或删重复**（三处真相是"改一处不生效"的温床）
#
# 5) guard_attach.protectors_def_after_redirect = true
#    判读：**已实现** —— `ShieldGuard` 类文档原文：「重定向后按保护者防御（**由管线以保护者为受害重算**）」，
#          且 `DamagePipeline:119-122` 确实把 `protector` 取出来当受害重算 ✓
#    归属：架构/策划（文档性 ✓ 保留，建议注明"由管线重算受害实现"）
#
# 6) guard_attach.recalc_range_at_resolution = true
#    判读：**已实现（由构造保证）** —— 邻接判定 `s == victimSlot ± 1` 是**在结算时**用当前站位算的 ✓
#    归属：同上（文档性 ✓）
#
# 7) guard_attach.weak_protector_triggers_deaths_door = true
#    判读：**疑似已实现（由通用路径覆盖）** —— 重定向后保护者成为受害 ⇒ 死门/虚弱判定走**通用受害路径** ✓
#          ⚠️ 但我**没有**找到专门处理"虚弱守护者"的分支 ⇒ 标**待核**（请你或架构抽检一次）✓
#    归属：架构（语义确认）· 我不擅自改
#
# 8) guard_attach.guarded_ally_morale = 3
#    判读：🔴 **未找到消费点** —— 内核里没有读它；被守护者士气 **可能**经 `morale_events`（如 `physical_hit_*`）表达，
#          但**未证实**。⇒ 二选一：**接线**（重定向发生时给被守护者 −3 士气并留痕）或 **删**（若已由 morale_events 覆盖）
#    归属：**策划定语义 + 我执行** ⇒ 请裁：这条规则**现在生效吗**？
#
# ══ B. `*_note` / `*_rule` 文档键（10 条 + 1）—— 全部判为【登记未消费·文档价值】══
#   tuning.json        : battle_goal_note（#273 打赢 ≥3 场）· difficulty_note · loot_note（#276 柴火优先）
#   economy.json       : stagecoach_note（#283 免费招募）· stress_relief_note（#283 7.3 同价同效）
#   expedition_map.json: move_note · scout_note
#   heirlooms.json     : drop_note · kind_note
#   sanitarium.json    : service_note · trait_rule（规则语义）
#   判读：**不会改变游戏行为**（未解析、纯说明），但**有决策价值**（都带决策号 `#2xx`，是"为什么这么定"的存档）✓
#   归属：**策划**（这些键是策划写给自己/后来者的）⇒ 工具已按**文档键约定**放行（`*_note`/`*_rule`）✓
#
# ══ 汇总 ══
#   已实现(文档性，保留)：5 条（not_consumed_by · aoe_consumes · protectors_def_after_redirect ·
#                          recalc_range_at_resolution · 以及 11 条文档键）
#   🔴 **红线 21 形态（数据没被读）**：2 条（max_per_turn · physical_only）⇒ 接线或删重复
#   🔴 **未找到消费点，请裁语义**：2 条（guarded_ally_morale · weak_protector_triggers_deaths_door ⇒ 后者偏"已实现待确认"）
#   ⇒ 我**没有**擅自接线任何一条（缺语义裁定，且"未接线不入白名单"的反面是"不许假接"）✓
