// ① 来源：从 `ExpeditionSession.CampAndBonuses.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **本趟台账 ⇄ 本场战斗的桥**（M11 ① 预警面拆分第三片）✓
// ② 职责：`ApplyCampBonusesToBattle` 施加 · `ApplyRetainedHpToBattle`/`CaptureBattleEndHp` 承上启下（O-83）·
//    `StripCampBonusesFromRetained` 战后扣回 · `UnitAtHeroSlot` 英雄↔战斗单位映射 ＋ `log_note`/`CampNotes` 施加留痕 ✓
// ③ 🔴 依赖主类私有成员/状态：`Retained`/`MoraleStart`（`.CampAndBonuses`）· `_heroSlots`（`.RunBuffs`）· `_campMoraleBonus`/`_campHpBonusPct`（`.CampSkills`）
//    ＋ 本片私有 `_heroBattleIds`/`_campNotes`（**调用顺序契约**：`ApplyRetainedHpToBattle` 必须先于 `ApplyCampBonusesToBattle`）✓
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
    /// <summary>
    /// 把本趟营地加成施加到本场（**战斗开场**调用；加成不进 `Retained` 的持久值）。
    /// 🔴 **必须按【槽位】映射**：本台账按**英雄 id** 记账，而战斗单位的 `Id` 是**原型 id**（两套体系）
    /// —— 我第一版直接比 `u.Id.Value` ⇒ **永不命中、加成静默失效** ⚠️（用例当场抓到）
    /// </summary>
    public void ApplyCampBonusesToBattle(BattleDirector director)
    {
        if (_campMoraleBonus.Count == 0 && _campHpBonusPct.Count == 0)
        {
            return;
        }

        foreach ((string heroId, int morale) in _campMoraleBonus)
        {
            UnitRuntime? u = UnitAtHeroSlot(director, heroId);
            if (u is not null && morale != 0)
            {
                u.Morale = Math.Clamp(u.Morale + morale, 0, 100);
                // 🔴 `Retained` 的键是**战斗单位 id**（不是英雄 id）⇒ 记下来供战后【扣回】用
                _heroBattleIds[heroId] = u.Id.Value;
                log_note($"[camp] 本趟士气加成 +{morale} ⇒ 英雄 {heroId}（槽位 {_heroSlots[heroId]}）");
            }
        }

        foreach ((string heroId, int pct) in _campHpBonusPct)
        {
            UnitRuntime? u = UnitAtHeroSlot(director, heroId);
            if (u is not null && pct > 0)
            {
                int bonus = Math.Max(1, (int)Math.Round(u.MaxHp * (pct / 100.0)));
                u.CurrentHp = Math.Min(u.MaxHp, u.CurrentHp + bonus);
                log_note($"[camp] 本趟开局 HP +{pct}% ⇒ 英雄 {heroId}（+{bonus}）");
            }
        }
    }

    /// <summary>
    /// 🔴 **`O-83`：把上一场保留的 HP 应用到本场开局**（"承上"）。
    ///
    /// 契约（`blueprint` 明文）：「战后【不自动恢复】：**HP 与士气跨战斗完全保留**；场间无恢复（切片无扎营/回城/战后回血）」。
    /// ⚠️ 我此前只把 HP 记进 `Retained`、**从未用于下一场** ⇒ **每场都满血开局** = 红线 21 家族缺口（架构在 `blueprint` 里也标了这个问号）。
    ///
    /// 🔴 **必须按【槽位】映射**（两套 id 体系的唯一正确接法，照 `ApplyCampBonusesToBattle`）：
    /// `Retained` 的键是**上一场的战斗单位 id**（原型 id 体系），英雄用英雄 id 记账 ⇒
    /// 先经 `_heroSlots` 找到本场单位，再用 `_heroBattleIds`（此刻还是**上一场的** id）查 `Retained`。
    /// 因此本方法**必须在 `ApplyCampBonusesToBattle` 之前调用**（后者会覆写 `_heroBattleIds`）。
    ///
    /// ⚠️ 结转值为 0（阵亡却仍被编入）⇒ **不静默补满**：照实写 0 并留痕 `retained_hp_zero_but_deployed`（那是流程 bug 的信号）✓
    /// </summary>
    public void ApplyRetainedHpToBattle(BattleDirector director, CombatLog log)
    {
        foreach ((string heroId, int slot) in _heroSlots)
        {
            UnitRuntime? u = UnitAtHeroSlot(director, heroId);
            if (u is null)
            {
                continue;
            }

            // 上一场的战斗单位 id（本方法在 `ApplyCampBonusesToBattle` 之前 ⇒ 此刻仍是上一场的）✓
            // 🔴🔴 **两种键空间都要认**（`O-83` 真缺陷，靠"绑了阵型读数仍逐字不变"追出来的）：
            //    · **场景路径**：`CaptureBattleEndHp`（我加的）按【战斗单位 id】写 `Retained` ⇒ 这里用 `prevBattleId` 命中 ✓
            //    · **内核路径**（探针 / `RunSession.EndBattle`）：按【`_roster` 里的 id】写 `Retained`
            //      ⇒ 用战斗单位 id 查**必然落空** ⇒ 于是"承上"在这条路上**静默跳过**（连留痕都没有）⚠️
            //    ⇒ 只认一种键 = 一半路径永远不生效（两套 id 体系的老陷阱的又一形态）；这里显式认两种 ✓
            (int Hp, int Morale, bool Weak) prev = default;
            bool found = _heroBattleIds.TryGetValue(heroId, out string? prevBattleId)
                         && Retained.TryGetValue(prevBattleId, out prev);
            if (!found)
            {
                found = Retained.TryGetValue(heroId, out prev);
            }
            if (found)
            {
                // 🔴 **开局 HP = 结转值**（无条件设置 + **必留痕**）——
                //    我第一版写成"只在 carried < u.CurrentHp 时才压低"，那是**静默分支**：
                //    实测（驱动复用棋盘 ⇒ 新单位起始值恰好等于结转值）⇒ 条件不成立 ⇒ **既没设值也没留痕**
                //    ⇒ 读数和用例都抓不到（红线 21：不许有"看起来接了、其实什么都没发生"的分支）⚠️
                int carried = Math.Min(prev.Hp, u.MaxHp); // `int.MaxValue` = 全满（开局/恢复后的哨兵值）✓
                u.CurrentHp = Math.Clamp(carried, 0, u.MaxHp);
                log.Append(new EffectEvent(u.Id, $"retained_hp_carried:{u.CurrentHp}/{u.MaxHp}", u.CurrentHp, true));
                if (u.CurrentHp == 0)
                {
                    log.Append(new EffectEvent(u.Id, "retained_hp_zero_but_deployed", 0.0, true));
                }
            }

            _heroBattleIds[heroId] = u.Id.Value; // 记下【本场】id，供战后扣回/下一场查找 ✓
        }
    }

    /// <summary>
    /// 🔴 **`O-83` 的前一半：把【本场结束时的血量】落进跨场台账**（战后、切场景之前调用）。
    ///
    /// ⚠️ 我自查发现的**惰性风险**：`Retained` 原先只被"士气/恢复/回城/扎营"写过，**没有任何一处写战后血量**
    /// ⇒ 若只加"承上"（`ApplyRetainedHpToBattle`）而不加本方法，它读到的**永远是满血哨兵值** ⇒ **改动是惰性的**
    /// （红线 25「动作 ≠ 意义」：看起来接了、其实什么都没发生）⚠️
    ///
    /// 🔴 键 = **本场战斗单位 id**（`u.Id.Value`，与 `_heroBattleIds` 同口径）⇒ 下一场开局才查得到 ✓
    /// ⚠️ 只动 **HP**；士气/虚弱沿用台账现值（本方法**不越权**改它们）✓
    /// </summary>
    public void CaptureBattleEndHp(BattleDirector director, CombatLog log)
    {
        int captured = 0;
        foreach (UnitRuntime u in director.Player.UnitsInSlotOrder())
        {
            string id = u.Id.Value;
            int morale = Retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) cur) ? cur.Morale : MoraleStart; // 🆕 数字外置：缺台账 ⇒ 用 `MoraleStart`（= `tuning.morale.start`，未注入退回契约常量 `RookieMorale`）✓
            bool weak = Retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) cur2) && cur2.Weak;
            int hp = Math.Clamp(u.CurrentHp, 0, u.MaxHp);
            if (Retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) before) && before.Hp == hp)
            {
                continue; // 无变化 ⇒ 不留痕（避免事件流噪声）
            }

            Retained[id] = (hp, morale, weak);
            captured++;
            log.Append(new EffectEvent(u.Id, $"battle_end_hp_captured:{hp}/{u.MaxHp}", hp, true));
        }

        if (captured > 0)
        {
            log.Append(new EffectEvent(default, $"battle_end_hp_captured_count:{captured}", captured, true));
        }
    }

    /// <summary>英雄 id → 本场单位（**按槽位**；两套 id 体系的唯一正确接法）。</summary>
    private UnitRuntime? UnitAtHeroSlot(BattleDirector director, string heroId)
        => _heroSlots.TryGetValue(heroId, out int slot) && slot > 0
            ? director.Player.UnitRuntimeAt(slot)
            : null;

    private void log_note(string message)
    {
        // 内核层不碰 Godot；这里只把说明写进事件流（可审计）
        _campNotes.Add(message);
    }

    /// <summary>本趟营地加成的施加记录（供测试/审计：证明"真的施加了"，不是只记账）。</summary>
    private readonly List<string> _campNotes = new();

    /// <summary>本趟营地加成的施加记录（供测试/审计）。</summary>
    public IReadOnlyList<string> CampNotes => _campNotes;

    /// <summary>
    /// 🔴 **把营地加成从跨场台账里扣回**（每场 `EndBattle` **之后**调用）——
    /// 否则营地士气会经 `Retained` 漏进名册 ⇒ 变成【免费减压】（契约 `#310` ② 明文禁止）⚠️
    /// </summary>
    public void StripCampBonusesFromRetained()
    {
        foreach ((string heroId, int back) in _campMoraleBonus)
        {
            // 🔴 必须用【战斗单位 id】查 `Retained`（两套 id 体系；我第一版用英雄 id ⇒ 查不到 ⇒ 扣回静默失效）
            if (!_heroBattleIds.TryGetValue(heroId, out string? battleId))
            {
                continue;
            }

            if (!Retained.TryGetValue(battleId, out (int Hp, int Morale, bool Weak) cur))
            {
                continue;
            }

            Retained[battleId] = (cur.Hp, Math.Clamp(cur.Morale - back, 0, 100), cur.Weak);
        }
    }

    /// <summary>英雄 id → **本场战斗单位 id**（在 `ApplyCampBonusesToBattle` 时记录；供战后扣回）。</summary>
    private readonly Dictionary<string, string> _heroBattleIds = new(StringComparer.Ordinal);
}
