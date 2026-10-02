using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求**按相位/职责**切）✓
/// ② 本文件 = **战斗 · 建树总装**（`Build()` 一次性搭出顶栏/战场/底栏三行容器树；骨架优先、缺失回落）✓
///    三行**细构建**（`BuildTopRow`/`BuildBattlefield`/`BuildBottomRow`）已按 M11 ① 拆入 `BattleUI.Build.Rows.cs`（2026-10-02 · 只搬家零行为）✓
/// ③ 🔴 依赖主类私有成员/状态：`_uiRoot`/`_bg`/`_motionLayer`/`_vignette`/`_topRow`/`_midRow`/`_bottomRow`/
///    `_topBarSkel`/`_bottomBarSkel`/`_playerCards`/`_enemyCards`/`_playerSupport`/`_skillBar`/`_actionButtons`/`_orderBox` 等 ✓
/// ④ **只搬家、零行为改动**（骨架优先/回落分支一字未改）✓
/// </summary>
public partial class BattleUI : Control
{
    private void Build()
    {
        // 🔴 `ui_spec §14.3` + `#321`③ 分区表 —— **先立容器树，再让控件"创建时进容器"**（`#319`）
        //    A 顶栏：回合·支援点 │ 行动顺序头像 │ 进度 │ 日志 │ 撤退
        //    主体  ：我方 战4·3·2·1（前排）＋ 辅5·6（支援位） ←→ 敌方 1·2·3·4（**均分、不 ExpandFill**）
        //    底栏  ：**C 区**（当前轮次角色面板 + **技能栏在 C 区内**，固定宽 ~30%，不 ExpandFill）
        //            ＋ **E 区**（多功能框，**唯一 ExpandFill**）
        //    ⚠️ 为什么必须"创建时进容器"（而不是建完再搬）：主程序实测 —— 事后 `Reparent()`/`RemoveChild+AddChild`
        //       在"边遍历边搬"时触发引擎断言 `Condition "p_child->data.parent != this" is true` ⇒ 树状态不一致。
        _uiRoot = new Control { Name = "UiRoot" };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Darkest.UI.DdTheme.Apply(_uiRoot);
        AddChild(_uiRoot);

        // 🔴 Track 3（DD `fe_flow/overlays`）：**Overlay 层压在最后**，一切模态/悬停都住这里 ✓
        _overlay = Darkest.UI.OverlayLayer.TryInstantiate();
        if (_overlay is not null)
        {
            _uiRoot.AddChild(_overlay);
        }


        // 🔴 背景：世界层暗色底（落在相机视口、压在角色之下），**不在 UI(CanvasLayer) 层** ⇒
        //   不会盖住 HUD，也不会盖住世界单位（单位运行时加入世界层、位于 bg 之上）✓
        //   （旧实现把满屏不透明 bg 放在 UI 层 ⇒ 直接遮住世界单位；现归世界层、随相机视口定位）
        Camera2D? cam = GetParent()?.GetParent()?.GetNode<Camera2D>("Camera2D");
        var bg = new Panel { Name = "BattleBg" };
        _bg = bg; // 🔴 `#327` S1：背景属于【必须存活的骨架】（其 id 在进战斗前后应不变）
        bg.Modulate = Darkest.UI.DdTheme.BgDeep;
        Vector2 viewSize = GetViewport().GetVisibleRect().Size;
        Vector2 camCenter = cam is not null ? cam.Position : viewSize * 0.5f;
        bg.Position = camCenter - viewSize * 0.5f;
        bg.Size = viewSize;
        Node worldRoot = GetParent()?.GetParent() ?? this;
        worldRoot.AddChild(bg);

        var uiMargin = new MarginContainer { Name = "BattleMargin" };
        uiMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        uiMargin.AddThemeConstantOverride("margin_left", 10);
        uiMargin.AddThemeConstantOverride("margin_top", 4);   // 🔴 相机 720 口径：8→4（战斗内容需求曾 723，差 3px）
        uiMargin.AddThemeConstantOverride("margin_right", 10);
        uiMargin.AddThemeConstantOverride("margin_bottom", 4);   // 🔴 同上
        _uiRoot.AddChild(uiMargin);

        var uiCol = new VBoxContainer { Name = "BattleCol" };
        uiCol.AddThemeConstantOverride("separation", 6);
        uiMargin.AddChild(uiCol);

        var topPanel = new PanelContainer { Name = "TopRow" };
        uiCol.AddChild(topPanel);
        // 🔴 骨架优先（2026-09-17）：战斗顶栏容器用骨架（三区位置/间距/占比可在编辑器改）✓
        _topBarSkel = Darkest.UI.BattleTopBarSkeleton.TryInstantiate();
        HBoxContainer topRow;
        if (_topBarSkel is not null)
        {
            _topBarSkel.Name = "TopRowBox";
            topPanel.AddChild(_topBarSkel);
            topRow = _topBarSkel;
        }
        else
        {
            topRow = new HBoxContainer { Name = "TopRowBox" };
            topRow.AddThemeConstantOverride("separation", 4);
            topPanel.AddChild(topRow);
        }

        _topRow = topRow;

        var midPanel = new PanelContainer { Name = "MidRow", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        uiCol.AddChild(midPanel);
        var midRow = new Control { Name = "StageLayer", SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };   // 🔴 相机口径：不参与垂直拉伸   // 🔴 DD 1:1 ④-3c：中段**底对齐**（DD overlays y=680/1080 = 63.0% ⇒ 立绘站在低处；容器语义 = ShrinkEnd）✓
        midRow.AddThemeConstantOverride("separation", 8);
        var midCol = new VBoxContainer { Name = "MidCol" };   // P4-c(A)：DD overlays y=680/1080=0.6297 ⇒ 用顶部空档比例表达（不写像素）
        midCol.AddThemeConstantOverride("separation", 0);
        midCol.AddChild(new Control { Name = "MidPadTop", SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.6297f });
        midCol.AddChild(midRow);
        midPanel.AddChild(midCol);
        _midRow = midRow;

        var bottomPanel = new PanelContainer { Name = "BottomRow", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        uiCol.AddChild(bottomPanel);
        _bottomBarSkel = Darkest.UI.BattleBottomBarSkeleton.TryInstantiate();
        if (_bottomBarSkel is not null)
        {
            // 🔴 骨架根 `BottomRowBox`(HBox) 即真正底栏容器；`_bottomRow` 必须指向它。
            //    之前指向空 HBox：死代码 + 隐患 —— DungeonHost / Refresh 尺寸读取都依赖 `_bottomRow`，
            //    若指向空容器，DungeonHost 会进死节点、底栏尺寸读数为 0。
            _bottomBarSkel.Name = "BottomRowBox";
            bottomPanel.AddChild(_bottomBarSkel);
            _bottomRow = _bottomBarSkel;
        }
        else
        {
            // 回落：手建底栏（保持原分离逻辑，确保 `_bottomRow` 非空、DungeonHost 有家可归）✓
            var bottomRow = new HBoxContainer { Name = "BottomRowBox", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            bottomRow.AddThemeConstantOverride("separation", 10);
            bottomPanel.AddChild(bottomRow);
            _bottomRow = bottomRow;
        }

        BuildTopRow();
        BuildBattlefield();
        BuildBottomRow();

        // 🔴 屏幕空间覆盖层（状态托盘 / 地图角 / 攻击覆盖位 / 怪物面板 / 换位按钮 / 库存网格）：
        //    这些都是**全屏坐标**（y 0.078~0.676），绝不能留在底栏 HBox —— 否则锚点只会对到底栏那一小块，
        //    整片托盘被压成一坨。挂到 FullRect 的 `_uiRoot` 上，锚点才能对到整块 1920×1080 画布。
        var overlayScene = GD.Load<PackedScene>("res://scenes/ui/battle_overlay.tscn");
        if (overlayScene is not null)
        {
            var overlay = overlayScene.Instantiate<Control>();
            overlay.Name = "BattleOverlay";
            _uiRoot.AddChild(overlay);
            _statusTray = overlay.GetNodeOrNull<Control>("StatusTray");
            GD.Print("[BattleUI] ✅ 战斗屏幕空间覆盖层就位（状态托盘回到全屏坐标，不再被底栏 HBox 压缩）");
        }

        // 结算 / 开发者日志 = **满屏不透明模态**（挂 `_uiRoot`：它是真 `Control` ⇒ `FullRect` 锚点算得出满屏 ✓）
        _resultLabel = MakeOpaqueModal("ResultPanel", out _resultPanel);
        RaidResultsSkeleton.AttachInto(_resultPanel);   // 阶段2：结算屏按 DD raid_results 落位（色块占位）
        _devLogLabel = MakeOpaqueModal("DevLogPanel", out _devLogPanel);

        // 🔴 `ui_spec §12.1` **动效层**（满屏 + 鼠标穿透）：瞬态 VFX（伤害数字 / 暗角）画在它上面。
        //    ⚠️ 它挂在 `_uiRoot` 上而**不在容器树里**（不参与布局）；`LayoutAudit` 按口径**跳过 `MotionLayer`**
        //       —— 瞬态特效**按设计**会短暂叠在卡片上，那不是"布局重叠"（口径见 `LayoutAudit` 注释）✓
        _motionLayer = UiMotion.MakeLayer("MotionLayer");
        _uiRoot.AddChild(_motionLayer);
        _motionLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // 🔴 `§12.3`：暗角/闪白 = **满屏 `ColorRect` + `ShaderMaterial`**（放文件即生效；缺则退回纯色罩 + 留痕）
        _vignette = UiMotion.MakeOverlay("VignetteOverlay");
        _motionLayer.AddChild(_vignette);
        _vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // 🔴 `ui_spec §12.2` **音效**：注入播放宿主（占位音为程序生成 ⇒ **音源缺失也能跑**）✓
        Darkest.UI.UiSfx.Attach(_uiRoot);

        GD.Print("[BattleUI] 容器树就绪：顶栏／主体（我方 4+2 ←→ 敌方 4）／底栏（C 区含技能栏 ＋ E 区多功能框）" +
                 " ⇒ 控件**创建时进容器** ✓");
    }

}
