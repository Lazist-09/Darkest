using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.Recruit.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **M7u 招募冒烟族**（原 `:421-505` 逐字节：`HandleRecruitSmokeFlags`（`--hamlet-recruit-list` ／ `-refresh` ／ `-press=` ／ `-seed=`）／ `DescribeRecruitOffers` ／ `PressRecruitOffer`（红线 26：发真实 `Pressed` 信号））✓
/// ③ 🔴 **依赖主类私有成员**：`_recruitRng`／`_recruitOfferButtons`／`_recruitOffers`／`_recruitOfferArchetypes`；主类 `FindSmokeArg()`／`OpenBuildingPopup()`；片间 `RefreshRecruitOffers`／`EffectiveCapNow`／`LastRecruitEventText`（Business 片）＋ 只读属性四件（主片）✓
/// ④ **只搬家、零行为改动**（逐字节同序；主片仅删 1 行纯空白 —— 原 `:223`）✓
/// </summary>
public partial class HamletRoot : Control
{
    // ------------------------------------------------------------------
    // 冒烟族（全部走真实入口；⚠️ 必须挂在 `HandleGearSmokeFlags` **之前**）✓
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 招募冒烟族（**全部走真实入口**）：
    ///   ① `--hamlet-recruit-list`        ⇒ 打开驿站弹窗并打印名单读数（条目数 ／ 高级数 ／ 名册 a/b）
    ///   ② `--hamlet-recruit-refresh`     ⇒ 再掷一次并打印**前后两份**名单（证明刷新真的换了人）
    ///   ③ `--hamlet-recruit-press=<idx>` ⇒ 真实 `EmitSignal(Pressed)` 招第 idx 个（红线 26）✓
    /// ⚠️ 位置：`HandleGearSmokeFlags` 之前 —— 后者可能把弹窗切到铁匠铺（那会把名单行隐藏）✓
    /// </summary>
    private void HandleRecruitSmokeFlags(string[] args)
    {
        // 🆕 2026-10-01 M5u：`--hamlet-recruit-seed=N` ⇒ 重播招募掷骰（**必须先于** `OpenBuildingPopup` —— 名单在开窗时掷）
        //    🔴 为什么需要：**高级新兵带怪癖**（M7③）只在 `Upgraded` 条目上发生，而固定种子 `20261001`
        //    在马车 Lv0 实测掷不出高级条目 ⇒ 不重播就**验不到那条分支**（只能「假定它对」）✓
        if (FindSmokeArg(args, "--hamlet-recruit-seed=") is { } rSeedArg && int.TryParse(rSeedArg, out int rSeed))
        {
            _recruitRng = new Darkest.Core.Rng.RngProvider(rSeed);
            GD.Print($"[HamletRoot] 招募冒烟：掷骰种子 ⇒ {rSeed}（同 seed 同名单 · 供复验高级分支）✓");
        }

        bool wantList = Array.Exists(args, a => a == "--hamlet-recruit-list" || a == "--hamlet-recruit-refresh")
            || FindSmokeArg(args, "--hamlet-recruit-press=") is not null;
        if (!wantList)
        {
            return;
        }

        OpenBuildingPopup("stagecoach");
        GD.Print($"[HamletRoot] 招募冒烟：RecruitRowMounted={RecruitRowMounted}　RecruitRowVisible={RecruitRowVisible}" +
                 $"　今日新兵 {RecruitOfferCount} 人（高级 {RecruitOfferUpgradedCount} 人）" +
                 $"　名册 {ExpeditionContext.Roster?.Heroes.Count ?? -1} / {EffectiveCapNow(ExpeditionContext.Roster)}" +
                 $"　列表条目节点 {_recruitOfferButtons.Count} 个 ✓");

        if (Array.Exists(args, a => a == "--hamlet-recruit-refresh"))
        {
            string before = DescribeRecruitOffers();
            RefreshRecruitOffers("冒烟·刷新");
            GD.Print($"[HamletRoot] 招募冒烟·刷新：前 [{before}] ⇒ 后 [{DescribeRecruitOffers()}]" +
                     $"　条目 {_recruitOfferButtons.Count} 个（应 = 曲线值）✓");
        }

        // ⚠️ 支持**逗号分隔的多个下标**（`--hamlet-recruit-press=0,1`）—— 只为一条验收：
        //    先招到满员、再按下一个条目 ⇒ 必须走「置灰 + 原样显示理由」分支（红线 21 (b)）✓
        if (FindSmokeArg(args, "--hamlet-recruit-press=") is { } pressArg)
        {
            foreach (string token in pressArg.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(token, out int idx)) { continue; }
                PressRecruitOffer(idx);
                GD.Print($"[HamletRoot] 招募冒烟·按下 #{idx}：名册 {ExpeditionContext.Roster?.Heroes.Count ?? -1} 人" +
                         $" / {EffectiveCapNow(ExpeditionContext.Roster)}　事件原文 {LastRecruitEventText()} ✓");
            }
        }
    }

    /// <summary>名单一行读法（冒烟对照用：职业 + 高级标记）✓</summary>
    private string DescribeRecruitOffers()
        => string.Join("、", _recruitOfferArchetypes.Select((a, i) =>
            $"{a}{(_recruitOffers[i].Upgraded ? "+" : "")}"));

    /// <summary>
    /// 🔴 红线 26：**功能级验收走玩家路径** —— 本方法发出**真实 `Pressed` 信号**（不直接调业务方法）✓
    /// ⚠️ 条目置灰 ⇒ **不改名册**：打印"UI 原样显示的理由"后返回 ✓
    /// </summary>
    public void PressRecruitOffer(int index)
    {
        if (index < 0 || index >= _recruitOfferButtons.Count)
        {
            GD.Print($"[HamletRoot] PressRecruitOffer(#{index})：找不到条目（列表 {_recruitOfferButtons.Count} 个；红线 21：按钮没挂上）");
            return;
        }

        Button btn = _recruitOfferButtons[index];
        if (btn.Disabled)
        {
            GD.Print($"[HamletRoot] PressRecruitOffer(#{index})：条目**置灰** ⇒ 不改名册；" +
                     $"UI 原样显示的理由 =「{btn.TooltipText}」（红线 21）✓");
            return;
        }

        GD.Print($"[HamletRoot] PressRecruitOffer(#{index})：发出真实 Pressed（条目「{btn.Text}」）⇒ 招募");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
