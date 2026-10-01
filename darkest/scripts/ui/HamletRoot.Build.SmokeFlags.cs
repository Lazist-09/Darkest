using System;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.Build.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 冒烟旗标族**：`--hamlet-hover` / `--hamlet-building` /
///    `--hamlet-controls` / `--hamlet-loot` / `--hamlet-heirloom` /
///    `--hamlet-quest-select` / `--hamlet-provision` / `--hamlet-menu` /
///    `--hamlet-popup-close` + 怪癖／饰品播种 — 全部走**真实公开入口**
///    （红线 26：功能级验收走玩家路径；播种只经 `Roster.AddQuirk` / `Roster.EquipTrinket`）✓
/// ③ 🔴 **只搬家、零行为改动**（逐条同序）；调用点在 `_Ready()` —
///    **必须**排在 `--hamlet-hero-detail` / `--hamlet-row` 之前（详情打开那一刻现读）✓
/// </summary>
public partial class HamletRoot : Control
{
    private void HandleHamletSmokeFlags(string[] hamletArgs)
    {
        string? hover = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-hover=", StringComparison.Ordinal));
        if (hover is not null)
        {
            ShowBuildingInfo(hover["--hamlet-hover=".Length..]);
        }

        // 🔴 二级窗口冒烟（用户 2026-09-14 要求"弹窗要能开也能关"）：
        //    ① `--hamlet-building=<id>` ⇒ 打开建筑弹窗（真实走 `PressUpgrade` 那条入口下方同一条路径）
        //    ② `--hamlet-popup-close`  ⇒ 关掉最上层弹窗（等价于 `✕ 关闭` / `Esc`）
        string? bArg = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-building=", StringComparison.Ordinal));
        if (bArg is not null)
        {
            OpenBuildingPopup(bArg["--hamlet-building=".Length..]);
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-controls"))   // 阶段2：按键提示屏（可复验）
        {
            OpenControls();
        }
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-loot"))   // DD 1:1 战利品弹层（可复验）
        {
            OpenLootOverlay();
        }
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-heirloom"))   // DD 1:1 P5：传家宝兑换（可复验）
        {
            OpenHeirloomExchange();
        }
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-quest-select"))   // DD 1:1 ②：任务选择屏（可复验）
        {
            OpenQuestSelect();
        }
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-provision"))   // DD 1:1 ②：供应屏入口（可复验）
        {
            OpenProvision();
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-menu"))
        {
            OpenHamletMenu(); // 🔴 P2 冒烟：打开城池二级菜单 ✓
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-popup-close"))
        {
            GD.Print($"[HamletRoot] --hamlet-popup-close：关闭前 BuildingPopupOpen={BuildingPopupOpen}");
            CloseTopPopup();
            GD.Print($"[HamletRoot] --hamlet-popup-close：关闭后 BuildingPopupOpen={BuildingPopupOpen}（应 False）");
        }

        // 🆕 2026-10-01 M5u：怪癖冒烟播种 —— `--hamlet-quirk-seed=<英雄>:<怪癖>[,…]`，走**公开 API** `Roster.AddQuirk`
        //    （必写 `HeroQuirkGainedEvent`）⇒ 详情页/名册行读到的是**真状态**（红线 26：不直接改私有字典）✓
        //    ⚠️ 位置**必须在 `--hamlet-hero-detail` 之前** —— 详情内容是「打开那一刻现读」的 ✓
        if (FindSmokeArg(hamletArgs, "--hamlet-quirk-seed=") is { } quirkSeed)
        {
            SeedQuirksForSmoke(quirkSeed);
        }

        // 🆕 2026-10-02 M4u：饰品冒烟播种 —— `--hamlet-trinket-seed=<英雄>:<饰品>[,…]`，走**公开 API** `Roster.EquipTrinket`
        //    （必写 `HeroTrinketEquippedEvent`）⇒ 详情 2 格读到的是**真状态**（红线 26：不直接改私有字典）✓
        //    ⚠️ 位置**必须在 `--hamlet-hero-detail` 之前** —— 详情内容是「打开那一刻现读」的 ✓
        if (FindSmokeArg(hamletArgs, "--hamlet-trinket-seed=") is { } trinketSeed)
        {
            SeedTrinketsForSmoke(trinketSeed);
        }
    }
}
