/// <summary>#211/O-60 支援点 SP（战斗级资源，全队共享）：起手 3 / 每回合 +1 / 上限 4；
/// 支援位技能 −1、增援 −2；**战斗位技能与被动、撤退零消耗**。纯计数、零随机。</summary>
// 🔴 **从 `TuningConfig.cs` 拆出**（用户 2026-09-18 红线：程序文件 ≤600 行）——
//    本文件 = **战斗机制 + 远征/光照/拾取/侦察/背包/难度/资源/扎营/食物/浮动/崩溃** 的参数记录族
//    ⇒ **纯文件搬家**（这些是**顶层 record**，不是嵌套类型 ⇒ **不需要 `partial`** ✓）· **零行为改动** ✓
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

public sealed record TuningSupportPoints(
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("regen_per_round")] int RegenPerRound,
    [property: JsonPropertyName("cap")] int Cap,
    [property: JsonPropertyName("cost_skill")] int CostSkill,
    [property: JsonPropertyName("cost_reinforce")] int CostReinforce);

/// <summary>#198 弹性增援（橡胶筋）：窗口 K 回合内「未使用 output 技能的存活战斗位 ≥ idle_output_slots」→ M+1；
/// 达标 → M 回落 M_base；浮区 M ∈ [M_base, M_base+max_bonus]（首波固定 trigger_round 不受弹性影响）。</summary>
public sealed record TuningElasticSpec(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("k_rounds")] int KRounds,
    [property: JsonPropertyName("idle_output_slots")] int IdleOutputSlots,
    [property: JsonPropertyName("max_bonus")] int MaxBonus);

/// <summary>敌方每回合行动次数（默认 1；M6 探针验证行动不对称调节，数据可表达，不做代码拍死）。</summary>
public sealed record EnemyActionsPerTurn(int Value);

/// <summary>流血（combat_math §4）：每回合 3 / 2 回合；须与 buff_defs 一致（P6）。</summary>
public sealed record TuningBleed(
    [property: JsonPropertyName("per_round_damage")] int PerRoundDamage,
    [property: JsonPropertyName("rounds")] int Rounds);

/// <summary>属性减益通用默认 −3 / 2 回合（combat_math §4/§10；O-23 与技能韧性减益并存）。</summary>
public sealed record TuningStatDebuffDefault(
    [property: JsonPropertyName("delta")] int Delta,
    [property: JsonPropertyName("rounds")] int Rounds);

/// <summary>眩晕（GDD §2.5）：跳过 1 次行动即结束。</summary>
public sealed record TuningStun(
    [property: JsonPropertyName("effect")] string Effect,
    // 🔴 数字外置（P29）：成功施加眩晕后的**抗性累积 +N**（原先硬写在 `EffectsStep` 里是 `+ 50`）✓
    [property: JsonPropertyName("buildup_on_apply")] int BuildupOnApply);

/// <summary>
/// 撤退公式（O-11/#169 已拍板；M5 撤退按钮用）：
/// 基础 = 50% + (我方存活平均实际速度 − 敌方存活平均实际速度) × 4%，钳制 [15,85]；
/// 实际 = 基础 + uniform(−random_range, +random_range)，钳制 [rand_clamp_min, rand_clamp_max]。
/// </summary>
public sealed record TuningRetreatFormula(
    [property: JsonPropertyName("base_percent")] int BasePercent,
    [property: JsonPropertyName("per_speed_diff_percent")] int PerSpeedDiffPercent,
    [property: JsonPropertyName("clamp_min")] int ClampMin,
    [property: JsonPropertyName("clamp_max")] int ClampMax,
    [property: JsonPropertyName("random_range")] int RandomRange,
    [property: JsonPropertyName("rand_clamp_min")] int RandClampMin,
    [property: JsonPropertyName("rand_clamp_max")] int RandClampMax,
    [property: JsonPropertyName("source")] string Source);

