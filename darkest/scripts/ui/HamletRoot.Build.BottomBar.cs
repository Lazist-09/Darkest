using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.Build.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 底栏建树**：骨架优先（`BottomBar` ／ `BottomRow` ，缺失回落代码建）＋
///    资源条（`_resourceBar`） ＋ 比例空隙（DD 1:1 #1d：heirloom 0.177 ／ embark 0.3927） ＋
///    `☰ 菜单`（`_menuButton` ⇒ `OpenHamletMenu`） ＋ 全屏唯一大红 `EMBARK`（`_embark` ⇒ 再出发）✓
/// ③ 🔴 依赖主类私有成员（`HamletRoot.cs` 字段区）： `_resourceBar` ／ `_menuButton` ／ `_embark` ／
///    `_status` ／ `_hint` ／ `_buildingInfo` ／ `_upgradeStatus` ／ `_saniStatus` ／ `_rosterCount` ／
///    `_rosterTitle` ／ `_banner` （ClipText 族）✓
///    依赖公有面： `OpenHamletMenu` ／ Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon ／
///    Darkest.UI.MainMenuRoot.BattleScene ／ Darkest.UI.DdTheme ／ Darkest.UI.HamletSkeleton✓
/// ④ **只搬家、零行为改动**（骨架采用／回落分支一字未改；调用点在 `_Ready()` 原位置）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 底栏建树（骨架优先）：资源条 ＋ 比例空隙 ＋ `☰ 菜单` ＋ `EMBARK` ✓
    /// 🔴 顺序纪律：调用点**必须**在建筑详情／右栏建树**之后**、 Refresh() **之前**（与原 `_Ready()` 同序）✓
    /// </summary>
    private void BuildBottomBar(Darkest.UI.HamletSkeleton? skel, VBoxContainer rootCol)
    {
        // ---- 底栏：资源条 + Embark（全屏唯一大红）----
        // 🔴 骨架优先（2026-09-17）：底栏容器也从骨架取（节点名 BottomBar / BottomRow 保持不变）✓
        //    ⚠️ 缺失 ⇒ 回落代码构建；子项（资源条/菜单/出发）仍由代码追加 ✓
        PanelContainer bottomPanel = skel?.GetNodeOrNull<PanelContainer>("HamletMargin/HamletRootCol/BottomBar")
            ?? new PanelContainer { Name = "BottomBar" };
        if (bottomPanel.GetParent() is null)
        {
            rootCol.AddChild(bottomPanel);
        }

        HBoxContainer bottomRow = skel?.GetNodeOrNull<HBoxContainer>("HamletMargin/HamletRootCol/BottomBar/BottomRow")
            ?? new HBoxContainer { Name = "BottomRow" };
        if (bottomRow.GetParent() is null)
        {
            bottomRow.AddThemeConstantOverride("separation", 12);
            bottomPanel.AddChild(bottomRow);
        }
        _resourceBar = new Label
        {
            Name = "HamletResourceBar",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,   // 🔴 DD 1:1 #1d：资源条**靠左**（DD 340/1920 ≈ 左下）✓
            VerticalAlignment = VerticalAlignment.Center,
        };
        // P1.3（DD town.layout）：heirloom 340/1920 = **0.177** ⇒ 资源条前加左空（比例表达，不写像素）         bottomRow.AddChild(new Control { Name = "BottomPadLeft", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.177f });
        bottomRow.AddChild(_resourceBar);
        bottomRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });   // 🔴 DD 1:1 #1d：左弹性空隙（把 Embark 顶到**底部居中** = DD 754/1920 ≈ 39% x）✓
        bottomRow.AddChild(new Control { Name = "BottomPadMid", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.2157f });   // P1.3：DD embark 0.3927 − heirloom 0.177 = 0.2157 ✓
        // 🔴 P2（用户参考图①）：**最下方资源 UI 可点开【二级菜单】** —— 库存/角色详情/建筑都从这里进 ✓
        _menuButton = new Button
        {
            Name = "HamletMenuButton",
            Text = "☰ 菜单",
            CustomMinimumSize = new Vector2(120, 44),
        };
        _menuButton.Pressed += OpenHamletMenu;

        // 🔴 相机 1280 口径（规则①）：**把所有单行长文本 Label 设为"裁切+省略号"** ——
        //    否则它们的最小宽（= 文本宽）会把整屏撑宽 ⇒ 名册被切（实测 HamletMargin 1397 > 1280）✓
        foreach (Label l in new[] { _status, _hint, _buildingInfo, _upgradeStatus, _saniStatus, _rosterCount, _rosterTitle, _resourceBar, _banner }) // 🔴 `_banner` 补进（实测它宽 1348px，是整屏 1373 的元凶）
        {
            l.AutowrapMode = TextServer.AutowrapMode.Off;
            l.ClipText = true;
            l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        bottomRow.AddChild(_menuButton);

        var embark = new Button
        {
            Name = "Embark",
            Text = "再出发（远征）· EMBARK",
            CustomMinimumSize = new Vector2(280, 44),
            Modulate = Darkest.UI.DdTheme.Danger, // 🔴 `§14.4`：颜色不得在节点上硬写 ⇒ 走语义色（Embark = 危险红：出发是要付代价的）
        };
        embark.Pressed += () =>
        {
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4①：再出发 ⇒ 宿主进地牢 ✓
            GetTree().ChangeSceneToFile(Darkest.UI.MainMenuRoot.BattleScene);
        };
        bottomRow.AddChild(embark);
        bottomRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });   // 🔴 DD 1:1 #1d：右弹性空隙（两侧等权 ⇒ Embark 居中）✓
        _embark = embark;
    }
}
