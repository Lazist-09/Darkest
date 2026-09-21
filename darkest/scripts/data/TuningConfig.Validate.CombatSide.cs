// 🔴 从 TuningConfig.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.2 的 **②.Combat**）
//    ✅ **按域重切已完成**（2026-09-21）：原先混进来的 `dungeon_layer` / `retreat_formula` / `Light` 等
//       **256 行**已搬到 `.Validate.ExpeditionSide` ✓ ⇒ 本片现在**只剩战斗域**：
//       物理/精神减伤 · 命中钳制 · 暴击 · 士气 · 虚弱 · 死门 · 速度浮动 等 ✓
//    🔴 只搬家、零行为：`Validate` 的调用顺序**未变**（先 Expedition 后 Combat ✓）⇒ 全量 **798/798** 未变 ✓
//    🔴 复核手法：`python tools/dsh/audit_split_integrity.py --baseline <拆分前快照> --parts ...` ✓

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

public sealed partial record TuningConfig
{
    private static void ValidateCombat(TuningConfig t, string rawJson)
    {
        if (t.PhysicalMitigation is null || t.PhysicalMitigation.Divisor <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: physical_mitigation.divisor 缺失或 ≤ 0 —— 物理减免除数**必须**来自数据（数字外置纪律）。");
        }

        // 🔴 眩晕累积（数字外置，P29）：成功施加后累积 +N，抗性上限 100 ⇒ 要求 0..100 ✓
        if (t.Stun.BuildupOnApply is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: stun.buildup_on_apply = {t.Stun.BuildupOnApply} 越界（须 0..100）" +
                " —— 它**必须**来自数据（原先硬写在 `EffectsStep` 里是 +50）✓");
        }

        // 🔴 目睹暴击牵连概率（数字外置，P29）：0..100（原先硬写在 `DamagePipeline` 里是 50.0）✓
        if (t.Morale.WitnessCritShockChancePercent is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: morale.witness_crit_shock_chance_percent = {t.Morale.WitnessCritShockChancePercent} 越界（须 0..100）。");
        }

        // 🔴 连续未命中补偿（数字外置，P29）：每多一次未命中 +N 隐藏命中，0..100 ✓
        if (t.ConsecutiveMiss is null || t.ConsecutiveMiss.HitBonusPerMiss is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: consecutive_miss.hit_bonus_per_miss 缺失或越界（须 0..100）" +
                " —— 它**必须**来自数据（原先硬写在 `HitStep` 里是 4）✓");
        }

        // 🔴 暴击治疗概率（数字外置，P29）：两个都必须在 0..100（原先硬写在 `SkillExecutor` 里是 12 / 5）✓
        if (t.HealCrit is null
            || t.HealCrit.SingleTargetPercent is < 0 or > 100
            || t.HealCrit.MultiTargetPercent is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"{ResPath}: heal_crit 缺失或越界（single/multi 须 0..100）—— 它们**必须**来自数据。");
        }

        if (t.DamageFloor < 1)
        {
            throw new InvalidDataException($"{ResPath}: damage_floor 必须 ≥ 1（combat_math §2.3）。");
        }

        if (t.HitClamp.Min >= t.HitClamp.Max || t.HitClamp.Min < 0 || t.HitClamp.Max > 100)
        {
            throw new InvalidDataException($"{ResPath}: hit_clamp 取值非法（应 [55,100]）。");
        }

        if (t.CritMultiplier <= 0)
        {
            throw new InvalidDataException($"{ResPath}: crit_multiplier 必须 > 0。");
        }

        if (t.SpeedFloat.Percent is < 0 or > 100)
        {
            throw new InvalidDataException($"{ResPath}: speed_float.percent 越界 [0,100]。");
        }

    }
}