// ---------------------------------------------------------------------------
// M7 远征层（#240 / O-66）：expedition / resources / camp（P20 数据一致性）
// ---------------------------------------------------------------------------

/// <summary>远征层参数（E0/E6）：N 场战斗、夜袭概率、撤退两档惩罚、**难度递进三档（#250）**。</summary>
public sealed record TuningExpedition(
    [property: JsonPropertyName("n_battles")] int NBattles,
    [property: JsonPropertyName("ambush_chance")] double AmbushChance,
    [property: JsonPropertyName("retreat_penalty")] TuningRetreatPenalty RetreatPenalty,
    // 🔴 数字外置：**必需参数放在可选参数之前**（C# 规则：可选参数必须都在最后）
    //    并由 `DataPresence.RequireKeys` 断言该键**存在于数据**（缺键即报错）✓
    [property: JsonPropertyName("battle_goal")] int BattleGoal,
    [property: JsonPropertyName("difficulty_tiers")] IReadOnlyList<TuningDifficultyTier>? DifficultyTiers = null,
    [property: JsonPropertyName("pass_morale_delta")] int PassMoraleDelta = 0);

/// <summary>
/// 难度递进档（#250）：按**场序**施加的乘数（**远征层**，不得写进 `units.json` 的单场基准值）。
/// 🔴 `Target` 为 null = **施加对象待 O-70 ① 裁定**（敌 HP / 敌攻击 / 两者）——**实现方不得自行选定**。
/// </summary>
/// <summary>M7.5 地牢层（#258 / D0）：光照计 + 侦察 + 背包。**效果表 7 项，禁止任何 HP 字段**（P21 ③）。</summary>
public sealed record TuningLight(
    [property: JsonPropertyName("enter_value")] int EnterValue,
    [property: JsonPropertyName("node_step")] int NodeStep,
    [property: JsonPropertyName("brighten_gain")] int BrightenGain,
    [property: JsonPropertyName("brighten_firewood_cost")] int BrightenFirewoodCost,
    [property: JsonPropertyName("camp_restore_to")] int CampRestoreTo,
    [property: JsonPropertyName("event_torch_gain")] int EventTorchGain,
    [property: JsonPropertyName("event_dark_cost")] int EventDarkCost,
    [property: JsonPropertyName("tiers")] IReadOnlyList<TuningLightTier> Tiers,
    [property: JsonPropertyName("loot")] Dictionary<string, TuningLootSpec> Loot,
    [property: JsonPropertyName("effects")] Dictionary<string, TuningLightEffect> Effects);

/// <summary>掉落规格（#276）：**类型 + 份数**（柴火优先 —— 口粮换不来扎营，故只给口粮时补给再多也换不来续航）。</summary>
public sealed record TuningLootSpec(
    [property: JsonPropertyName("firewood")] int Firewood = 0,
    [property: JsonPropertyName("food")] int Food = 0);

public sealed record TuningLightTier(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("min")] int Min,
    [property: JsonPropertyName("max")] int Max);

/// <summary>光照效果（7 项；**无 HP 字段** = #255 已否决的杠杆不得回流）。</summary>
public sealed record TuningLightEffect(
    [property: JsonPropertyName("our_morale_damage_pct")] double OurMoraleDamagePct,
    [property: JsonPropertyName("enemy_acc")] double EnemyAcc,
    [property: JsonPropertyName("enemy_dmg_pct")] double EnemyDmgPct,
    [property: JsonPropertyName("enemy_crit_pct")] double EnemyCritPct,
    [property: JsonPropertyName("our_ambush_pct")] double OurAmbushPct,
    [property: JsonPropertyName("our_crit_pct")] double OurCritPct,
    [property: JsonPropertyName("scouting_pct")] double ScoutingPct);

/// <summary>侦察（D1）：基础命中率 + 光照加成；**只揭示下一节点类型**。</summary>
public sealed record TuningScouting(
    [property: JsonPropertyName("base_pct")] double BasePct,
    [property: JsonPropertyName("reveal")] string Reveal);

