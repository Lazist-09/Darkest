using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **战斗 · 名字与数据读取族**（技能池缓存 · 单位/技能/增益名 · `names.json` 惰性加载 · `ReadData`）✓
/// ③ 🔴 依赖主类静态成员：`_poolCache`/`_unitNames`/`_skillNames`/`_buffNames`/`_skillsCfg`；数据只读 `res://data/**` ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
    private static string[] SkillPool(string archetype)
    {
        if (_poolCache.TryGetValue(archetype, out string[]? cached))
        {
            return cached;
        }

        string[] pool = SkillsCfg.Skills
            .Where(s => s.OwnerUnit == archetype && !s.PoolExternal) // F1/P12：按 pool_external 标志过滤（非 id 后缀）
            .Select(s => s.Id).ToArray();
        _poolCache[archetype] = pool;
        return pool;
    }

    private static string NameOf(string unitId)
    {
        if (_unitNames.Count == 0)
        {
            LoadNames();
        }

        return _unitNames.TryGetValue(unitId, out string? n) ? n : unitId;
    }

    private static string SkillName(string skillId)
    {
        if (_skillNames.Count == 0)
        {
            LoadNames();
        }

        return _skillNames.TryGetValue(skillId, out string? n) ? n : skillId;
    }

    /// <summary>G2：buff 中文名（buff_defs.json；缺失回落 id）。</summary>
    private static string BuffNameOf(string buffId)
    {
        if (_buffNames.Count == 0)
        {
            foreach (BuffDefConfig b in BuffDefsConfig.Parse(ReadData("buff_defs.json")).Buffs)
            {
                _buffNames[b.Id] = b.Name;
            }
        }

        return _buffNames.TryGetValue(buffId, out string? n) ? n : buffId;
    }

    private static void LoadNames()
    {
        foreach (UnitConfig u in UnitsConfig.Parse(ReadData("units.json")).Units)
        {
            _unitNames[u.Id] = u.Name;
        }

        foreach (SkillTemplateConfig s in SkillsCfg.Skills)
        {
            _skillNames[s.Id] = s.Name;
        }
    }

    /// <summary>
    /// 🔴 **`O-84` 修复**（架构 `#314` 之后的裁定 / 红线 26）：表现层读数据**一律 `FileAccess`**。
    /// 原实现用 `System.IO`（`AppContext.BaseDirectory` 逐级向上找 `data/`）——
    /// 🔴 **导出构建里 `data/*.json` 在 PCK 内、不是磁盘目录** ⇒ `File.Exists` 永远找不到
    /// ⇒ **单场战斗入口（`Battle.tscn`）在发行版直接 `FileNotFoundException` 崩溃** ⚠️
    /// ⇒ 改用 `FileAccess.GetFileAsString("res://data/…")`（与其余 20 处同法，导出安全 ✓）
    /// </summary>
    private static string ReadData(string name)
    {
        string path = $"res://data/{name}";
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"{path}: 数据文件不存在（表现层只走 FileAccess/res://，见 O-84）。");
        }

        return Godot.FileAccess.GetFileAsString(path);
    }
}
