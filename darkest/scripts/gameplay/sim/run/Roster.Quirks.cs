using System;
using System.Collections.Generic;
using Darkest.Core.Events;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **M5 · Quirk（怪癖）状态半**（`dd1_workstreams.md §3` · M5u · 2026-10-01）——
/// `Roster` 的 partial 第三片（`Roster.cs` 状态 ／ `Roster.Save.cs` 存档 ／ **本文件 怪癖**）✓
///
/// <para>**为什么住在名册**：`QuirksConfig` 的协议（`QuirksConfig.cs` 类型注释）写明 ——
/// **不做 `QuirkLedger`**，「英雄身上有哪些 quirk」归 `Roster` ✓（与疾病 `_diseases` 同层同型）✓</para>
///
/// <para>🔴 **互斥判据不住这里**：`Roster` 不持有 `QuirksConfig`（配置不进状态对象 ⇒ 存档不卷配置）⇒
/// 互斥的唯一落点是 `QuirksConfig.AreIncompatible`，由**掷签方**在掷之前过滤候选池
/// （`StagecoachRecruits.RollQuirk`）✓ —— 本文件只做**状态搬运 + 事件留痕** ✓</para>
///
/// <para>⚠️ **本件不做「移除怪癖」**：`sanitarium.json` 的三项服务里没有怪癖治疗入口，
/// 加了就是"只被测试调用的死函数"（本项目纪律）⇒ 登记待策划，不预造 API ✓</para>
/// </summary>
public sealed partial class Roster
{
    private readonly Dictionary<string, HashSet<string>> _quirks = new();

    /// <summary>某英雄当前所持怪癖（id 集合；没有 ⇒ 空集合，不抛）。</summary>
    public IReadOnlyCollection<string> QuirksOf(string heroId)
    {
        _ = MoraleOf(heroId); // 顺带校验英雄存在（与 `DiseasesOf` 同一口径）✓
        return _quirks.TryGetValue(heroId, out HashSet<string>? set) ? set : Array.Empty<string>();
    }

    /// <summary>
    /// **获得一条怪癖**（重复 = no-op 返回 false）；变更**必写 `HeroQuirkGainedEvent`** ✓
    /// ⚠️ 互斥**不在本方法判**（本类不持有 `QuirksConfig`）—— 掷签方先过滤，见类型注释 ✓
    /// </summary>
    public bool AddQuirk(CombatLog log, string heroId, string quirkId, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _ = MoraleOf(heroId);
        if (!_quirks.TryGetValue(heroId, out HashSet<string>? set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _quirks[heroId] = set;
        }

        if (!set.Add(quirkId))
        {
            return false; // 已有 ⇒ no-op（与 `Infect` 的"重复患病 = no-op"一致）✓
        }

        log.Append(new HeroQuirkGainedEvent(heroId, quirkId, reason));
        return true;
    }
}
