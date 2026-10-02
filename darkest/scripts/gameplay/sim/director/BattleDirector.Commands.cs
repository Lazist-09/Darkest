using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Survival;

namespace Darkest.Gameplay.Sim.Director;

// ① 来源：从 `BattleDirector.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **玩家命令族**（原 `:179-319` ＋ `:382-403` 逐字节；M11 ① 预警面第二十一件·第一片）✓
// ② 职责：`PlayerUseSkill`（技能桥接 ＋ SP 扣点 ＋ output 记账 ＋ `self_damage_fixed` 收口）／`ApplyFixedSelfDamage`（固定自伤 ⇒ 既有死门链）／
//    `Reinforce`（#181 单按钮两步增援）／`TryFearRefusal`（F2 恐惧 proc）／`SwappedThisRound`（换位护栏读数）／`PassTurn`（S5.2 待命 ⇒ 士气代价）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片 · 代码区计数含声明行）：`_player` x13 ／ `_log` x9 ／ `_balance` x4 ／ `_swappedThisRound` x3 ／ `_enemy` x3 ／
//    `_skills` x1 ／ `_pipeline` x1 ／ `_outputUsersThisRound` x1 ／ `_executor` x1 ／ `_buffs` x1；
//    同类型其它片成员：`TrySpendSupportPoints` x2 ／ `SupportCostSkill` x1 ／ `IsSupportSlotActor` x1 ／ `SupportPoints` x1（片二 `BattleDirector.SupportPoints.cs`）／
//    `ApplyFixedSelfDamage` x2 ／ `Morale` x2 ／ `PlayerUseSkill` x1 ／ `Reinforce` x1 ／ `TryFearRefusal` x1 ／ `SwappedThisRound` x1 ／ `PassTurn` x1 ／ `Buffs` x1 ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 9 条；`Godot` 零引用 —— 构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
public sealed partial class BattleDirector
{
    /// <summary>
    /// 玩家命令：释放技能（SkillExecutor 桥接管线；chosenTargets 供实机单体选一，headless 默认随机选一 #178/#179）。
    /// #211（S0）：**支援位（5/6）技能 −1 SP**；不足 → 拒（返回 false，调用方保持该单位行动，不吞回合）；
    /// **战斗位技能永不因 SP 被拒**（硬提醒①）。
    /// </summary>
    public bool PlayerUseSkill(UnitId actor, string skillId, IRngProvider rng, IReadOnlyList<int>? chosenTargets = null)
    {
        SkillTemplateConfig skill = _skills.Get(skillId);
        // #211（S0）：SP 消耗**按技能声明优先**（`support_point_cost: 0` = 豁免，如【喘息】）；
        // 未声明时沿用"支援位技能扣 cost_skill、战斗位不扣"的默认规则
        int spCost = skill.SupportPointCost ?? (IsSupportSlotActor(actor) ? SupportCostSkill : 0);
        if (spCost > 0 && !TrySpendSupportPoints(spCost, "skill"))
        {
            _log.Append(new SkillRefusedEvent(actor, skillId, "support_points")); // 不足 = 不可用（非失败）
            return false;
        }

        if (skill.Tags.Contains(FuncTag.Output))
        {
            _outputUsersThisRound.Add(actor); // #198：攻击类技能 = tags 含 output（玩家可见的"输出"类）
        }

        _executor.Execute(skill, actor, _player, _enemy, rng, chosenTargets);

        // 🔴 补欠账（`m3_skills_units.md:228/247/312` + `data_schema.md:207`）：
        //    **`self_damage_fixed`（殊死一搏 6 ／ 舍身 8）**：M3 明确"自伤致死死门链【归 M4】"，
        //    但实测**全仓无消费点** ⇒ 这两张牌不会自伤、也不会致死门 ⚠️
        //    契约要求：**固定值、不被护盾吸收、可致死（走既有死门链）** —— 与 `TickBleedAtTurnStart` 同语义
        //    （直接 `CurrentHp -=` ⇒ 不经过 `DamageStep` 的护盾/防御判定 ⇒ 天然"不被护盾吸收"）。
        if (skill.SelfDamageFixed is { } selfDmg && selfDmg > 0)
        {
            ApplyFixedSelfDamage(actor, selfDmg, skillId, rng);
        }

        return true;
    }

    /// <summary>
    /// 固定自伤（`self_damage_fixed`）：**不经护盾、不受防御减免**（直接扣 HP）、
    /// **致死走既有死亡/死门链**（玩家 ⇒ 死门；敌方无死门 ⇒ 直接死）—— 与流血同通道。
    /// </summary>
    private void ApplyFixedSelfDamage(UnitId actor, int dmg, string skillId, IRngProvider rng)
    {
        int? pos = _player.UnitAtPosition(actor);
        UnitRuntime? u = pos is { } p ? _player.UnitRuntimeAt(p) : null;
        if (u is null || u.CurrentHp <= 0)
        {
            return;
        }

        u.CurrentHp -= dmg;
        _log.Append(new DamageEvent(u.Id, dmg, dmg, Crit: false, SegmentIndex: 0, Axis: "self",
            Attacker: u.Id, SkillId: skillId));

        if (u.CurrentHp > 0)
        {
            return;
        }

        if (u.IsPlayer)
        {
            WeakDeathsDoor.EnterWeak(u, _pipeline.Morale, rng, _log, _balance); // 契约：可致死（走死门）
        }
        else
        {
            _enemy.RemoveUnitAt(_enemy.UnitAtPosition(u.Id) ?? -1);
            _log.Append(new DeathEvent(u.Id, IsPlayer: false, Cause: "self_damage"));
        }
    }

