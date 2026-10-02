using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.UI;

/// <summary>
/// ① 从 `WalkMapView.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **内核表示 ⇒ 快照 · 适配器族**（原 `:143-276` 逐字节：旧签名转发 `Refresh(ExpeditionMap,…)` ／ 房间图适配 `FromExpeditionMap`（`D-3` 二态映射）／ 瓷砖网格适配 `FromTileWalk`（含 `D-6`：未揭示隐藏房**整格不画**））✓
/// ③ 🔴 **依赖主类私有成员**：无（纯适配/转发：`Refresh(ExpeditionMap,…)` 转发主片 `Refresh(MapSketch)`；记录类型 `SketchCell`／`SketchLink`／`MapSketch` 在主片）✓
/// ④ **只搬家、零行为改动**（逐字节同序；主片仅删 2 行纯空白 —— 原 `:277` 与 `:432`）✓
/// </summary>
public partial class WalkMapView : PanelContainer
{
    /// <summary>
    /// 🔴 **旧签名 = 适配器**（零行为变化）：把内核"房间+连线"图翻成 `MapSketch` 后交给新入口 ✓
    /// （(B) ③ 落地后，本签名与其调用点一并退役 —— 那时写 `FromDungeonGrid(...)` 即可）✓
    /// </summary>
    public void Refresh(
        Darkest.Gameplay.Sim.Run.ExpeditionMap map,
        int currentRoomId,
        IReadOnlyList<int> revealed,
        IReadOnlyList<int>? movable = null,
        int remainingSegments = -1,
        string? currentType = null)
        => Refresh(FromExpeditionMap(map, currentRoomId, revealed, movable ?? System.Array.Empty<int>(), remainingSegments, currentType));

    /// <summary>🔴 适配器：内核"房间+连线"图 ⇒ 表现层快照（**唯一读内核类型的地方**）✓</summary>
    public static MapSketch FromExpeditionMap(
        Darkest.Gameplay.Sim.Run.ExpeditionMap map,
        int currentRoomId,
        IReadOnlyList<int> revealed,
        IReadOnlyList<int> movable,
        int remainingSegments,
        string? currentType)
    {
        var revealedSet = new HashSet<int>(revealed);
        var cells = new List<SketchCell>();
        foreach (IGrouping<int, Darkest.Gameplay.Sim.Run.MapRoom> col in map.Rooms.GroupBy(r => r.Depth).OrderBy(g => g.Key))
        {
            int lane = 0;
            foreach (Darkest.Gameplay.Sim.Run.MapRoom room in col.OrderBy(r => r.Id))
            {
                cells.Add(new SketchCell(
                    room.Id,
                    room.Depth,
                    lane,
                    room.Type ?? string.Empty,
                    revealedSet.Contains(room.Id),
                    room.Id == currentRoomId,
                    room.Id == map.GoalId,
                    movable.Contains(room.Id),
                    // 🔴 `D-3`：房间层（旧图）没有"侦察态"这个来源 ⇒ 沿用二态映射
                    //    （`RevealedRoomIds` 在房间层就是"已走过"⇒ `Visited`）—— **不是**偷偷造第三态 ✓
                    revealedSet.Contains(room.Id)
                        ? Darkest.Gameplay.Sim.Run.RevealState.Visited
                        : Darkest.Gameplay.Sim.Run.RevealState.Unexplored));
                lane++;
            }
        }

        var links = map.Edges.Select(e => new SketchLink(e.From, e.To)).ToList();
        return new MapSketch(cells, links, remainingSegments, currentType ?? string.Empty);
    }

