using System;
using System.Collections.Generic;

namespace Darkest.Gameplay.Sim.Save;

/// <summary>🔴 一次迁移的结果（**失败也带快照** ⇒ 调用方能"保留原档"，绝不静默丢弃）✓</summary>
public sealed record MigrationResult(bool Ok, SaveSnapshot? Snapshot, string Message)
{
    /// <summary>迁移成功（或无需迁移）。</summary>
    public static MigrationResult Success(SaveSnapshot snapshot, string message)
        => new(true, snapshot, message);

    /// <summary>🔴 迁移失败：**快照原样带回**，调用方据此**保留原档**并回报玩家 ✓</summary>
    public static MigrationResult Fail(SaveSnapshot snapshot, string message)
        => new(false, snapshot, message);
}

/// <summary>
/// 🔴 **存档版本迁移**（`Phase 1`）—— 把旧版本存档升级到当前版本。
///
/// <para>🔴🔴 **核心纪律：版本不符 ⇒ 绝不删档** ——
/// "版本不符即删档"是**公认反模式**（玩家辛苦打的长线进度被一行代码抹掉）⇒
/// 本类的失败路径**把原档原样带回**，由调用方**保留文件 + 回报玩家**，交由玩家决定 ✓</para>
///
/// <para>**当前版本 = 4**（2026-10-02）：v1 = 第一版；**v2 = `H-1` 装备阶入档**（2026-09-30）；
/// **v3 = `roster.quirks` 怪癖入档**（M5u · 2026-10-01）；**v4 = `roster.trinkets` 饰品入档**（M4u）✓
/// —— 迁移链**逐级**走（v1 ⇒ v2 ⇒ v3 ⇒ v4）✓</para>
/// </summary>
public static class SaveMigrator
{
    /// <summary>🔴 当前存档格式版本。**新增字段/改结构时 +1，并在 `Migrate` 追加一级迁移** ✓</summary>
    public const int CurrentVersion = 4;

    /// <summary>
    /// 🔴 **`gear`（装备阶）自这一版起是【必备件】** —— v1 档里**结构上就没有**这个字段
    /// （那一版装备阶还没入档）⇒ 由下面的 v1⇒v2 迁移**补空表**；
    /// `SaveSerializer.Validate` 按本常量判「缺 = 损坏」（两处引用**同一个常量**，不各写一个字面量）✓
    /// </summary>
    public const int GearFieldSinceVersion = 2;

    /// <summary>
    /// 🔴 **`roster.quirks`（怪癖）自这一版起是【必备件】** —— v2 及更早的档里**结构上就没有**它
    /// （M5u 之前怪癖根本没入档、也没有任何怪癖入口）⇒ 由 v2⇒v3 迁移**补空表**；
    /// `SaveSerializer.Validate` 按本常量判「缺 = 损坏」（两处引用**同一个常量**）✓
    /// </summary>
    public const int QuirkFieldSinceVersion = 3;

    /// <summary>
    /// 🔴 **`roster.trinkets`（饰品）自这一版起是【必备件】** —— v3 及更早的档里**结构上就没有**它
    /// （M4u 之前饰品没有任何入档入口）⇒ 由 v3⇒v4 迁移**补空表**；
    /// `SaveSerializer.Validate` 按本常量判「缺 = 损坏」（两处引用**同一个常量**）✓
    /// </summary>
    public const int TrinketFieldSinceVersion = 4;

