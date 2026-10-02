// ① 来源：从 `DungeonGridDeriver.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **走廊格散布**（`ScatterCorridorContent` 原 :282-348 ＋ 其 XML 文档 原 :262-281；M11 ① 预警面第二十四件·第一片）✓
// ② 职责：`D-4`／`D-6` 把「陷阱 `^` ／ 隐藏房 `*`」撒到走廊格上 —— 一次遍历、每格至多一次掷骰、`OrderBy(Y).ThenBy(X)` 确定性推进 ✓
// ③ 🔴 依赖（实测扫描本片）：入参仅 `CorridorSegment`（主片）与 `DungeonTileKind`（`DungeonGrid.cs`）；每掷必写 `RngDraw`（`Darkest.Core.Events`）⇒ **零跨片依赖** ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 3 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

public static partial class DungeonGridDeriver
{
    /// <summary>
    /// 🔴 `D-4` / `D-6`：**在走廊格上撒"格内容"**（陷阱 `^` / 隐藏房 `*`）—— 一次遍历、每格至多掷一次 ✓
    ///
    /// <para>🔴 **为什么不"逐段掷一次"**：派生占位图的走廊**普遍只有 1~2 格**（房间块 3×3、间隔 1
    /// ⇒ L 形连线的大部分格落在房间块内，被 `Carve` 跳过）⇒ "逐段掷"会**一格也撒不出**
    /// （`D-4` 实测：9 段里 7 段长 1、2 段长 2 ⇒ 全部落空）。这属于"派生占位布局"的已知粗粒度，
    /// **不是撒布机制的问题** ⇒ 改用**全局滚动**：走遍所有走廊格，逐格掷 ✓</para>
    ///
    /// <para>🔴🔴 **为什么陷阱与隐藏房必须共用同一趟**：`DungeonTileKind` 是**单值** ⇒ 同一格不可能
    /// 既是陷阱又是隐藏房。若分两趟各扫一遍，第二趟会**覆盖**第一趟的成果 ⇒
    /// "陷阱密度"悄悄变成"隐藏房概率的函数"（不可解释）⚠️
    /// ⇒ 本方法一次遍历、每格**至多一次**掷骰：先判隐藏房（稀有、更有价值），未中再判陷阱 ✓</para>
    ///
    /// <para>**散布口径**（与 `trap_defs.json` / `SecretsConfig` 的"内容"解耦，这里只管"有几个"）：</para>
    /// <para>· **只在走廊格上撒**（DD：陷阱与隐藏房都在走廊里）；房间格**永不**放 ✓</para>
    /// <para>· 每格独立掷（**不是**"整图恰好 N 个"—— 那是"配额"语义，会与"密度"打架）✓</para>
    /// <para>· 🔴 **每次掷骰必写 `RngDraw`**（红线）；某类概率 `≤0` ⇒ **该类不掷**（零随机不留痕）✓</para>
    /// <para>· 🔴 要撒（任一概率 > 0）却**未提供 `rng`** ⇒ **抛错**（不许静默半生效）⚠️</para>
    /// </summary>
    /// <returns>`(陷阱格数, 隐藏房格数)` ✓</returns>
    private static (int Traps, int Secrets) ScatterCorridorContent(
        IReadOnlyList<CorridorSegment> segments, DungeonTileKind[] tiles, int width,
        Darkest.Core.Rng.IRngProvider? rng, double trapChance, double secretChance,
        Darkest.Core.Events.CombatLog? log)
    {
        // 🔴 两类都关 ⇒ 一格不撒、一次不掷（**零随机不留痕** —— 既有调用点行为逐字不变）✓
        if (trapChance <= 0 && secretChance <= 0)
        {
            return (0, 0);
        }

        if (rng is null)
        {
            throw new InvalidOperationException(
                $"派生网格：`trapChancePercent = {trapChance}` / `secretChancePercent = {secretChance}` " +
                "有 > 0 但**未提供 `rng`** ⇒ 拒绝派生 " +
                "（静默不撒 = 机制静默失效，红线 21）⚠️");
        }

        // 🔴 走廊格的**并集**（同一格可能属于多段 —— L 形走廊会交叉）⇒ 去重后逐格掷，
        //    否则交叉格会被掷多次（"密度"变成"段数"的函数，不可解释）⚠️
        var corridorTiles = new HashSet<(int X, int Y)>();
        foreach (CorridorSegment seg in segments)
        {
            foreach ((int X, int Y) t in seg.Tiles)
            {
                corridorTiles.Add(t);
            }
        }

        // 🔴 顺序**必须确定**（HashSet 迭代序不稳定 ⇒ 同种子会漂移）⇒ 显式排序 ✓
        var ordered = corridorTiles.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();

        int traps = 0;
        int secrets = 0;
        foreach ((int X, int Y) pos in ordered)
        {
            // 🔴 `D-6`：**先判隐藏房**（更稀有、更有价值）—— 中了就**不再掷陷阱**（该格已定）✓
            if (secretChance > 0)
            {
                double secretRoll = rng.NextPercent();
                log?.Append(new Darkest.Core.Events.RngDraw(rng.DrawCount, secretRoll));
                if (secretRoll < secretChance)
                {
                    tiles[(pos.Y * width) + pos.X] = DungeonTileKind.Secret;
                    secrets++;
                    continue;
                }
            }

            if (trapChance <= 0)
            {
                continue; // 陷阱关（本格已判过隐藏房，未中 ⇒ 保持走廊）✓
            }

            double roll = rng.NextPercent();
            log?.Append(new Darkest.Core.Events.RngDraw(rng.DrawCount, roll));
            if (roll >= trapChance)
            {
                continue;
            }

            tiles[(pos.Y * width) + pos.X] = DungeonTileKind.Trap;
            traps++;
        }

        return (traps, secrets);
    }
}
