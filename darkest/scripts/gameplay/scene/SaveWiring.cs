using System;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 `O-106`（2026-10-01）：**存档接线的单一真值** —— 城池（`--hamlet` 直达）与远征（`BattleRoot`）
/// 两条入口**共用本类**，避免"两处组装、各自漂移"（与 `ExpeditionRoot` 退休同族纪律）✓
///
/// <para>**为什么要有这个文件**：`Phase 1` 只把接线放在远征组装里（`ExpeditionComposition`）⇒
/// 城池直达路径**从不构造 `SaveController`** ⇒ `SaveController.Load` 在生产里**零调用点**
/// （写了不调 = 静默退化）⇒ 新进程启动**读不到档** ✓</para>
///
/// <para>**分工**：本类只管「**什么时候组装 / 什么时候读**」；存什么、怎么存由 `SaveController` 说话 ✓</para>
///
/// <para>🔴 **两条硬纪律**：
///   ① 读档失败**绝不写回**（原档原样留在磁盘上，只回报原因 —— 红线 21）✓
///   ② 启动读档**每进程只做一次**（`_startupDone`）：否则"读档后又走几步"会被磁盘上的旧文本回滚 ✓</para>
/// </summary>
public static class SaveWiring
{
    private static bool _startupDone;

    /// <summary>存档是否已接线（供报告 / 冒烟自证）✓</summary>
    public static bool IsBound => ExpeditionContext.Saves is not null;

    /// <summary>启动读档是否已做过（每进程一次；供报告 / 冒烟自证）✓</summary>
    public static bool StartupLoadDone => _startupDone;

    /// <summary>
    /// 🔴 **组装 + 绑定**（幂等：已绑定 ⇒ 复用不覆盖）—— 调用点**必须在五个跨趟持有者备齐之后**：
    /// 名册 / 进度 / 传家宝 / 经济 / **装备阶** ✓
    /// <para>🔴 **opt-in 纪律**：`save.json` 缺失 ⇒ 机制**显式关闭并打印**（绝不半生效）✓</para>
    /// <para>🔴 **不半生效**：名册 / 传家宝 / 经济缺任一 ⇒ 打印 + 返回 null（不组装半个存档）✓</para>
    /// </summary>
    public static SaveController? EnsureBound()
    {
        if (ExpeditionContext.Saves is { } bound)
        {
            return bound;
        }

        if (!FileAccess.FileExists(SaveConfig.ResPath))
        {
            GD.Print("[存档] 未找到 `save.json` ⇒ 存档机制**关闭**（opt-in；不静默）✓");
            return null;
        }

        Roster? roster = ExpeditionContext.Roster;
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        Economy? economy = ExpeditionContext.Gold;
        if (roster is null || heirlooms is null || economy is null)
        {
            GD.Print($"[存档] 持有者未备齐（名册={roster is not null} 传家宝={heirlooms is not null} " +
                     $"经济={economy is not null}）⇒ **不组装**（绝不半生效）✓");
            return null;
        }

        // 🔴 装备阶容器：**空表 = 全员第 0 阶 ≠ 未接线** ⇒ 缺则补一个（与 `HamletRoot.EnsureGearWiring` 同口径）✓
        HeroGearState gear = ExpeditionContext.Gear ?? new HeroGearState();
        if (ExpeditionContext.Gear is null)
        {
            ExpeditionContext.BindGear(gear);
        }

        SaveConfig saveCfg = SaveConfig.Parse(FileAccess.GetFileAsString(SaveConfig.ResPath));
        var saves = new SaveController(saveCfg, new SaveFileGateway(saveCfg),
            roster, ExpeditionContext.Progress, heirlooms, economy, gear);
        ExpeditionContext.BindSaves(saves);
        GD.Print($"[存档] 存档系统已接线：{saveCfg.SlotCount} 个槽位 · 目录 user://saves · " +
                 $"格式 v{Darkest.Gameplay.Sim.Save.SaveMigrator.CurrentVersion} ✓");
        return saves;
    }

    /// <summary>
    /// 🔴 **启动读档**（槽位 0；**每进程只做一次**）—— 无档 + `writeBack` ⇒ 建基线；有档 ⇒ 读；
    /// 读失败 ⇒ **绝不写回**（原档保留）✓
    /// <para>`writeBack`：城池路径 = true（读成功后**规范化回写**一次）；远征路径 = false
    /// （只读 —— 写回时机归城池：它是跨趟状态的编辑场所）✓</para>
    /// <para>🔴 **确定性冒烟不读档**（播种族 / 跨场景链）⇒ 读数与档无关（打印一行；**档原样保留**）✓</para>
    /// </summary>
    public static SaveLoadResult? LoadOnStart(SaveController? saves, bool writeBack)
    {
        if (saves is null || _startupDone)
        {
            return null;
        }

        if (IsDeterministicSmoke(OS.GetCmdlineArgs()))
        {
            _startupDone = true;
            GD.Print("[存档] 冒烟旗标存在 ⇒ 跳过启动读档（确定性读数；**档原样保留**）✓");
            return null;
        }

        _startupDone = true;

        if (!saves.HasSave(0))
        {
            if (!writeBack)
            {
                GD.Print("[存档] 槽位 0 无档 ⇒ 不写回（远征路径只读）✓");
                return null;
            }

            SaveLoadResult created = saves.Save(0);
            GD.Print($"[存档] 无档 ⇒ 建基线（槽位 0）：{created.Message}");
            return created;
        }

        SaveLoadResult loaded = saves.Load(0);
        GD.Print($"[存档] 启动读档（槽位 0）：{loaded.Message}");
        if (!loaded.Ok || !writeBack)
        {
            return loaded;   // 🔴 读失败 ⇒ 绝不写回（原档保留）✓
        }

        SaveLoadResult normalized = saves.Save(0);
        GD.Print($"[存档] 读档后规范化回写：{normalized.Message}");
        return normalized;
    }

    /// <summary>
    /// 🔴 **确定性冒烟族**（这些旗标会**播种 / 链式改写**状态）⇒ 启动读数必须**与档无关**：
    /// 播种族（`--hamlet-runs-seed=` / `--hamlet-gold=` / `--hamlet-heirloom-seed=` / `--hamlet-press-upgrade=`）
    /// 与跨场景链（`--e2e` / `--hamlet-next` / `--hamlet-embark`）✓
    /// <para>⚠️ 观察族旗标（`--hamlet-gear=` 等）**不在此列** —— `O-106` 的验收正是
    /// "新进程里看得见旧进程买下的阶" ✓</para>
    /// </summary>
    public static bool IsDeterministicSmoke(string[] args)
    {
        foreach (string a in args ?? Array.Empty<string>())
        {
            if (a.StartsWith("--hamlet-runs-seed=", StringComparison.Ordinal)
                || a.StartsWith("--hamlet-gold=", StringComparison.Ordinal)
                || a.StartsWith("--hamlet-heirloom-seed=", StringComparison.Ordinal)
                || a.StartsWith("--hamlet-press-upgrade=", StringComparison.Ordinal)
                || a == "--e2e" || a == "--hamlet-next" || a == "--hamlet-embark")
            {
                return true;
            }
        }

        return false;
    }
}
