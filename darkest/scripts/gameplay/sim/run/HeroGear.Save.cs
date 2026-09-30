using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Gameplay.Sim.Save;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 `HeroGearState` 的**存档存取**（`P4 ①` · 2026-09-30）—— `HeroGearState` 的 partial 另一半。
///
/// <para>**为什么在这里而不是独立静态类**：快照要读写私有字段 `_tiers`，静态类拿不到
/// ⇒ 用 partial 拿到私有访问权，同时**不撑大 `HeroGear.cs`**（同族：`Roster.Save.cs` / `MetaState.Save.cs`）✓</para>
///
/// <para>**分工**：本文件只做「**状态 ⇄ 快照**」的搬运；
/// **序列化**归 `SaveSerializer`、**迁移**归 `SaveMigrator`、**落盘**归 scene 层 `SaveFileGateway` ✓</para>
/// </summary>
public sealed partial class HeroGearState
{
    /// <summary>
    /// 🔴 **存**：把当前阶表搬进快照（**纯读取，不改任何状态**）。
    /// <para>输出**按 `heroId` 排序** ⇒ 同一状态每次存盘的文本逐字相同（可 diff、可逐字比对）✓</para>
    /// </summary>
    public GearSnapshot CaptureSnapshot()
    {
        IReadOnlyList<GearTierSnapshot> tiers = _tiers
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new GearTierSnapshot(kv.Key, kv.Value.Weapon, kv.Value.Armour))
            .ToList();

        return new GearSnapshot(tiers);
    }

    /// <summary>
    /// 🔴 **取**：用快照**覆盖**当前阶表（**只搬状态，不写事件** —— 恢复不是游戏事件 ✓）。
    ///
    /// <para>🔴 **先全量校验、再落地**：`_tiers` 是 `readonly` ⇒ 只能「清空再填」，
    /// 若**边填边判** ⇒ 坏档会让容器停在**"清了一半"的半截态**（读档失败了，内存里却留着半个阶表）
    /// ⇒ 先把整份快照读进临时列表（不合法即抛），确认整份合法后再动容器 ✓</para>
    /// </summary>
    /// <exception cref="InvalidDataException">快照缺 `tiers` / 条目缺 `heroId` / 阶越界（损坏档必须**看得见**）✓</exception>
    public void RestoreFrom(GearSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (snapshot.Tiers is null)
        {
            throw new InvalidDataException("装备阶快照缺 `tiers` —— 拒绝静默读成空表（红线 21）✓");
        }

        var next = new List<(string HeroId, int Weapon, int Armour)>();
        foreach (GearTierSnapshot t in snapshot.Tiers)
        {
            if (t is null || string.IsNullOrEmpty(t.HeroId))
            {
                throw new InvalidDataException("装备阶快照里有一条没有 `heroId` —— 判为损坏档 ✓");
            }

            // 🔴 阶域 = 0~`MaxTier`（与 `units.json` 的 5 阶表同口径）⇒ 越界**即抛**
            //    （不静默钳到 4 阶：钳住 = 玩家看到的读数与档里写的不是一回事）
            if (t.Weapon < 0 || t.Weapon > HeroGear.MaxTier || t.Armour < 0 || t.Armour > HeroGear.MaxTier)
            {
                throw new InvalidDataException(
                    $"装备阶越界：{t.HeroId} 武器 {t.Weapon} / 护甲 {t.Armour}" +
                    $"（合法 0~{HeroGear.MaxTier}）—— 判为损坏档 ✓");
            }

            next.Add((t.HeroId, t.Weapon, t.Armour));
        }

        _tiers.Clear();
        foreach ((string heroId, int weapon, int armour) in next)
        {
            _tiers[heroId] = (weapon, armour);
        }
    }
}