    /// <summary>
    /// 🔴 **(B) ③ 的适配器**：把主程序已就绪的**瓷砖网格**（`flow.TileWalk`）翻成表现层快照 ✓
    /// · 格 = 大方块（`Depth=x` · `Lane=y`）· 相邻非墙格之间的连线 = 走廊小方块串 ✓
    /// · `Movable` = **队伍所在格的 4 邻居**（能不能走由内核 `TryStepTile` 判：**返回 false ⇒ 状态零变化**）✓
    /// ⚠️ 本方法是**唯一读内核新类型**的地方；渲染类一行不改（这就是上一轮"渲染无关化"的目的）✓
    /// </summary>
    public static MapSketch FromTileWalk(
        Darkest.Gameplay.Sim.Run.DungeonGridDeriver.Derived tw,
        (int X, int Y) party,
        IReadOnlyList<int> revealed,
        int remainingSegments,
        Darkest.Gameplay.Sim.Run.DungeonTileKind here,
        Func<(int X, int Y), Darkest.Gameplay.Sim.Run.RevealState>? stateOf = null)
    {
        var revealedSet = new HashSet<int>(revealed);
        var cells = new List<SketchCell>();
        var links = new List<SketchLink>();

        int Id(int x, int y) => (y * tw.Grid.Width) + x;

        for (int y = 0; y < tw.Grid.Height; y++)
        {
            for (int x = 0; x < tw.Grid.Width; x++)
            {
                Darkest.Gameplay.Sim.Run.DungeonTileKind kind = tw.Grid.TileAt(x, y);
                if (kind == Darkest.Gameplay.Sim.Run.DungeonTileKind.Wall)
                {
                    continue; // 墙不画（DD 的迷宫就是"画出来的可走格"）✓
                }

                // 🔴🔴 `D-6`（2026-09-20）：**隐藏房在地图上【完全不显示】**。
                //
                //   DD 口径（`dungeon_layer_design.md §F3d` / wiki ④）：隐藏房不是"暗"（暗格还在图上，
                //   只是没内容），而是**根本不画** —— 玩家在侦察成功之前**不知道那一格存在** ⚠️
                //   ⇒ 与 `Wall` 同待遇：**跳过**（既不画格、也不加连线、更不加点击热区）。
                //
                //   🔴 **为什么必须在这里判、而不是"画成未知色"**：
                //      `Unexplored` 的格也是会画的（`MapUnknown` 色块）—— 若隐藏房也画成未知色，
                //      玩家只要**数格子**就能发现"这里多一块" ⇒ 隐藏房**立刻被看穿**（等于地图上明示）⚠️
                //   🔴 **揭示之后**（侦察成功 ⇒ `Scouted` ⇒ 本类读到 `Secret` 格 + 非 `Unexplored`）
                //      就会照常画出来（`MapScouted` 色）⇒ "揭示后成 rewards 房"在视觉上真的成立 ✓
                bool secretUndiscovered = kind == Darkest.Gameplay.Sim.Run.DungeonTileKind.Secret
                    && (stateOf?.Invoke((x, y)) ?? Darkest.Gameplay.Sim.Run.RevealState.Unexplored)
                        == Darkest.Gameplay.Sim.Run.RevealState.Unexplored;
                if (secretUndiscovered)
                {
                    continue; // 未揭示的隐藏房 ⇒ **连"未知格"都不给**（给了就等于告诉她这里有东西）⚠️
                }

                int roomId = tw.TileRoom.TryGetValue((x, y), out int r) ? r : -1;
                bool isCurrent = party.X == x && party.Y == y;
                bool isGoal = tw.Grid.Goal == (x, y);
                bool adjacent = Math.Abs(party.X - x) + Math.Abs(party.Y - y) == 1;

                // 🔴 `D-3`：**三态**优先由内核给出（`stateOf`）—— 它是唯一真值（含"视野临时可见"）✓
                //    ⚠️ 未接 `stateOf`（旧调用点/测试）⇒ 退化成**二态**：房间"走过"就 Visited，其余 Unexplored
                //       （这正是旧行为 ⇒ 既有调用点不接也不变）✓
                Darkest.Gameplay.Sim.Run.RevealState state = stateOf is not null
                    ? stateOf((x, y))
                    : revealedSet.Contains(roomId)
                        ? Darkest.Gameplay.Sim.Run.RevealState.Visited
                        : Darkest.Gameplay.Sim.Run.RevealState.Unexplored;

                cells.Add(new SketchCell(
                    Id(x, y), x, y, kind.ToString(),
                    state != Darkest.Gameplay.Sim.Run.RevealState.Unexplored,
                    isCurrent, isGoal, adjacent, state));

                // 连线只连"右/下"两个方向（避免重复），且两端都不是墙 ✓
                if (x + 1 < tw.Grid.Width && tw.Grid.TileAt(x + 1, y) != Darkest.Gameplay.Sim.Run.DungeonTileKind.Wall)
                {
                    links.Add(new SketchLink(Id(x, y), Id(x + 1, y)));
                }

                if (y + 1 < tw.Grid.Height && tw.Grid.TileAt(x, y + 1) != Darkest.Gameplay.Sim.Run.DungeonTileKind.Wall)
                {
                    links.Add(new SketchLink(Id(x, y), Id(x, y + 1)));
                }
            }
        }

        return new MapSketch(cells, links, remainingSegments, here.ToString(), party);
    }
}