/// <summary>背包（D2）：12 格；口粮不堆叠 ⇒ 默认推荐配置恰满 12；**包满禁止静默丢弃**。</summary>
public sealed record TuningInventory(
    [property: JsonPropertyName("slot_cap")] int SlotCap,
    [property: JsonPropertyName("food_stackable")] bool FoodStackable,
    [property: JsonPropertyName("recommended_loadout")] Dictionary<string, int>? RecommendedLoadout = null,
    [property: JsonPropertyName("full_policy")] string? FullPolicy = null);

public sealed record TuningDifficultyTier(
    [property: JsonPropertyName("battle_from")] int BattleFrom,
    [property: JsonPropertyName("battle_to")] int BattleTo,
    [property: JsonPropertyName("multiplier")] double Multiplier,
    [property: JsonPropertyName("target")] string? Target = null,
    [property: JsonPropertyName("stun_resist_pp")] int StunResistPp = 0);

/// <summary>撤退士气惩罚两档（只作用于存活者）。</summary>
public sealed record TuningRetreatPenalty(
    [property: JsonPropertyName("no_death")] int NoDeath,
    [property: JsonPropertyName("with_death")] int WithDeath);

/// <summary>
/// D-2 回头威胁一档：光照 ≤ <see cref="MaxLight"/> 时，回头触发威胁的概率 = <see cref="Percent"/>（%）。
/// <para>🔴 档位数组**必须按暗 → 亮排列**（`max_light` 严格递增 = 从最暗档开始），首个命中的档即生效 ——
/// 这是"越黑越容易出事"的**结构保证**，由加载期校验强制，不靠人工排序。</para>
/// </summary>
public sealed record RevisitThreatTier(
    [property: JsonPropertyName("max_light")] int MaxLight,
    [property: JsonPropertyName("percent")] double Percent);

