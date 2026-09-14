using System.Collections.Generic;
using System.Linq;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **战斗界面的小地图**（`tasks/ui_three_screens.md` §3）—— 放在 **E 区多功能框**的【地图】分页里。
///
/// 三条纪律：
/// ① 🔴 **只读**：**不可点**（DD 也是只显示）—— 避免"在战斗里改路线"的越权 ✓
/// ② 数据来源：跨场景走 `ExpeditionContext.Flow`（与 `PendingAmbush` 同法）⇒ 复用**同一趟**的地图与探索状态 ✓
/// ③ 显示：**房间方块 + 走廊 + 当前「▶」+ 已探索变暗 + 可走高亮 + 队伍位置（火把）** ✓
/// </summary>
public partial class BattleMiniMap : Control
{
    private const float CellW = 26f;
    private const float RowYMain = 62f;
    private const float RowYBranch = 88f;

    /// <summary>
    /// 🔴 **可断言的摘要**（headless 冒烟用；地图不靠截图取证）：
    /// 房间数 ／ 走廊数 ／ 已探索数 ／ 当前房间 ／ 段数 ／ 是否已达终点 —— 与远征侧读数**同源**。
    /// </summary>
    public string Describe()
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null || !flow.IsTopologyMode)
        {
            return "mini-map: 无地图（不在拓扑远征里）";
        }

        int visited = flow.Map.Rooms.Count(r => flow.HasVisited(r.Id));
        int reachable = flow.AdjacentUnexplored().Count;
        return $"mini-map: 房间 {flow.Map.Rooms.Count} ／ 走廊 {flow.Map.Edges.Count} ／ " +
               $"已探索 {visited} ／ 当前 {flow.CurrentRoomId} ／ 可走 {reachable} ／ 段 {flow.StepsDone} ／ " +
               $"终点 {flow.ReachedGoal}";
    }

    public override void _Draw()
    {
        ExpeditionFlow? flow = ExpeditionContext.Flow;
        if (flow is null || !flow.IsTopologyMode)
        {
            // 🔴 策划 `#324` 裁定：线性模式的战斗地图 = **显示【短说明】**（不是隐藏）——
            //    "隐藏" ⇒ 玩家看到"本该有东西却空着"的位置，无法解释（红线 21 不留不可解释的状态）✓
            //    🔴 措辞按裁定用**首选短句**；颜色用**弱化灰**（`§14.4` 四色之一）—— 那是"提示"，不是"告警" ✓
            DrawString(ThemeDB.FallbackFont, new Vector2(8, 20), "线性远征：无地图",
                HorizontalAlignment.Left, -1, 12, Darkest.Ui.DdTheme.Disabled);
            return;
        }

        ExpeditionMap map = flow.Map;
        var rooms = map.Rooms.OrderBy(r => r.Id).ToArray();
        var pos = new Dictionary<int, Vector2>();
        for (int i = 0; i < rooms.Length; i++)
        {
            MapRoom r = rooms[i];
            pos[r.Id] = new Vector2(16 + (i * CellW), r.IsBranch ? RowYBranch : RowYMain);
        }

        // 走廊（先画线，方块压在上层）
        foreach (MapEdge e in map.Edges)
        {
            if (pos.TryGetValue(e.From, out Vector2 a) && pos.TryGetValue(e.To, out Vector2 b))
            {
                DrawLine(a + new Vector2(5, 5), b + new Vector2(5, 5), Darkest.Ui.DdTheme.MapEdge, 1.5f);
            }
        }

        int current = flow.CurrentRoomId;
        var reachable = flow.AdjacentUnexplored().Select(r => r.Id).ToHashSet();
        foreach (MapRoom r in rooms)
        {
            Vector2 p = pos[r.Id];
            bool visited = flow.HasVisited(r.Id);
            bool here = r.Id == current;
            Color fill = here ? Darkest.Ui.DdTheme.MapCurrent                 // 当前房间（队伍位置）
                : reachable.Contains(r.Id) ? Darkest.Ui.DdTheme.MapReachable // 可走高亮
                : visited ? Darkest.Ui.DdTheme.MapVisited                   // 已探索变暗
                : Darkest.Ui.DdTheme.MapUnknown;                           // 未探索
            DrawRect(new Rect2(p, new Vector2(11, 11)), fill, filled: true);
            DrawRect(new Rect2(p, new Vector2(11, 11)), Darkest.Ui.DdTheme.MapFrame, filled: false, width: 1f);

            if (here)
            {
                // 🔴 队伍位置：火把（小黄点 + 「▶」）
                DrawCircle(p + new Vector2(5.5f, -4f), 2.5f, Darkest.Ui.DdTheme.TeamDot);
                DrawString(ThemeDB.FallbackFont, p + new Vector2(1f, 9f), "▶", HorizontalAlignment.Left, -1, 9);
            }

            DrawString(ThemeDB.FallbackFont, p + new Vector2(1f, 22f), r.Id.ToString(),
                HorizontalAlignment.Left, -1, 9);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(16, 14),
            $"段 {flow.StepsDone}　当前房间 {current}　已达终点 {flow.ReachedGoal}　" +
            $"(■ 当前 ▶ ／ ■ 可走 ／ ■ 已探索 ／ ■ 未探索)",
            HorizontalAlignment.Left, -1, 11);
    }
}
