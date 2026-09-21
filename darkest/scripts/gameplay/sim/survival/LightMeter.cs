using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using System.Collections.Generic;

namespace Darkest.Gameplay.Sim.Survival;

/// <summary>光照五档（DD 五档照抄；边界取档见 <see cref="LightMeter.TierFor"/>）。</summary>
public enum LightTier
{
    Radiant,
    Dim,
    Shadowy,
    Dark,
    Black,
}

/// <summary>
/// M7.5 D0 光照计（#258）：`Value` 0~100、进图 100、前进 −15、提亮 +30（**1 柴火**）、扎营回满。
/// 🔴 **柴火 = DD 的 Torch + Firewood 合一**（我们的资源本来就叫柴火）。
/// 🔴 **UI 必显 ⑨ ⇒ 每次变化必写 `LightChangedEvent`**（数字必须能从事件流复算）。
/// </summary>
public interface ILightMeter
{
    int Value { get; }

    LightTier Tier { get; }

    /// <summary>前进一个节点：−15（无随机）。</summary>
    void TryAdvanceNode(CombatLog log);

    /// <summary>手动提亮：+30，消耗 1 柴火（不足 → 拒绝且不变）。</summary>
    bool TryBrighten(CombatLog log, Func<bool> spendOneFirewood);

    /// <summary>扎营：光照回满 100。</summary>
    void OnCamp(CombatLog log);

    /// <summary>当前档位的光照效果（7 项；**无 HP 字段**）。</summary>
    TuningLightEffect Effect { get; }
}

/// <summary>默认实现（纯状态机，不持随机源）。</summary>
public sealed class LightMeter : ILightMeter
{
    public const int Max = 100;
    public const int Min = 0;

    private readonly TuningLight _config;

    public LightMeter(TuningLight config, int? initial = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Value = Math.Clamp(initial ?? config.EnterValue, Min, Max);
    }

    public int Value { get; private set; }

    public LightTier Tier => TierFor(Value, _config.Tiers);

    public TuningLightEffect Effect => _config.Effects[TierId(Tier)];

    /// <summary>
    /// 🔴 **档位边界 = 数据**（策划 `#328`① 裁 (b)：两侧都读 data ⇒ **唯一真相**）：
    /// 边界本来就在 `tuning.json` 的 `light.tiers`（每档 `min`/`max`）里 —— 此前**内核没用它**、
    /// 而在本文件硬写 `>75/>50/>25` ⚠️（UI 侧又抄一份 ⇒ 只改一处就"刻度与判定不符"）✓
    /// ⇒ 现在按 `tiers` 取档：**Radiant 76..100 ／ Dim 51..75 ／ Shadowy 26..50 ／ Dark 1..25 ／ Black 0** ✓
    /// （注意：仍**不是**数值改动 ⇒ `tiers` 的现有 min/max 与旧硬编码边界**逐一等价** ✓）
    /// </summary>
    public static LightTier TierFor(int value, IReadOnlyList<Darkest.Data.TuningLightTier> tiers)
    {
        foreach (Darkest.Data.TuningLightTier t in tiers)
        {
            if (value >= t.Min && value <= t.Max)
            {
                return TierFromId(t.Id);
            }
        }

        // 数据没覆盖 ⇒ 落到黑（例如 value < 0）；**不静默选一个"看起来合理"的档** ✓
        return LightTier.Black;
    }

    /// <summary>档位 id（data）→ 枚举 ✓</summary>
    public static LightTier TierFromId(string id) => id switch
    {
        "radiant" => LightTier.Radiant,
        "dim" => LightTier.Dim,
        "shadowy" => LightTier.Shadowy,
        "dark" => LightTier.Dark,
        _ => LightTier.Black,
    };

