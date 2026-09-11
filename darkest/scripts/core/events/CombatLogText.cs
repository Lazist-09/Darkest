using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Darkest.Core.Contracts;

namespace Darkest.Core.Events;

/// <summary>
/// G1（O-55）事件 → 中文可读文本层：日志的每个事件都能翻成一句人话
/// （谁对谁用了什么技能、造成多少伤害、谁给谁上了什么 buff、谁在第几回合行动、怎么结束的）。
/// 只读事件流，不产生抽取、不改变 Sequence（blueprint 只读红线）。
/// 名称解析走委托（内核不反向依赖数据层）：调用方（UI/测试）注入 units.json / skills.json 的中文名。
/// </summary>
public static class CombatLogText
{
    /// <summary>把整条事件流翻成文本行（RngDraw 默认折叠，便于人读流程）。</summary>
    public static IReadOnlyList<string> Render(IEnumerable<BattleEvent> events, bool includeRng = false,
        Func<string, string>? unitName = null, Func<string, string>? skillName = null, Func<string, string>? buffName = null)
    {
        var lines = new List<string>();
        foreach (BattleEvent e in events)
        {
            if (e is RngDraw && !includeRng)
            {
                continue;
            }

            lines.Add(Line(e, unitName, skillName, buffName));
        }

        return lines;
    }