    /// <summary>
    /// 🔴 **迁移**：把 `raw` 升到 `CurrentVersion`。
    /// <para>· `Version == CurrentVersion` ⇒ 直接通过；</para>
    /// <para>· `Version &lt; CurrentVersion` ⇒ 逐级迁移（今天只有 v1⇒v2 一级；再早的版本视为损坏档，失败并保留）；</para>
    /// <para>· `Version &gt; CurrentVersion` ⇒ **未来版本**（用更新的游戏写过）⇒ 失败并保留 ⚠️</para>
    /// </summary>
    public static MigrationResult Migrate(SaveSnapshot raw)
    {
        if (raw is null)
        {
            throw new ArgumentNullException(nameof(raw));
        }

        if (raw.Version == CurrentVersion)
        {
            return MigrationResult.Success(raw, $"存档版本 {raw.Version}（当前）✓");
        }

        // 🔴 **未来版本**：绝不尝试"降级读取"（读不懂却假装读懂 = 静默损坏）⇒ 原样保留 + 明确回报 ✓
        if (raw.Version > CurrentVersion)
        {
            return MigrationResult.Fail(raw,
                $"存档版本 {raw.Version} 高于当前支持的 {CurrentVersion}（来自更新的版本）" +
                "—— **原档已保留**，请用对应版本打开 ✓");
        }

        // 🔴 **逐级迁移**（v1 ⇒ v2 ⇒ v3 ⇒ v4）：每一级只补"那一版之后新增的必备件"，
        //    且**如实回报补了什么**（逐级写进消息，不合并成一句含糊的"已升级"）；
        //    迁移**不碰磁盘**（落盘与否由调用方决定）✓
        SaveSnapshot cur = raw;
        var notes = new System.Text.StringBuilder();

        // v1 ⇒ v2（`H-1` 装备阶入档 · 2026-09-30）：v1 档**结构上不可能**含装备阶
        // （那一版既没有 `gear` 字段、也没有升级入口）⇒ "补空表"**不是静默兜底**，
        // 而是**那一版语义的唯一忠实读法**：空表 == 全部英雄第 0 阶 ✓
        if (cur.Version == 1)
        {
            cur = cur with
            {
                Version = GearFieldSinceVersion,   // 🔴 引用常量（不写死字面量 —— 与 `Validate` 同源 · 数字纪律）✓
                Gear = cur.Gear ?? new GearSnapshot(System.Array.Empty<GearTierSnapshot>()),
            };
            notes.Append("v1 ⇒ v2：新增 `gear`（H-1 装备阶）⇒ 补**空表**（= 全部英雄第 0 阶）✓　");
        }

        // v2 ⇒ v3（`M5u` 怪癖入档 · 2026-10-01）：v2 档里**没有任何怪癖入口**
        // （招募不发怪癖、curio 未接线）⇒ 补**空表**同样是那一版语义的唯一忠实读法 ✓
        if (cur.Version == 2)
        {
            cur = cur with
            {
                Version = QuirkFieldSinceVersion,   // 🔴 同上：字面量 3 不进内核（数据纪律门禁会红）✓
                Roster = cur.Roster with
                {
                    Quirks = cur.Roster.Quirks ?? new Dictionary<string, IReadOnlyList<string>>(),
                },
            };
            notes.Append("v2 ⇒ v3：新增 `roster.quirks`（M5u 怪癖）⇒ 补**空表**（= 没有英雄持怪癖）✓　");
        }

        // v3 ⇒ v4（`M4u` 饰品入档 · 2026-10-02）：v3 档里**没有任何饰品入口**
        // （装备阶与怪癖是当时仅有的两块跨趟新增状态）⇒ 补**空表**同样是那一版语义的唯一忠实读法 ✓
        if (cur.Version == 3)
        {
            cur = cur with
            {
                Version = TrinketFieldSinceVersion,   // 🔴 同上：字面量 4 不进内核（数据纪律门禁会红）✓
                Roster = cur.Roster with
                {
                    Trinkets = cur.Roster.Trinkets ?? new Dictionary<string, IReadOnlyList<string>>(),
                },
            };
            notes.Append("v3 ⇒ v4：新增 `roster.trinkets`（M4u 饰品 · 每英雄 2 槽）⇒ 补**空表**（= 没有英雄持饰品）✓　");
        }

        if (cur.Version == CurrentVersion)
        {
            return MigrationResult.Success(cur, $"存档版本 {raw.Version} ⇒ {CurrentVersion}：" + notes);
        }

        // 🔴 **更早的版本**（v0 及以下）：不存在 ⇒ 走到这里说明档是坏的/伪造的 ⇒ 保留并报错 ✓
        return MigrationResult.Fail(raw,
            $"存档版本 {raw.Version} 低于当前 {CurrentVersion}，且没有对应的迁移路径" +
            "—— **原档已保留**（不删档是硬纪律）✓");
    }
}
