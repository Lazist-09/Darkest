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
/// ② 本文件 = **城池 · 怪癖文案三件**：`QuirkKindLabel`（分类标签）／ `DescribeQuirks`（行正文 + 「显示与数据一致」自证）／
///    `DescribeQuirksDetail`（悬停全文：buff 原语 · 互斥名单 · 掷签权重）✓
/// ③ 🔴 **零私有状态依赖**（三件都是 `private static`，`QuirksConfig?`/`QuirkConfig` 全部入参）；跨片消费方：
///    详情页 `HamletRoot.HeroDetail.Refresh.cs` · 名册 `HamletRoot.Refresh.cs:115` · 驿站招募 `HamletRoot.Recruit.cs:368/414` ✓
/// ④ **只搬家、零行为改动**（:419-504 逐字节原样；三件仍是 `partial HamletRoot` 私有成员 ⇒ 调用点零改动）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 🆕 **M5u · 怪癖分类标签**（详情页与驿站招募**共用** ⇒ 分类口径只有一处）——
    /// 判据**全部按数据字段**（不写死 id）：`is_disease` ⇒ 疾病；否则 `is_positive` ⇒ 正面/负面；
    /// 再拼 `classification`（一手实测只有 `mental`/`physical`/空串 ⇒ 空串如实写「未分类」）✓
    /// </summary>
    private static string QuirkKindLabel(QuirkConfig q)
    {
        string kind = q.IsDisease ? "疾病" : (q.IsPositive ? "正面" : "负面");
        string cls = q.Classification switch
        {
            "mental" => "精神",
            "physical" => "肉体",
            "" => "未分类",
            _ => q.Classification,
        };
        return $"{kind}·{cls}";
    }

    /// <summary>🆕 M5u：怪癖行正文（分类 + 互斥提示；库未加载 ⇒ 如实标注，不假装分类）✓</summary>
    private static string DescribeQuirks(QuirksConfig? cfg, IReadOnlyCollection<string> quirks)
    {
        if (quirks.Count == 0)
        {
            return "【怪癖】无";
        }

        var sb = new System.Text.StringBuilder($"【怪癖】{quirks.Count} 条");
        foreach (string id in quirks)
        {
            if (cfg is null)
            {
                sb.Append($"\n　· {id}（🔴 怪癖库未加载 ⇒ 只有 id，分类/互斥不可读）");
                continue;
            }

            QuirkConfig q = cfg.Get(id);
            string clash = q.IncompatibleQuirks.Count == 0
                ? string.Empty
                : $"　互斥 {q.IncompatibleQuirks.Count} 条：{string.Join("、", q.IncompatibleQuirks)}";
            sb.Append($"\n　· {id}（{QuirkKindLabel(q)}）{clash}");
        }

        // 🔴 数据自证（显示与数据一致）：现持的几条**彼此**不应互斥（掷签前已过滤候选池）
        //    ⇒ 真出现就是数据或接线被改坏 —— 当场写在玩家看得见的地方（不静默）✓
        if (cfg is not null)
        {
            foreach (string a in quirks)
            {
                foreach (string b in quirks)
                {
                    if (string.CompareOrdinal(a, b) < 0 && cfg.AreIncompatible(a, b))
                    {
                        sb.Append($"\n　⚠ 数据异常：{a} 与 {b} 互斥却同时持有（应已被候选池过滤）");
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>🆕 M5u：悬停全文（Godot 内建 `TooltipText`）—— 逐条 id ／ 分类 ／ buff 原语 ／ 互斥名单 ／ 掷签权重 ✓</summary>
    private static string DescribeQuirksDetail(QuirksConfig? cfg, IReadOnlyCollection<string> quirks)
    {
        if (quirks.Count == 0)
        {
            return "怪癖：无（本屏与名册同源：Roster.QuirksOf）";
        }

        if (cfg is null)
        {
            return $"怪癖 {quirks.Count} 条：{string.Join("、", quirks)}（🔴 怪癖库未加载 ⇒ 完整信息不可读）";
        }

        var sb = new System.Text.StringBuilder($"怪癖全信息（{quirks.Count} 条 · 同源 Roster.QuirksOf）");
        foreach (string id in quirks)
        {
            QuirkConfig q = cfg.Get(id);
            sb.Append($"\n· {id}｜{QuirkKindLabel(q)}｜掷签权重 {q.RandomChance}");
            sb.Append($"\n　　buff 原语 {q.Buffs.Count} 条：{(q.Buffs.Count == 0 ? "无" : string.Join("、", q.Buffs))}");
            sb.Append($"\n　　互斥 {(q.IncompatibleQuirks.Count == 0 ? "无" : string.Join("、", q.IncompatibleQuirks))}");
            sb.Append($"\n　　curio 标签 {(string.IsNullOrEmpty(q.CurioTag) ? "无" : q.CurioTag)}｜可被新怪癖替换={q.CanBeReplacedByNewQuirk}");
        }
        sb.Append("\n（buff 原语层未接线：数值只登记不动 · #307 冻结）");
        return sb.ToString();
    }
}