    /// <summary>单条事件 → 一行中文。</summary>
    public static string Line(BattleEvent e, Func<string, string>? unitName = null,
        Func<string, string>? skillName = null, Func<string, string>? buffName = null)
    {
        string U(UnitId? id) => id is { } u ? (unitName?.Invoke(u.Value) ?? u.Value) : "?";
        string S(string? id) => id is null ? "?" : (skillName?.Invoke(id) ?? id);
        string B(string id) => buffName?.Invoke(id) ?? id;
        string Slots(IReadOnlyList<int>? s) => s is null || s.Count == 0 ? "—" : string.Join("、", s) + " 位";
        string R(BattleEvent ev) => ev.Round > 0 ? $"第 {ev.Round} 回合 " : string.Empty;

        return e switch
        {
            RoundStartEvent => $"— 第 {((RoundStartEvent)e).Round} 回合开始 —",
            TurnStartEvent t => $"{R(t)}{U(t.Actor)} 行动（{t.Slot} 位，速度 {t.EffectiveSpeed:F2}）",
            TurnSkippedEvent s => $"{R(s)}{U(s.Actor)} 跳过行动（{Reason(s.Reason)}）",
            SkillUseEvent k => $"{R(k)}{U(k.Actor)} 使用【{S(k.SkillId)}】→ 目标 {Slots(k.TargetSlots)}",
            SkillRefusedEvent r => $"{R(r)}{U(r.Actor)} 的技能被拒（{Reason(r.Reason)}）",
            HitEvent h => $"{R(h)}命中判定 攻方{U(h.Attacker)} → 守方{U(h.Target)}：{(h.Hit ? "命中" : "未命中")}（{h.HitRate}%）",
            CritEvent c => $"{R(c)}{U(c.Attacker)} → {U(c.Target)}：{(c.Crit ? "暴击" : "非暴击")}",
            DamageEvent d => $"{R(d)}{U(d.Attacker)} 对 {U(d.Target)} 造成 {d.Amount} 点{ (d.Crit ? "暴击" : "") }伤害（{Axis(d.Axis)}，第 {d.SegmentIndex + 1} 段）",
            HealEvent he => $"{R(he)}{U(he.Source)} 治疗 {U(he.Target)} {he.Amount} 点",
            EffectEvent ef => $"{R(ef)}{U(ef.Source)} 对 {U(ef.Target)} 施加效果 {ef.EffectType}（{ef.ActualChance:F0}%，{(ef.Triggered ? "生效" : "未生效")}）",
            StatModEvent sm => $"{R(sm)}{U(sm.Target)} 属性变化：{sm.Stat} {(sm.Delta >= 0 ? "+" : "")}{sm.Delta}（{sm.DurationRounds} 回合）",
            BuffAppliedEvent ba => $"{R(ba)}{U(ba.Source)} 使 {U(ba.Target)} 获得 buff【{B(ba.BuffId)}】（{ba.DurationRounds} 回合 / {ba.Stacks} 层）",
            BuffRemovedEvent br => $"{R(br)}{U(br.Target)} 的 buff【{B(br.BuffId)}】被移除（{Reason(br.Reason)}）",
            MoraleEvent m => $"{R(m)}{U(m.Unit)} 士气 {(m.Delta >= 0 ? "+" : "")}{m.Delta} → {m.NewValue}（{m.Source}）",
            MoraleEmberEvent me => $"{R(me)}{U(me.Unit)} 士气余烬 {(me.Kind == "enter" ? "触发" : "解除")}",
            DeathDoorEvent dd => $"{R(dd)}死门判定 {U(dd.Unit)}：{dd.SurvivePercent}% 存活率，掷 {dd.Roll:F0} → {(dd.Survived ? "生还" : "倒下")}",
            DeathEvent de => $"{R(de)}{U(de.Unit)} 阵亡（{(de.IsPlayer ? "我方" : "敌方")}，{Reason(de.Cause)}）",
            DisplaceEvent dp => $"{R(dp)}{U(dp.Mover)} 位移 {dp.FromPos} → {dp.ToPos} 位（{(dp.PassedResist ? "成功" : "被抵抗")}）",
            SwapEvent sw => $"{R(sw)}{U(sw.Actor)} 换位 {sw.FromPos} ↔ {sw.ToPos}（{U(sw.MovedUnit)}，{(sw.Kind == "reinforce" ? "增援" : "移动")}）",
            ReinforcementEvent rf => $"{R(rf)}敌方{(rf.Kind == "Fill" ? "增援" : "强化")}：{U(rf.Unit)} → {rf.Slot?.ToString(CultureInfo.InvariantCulture) ?? "全场"}",
            ReinforcementElasticEvent re => $"{R(re)}增援间隔 M：{re.MFrom} → {re.MTo}（{Reason(re.Reason)}）",
            EnemyDecisionEvent ed => $"{R(ed)}敌方 {U(ed.Actor)} 决策：{S(ed.SkillId)} → {Slots(ed.TargetSlots)}",
            RetreatEvent rt => $"{R(rt)}撤退判定：{(rt.Success ? "成功" : "失败")}（{rt.Rate:F0}%）",
            BattleEndEvent be => $"— 战斗结束：{Outcome(be.Outcome)}（{Reason(be.Reason)}）—",
            SupportPointEvent sp => $"{R(sp)}支援点 {(sp.Delta >= 0 ? "+" : "")}{sp.Delta} → {sp.NewValue}（{Reason(sp.Reason)}）",
            RngDraw rd => $"{R(rd)}随机抽取 #{rd.DrawCount} = {rd.Value:F2}",
            _ => $"{R(e)}{e.GetType().Name}",
        };
    }

    private static string Axis(string axis) => axis switch
    {
        "mental" => "精神",
        "physical" => "物理",
        _ => axis,
    };

    private static string Outcome(string outcome) => outcome switch
    {
        "Victory" => "胜利",
        "Defeat" => "失败",
        _ => outcome,
    };

    private static string Reason(string reason) => reason switch
    {
        "stunned" => "眩晕",
        "bound" => "捆缚",
        "no_usable_skill" => "无可用技能",
        "no_target" => "无可选目标",
        "cooldown" => "冷却中",
        "per_battle" => "每战已用尽",
        "affliction_fear" => "恐惧发作",
        "expired" => "到期",
        "dispelled" => "被驱散",
        "consumed" => "已消耗",
        "morale_reset" => "士气重置",
        "death" => "阵亡",
        "not_full_attack" => "未全力进攻",
        "reset" => "全力进攻（回落）",
        "enemy_wiped" => "敌方全灭",
        "player_wiped" => "我方全灭",
        "table_pick" => "技能表选取",
        "skill" => "支援位技能",
        "reinforce" => "增援",
        "regen" => "回合恢复",
        "rejected" => "点数不足（被拒）",
        "passed" => "待命",
        "unknown" => "未知",
        _ => reason,
    };
}