    /// <summary>
    /// 🔴 **UI 刻度用**：由 `light.tiers` 推出的边界值（除最亮档外，每档的 `max`）——
    /// UI 侧**必须**用它（或直接读 `tiers`）而不是另写一份数字 ⚠️
    /// </summary>
    public static int[] BoundariesFrom(IReadOnlyList<Darkest.Data.TuningLightTier> tiers)
    {
        var marks = new List<int>();
        foreach (Darkest.Data.TuningLightTier t in tiers)
        {
            // ⚠️ 排除两种：① 最亮档（max = 100 ⇒ 不是刻度线）② **零宽底档**（black 是 0..0 ⇒ max=0 不是刻度；
            //    我第一版漏了 ② ⇒ 刻度变成 {0,25,50,75} ⇒ 用例当场抓到 ✓）
            if (t.Max is > 0 and < 100)
            {
                marks.Add(t.Max);
            }
        }

        marks.Sort();
        return marks.ToArray();
    }

    public static string TierId(LightTier tier) => tier switch
    {
        LightTier.Radiant => "radiant",
        LightTier.Dim => "dim",
        LightTier.Shadowy => "shadowy",
        LightTier.Dark => "dark",
        _ => "black",
    };

    public void TryAdvanceNode(CombatLog log)
    {
        int before = Value;
        // 🔴 #278：`node_step` = **−30**（负值 = 前进消耗；提亮 +30 恰好抵消 ⇒ 提亮 = "买一个节点"）
        Value = Math.Clamp(Value + _config.NodeStep, Min, Max);
        Log(log, before, "advance");
    }

    /// <summary>
    /// 🔴 M7.6（§4.3②）：**按段移动** —— 用**显式代价**推进（新区域 −30 ／ 重走已探索 −10）。
    /// 与 `TryAdvanceNode` 同源（同一 `LightChangedEvent` 通道），但代价由调用方给（拓扑层决定"新/旧"）。
    /// </summary>
    public void TryAdvanceBy(CombatLog log, int cost, string reason)
    {
        int before = Value;
        Value = Math.Clamp(Value + cost, Min, Max);
        Log(log, before, reason);
    }

    public bool TryBrighten(CombatLog log, Func<bool> spendOneFirewood)
    {
        if (spendOneFirewood is null || !spendOneFirewood())
        {
            return false; // 🔴 柴火不足 → 拒绝且**不变**（P20 ② 同款语义）
        }

        int before = Value;
        Value = Math.Clamp(Value + _config.BrightenGain, Min, Max);
        Log(log, before, "brighten");
        return true;
    }

    public void OnCamp(CombatLog log)
    {
        int before = Value;
        Value = Math.Clamp(_config.CampRestoreTo, Min, Max);
        Log(log, before, "camp");
    }

    /// <summary>事件节点分支：举火把前进（+20）／摸黑前进（−20，但掉落更好）。</summary>
    public void ApplyEventChoice(CombatLog log, bool torch)
    {
        int before = Value;
        Value = Math.Clamp(Value + (torch ? _config.EventTorchGain : -_config.EventDarkCost), Min, Max);
        Log(log, before, torch ? "event_torch" : "event_dark");
    }

    /// <summary>从事件流**复算**光照（UI 必显 11 / ㉑㉒ 只能从 `LightChangedEvent` 统计；无事件则 = 进图值）。</summary>
    public static int Recompute(CombatLog log, int enterValue)
    {
        int value = enterValue;
        foreach (LightChangedEvent e in log.Events.OfType<LightChangedEvent>())
        {
            value = e.To; // 事件自带 To → 复算 = 取最后一条
        }

        return value;
    }

    /// <summary>进图起点（reason = "start"；契约六值之一，**必须发**以便复测归因）。</summary>
    public void EmitStart(CombatLog log)
        => log.Append(new LightChangedEvent(Value, Value, TierId(Tier), "start"));

    private void Log(CombatLog log, int before, string reason)
    {
        if (log is not null && before != Value)
        {
            log.Append(new LightChangedEvent(before, Value, TierId(Tier), reason));
        }
    }
}
