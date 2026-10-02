using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Director;

// ① 来源：从 `BattleDirector.OvertimeAndRetreat.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **弹性增援与波次间隔**（原 `:332-469` 逐字节；M11 ① 预警面第十九件·第二片）✓
// ② 职责：`EvaluateElasticWindow`（#198 弹性增援/橡胶筋：窗口 K 回合未全力进攻 ⇒ M+1，上限 `M_base + max_bonus`；
//    每次变动写 `ReinforcementElasticEvent`）／`CurrentM`（读数口）／`ApplyReinforcement`（超时增援：补位 ＋ 强化 ＋
//    记 `ReinforcementEvent`）／`DeriveWaveInterval`（#196 波次间隔导出，只读不改状态）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_round` x8 · `_recentNotFull` x5 · `_enemy` x4 · `_lastWaveRound` x4 ·
//    `_balance` x3 · `_elasticBonus` x3 · `_log` x3 · `_player` x2 · `_units` x2 · `_reinforcePool` x2 ·
//    `_reinforcementCount` x2 · `_enemyRoster` x1 · `_outputUsersThisRound` x1；根片私有口：`BuffAllEnemies` x1 ／
//    `IsPlayerId` x1（`DeriveWaveInterval` 取玩家攻击者）✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 7 条；`Godot` 零引用 —— 构建 RC=0 验证；
//    全量重建逐字节 == 基线，见 `reports/p6_m11_split19_20261002.md`）✓
// ─────────────────────────────────────────────────────────────
public sealed partial class BattleDirector
{
    /// <summary>
    /// #198 弹性增援（橡胶筋）：窗口 K 回合内每回合都"未全力进攻"（存活战斗位中未使用 output 技能者 ≥ idle_output_slots）
    /// → M += 1（上限 M_base + max_bonus）；任一回合达标 → M 回落 M_base。每次变动写 ReinforcementElasticEvent。
    /// </summary>
    private void EvaluateElasticWindow()
    {
        TuningElasticSpec? e = _balance.Tuning.OvertimeReinforcement.Elastic;
        if (e is null || !e.Enabled)
        {
            return;
        }

        int aliveCombat = 0;
        int idle = 0;
        for (int slot = 1; slot <= _player.Layout.CombatSlots; slot++)
        {
            UnitRuntime? u = _player.UnitRuntimeAt(slot);
            if (u is null)
            {
                continue;
            }

            aliveCombat++;
            if (!_outputUsersThisRound.Contains(u.Id))
            {
                idle++;
            }
        }

        bool notFull = aliveCombat > 0 && idle >= Math.Max(1, e.IdleOutputSlots);
        _recentNotFull.Enqueue(notFull);
        int window = Math.Max(1, e.KRounds);
        while (_recentNotFull.Count > window)
        {
            _recentNotFull.Dequeue();
        }

        int before = CurrentM();
        bool windowSatisfied = _recentNotFull.Count >= window && _recentNotFull.All(x => x);
        _elasticBonus = windowSatisfied
            ? Math.Min(Math.Max(0, e.MaxBonus), _elasticBonus + 1)
            : 0;

        int after = CurrentM();
        if (after != before)
        {
            _log.Append(new ReinforcementElasticEvent(before, after, windowSatisfied ? "not_full_attack" : "reset"));
        }
    }

    /// <summary>当前波次间隔 M = M_base（#196 导出量）+ 弹性浮动（#198）。</summary>
    private int CurrentM() => DeriveWaveInterval(_balance.Tuning.OvertimeReinforcement) + _elasticBonus;

    /// <summary>T-M5-03 超时增援：第 trigger_round 起每回合开始判定；有空位填（不超 4 位上限），满编上增益（O-20 占位数值）。</summary>
    private void ApplyReinforcement(IRngProvider rng)
    {
        _ = rng; // 切片增援不走随机分布（O-20 裁定）
        TuningOvertimeReinforcement o = _balance.Tuning.OvertimeReinforcement;
        if (_round < o.TriggerRound || IsBattleOver)
        {
            return;
        }

        // #196：M 不是常数，改为**导出量** M = ceil(敌方满编总HP ÷ (D × 0.8))，护栏 M ≥ 3；
        // D = 我方每回合对敌方造成的总伤害（事件流实测）。无输出（D≈0）时回落到 tuning 的 M 下限。
        int interval = CurrentM();
        if (_lastWaveRound == 0)
        {
            if (_round != o.TriggerRound)
            {
                return; // 首波严格在 trigger_round
            }
        }
        else if (_round - _lastWaveRound < interval)
        {
            return; // 未到下一波
        }

        int[] empties = Enumerable.Range(1, _enemy.SlotCount)
            .Where(s => _enemy.GetSlot(s) == SlotState.Empty)
            .ToArray();
        if (empties.Length == 0)
        {
            // 满编分支：给在场全体敌人 +攻/+速（O-20 占位数值；每波叠加一次）
            BuffAllEnemies(_enemy);
            _lastWaveRound = _round;
            return;
        }

        // 有空位分支：**一次性补齐当时全部空位**（按原型轮换，默认起手值；不超 4 位上限）
        foreach (int slot in empties)
        {
            string archetype = _reinforcePool[_reinforcementCount % _reinforcePool.Length];
            _reinforcementCount++;
            var unit = new UnitRuntime(UnitId.Of(archetype), FormationSide.Enemy,
                UnitStatsMapper.From(_units.Get(archetype)));
            _enemy.PlaceUnitAt(slot, unit);
            _log.Append(new ReinforcementEvent("Fill", unit.Id, slot));
        }

        _lastWaveRound = _round;
    }

    /// <summary>
    /// #196：增援波次间隔 M = ceil(敌方满编总HP ÷ (D × 0.8))，护栏 ≥ tuning.wave_interval_rounds（默认 3，即 M 下限）。
    /// D = 我方每回合对敌方造成的总伤害（事件流 DamageEvent 实测均值）；D≈0（无输出）时回落 M 下限。
    /// </summary>
    private int DeriveWaveInterval(TuningOvertimeReinforcement o)
    {
        // P16 门禁：m_value 未回填时以 tuning 下限为占位（复测出实测 D 后回填，见 tasks/m6_fix_pack P3 v3）
        int minM = Math.Max(3, o.MValue ?? o.WaveIntervalRounds);
        int rounds = Math.Max(1, _round - 1);
        int dealt = _log.Events.OfType<DamageEvent>()
            .Where(e => e.Attacker is { } a && IsPlayerId(a))
            .Sum(e => e.Amount);
        double d = (double)dealt / rounds;
        if (d <= 0.01)
        {
            return minM;
        }

        int fullHp = 0;
        foreach (string archetype in _enemyRoster)
        {
            fullHp += _units.Get(archetype).Hp;
        }

        if (fullHp <= 0)
        {
            return minM;
        }

        // 🔴 数字外置：`o.SafetyFactor` 已由 `TuningConfig` 校验为 > 0 ⇒ **不再回落 0.8**
        //    （旧写法 `o.SafetyFactor > 0 ? o.SafetyFactor : 0.8` 是"静默默认"，违反纪律且掩盖数据缺键 ⚠️）
        double safety = o.SafetyFactor;
        return Math.Max(minM, (int)Math.Ceiling(fullHp / (d * safety)));
    }

}
