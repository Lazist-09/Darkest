using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求**按相位/职责**切）✓
/// ② 本文件 = **战斗 · E 区多功能框族**（建三页起步 / 切页 `SetMultiFunctionPage` / 内容刷新 `RefreshMultiFunctionContent`）✓
/// ③ 🔴 依赖主类私有成员/状态：`_eArea` · `_mfPanel` · `_mfTabs` · `_mfContent` · `_mfPage` · `_host` ·
///    `_bottomRow`（骨架优先/回落）· `DdTheme`（样式）；页面数据只读内核投影 ✓
/// ④ **只搬家、零行为改动**（页签模板实例化、只读口径一字未改）✓
/// </summary>
public partial class BattleUI : Control
{
    /// <summary>建 E 区多功能框（三页起步；旧 F1 浮层保留为开发工具，本框的【日志】页显示事件流尾部）。</summary>
    private void BuildMultiFunctionBox()
    {
        // 🔴 Godot 内置清单 ②（第二批：**容器 + 锚点**）：
        //    · 面板**贴右下角**（锚点 BottomRight + 负偏移）⇒ 与分辨率无关（不再写死 640,556）✓
        //    · 内部用 **VBox/HBox 容器**排布（页签一行 + 内容区）⇒ 子控件**不再各写 Position** ✓
        // 🔴 `#321`③：E 区 = 底栏**唯一 ExpandFill** 的分区 ⇒ 多功能框**创建时进 `_eArea`**
        //    （容器负责尺寸 ⇒ 不再写 `BottomRight` 锚点与负偏移）
        _mfPanel = new Panel { Name = "MultiFunctionBox" };
        _mfPanel.Modulate = Darkest.UI.DdTheme.PanelBgRaised;
        // 🔴 P5：**紫框分区** —— 同上一律只染边框（不 `Modulate`）✓
        if (_eArea is Control eCtl)
        {
            eCtl.AddThemeStyleboxOverride("panel",
                Darkest.UI.DdTheme.MakePanelStyle(Darkest.UI.DdTheme.PanelBgRaised.Lightened(0.22f), Darkest.UI.DdTheme.Mental)); // 🔴 用户：紫框太黑 ⇒ 底色提亮 22%（边框仍是紫）暗底
        }
        _eArea.CustomMinimumSize = new Vector2(0, 0); // 🔴 相机口径：E 区**可压缩到 0**（否则撑过右长条框 ⇒ 重叠）
        _eArea.AddChild(_mfPanel);

        var column = new VBoxContainer { Name = "MfColumn" };
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 4);
        // 🔴 P5（用户参考图②）：**紫框显式标题** —— 加在 E 区的**内层 VBox** 里（不是外层 Panel 上）
        //    ⚠️ 教训：加在 `_eArea` 上会与既有手工尺寸内容重叠 ⇒ 必须进**容器**才会自动排布 ✓
        _eAreaTitle = new Label { Name = "EAreaTitle", VerticalAlignment = VerticalAlignment.Center };
        _eAreaTitle.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
        _eAreaTitle.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextAccent);
        _eAreaTitle.AutowrapMode = TextServer.AutowrapMode.Off;   // 🔴 标题不换行、裁切（不撑宽 E 区）✓
        _eAreaTitle.ClipText = true;
        _eAreaTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _eAreaTitle.CustomMinimumSize = new Vector2(0, 0);
        column.AddChild(_eAreaTitle);

        // 🔴 用户要求：6 号位**集成在紫色多功能框里**（不再单占一个最右长条框）✓
        // 🔴 用户要求（2026-09-16）：**紫框里那个"多出来的框"删掉** ——
        //    5/6 号位**合并到左侧同一个长条框**里（两种模式都显示 ⇒ 6 号位不再看不见）✓
        _slotRight = null;

        _mfPanel.AddChild(column);

        // 🔴 P5：右侧 6 号位长条框（**向右靠齐**）—— 加在底栏最后 ✓
        // 🔴 用户要求（2026-09-16）：**底部最右框撤掉** ⇒ 6 号位集成进紫色多功能框（`BackSlot6InE`）✓
        // 🔴 已撤：6 号位不再挂底栏（集成在紫色多功能框内）✓

        var tabsRow = new HBoxContainer { Name = "MfTabs" };
        tabsRow.AddThemeConstantOverride("separation", 4);
        column.AddChild(tabsRow);

        // 🔴 `ui_spec.md` §1.2：**E 区多功能框 = 可切换分页**（DD 式）——
        //    规格列的是 详情 ／ 日志 ／ 序列 ／ 编成；我再加【地图】（片③ 用户点名要的）
        string[] tabs = { "详情", "日志", "序列", "编成", "地图" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            // 容器自动排布 ⇒ 只给"最小尺寸"，不写 Position ✓
            // 🔴 用户要求（2026-09-17）：**重复元素抽模板** ⇒ 多功能页签（4~5 处同构）实例化 `mf_tab.tscn`
            //    ⚠️ 场景缺失 ⇒ 回落代码构建（不崩、不静默）✓
            Button b = Darkest.UI.MfTabButtonTemplate.TryCreate(tabs[i])
                ?? new Button { Text = tabs[i], CustomMinimumSize = new Vector2(80, 26) };
            b.Pressed += () => SetMultiFunctionPage(idx);
            tabsRow.AddChild(b);
            _mfTabs.Add(b);
        }

        _mfContent = new Label
        {
            Name = "MfContent",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, // 容器里"占满剩余高度"（不再写死 110）✓
        };
        _mfContent.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
        column.AddChild(_mfContent);

        // 地图页与文本页**共占同一内容区**（同一容器位置 ⇒ 切换时不需要各自算坐标）✓
        _mfMap = new Darkest.UI.BattleMiniMap
        {
            Name = "MfMap",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        // 🔴 主程序 `DUNGEON-IS-GRID` 的 (A)：**把"地图页"升级成【格子主画面】** ——
        //    拓扑模式（行走地牢）用 `WalkMapView`（房=大方块／廊=小方块，DD 式），线性远征仍用原迷你图 ✓
        _mfMapWalk = new Darkest.UI.WalkMapView { Name = "MapPageWalkMap", Visible = false };
        column.AddChild(_mfMapWalk);

        _mfMap.Visible = false;
        column.AddChild(_mfMap);

        SetMultiFunctionPage(0);
    }

    /// <summary>🔴 取证（容器/锚点）：E 区面板的锚点 + 页签是否由**容器**排布（页签同 y、x 递增）。</summary>
    public string ContainerAudit()
    {
        string tabPos = string.Join(" ", _mfTabs.Select(b => $"({(int)b.Position.X},{(int)b.Position.Y})"));
        bool rowLike = _mfTabs.Count >= 2
                       && _mfTabs.All(b => Math.Abs(b.Position.Y - _mfTabs[0].Position.Y) < 0.5)
                       && _mfTabs.Zip(_mfTabs.Skip(1)).All(p => p.Second.Position.X > p.First.Position.X);
        return $"E 区：锚点 L={_mfPanel.AnchorLeft}/T={_mfPanel.AnchorTop}/R={_mfPanel.AnchorRight}/B={_mfPanel.AnchorBottom}" +
               $"（右下=1/1）　页签坐标 {tabPos}　容器排布={(rowLike ? "✅ 同行且递增（HBox 生效）" : "🔴 非容器排布")}";
    }

    /// <summary>🔴 切换 E 区分页（**真实按钮走这里**；冒烟也走同一入口）。</summary>
    public void SetMultiFunctionPage(int page)
    {
        _mfPage = page;

        // 🔴 P5：紫框显式标题（第 0 页 = 角色详情；其余 = 多功能：…）✓
        if (_eAreaTitle is not null)
        {
            _eAreaTitle.Text = page == 0
                ? "角色详情"
                : "多功能：" + (page switch { 1 => "日志", 2 => "行动序列", 3 => "编成", 4 => "地图", _ => "—" });
        }
        if (_mfContent is not null)
        {
            _mfContent.Visible = page is >= 0 and <= 3; // 前四页共用文本区
        }

        if (_mfMap is not null)
        {
            Darkest.Gameplay.Sim.Run.ExpeditionFlow? mFlow = Darkest.Gameplay.Scene.ExpeditionContext.Flow;
        // 🔴 **冒烟专用**入口（`--tile-walk` / `--tile-step`）：**正式开关仍归宿主**（进地牢时调一次）——
        //    我加这两条只为把"瓷砖主画面 + 点格走格"**先自证到位**（红线 25：改了交互入口 ⇒ 证据链重走）✓
        if (mFlow is not null && Array.Exists(OS.GetCmdlineArgs(), x => x == "--tile-walk") && !mFlow.TileWalkEnabled)
        {
            // 📌 主程序 2026-09-21 提醒：此处**硬写 30**（仅冒烟验证用）；正式开关由宿主按 −tuning.light.node_step 调 ⇒ 值若变会不一致 ⚠️
            mFlow.EnableTileWalk(30);
            GD.Print("[UI 走格·冒烟] 已开 `EnableTileWalk(30)`（**仅验证用**；正式开关应由宿主调）✓");
        }

        bool topology = mFlow?.IsTopologyMode == true && mFlow.Map is not null;
        if (_mfMapWalk is not null)
        {
            // 🔴 主程序 (A)：拓扑模式下**格子主画面**替代线性迷你图（同一页签内择优显示）✓
            _mfMapWalk.Visible = page == MapPageIndex && topology;
            if (_mfMapWalk.Visible && mFlow is not null)
            {
                // 🔴 主程序 (A)：把**相邻未探索房间**设为可点（点击 ⇒ 走一格）✓
                // 🔴 (B) ③ 落地：**走格开启时用【瓷砖网格主画面】**（主程序 `TileWalk` 已就绪）——
                //    格=大方块、相邻可走格之间=走廊小方块；点击格 ⇒ `TryStepTile(dx,dy)`
                //    （**返回 false = 墙/越界 ⇒ 内核状态零变化**：我只重绘，不改状态）✓
                if (mFlow.TileWalkEnabled && mFlow.TileWalk is not null)
                {
                    Darkest.UI.MapSketch ts = Darkest.UI.WalkMapView.FromTileWalk(
                        mFlow.TileWalk, mFlow.TilePosition, mFlow.RevealedRoomIds,
                        mFlow.RemainingSegmentsToGoal, mFlow.TileHere, mFlow.TileStateAt);
            // 🔴 `D-3`：三态读数与文字速写**必须打出来**（画面在 headless 看不见 ⇒ 靠文字自证三态可分辨）✓
            GD.Print($"[UI 瓷砖] ✅ 主画面（引擎内置 TileMapLayer）：格 {ts.Cells.Count} ／ 连线 {ts.Links.Count}" +
                     $"　已揭示 {ts.Cells.Count(c => c.Revealed)} ／ {ts.Cells.Count}" +
                     $"（其中只有轮廓 {ts.Cells.Count(c => c.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted)}" +
                     $" · 已看清 {ts.Cells.Count(c => c.State == Darkest.Gameplay.Sim.Run.RevealState.Visited)}）");
            GD.Print($"[UI 瓷砖·三态速写] {Darkest.UI.WalkMapView.SketchText(ts)}");

                    // 🔴 **网格字符留档**（`DungeonGrid.ToRows` 的**生产消费点**，2026-09-20 接线）——
                    //    主画面是图形（headless 看不见）⇒ 把网格按**字符行**打一遍：可审计、可比对、可复现 ✓
                    //    ⚠️ 只在**首次**（`_lastTileRowsSketch` 空）打一次，避免切页签时刷屏 ✓
                    string rowsSketch = string.Join(" / ", mFlow.TileWalk.Grid.ToRows());
                    if (rowsSketch != _lastTileRowsSketch)
                    {
                        _lastTileRowsSketch = rowsSketch;
                        GD.Print($"[UI 瓷砖·网格留档] {mFlow.TileWalk.Grid.Width}×{mFlow.TileWalk.Grid.Height} 字符网格：" +
                                 $"`{rowsSketch}`（墙/地板/起点/终点 ⇒ 与 `TileWalk` 同源）✓");
                    }
                    _mfMapWalk.MovableRooms = ts.Cells.Where(c => c.Movable).Select(c => c.Id).ToList();
                    _mfMapWalk.OnRoomClicked = id =>
                    {
                        Darkest.UI.SketchCell? cell = ts.Cells.FirstOrDefault(c => c.Id == id);
                        if (cell is null)
                        {
                            return;
                        }

                        int dx = cell.Depth - mFlow.TilePosition.X;
                        int dy = cell.Lane - mFlow.TilePosition.Y;
                        // 🔴 `D-3` / `D-1`：**走之前**先记下"这格站过没" —— 因为走完它就必然变"站过"，
                        //    事后读只能得到 true（会把"第一次探索"误报成"回头"）⚠️
                        bool wasVisited = mFlow.VisitedTileAt((cell.Depth, cell.Lane));
                        bool moved = mFlow.TryStepTile(dx, dy);
                        GD.Print($"[UI 走格] 点格 ({cell.Depth},{cell.Lane}) ⇒ `TryStepTile({dx},{dy})`={moved}" +
                                 $"（{(wasVisited ? "回头" : "新格")}　现在 {mFlow.TilePosition}：{mFlow.TileHere}" +
                                 $"　已走 {mFlow.TileStepsTaken} 格　三态 {mFlow.TileStateAt((cell.Depth, cell.Lane))}）✓");

                        // 🔴🔴 `D-5`：**走完一格 ⇒ 检查队伍是否饿了**（`HasPendingHunger` 是唯一入口，
                        //    它同时确认"名册台账已建" ⇒ 首场战斗之前**不会**弹，因为那时无人可结算）✓
                        //    DD 铁律：饥饿触发后**必须二选一**（不能拖、不能跳）⇒ 立刻弹面板 ✓
                        if (moved && TryOpenHunger())
                        {
                            GD.Print("[UI 走格] 🔴 队伍**饿了** ⇒ 已弹【吃 / 不吃】面板（必须选）✓");
                        }
                        else if (moved)
                        {
                            HostDungeonPanels(); // 没饿也要刷新读数（光照/位置/投影都变了）✓
                        }
                    };
                    _mfMapWalk.Refresh(ts);

                    // 🔴 冒烟自证（`--tile-step`）：**走我真实接的回调**（`OnRoomClicked` ⇒ TryStepTile）✓
                    if (Array.Exists(OS.GetCmdlineArgs(), x => x == "--tile-step"))
                    {
                        Darkest.UI.SketchCell? mv = ts.Cells.FirstOrDefault(c => c.Movable);
                        if (mv is null)
                        {
                            GD.Print("[UI 走格·冒烟] 没有可移动的相邻格 ⇒ 无事可做（如实报）✓");
                        }
                        else
                        {
                            GD.Print($"[UI 走格·冒烟] 点格 ({mv.Depth},{mv.Lane})（可移动）⇒ 走真实回调");
                            _mfMapWalk.OnRoomClicked?.Invoke(mv.Id);
                        }
                    }
                }
                else
                {                System.Collections.Generic.List<int> movable = mFlow.AdjacentUnexplored().Select(r => r.Id).ToList();
                _mfMapWalk.MovableRooms = movable;
                _mfMapWalk.OnRoomClicked = rid =>
                {
                    var outcome = mFlow.StepTo(rid);
                    GD.Print($"[UI 行走] 点击房间 {rid} ⇒ `StepTo` 结果={outcome}（当前房间 {mFlow.CurrentRoomId}：{mFlow.CurrentRoomType}）剩余 {mFlow.RemainingSegmentsToGoal} 段 ✓");

                    // 🔴 **已处理过的格 ⇒ 不再触发遭遇**（`IsRoomResolved` 的**生产消费点**，2026-09-20 接线）——
                    //    口径（`#352` / `retreat.md §2`）：**撤退后那格算"避过"** ⇒ 走回它**不重打** ✓
                    //    此前本方法只推进位置、**从不问"这格是否已处理"** ⇒ 死字段 `_resolvedRooms` 形同虚设 ⚠️
                    if (outcome.Moved && mFlow.IsRoomResolved(rid))
                    {
                        GD.Print($"[UI 行走] 🔴 房间 {rid} **已处理过**（撤退后标记）⇒ **不触发遭遇/战斗**，" +
                                 "只把位置挪过去（`#352`：撤退后不重打）✓");
                    }
                    else if (outcome.Moved && !string.IsNullOrEmpty(mFlow.EventNodeIdForRoom(rid)) && mFlow.CurrentRoomType == "event")
                    {
                        // 🔴 **事件房 ⇒ 开内容面板**（`EventNodeIdForRoom` + `EnterRoomCurio` 的生产消费点）✓
                        GD.Print($"[UI 行走] 走进事件房 {rid} ⇒ 取事件节点 `{mFlow.EventNodeIdForRoom(rid)}` ⇒ 开房间内容 ✓");
                        EnterRoomCurio(rid, isBranch: false);
                    }
                };
                _mfMapWalk.Refresh(mFlow.Map!, mFlow.CurrentRoomId, mFlow.RevealedRoomIds, movable,
                    mFlow.RemainingSegmentsToGoal, mFlow.CurrentRoomType);
                }
                if (_mfMapWalk.LastSketch != _lastMapPageSketch)
                {
                    _lastMapPageSketch = _mfMapWalk.LastSketch;
                    GD.Print("[UI 地图页·格子主画面] " + _mfMapWalk.LastSketch.Replace("\n", " ／ "));
                }
            }
        }

        _mfMap.Visible = page == MapPageIndex && !topology;
            if (page == MapPageIndex)
            {
                _mfMap.QueueRedraw(); // 进战斗时地图已定，重绘一次即可（只读）
            }
        }

        RefreshMultiFunctionContent();
        RefreshProgressLabel();

        // 页签高亮（当前页亮、其余暗）
        for (int i = 0; i < _mfTabs.Count; i++)
        {
            _mfTabs[i].Modulate = i == page ? Darkest.UI.DdTheme.Highlight : Darkest.UI.DdTheme.Disabled;
        }

        GD.Print($"[片③] E 区多功能框 ⇒ 切到【{_mfTabs.ElementAtOrDefault(page)?.Text ?? "?"}】页");
    }

    /// <summary>详情 / 日志 / **序列** / **编成** 四页的文本（都读**同一份事实来源**，不另造数据）。</summary>
    private void RefreshMultiFunctionContent()
    {
        if (_mfContent is null || _host is null)
        {
            return;
        }

        if (_mfPage == 1)
        {
            IReadOnlyList<Darkest.Core.Events.BattleEvent> ev = _host.Director.Log.Events;
            int take = System.Math.Min(7, ev.Count);
            var lines = new List<string> { $"【日志】尾部 {take} 条（共 {ev.Count} 条；F1 仍可开全屏日志）" };
            for (int i = ev.Count - take; i < ev.Count; i++)
            {
                lines.Add($"　{CombatLogText.Line(ev[i])}");
            }

            _mfContent.Text = string.Join("\n", lines);
            return;
        }

        if (_mfPage == 2)
        {
            // 🔴 **序列**：本回合行动顺序（`Director.LastRoundOrder`；与"顶部回合条"同源）
            var lines = new List<string> { $"【序列】回合 {_host.Director.Round}　行动顺序：" };
            IReadOnlyList<UnitId> order = _host.Director.LastRoundOrder;
            if (order.Count == 0)
            {
                lines.Add("　（本回合还没有人行动）");
            }
            else
            {
                for (int i = 0; i < order.Count; i++)
                {
                    UnitId id = order[i];
                    bool mine = _host.Director.Player.UnitAtPosition(id) is not null;
                    int pos = _host.Director.Player.UnitAtPosition(id) ?? _host.Director.Enemy.UnitAtPosition(id) ?? 0;
                    lines.Add($"　{i + 1}. {NameOf(id.Value)}（{(mine ? "我" : "敌")}·{pos}）");
                }
            }

            _mfContent.Text = string.Join("\n", lines);
            return;
        }

        if (_mfPage == 3)
        {
            // 🔴 **编成**：双方站位占用（读 `FormationBoard`）
            var lines = new List<string> { "【编成】站位占用（我方 4→1 ／ 支援 5·6 ／ 敌方 1→4）" };
            lines.Add("　我方：" + DescribeSide(_host.Director.Player));
            lines.Add("　敌方：" + DescribeSide(_host.Director.Enemy));
            lines.Add("　（只读：编成改动在远征侧，不在战斗里）");
            _mfContent.Text = string.Join("\n", lines);
            return;
        }

        _mfContent.Text =
            "【详情】点战场上的单位 ⇒ 这里显示其详情（DD 式：详情 ／ 日志 ／ 序列 ／ 编成 ／ 地图）。\n" +
            "　· 地图**只读**（不能在这里改路线）· 其余页同样只读。";
    }
}
