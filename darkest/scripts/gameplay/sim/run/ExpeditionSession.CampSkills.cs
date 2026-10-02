// ① 来源：从 `ExpeditionSession.CampAndBonuses.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **扎营技能效果接线**（M11 ① 预警面拆分第二片）✓
// ② 职责：`UseCampSkill` 按 `camp_skills.json` 的 `effect` 分支 ＋ `effectNumberOrThrow`（P29 数字外置取值 ·
//    缺失即报错）＋ 本趟台账【写】侧 `_campMoraleBonus`/`_campHpBonusPct` 与 `GrantCampMorale*`/`GrantCampHpPercent`/`BindCampHeroes` ✓
// ③ 🔴 依赖主类私有成员/状态：`RespiteLeft`（扎营点数）· `_allHeroesForCamp`（本片声明）
//    ＋ 同族 part：`AmbushImmune`（`.CampAndBonuses` 声明 · 本片写）· `GrantRunBuff`（`.RunBuffs`）· 台账由 `.BattleCarryOver` 读 ✓
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
    /// <summary>使用扎营技能：**点数不足 → 不可选（返回 false，不扣）**；成功写 `CampSkillUsedEvent`。</summary>
    public bool UseCampSkill(CombatLog log, Darkest.Data.CampSkillConfig skill, UnitId target,
        Darkest.Data.TuningCamp camp)
    {
        // 🔴 数字外置（P29 / 消掉"两处真值"）：数字一律来自 data ⇒ 调用方不必记得传 ✓
        string skillId = skill.Id;
        int cost = skill.Cost;
        string effect = skill.Effect;
        if (cost < 1 || cost > RespiteLeft)
        {
            return false; // 点数不足 → 灰显（E3 验收）
        }

        RespiteLeft -= cost;
        log.Append(new CampSkillUsedEvent(skillId, target, RespiteLeft));

        // 🔴 M7.5 补欠账（`#305`③ / 红线 21）：**扎营技能的【效果】以前只扣点数、不生效** ——
        //    `camp_skills.json` 的 `effect` 字段（如 `ambush_immunity_once` = 轮流守夜 ／ 站岗）
        //    **此前全仓没有消费点** ⇒ 玩家花 3 点买了一无所获（红线 21 最坏形态：**误导玩家**）。
        //    本片先接**语义已存在**的那一项：`ambush_immunity_once` ⇒ **免疫【下一次】夜袭**。
        if (effect == "ambush_immunity_once")
        {
            AmbushImmune = true;
        }

        // 🔴 `m7_expedition.md:160`（三类型之二）：**`next_battle` 类 buff** ——
        //    磨刀（`next_battle_sharpen` 伤害 +25%）／加固甲胄（`next_battle_armor` 物防 +4）
        //    ⇒ 挂成【跨场 buff，剩余 1 场】：下一场开场注入、该场结束即消耗 ✓
        //    （两个 buff 的消费点 `dealt_damage_mult` ／ `phys_def` 早在 `ConsumedEffectNames` 里 ✓）
        if (effect == "grant_buff:next_battle_sharpen")
        {
            GrantRunBuff(target, "next_battle_sharpen", remainingBattles: 1);
        }

        if (effect == "grant_buff:next_battle_armor")
        {
            GrantRunBuff(target, "next_battle_armor", remainingBattles: 1);
        }

        // 🔴 `m7_expedition.md:160`（三类型之二 `battles:N`）：**打气**（士气伤害 −15%）——
        //    契约：**跨场 4 场**、**扎营【不清】它**（`ConsumeRunBuffsAfterBattle` 每场 −1，扎营不碰 ✓）
        if (effect == "morale_damage_minus_15_for_4_battles")
        {
            GrantRunBuff(target, "pep_talk", remainingBattles: camp.PepTalkBattles);
        }

        // 🔴 `#310` ②/①（**本趟台账 `until_run_end`**）：士气类与 HP 类
        if (effect == "morale_plus_8")
        {
            GrantCampMorale(target, effectNumberOrThrow(skill.EffectNumber, "morale_plus_8"));            // 笑谈（单体 +8）
        }

        if (effect == "morale_plus_5_team")
        {
            GrantCampMoraleTeam(effectNumberOrThrow(skill.EffectNumber, "morale_plus_5_team"));                // 埋锅造饭（全队 +5）
        }

        if (effect == "morale_plus_8_team")
        {
            GrantCampMoraleTeam(effectNumberOrThrow(skill.EffectNumber, "morale_plus_8_team"));                // 动员（全队 +8）
        }

        if (effect == "heal_15_percent_and_clear_bleed")
        {
            // ⚠️ 「清流血」部分：流血是**战斗内**状态 ⇒ 营地"清"没有落点（契约 `#310`：归【冗余·阶段二】）
            GrantCampHpPercent(target, effectNumberOrThrow(skill.EffectNumber, "heal_15_percent_and_clear_bleed"));        // 包扎：HP +15% 部分按裁定落地 ✓
        }

        if (effect == "heal_5_percent")
        {
            GrantCampHpPercent(target, effectNumberOrThrow(skill.EffectNumber, "heal_5_percent"));         // 照料
        }

        return true;
    }

    /// <summary>🆕 效果数字取值（**data 驱动**；缺失 ⇒ 立刻报错，**不静默取默认值**）✓</summary>
    private static int effectNumberOrThrow(int? value, string effect) => value
        ?? throw new InvalidOperationException(
            $"camp_skills.json: 效果 \"{effect}\" 需要 effect_number（P29 数字外置）—— 缺失即报错，不静默取默认值。");

    // ------------------------------------------------------------------
    // 🔴 扎营技能的第二批（`#310` ②/① 裁定：**写【本趟台账】`until_run_end`，不写回名册**）：
    //    · 士气类：笑谈 +8（单体）／埋锅造饭 全队 +5／动员 全队 +8  ⇒ **下一场起手士气 +N**
    //    · HP 类：包扎 +15% ／照料 +5%                              ⇒ **本趟剩余场次的开局 HP +N%**
    //    🔴 关键纪律：**这两个台账不得漏进名册** —— 战斗开场施加后，在 `EndBattle` 之后要**扣回**
    //       （否则"营地加士气"会变成【免费减压】，与 M8.0 的减压冲突 —— 契约明文禁止）
    // ------------------------------------------------------------------

    private readonly Dictionary<string, int> _campMoraleBonus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _campHpBonusPct = new(StringComparer.Ordinal);

    /// <summary>本趟的营地士气加成（英雄 → +N；供测试/日志）。</summary>
    public IReadOnlyDictionary<string, int> CampMoraleBonus => _campMoraleBonus;

    /// <summary>本趟的营地 HP 加成（英雄 → +N% MaxHp；供测试/日志）。</summary>
    public IReadOnlyDictionary<string, int> CampHpBonusPct => _campHpBonusPct;

    /// <summary>单体 +N 士气（**本趟台账**；如「笑谈」）。</summary>
    public void GrantCampMorale(UnitId hero, int delta)
    {
        _campMoraleBonus[hero.Value] = _campMoraleBonus.GetValueOrDefault(hero.Value) + delta;
    }

    /// <summary>全队 +N 士气（**本趟台账**；如「埋锅造饭」「动员」）。</summary>
    public void GrantCampMoraleTeam(int delta)
    {
        foreach (Darkest.Data.HeroConfig h in _allHeroesForCamp)
        {
            GrantCampMorale(new UnitId(h.Id), delta);
        }
    }

    /// <summary>单体开局 HP +N%（**本趟台账**；如「包扎 +15%」「照料 +5%」）。</summary>
    public void GrantCampHpPercent(UnitId hero, int percent)
    {
        _campHpBonusPct[hero.Value] = Math.Max(_campHpBonusPct.GetValueOrDefault(hero.Value), percent);
    }

    /// <summary>营地技能的作用对象全集（名册英雄；由组合根注入 —— 名字册与战斗 id 是两套体系）。</summary>
    private IReadOnlyList<Darkest.Data.HeroConfig> _allHeroesForCamp = Array.Empty<Darkest.Data.HeroConfig>();

    /// <summary>注入名册英雄（供"全队"类营地技能枚举目标）。</summary>
    public void BindCampHeroes(IReadOnlyList<Darkest.Data.HeroConfig> heroes) => _allHeroesForCamp = heroes;
}
