using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

// ① 来源：从 `Roster.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **成长与招募**（经验 ／ 升级判定 ／ 招募；原 `:88-148` ＋ `:344-384` 逐字节；M11 ① 预警面第十八件·第二片）✓
// ② 职责：`ExperienceOf`／`LevelOf`（读数口）／`AwardExperienceForBattle`（战斗结算给经验 ⇒ 攒够当前级费用升 1 级 ⇒
//    写 `HeroLevelUpEvent`；`_cfg.Experience` 未接线 ⇒ 显式不生效、不假装）／`ReplaceLevel`／`Recruit`（免费新兵 ·
//    起点等级由马车曲线定 · **满员即拒绝** —— `CanRecruit` 是唯一可用性判据 · 红线 21 (b)）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_cfg` x5 · `_heroes` x7 · `_xp` x5 · `_recruitSeq` x1 · `_morale` x1；
//    根片公开口：`CanRecruit` x1（`Recruit` 前置判据）✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 5 条：实体引用 `List<>`／LINQ／事件／Data 均保留；
//    `Godot` 零引用 —— 构建 RC=0 验证；全量重建逐字节 == 基线，见 `reports/p6_m11_split18_20261002.md`）✓
// ─────────────────────────────────────────────────────────────
public sealed partial class Roster
{
    /// <summary>🆕 某英雄的累计经验（读数口）✓</summary>
    /// <summary>🆕 某英雄**当前这一级的累计经验**（攒够 `level_costs[级-1]` 就升 ✓）</summary>
    public int ExperienceOf(string heroId) => _xp.GetValueOrDefault(heroId);

    /// <summary>🆕 某英雄的当前**等级**（可直接被 `HeroProjection.ApplyLevel` 投影 ✓）</summary>
    public int LevelOf(string heroId) => _heroes.First(h => h.Id == heroId).Level;

    /// <summary>
    /// 🆕 **战斗结算 ⇒ 给经验 ⇒ 可能升级**（契约：`hamlet.md` §7.2/§7.6「战斗给经验 ⇒ 等级成长」）✓
    /// 纪律：**数字全部来自 `_cfg.Experience`**（缺省 ⇒ 显式不生效）· **跨过阈值 ⇒ 写 `HeroLevelUpEvent`** ✓
    /// </summary>
    public bool AwardExperienceForBattle(CombatLog log, bool win, string reason = "battle")
    {
        RosterExperience? exp = _cfg.Experience;
        if (exp is null)
        {
            return false; // 🔴 未接线：**不假装**（不写事件、不改等级）✓
        }

        int amount = win ? exp.XpPerWin : exp.XpPerLoss;
        if (amount <= 0)
        {
            return false;
        }

        foreach (HeroConfig h in _heroes.ToList())
        {
            int total = _xp.GetValueOrDefault(h.Id) + amount;
            _xp[h.Id] = total;
            log.Append(new HeroExperienceGainedEvent(h.Id, amount, total, reason));

            // 🔴 **语义 = 每级固定经验**（策划 `#399` 裁定：`1→2: 2 · 2→3: 3 · 3→4: 4 · 4→5: 5 · 5→6: 6`
            //    递增 1 ⇒ **升到 Lv6 累计 20 场胜利**）✓ —— 实现 = **攒够当前级费用 ⇒ 扣掉、升 1 级** ✓
            int level = h.Level;
            while (level < _cfg.LevelMax)
            {
                int? cost = exp.CostToNextLevel(level, _cfg.LevelMin, _cfg.LevelMax);
                if (cost is null || _xp[h.Id] < cost.Value)
                {
                    break;
                }

                _xp[h.Id] -= cost.Value;
                level++;
                ReplaceLevel(h.Id, level);
                log.Append(new HeroLevelUpEvent(h.Id, level - 1, level));
            }
        }

        return true;
    }

    private void ReplaceLevel(string heroId, int level)
    {
        int i = _heroes.FindIndex(h => h.Id == heroId);
        if (i >= 0)
        {
            _heroes[i] = _heroes[i] with { Level = level };
        }
    }

    /// <summary>
    /// **招募**（M8.0 ⑤ / `#283` 硬要求③）：**免费**；新兵 `level == 1`、`morale == 50`（**不比老的强**）；
    /// 特质从既有英雄的特质池里取**一正一负**（与 7.7「小幅、正负都有」一致）。
    /// 满员即拒绝（返回 null；**不静默顶替**）。
    /// </summary>
    public HeroConfig? Recruit(CombatLog log, StagecoachConfig coach, string archetype, string name,
        int? rookieLevel = null)   // 🆕 马车升级的"新兵起始等级"（`EffectiveRookieLevel` ⇒ **真正应用** ✓）
    {
        if (log is null || coach is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (!CanRecruit)
        {
            return null; // 名册已满（P22 ⑥ · 上限 = `CurrentCap`，由马车曲线决定）—— 拒绝，不悄悄顶替
        }

        List<HeroTraitConfig> pool = _heroes.SelectMany(h => h.Traits).ToList();
        HeroTraitConfig? pos = pool.FirstOrDefault(t => t.DamagePct > 0 || t.MoraleDamagePct < 0);
        HeroTraitConfig? neg = pool.FirstOrDefault(t => t.DamagePct < 0 || t.MoraleDamagePct > 0);
        if (pos is null || neg is null)
        {
            throw new InvalidOperationException($"{RosterConfig.ResPath}: 特质池里没有可用的正/负特质（P22 ③）。");
        }

        var rookie = new HeroConfig(
            $"hero_{archetype}_r{++_recruitSeq}",
            name,
            archetype,
            // 🔴 应用"新兵起始等级"：**马车升级真的改变新兵起点**（此前只被 UI 打印、**没被消费** ⚠️ ⇒ 已接）✓
            rookieLevel ?? coach.RookieLevel,
            new[] { pos, neg },
            coach.RookieMorale);

        _heroes.Add(rookie);
        _morale[rookie.Id] = rookie.Morale;
        log.Append(new HeroRecruitedEvent(rookie.Id, rookie.Name, rookie.Archetype, rookie.Level, rookie.Morale, coach.RecruitCost));
        return rookie;
    }

}
