// 🔴 **从 `BoardTests.cs` 拆出**（用户 2026-09-18 红线：程序文件 ≤600 行）：
//    本文件 = **夹具与助手**（布局/造板/快照/回放/取数据文件）—— **只搬家、零行为改动** ✓
//    ⇒ 主文件 `BoardTests.cs` 只留用例 ⇒ 两边都是 `partial class BoardTests` ✓
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

public sealed partial class BoardTests
{
    private static readonly SlotLayout PlayerLayout = new(6, 4, new[] { 5, 6 });
    private static readonly SlotLayout EnemyLayout = new(4, 4, Array.Empty<int>());

    // ------------------------------------------------------------------
    // 测试脚手架
    // ------------------------------------------------------------------

    private static UnitStats DummyStats()
        => new(Hp: 10, Attack: 12, PhysDef: 8, Speed: 8, Dodge: 10, Crit: 5, Resilience: 50,
            StunResist: 30, BleedResist: 30, StatDebuffResist: 25, DisplaceResist: 40,
            DeathsDoorResist: null);

    private static UnitRuntime U(string id, FormationSide side, bool weak = false)
        => new(UnitId.Of(id), side, DummyStats(), weak);

    private static FormationBoard MakeBoard(FormationSide side, params (int slot, string id, bool weak)[] roster)
    {
        SlotLayout layout = side == FormationSide.Player ? PlayerLayout : EnemyLayout;
        var units = roster.ToDictionary(r => r.slot, r => U(r.id, side, r.weak));
        return new FormationBoard(side, layout, FormationRules.Default(), units);
    }

    private static FormationBoard MakeBoardWithObstacles(
        FormationSide side,
        (int slot, string id, bool weak)[] roster,
        params (int slot, int? hp)[] obstacles)
    {
        SlotLayout layout = side == FormationSide.Player ? PlayerLayout : EnemyLayout;
        var units = roster.ToDictionary(r => r.slot, r => U(r.id, side, r.weak));
        var obs = obstacles.ToDictionary(o => o.slot, o => new ObstacleRuntime(o.hp));
        return new FormationBoard(side, layout, FormationRules.Default(), units, obs);
    }

    private static string Snapshot(IFormation board)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        return string.Join("|", Enumerable.Range(1, count)
            .Select(p => $"{p}:{board.GetSlot(p)}:{(board.UnitAt(p)?.ToString() ?? "-")}"));
    }

    /// <summary>紧凑单位排布视图（"-"=空槽），便于断言。</summary>
    private static string UnitsBrief(IFormation board)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        return string.Join(",", Enumerable.Range(1, count).Select(p => board.UnitAt(p)?.ToString() ?? "-"));
    }

    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到（应从 darkest/ 运行测试）。");
    }

    private static string FindFormationJson() => FindDataFile("formation.json");

    private static string FindUnitsJson() => FindDataFile("units.json");

    /// <summary>按序回放 Step 序列（模型版），用于断言「序列可无歧义还原最终板」。</summary>
    private static string ReplayAndSnapshot(
        FormationBoard board, IReadOnlyList<SlotChange> steps)
    {
        return ReplayAndSnapshot(board, steps, upto: -1);
    }

    private static string ReplayAndSnapshot(
        FormationBoard board, IReadOnlyList<SlotChange> steps, int upto)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        var state = new SlotState[count];
        var unit = new UnitId?[count];
        for (int p = 1; p <= count; p++)
        {
            state[p - 1] = board.GetSlot(p);
            unit[p - 1] = board.UnitAt(p);
        }

        int effective = upto < 0 ? steps.Count : upto;
        for (int i = 0; i < effective; i++)
        {
            SlotChange step = steps[i];
            int a = step.FromSlot - 1;
            int b = step.ToSlot - 1;
            switch (step.Kind)
            {
                case SlotChangeKind.Swap:
                    (state[a], state[b]) = (state[b], state[a]);
                    (unit[a], unit[b]) = (unit[b], unit[a]);
                    break;
                case SlotChangeKind.Move:
                    state[a] = step.FromSlotState; // 移走后（Empty / Blocked 不会用于 Move 源）
                    unit[a] = null;
                    state[b] = step.ToSlotState;
                    unit[b] = step.UnitA;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return string.Join("|", Enumerable.Range(1, count)
            .Select(p => $"{p}:{state[p - 1]}:{(unit[p - 1]?.ToString() ?? "-")}"));
    }

    /// <summary>仅回放前 upto 步后的「槽位:单位」视图（逐级断言用）。</summary>
    private static string ReplayUnitView(FormationBoard board, IReadOnlyList<SlotChange> steps, int upto)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        var unit = new UnitId?[count];
        for (int p = 1; p <= count; p++)
        {
            unit[p - 1] = board.UnitAt(p);
        }

        for (int i = 0; i < upto; i++)
        {
            SlotChange step = steps[i];
            int a = step.FromSlot - 1;
            int b = step.ToSlot - 1;
            if (step.Kind == SlotChangeKind.Swap)
            {
                (unit[a], unit[b]) = (unit[b], unit[a]);
            }
            else
            {
                unit[a] = null;
                unit[b] = step.UnitA;
            }
        }

        return string.Join(",", Enumerable.Range(1, count)
            .Select(p => $"{p}:{unit[p - 1]?.ToString() ?? "-"}"));
    }

    // ------------------------------------------------------------------
    // T-M1-01：数据结构与编成装载
    // ------------------------------------------------------------------

}
