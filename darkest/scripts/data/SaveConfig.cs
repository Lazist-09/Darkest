using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **`save.json` 根模型**（`Phase 1` 存档系统）—— 存档的**工程参数**（不是游戏数值）。
///
/// <para>**为什么要有这份配置**：槽位数是**数字**（#307：数字一律外置、标 `placeholder`），
/// 硬编码在代码里将来改不动 ⇒ 走与 `economy.json` / `roster.json` **同一套**"数据 + 加载期校验"管线 ✓</para>
///
/// <para>**不在这里的东西**：存档的**内容结构**由 `SaveSnapshot` 定义、**格式版本**由
/// `SaveMigrator.CurrentVersion` 定义 ⇒ 本配置只管"**存几个档、叫什么名字**" ✓</para>
/// </summary>
public sealed record SaveConfig(
    [property: JsonPropertyName("slot_count")] int SlotCount,
    [property: JsonPropertyName("file_prefix")] string FilePrefix,
    // 🔴 占位标注（`#307`）：本配置的值是"真值之前的临时值"，待策划定档 ✓
    [property: JsonPropertyName("placeholder")] bool Placeholder = false)
{
    public const string ResPath = "res://data/save.json";

    public static SaveConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        SaveConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<SaveConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    /// <summary>第 `slot` 槽（0 基）的文件名（不含目录）；目录由 scene 层 `SaveFileGateway` 拼 `user://` ✓</summary>
    public string FileNameFor(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"槽位必须 ∈ [0,{SlotCount})。");
        }

        return $"{FilePrefix}{slot}.json";
    }

    private static void Validate(SaveConfig cfg)
    {
        // 🔴 槽位数：0 = "存档系统整个关掉"（与项目 opt-in 纪律一致：要关闭就**整段删掉**，而不是配 0 静默失效）
        if (cfg.SlotCount <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: slot_count 必须 > 0 —— 0 等于「配了但永远存不了」（静默失效家族）；" +
                "要关闭存档请**不加载** `save.json` ✓");
        }

        if (string.IsNullOrWhiteSpace(cfg.FilePrefix))
        {
            throw new InvalidDataException($"{ResPath}: file_prefix 不得为空（存档文件名前缀）。");
        }
    }
}
