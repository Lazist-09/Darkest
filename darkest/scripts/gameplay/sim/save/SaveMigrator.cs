using System;

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
/// <para>**当前版本 = 1**：本版是存档系统的**第一版**，还没有"旧版本"要迁 ⇒
/// 迁移链目前是**空的**，但**结构已就位**（将来加版本只需在 `Migrate` 里追加一级）✓</para>
/// </summary>
public static class SaveMigrator
{
    /// <summary>🔴 当前存档格式版本。**新增字段/改结构时 +1，并在 `Migrate` 追加一级迁移** ✓</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// 🔴 **迁移**：把 `raw` 升到 `CurrentVersion`。
    /// <para>· `Version == CurrentVersion` ⇒ 直接通过；</para>
    /// <para>· `Version &lt; CurrentVersion` ⇒ 逐级迁移（目前无旧版本 ⇒ 视为损坏档，失败并保留）；</para>
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

        // 🔴 **旧版本**：本版是 v1，不存在"更早的版本" ⇒ 走到这里说明档是坏的/伪造的 ⇒ 保留并报错 ✓
        return MigrationResult.Fail(raw,
            $"存档版本 {raw.Version} 低于当前 {CurrentVersion}，且没有对应的迁移路径" +
            "—— **原档已保留**（不删档是硬纪律）✓");
    }
}
