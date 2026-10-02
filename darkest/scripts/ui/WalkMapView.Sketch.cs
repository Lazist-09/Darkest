using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.UI;

/// <summary>
/// ① 从 `WalkMapView.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **文字速写与确定性摆位族**（原 `:433-492` 逐字节：只读入口 `SketchText` ／ 摆位 `Layout`（x=深度 · y=同深度内序号；主片 `Refresh` 与 `SketchText` **共用一份**）／ 三态字形速写 `Sketch`）✓
/// ③ 🔴 **依赖主类私有成员**：常量 `StepX`／`StepY`／`Pad`（主片）；片外消费方 = 主片 `Refresh(MapSketch)`（调 `Layout`／`Sketch`）✓
/// ④ **只搬家、零行为改动**（逐字节同序；主片仅删 2 行纯空白 —— 原 `:277` 与 `:432`）✓
/// </summary>
public partial class WalkMapView : PanelContainer
{
    /// <summary>🔴 `D-3`：文字速写的**只读入口**（headless 验收用 —— 画面看不见，靠文字证明三态可分辨）✓</summary>
    public static string SketchText(MapSketch sketch) => Sketch(sketch, Layout(sketch));

    /// <summary>🔴 `D-3`：格子摆位（x = 深度、y = 同深度内序号；确定性）—— `SketchText` 与 `Refresh` **共用一份** ✓</summary>
    private static Dictionary<int, (int X, int Y)> Layout(MapSketch sketch)
    {
        var pos = new Dictionary<int, (int X, int Y)>();
        foreach (SketchCell c in sketch.Cells)
        {
            pos[c.Id] = (Pad + c.Depth * StepX, Pad + c.Lane * StepY);
        }

        return pos;
    }

    /// <summary>
    /// 文字速写（**布局自证**）：每行 = 一个 lane、每列 = 一个 depth。
    /// 🔴 `D-3`：**三态各一个字形** —— `■`=当前 `◆`=终点 `□`=已看清（Visited）`▒`=只有轮廓（Scouted）`·`=未知。
    /// ⚠️ **只画房间**：走廊在这里**不画**（它在画面上是两房之间的小方块，共 N 条，见行首计数）——
    ///    图例不得承诺没画的东西（我自己立的规矩：读数与事实必须一致）✓
    /// </summary>
    private static string Sketch(MapSketch sketch, Dictionary<int, (int X, int Y)> pos)
    {
        int lanes = pos.Values.Select(v => v.Y).DefaultIfEmpty(0).Max() / StepY + 1;
        int depths = pos.Values.Select(v => v.X).DefaultIfEmpty(0).Max() / StepX + 1;
        var grid = new char[lanes, depths * 2];
        for (int y = 0; y < lanes; y++)
        {
            for (int x = 0; x < depths * 2; x++)
            {
                grid[y, x] = ' ';
            }
        }

        foreach (SketchCell c in sketch.Cells)
        {
            (int X, int Y) p = pos[c.Id];
            // 🔴 `D-3`：三态各一个字形（`▒` = "知道有东西、但没看清"—— 与画面上的"亮轮廓"同义）✓
            char ch = c.IsCurrent ? '■'
                : c.IsGoal ? '◆'
                : c.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted ? '▒'
                : c.Revealed ? '□'
                : '·';
            grid[p.Y / StepY, (p.X / StepX) * 2] = ch;
        }

        var sb = new StringBuilder();
        sb.Append($"房间方块 {sketch.Cells.Count} 个／走廊小方块 {sketch.Links.Count} 条（x=深度 · y=同深度内序号）");
        sb.Append("　图例：■当前 ◆终点 □已看清 ▒只有轮廓 ·未知（**只画房间**；走廊在画面上是两房之间的小方块）");
        for (int y = 0; y < lanes; y++)
        {
            sb.Append('\n');
            for (int x = 0; x < depths * 2; x++)
            {
                sb.Append(grid[y, x]);
            }
        }

        return sb.ToString();
    }
}
