// 🔴 从 ExpeditionSession.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3）——只搬家、零行为改动 ✓
//    本文件 = 存续与生存（Retained / Roster / 士气 / 饥饿）

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Morale;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionSession
{
    public IReadOnlyList<(string Id, int Hp, int MaxHp, int Morale)> Roster()
    {
        var list = new List<(string, int, int, int)>();
        foreach ((string id, (int hp, int morale, bool weak)) in Retained)
        {
            int max = RosterMaxHp.TryGetValue(id, out int m) && m > 0 ? m : Math.Max(1, hp);
            list.Add((id, hp > 0 ? Math.Min(hp, max) : 0, max, morale));
        }

        list.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        return list;
    }

    /// <summary>
    /// **跨趟携带**（#245 的必然结果）：上一趟结束（回城）后开新一趟时——
    /// **HP 完全恢复**（记 `int.MaxValue`，下场按 MaxHp 钳制）、**士气保留**、虚弱与死门后遗症清除、阵容满编。
    /// 这是"**跨趟累积**"的唯一载体；主循环（回城 → 再出发）必须调用它。
    /// </summary>
    public void CarryOverFrom(ExpeditionSession previous)
    {
        foreach ((string id, int hp, int max, int morale) in previous.Roster())
        {
            _ = hp;
            _ = max;
            Retained[id] = (int.MaxValue, morale, false); // 回城：HP 全满、士气不回（#245）
            RetainedRecovery.Remove(id);
        }
    }

    private int AverageRetainedMorale()
        => Retained.Count == 0 ? MoraleStart : (int)Math.Round(Retained.Values.Average(v => v.Morale));

    // ------------------------------------------------------------------
    // 🔴🔴 D-7 · 探索层 act-out（DD ⑩ / `dungeon_layer_design.md §F5`）
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 `D-7`：**队内最低士气**（只算**存活者**）；无人可算（`Retained` 空）⇒ `null`。
    ///
    /// <para>⚠️ **为什么用"最低"而不是"平均"**：折磨是**个体**状态（DD ⑩ 是"**某个**英雄被恐惧攫住"），
    /// 只要**任何一名**队员带折磨，那次 Curio / 那顿饭就可能被拒 ⇒ 判据必须取**最低者**，不是平均
    /// （平均会把"一个崩溃 + 五个满状态"抹平成 42 ⇒ 判不出个体折磨）✓</para>
    ///
    /// <para>🔴 **为什么不在这里判"是否带折磨"**：阈值是**数据**（`tuning.dungeon_layer.exploration`）
    /// ⇒ 会话**只给读数**，判定归 `ExplorationActOut.IsAfflicted`（纯函数、可单测、口径唯一）✓</para>
    ///
    /// <para>⚠️ **已知的有界缺口（与 `D-5`/`D-4` 同源，诚实标注）**：`Retained` 只在 `EndBattle` 落账 ⇒
    /// **首战之前**返回 `null` ⇒ 门禁**不生效**（不掷、不写）。窗口 = "开局到首战之间"，
    /// 与 `HungerCanApply` / `TrapCanApply` **同因同果** ✓</para>
    /// </summary>
    public int? LowestSurvivorMorale()
    {
        int? min = null;
        foreach ((int hp, int morale, bool _) in Retained.Values)
        {
            if (hp <= 0)
            {
                continue; // 阵亡者不参与（与 `ResolveHunger` / `ResolveTrapByResist` 同口径）✓
            }

            if (min is null || morale < min.Value)
            {
                min = morale;
            }
        }

        return min;
    }

    /// <summary>🔴 `D-7`：**探索层门禁能否真的生效**（= 名册台账已建立，与 `HungerCanApply` 同因同果）✓</summary>
    public bool ActOutCanApply => LowestSurvivorMorale() is not null;

    // ------------------------------------------------------------------
    // E3 · 扎营流程（最小版内核）：许可（柴火 1）→ 食物档位 → Respite 分配 → 结束
    // 说明：`next_battle` 类效果（磨刀/加固甲胄）由 camp_skills 声明为 **grant_buff**，
    //       其生效与到期**归 buff 台账**，本会话**不另存**"下一场加成"（单源）。
    // ------------------------------------------------------------------

    /// <summary>当前存活人数（口粮缩放 / Respite / 事件士气的作用面）。</summary>
    public int Survivors => Retained.Values.Count(v => v.Hp > 0);

    // ------------------------------------------------------------------
    // 🔴🔴 D-5 · 饥饿（DD wiki "Hunger"）—— 触发后的"吃 / 不吃"结算
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 `D-5`：**吃得起这一顿吗**（每人 <see cref="HungerConfig.FoodPerHero"/> 口粮）。
    /// **DD 铁律：不能只喂一部分人** —— 凑不齐 ⇒ **只能挨饿，且一口粮都不消耗** ✓
    /// </summary>
    public bool CanEatForHunger(HungerConfig config) => Food >= HungerSpawner.FoodRequired(config, Survivors);

    /// <summary>
    /// 🔴🔴 `D-5` **结算一次饥饿**（触发之后**必须**二选一，不能拖/跳）。
    ///
    /// <para>· **吃**（`eat: true` 且**口粮够**）：扣 `Survivors × FoodPerHero` 口粮，全队回
    ///   `EatHealPercent`% 最大 HP（**每人按自己 MaxHp 算** —— DD 原文 "heal every hero for 5% of their maximum HP"）✓</para>
    /// <para>· **不吃**（或口粮不够）：全队掉 `StarveHpPercent`% 最大 HP + 涨 `StarveMorale` 压力，
    ///   🔴 **口粮一点不扣**（DD 原文："the entire party is forced to starve, and **no Food will be eaten**,
    ///   regardless of any Food you may have below the threshold"）✓</para>
    /// <para>· **阵亡者**（`Hp ≤ 0`）既不吃也不挨饿（不再二次伤害）✓</para>
    /// </summary>
    /// <returns>实际发生的是"吃了"还是"挨饿"（口粮不够时即使用户选吃也**必然**是挨饿）✓</returns>
    public string ResolveHunger(CombatLog log, HungerConfig config, bool eat,
        TuningExplorationActOut? actOut = null)
    {
        // 🔴🔴 `D-7`（2026-09-20）：**探索层 act-out —— 拒绝进食**（DD ⑩ / `dungeon_layer_design.md §F5 ③`）。
        //
        //   带**折磨**的队员会**拒绝进食** ⇒ 掷中即**强制挨饿**（推翻玩家选的"吃"）。
        //
        //   🔴 **为什么挂在入口（而不是别处）**：
        //     · 它是"**这次吃饭的结果**"，与 `CanEatForHunger` 同层 ⇒ 必须在这里改写 `eat`，
        //       这样下游（扣口粮 / 回血 / 掉血）**逐字不用改** ✓
        //     · 掷中时 **`ate` 必为 `false`** ⇒ `TrySpend` 不会被调用 ⇒ 口粮**一点不扣**
        //       （与"口粮不够 ⇒ 一口都不消耗"的既有口径**自动一致**）✓
        //
        //   🔴 **配置从参数进**（`actOut`）—— 与 `config`（`HungerConfig`）**同一手法**：
        //      会话不持 `TuningConfig`（那是流程层的），故按需传入；`null` ⇒ 门禁关闭 ✓
        //   🔴 **未配置 ⇒ 一次都不判**（`RollEatRefuse` 内部直接返回 `None`，不掷不写）✓
        bool eatRefused = false;
        if (actOut is not null && LowestSurvivorMorale() is { } lowestMorale)
        {            // 🔴 复用**已绑定**的随机流（`BindTrapRng`）—— 与 `ResolveTrapByResist` 同一条流（可复现）；
            //    未绑定 ⇒ **明确抛**（不静默跳过整个门禁，那是"机制静默失效"家族）⚠️
            IRngProvider actOutRng = _trapRng ?? throw new InvalidOperationException(
                "`ResolveHunger` 判 D-7 拒绝进食需要 `IRngProvider` ⇒ 本会话未注入（`BindTrapRng`）✓");
            ActOutRollResult refused = ExplorationActOut.RollEatRefuse(log, actOutRng, actOut, lowestMorale);
            if (refused.Refused)
            {
                eatRefused = true;
                ActOutEatRefuseCount++;
                log.Append(new EffectEvent(default, "act_out_eat_refuse", 100.0, Triggered: true));
                if (eat)
                {
                    eat = false; // 🔴 **强制挨饿**：推翻玩家的"吃" ✓
                }
            }
        }

        _ = eatRefused; // 留档：它已通过 `act_out_eat_refuse` 事件留痕（不静默）✓

        bool canEat = CanEatForHunger(config);
        bool ate = eat && canEat;

        if (ate)
        {
            int need = HungerSpawner.FoodRequired(config, Survivors);
            TrySpend(log, "food", need, "hunger");
        }

        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            if (hp <= 0)
            {
                continue; // 阵亡者不受饥饿影响 ✓
            }

            // 🔴 **血量上限的口径 = 全仓唯一真值**（`#325` D6「两份真值」禁令）：
            //    `RosterMaxHp` 由 `RunSession.BeginBattle` 在**第一场**从 `director.Player` 的
            //    `UnitRuntime.MaxHp` 记录 —— 那正是**等级/特质/疾病投影之后**的真值 ✓
            //    🔴 **绝不可**另造第二份公式（那是 `#325` D6 明令禁止的），故与 `ApplyToSurvivors`
            //    （扎营 HP% 的同族结算）**逐字同式**：取不到就回落到"当前 HP"（钳制下限 1）✓
            int max = RosterMaxHp.TryGetValue(id, out int m) && m > 0 ? m : Math.Max(1, hp);

            if (ate)
            {
                int heal = HungerSpawner.EatHealFor(config, max);
                Retained[id] = (Math.Min(max, hp + heal), morale, weak);
            }
            else
            {
                int dmg = HungerSpawner.StarveDamageFor(config, max);
                int newHp = Math.Max(0, hp - dmg);
                Retained[id] = (newHp, Math.Clamp(morale - config.StarveMorale, 0, 100), weak);
            }
        }

        log.Append(new EffectEvent(default, ate ? "hunger_eat" : "hunger_starve", 100.0, true));
        return ate ? "eat" : "starve";
    }

    /// <summary>
    /// 🔴 `D-5`：**饥饿能否真的生效**（= 名册台账已建立）。
    ///
    /// <para>⚠️ **已知的有界缺口（诚实标注，不粉饰）**：`Retained` 只在 `EndBattle`（战后）落账 ⇒
    /// **第一场战斗之前**走走廊若掷中饥饿，此时没有任何人可结算 ⇒ 本方法返回 `false`，
    /// 表现层**不应弹"吃/不吃"**（弹了就是骗玩家：选哪个都没效果）✓</para>
    /// <para>· 实际暴露窗口极窄：`hunger.buffer_at_start = 2` 已保护**开局两条走廊**，而首场战斗
    ///   通常就在起点房间或紧邻处触发 ⇒ 真正的暴露 = "开局 2 条走廊之后、首战之前"这一段 ✓</para>
    /// <para>🔴 **为什么不顺手补上**：唯一真值 `RosterMaxHp` 由 `BeginBattle` 从**投影后的** `UnitRuntime.MaxHp`
    ///   记录；在首战之前要拿到它就得**自己跑一遍投影** ⇒ 那是 `#325` D6 明令禁止的「**两份真值**」⚠️
    ///   ⇒ 宁可留一个**有界且有痕**的缺口，也不制造第二份血量公式 ✓</para>
    /// </summary>
    public bool HungerCanApply => Retained.Count > 0;

    // ------------------------------------------------------------------
    // 🔴🔴 D-4（2026-09-20）· 陷阱结算（DD wiki `Trap`）—— 与 `ResolveHunger` **同族同式**
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴🔴 `D-4` **结算一次陷阱**（踏中 / 拆除 / 已消费 三条路径由 `TrapResolver` 判，本方法只管**记账**）。
    ///
    /// <para>**DD 原文口径**（`darkestdungeon.fandom.com/wiki/Trap`）：</para>
    /// <para>· **踏中** ⇒ **随机一名**存活队员掉 `hp_percent`% **其**最大 HP 的血 + 压力 **恒定** `stress_damage` ✓</para>
    /// <para>· **拆除成功** ⇒ **完全无害**（"renders it harmless"）+ **回** `disarm_stress_heal` 压力 ✓</para>
    /// <para>· **已消费** ⇒ 什么都不做（**不掷、不写、不改任何数**）✓</para>
    ///
    /// <para>🔴 **为什么"随机一人"也要写 `RngDraw`**：DD 原文的伤害是打在"**a random party member**"身上 ⇒
    /// 选人本身是一次**真随机**（且**展示值必须 == 消费值**，纪律 V）⇒ 必写日志 ⚠️</para>
    /// <para>🔴 **血量上限的唯一真值** = `RosterMaxHp`（由 `BeginBattle` 从**投影后**的 `UnitRuntime.MaxHp` 记录）
    /// —— 与 `ResolveHunger` / `ApplyToSurvivors` **逐字同式**，**绝不另造第二份公式**（`#325` D6 禁令）✓</para>
    /// </summary>
    /// <returns>实际发生的路径（`triggered` / `disarmed` / `avoided` / `consumed` / `triggered_no_roster`）✓</returns>
    /// <param name="resistFor">🔴 **按【英雄 id】查陷阱抗性**的口（`null` ⇒ 全体按 0 算的退化解）。
    /// 选人**在本方法内**做（因为"谁被踩中"要等掷骰完才知道）⇒ 故传入的是**查询口**而不是某个值 ✓</param>
}
