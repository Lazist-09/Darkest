using System;

namespace Darkest.Data;

/// <summary>
/// BalanceTable：tuning.json 的只读快照（blueprint §7 数据管线：启动一次加载 → 冻结只读 → 运行期不再变更）。
/// 所有公式常量经此读取（blueprint §7 违规判据：拍死在内核代码里、脱离 tuning.json 的起手值 = 架构违规）。
/// </summary>
public sealed record BalanceTable(TuningConfig Tuning)
{
    public static BalanceTable FromTuning(TuningConfig tuning)
        => tuning is null ? throw new ArgumentNullException(nameof(tuning)) : new BalanceTable(tuning);

    // 常用常量便捷只读口（语义来源见 TuningConfig 对应字段）。
    public int HitClampMin => Tuning.HitClamp.Min;
    public int HitClampMax => Tuning.HitClamp.Max;
    public int DamageFloor => Tuning.DamageFloor;
    public double CritMultiplier => Tuning.CritMultiplier;
    public bool DamageFloatEnabled => Tuning.DamageFloat.Enabled;
    public int MentalReductionDivisor => Tuning.MentalReduction.ResilienceDivisor;
    public int MentalReductionCapPercent => Tuning.MentalReduction.CapPercent;
    public int SpeedFloatPercent => Tuning.SpeedFloat.Percent;
    public bool SpeedFloatEnabled => Tuning.SpeedFloat.Enabled;
    public int WeakDamageMultPercent => (int)(Tuning.Weak.DamageMult * 100);
    public double WeakSpeedMult => Tuning.Weak.SpeedMult;
    public int WeakHpLock => Tuning.Weak.HpLock;
    public int DeathsDoorAfflictionPenaltyPercent => Tuning.DeathsDoor.AfflictionPenaltyPercent;
    public int MoraleMin => Tuning.Morale.Min;
    public int MoraleMax => Tuning.Morale.Max;
    public int MoraleStart => Tuning.Morale.Start;
    public int BleedPerRound => Tuning.Bleed.PerRoundDamage;
    public int BleedRounds => Tuning.Bleed.Rounds;
    public int StatDebuffDelta => Tuning.StatDebuffDefault.Delta;
    public int StatDebuffRounds => Tuning.StatDebuffDefault.Rounds;
}