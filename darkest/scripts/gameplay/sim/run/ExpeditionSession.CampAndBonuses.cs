// ① 来源：从 `ExpeditionSession.CampAndBonuses.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **扎营流程主片**（原 `.CampAndBonuses` 518 行 ⇒ M11 ① 预警面最大者；文件名沿用历史命名）✓
// ② 职责：**扎营流程**（`StartCamp` 开始扎营 · `CanAffordFood`/`ChooseFood` 食物档位 · `EndCamp` 收营 ·
//    `ApplyToSurvivors` 存活者施加 ＋ 相位状态 `MoraleStart`/`AmbushImmune`）✓
// ③ 🔴 依赖主类私有成员/状态：`Retained`/`RosterMaxHp`/`Survivors`/`Food`/`CanCamp`/`RespiteLeft`/`EnterPhase`/`RetainedRecovery`
//    ＋ 同族 part：`_curioDamageBlessingPct`（`.RunBuffs` 声明 · 本片 `EndCamp` 读取并清零）✓
// ④ 只搬家、零行为改动（逐行原样搬运；`AmbushImmune` 的孤儿 `<summary>` 按语义归位到属性正上方 —— 纯注释重排、无文字改动）＋
//    读数对照：拆前 29 通过/0 失败（9 个相关测试类）⇒ 拆后同数 ✓

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionSession : RunSession, IExpeditionSession
{
    /// <summary>开始扎营：**扣 1 份柴火**（不足 → 拒绝且不扣）+ `CampStartedEvent`；
    /// Respite 池 = `respite_base + 存活人数`（满编 12）。</summary>
    public bool StartCamp(CombatLog log, int campIndex, int respiteBase)
    {
        if (!CanCamp)
        {
            return false; // 🔴 相位条件：战斗中/已结算时**不许**扎营（原先只看柴火 ⇒ 等于内核允许战斗中扎营）⚠️
        }

        EnterPhase(FlowPhase.Camp);

        if (!TrySpend(log, "firewood", 1, "camp"))
        {
            EnterPhase(FlowPhase.Walking); // 扣不到柴火 ⇒ 相位**不留下**（避免"扎营了但没扣"这种半态）✓
            return false; // 无柴火 → 不可扎营（E3 验收）
        }

        RespiteLeft = ExpeditionCampMath.RespitePool(respiteBase, Survivors);
        log.Append(new CampStartedEvent(campIndex));
        return true;
    }

    /// <summary>该档位是否**可支付**（口粮足够）——不足时 UI **灰显 + tooltip「口粮不足」**（v0.86 规格）。</summary>
    public bool CanAffordFood(Darkest.Data.TuningCamp camp, string tier)
        => ExpeditionCampMath.FoodRequired(camp.FoodTiers, tier, Survivors) <= Food;

    /// <summary>
    /// 选择食物档位（v0.86 规格修订）：**口粮不足的档位不可选** —— 抛 `InvalidOperationException`，
    /// **不施加任何效果、不扣口粮**；🔴 **禁止"自动退化为 starve 且不扣"**（那是免费午餐，曾导致灵敏度非单调）。
    /// 【Starve】需**主动选择**：需求 0（永远可选），照常受罚（−20% HP / −15 士气），不是"免费躲罚"。
    /// </summary>
    public string ChooseFood(CombatLog log, Darkest.Data.TuningCamp camp, string tier)
    {
        int need = ExpeditionCampMath.FoodRequired(camp.FoodTiers, tier, Survivors);
        if (need > Food)
        {
            throw new InvalidOperationException(
                $"口粮不足：「{tier}」需要 {need} 份、现有 {Food} 份 → **该档位不可选（灰显）**；" +
                "不得自动退化为 starve（v0.86 / E3 规格）。");
        }

        if (need > 0)
        {
            TrySpend(log, "food", need, "camp_food");
        }

        (double hpPct, int moraleDelta) = ExpeditionCampMath.FoodEffect(camp.FoodEffects, tier); // 🔴 效果来自 data ✓
        ApplyToSurvivors(hpPct, moraleDelta);
        log.Append(new CampFoodChosenEvent(tier, need, Survivors));
        return tier;
    }

    /// <summary>🆕 空台账默认士气（`tuning.morale.start`，`init` 注入；未注入 ⇒ 契约常量 `RookieMorale`）✓</summary>
    public int MoraleStart { get; init; } = Darkest.Data.RosterConfig.RookieMorale;

    /// <summary>是否持有"免下一次夜袭"（由扎营技能 `ambush_immunity_once` 授予；**在 `RollAmbush` 里消费**）。</summary>
    public bool AmbushImmune { get; private set; }

    /// <summary>结束扎营（夜袭判定由调用方接 `RollAmbush`；E5）。</summary>
    public void EndCamp(CombatLog log)
    {
        EnterPhase(FlowPhase.Walking); // 🔴 收营 ⇒ 回【走图】相位（内核收口 ⇒ 不依赖调用方记得）✓
        // 🔴 补欠账（契约 `m7_expedition.md:160` ① / `O-67`）：
        //    **`until_next_recovery` = 到下次恢复（**扎营**/回城）** ⇒ **扎营必须清【死门后遗症】**。
        //    实测此前：只有**回城**清（`ReturnToTown`）⇒ 扎营后下一场**仍带着后遗症** ⚠️（红线 21 家族）。
        //    清的是跨场保留集合（`RetainedRecovery`）⇒ 下一场开场就不会再把该 buff 挂上去 ✓（单源）
        int cleared = RetainedRecovery.Count;
        RetainedRecovery.Clear();

        // 🔴 同属"到下次恢复"：**Curio 圣坛祝福也扎营清**（契约"本趟 +N% 伤害（到扎营）"✓）
        if (_curioDamageBlessingPct > 0)
        {
            log.Append(new EffectEvent(default, $"camp_cleared_curio_blessing:{_curioDamageBlessingPct}", 100.0, true));
            _curioDamageBlessingPct = 0;
        }

        log.Append(new CampEndedEvent(0));
        if (cleared > 0)
        {
            log.Append(new EffectEvent(default, $"camp_cleared_until_next_recovery:{cleared}", 100.0, true));
        }
    }

    /// <summary>把 HP%（按整编 MaxHp）与士气增量施加到**存活者**（阵亡者不参与）。</summary>
    private void ApplyToSurvivors(double hpPercent, int moraleDelta)
    {
        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            if (hp <= 0)
            {
                continue;
            }

            int max = RosterMaxHp.TryGetValue(id, out int m) && m > 0 ? m : Math.Max(1, hp);
            int delta = (int)Math.Round(max * hpPercent);
            int newHp = Math.Clamp(hp + delta, 1, max);
            Retained[id] = (newHp, Math.Clamp(morale + moraleDelta, 0, 100), weak);
        }
    }
}
// ⚠️ 以下为 2026-09-20 拆分时的旧 footer（原文保留；依赖已按 §3 上移到头部 ③ 行，本行为历史留痕）——
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_curioDamageBlessingPct x7
