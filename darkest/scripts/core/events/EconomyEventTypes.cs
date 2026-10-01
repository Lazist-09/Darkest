namespace Darkest.Core.Events;

/// <summary>
/// M8.0 ②（`#283` 7.1）：**金钱变更事件**（跨趟经济的最小可审计单元）。
/// 🔴 纪律：UI / 报告要显示的金钱数字**必须能从事件流算出**（`Total` 是冗余但可对账，同 `TurnSkippedEvent.MoraleDelta` 的处理）。
/// </summary>
public sealed record GoldChangedEvent(int Delta, string Reason, int Total) : BattleEvent;

/// <summary>
/// M8.0（`#287` = **`#245` 的落地**）：**英雄士气变更事件** —— 士气是**跨趟状态**（存于名册），
/// 每次变更必写事件（数字必须来自事件流；`Total` 冗余但可对账）。
/// </summary>
public sealed record HeroMoraleChangedEvent(string HeroId, int Delta, int Total, string Reason) : BattleEvent;

/// <summary>
/// M8.0 ⑤（`#283` 硬要求③）：**招募事件** —— 招募**免费**（`Cost` 恒 0）、新兵 `level == 1`、`morale == 50`。
/// 事件留下新兵的等级与士气，便于验收"**补的人不比老的强**"。
/// </summary>
public sealed record HeroRecruitedEvent(string HeroId, string Name, string Archetype, int Level, int Morale, int Cost) : BattleEvent;

/// <summary>
/// M8.1（`m8_roadmap §1`）：**传家宝变更事件** —— 传家宝是**第三种资源**（跨趟、与金钱同源，从光照档掉落）。
/// 🔴 变更必写事件（数字必须来自事件流）。
/// </summary>
public sealed record HeirloomChangedEvent(string Kind, int Delta, int Total, string Reason) : BattleEvent;

/// <summary>M8.1：**建筑升级事件** —— 记录升到几级、花掉哪些传家宝（供"升级真的改变数字"可审计）。</summary>
public sealed record BuildingUpgradedEvent(string Building, int Level, string Cost) : BattleEvent;

/// <summary>M8.2：**英雄患病事件**（每趟结束按概率获得 ⇒ 长线损耗）。</summary>
public sealed record HeroDiseasedEvent(string HeroId, string DiseaseId, string Reason) : BattleEvent;

/// <summary>M8.2：**治愈事件**（Sanitarium 治病：消耗金钱 + 传家宝）。</summary>
public sealed record HeroCuredEvent(string HeroId, string DiseaseId, int GoldCost, string HeirloomCost) : BattleEvent;

/// <summary>🆕 **M5u（2026-10-01）· 获得怪癖事件**（`quirks.json` 的 170 条 · M7③「高级新兵带 Quirk」）——
/// 怪癖是**跨趟状态**（存于名册、入档）⇒ 变更必留痕（数字/事实必须来自事件流）✓</summary>
public sealed record HeroQuirkGainedEvent(string HeroId, string QuirkId, string Reason) : BattleEvent;

/// <summary>🆕 **M4u（2026-10-01）· 装上一件饰品**（契约 `doc/modules/trinkets.md` T3/T4）——
/// 饰品是**跨趟状态**（存于名册、入档）⇒ 变更必留痕；`Slot` 从 **1** 起（= 第几个饰品位）✓</summary>
public sealed record HeroTrinketEquippedEvent(string HeroId, string TrinketId, int Slot, string Reason) : BattleEvent;

/// <summary>🆕 **M4u · 卸下一件饰品**（`Slot` = **卸下前**所在槽，从 1 起）✓</summary>
public sealed record HeroTrinketUnequippedEvent(string HeroId, string TrinketId, int Slot, string Reason) : BattleEvent;

/// <summary>M8.2 / V15：**负面特质被清除**（Sanitarium：消耗金钱 + 传家宝）。</summary>
public sealed record TraitRemovedEvent(string HeroId, string TraitId, int GoldCost, string HeirloomCost) : BattleEvent;

/// <summary>M8.2 / V15：**正面特质被固化**（Sanitarium：消耗金钱 + 传家宝；固化后不可再被清除）。</summary>
public sealed record TraitLockedEvent(string HeroId, string TraitId, int GoldCost, string HeirloomCost) : BattleEvent;





public sealed record StressReliefEvent(
    string Building,
    string Hero,
    int Cost,
    int MoraleRestored,
    double PenaltyRoll,
    bool PenaltyTriggered,
    int NextRunPenalty) : BattleEvent;

/// <summary>🆕 **M7③（策划 `#423`）**：马车"今日新兵"的**每一次高级判定掷骰**（确定性 & 可审计 ✓）。
/// `SlotIndex` 从 0 起；`DrawPercent` / `ThresholdPct` 单位都是**百分比**（与 `upgraded_recruit_chances_pct` 同）✓</summary>
public sealed record StagecoachRecruitRolledEvent(
    int SlotIndex,
    int StagecoachLevel,
    double DrawPercent,
    double ThresholdPct,
    bool Upgraded) : BattleEvent;
