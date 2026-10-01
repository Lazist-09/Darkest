using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// 🆕 `O-106`（2026-10-01）· 城池 · **存档族** —— 从 `HamletRoot.cs` 拆出（每文件 ≤600 行）✓
///
/// <para>**本文件只做两件事**：
///   ① `EnsureSaves()`：启动时**组装 + 读档**（走 `SaveWiring` 单一真值 ⇒ 与远征路径同源）✓
///   ② `AutoSave(reason)`：**状态一改就落盘**（城池里 6 个真改状态的入口各调一次）✓</para>
///
/// <para>🔴 **为什么城池侧必须读档**：`Phase 1` 的接线只在远征组装里 ⇒ 城池直达路径
/// （主菜单 ⇒ 城池 ／ `--hamlet`）**从不构造 `SaveController`** ⇒ `Load` 零调用点
/// ⇒ 关掉游戏再打开，名册 ／ 金币 ／ 装备阶**回到出厂值**（`O-106`）✓</para>
///
/// <para>🔴 **为什么城池侧写回、远征侧只读**：写回时机归「**状态的主人**」——
/// 城池是跨趟状态的编辑场所（减压 ／ 招募 ／ 升级 ／ 升阶都在这），远征只消费它们 ✓</para>
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 🔴 **启动：组装 + 读档**（每进程只做一次；幂等）—— 调用点**必须在五个持有者备齐之后、
    /// 任何冒烟播种之前**（播种族的前置读数必须与档无关 ⇒ 见 `SaveWiring.IsDeterministicSmoke`）✓
    /// </summary>
    private void EnsureSaves()
    {
        SaveController? saves = SaveWiring.EnsureBound();
        if (saves is null)
        {
            return;
        }

        SaveLoadResult? r = SaveWiring.LoadOnStart(saves, writeBack: true);
        if (r is { Ok: true })
        {
            // 🔴 验收自证：新进程里**直接看见**上个进程买下的阶（`O-106` 的判据）✓
            GD.Print($"[存档] 读档后装备阶：{GearAuditLine()}");
        }
    }

    /// <summary>
    /// 🔴 **状态一改就落盘**（城池侧 6 个真改状态的入口各调一次）——
    /// 存档没接线 ⇒ **静默跳过**（`save.json` 缺失 = 机制关闭，不是缺陷）✓
    /// <para>⚠️ `SelectHero` **不在此列**：它只改 UI 局部的 `_selectedHero`，**不进快照** ✓</para>
    /// </summary>
    private void AutoSave(string reason)
    {
        if (ExpeditionContext.Saves is not { } saves)
        {
            return;
        }

        SaveLoadResult r = saves.Save(0);
        GD.Print(r.Ok
            ? $"[存档] 自动存档（{reason}）：{r.Message}"
            : $"[存档] 自动存档（{reason}）**失败**：{r.Message}");
    }

    /// <summary>装备阶一行摘要（逐人 `武N/甲N`；容器或名册缺失 ⇒ 如实标注）✓</summary>
    private static string GearAuditLine()
    {
        HeroGearState? gear = ExpeditionContext.Gear;
        Roster? roster = ExpeditionContext.Roster;
        if (gear is null || roster is null)
        {
            return "（容器或名册缺失，无法出摘要）";
        }

        return roster.Heroes.Count == 0 ? "（名册为空）" : gear.Audit(roster.Heroes);
    }
}
