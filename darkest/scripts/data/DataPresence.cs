using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Darkest.Data;

/// <summary>
/// 🔴 **数据键【存在性】检查**（数字外置纪律 · 用户 2026-09-14 / `data_schema` **P29**）。
///
/// 解决一个**具体而隐蔽**的坑：C# 记录字段的默认值（`int BattleGoal = 3`）会让**JSON 缺键**时
/// **静默取 3** ⇒ ① 策划改不动它（值住在代码里）② 缺键这种数据错误**永远暴露不出来** ⚠️
///
/// 为什么不逐字段改成 `int?`：那会让**整条读取链**都要 `.Value`（涟漪大、且 0 与"缺键"仍难区分）。
/// ⇒ 这里用更直接的办法：**直接在原始 JSON 上断言"这些键必须存在"** ✓
/// （存在性 + 数值校验 各管一半：前者抓"缺键"，后者抓"值非法"）
/// </summary>
public static class DataPresence
{
    /// <summary>断言 `json` 里**存在**这些键（任意层级深度的**叶键名**匹配）；缺失 ⇒ 抛 `InvalidDataException` ✓</summary>
    public static void RequireKeys(string resPath, string json, params string[] keys)
    {
        HashSet<string> present = CollectKeys(json);
        var missing = new List<string>();
        foreach (string key in keys)
        {
            if (!present.Contains(key))
            {
                missing.Add(key);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidDataException(
                $"{resPath}: 缺少必需键 [{string.Join(" / ", missing)}] —— " +
                "它们属**可调数字/必需字段**，必须在数据里**显式给出**；" +
                "🔴 不许靠 C# 记录默认值兜底（否则缺键会静默生效，且策划改不动它）—— P29 数字外置纪律。");
        }
    }

    private static HashSet<string> CollectKeys(string json)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        using JsonDocument doc = JsonDocument.Parse(json);
        Walk(doc.RootElement, keys);
        return keys;
    }

    private static void Walk(JsonElement el, HashSet<string> keys)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty p in el.EnumerateObject())
                {
                    keys.Add(p.Name);
                    Walk(p.Value, keys);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in el.EnumerateArray())
                {
                    Walk(item, keys);
                }

                break;
        }
    }
}
