using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// G1（O-55）文本层：事件流 → 中文可读流程。要求「每一步都读得出来」：
/// 谁用了什么技能、对谁造成多少伤害、谁给谁上了 buff、第几回合谁行动、怎么结束。
/// </summary>
[TestClass]
public sealed class LogTextTests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static BattleDirector NewDirector(out CombatLog log)
    {
        log = new CombatLog();
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return d;
    }

    private static string Name(string id) => id switch
    {
        "warrior" or "warrior_2" => "重斧战士",
        "tank" => "坦克",
        "medic" or "medic_2" => "军医",
        "commissar" => "政委",
        "melee_soldier" => "近战小兵",
        "ranged_archer" => "远程射手",
        "caster" => "施法者",
        _ => id,
    };

    private static string SkillName(string id) => id switch
    {
        "warrior_cleave" => "劈砍",
        "warrior_iron_wall" => "铁壁",
        "tank_iron_wall" => "铁壁",
        _ => id,
    };

    [TestMethod]
    public void Render_EveryEventHasChineseLine_NoRawTypeNames()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(21);
        d.StartTurn(rng);
        d.RunFullRound(rng, _ => PlayerDecision.Skill("warrior_cleave", null));

        IReadOnlyList<string> lines = CombatLogText.Render(log.Events, includeRng: true, Name, SkillName);
        Assert.AreEqual(log.Events.Count, lines.Count, "每个事件恰好一行（含随机抽取）");
        Assert.IsFalse(lines.Any(l => l.Contains("Event {") || l.EndsWith("Event")), "不得出现未翻译的裸类型名");
    }

    [TestMethod]
    public void Flow_Readable_SkillUse_Damage_Buff_Turn_End()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(22);
        d.StartTurn(rng);
        d.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", rng, new[] { 1 });
        d.Buffs.Add(UnitId.Of("warrior"), "virtue_focused", source: null);
        d.Buffs.Remove(UnitId.Of("warrior"), "virtue_focused");
        d.EmitBattleEnd("enemy_wiped");

        IReadOnlyList<string> lines = CombatLogText.Render(log.Events, unitName: Name, skillName: SkillName);
        string joined = string.Join("\n", lines);

        Assert.IsTrue(joined.Contains("使用【劈砍】"), $"流程里读得出『谁用了什么技能』：\n{joined}");
        Assert.IsTrue(joined.Contains("对 近战小兵 造成"), $"读得出『对谁造成多少伤害』：\n{joined}");
        Assert.IsTrue(joined.Contains("获得 buff【virtue_focused】"), $"读得出『谁获得 buff』：\n{joined}");
        Assert.IsTrue(joined.Contains("被移除"), "读得出 buff 移除");
        Assert.IsTrue(joined.Contains("战斗结束：胜利"), "读得出结局");
        Assert.IsTrue(joined.Contains("第 1 回合"), "读得出回合号");
    }

    [TestMethod]
    public void RngDraw_HiddenByDefault_ShownOnDemand()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(23);
        d.StartTurn(rng);
        d.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", rng, new[] { 1 });

        IReadOnlyList<string> quiet = CombatLogText.Render(log.Events);
        IReadOnlyList<string> loud = CombatLogText.Render(log.Events, includeRng: true);
        Assert.IsTrue(quiet.Count < loud.Count, "默认折叠随机抽取行");
        Assert.IsTrue(loud.Any(l => l.Contains("随机抽取")), "需要时可展开");
    }
}