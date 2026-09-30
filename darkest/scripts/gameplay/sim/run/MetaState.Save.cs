using System;
using System.Collections.Generic;
using Darkest.Gameplay.Sim.Save;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **元层「三小件」的存档存取**（`Phase 1`）—— `RunProgress` / `HeirloomStock` / `Economy`
/// 各自的 partial 另一半。
///
/// <para>**为什么三合一**：这三个状态各自只有 2 个字段，单独开文件会变成三个 40 行桩文件；
/// 它们**同属一个主题**（"元层跨趟状态"）⇒ 合并在一处更好读 ✓</para>
///
/// <para>**为什么用 partial 而不是独立静态类**：快照要写 `private set` 的属性与私有字典，
/// 静态类拿不到 ⇒ partial 拿到访问权，同时**不撑大各自的主文件** ✓</para>
///
/// <para>🔴 **恢复只搬状态、不写事件** —— 读档不是游戏事件，写进 `CombatLog` 会污染事件流 ✓</para>
/// </summary>
public sealed partial class RunProgress
{
    /// <summary>🔴 **存**：跨趟进度（已完成出征数 / 已胜场数）。</summary>
    public ProgressSnapshot CaptureSnapshot() => new(RunsFinished, BattlesWon);

    /// <summary>🔴 **取**：覆盖当前跨趟进度。</summary>
    public void RestoreFrom(ProgressSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        RunsFinished = Math.Max(0, snapshot.RunsFinished);
        BattlesWon = Math.Max(0, snapshot.BattlesWon);
    }
}

public sealed partial class HeirloomStock
{
    /// <summary>🔴 **存**：传家宝存量 + 各建筑当前等级。</summary>
    public HeirloomSnapshot CaptureSnapshot() => new(
        new Dictionary<string, int>(_counts),
        new Dictionary<string, int>(_levels));

    /// <summary>
    /// 🔴 **取**：覆盖存量与建筑等级。
    /// <para>🔴 `_counts` 在构造时已按配置种类**预置为 0** ⇒ 这里**只覆盖、不新增种类**
    /// （快照里出现配置没有的种类会被忽略 —— 那是**删掉的种类**，不是错误）✓</para>
    /// </summary>
    public void RestoreFrom(HeirloomSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        foreach ((string kind, int count) in snapshot.Counts)
        {
            _counts[kind] = Math.Max(0, count);
        }

        foreach ((string building, int level) in snapshot.Levels)
        {
            _levels[building] = Math.Max(0, level);
        }
    }
}

public sealed partial class Economy
{
    /// <summary>🔴 **存**：金币与累计发放额。</summary>
    public EconomySnapshot CaptureSnapshot() => new(Gold, AwardedTotal);

    /// <summary>🔴 **取**：覆盖金币与累计发放额（负数钳到 0 —— 损坏档不该让金币变负）。</summary>
    public void RestoreFrom(EconomySnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        Gold = Math.Max(0, snapshot.Gold);
        AwardedTotal = Math.Max(0, snapshot.AwardedTotal);
    }
}
