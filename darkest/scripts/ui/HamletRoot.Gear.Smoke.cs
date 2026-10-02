// ① 来源：从 `HamletRoot.Gear.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **冒烟族**（解锁播种 `HandleSmokeUnlockSeed` ＋ 装备阶冒烟 `HandleGearSmokeFlags`；原 `:375-377` ＋ `:383-484` 逐字节；第十七件·第三片）✓
// ② 职责：`--hamlet-runs-seed=N` ⇒ `RunProgress.FinishRun` ×N（公开 API 播种；必须在 nav 建树**之前**）／
//    `--hamlet-gold=` ／ `--hamlet-heirloom-seed=` 播种（`Economy.AwardContent`／`HeirloomStock.Add`；金币口径保持不动）／
//    `--hamlet-press-upgrade=` ／ `--hamlet-gear=weapon|armour` ＋ `-hero=`（点方块）或 `-drop=`（孔回调）⇒ 升阶 ✓
// ③ 🔴 依赖主片私有成员/状态（实测扫描本片）：`_gearSlot` x2 · `_gearHero` x1 · `_log` x3 ＋ 主片 `FindSmokeArg` x7
//    ＋ 动作片 `PressGearHeroSquare` x1 ／ `PressGearUpgrade` x1 ＋ 主类根片 `Refresh()` x2 ／ `OpenBuildingPopup` ／
//    `PressUpgrade` ／ `LastGearEventText` 各 x1 ＋ 只读属性 `GearRowMounted`／`GearRowVisible` 各 x2 ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪：本片无 `Dictionary`/`CultureInfo`/`Linq` 实体引用 ⇒
//    只留 `System`／`Scene`／`Sim.Run`／`Godot`；旧「冒烟族」段注（原 `:375-377`）随族**逐字节**迁入本片）✓

using System;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

public partial class HamletRoot : Control
{
    // ------------------------------------------------------------------
    // 冒烟族（全部走真实入口；⚠️ 游戏参数必须放在 `--` **之前**，见报告）✓
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 **解锁播种**：`--hamlet-runs-seed=N` ⇒ `RunProgress.FinishRun` ×N（**公开 API**，不改内核）✓
    /// ⚠️ **必须在 nav 建树之前调用** —— 锁着的建筑只建 🔒 Label、不建按钮（建完再播种就晚了）✓
    /// </summary>
    private void HandleSmokeUnlockSeed(string[] args)
    {
        if (FindSmokeArg(args, "--hamlet-runs-seed=") is not { } seedArg
            || !int.TryParse(seedArg, out int runs) || runs <= 0)
        {
            return;
        }

        for (int i = 0; i < runs; i++)
        {
            ExpeditionContext.Progress.FinishRun(_log, "smoke-seed");
        }

        GD.Print($"[HamletRoot] 冒烟播种：已完成出征 {ExpeditionContext.Progress.RunsFinished} 趟" +
                 $"（FinishRun ×{runs}；解锁阈值表的输入）✓");
    }

    /// <summary>
    /// 🔴 装备阶冒烟族（**全部走真实入口**）：
    ///   ① `--hamlet-gold=N` ／ ② `--hamlet-heirloom-seed=N`：用**公开 API** 播种
    ///      （`Economy.AwardContent` ／ `HeirloomStock.Add`）—— 金币口径**保持不动**（750/1750/3000/6000
    ///      由 `hero_upgrades.json` 说话，UI 不改一个数）✓
    ///   ③ `--hamlet-press-upgrade=<building>`：既有两步路径（nav ⇒ 弹窗升级按钮）✓
    ///   ④ `--hamlet-gear=weapon|armour` ＋ `--hamlet-gear-hero=<id>`（点方块选人）
    ///      或 `--hamlet-gear-drop=<id>`（孔回调 = 引擎 `_DropData` 的同一入口）⇒ 升阶 ✓
    /// </summary>
    private void HandleGearSmokeFlags(string[] args)
    {
        if (FindSmokeArg(args, "--hamlet-gold=") is { } goldArg
            && int.TryParse(goldArg, out int gold) && gold > 0 && ExpeditionContext.Gold is { } economy)
        {
            economy.AwardContent(_log, gold, "smoke-seed");
            GD.Print($"[HamletRoot] 冒烟播种：金币 +{gold} ⇒ {economy.Gold}（Economy.AwardContent；公开 API）✓");
            Refresh();
        }

        if (FindSmokeArg(args, "--hamlet-heirloom-seed=") is { } seedArg
            && int.TryParse(seedArg, out int seed) && seed > 0 && ExpeditionContext.Heirlooms is { } stock)
        {
            foreach (string kind in new[] { "deeds", "crests" })
            {
                bool ok = stock.Add(_log, kind, seed, "smoke-seed");
                GD.Print($"[HamletRoot] 冒烟播种：传家宝 {kind} +{seed} ⇒ {stock.Count(kind)}（accepted={ok}）✓");
            }

            Refresh();
        }

        if (FindSmokeArg(args, "--hamlet-press-upgrade=") is { } upBuilding)
        {
            PressUpgrade(upBuilding);
        }

        if (FindSmokeArg(args, "--hamlet-gear=") is not { } axisArg)
        {
            return;
        }

        GearAxis? axis = axisArg switch
        {
            "weapon" => GearAxis.Weapon,
            "armour" => GearAxis.Armour,
            _ => null,
        };
        if (axis is not { } a)
        {
            GD.Print($"[HamletRoot] --hamlet-gear={axisArg}：只认 weapon / armour（如实拒绝）");
            return;
        }

        OpenBuildingPopup(HeroGear.BuildingTreeId(a));
        if (FindSmokeArg(args, "--hamlet-gear-drop=") is { } dropHero)
        {
            GD.Print($"[HamletRoot] 装备阶冒烟：投递英雄方块 {dropHero} 进孔（走**孔的落孔入口** TryDropPayload：" +
                     "先 _CanDropData 族校验再 _DropData；拖动阈值本身无法 headless 复验 ⇒ 报告如实标注）");
            if (_gearSlot is null)
            {
                GD.Print("[HamletRoot] 装备阶冒烟：孔模板不可用 ⇒ 投递失败（如实上报，不静默）");
            }
            else
            {
                _gearSlot.TryDropPayload(DragPayload.Encode(DragPayload.HeroTag, dropHero));
            }
        }
        else if (FindSmokeArg(args, "--hamlet-gear-hero=") is { } heroId)
        {
            PressGearHeroSquare(heroId);
        }
        else
        {
            GD.Print("[HamletRoot] 装备阶冒烟：未给 --hamlet-gear-hero ／ --hamlet-gear-drop ⇒ 未选人（按钮应置灰 ⇒ 走拒绝路径）");
        }

        GD.Print($"[HamletRoot] 装备阶冒烟：GearRowMounted={GearRowMounted}　GearRowVisible={GearRowVisible}" +
                 $"　选中={_gearHero ?? "（无）"}");
        PressGearUpgrade(a);
        GD.Print($"[HamletRoot] 装备阶冒烟：事件原文 = {LastGearEventText()}");
    }
}
