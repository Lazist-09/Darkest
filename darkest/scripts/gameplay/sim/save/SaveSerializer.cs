using System;
using System.IO;
using System.Text.Json;

namespace Darkest.Gameplay.Sim.Save;

/// <summary>
/// 🔴 **存档序列化**（`Phase 1`）—— `SaveSnapshot` ⇄ JSON 文本。
///
/// <para>**为什么用 `System.Text.Json` 而不是 Godot 的 `ResourceSaver`**：本文件在**内核层**
/// （`scripts/gameplay/sim/`），内核必须**零 `using Godot`** ⇒ 只能走 BCL 序列化 ✓
/// （落盘的**物理 IO** 在 scene 层 `SaveFileGateway`，那里才用 `FileAccess` —— O-84 红线 26）</para>
///
/// <para>**分工**：本类只管「**文本 ⇄ 对象**」；**版本迁移**归 `SaveMigrator`、
/// **文件读写**归 scene 层 ⇒ 三者互不越界 ✓</para>
///
/// <para>🔴 **纪律（红线 21）：绝不静默补默认** —— 反序列化后必做结构校验，
/// 缺了关键部件就抛（损坏档必须**看得见**，不能被悄悄读成一个"空档"）✓</para>
/// </summary>
public static class SaveSerializer
{
    /// <summary>序列化选项：**缩进**（存档要能被人打开看）+ **大小写不敏感**（兼容手写/旧档）✓</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 🔴 **存**：快照 ⇒ JSON 文本。
    /// </summary>
    public static string Serialize(SaveSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        return JsonSerializer.Serialize(snapshot, Options);
    }

    /// <summary>
    /// 🔴 **取**：JSON 文本 ⇒ 快照；**结构不合法即抛**（不返回 null、不静默填空档）。
    /// </summary>
    /// <exception cref="InvalidDataException">文本为空 / 不是合法 JSON / 关键部件缺失。</exception>
    public static SaveSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("存档文本为空 —— 拒绝静默读成「空档」（红线 21）✓");
        }

        SaveSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SaveSnapshot>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"存档不是合法 JSON（损坏档）：{ex.Message}", ex);
        }

        if (snapshot is null)
        {
            throw new InvalidDataException("存档反序列化结果为 null —— 拒绝静默读成「空档」（红线 21）✓");
        }

        Validate(snapshot);
        return snapshot;
    }

    /// <summary>
    /// 🔴 **结构校验**：关键部件缺失 ⇒ 抛。
    /// <para>**为什么需要**：`System.Text.Json` 缺字段会**静默给默认值** ⇒
    /// 一个被截断的档会被读成"名册空、金币 0" —— 玩家以为档没了，实际是**读坏了却没报** ⚠️
    /// ⇒ 这里显式把"必须存在的部件"列出来逐个查 ✓</para>
    /// </summary>
    private static void Validate(SaveSnapshot snapshot)
    {
        if (snapshot.Roster is null)
        {
            throw new InvalidDataException("存档缺 `roster` —— 名册是存档的核心，缺失即损坏 ✓");
        }

        if (snapshot.Roster.Heroes is null || snapshot.Roster.Morale is null)
        {
            throw new InvalidDataException("存档的 `roster.heroes` / `roster.morale` 缺失 —— 即损坏 ✓");
        }

        if (snapshot.Progress is null)
        {
            throw new InvalidDataException("存档缺 `progress` —— 即损坏 ✓");
        }

        if (snapshot.Heirlooms is null)
        {
            throw new InvalidDataException("存档缺 `heirlooms` —— 即损坏 ✓");
        }

        if (snapshot.Economy is null)
        {
            throw new InvalidDataException("存档缺 `economy` —— 即损坏 ✓");
        }

        // 🔴 `gear`（装备阶）**自 v2 起是必备件**：缺 ⇒ 损坏（红线 21：绝不静默当成"全 0 阶"）。
        //    ⚠️ **必须按版本分档判**：v1 档里**结构上就没有**这个字段（不是被截断）
        //    ⇒ 一刀切会把**老档误判成损坏档**；补空表的职责归 `SaveMigrator` 的 v1⇒v2 迁移 ✓
        if (snapshot.Version >= SaveMigrator.GearFieldSinceVersion)
        {
            if (snapshot.Gear is null)
            {
                throw new InvalidDataException(
                    $"存档（v{snapshot.Version}）缺 `gear` —— 自 v{SaveMigrator.GearFieldSinceVersion} " +
                    "起它是必备件，缺失即损坏 ✓");
            }

            if (snapshot.Gear.Tiers is null)
            {
                throw new InvalidDataException("存档的 `gear.tiers` 缺失 —— 即损坏（不静默读成空表）✓");
            }
        }

        // 🔴 `roster.quirks`（怪癖）**自 v3 起是必备件**：缺 ⇒ 损坏（红线 21：绝不静默当成「没有怪癖」）。
        //    ⚠️ 同样**按版本分档判**：v2 及更早的档里结构上就没有这个字段 ⇒ 补空表的职责归
        //    `SaveMigrator` 的 v2⇒v3 迁移（一刀切会把老档误判成损坏档）✓
        if (snapshot.Version >= SaveMigrator.QuirkFieldSinceVersion && snapshot.Roster.Quirks is null)
        {
            throw new InvalidDataException(
                $"存档（v{snapshot.Version}）缺 `roster.quirks` —— 自 v{SaveMigrator.QuirkFieldSinceVersion} " +
                "起它是必备件，缺失即损坏（不静默读成「没有怪癖」）✓");
        }
    }
}
