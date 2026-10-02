// ① 来源：从 `HamletRoot.Gear.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **装备阶动作族**（选人 ／ 升阶 ／ 真实 Pressed 验收入口 ／ 事件原文回读；原 `:273-373` 逐字节；M11 ① 预警面第十七件·第二片）✓
// ② 职责：`SelectGearHero`（拖放落孔与点方块**共用**同一条入口；名册里没有 ⇒ 如实拒绝）／`UpgradeGear`（转发内核
//    `HeroGearState.TryUpgrade` —— 扣金币/涨阶/写事件全在内核，UI 只转发）／`PressGearUpgrade`／`PressGearHeroSquare`
//    （红线 26：发**真实** `Pressed` 信号，不直调业务方法）／`LastGearEventText`（回读事件原文 ⇒ 不造第二份文本）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_gearHero` x3 · `_gearUpgradeButtons` x1 · `_gearRow` x1 · `_log` x3
//    ＋ 主片方法 `RefreshGearRow` x2 ／ `EnsureGearWiring` x1 ／ `GearAxisSuffix` x1 ／ `GearAxisWord` x1
//    ＋ 主类根片 `Refresh()`／`AutoSave`／`BuildingPopupOpen`／`RefreshBuildingPopup` 各 x1 ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪：本片无 `Dictionary`/`CultureInfo` 实体引用 ⇒ 去掉
//    `System.Collections.Generic`/`System.Globalization`；`EffectEvent` 全限定 ⇒ 不留 `Darkest.Core.Events`）✓

using System;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

public partial class HamletRoot : Control
{
    /// <summary>🔴 选中装备阶对象（拖放落孔 ／ 点方块**共用**这一条入口）；名册里没有 ⇒ **如实拒绝**（不静默换人）✓</summary>
    public void SelectGearHero(string heroId)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null || !roster.Heroes.Any(h => h.Id == heroId))
        {
            GD.Print($"[HamletRoot] 装备阶选人拒绝：名册里没有「{heroId}」（不静默换人）✓");
            return;
        }

        _gearHero = heroId;
        GD.Print($"[HamletRoot] 装备阶已选中 {heroId}（只重画本行；**不调** Refresh ⇒ 不整屏抖）✓");
        RefreshGearRow();
    }

    /// <summary>
    /// 🔴 **升一阶**（真实业务入口）：转发内核 `HeroGearState.TryUpgrade`（扣金币 / 涨阶 / 写事件全在内核）✓
    /// 🔴 事件原文（`gear_upgraded:` ／ `gear_upgrade_refused:`）由 `LastGearEventText()` 回读 ⇒ 取证不靠 UI 自述 ✓
    /// </summary>
    public void UpgradeGear(GearAxis axis)
    {
        EnsureGearWiring();
        Roster? roster = ExpeditionContext.Roster;
        HeroConfig? hero = _gearHero is null ? null : roster?.Heroes.FirstOrDefault(h => h.Id == _gearHero);
        HeroGearState? gear = ExpeditionContext.Gear;
        HeroUpgradesConfig? cfg = ExpeditionContext.Upgrades;
        Economy? economy = ExpeditionContext.Gold;
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (hero is null || gear is null || cfg is null || economy is null || heirlooms is null)
        {
            GD.Print("[HamletRoot] 装备阶升级：前置缺失（hero/gear/cfg/economy/heirlooms 任一为空）⇒ **如实拒绝**，不改阶 ✓");
            RefreshGearRow();
            return;
        }

        int before = gear.TierOf(hero.Id, axis);
        int goldBefore = economy.Gold;
        int after = gear.TryUpgrade(_log, cfg, economy, hero, axis, heirlooms.LevelOf(HeroGear.BuildingTreeId(axis)));
        GD.Print($"[HamletRoot] 装备阶升级：{hero.Name} {GearAxisWord(axis)}轴 {before} ⇒ {after}" +
                 $"　金币 {goldBefore} ⇒ {economy.Gold}　事件原文：{LastGearEventText()}");
        Refresh();
        AutoSave("装备阶升级");
        if (BuildingPopupOpen)
        {
            RefreshBuildingPopup();   // 弹窗里的等级链 / 按钮跟着刷新（弹窗不关）✓
        }
    }

    /// <summary>
    /// 🔴 红线 26：**功能级验收走玩家路径** —— 本方法发出**真实 `Pressed` 信号**（不直接调业务方法）✓
    /// ⚠️ 按钮置灰 ⇒ **不改阶**：打印"UI 原样显示的理由"（= `HeroGear.Why` 原文）后返回 ✓
    /// </summary>
    public void PressGearUpgrade(GearAxis axis)
    {
        string suffix = GearAxisSuffix(axis);
        if (!_gearUpgradeButtons.TryGetValue(suffix, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressGearUpgrade({suffix})：找不到按钮（红线 21：按钮没挂上）");
            return;
        }

        if (btn.Disabled)
        {
            GD.Print($"[HamletRoot] PressGearUpgrade({suffix})：按钮**置灰** ⇒ 不改阶；" +
                     $"UI 原样显示的理由 =「{btn.TooltipText}」（红线 21）✓");
            return;
        }

        GD.Print($"[HamletRoot] PressGearUpgrade({suffix})：发出真实 Pressed（按钮「{btn.Text}」）⇒ 升阶");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>🔴 红线 26：**点英雄方块**也走真实 `Pressed`（比直接调 `SelectGearHero` 更接近玩家）✓</summary>
    private void PressGearHeroSquare(string heroId)
    {
        Button? square = _gearRow?.FindChild($"GearHero_{heroId}", true, false) as Button;
        if (square is null)
        {
            GD.Print($"[HamletRoot] 装备阶冒烟：网格里找不到英雄方块 {heroId} ⇒ 如实拒绝（不静默换人）");
            return;
        }

        GD.Print($"[HamletRoot] 装备阶冒烟：真实按下英雄方块「{square.Text}」（{heroId}）⇒ 选人");
        square.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>回读最近一条装备阶事件原文（`gear_upgraded:` ／ `gear_upgrade_refused:`）；没有 ⇒ "（无）" ✓</summary>
    private string LastGearEventText()
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            if (_log.Events[i] is Darkest.Core.Events.EffectEvent e
                && (e.EffectType.StartsWith("gear_upgraded:", StringComparison.Ordinal)
                    || e.EffectType.StartsWith("gear_upgrade_refused:", StringComparison.Ordinal)))
            {
                return e.EffectType;
            }
        }

        return "（无）";
    }
}
