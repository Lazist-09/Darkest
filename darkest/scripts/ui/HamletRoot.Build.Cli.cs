using System;
using System.Linq;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.Build.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · `_Ready()` 尾段（冒烟／CLI）**： `--hamlet-hero-detail=` ／ `--hamlet-row=` ／
///    `--hamlet-detail-back` ／ `--hamlet-embark` ／ `SmokeScript.Step` ／ `--e2e` 阶段 1 —— 全部走**真实玩家入口**
///    （红线 26： PressPortraitRightClick ／ PressRosterRow ／ PressEmbark ／ PressUpgrade ／ PressService）✓
/// ③ 🔴 依赖主类私有成员（全部在 `HamletRoot.cs` 字段区声明）： `_cfg` ／ `_saniCfg` ／ `_log` ／ `_rng` ／
///    `_selectedHero` ／ `_heroButtons` ／ `_detailLeft` ／ `_detailRight` ／ `_detailCampSkills`✓
///    依赖公有面： DetailOpen ／ DetailHeroId ／ OpenHeroDetail ／ CloseHeroDetail ／
///    PressPortraitRightClick ／ PressRosterRow ／ RosterRowCount ／ PressEmbark ／ PressUpgrade ／ PressService ／
///    SelectHero ／ DoRelief ／ HandleTrinketUiSmokeFlags ／ HandleGearSmokeFlags ／ HandleProvisionSmokeFlags ／
///    HandleRecruitSmokeFlags （以上均在同 partial 类的其他片） ＋ ExpeditionContext ／ SmokeScript ／
///    Sanitarium ／ Economy ／ Roster （Darkest.Gameplay.Sim.Run）✓
/// ④ **只搬家、零行为改动**（逐字同序）；唯一改写 = `--hamlet-embark` 的 `return;` ⇒ `return true`
///    由 `_Ready()` 早退（原语义 = 切场景后 `_Ready()` 直接返回）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 城池 `_Ready()` 的**尾段**（冒烟／CLI 族）。返回值 = **是否已切场景**（true ⇒ `_Ready()` 应立即 return）✓
    /// 🔴 顺序纪律：调用点**必须**在 Refresh() ＋「回城就绪」读数**之后**；本族内部顺序与搬家前逐条相同✓
    /// </summary>
    private bool RunHamletCliAndSmokeFlags(Economy economy, Roster roster)
    {
        // 🔴 M8.0 ⑥ 端到端：回城阶段 ⇒ **花钱（减压）** 然后 **再出发**
        // 🔴 片①（三界面卡 §1.3）冒烟钩子：**全部走真实 `Pressed`**（红线 26：功能级验收走玩家路径）
        // 🔴 2026-10-01 M6u：`--hamlet-embark` 的提前 `return` 已移到本方法**末尾** ——
        //    组合冒烟「先买阶 ⇒ 再出征 ⇒ 回城自动存档」需要装备阶冒烟**先跑完**（放在这里会整段被跳过）✓
        string[] hamletArgs = OS.GetCmdlineArgs();
        // 🆕 2026-10-02 分片：本族 = **冒烟旗标族**（悬停／弹窗／二级屏／播种）— 已移到
        //    `HamletRoot.Build.SmokeFlags.cs`（用户红线：程序文件 ≤600 行；只搬家、零行为改动）✓
        //    🔴 顺序纪律：本调用**必须**留在 `--hamlet-hero-detail` / `--hamlet-row` 分支之前 —
        //       播种族（怪癖／饰品）要让详情「打开那一刻现读」到真状态；`--hamlet-popup-close` 的读数
        //       也按搬家前逐条同序（旗标族内部顺序一字未改）✓
        HandleHamletSmokeFlags(hamletArgs);

        // ⚠️ 本查找 = 搬家前同一行：只是位置移到旗标族调用**之后**（命令行参数不因调用而变 ⇒ 同值同序）✓
        string? detailArg = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-hero-detail=", StringComparison.Ordinal));
        if (detailArg is not null && int.TryParse(detailArg["--hamlet-hero-detail=".Length..], out int dIdx))
        {
            PressPortraitRightClick(dIdx); // 🔴 右键头像 ⇒ 角色详情（用户 2026-09-15 要求）✓
            // 审计修复：右键路径若当时未开（名册行可能尚未建好）⇒ 兜底直接打开首位英雄，
            // 保证 hero-detail 入口**不空跑**（此前实测该入口长期静默无效，属假绿）✓
            if (!DetailOpen)
            {
                string? firstHero = ExpeditionContext.Roster?.Heroes.FirstOrDefault()?.Id;
                if (!string.IsNullOrEmpty(firstHero))
                {
                    GD.Print($"[HamletRoot] --hamlet-hero-detail：右键未开 ⇒ 兜底直接 OpenHeroDetail({firstHero})（审计不空跑）");
                    OpenHeroDetail(firstHero);
                }
                else
                {
                    GD.Print("[HamletRoot] --hamlet-hero-detail：名册为空 ⇒ 无法打开详情（如实留痕，不静默）");
                }
            }
        }

        // 🆕 2026-10-02 M4u：饰品 **UI 冒烟族**（落孔 ／ 点方块卸下）—— 🔴 **必须排在 `--hamlet-hero-detail` 之后**：
        //    详情里的 2 个孔/方块是「打开那一刻」建的（与旗标族里的**播种族**正好相反：那族必须排在前）✓
        //    两条旗标都走真实控件（红线 26）：落孔 = `GearHeroSlot.TryDropPayload`（引擎拖动同一入口），
        //    卸下 = 方块真发 `Pressed` ⇒ 验收看的是玩家那条路，不是绕过 UI 直调内核 ✓
        HandleTrinketUiSmokeFlags(hamletArgs);

        string? rowArg = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-row=", StringComparison.Ordinal));
        if (rowArg is not null && int.TryParse(rowArg["--hamlet-row=".Length..], out int rowIdx))
        {
            GD.Print($"[HamletRoot] 名册竖列行数 = {RosterRowCount}（名册 {roster.Heroes.Count} 人）");
            PressRosterRow(rowIdx);

            // 🆕 2026-10-01 M5u：**名册行读数**（行内文本 + 悬停）—— 供「名册显示怪癖」验收留证
            //    🔴 读的就是行上的**真控件**（红线 26：不另算一份 ⇒ 不会出现「打印的与屏上的不是一份」）✓
            if (rowIdx >= 0 && rowIdx < _heroButtons.Count)
            {
                Button rowBtn = _heroButtons[rowIdx];
                GD.Print($"[片②·名册行#{rowIdx}] 行文本=「{(rowBtn.FindChild("RosterInfo", true, false) as Label)?.Text}」" +
                         $"　悬停=「{rowBtn.TooltipText}」");
            }

            // 🔴 片② 冒烟：**打印详情内容摘要**，供断言"显示的是被点的人 / 特质状态 / 只列已接线 / 装备未实现"
            if (DetailOpen)
            {
                GD.Print($"[片②] DetailOpen={DetailOpen}　DetailHeroId={DetailHeroId}");
                GD.Print($"[片②·左] {_detailLeft?.Text?.Replace("\n", " ｜ ")}");
                GD.Print($"[片②·右] {_detailRight?.Text?.Replace("\n", " ｜ ")}");
                GD.Print($"[片②·扎营] {_detailCampSkills?.Text?.Replace("\n", " ｜ ")}");
            }
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-detail-back"))
        {
            GD.Print($"[片②] 返回前：DetailOpen={DetailOpen}");
            CloseHeroDetail();
            GD.Print($"[片②] 返回后：DetailOpen={DetailOpen}（应回到城池，红线 18：不是孤岛）");
        }

        // 🆕 2026-10-01 M6u：装备阶冒烟族（金币/传家宝播种 + 真实 `Pressed` 升阶；全部走玩家路径）✓
        HandleGearSmokeFlags(hamletArgs);

        // 🆕 2026-10-02 M12：供应冒烟族（买卖走**真实按钮**；必须排在 `--hamlet-gold=` 播种**之后**
        //    ⇒ 状态行按播种后余额现读；本族自己 `OpenProvision()` ⇒ 不依赖上一族把弹窗留在哪一栋）✓
        HandleProvisionSmokeFlags(hamletArgs);

        // 🆕 2026-10-01 M7u：招募冒烟族（**排在装备阶之后** ⇒ 组合冒烟「先升马车 ⇒ 再看今日新兵」读到的是**升后**的名单
        //    与上限；本族自己会 `OpenBuildingPopup("stagecoach")` ⇒ 不依赖上一族把弹窗留在哪一栋）✓
        HandleRecruitSmokeFlags(hamletArgs);

        // 🔴 M8.0 ⑥ 端到端：回城阶段 ⇒ **花钱（减压）** 然后 **再出发**（`--hamlet-embark`）
        //    ⚠️ 位置在装备阶冒烟**之后**：本分支会切场景并 `return`（M6u 组合冒烟依赖此顺序）✓
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-embark"))
        {
            PressEmbark();
            return true; // 已切场景（--hamlet-embark ⇒ `_Ready()` 立即早退）
        }

        // 🔴 跨场景步进冒烟：消费本场景的一步（`ui_three_screens.md` §3 / `#310`⑦）
        Darkest.Gameplay.Scene.SmokeScript.Step(this);

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--e2e") && ExpeditionContext.E2EStage == 1)
        {
            int goldBefore = economy.Gold;
            // ② 选人权：冒烟里**显式指定对象**（证明"能对指定的人减压"）
            string target = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
            int moraleBefore = roster.MoraleOf(target);
            SelectHero(target);
            DoRelief("tavern");
            int moraleAfter = roster.MoraleOf(target);
            GD.Print($"[E2E] 阶段1 回城：**花钱** {goldBefore} 减 {economy.Gold} ⇒ 剩余 {economy.Gold}" +
                     $"　指定对象 {target} 士气 {moraleBefore} 到 {moraleAfter}（V2：减压 ⇒ 士气确实更高）" +
                     $"　名册最低士气 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}");
            // 🔴 M8.1：**真实点击路径**升级（发 `Pressed` 信号）⇒ 证明"升级入口从启动场景可达、且点得动"
            int costBefore = ExpeditionContext.Heirlooms?.EffectiveReliefCost(_cfg.StressReliefCost) ?? -1;
            PressUpgrade("tavern");
            int costAfter = ExpeditionContext.Heirlooms?.EffectiveReliefCost(_cfg.StressReliefCost) ?? -1;
            GD.Print($"[E2E] 阶段1 升级：减压价 {costBefore} 到 {costAfter}");

            // 🔴 M8.2 / V16：**患病 → 治病**（冒烟用：对**全队**按概率掷骰使其患病，再走**真实点击路径**治愈）
            if (_saniCfg is not null)
            {
                Sanitarium.RollContract(_log, _rng, _saniCfg, roster, roster.Heroes.Select(h => h.Id).ToArray());
                int sickTotal = roster.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0);
                string? sickHero = roster.Heroes.FirstOrDefault(h => roster.DiseasesOf(h.Id).Count > 0)?.Id;
                GD.Print($"[E2E] 阶段1 患病：全队 {roster.Heroes.Count} 人掷骰 ⇒ 患病 {sickTotal} 人（概率 0.15/0.12/0.10 ×3 病）");

                if (sickHero is not null)
                {
                    _selectedHero = sickHero; // 指定治疗对象（走"按人选"的入口）
                    int before = roster.DiseasesOf(sickHero).Count;
                    PressService("cure_disease");
                    int after = roster.DiseasesOf(sickHero).Count;
                    GD.Print($"[E2E] 阶段1 治病：{sickHero} 患病 {before} 到 {after}（V16：患病 → 治病 回路成立）");
                }
            }

            ExpeditionContext.E2EStage = 2;
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4：再出发 ⇒ 宿主进地牢（旧场景已退休）✓
                GetTree().CallDeferred("change_scene_to_file", Darkest.UI.MainMenuRoot.BattleScene);
        }

        return false;
    }
}
