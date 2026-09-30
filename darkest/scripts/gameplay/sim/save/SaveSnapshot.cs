using System.Collections.Generic;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Save;

/// <summary>
/// 🔴 **存档快照族**（`Phase 1` · 架构方案 §6 Phase 1-A）—— **跨趟元层状态**的纯数据镜像。
///
/// <para>**为什么是「独立快照」而不是直序持有者**：持有者（`Roster`/`HeirloomStock`/`Economy`）
/// 都持有**配置引用**（`_cfg` 等），直序会把整份配置数据卷进存档 ⇒ 存档体积膨胀、
/// 且配置一改老档就读不通。快照只存**跨趟可变的部分**，配置在恢复时由组合根重新注入 ✓</para>
///
/// <para>**边界**：本族是**纯数据**（零 Godot、零行为），序列化与迁移分别见
/// `SaveSerializer` / `SaveMigrator` ✓</para>
///
/// <para>🔴 **不存战斗中途状态**（方案 O-1）：本版只做「跨趟元层」——
/// `ExpeditionSession` 的战斗中态**不入档**（复杂度高、收益低）⇒ 中途退出按"这一趟没打完"处理 ✓</para>
/// </summary>

/// <summary>一趟出征结束后的**跨趟进度**（`RunProgress` 的可变部分）。</summary>
public sealed record ProgressSnapshot(int RunsFinished, int BattlesWon);

/// <summary>**传家宝与建筑等级**（`HeirloomStock` 的可变部分）：存量 + 各建筑当前等级。</summary>
public sealed record HeirloomSnapshot(
    Dictionary<string, int> Counts,
    Dictionary<string, int> Levels);

/// <summary>**经济**（`Economy` 的可变部分）：金币与累计发放额。</summary>
public sealed record EconomySnapshot(int Gold, int AwardedTotal);

/// <summary>**墓园**的一条阵亡留档（与 `Roster.Graveyard` 的元素同构）。</summary>
public sealed record GraveyardSnapshot(string HeroId, string Name, int Level, string Cause);

/// <summary>
/// **名册快照**（`Roster` 的可变部分）。
/// <para>🔴 `Heroes` 存**完整 `HeroConfig`**（含招募来的新兵 —— 新兵**不在** `roster.json` 里，
/// 不存就找不回来）；`HeroConfig` 全由原始字段构成 ⇒ 可直接序列化 ✓</para>
/// <para>🔴 `Traits` 存**特质实例**而非 id：`HeroTraitConfig` 是四个原始字段的纯 record，
/// 且特质是"**每人一份的实例**"（策划 `#283`）⇒ 存实例才是忠实语义，也省掉一套 id 反查 ✓</para>
/// </summary>
public sealed record RosterSnapshot(
    IReadOnlyList<HeroConfig> Heroes,
    Dictionary<string, int> Morale,
    Dictionary<string, int> Xp,
    Dictionary<string, IReadOnlyList<string>> Diseases,
    Dictionary<string, IReadOnlyList<HeroTraitConfig>> Traits,
    IReadOnlyList<string> LockedTraits,
    IReadOnlyList<GraveyardSnapshot> Graveyard,
    int RecruitSeq,
    int CurrentCap,
    Dictionary<string, int> PendingOpeningPenalty);

/// <summary>
/// 🔴 **顶层存档快照**（一个存档槽位的全部内容）。
/// <para>`Version` 由 `SaveMigrator` 消费 —— 🔴 **版本不符绝不删档**（那是公认反模式）：
/// 迁移失败时**保留原档并回报**，交由玩家决定 ✓</para>
/// </summary>
public sealed record SaveSnapshot(
    int Version,
    RosterSnapshot Roster,
    ProgressSnapshot Progress,
    HeirloomSnapshot Heirlooms,
    EconomySnapshot Economy);

/// <remarks>
/// 🔴 **不提供 `Empty` 哨兵**（2026-09-27 删除）：第一版曾写过一个 `SaveSnapshot.Empty(version)`，
/// 但**只有测试调、生产无人调** ⇒ 按本项目纪律「只被测试调用也算死函数」**判为缺陷并删除** ✓
/// "**还没有存档**"这个状态由 `SaveFileGateway.ReadText` 返回 **`null`** 表达
/// （`SaveController.Load` 据此走"新建一局"分支），**不需要**再造一个空快照对象 ✓
/// </remarks>