/// <summary>
/// D-2 回头威胁（DD 口径：反复走同一格 ⇒ 威胁刷新，且**越黑越频繁**）。
/// <para>未配置 ⇒ 完全不掷骰、不写日志（等价于"该地牢不刷回头怪"）。</para>
/// </summary>
public sealed record RevisitThreatConfig(
    [property: JsonPropertyName("tiers")] IReadOnlyList<RevisitThreatTier> Tiers,
    // 🔴 触发后的威胁种类权重：battle vs trap（DD 原版两者都有；权重比 = 相对频率）
    [property: JsonPropertyName("battle_weight")] int BattleWeight = 1,
    [property: JsonPropertyName("trap_weight")] int TrapWeight = 1,
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>地牢层（D-1 / D-2）：网格行走的**回头代价**与**回头威胁**。</summary>
public sealed record TuningDungeonLayer(
    // 🔴 D-1：重走一格的**额外**光照代价（正数 = 要扣多少；`null`/0 ⇒ 关闭回头代价）——
    //    DD 口径：回头**仍要付**，只是比走新格便宜（与旧"按段"层的 revisit 同向，各层自洽）✓
    [property: JsonPropertyName("revisit_light_cost")] int? RevisitLightCost = null,
    [property: JsonPropertyName("revisit")] RevisitThreatConfig? Revisit = null,
    // 🆕 D-5：**饥饿**（途中"队伍饿了"检查）—— `null` ⇒ 完全不启用（既有调用点行为逐字不变）✓
    [property: JsonPropertyName("hunger")] HungerConfig? Hunger = null,
    // 🆕 D-3：**格视野**（进格揭示 + 半径以**格**为单位）—— `null` ⇒ 完全不启用（= 旧二态行为）✓
    [property: JsonPropertyName("vision")] TuningGridVision? Vision = null,

    // 🆕 D-4：**陷阱撒布**（走廊段上按概率撒 `^` 格）—— `null` ⇒ 一格都不撒（既有调用点行为逐字不变）✓
    [property: JsonPropertyName("traps")] TuningTrapScatter? Traps = null,

    // 🆕 D-6：**隐藏房**（地图上不显示、靠侦察揭示 ⇒ 给侦察一个**非信息类**回报）——
    //   `null` ⇒ 一格都不撒、一次都不给（既有调用点行为逐字不变）✓
    [property: JsonPropertyName("secrets")] TuningSecretScatter? Secrets = null,

    // 🆕 D-7：**探索层 act-out**（带折磨的英雄拒绝摸奇物 / 拒绝进食）——
    //   `null` ⇒ 一次都不判、一次都不掷（既有调用点行为逐字不变）✓
    [property: JsonPropertyName("exploration")] TuningExplorationActOut? Exploration = null);

/// <summary>
/// 🔴🆕 `D-7`（2026-09-20）**探索层 act-out** —— 带**折磨**的英雄在**战斗之外**的行为表现。
///
/// <para>🔴 **为什么需要它**：折磨此前**只**在战斗内有效（`AfflictionProcs` 的 `refuse_skill` /
/// `refuse_heal` / `randomize_attack_target`）⇒ 玩家一趟远征里"最惨的时刻"全发生在战斗里，
/// **走廊上、奇物前、饭点**完全感受不到折磨 ⇒ 折磨的**一半表现力**是缺失的（DD ⑩）✓</para>
///
/// <para>本刀落两条（`dungeon_layer_design.md §F5`）：</para>
/// <para>· **拒绝摸奇物**（`curio.md §1.2 ⑤`）：DD 原文 = 带折磨者会「**自动空手碰**」某些 Curio
///   （**绝不用道具**）⇒ 本刀落成「**拒绝用道具、被迫空手**」（空手掷骰照常走，代价照常承担）✓</para>
/// <para>· **拒绝进食**（`§F5 ③`）：掷中 ⇒ **强制挨饿**（推翻玩家选的"吃"）✓</para>
///
/// <para>🔴🔴 **折磨判据 = 士气 &lt; <see cref="MoraleAfflictionThreshold"/>** —— 该阈值**不是近似，
/// 而是模型自身的边界**（用户 2026-09-20 裁定）：</para>
/// <para>· 折磨**只**在士气 == 0 挂上（`MoraleLedger.GrantAffliction`），且**只有**士气回到
///   `morale.start`（= <see cref="MoraleAfflictionThreshold"/> = 50）才解除（`MoraleLedger` 第 127~134 行）；</para>
/// <para>· 美德走士气 == 100（`HandleMoraleMax`）且**立即把士气拉回 50** ⇒ 美德与折磨**永不并存** ✓</para>
/// <para>⇒ **「士气 &lt; 50」区间与「带折磨」在模型上等价**，且**天然跨趟**（士气本就是跨趟累积，`#245`/`#287`）
/// —— 无需新增任何状态载体 ✅（这也正是"为什么不给 `Retained` 加 affliction 字段"：那是第二份真值，`#325` D6）</para>
///
/// <para>🔴 **opt-in**：`exploration = null` ⇒ 一次都不判、一次都不掷、一句都不写
/// （零随机不留痕）⇒ 既有调用点行为**逐字不变** ✓</para>
/// </summary>
public sealed record TuningExplorationActOut(
    /// <summary>
    /// 🔴 **折磨阈值**：士气 **&lt;** 本值 ⇒ 判为"带折磨"。
    /// ⚠️ **必须等于 `tuning.morale.start`**（加载期校验强制）—— 因为那正是 `MoraleLedger` 解除折磨的
    /// **唯一**判据；两者不同值会出现「该解除还被判折磨」或「该判折磨却已解除」⚠️
    /// </summary>
    [property: JsonPropertyName("morale_affliction_threshold")] int MoraleAfflictionThreshold,

    /// <summary>
    /// 带折磨者**拒绝用道具**的概率（%；不写默认值 ⇒ 漏配即 0 ⇒ 加载期拒绝）。
    /// 🔴 **必须 ∈ (0,100]**：0 = "配了但永远不生效"（静默失效家族）⇒ 要关闭请**整段删掉** `exploration`✓
    /// </summary>
    [property: JsonPropertyName("curio_refuse_percent")] double CurioRefusePercent,

    /// <summary>同上的**拒绝进食**概率（%）；同上纪律 ✓</summary>
    [property: JsonPropertyName("eat_refuse_percent")] double EatRefusePercent,

    /// <summary>人类可读说明（**不参与行为**）✓</summary>
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>
/// 🔴🆕 `D-4`（2026-09-20）**陷阱撒布** —— 派生图上"哪里会有陷阱格"的**唯一**数值来源。
///
/// <para>🔴 **为什么是数值（`tuning`）而不是内容（`trap_defs.json`）**：
/// `trap_defs.json` 回答"**踩中会怎样**"（伤害 %、压力、拆除加成 —— 内容/规则）；
/// 本类回答"**图上有多少陷阱**"（撒布密度 —— 平衡数值）⇒ 两者**不同族**，各归其位 ✓</para>
///
/// <para>⚠️ **为什么需要它**：`DungeonTileKind.Trap` 此前**全仓无人产出**（派生器只挖房间与走廊）
/// ⇒ `TrapResolver` 再完备也永远触发不了。本类把"撒陷阱"变成**可配置的派生步骤** ✓</para>
///
/// <para>🔴 **opt-in**：`traps = null` ⇒ 一格不撒、一次不掷（零随机不留痕）⇒ 既有行为逐字不变 ✓</para>
/// </summary>
public sealed record TuningTrapScatter(
    /// <summary>每条**走廊段**的陷阱概率（%）；`≤0` ⇒ 关闭。🔴 数值 `placeholder`（`#307`）✓</summary>
    [property: JsonPropertyName("corridor_chance_percent")] double CorridorChancePercent,

    /// <summary>人类可读说明（**不参与行为**）✓</summary>
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>
/// 🔴🆕 `D-6`（2026-09-20）**隐藏房撒布 + 揭示回报** —— 派生图上"哪里会有 `*` 格"以及
/// "揭示它值多少"的**唯一**数值来源。
///
/// <para>🔴 **为什么是数值（`tuning`）而不是内容表**：本类回答"**图上有多少隐藏房 / 揭示它给多少**"
/// （撒布密度 + 回报量级 —— 平衡数值）；至于"隐藏房里**具体是哪件战利品**"归内容层
/// （`loot` / `curios.json`），**不在本刀范围** ⇒ 本刀只给**资源回报**（`gold`）✓</para>
///
/// <para>🔴🔴 **为什么必须有"非信息类回报"**：`D-3` 三态揭示给的是**信息**（看得见），
/// `D-4` 陷阱给的是**避免损失**。若隐藏房"揭示了但没东西"，侦察就永远是**净亏**（花光照换好看）
/// ⇒ 玩家会**理性地永不侦察** ⇒ 整条侦察链路沦为装饰 ⚠️
/// 故本类强制 `reward_*` > 0（加载期校验）：**揭示 = 拿到实打实的东西** ✓</para>
///
/// <para>🔴 **opt-in**：`secrets = null` ⇒ 一格不撒、一次不掷、一分不给（零随机不留痕）⇒ 既有行为逐字不变 ✓</para>
/// </summary>
public sealed record TuningSecretScatter(
    /// <summary>每个**走廊格**的隐藏房概率（%）；`≤0` ⇒ 关闭。🔴 数值 `placeholder`（`#307`）✓</summary>
    [property: JsonPropertyName("corridor_chance_percent")] double CorridorChancePercent,

    /// <summary>
    /// 揭示一处隐藏房的**金币**回报（DD 口径：隐藏房 = rewards 房）。
    /// 🔴 **必须 > 0**：0 等于"揭示了但没东西" ⇒ 侦察变成纯亏（加载期拒收，见上）⚠️
    /// </summary>
    [property: JsonPropertyName("reward_gold")] int RewardGold,

    /// <summary>
    /// 每趟**最多**揭示（并结算）几处隐藏房 —— **配额**语义（与"逐格概率"是两码事）。
    /// 🔴 `≤0` ⇒ 不设上限（给 0 合法：表示"不限"）—— 这是结构参数，允许默认 0（`#307` 白名单口径）✓
    /// </summary>
    [property: JsonPropertyName("max_rewards_per_run")] int MaxRewardsPerRun = 0,

    /// <summary>人类可读说明（**不参与行为**）✓</summary>
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>
/// 🔴🆕 `D-3` **格视野（`GridVision`）** —— DD 揭示三态的**格级**通道（"能看清什么"）。
///
/// <para>🔴 **为什么放在 `tuning` 而不是 `dungeon_grid.json`**：
/// `DungeonGridConfig.ResPath`（`res://data/dungeon_grid.json`）**尚不存在**（我们仍在**派生图**阶段，
/// 真关卡属阶段 4）⇒ 该文件里的 `vision` 字段目前**没有数据载体**。
/// 而 `D-1`（`revisit_light_cost`）/ `D-5`（`hunger`）都已落在 `tuning.dungeon_layer` ⇒
/// **同一族机制、同一位置、同一纪律**（opt-in：`null` ⇒ 行为逐字不变）✓</para>
///
/// <para>🔴 与 <see cref="Darkest.Gameplay.Sim.Run.DungeonGridVision"/> **字段一一对应**——
/// 后者是"关卡的视野契约"（将来自 `dungeon_grid.json` 来），本类是"全局默认值"（从 `tuning` 来）。
/// ⚠️ **不合并成一个类**：两者的**加载期校验不同**（关卡级要校验"半径 < 图边长"，全局级不知道图尺寸）⇒
/// 合并会逼出一堆 `if (是关卡级)`（就是"一份数据两套规则"的开端）✓</para>
/// </summary>
public sealed record TuningGridVision(
    /// <summary>进格即揭示（关 = 只有"走到的格"可见 ⇒ 旧二态行为）✓</summary>
    [property: JsonPropertyName("reveal_on_enter")] bool RevealOnEnter,

    /// <summary>视野半径（**格**；曼哈顿距离）—— 数值 `placeholder`（`#307`）✓</summary>
    [property: JsonPropertyName("radius")] int Radius,

    /// <summary>侦察临时 +N（**段级**通道的加成；`D-3` 阶段**只登记不消费** ⇒ 见下方说明）⚠️</summary>
    [property: JsonPropertyName("scout_bonus")] int ScoutBonus = 0);

/// <summary>
/// 🔴 `D-5` 饥饿一档：光照 ≤ <see cref="MaxLight"/> 时，走廊**前行格**触发饥饿检查的概率 = <see cref="Percent"/>（%）。
/// <para>结构与 <see cref="RevisitThreatTier"/> **刻意同构**（同为"越黑越频繁"）：数组**必须按暗 → 亮排列**
/// （`max_light` 严格递增），首个命中即最暗档 —— 由加载期校验强制，不靠人工排序 ✓</para>
/// <para>🔴 DD 原版刻度（wiki）：`Radiant/Dim 7.5%` · `Shadowy/Dark 10%` · `Black 12.5%`
/// ⇒ 本项目刻度对齐为 `≤0 → 12.5` / `≤50 → 10` / `≤75 → 7.5` / `≤100 → 7.5`。</para>
/// </summary>
public sealed record HungerTier(
    [property: JsonPropertyName("max_light")] int MaxLight,
    [property: JsonPropertyName("percent")] double Percent);

/// <summary>
/// 🔴🔴 `D-5` **饥饿**（DD wiki "Hunger"）：走廊格上的**隐藏事件** —— 触发时队伍"饿了"，
/// **必须二选一**（不能拖、不能跳）：**吃**（每名存活成员 1 口粮，全队回 HP）/ **不吃**
/// （全队掉 HP + 涨压力）。**不能只喂一部分人**：口粮不够 ⇒ **只能挨饿**（且**一口粮都不消耗**）。
///
/// <para>**缓冲（Hunger Buffer）**：开场 / 扎营后给 <see cref="BufferAtStart"/> 条走廊；
/// 每次触发后再给 <see cref="BufferAfterTrigger"/> 条。🔴 **只在前行时递减** —— 回头/退回原房间不算
/// （DD 原文："moving backwards (left) and re-entering the starting room does not reduce the hunger buffer"）✓</para>
///
/// <para>数值全部 `placeholder`（`#307`：我不动任何数值）⇒ 由策划定实。</para>
/// </summary>
public sealed record HungerConfig(
    [property: JsonPropertyName("tiers")] IReadOnlyList<HungerTier> Tiers,
    // 🔴 吃：每名**存活**成员消耗多少口粮（DD = 1/人）
    // 🔴🔴 `#307` / 三扫纪律：**不写默认值** —— 默认参数 = 静默默认（数据漏配会被悄悄补成 DD 值，
    //    表现为"我改了 data 却没生效"的反向陷阱）⇒ 余额数字**必须**在 `tuning.json` 里显式给，
    //    漏配就直接反序列化成 0 ⇒ 被 `TuningConfig.Validate` 当场拒绝（fail-fast，不给静默兜底）✓
    [property: JsonPropertyName("food_per_hero")] int FoodPerHero,
    // 🔴 吃：全队回多少 **% 最大 HP**（DD = 5）
    [property: JsonPropertyName("eat_heal_percent")] double EatHealPercent,
    // 🔴 不吃：全队掉多少 **% 最大 HP**（DD = 20）
    [property: JsonPropertyName("starve_hp_percent")] double StarveHpPercent,
    // 🔴 不吃：全队涨多少**压力**（DD = 20）
    // ⚠️ **`starve_morale` 是唯一例外**：0 = "挨饿不加压力"是**合法**配置 ⇒ 漏配与显式 0
    //    在 JSON 层不可区分 ⇒ 它**无法**用"反序列化成 0 就拒绝"兜底（有意的取舍：
    //    宁可允许漏配，也不禁止一个语义正当的 0）⇒ 其余四个字段已 fail-fast，本条靠 `note` 契约 ✓
    [property: JsonPropertyName("starve_morale")] int StarveMorale,
    // 🔴 缓冲条数：**结构参数**（0 = 不缓冲，是正当配置）⇒ 允许默认 0（`#307` 的白名单口径）✓
    [property: JsonPropertyName("buffer_at_start")] int BufferAtStart = 0,
    [property: JsonPropertyName("buffer_after_trigger")] int BufferAfterTrigger = 0,
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>一趟远征的起手资源（E2）：柴火 = 扎营许可；口粮 = 吃饭。</summary>
public sealed record TuningResources(
    [property: JsonPropertyName("firewood")] int Firewood,
    [property: JsonPropertyName("food")] int Food);

/// <summary>扎营参数（E3）：四档食物 / Respite 基准 / 打气持续场数 / 夜袭概率。</summary>
public sealed record TuningCamp(
    [property: JsonPropertyName("food_tiers")] TuningFoodTiers FoodTiers,
    // 🔴 数字外置（P29）：四档食物的**效果**（原先硬写在 `ExpeditionCampMath.FoodEffect`）✓
    [property: JsonPropertyName("food_effects")] TuningFoodEffects FoodEffects,
    [property: JsonPropertyName("respite_base")] int RespiteBase,
    [property: JsonPropertyName("pep_talk_battles")] int PepTalkBattles,
    [property: JsonPropertyName("ambush_chance")] double AmbushChance);

public sealed record TuningFoodTiers(
    [property: JsonPropertyName("starve")] int Starve,
    [property: JsonPropertyName("half")] int Half,
    [property: JsonPropertyName("full")] int Full,
    [property: JsonPropertyName("feast")] int Feast);

/// <summary>
/// 🔴 单个食物档位的**效果**（数字外置，P29）：HP 百分比增量（可负）与士气增量。
/// 原先这四档效果**硬写在 `ExpeditionCampMath.FoodEffect` 的 switch 里** ⇒ 策划改不了 ⚠️
/// ⇒ 现搬到 `tuning.camp.food_effects`（**值不变 = 零数值改动**）✓
/// </summary>
public sealed record TuningFoodEffect(
    [property: JsonPropertyName("hp_percent")] double HpPercent,
    [property: JsonPropertyName("morale")] int Morale);

/// <summary>四档食物效果（starve / half / full / feast）—— 与 `food_tiers`（成本）配套 ✓</summary>
public sealed record TuningFoodEffects(
    [property: JsonPropertyName("starve")] TuningFoodEffect Starve,
    [property: JsonPropertyName("half")] TuningFoodEffect Half,
    [property: JsonPropertyName("full")] TuningFoodEffect Full,
    [property: JsonPropertyName("feast")] TuningFoodEffect Feast);

/// <summary>
/// 🔴 连续未命中补偿（数字外置，P29）：每多未命中一次，**隐藏**命中加成 +N。
/// 原先这个 4 **硬写在 `HitStep` 里**（`Math.Max(0, misses - 1) * 4`）⇒ 策划改不了 ⚠️
/// </summary>
public sealed record TuningConsecutiveMiss(
    [property: JsonPropertyName("hit_bonus_per_miss")] int HitBonusPerMiss);

/// <summary>
/// 🔴 暴击治疗概率（数字外置，P29；D7 / `#209`）：单体 12% ／ 多目标 5%（不受任何修正影响）。
/// 原先这两个数**硬写在 `SkillExecutor` 里**（`targets.Length > 1 ? 5.0 : 12.0`）⇒ 策划改不了 ⚠️
/// （暴击治疗后的士气加成**本来就在** `morale_events["critical_heal"].Delta` ✓，无需重复外置）
/// </summary>
public sealed record TuningHealCrit(
    [property: JsonPropertyName("single_target_percent")] int SingleTargetPercent,
    [property: JsonPropertyName("multi_target_percent")] int MultiTargetPercent);

/// <summary>命中率钳制 [55,100]（combat_math §1）。</summary>
public sealed record TuningHitClamp(
    [property: JsonPropertyName("min")] int Min,
    [property: JsonPropertyName("max")] int Max);

/// <summary>伤害浮动（combat_math §2.1；O-01 默认关闭 = 1.0）。</summary>
public sealed record TuningDamageFloat(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("min")] double Min,
    [property: JsonPropertyName("max")] double Max);

/// <summary>速度浮动 0~10%（#163，每回合重掷）。</summary>
public sealed record TuningSpeedFloat(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("percent")] int Percent);

/// <summary>崩溃判定池（morale §4~§6；F2/#193：美德池 4 个全量，振奋 +3/回合为 O-27 本包定值）。</summary>
public sealed record TuningCollapse(
    [property: JsonPropertyName("affliction_pool")] IReadOnlyList<string> AfflictionPool,
    [property: JsonPropertyName("virtue_pool")] IReadOnlyList<string> VirtuePool,
    [property: JsonPropertyName("proc")] string Proc,
    // 🔴 数字外置：同上（去默认 + 存在性断言）✓
    [property: JsonPropertyName("virtue_inspired_morale_per_turn")] int VirtueInspiredMoralePerTurn);
