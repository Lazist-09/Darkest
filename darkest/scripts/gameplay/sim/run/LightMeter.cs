using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

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

    public LightTier Tier => TierFor(Value);

    public TuningLightEffect Effect => _config.Effects[TierId(Tier)];

    /// <summary>边界取档（**写死**，P21 ①）：`&gt;75` Radiant ／ `75~51` Dim ／ `50~26` Shadowy ／ `25~1` Dark ／ `0` Black。</summary>
    public static LightTier TierFor(int value) => value switch
    {
        > 75 => LightTier.Radiant,
        > 50 => LightTier.Dim,
        > 25 => LightTier.Shadowy,
        > 0 => LightTier.Dark,
        _ => LightTier.Black,
    };

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
