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
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求按职责切）✓
/// ② 本文件 = **战斗 · 模态与单位详情族**（`TitleLabel` 标题工厂 · `MakeOpaqueModal` 不透明模态工厂（必带 ✕）· 
///    `ShowUnitDetail` 单位详情 · `PressCard` · `ToggleDevLog` 开发者日志浮层）✓
/// ③ 🔴 依赖主类私有成员/状态：`_uiRoot`（模态挂载点；未就绪则留痕不挂，2026-09-21 守卫）· `_host` ·
///    `_devLogPanel` · `_detailText`（如存在）· `DdTheme` 样式 ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
    /// <summary>分区小标题（进容器的 Label ⇒ 不再手摆坐标）。</summary>
    private static Label TitleLabel(string text)
    {
        var label = new Label { Text = text };
        label.ThemeTypeVariation = Darkest.UI.DdTheme.TitleVariation; // 🔴 架构裁定②：分区标题用 Bold
        label.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextAccent);
        return label;
    }

    /// <summary>
    /// 🔴 **满屏不透明模态**（结算 / 开发者日志）：`PanelContainer` + `Margin` + `VBox` + 一个 `Label`。
    /// · 满屏 + 不透明 ⇒ 判据把它识别为**模态**（只审它内部）⇒ 不会把"被它盖住的 Label"算成重叠 ✓
    /// · `ExpandFill` + `autowrap` ⇒ 长文本不溢出（`§14.2`④ / `§14.6`）✓
    /// </summary>
        // （体首守卫在下方 if 内；此处仅占位不改语义）
    private Label MakeOpaqueModal(string name, out PanelContainer panel,
        Darkest.UI.PopupLayout layout = Darkest.UI.PopupLayout.Modal)
    {
        // 🔴 Track 3：**模态优先用模板** `scenes/ui/modal_dialog.tscn`（外观在编辑器可改）；
        //    模板缺失 ⇒ **回落下面原有的代码构建**（一字不改，不崩不静默）✓
        //    契约保持：返回内容 `Label`（节点名仍是 `{name}Text`，调用方对它写 `.Text`）✓
        (PanelContainer? tplPanel, VBoxContainer? tplBody) = Darkest.UI.ModalDialogTemplate.TryCreate(name);
        if (tplPanel is not null && tplBody is not null)
        {
            tplPanel.Name = name;
            tplPanel.Visible = false;
            var tplLabel = new Label
            {
                Name = $"{name}Text",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            tplBody.AddChild(tplLabel);

            // ✕ 走模板自带的 `DialogClose`（避免与下方代码构建的 ✕ 并存）✓
            PanelContainer tplLocal = tplPanel;
            Darkest.UI.ModalDialogTemplate.BindClose(tplPanel, () =>
            {
                // 🔴 关自己 + **出栈**（此前只 Visible=false ⇒ 栈里还留着 ⇒ 遮罩不消失）
                tplLocal.Visible = false;
                _overlay?.CloseModal(tplLocal);
                GD.Print($"[UI] {name} 关闭（模板 ✕）✓");
            });

            panel = tplPanel;
            (_overlay?.ModalHost ?? _uiRoot).AddChild(panel);
            // 🔴 不再一律满屏：按 DD `shared/modal_dialog` ⇒ 840×464 居中 ✓
            Darkest.UI.UILayoutSpec.Place(panel, layout);
            GD.Print($"[UI 布局] `{name}`：{Darkest.UI.UILayoutSpec.Describe(layout)}");
            // ⚠️ **不在这里入栈**：`OpenModal` 会把面板设为可见，而 ResultPanel/DevLogPanel
            //    是 **Build 期创建、之后才显示** ⇒ 入栈时机交给调用方（见 `Refresh`/`ToggleDevLog`）✓
            return tplLabel;
        }

        panel = new PanelContainer { Name = name, Visible = false };
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        var col = new VBoxContainer { Name = $"{name}Col" };
        margin.AddChild(col);
        panel.AddChild(margin);

        // 🔴 **用户要求（2026-09-15）：所有二级窗口都要有【关闭】** ✓（结算/日志模态）
        var headRow = new HBoxContainer { Name = $"{name}Head" };
        headRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(headRow);
        headRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var closeBtn = new Button { Name = $"{name}Close", Text = "✕ 关闭", CustomMinimumSize = new Vector2(110, 32) };
        PanelContainer panelLocal = panel; // ⚠️ `out` 参数不能进 lambda ⇒ 取局部副本 ✓
        closeBtn.Pressed += () => { panelLocal.Visible = false; GD.Print($"[UI] {name} 关闭（✕）✓"); };
        headRow.AddChild(closeBtn);

        var label = new Label
        {
            Name = $"{name}Text",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        col.AddChild(label);

        (_overlay?.ModalHost ?? _uiRoot).AddChild(panel);   // 🔴 Track 3：**统一走 Overlay**（缺失回落 _uiRoot）✓
        // 🔴 不再一律满屏：按档位定位（此前 FullRect ⇒ 铺满 1920×1080）✓
        Darkest.UI.UILayoutSpec.Place(panel, layout);
        // ⚠️ 同上：**入栈交给调用方**（本工厂只负责建 + 摆位）✓
        return label;
    }

    private Control _uiRoot = null!;
    private Darkest.UI.OverlayLayer? _overlay;   // 🔴 Track 3：Overlay 层（模态/悬停统一住这里；缺失回落 _uiRoot）✓

    // 🔴 `#327` S1（**无缝的可测定义**）：**必须存活的骨架** —— 背景 + 队伍区宿主 + E 区宿主 + 右下角地图宿主。
    //    "进战斗前后这些节点的 `GetInstanceId()` 不变" ⇒ 无缝；反例：走了场景切换/整体重建 ⇒ id 必变 ✓
    //    ⚠️ 现状**如实**：`Build()` 是一次性全建 ⇒ 每次 `Bind()` 这些 id **都会变** ⇒ 这正是迁移（片 1/2）要改的点
    private Control _bg = null!;

    // 🔴 `ui_spec §12.1`：动效层（伤害数字 / 暗角）与它的状态
    private Control _motionLayer = null!;
    private ColorRect _vignette = null!;   // `§12.3` 满屏 shader 覆盖层（暗角 + 闪白）
    private int _seenEvents;      // 已消费的事件条数（**只对"新事件"播动效**，不重播）
    private bool _resultShown;    // 结算淡入只播一次（不可见 → 可见那一次）
    private bool _motionAuditPrinted;

    // 🔴 主程序清单"等界面接线的内核 API"第 1 条：**使用支援包**（`Inventory.TryUseSupportPack` ⇒ `BattleDirector.TryUseSupportPackForSp`）
    //    此前**玩家碰不到**（红线 18/21：内核备好了但没有入口）⇒ 本按钮就是那个入口 ✓
    private Button _supportButton = null!;

    // 🔴 `ui_spec §14`：三行容器（顶部 / 中部卡片 / 底部技能与 E 区）——
    //    **重建路径**（行动顺序图标 / 技能键 / 卡片刻）也必须加进这些容器，
    //    否则它们会加回 CanvasLayer（`this`）⇒ 逃出 `_uiRoot` 子树 ⇒ 判据看不到它们（实测"可见 Label 0"）⚠️
    private Container _topRow = null!;
    private Darkest.UI.BattleTopBarSkeleton? _topBarSkel;   // 🔴 顶栏骨架（字段承载 ⇒ 避开作用域问题）✓
    private Darkest.UI.BattleBottomBarSkeleton? _bottomBarSkel;
    private Control _midRow = null!;   // C2a：中段改为"舞台层"Control ⇒ 子节点可用锚点（DD overlays y 0.6297 / band 0.148-0.410 与 0.547-0.809）
    private Container _bottomRow = null!;

    // 🆕 `#319`/`#321`③：**分区子容器** —— 控件一律【创建时】就加进这些容器（不再"事后搬运"，见下）
    private HBoxContainer _orderBox = null!;       // 顶栏：行动顺序头像
    private HBoxContainer _playerCards = null!;    // 主体：我方前排 4（战 4·3·2·1）
    private HBoxContainer _playerSupport = null!;  // 主体：我方支援位 2（辅 5·6）
    private HBoxContainer _enemyCards = null!;     // 主体：敌方 4
    private GridContainer _skillBar = null!;       // 底栏 C 区：技能栏（**在 C 区内**，`#321`③）
    private HBoxContainer _actionButtons = null!;  // 底栏 C 区：增援 / 移动 / 待命
    private PanelContainer _cArea = null!;         // 底栏 C 区（**固定宽 ~30%，不 ExpandFill**）
    private PanelContainer _eArea = null!;         // 底栏 E 区（**唯一 ExpandFill**）

    /// <summary>🔴 取证：满屏 Control 根（Theme 继承与锚点的落点）+ **Theme 是否真的生效**。</summary>
    public string RootAudit()
    {
        if (_uiRoot is null)
        {
            return "Control 根：未建";
        }

        // 🔴 从**真实控件**读出生效字号 ⇒ 这才是"Theme 继承成功"的证据（不是"我挂了 Theme"）
        int effective = _statusLabel.GetThemeFontSize("font_size");
        string inherited = effective == Darkest.UI.DdTheme.FontBody
            ? $"✅ Theme 继承生效（生效字号 {effective} = 中央 Theme）"
            : $"🔴 Theme 未生效（生效字号 {effective} ≠ 中央 {Darkest.UI.DdTheme.FontBody}）";

        // ⚠️ 尺寸**不作为判据**：headless 下视口尺寸会在运行间波动（实测见过 1280×1280 与 2560×2000）
        //    ⇒ 只判"是否**跟随视口**"（锚点生效的正确含义），而不是把某个具体数当结论（红线 17⑧：极端/波动读数先怀疑口径）✓
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        bool follows = _uiRoot.Size == vp || _uiRoot.AnchorRight == 1 && _uiRoot.AnchorBottom == 1;
        return $"Control 根：锚点={(int)_uiRoot.AnchorRight}/{(int)_uiRoot.AnchorBottom}（1/1 = 满屏）　" +
               $"跟随视口={(follows ? "✅" : "🔴")}（本帧 size={_uiRoot.Size} ／ 视口={vp}；**尺寸不作为判据**）　" +
               $"Theme={(_uiRoot.Theme is null ? "（无）" : "已挂中央 Theme")}　{inherited}";
    }

    /// <summary>供 BattleRoot 的提示文案使用（单位原型中文名）。</summary>
    public string ArchetypeNameOf(UnitId actor) => NameOf(_host?.ArchetypeOf(actor) ?? actor.Value);

    // ------------------------------------------------------------------
    // 🔴 片③：**点击单位 ⇒ 锁定到 E 区【详情】页**（`ui_spec.md` §1.1）
    //    · **任何时刻**都能看（不要求轮到你行动）；**纯只读**，不改战斗状态 ✓
    // ------------------------------------------------------------------

    private int _lockedSlot;

    /// <summary>被锁进详情页的槽位（0 = 未锁；供冒烟断言）。</summary>
    public int LockedSlot => _lockedSlot;

    /// <summary>当前 E 区页面的**可断言摘要**（headless 冒烟用）。</summary>
    public string DescribeCurrentPage()
        => _mfPage == MapPageIndex ? DescribeMiniMap() : (_mfContent?.Text?.Replace("\n", " ｜ ") ?? "（无内容）");

    /// <summary>把某单位锁进 E 区【详情】页（真实点击卡时由 `BattleRoot.OnCardClicked` 调）。</summary>
    public void ShowUnitDetail(int slot, bool isPlayer)
    {
        _lockedSlot = slot;
        SetMultiFunctionPage(0);

        if (_host is null)
        {
            return;
        }

        UnitRuntime? u = isPlayer ? _host.Director.Player.UnitRuntimeAt(slot)
            : _host.Director.Enemy.UnitRuntimeAt(slot);
        if (u is null)
        {
            _mfContent!.Text = $"【详情】{(isPlayer ? "我方" : "敌方")}槽位 {slot}：空位。";
            return;
        }

        _mfContent!.Text =
            $"【详情·{NameOf(u.Id.Value)}】{(isPlayer ? "我方" : "敌方")}槽位 {slot}\n" +
            $"　HP {u.CurrentHp}/{u.MaxHp}　士气 {u.Morale}　速度 {u.EffectiveSpeed(1.0)}\n" +
            $"　状态：{(u.Weak ? "死门 " : string.Empty)}{(u.CurrentHp <= 0 ? "已阵亡 " : string.Empty)}\n" +
            "　（点其它单位可切换；本页只读 —— 不改战斗状态）";
        GD.Print($"[片③] 单位锁进 E 区详情页：{(isPlayer ? "我方" : "敌方")}槽位 {slot}（{NameOf(u.Id.Value)}）");
    }

    /// <summary>🔴 供冒烟：**真实点击某单位的卡**（走 `BattleRoot.OnCardClicked` 同一入口）。</summary>
    public void PressCard(int slot, bool isPlayer) => _host?.OnCardClicked(slot, isPlayer);

    /// <summary>G2：开发者日志开/关（每次打开重绘整个事件流尾部）。</summary>
    public void ToggleDevLog()
    {
        // 🔴 走模态栈（此前直接翻 Visible ⇒ 遮罩不同步、Esc 关不掉）
        bool nowOpen = !_devLogPanel.Visible;
        if (nowOpen) { _overlay?.OpenModal(_devLogPanel); }
        else { _devLogPanel.Visible = false; _overlay?.CloseModal(_devLogPanel); }
        _devLogRendered = -1;
        _devLogButton.Text = _devLogPanel.Visible ? "日志 F1（开）" : "日志 F1";
    }
}
