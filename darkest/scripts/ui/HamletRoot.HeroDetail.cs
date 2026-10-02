using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// ① 从 `HamletRoot.cs` 拆出（用户红线 28：程序文件 ≤600 行）；🔴 2026-10-02 **第十件·结构化拆分**：本文件只留门面 ✓
/// ② 本文件 = **城池 · 角色详情族【门面】**：打开 `OpenHeroDetail`（前置校验 → 建树委托 → 刷新委托）／ 关闭 `CloseHeroDetail` ✓
///    同族三片：`HamletRoot.HeroDetail.Build.cs`（懒建整树）· `.Refresh.cs`（每次打开的读数）· `.Quirks.cs`（怪癖文案三件）✓
/// ③ 🔴 依赖主类私有成员/状态：`_detailPanel`（`HamletRoot.cs:86`）· `_detailHeroId`（`:93`）· `_rosterCfgForDetail`（`:52`）✓
///    委托面（同为 `partial HamletRoot` ⇒ 直读字段、不新造接口）：`BuildHeroDetailPanel` ／ `RefreshHeroDetail` ✓
/// ④ **零行为改动**（是「拆方法」不是「搬成员」：调用点顺序 = 原内序；读数对照见 `reports/p6_m11_split8_20261002.md`）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>🔴 打开某英雄的**角色详情**（片②）—— 数据全部真读（红线 26：断言"显示的是被点的人"）。</summary>
    public void OpenHeroDetail(string heroId)
    {
        RosterConfig? rosterCfg = _rosterCfgForDetail;
        Roster? roster = ExpeditionContext.Roster;
        if (rosterCfg is null || roster is null)
        {
            GD.Print("[HamletRoot] 角色详情：名册未加载");
            return;
        }

        HeroConfig? hero = rosterCfg.Heroes.FirstOrDefault(h => h.Id == heroId);
        if (hero is null)
        {
            GD.Print($"[HamletRoot] 角色详情：找不到英雄 {heroId}");
            return;
        }

        _detailHeroId = heroId;

        // 🔴 面板只在**首次打开**时建树（原 `if (_detailPanel is null)` 的守卫语义原样保留 ——
        //    整块 :40-295 已抽到 `HamletRoot.HeroDetail.Build.cs`，方法内仍带同一守卫 ⇒ 幂等）✓
        if (_detailPanel is null)
        {
            BuildHeroDetailPanel(hero, heroId);
        }

        // 🔴 每次打开都现读刷新（英雄/名册/装备都可能已变）—— 顺序与原方法内序一致：
        //    建树 → 刷新读数 → 收尾置可见（`_detailPanel.Visible = true` 随刷新片带走）✓
        RefreshHeroDetail(hero, roster, heroId);
    }

    /// <summary>🔴 关闭详情 ⇒ **回到城池**（红线 18：不是孤岛）。</summary>
    public void CloseHeroDetail()
    {
        if (_detailPanel is not null)
        {
            _detailPanel.Visible = false;
        }

        _detailHeroId = null;
        GD.Print("[HamletRoot] 角色详情关闭 ⇒ 回到城池");
    }
}
