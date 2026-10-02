// ① 来源：从 `ExpeditionSession.CampAndBonuses.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **跨场 buff 与 Curio 圣坛台账**（M11 ① 预警面拆分第四片）✓
// ② 职责：`_runBuffs`/`BindSortie`/`RunBuffs`/`GrantRunBuff`/`InjectRunBuffs`/`UnmappedRunBuffs`/`ConsumeRunBuffsAfterBattle`
//    ＋ Curio 圣坛 `_curioDamageBlessingPct`/`CurioDamageBlessingPct`/`GrantCurioDamageBlessing` ✓
// ③ 🔴 依赖主类私有成员/状态：`_heroSlots`/`_runBuffs`/`_curioDamageBlessingPct`（本片声明）
//    ＋ 同族 part：`_curioDamageBlessingPct` 另被 `.CampAndBonuses`（`EndCamp` 清）与 `ExpeditionSession.cs`（每场注入）读写 ✓
// ④ 只搬家、零行为改动（逐行原样搬运 · 一字未改）＋ 读数对照：拆前 29 通过/0 失败 ⇒ 拆后同数 ✓

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionSession
{
    // ------------------------------------------------------------------
    // 🔴 跨场 buff（`m7_expedition.md:160` 的三类型之二：`next_battle`）
    //    契约：`next_battle`（磨刀/加固甲胄）= **仅下一场**；`battles:N` = 跨场计数（每场 −1，扎营不清）
    //    落点：**每场开场把未过期的 buff 注入该场台账**（不是新状态层；台账仍是 buff 的唯一真相）
    // ------------------------------------------------------------------

    private readonly List<(int Slot, string BuffId, int RemainingBattles)> _runBuffs = new();
    private readonly Dictionary<string, int> _heroSlots = new(StringComparer.Ordinal);

    /// <summary>
    /// 🔴 **绑定出征编成**（`英雄 id → 阵型槽位`）—— 由组合根在建会话时调用。
    /// 原因（实测）：**营地侧用英雄 id**（`hero_warrior_1`），**战斗侧玩家单位用原型 id**（`warrior` ／ `warrior_2` …）
    /// ⇒ 两套 id 体系 ⇒ 跨场 buff 必须**按槽位**挂载，注入时用 `Player.UnitRuntimeAt(slot)` 换成本场单位 id ✓
    /// （槽位来自 `FormationConfig` 的 `initial_roster.player[].slot`，与 `FormationSortie` 的顺序一致）
    /// </summary>
    public void BindSortie(IReadOnlyDictionary<string, int> heroSlotById)
    {
        _heroSlots.Clear();
        foreach ((string hero, int slot) in heroSlotById)
        {
            _heroSlots[hero] = slot;
        }
    }

    /// <summary>本趟挂着的跨场 buff（供测试/日志：`(槽位, buffId, 剩余场数)`）。</summary>
    public IReadOnlyList<(int Slot, string BuffId, int RemainingBattles)> RunBuffs => _runBuffs;

    /// <summary>给某英雄挂一个【跨场】buff（`next_battle` ⇒ `remainingBattles: 1`）；按槽位记账。</summary>
    public void GrantRunBuff(UnitId unit, string buffId, int remainingBattles)
    {
        if (remainingBattles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingBattles), "跨场 buff 的剩余场数必须 ≥ 1。");
        }

        int slot = _heroSlots.TryGetValue(unit.Value, out int s) ? s : 0;
        _runBuffs.RemoveAll(b => b.Slot == slot && b.BuffId == buffId); // refresh 语义
        _runBuffs.Add((slot, buffId, remainingBattles));
    }

    /// <summary>
    /// 🔴 **把跨场 buff 注入本场**（每场开场调用；只对"本场在场的单位"注入）。
    /// `next_battle` 类的注入后**在本场结束时消耗**（见 <see cref="ConsumeRunBuffsAfterBattle"/>）。
    /// </summary>
    public void InjectRunBuffs(BattleDirector director, CombatLog log)
    {
        if (_runBuffs.Count == 0)
        {
            return;
        }

        foreach ((int slot, string buffId, int _) in _runBuffs.ToArray())
        {
            UnitRuntime? u = slot > 0 ? director.Player.UnitRuntimeAt(slot) : null;
            if (u is null)
            {
                // 🔴 **已知阻塞（不静默）**：未能把"英雄"映射到本场单位（槽位未绑定 / 该位无人）。
                UnmappedRunBuffs++;
                log.Append(new EffectEvent(default, $"run_buff_unmapped:{buffId}", 0.0, Triggered: false));
                continue;
            }

            director.Buffs.Add(u.Id, buffId, source: null);
            log.Append(new EffectEvent(u.Id, $"run_buff:{buffId}", 100.0, true));
        }
    }

    /// <summary>未能注入的跨场 buff 次数（**供测试/日志断言**：`> 0` 说明 hero→战斗单位映射仍缺）。</summary>
    public int UnmappedRunBuffs { get; private set; }

    // ------------------------------------------------------------------
    // 🔴 Curio 圣坛（`curio.md` §3 #5）：**本趟 +N% 伤害，到扎营**
    //    · 跨场：每场开场挂 `curio_altar_blessing_{pct}` 到**全队**
    //    · 到期：**扎营清**（= `until_next_recovery` 的语义 ✓ 与死门后遗症同一个"下次恢复"）
    //    · 叠加：**取大（refresh）** —— 契约设计检查③："不同道具给不同等级的好结果"（空手 +20 ／ 支援包 +30）
    // ------------------------------------------------------------------

    private int _curioDamageBlessingPct;

    /// <summary>本趟的 Curio 伤害祝福（0 = 无）。</summary>
    public int CurioDamageBlessingPct => _curioDamageBlessingPct;

    /// <summary>授予 Curio 伤害祝福（**取大**；不叠加成 +50）。</summary>
    public void GrantCurioDamageBlessing(int percent)
    {
        if (percent > _curioDamageBlessingPct)
        {
            _curioDamageBlessingPct = percent;
        }
    }

    /// <summary>一场结束后：跨场 buff 的剩余场数 −1（到 0 清除）。`next_battle` ⇒ 1 ⇒ 紧接着就被清 ✓</summary>
    public void ConsumeRunBuffsAfterBattle()
    {
        for (int i = _runBuffs.Count - 1; i >= 0; i--)
        {
            (int slot, string buffId, int left) = _runBuffs[i];
            if (left <= 1)
            {
                _runBuffs.RemoveAt(i); // 用完即清（`next_battle` 的语义）
            }
            else
            {
                _runBuffs[i] = (slot, buffId, left - 1);
            }
        }
    }
}
