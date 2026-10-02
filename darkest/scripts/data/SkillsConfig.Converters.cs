// 🔴 从 `SkillsConfig.cs` 拆出（用户红线 ≤600 行 · 架构 file_size_split §1.2 的 ③.数据层）
//    职责 = **JSON 转换器**：`LowerEnumJsonConverter<T>`（小写 ASCII 枚举词表）＋ `SelfSlotsConverter`（"all" 或位置数组）
//    来源 = 原 `SkillsConfig` 记录体末尾「转换器」段（逐行搬家，**只搬家、零行为**）
//    依赖私有成员 = 无（两个转换器都是自足的私有嵌套类）
//    依赖公有面 = `SelfSlots` ／ 各枚举（`CreateOptions` 在主编里 new 它们）
//    复核 = `tools/dsh/audit_split_integrity.py`（拆前后成员名比对 · 期望 no member lost）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

public sealed partial record SkillsConfig
{

    // ------------------------------------------------------------------
    // 转换器
    // ------------------------------------------------------------------

    private sealed class LowerEnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        private readonly Dictionary<string, T> _read = new();
        private readonly Dictionary<T, string> _write = new();

        public LowerEnumJsonConverter(params (string Word, T Value)[] vocab)
        {
            foreach ((string word, T value) in vocab)
            {
                _read[word] = value;
                _write[value] = word;
            }
        }

        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            string? raw = reader.GetString();
            if (raw is not null && _read.TryGetValue(raw, out T value))
            {
                return value;
            }

            throw new JsonException($"未知枚举值 \"{raw}\"（{typeof(T).Name}）。");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(_write[value]);
    }

    private sealed class SelfSlotsConverter : JsonConverter<SelfSlots>
    {
        public override SelfSlots Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? s = reader.GetString();
                if (s == "all")
                {
                    return new SelfSlots(IsAll: true, Slots: Array.Empty<int>());
                }

                throw new JsonException($"self_slots 字符串只能为 \"all\"，实际 \"{s}\"。");
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = JsonSerializer.Deserialize<List<int>>(ref reader, options) ?? new List<int>();
                return new SelfSlots(IsAll: false, Slots: list);
            }

            throw new JsonException("self_slots 必须为 \"all\" 或位置数组。");
        }

        public override void Write(Utf8JsonWriter writer, SelfSlots value, JsonSerializerOptions options)
        {
            if (value.IsAll)
            {
                writer.WriteStringValue("all");
            }
            else
            {
                writer.WriteStartArray();
                foreach (int pos in value.Slots)
                {
                    writer.WriteNumberValue(pos);
                }

                writer.WriteEndArray();
            }
        }
    }
}
