using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// ① 从 `HamletRoot.HeroDetail.cs` 拆出（用户红线 28；2026-10-02 第十件）✓
/// ② 本文件 = **城池 · 角色详情【每次打开的读数刷新】**：左栏（立绘位/名字/原型/等级/士气/特质/疾病）· 怪癖行（现读 + 悬停全文）·
///    属性 6 项 + 5 项抗性折叠 · 战斗技能图标行与文字行 · 饰品真读数 · 扎营技能行（只列该原型 + 只列已接线）·
///    收尾：`_detailPanel.Visible = true` + 一行读数打印 ✓
/// ③ 🔴 依赖主类私有成员：**读** `_rosterCfgForDetail`(`HamletRoot.cs:52`) ／ `_skillsCfg`(`:53`) ／ `_unitsCfg`(`:54`) ／ `_campSkills`(`:55`) ／ `_quirksCfg`(`:58`)；
///    **写** `_detailPanel`(`:86`) ／ `_detailLeft`(`:87`) ／ `_detailRight`(`:88`) ／ `_detailCampSkills`(`:89`) ／ `_detailQuirks`(`:90`) ／ `_detailSkills`(`:91`)；
///    **调** `DescribeTrinketSlots`(`HeroDetail.Trinkets.cs:251`) ／ `RefreshTrinketSlots`(`:90`) ／ `WithPlaceholderAlpha`(`HamletRoot.RosterServices.cs:74`) ／ 同族 Quirks 三件 ✓
/// ④ **只搬家、零行为改动**（唯一例外：原 `:414` `_detailPanel.Visible` ⇒ `_detailPanel!.Visible` + 注释 —— 拆方法后编译器不再跨方法流分析，`!` 只标注不变量、**运行时零差异**）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 每次打开都**现读**刷新全部读数（红线 26：屏上显示 = 真状态同一份）—— 含收尾「置可见 + 读数打印」✓
    /// 🔴 特质/疾病/怪癖计数等局部变量只在本方法内使用 ⇒ 收尾打印随之内聚（不新造返回值/字段）✓
    /// </summary>
    private void RefreshHeroDetail(HeroConfig hero, Roster roster, string heroId)
    {
        // ① 左栏：立绘占位 + 名字/原型/等级/士气（数值 + 条）
        int morale = roster.MoraleOf(heroId);
        string moraleBar = new string('█', Math.Clamp(morale / 10, 0, 10)).PadRight(10, '·');
        var left = new System.Text.StringBuilder();
        left.AppendLine($"【立绘位】（占位块）　原型：{hero.Archetype}");
        left.AppendLine($"名字：{hero.Name}");
        left.AppendLine($"原型：{hero.Archetype}　等级：Lv{hero.Level}");
        left.AppendLine($"士气：{morale} / 100　{moraleBar}");
        left.AppendLine();

        // 🔴 ② 特质：**已固化 / 已清除 标出**（与 `Roster` 状态一致 —— 卡 §2.3 的验收项）
        left.AppendLine("【特质】");
        IReadOnlyList<HeroTraitConfig> currentTraits = roster.TraitsOf(heroId);
        foreach (HeroTraitConfig t in currentTraits)
        {
            bool locked = roster.IsTraitLocked(heroId, t.Id);
            string sign = t.DamagePct != 0 ? $"伤害{t.DamagePct:+#;-#;0}%" : $"士气伤害{t.MoraleDamagePct:+#;-#;0}%";
            left.AppendLine($"　· {t.Id}（{sign}）{(locked ? "🔒 已固化" : string.Empty)}");
        }

        IReadOnlyCollection<string> diseases = roster.DiseasesOf(heroId);
        left.AppendLine();
        left.AppendLine($"【疾病】{(diseases.Count == 0 ? "无" : string.Join("、", diseases))}");
        _detailLeft!.Text = left.ToString();

        // 🆕 2026-10-01 M5u：**怪癖行**（`dd1_workstreams.md §3` 验收：正/负/疾病分类 · 互斥提示 · 显示与数据一致 · 悬停出完整信息）
        //    🔴 显示与数据一致：逐条**从 `roster.QuirksOf` 现读**（红线 26）—— 不写死任何 id ✓
        IReadOnlyCollection<string> quirks = roster.QuirksOf(heroId);
        _detailQuirks!.Text = DescribeQuirks(_quirksCfg, quirks);
        _detailQuirks.TooltipText = DescribeQuirksDetail(_quirksCfg, quirks);
        GD.Print($"[HamletRoot] 怪癖读数：{_detailQuirks.Text.Replace('\n', '｜')}" +
                 $"（悬停全文 {_detailQuirks.TooltipText.Length} 字 · 库={(_quirksCfg is null ? "未加载" : $"{_quirksCfg.Quirks.Count} 条")}）✓");

        // ③ 属性 6 项常显 + 5 项抗性折叠一行（**读按原型的单位数据**，不是写死）
        UnitConfig? unit = _unitsCfg?.Units.FirstOrDefault(u => u.Id == hero.Archetype);
        var right = new System.Text.StringBuilder();
        if (unit is null)
        {
            right.AppendLine($"【属性】🔴 找不到原型单位数据（{hero.Archetype}）");
        }
        else
        {
            right.AppendLine("【属性】");
            right.AppendLine($"　攻击 {unit.Attack}　物防 {unit.Prot}　速度 {unit.Speed}　" +
                             $"闪避 {unit.Dodge}　暴击 {unit.Crit}　韧性 {unit.Resilience}");
            right.AppendLine($"　（5 项抗性折叠）眩晕 {unit.StunResist}　流血 {unit.BleedResist}　" +
                             $"减益 {unit.StatDebuffResist}　位移 {unit.DisplaceResist}　死门 {unit.DeathsDoorResist}");
        }
        right.AppendLine();

        // 🔴 P4（用户参考图④）：**战斗技能改成【图标 + 悬停讲解】** —— 图标行在这里填充，
        //    文字区**只留一行提示**（不再逐条列大段说明 ⇒ "简洁、文字不要太多"）✓
        if (_detailSkills is not null)
        {
            foreach (Node c in _detailSkills.GetChildren().ToArray())
            {
                _detailSkills.RemoveChild(c);
                c.QueueFree();
            }

            if (_skillsCfg is not null)
            {
                foreach (SkillTemplateConfig s in _skillsCfg.Skills.Where(s => s.OwnerUnit == hero.Archetype).Take(5))
                {
                    // 图标 = 立绘留框同款（1px 边框 + 色块占位）；**讲解走 TooltipText**（悬停即可读全）✓
                    var slot = new PanelContainer
                    {
                        Name = $"SkillIcon_{s.Id}",
                        CustomMinimumSize = new Vector2(40, 40),
                        TooltipText = $"{s.Name}（{s.Id}）\n命中修正 {s.HitMod:+#;-#;0}　效果 {s.Effects.Count} 条",
                    };
                    slot.AddChild(new ColorRect
                    {
                        Name = "SkillIconPlaceholder",
                        Color = WithPlaceholderAlpha(Darkest.UI.DdTheme.ArchetypeColor(hero.Archetype, isPlayer: true)),   // 🔴 规则②：α 取调色板
                    });
                    _detailSkills.AddChild(slot);
                }
            }
        }

        // ④ 战斗技能 5 个（该原型；悬停 tooltip 的文本直接展开，避免依赖 tooltip 机制）
        right.AppendLine("【战斗技能】（见上方图标；悬停读讲解）");
        if (_skillsCfg is not null)
        {
            foreach (SkillTemplateConfig s in _skillsCfg.Skills.Where(s => s.OwnerUnit == hero.Archetype).Take(5))
            {
                string dmg = s.Damage is null ? "非伤害" : "有伤害";
                right.AppendLine($"　· {s.Name}（{s.Id}）{dmg}　命中修正 {s.HitMod}　暴击修正 {s.CritMod}");
            }
        }

        right.AppendLine();
        // 🔴 2026-10-02 M4u：饰品行换成**真读数**（此前是「装备 2 格 · 未实现」的占位话术）——
        //    与格上 2 个孔**同源**（`Roster.TrinketsOf`，红线 26：不是第二份真值）✓
        right.AppendLine(DescribeTrinketSlots(roster, heroId));
        _detailRight!.Text = right.ToString();
        RefreshTrinketSlots(heroId);   // 🔴 每次开详情都现读（不缓存 → 格上读数与名册同一份）✓

        // 🔴 ⑤ 扎营技能：**只列该原型的** + **只列已接线的**（`ConsumedEffectNames`，红线 21）
        var campLines = new List<string> { "【扎营技能】（只列该原型 + 只列已接线）" };
        if (_campSkills is not null)
        {
            CampSkillConfig[] usable = _campSkills.Skills
                .Where(s => s.OwnerUnit == hero.Archetype)
                .ToArray();
            foreach (CampSkillConfig s in usable)
            {
                bool wired = CampSkillsConfig.ConsumedEffectNames.Contains(s.Effect);
                campLines.Add(wired
                    ? $"　· {s.Name}（{s.Cost} 点）✓ 已接线"
                    : $"　· {s.Name}（{s.Cost} 点）🔴 阶段二·未接线（不出现于扎营面板）");
            }
        }

        _detailCampSkills!.Text = string.Join("\n", campLines);

        _detailPanel!.Visible = true;   // 🔴 非空由建树保证（`OpenHeroDetail` 先建后刷）—— 拆方法后编译器不再跨方法流分析 ⇒ 显式标注；运行时零差异 ✓
        GD.Print($"[HamletRoot] 角色详情打开：{hero.Name}（{heroId}）原型 {hero.Archetype} Lv{hero.Level} 士气 {morale}" +
                 $"　特质 {currentTraits.Count} 条　疾病 {diseases.Count} 项　怪癖 {quirks.Count} 条");
    }
}
