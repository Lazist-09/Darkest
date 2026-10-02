using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.Recruit.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **M5u 高级新兵怪癖族**（原 `:346-419` 逐字节：`GrantRecruitQuirk`（掷签走内核纯函数 `StagecoachRecruits.RollQuirk`）／ `LastQuirkEventText` ／ 怪癖冒烟播种 `SeedQuirksForSmoke`（只经公开 API `Roster.AddQuirk`））✓
/// ③ 🔴 **依赖主类私有成员**：`_quirksCfg`／`_recruitRng`／`_log`；主类 `Refresh()`；片外 `QuirkKindLabel`（`HamletRoot.HeroDetail.Quirks.cs`）✓
/// ④ **只搬家、零行为改动**（逐字节同序；主片仅删 1 行纯空白 —— 原 `:223`）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 🆕 **M5u · M7③：高级新兵带 Quirk** —— 掷签走内核纯函数 `StagecoachRecruits.RollQuirk`
    /// （疾病不进池 · 与现持互斥的不进池 ⇒ 候选池口径**只有一处**），落状态走 `Roster.AddQuirk`（必写事件）✓
    /// ⚠️ 库未加载 ／ 池为空 ⇒ **如实打印「本次不发」**（不编一条怪癖 —— 红线 21：不留不可解释状态）✓
    /// </summary>
    private void GrantRecruitQuirk(Roster roster, string heroId)
    {
        if (_quirksCfg is null)
        {
            GD.Print($"[HamletRoot] 高级新兵 {heroId}：怪癖库未加载 ⇒ **本次不发怪癖**（如实上报，不编）✓");
            return;
        }

        string? quirkId = StagecoachRecruits.RollQuirk(_quirksCfg, roster.QuirksOf(heroId), _recruitRng);
        if (quirkId is null)
        {
            GD.Print($"[HamletRoot] 高级新兵 {heroId}：候选池为空（库 {_quirksCfg.Quirks.Count} 条全部被过滤）" +
                     "⇒ **本次不发怪癖** ✓");
            return;
        }

        bool added = roster.AddQuirk(_log, heroId, quirkId, "stagecoach_upgraded");
        GD.Print($"[HamletRoot] 高级新兵 {heroId} ⇒ 掷得怪癖 `{quirkId}`（{QuirkKindLabel(_quirksCfg.Get(quirkId))}" +
                 $" · 新增={added}）　事件原文 {LastQuirkEventText()} ✓");
    }

    /// <summary>回读最近一条怪癖事件原文（`HeroQuirkGainedEvent`；没有 ⇒ "（无）"）✓</summary>
    private string LastQuirkEventText()
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            if (_log.Events[i] is Darkest.Core.Events.HeroQuirkGainedEvent e)
            {
                return $"hero_quirk_gained: {e.HeroId} {e.QuirkId}（{e.Reason}）";
            }
        }

        return "（无）";
    }

    /// <summary>
    /// 🆕 **M5u · 怪癖冒烟播种**（`--hamlet-quirk-seed=&lt;英雄&gt;:&lt;怪癖&gt;`，逗号可多条）——
    /// 走**公开 API** `Roster.AddQuirk`（必写 `HeroQuirkGainedEvent`），**不直接改私有字典**（红线 26）✓
    /// ⚠️ 库里没有的 id ⇒ `QuirksConfig.Get` **fail-fast**（如实炸，不静默跳过）；
    /// 英雄不存在 ⇒ `AddQuirk` 内 `MoraleOf` 当场抛 ✓（数值本身**一个都不改** —— `#307` 冻结）✓
    /// </summary>
    private void SeedQuirksForSmoke(string spec)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null || _quirksCfg is null)
        {
            GD.Print($"[HamletRoot] 怪癖播种：名册={roster is not null} 库={_quirksCfg is not null} ⇒ 跳过（如实上报，不假装种上）");
            return;
        }

        foreach (string item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int sep = item.IndexOf(':');
            if (sep <= 0 || sep == item.Length - 1)
            {
                GD.Print($"[HamletRoot] 怪癖播种：`{item}` 不是「英雄:怪癖」⇒ 拒绝（不猜）✓");
                continue;
            }

            string heroId = item[..sep];
            string quirkId = item[(sep + 1)..];
            QuirkConfig q = _quirksCfg.Get(quirkId);   // 不存在 ⇒ fail-fast ✓
            bool ok = roster.AddQuirk(_log, heroId, quirkId, "smoke-seed");
            GD.Print($"[HamletRoot] 怪癖播种：{heroId} ⇒ {quirkId}（{QuirkKindLabel(q)} · 新增={ok}" +
                     $" · 现持 {roster.QuirksOf(heroId).Count} 条）✓");
        }

        Refresh();   // 🔴 名册行重建 ⇒ 播种立刻可见（不等到下一次刷新）✓
    }
}