    /// <summary>
/// 增援（#181 单按钮两步，推翻旧双按钮 PlayerSwap）：发起者 A（战斗位，消耗本次行动）指定
/// 支援位角色 B（5/6 占用者）与目标战斗位 X（1~4）——X 有人 → B 与 X 上单位【直接互换】；
/// X 空 → B 直接进入并（若需）前移。同回合至多 1 次。
/// </summary>
    public bool Reinforce(UnitId a, int bSlot, int x)
    {
        if (_swappedThisRound)
        {
            return false;
        }

        int aPos = _player.UnitAtPosition(a) ?? -1;
        if (aPos is < 1 or > 4) // 发起者须在战斗位
        {
            return false;
        }

        UnitRuntime? b = _player.UnitRuntimeAt(bSlot);
        if (b is null || !_player.Layout.ExtensionSlots.Contains(bSlot))
        {
            return false;
        }

        if (x is < 1 or > 4 || _player.GetSlot(x) == SlotState.Empty && aPos == x)
        {
            return false;
        }

        // #211（S0）：增援 = 调动一个人上场 → 比放技能重（−2）；不足 → 被拒，且**不得吞掉发起者行动**
        if (!TrySpendSupportPoints(_balance.Tuning.SupportPoints.CostReinforce, "reinforce"))
        {
            return false;
        }

        if (_player.GetSlot(x) == SlotState.Occupied)
        {
            // X 有人：B 与 X 上单位【直接互换】（#41a「X 有人则交换」——原 X 上的人到 B 的原位，途经槽位不动）
            if (!_player.SwapSlots(bSlot, x))
            {
                return false;
            }
        }
        else
        {
            // X 空：B 直接进入（原支援位槽位随之空出）
            _player.RemoveUnitAt(bSlot);
            _player.PlaceUnitAt(x, b);
        }

        _swappedThisRound = true;
        // G0/O-55：SwapEvent 补被调动者（B）与种类（#181 增援）
        _log.Append(new SwapEvent(a, bSlot, x, b.Id, "reinforce"));
        return true;
    }

    /// <summary>F2（#193）恐惧 proc：使用技能前 33% 拒放（技能灰掉、**不消耗行动**、须重选）；勇猛 immune_fear 豁免。</summary>
    public bool TryFearRefusal(UnitId actor, IRngProvider rng)
    {
        bool refused = Darkest.Gameplay.Sim.Buffs.AfflictionProcs.Triggered(_buffs, _balance, actor, "affliction_fear", rng, _log);
        if (refused)
        {
            _log.Append(new SkillRefusedEvent(actor, "-", "affliction_fear")); // G0/O-55
        }

        return refused;
    }

    /// <summary>本回合是否已换位（策略护栏；StartTurn 重置）。</summary>
    public bool SwappedThisRound => _swappedThisRound;

    /// <summary>显式「待命」（S5.2）：放弃本次行动，不消耗 SP、不结算任何效果、不进技能栏。</summary>
    /// <remarks>
    /// 🔴 M7.5 D3（`m7_5_dungeon_layer.md` §D3 / `#266`）：**待命【受士气伤害】** —— "最后手段"必须有代价。
    /// · 数值走 `tuning.expedition.pass_morale_delta`（起手 **−5**；**禁止硬编码**）；
    /// · 士气变更**走既有 `MoraleLedger` 通道**（士气状态的唯一来源）；
    /// · `TurnSkippedEvent(Reason:"passed", MoraleDelta:…)` 只**同时携带**该增量（㉗ 的统计来源），
    ///   不构成第二套状态；若架构要求只留一处，删事件字段即可（已写进窗口备案）。
    /// · 阈值与处置：**待命占支援位行动回合 ≤ 25%**；越界 ⇒ **调 SP / 补"不耗 SP 的事"，不加罚**。
    /// </remarks>
    public void PassTurn(UnitId actor)
    {
        int position = _player.UnitAtPosition(actor) ?? -1;
        UnitRuntime? unit = position >= 0 ? _player.UnitRuntimeAt(position) : null;
        int delta = unit is null ? 0 : -Math.Abs(_balance.Tuning.Expedition.PassMoraleDelta);

        if (unit is not null && delta != 0)
        {
            Morale.Apply(unit, delta, "pass", _log); // 士气唯一来源（写 MoraleEvent）
        }

        _log.Append(new TurnSkippedEvent(actor, "passed", delta));
    }

}
