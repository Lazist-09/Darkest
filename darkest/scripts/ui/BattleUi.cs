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
using UiMotion = Darkest.Ui.UiMotion; // ⚠️ 本文件命名空间是 `Darkest.UI`（大写）≠ `Darkest.Ui` ⇒ 用别名（最小改动）

namespace Darkest.UI;

/// <summary>
/// BattleUi：暗黑地牢式排布（1280×720，中文）。单位**一字横排、两军对望**：
/// 我方 4·3·2·1（左，1 位贴近中线）｜敌方 1·2·3·4（右）；支援位 5·6 为我方后排小卡；
/// 顶部状态+回合条，底部当前行动者技能栏 + 增援/移动。
/// 高亮（修复）：① 当前行动者一律高亮（含支援位 5/6）；② 仅"需选目标"时高亮候选且**按阵营匹配**
/// （敌技亮敌卡 / 友技亮友卡；AOE·团队·自身不进入选目标 → 不会全亮）；③ 增援两步按阶段亮 5/6 → 1~4。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    private const float CardW = 146f;
    private const float CardH = 170f;
    private const float GapX = 10f;
    private const float HeroX0 = 13f;
    private const float EnemyX0 = 653f;
    private const float StageY = 96f;
    private const float SupportY = 286f;
    private const float SupportW = 130f;
    private const float SupportH = 86f;
    private const float SkillTitleY = 400f;
    private const float SkillBarY = 428f;

    private BattleRoot? _host;
    private Action<UnitId, string>? _useSkill;
    private Action? _reinforce;
    private Action? _move;
    private Action? _retreat;
    private Action? _pass;

    private Label _statusLabel = null!;
    private Label _actionOrderLabel = null!;
    private Button _retreatButton = null!;
    // 卡序：0..3=我方 4,3,2,1；4..7=敌方 1,2,3,4；8..9=支援位 5,6
    private readonly List<(Control card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer)> _cards = new();
    private readonly List<Label> _portraits = new();          // 立绘占位框文字（与 _cards 同序）
    private readonly List<(PanelContainer panel, Label glyph)> _orderIcons = new(); // 顶部回合条头像
    private string _orderFor = "";
    private readonly List<Button> _skillButtons = new();
    private Button _reinforceButton = null!;
    private Button _moveButton = null!;
    private Button _passButton = null!; // S5.2 待命
    private PanelContainer _resultPanel = null!;
    private Label _resultLabel = null!;
    private PanelContainer _devLogPanel = null!;   // G2：开发者日志面板（F1 开关）

    // ------------------------------------------------------------------
    // 🔴 片③：**E 区 · 多功能框**（`ui_spec.md` §1.2：右 · 可切换分页：详情 ／ 日志 ／ 地图）
    //    · 地图**只读、不可点**（避免在战斗里改路线）
    //    · 数据跨场景走 `ExpeditionContext.Flow`（与 `PendingAmbush` 同法）
    // ------------------------------------------------------------------

    private Label _progressLabel = null!;
    private Panel _mfPanel = null!;
    private Label _mfContent = null!;
    private Darkest.Ui.BattleMiniMap? _mfMap;
    private int _mfPage;
    private readonly List<Button> _mfTabs = new();

    /// <summary>E 区当前分页（0 详情 ／ 1 日志 ／ 2 地图）—— 供冒烟断言。</summary>
    public int MultiFunctionPage => _mfPage;

    /// <summary>🔴 地图页的**可断言摘要**（headless 冒烟：地图与远征侧读数同源）。</summary>
    public string DescribeMiniMap() => _mfMap?.Describe() ?? "mini-map: 未建";

    /// <summary>建 E 区多功能框（三页起步；旧 F1 浮层保留为开发工具，本框的【日志】页显示事件流尾部）。</summary>
    private void BuildMultiFunctionBox()
    {
        // 🔴 Godot 内置清单 ②（第二批：**容器 + 锚点**）：
        //    · 面板**贴右下角**（锚点 BottomRight + 负偏移）⇒ 与分辨率无关（不再写死 640,556）✓
        //    · 内部用 **VBox/HBox 容器**排布（页签一行 + 内容区）⇒ 子控件**不再各写 Position** ✓
        // 🔴 `#321`③：E 区 = 底栏**唯一 ExpandFill** 的分区 ⇒ 多功能框**创建时进 `_eArea`**
        //    （容器负责尺寸 ⇒ 不再写 `BottomRight` 锚点与负偏移）
        _mfPanel = new Panel { Name = "MultiFunctionBox" };
        _mfPanel.Modulate = Darkest.Ui.DdTheme.PanelBgRaised;
        _eArea.AddChild(_mfPanel);

        var column = new VBoxContainer { Name = "MfColumn" };
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 4);
        _mfPanel.AddChild(column);

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
            var b = new Button { Text = tabs[i], CustomMinimumSize = new Vector2(80, 26) };
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
        _mfContent.AddThemeFontSizeOverride("font_size", Darkest.Ui.DdTheme.FontSmall);
        column.AddChild(_mfContent);

        // 地图页与文本页**共占同一内容区**（同一容器位置 ⇒ 切换时不需要各自算坐标）✓
        _mfMap = new Darkest.Ui.BattleMiniMap
        {
            Name = "MfMap",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
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
        if (_mfContent is not null)
        {
            _mfContent.Visible = page is >= 0 and <= 3; // 前四页共用文本区
        }

        if (_mfMap is not null)
        {
            _mfMap.Visible = page == 4;
            if (page == 4)
            {
                _mfMap.QueueRedraw(); // 进战斗时地图已定，重绘一次即可（只读）
            }
        }

        RefreshMultiFunctionContent();
        RefreshProgressLabel();

        // 页签高亮（当前页亮、其余暗）
        for (int i = 0; i < _mfTabs.Count; i++)
        {
            _mfTabs[i].Modulate = i == page ? Darkest.Ui.DdTheme.Highlight : Darkest.Ui.DdTheme.Disabled;
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

    /// <summary>一侧的站位摘要（只读）。</summary>
    private string DescribeSide(FormationBoard board)
    {
        var parts = new List<string>();
        for (int slot = 1; slot <= board.SlotCount; slot++)
        {
            UnitRuntime? u = board.UnitRuntimeAt(slot);
            parts.Add(u is null ? $"{slot}·空" : $"{slot}·{NameOf(u.Id.Value)}");
        }

        return string.Join("　", parts);
    }

    /// <summary>🔴 顶部**队伍进度条：段数**（**不是 HP 条**）—— 线性模式没有"段"，则如实标成战斗目标。</summary>
    public void RefreshProgressLabel()
    {
        if (_progressLabel is null)
        {
            return;
        }

        Darkest.Gameplay.Sim.Run.ExpeditionFlow? flow = Darkest.Gameplay.Scene.ExpeditionContext.Flow;
        if (flow is null || !flow.IsTopologyMode)
        {
            _progressLabel.Text = $"[进度] 本场（线性 ／ 单场：无段数口径）　回合 {_host?.Director.Round ?? 0}";
            return;
        }

        int visited = flow.Map.Rooms.Count(r => flow.HasVisited(r.Id));
        _progressLabel.Text = $"[进度] 段 {flow.StepsDone}　房间 {visited}/{flow.Map.Rooms.Count}　" +
                              $"已胜 {flow.Wins}　终点 {flow.ReachedGoal}";
    }    private Label _devLogLabel = null!;
    private Button _devLogButton = null!;
    private int _devLogRendered = -1;
    private Label _skillTitle = null!;
    private Label _hintLabel = null!;
    private double _hintTimer;
    private string _skillBarFor = "";
    private bool _skillBarWaiting;
    private static readonly Dictionary<string, string> _unitNames = new();
    private static readonly Dictionary<string, string> _skillNames = new();
    private static readonly Dictionary<string, string> _buffNames = new();
    private static readonly Dictionary<string, string[]> _poolCache = new();
    private static SkillsConfig? _skillsCfg;

    public void Bind(BattleRoot host, Action<UnitId, string> useSkill, Action reinforce, Action move, Action retreat,
        Action? pass = null)
    {
        _host = host;
        _useSkill = useSkill;
        _reinforce = reinforce;
        _move = move;
        _retreat = retreat;
        _pass = pass;
        foreach (Node child in GetChildren().ToArray())
        {
            child.QueueFree();
        }

        _cards.Clear();
        _portraits.Clear();
        _orderIcons.Clear();
        _orderFor = "";
        _skillButtons.Clear();
        _skillBarFor = "";
        _skillBarWaiting = false;
        Build();
        GD.Print("[BattleUi] 暗黑地牢式排布就绪（横排：我方 4321 ｜ 敌方 1234；支援位后排；底部技能栏）。");

        // 🔴 审计清单③：**进场就给焦点**（否则键盘/手柄用户"没有起点"，方向键无处可动）
        if (_cards.Count > 0 && _cards[0].card is Control first)
        {
            first.GrabFocus();
        }
    }

    /// <summary>🔴 审计清单③的**取证**：当前焦点所有者 + 可聚焦控件数（headless 可断言）。</summary>
    public string FocusAudit()
    {
        Control? owner = GetViewport()?.GuiGetFocusOwner();
        int focusableCards = _cards.Count(c => c.card is Control { FocusMode: not Control.FocusModeEnum.None });
        int focusableButtons = _skillButtons.Count(b => b.FocusMode != Control.FocusModeEnum.None);
        bool uiAccept = InputMap.HasAction("ui_accept") && InputMap.ActionGetEvents("ui_accept").Count > 0;
        bool uiCancel = InputMap.HasAction("ui_cancel") && InputMap.ActionGetEvents("ui_cancel").Count > 0;

        // ⚠️ 区分【未建】与【不可聚焦】：技能栏只在"轮到玩家"时才建 ⇒ 0 个 ≠ 不可聚焦（不误导）
        string buttons = _skillButtons.Count == 0
            ? "技能键：尚未建（未到玩家行动）"
            : $"可聚焦技能键 {focusableButtons}/{_skillButtons.Count}";
        return $"焦点所有者 = {owner?.Name ?? "（无）"}　可聚焦卡片 {focusableCards}/{_cards.Count}　{buttons}　" +
               $"引擎内置动作 ui_accept={uiAccept}／ui_cancel={uiCancel}　{RootAudit()}";
    }

    private static SkillsConfig SkillsCfg => _skillsCfg ??= SkillsConfig.Parse(ReadData("skills.json"));

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
        Darkest.Ui.DdTheme.Apply(_uiRoot);
        AddChild(_uiRoot);

        // 背景：**刻意不让它成为"满屏不透明 Panel"**（锚点不是 0/0/1/1）——
        //   否则判据会把它当成**模态覆盖层**，只审它自己的子树（= 空）⇒ 报 ✅ 却是**假通过** ⚠️（实测踩过两次）
        var bg = new Panel { Name = "BattleBg", Size = GetViewport().GetVisibleRect().Size };
        bg.Modulate = Darkest.Ui.DdTheme.BgDeep;
        _uiRoot.AddChild(bg);

        var uiMargin = new MarginContainer { Name = "BattleMargin" };
        uiMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        uiMargin.AddThemeConstantOverride("margin_left", 10);
        uiMargin.AddThemeConstantOverride("margin_top", 8);
        uiMargin.AddThemeConstantOverride("margin_right", 10);
        uiMargin.AddThemeConstantOverride("margin_bottom", 8);
        _uiRoot.AddChild(uiMargin);

        var uiCol = new VBoxContainer { Name = "BattleCol" };
        uiCol.AddThemeConstantOverride("separation", 6);
        uiMargin.AddChild(uiCol);

        var topPanel = new PanelContainer { Name = "TopRow" };
        uiCol.AddChild(topPanel);
        var topRow = new HBoxContainer { Name = "TopRowBox" };
        topRow.AddThemeConstantOverride("separation", 10);
        topPanel.AddChild(topRow);
        _topRow = topRow;

        var midPanel = new PanelContainer { Name = "MidRow", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        uiCol.AddChild(midPanel);
        var midRow = new HBoxContainer { Name = "MidRowBox", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        midRow.AddThemeConstantOverride("separation", 8);
        midPanel.AddChild(midRow);
        _midRow = midRow;

        var bottomPanel = new PanelContainer { Name = "BottomRow", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        uiCol.AddChild(bottomPanel);
        var bottomRow = new HBoxContainer { Name = "BottomRowBox", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        bottomRow.AddThemeConstantOverride("separation", 10);
        bottomPanel.AddChild(bottomRow);
        _bottomRow = bottomRow;

        BuildTopRow();
        BuildBattlefield();
        BuildBottomRow();

        // 结算 / 开发者日志 = **满屏不透明模态**（挂 `_uiRoot`：它是真 `Control` ⇒ `FullRect` 锚点算得出满屏 ✓）
        _resultLabel = MakeOpaqueModal("ResultPanel", out _resultPanel);
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
        Darkest.Ui.UiSfx.Attach(_uiRoot);

        GD.Print("[BattleUi] 容器树就绪：顶栏／主体（我方 4+2 ←→ 敌方 4）／底栏（C 区含技能栏 ＋ E 区多功能框）" +
                 " ⇒ 控件**创建时进容器** ✓");
    }

    /// <summary>A 顶栏：状态（回合·支援点）／行动顺序头像／进度／日志／撤退。</summary>
    private void BuildTopRow()
    {
        _statusLabel = new Label { Text = "" };
        _statusLabel.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.TextPrimary);
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // 占满剩余宽度
        _topRow.AddChild(_statusLabel);

        var orderLabel = new Label { Text = "本回合顺序", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        orderLabel.AddThemeFontSizeOverride("font_size", Darkest.Ui.DdTheme.FontSmall);
        _topRow.AddChild(orderLabel);

        _orderBox = new HBoxContainer { Name = "OrderBox", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        _orderBox.AddThemeConstantOverride("separation", 4);
        _topRow.AddChild(_orderBox);

        _progressLabel = new Label { Text = "" };
        _progressLabel.AddThemeFontSizeOverride("font_size", 13);
        _progressLabel.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.TextInfo);
        _progressLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _topRow.AddChild(_progressLabel);

        _devLogButton = new Button { Text = "日志 F1" };
        _devLogButton.Pressed += ToggleDevLog;
        _topRow.AddChild(_devLogButton);

        _retreatButton = new Button { Text = "撤退 0%" };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        _topRow.AddChild(_retreatButton);

        // 兼容既有刷新路径：状态/顺序文案仍由 `Refresh()` 写
        _actionOrderLabel = orderLabel;
    }

    /// <summary>主体：我方（前排 4 ＋ 支援位 2）←→ 敌方 4。卡片**均分宽**（`#321`③：位置编号要稳定映射横坐标）。</summary>
    private void BuildBattlefield()
    {
        var playerArea = new VBoxContainer { Name = "PlayerArea", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        playerArea.AddThemeConstantOverride("separation", 4);
        _midRow.AddChild(playerArea);

        playerArea.AddChild(TitleLabel("我方　战 4 · 3 · 2 · 1　｜　辅 5 · 6"));

        _playerCards = new HBoxContainer { Name = "PlayerCards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _playerCards.AddThemeConstantOverride("separation", 6);
        playerArea.AddChild(_playerCards);

        _playerSupport = new HBoxContainer { Name = "PlayerSupport", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _playerSupport.AddThemeConstantOverride("separation", 6);
        playerArea.AddChild(_playerSupport);

        var vs = new Label { Text = "VS", CustomMinimumSize = new Vector2(24, 24), VerticalAlignment = VerticalAlignment.Center };
        vs.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.Danger);
        _midRow.AddChild(vs);

        var enemyArea = new VBoxContainer { Name = "EnemyArea", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        enemyArea.AddThemeConstantOverride("separation", 4);
        _midRow.AddChild(enemyArea);
        enemyArea.AddChild(TitleLabel("敌方　1 · 2 · 3 · 4"));

        _enemyCards = new HBoxContainer { Name = "EnemyCards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _enemyCards.AddThemeConstantOverride("separation", 6);
        enemyArea.AddChild(_enemyCards);

        // 卡片**创建时**就进各自的容器（顺序保持：我方 4 → 敌方 4 → 支援 2，`Refresh()` 的下标依赖它）
        for (int i = 0; i < 4; i++)
        {
            _playerCards.AddChild(BuildCard(CardW, CardH));
        }

        for (int i = 0; i < 4; i++)
        {
            _enemyCards.AddChild(BuildCard(CardW, CardH));
        }

        for (int i = 0; i < 2; i++)
        {
            _playerSupport.AddChild(BuildCard(SupportW, SupportH));
        }
    }

    /// <summary>底栏：C 区（固定宽，**技能栏在 C 区内**）＋ E 区（多功能框，**唯一 ExpandFill**）。</summary>
    private void BuildBottomRow()
    {
        _cArea = new PanelContainer
        {
            Name = "CArea",
            CustomMinimumSize = new Vector2(380, 0),                    // 固定宽 ≈ 30%（`#321`③）
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,        // 不 ExpandFill
        };
        var cCol = new VBoxContainer { Name = "CCol" };
        cCol.AddThemeConstantOverride("separation", 6);
        _cArea.AddChild(cCol);
        _bottomRow.AddChild(_cArea);

        _skillTitle = new Label { Text = "技能栏（轮到行动者时可用）", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _skillTitle.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.TextSkill);
        cCol.AddChild(_skillTitle);

        _skillBar = new GridContainer
        {
            Name = "SkillBar",
            Columns = Darkest.Ui.DdTheme.SkillBarColumns,               // 🔴 `#325` D5：常量集中在 DdTheme（不是局部 const）
        };
        _skillBar.AddThemeConstantOverride("h_separation", 6);
        _skillBar.AddThemeConstantOverride("v_separation", 6);
        cCol.AddChild(_skillBar);

        _hintLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hintLabel.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.TextHint);
        cCol.AddChild(_hintLabel);

        _actionButtons = new HBoxContainer { Name = "ActionButtons" };
        _actionButtons.AddThemeConstantOverride("separation", 6);
        cCol.AddChild(_actionButtons);

        _reinforceButton = new Button { Text = "增援", CustomMinimumSize = new Vector2(116, 44) };
        _reinforceButton.Pressed += () => _reinforce?.Invoke();
        _actionButtons.AddChild(_reinforceButton);

        _moveButton = new Button { Text = "移动", CustomMinimumSize = new Vector2(116, 44) };
        _moveButton.Pressed += () => _move?.Invoke();
        _actionButtons.AddChild(_moveButton);

        _passButton = new Button { Text = "待命", CustomMinimumSize = new Vector2(116, 44) };
        _passButton.Pressed += () => _pass?.Invoke();
        _actionButtons.AddChild(_passButton);

        _eArea = new PanelContainer
        {
            Name = "EArea",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,          // 🔴 **唯一 ExpandFill**（`#321`③）
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _bottomRow.AddChild(_eArea);

        BuildMultiFunctionBox(); // E 区多功能框（内部自带容器；**创建时进 `_eArea`**）
    }

    /// <summary>分区小标题（进容器的 Label ⇒ 不再手摆坐标）。</summary>
    private static Label TitleLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.TextAccent);
        return label;
    }

    /// <summary>
    /// 🔴 **满屏不透明模态**（结算 / 开发者日志）：`PanelContainer` + `Margin` + `VBox` + 一个 `Label`。
    /// · 满屏 + 不透明 ⇒ 判据把它识别为**模态**（只审它内部）⇒ 不会把"被它盖住的 Label"算成重叠 ✓
    /// · `ExpandFill` + `autowrap` ⇒ 长文本不溢出（`§14.2`④ / `§14.6`）✓
    /// </summary>
    private Label MakeOpaqueModal(string name, out PanelContainer panel)
    {
        panel = new PanelContainer { Name = name, Visible = false };
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        var col = new VBoxContainer { Name = $"{name}Col" };
        margin.AddChild(col);
        panel.AddChild(margin);

        var label = new Label
        {
            Name = $"{name}Text",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        col.AddChild(label);

        _uiRoot.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); // 父是真 Control ⇒ 锚点算得出满屏 ✓
        return label;
    }

    private Control _uiRoot = null!;

    // 🔴 `ui_spec §12.1`：动效层（伤害数字 / 暗角）与它的状态
    private Control _motionLayer = null!;
    private ColorRect _vignette = null!;   // `§12.3` 满屏 shader 覆盖层（暗角 + 闪白）
    private int _seenEvents;      // 已消费的事件条数（**只对"新事件"播动效**，不重播）
    private bool _resultShown;    // 结算淡入只播一次（不可见 → 可见那一次）
    private bool _motionAuditPrinted;

    // 🔴 `ui_spec §14`：三行容器（顶部 / 中部卡片 / 底部技能与 E 区）——
    //    **重建路径**（行动顺序图标 / 技能键 / 卡片刻）也必须加进这些容器，
    //    否则它们会加回 CanvasLayer（`this`）⇒ 逃出 `_uiRoot` 子树 ⇒ 判据看不到它们（实测"可见 Label 0"）⚠️
    private Container _topRow = null!;
    private Container _midRow = null!;
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
        string inherited = effective == Darkest.Ui.DdTheme.FontBody
            ? $"✅ Theme 继承生效（生效字号 {effective} = 中央 Theme）"
            : $"🔴 Theme 未生效（生效字号 {effective} ≠ 中央 {Darkest.Ui.DdTheme.FontBody}）";

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
        => _mfPage == 4 ? DescribeMiniMap() : (_mfContent?.Text?.Replace("\n", " ｜ ") ?? "（无内容）");

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
        _devLogPanel.Visible = !_devLogPanel.Visible;
        _devLogRendered = -1;
        _devLogButton.Text = _devLogPanel.Visible ? "日志 F1（开）" : "日志 F1";
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // 🔴 动作化（附 B ①）：`dd_toggle_log` 见 `project.godot [input]`（玩家可重映射）
        if (e.IsAction("dd_toggle_log"))
        {
            ToggleDevLog();
        }
    }

    private Control BuildCard(float w, float h)
    {
        // 🔴 `§14.2`：卡片**自己也是容器**（`PanelContainer` + 内部 `VBox`/`HBox`）——
        //    原来卡片内部全是**手写坐标的 Label**（实测 `name`(y 8..32) 与 `stats`(y 30..50) 就压 2px ⇒ 4 张卡各 1 对重叠）⚠️
        //    容器堆叠 ⇒ 卡片内部**物理上不可能重叠** ✓（并给最小尺寸：`#14.2`④）
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(w, h),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, // 均分宽 ⇒ 位置编号稳定映射横坐标（`#321`③）
        };
        var col = new VBoxContainer { Name = "CardCol" };
        col.AddThemeConstantOverride("separation", 2);
        card.AddChild(col);

        var head = new HBoxContainer { Name = "CardHead" };
        head.AddThemeConstantOverride("separation", 4);
        col.AddChild(head);

        // ② 立绘占位框（色块 + 首字）—— 🔴 **必须是 `PanelContainer`**：`Panel` 不是容器 ⇒ Label 变宽会溢出压邻居（同上）
        var portraitBox = new PanelContainer { CustomMinimumSize = new Vector2(44, 44) };
        head.AddChild(portraitBox);
        var glyph = new Label
        {
            Text = "—",
            CustomMinimumSize = new Vector2(44, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true, // 🔴 长文本裁切（`§14.6`）
        };
        glyph.AddThemeFontSizeOverride("font_size", 20);
        glyph.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.TextPrimary);
        portraitBox.AddChild(glyph);

        var nameCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        nameCol.AddThemeConstantOverride("separation", 2);
        head.AddChild(nameCol);

        var name = new Label { Text = "[-]", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 15);
        nameCol.AddChild(name);

        var stats = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stats.AddThemeFontSizeOverride("font_size", 12);
        nameCol.AddChild(stats);

        var hp = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
        col.AddChild(hp);
        var morale = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) };
        col.AddChild(morale);

        var tag = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        tag.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(tag);
        _portraits.Add(glyph);

        int slot = _cards.Count < 4 ? 4 - _cards.Count
            : _cards.Count < 8 ? _cards.Count - 3
            : _cards.Count - 8 + 5;
        bool isPlayer = _cards.Count < 4 || _cards.Count >= 8;
        int slotCaptured = slot;
        bool playerCaptured = isPlayer;

        // 🔴 Godot 内置（审计清单③：Control 焦点/手柄导航）：
        //    ① 卡片**可聚焦**（`FocusMode = All`）⇒ 键盘方向键/手柄十字键能在单位间移动（引擎自动算邻居）✓
        //    ② `ui_accept`（回车/空格/手柄 A，**引擎内置动作**）⇒ 与鼠标左键等价地"锁定该单位"✓
        //    ⚠️ 没有这两行，"InputMap 动作化"只完成一半：键位可重映射了，但**导航收益兑现不了**（架构指出）
        card.FocusMode = Control.FocusModeEnum.All;
        card.GuiInput += (InputEvent e) =>
        {
            bool activate = e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                            || e.IsAction("ui_accept"); // 🔴 键盘/手柄确认
            if (activate)
            {
                _host?.OnCardClicked(slotCaptured, playerCaptured);
            }
        };
        _cards.Add((card, name, stats, hp, morale, tag, slot, isPlayer));
        return card;
    }

    public void Refresh(string status = "")
    {
        if (_host is null || _host.Director is null || _host.Projector is null)
        {
            return;
        }

        BattleDirector d = _host.Director;
        BattleProjector p = _host.Projector;
        DecisionSupportProjection support = p.Support();

        _statusLabel.Text = _host.GameOver
            ? $"战斗结束（第 {support.Round} 回合）：{status}"
            : status.Length > 0 ? status : $"回合 {support.Round}";
        // #211（S0）必显 #10：支援点常驻显示（含本回合恢复预览）；数字只来自投影（UI 不得自行扣点）
        _statusLabel.Text += $"　　支援点 {support.SupportPoints}/{support.SupportCap}（下回合 {support.SupportRegenPreview}）";
        // 队列只保留头像方块（下方 RefreshOrderStrip）；原文字队列与方块重合已移除
        _actionOrderLabel.Text = "本回合顺序";
        _retreatButton.Text = support.CanRetreat && !_host.GameOver ? $"撤退 {support.RetreatRatePercent}%" : "本回合不可撤退";
        _retreatButton.Disabled = !support.CanRetreat || _host.GameOver;

        int activeSlot = _host.IsAwaitingPlayer ? (d.Player.UnitAtPosition(_host.ActiveActor) ?? -1) : -1;
        UnitProjection[] player = p.Units(player: true).ToArray();
        UnitProjection[] enemy = p.Units(player: false).ToArray();

        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[i], player[3 - i], _portraits[i]); // 我方 4,3,2,1
        }

        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[4 + i], enemy[i], _portraits[4 + i]); // 敌方 1,2,3,4
        }

        FillCard(_cards[8], player[4], _portraits[8]);
        FillCard(_cards[9], player[5], _portraits[9]);

        RefreshOrderStrip(support.ActionOrderThisRound, d);
        PlayMotionFromNewEvents(d, p);

        // ④ 结算：**面板出现 ⇒ 淡入 0.20s**（只在"不可见 → 可见"那一次播；可见性本身不被动效门控 ⇒ 不延迟可操作时刻）✓
        if (_resultPanel.Visible && !_resultShown)
        {
            _resultShown = true;
            UiMotion.Settle(_resultPanel);
        }
        else if (!_resultPanel.Visible)
        {
            _resultShown = false;
        }

        // 🔴 `§12.1` 取证（一次性）：战斗结束时打印动效读数（`--battle-auto-finish` 冒烟即可看到）
        if (_host.GameOver && !_motionAuditPrinted)
        {
            _motionAuditPrinted = true;
            Darkest.Ui.UiSfx.Play(Darkest.Ui.UiSfx.Kind.Settle); // ③ 结算（胜/败）
            GD.Print($"[UI 动效] {MotionAudit()}");
            GD.Print($"[UI 音效] {Darkest.Ui.UiSfx.Audit()}");
        }

        int[] pending = _host.PendingCandidates;
        bool targeting = _host.IsTargeting;
        bool targetsEnemy = _host.PendingTargetsEnemy;
        int phase = _host.ReinforcePhase;
        foreach ((Control card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c in _cards)
        {
            bool isActive = _host.IsAwaitingPlayer && c.isPlayer && c.slot == activeSlot;
            bool hl = false;
            if (phase == 1)
            {
                hl = c.isPlayer && c.slot is 5 or 6 && d.Player.UnitRuntimeAt(c.slot) is not null;
            }
            else if (phase == 2)
            {
                hl = c.isPlayer && c.slot is >= 1 and <= 4;
            }
            else if (targeting && !isActive)
            {
                hl = targetsEnemy != c.isPlayer && Array.IndexOf(pending, c.slot) >= 0;
            }

            c.card.Modulate = isActive
                ? Darkest.Ui.DdTheme.Highlight
                : hl ? Darkest.Ui.DdTheme.Ally : Darkest.Ui.DdTheme.TextPrimary;

            // G3（O-56）：悬停单位卡 → 详情（属性/士气/buff/技能表；敌方同样全暴露）
            c.card.TooltipText = DetailTooltip(c.isPlayer, c.slot);

            // D7（#209）三态可读性：普通物理（默认掉血条）/ 精神（紫）/ 被暴击（橙·震慑）
            UnitRuntime? runtime = c.isPlayer ? d.Player.UnitRuntimeAt(c.slot) : d.Enemy.UnitRuntimeAt(c.slot);
            (Color barColor, string? tagOverride) = RecentHitFeedback(runtime?.Id);
            if (barColor != default)
            {
                c.morale.Modulate = barColor;
            }

            if (tagOverride is not null)
            {
                c.tag.Text = tagOverride;
                c.tag.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.Shock);
            }
        }

        RefreshSkillBar(d, p);

        _resultPanel.Visible = _host.GameOver;
        if (_host.GameOver)
        {
            int[] c = _host.ResultCounts;
            _resultLabel.Text =
                $"{_host.ResultText}\n\n回合数：{_host.ResultRound}\n\n系统触发计数：\n" +
                $"　士气触底 {c[0]}　虚弱 {c[1]}　死门 {c[2]}\n　撤退 {c[3]}　美德 {c[4]}　折磨 {c[5]}\n　位移 {c[6]}\n\n按 R 重开（新 seed）";
        }

        // G2：日志面板可见时，仅在事件数变化时重绘（取尾部 20 行，避免每帧重建）
        if (_devLogPanel.Visible && _host.Director is { } dir)
        {
            int count = dir.Log.Count;
            if (count != _devLogRendered)
            {
                _devLogRendered = count;
                IReadOnlyList<string> lines = CombatLogText.Render(dir.Log.Events, includeRng: false, NameOf, SkillName, BuffNameOf);
                _devLogLabel.Text = lines.Count <= 20
                    ? string.Join("\n", lines)
                    : string.Join("\n", lines.Skip(lines.Count - 20));
            }
        }

        if (_hintTimer > 0)
        {
            _hintTimer -= 1.0 / 60.0;
            if (_hintTimer <= 0)
            {
                _hintLabel.Text = "";
            }
        }
    }

    public void FlashHint(string text)
    {
        _hintLabel.Text = text;
        _hintTimer = 3.0;
    }

    /// <summary>① 顶部回合条：头像格（首字 + 阵营色，当前行动者金框），替代纯文字。</summary>
    private void RefreshOrderStrip(IReadOnlyList<string> order, BattleDirector d)
    {
        int activePos = d.Player.UnitAtPosition(_host!.ActiveActor) ?? d.Enemy.UnitAtPosition(_host.ActiveActor) ?? 0;
        string key = string.Join(",", order) + "|" + activePos + "|" + _host.IsAwaitingPlayer;
        if (key == _orderFor)
        {
            return;
        }

        _orderFor = key;
        foreach ((PanelContainer panel, Label glyph) icon in _orderIcons)
        {
            icon.panel.QueueFree();
        }

        _orderIcons.Clear();
        float x = 96f; // 只用于"是否成行"的旧口径；容器排布后不再需要写位置
        foreach (string id in order)
        {
            var unitId = new UnitId(id);
            bool isPlayer = d.Player.UnitAtPosition(unitId) is not null;
            string archetype = _host.ArchetypeOf(unitId);
            // 🔴 `§14.2`：面板必须是 **`PanelContainer`**（`Panel` **不是容器** ⇒ 内部 Label 一旦变宽就**溢出并压住邻居**）
            //    实测（`--ui-longtext` 长文本压力，`§12.4`）：`Panel` + 宽 Label ⇒ **10 对重叠**；
            //    改 `PanelContainer` + `ClipText` ⇒ 文本被**裁在框内**、不再溢出 ✓
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(42, 34) };
            var glyph = new Label
            {
                CustomMinimumSize = new Vector2(42, 26),
                Text = NameOf(archetype).Substring(0, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true, // 🔴 长文本**裁切**而非溢出（`§14.6`）
            };
            glyph.AddThemeFontSizeOverride("font_size", 14);
            panel.AddChild(glyph);
            bool isActive = _host.IsAwaitingPlayer && id == _host.ActiveActor.Value;
            panel.Modulate = isActive
                ? Darkest.Ui.DdTheme.Highlight
                : isPlayer ? Darkest.Ui.DdTheme.Ally : Darkest.Ui.DdTheme.Danger;
            _orderBox.AddChild(panel); // 🔴 §14：行动顺序头像进【顶栏的顺序容器】（不再加回 CanvasLayer）
            _orderIcons.Add((panel, glyph));
            x += 40f;
        }
    }

    private void FillCard((Control card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c, UnitProjection u, Label portrait)
    {
        bool empty = u.UnitId == "-";
        string display = NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId);
        c.name.Text = empty ? $"[{u.Slot}] 空位" : $"[{u.Slot}] {display}";
        c.stats.Text = empty ? "" : $"HP {u.Hp}/{u.MaxHp}　士气 {u.Morale}";
        // ② 立绘占位框：首字 + 阵营/原型色块
        portrait.Text = empty ? "—" : display.Substring(0, 1);
        if (portrait.GetParent() is PanelContainer box)
        {
            box.Modulate = empty ? Darkest.Ui.DdTheme.Muted : Darkest.Ui.DdTheme.ArchetypeColor(u.Archetype.Length > 0 ? u.Archetype : u.UnitId, c.isPlayer);
        }

        c.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        c.hp.Value = u.Hp;
        c.hp.Modulate = u.Weak ? Darkest.Ui.DdTheme.HpWeak : Darkest.Ui.DdTheme.Hp;
        c.morale.MaxValue = 100;
        c.morale.Value = u.Morale;
        c.morale.Modulate = c.isPlayer ? Darkest.Ui.DdTheme.Morale : Darkest.Ui.DdTheme.MoraleEnemy;
        c.tag.Text = empty ? "" : (u.Weak ? "虚弱" : (c.isPlayer ? "我方" : "敌方"));
        // D4（#206）：死门后遗症必须显著标注（橙字）
        if (!empty && _host?.Director is { } dir && dir.Buffs.Has(new UnitId(u.UnitId), "deaths_door_recovery"))
        {
            c.tag.Text = "死门后遗症（伤+10% 命中−5 速−1）";
            c.tag.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.Shock);
        }
    }

    private void RefreshSkillBar(BattleDirector d, BattleProjector p)
    {
        bool waiting = _host!.IsAwaitingPlayer;
        UnitId actor = _host.ActiveActor;

        bool combatActor = waiting && (d.Player.UnitAtPosition(actor) ?? -1) is >= 1 and <= 4;
        _reinforceButton.Disabled = !waiting || d.SwappedThisRound || d.SupportPoints < d.SupportCostReinforce;
        _reinforceButton.TooltipText = d.SupportPoints < d.SupportCostReinforce
            ? $"支援点不足（当前 {d.SupportPoints} / 需要 {d.SupportCostReinforce}）"
            : $"增援：调动支援位上场（消耗 {d.SupportCostReinforce} 点）";
        _passButton.Disabled = !waiting;
        _passButton.TooltipText = $"待命：放弃本次行动（不消耗支援点）";
        _moveButton.Disabled = !combatActor || d.SwappedThisRound || MoveCandidates(actor, d).Length == 0;

        if (!waiting)
        {
            _skillTitle.Text = "敌方行动中…（自动结算）";
            if (_skillBarWaiting)
            {
                ClearSkillButtons();
            }

            _skillBarWaiting = false;
            return;
        }

        _skillTitle.Text = $"轮到 {NameOf(_host.ActiveArchetype)}　—　点技能 / 移动 / 增援（灰=不可用，悬停看原因）";
        if (_skillBarFor == actor.ToString() && _skillBarWaiting)
        {
            return;
        }

        _skillBarFor = actor.ToString();
        _skillBarWaiting = true;
        ClearSkillButtons();
        string archetype = _host.ActiveArchetype;
        var pool = new HashSet<string>(SkillPool(archetype));
        string[] poolIds = SkillPool(archetype);
        // 🔴 `#325` D5：**列数不再写死在这里** ⇒ 常量集中在 `DdTheme.SkillBarColumns`（原 `perRow = 8` 是 D5 点名的反例）
        for (int i = 0; i < poolIds.Length; i++)
        {
            string skillId = poolIds[i];
            SkillProjection sp = p.Skill(skillId, actor, d.Player, d.Enemy, pool);
            string full = SkillName(skillId);
            var b = new Button
            {
                CustomMinimumSize = new Vector2(88, 88), // §14.2 ④：容器排布要最小尺寸 ✓
                Text = full.Length <= 2 ? full : full.Substring(0, 2),
                Disabled = sp.Reason != AvailabilityReason.Ok,
                TooltipText = sp.Reason == AvailabilityReason.Ok ? SkillTooltip(skillId, actor, d) : $"{full}（{sp.Tooltip}）",
            };
            b.AddThemeFontSizeOverride("font_size", 20);
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            _skillBar.AddChild(b); // 🔴 §14：技能键进【C 区的技能栏容器】（不再手摆坐标）
            _skillButtons.Add(b);
        }
    }

    /// <summary>
    /// 🔴 `ui_spec §12.1` ① ② ③：**只对【新事件】播动效**（事件流 = 唯一事实来源，不另造状态）：
    /// 伤害 ⇒ 受击（抖动闪白）+ 上浮伤害数字；治疗 ⇒ 上浮绿色数字；进死门 ⇒ 士气崩溃（暗角 + 单位框红）✓
    /// ⚠️ 动效**不改任何玩法状态**（只写 `Modulate`/`Position`）⇒ 不吞输入、不延迟可操作时刻（`#321`⑤）✓
    /// </summary>
    private void PlayMotionFromNewEvents(BattleDirector d, BattleProjector p)
    {
        if (_motionLayer is null)
        {
            return;
        }

        IReadOnlyList<BattleEvent> events = d.Log.Events;
        if (events.Count < _seenEvents)
        {
            _seenEvents = 0; // 重开/换局 ⇒ 归零（事件流被重建）
        }

        for (int i = _seenEvents; i < events.Count; i++)
        {
            switch (events[i])
            {
                case DamageEvent { Target: { } dt, Amount: > 0 } dmg:
                    bool targetIsPlayer = IsPlayerUnit(dt, p);
                    PlayHitMotion(dt, $"-{dmg.Amount}",
                        dmg.Axis == "mental" ? Darkest.Ui.DdTheme.Mental : Darkest.Ui.DdTheme.Danger, p);
                    // 🔴 `§12.2` ① 命中（**区分我/敌**）+ ② 受击：
                    //    打敌人 ⇒ 我方命中音（高音方波）；**敌方打出** ⇒ 敌方命中音（低音方波）**＋** 我方受击音（噪声）
                    //    ⚠️ 这让 `HitEnemy` 有真实触发点（红线 21：**枚举项没有触发点 = 死声明**）；
                    //       若策划认为"敌方打出"只该有一种音，删掉其中一条即可（口径待确认，已投窗口）
                    if (targetIsPlayer)
                    {
                        Darkest.Ui.UiSfx.Play(Darkest.Ui.UiSfx.Kind.HitEnemy);
                        Darkest.Ui.UiSfx.Play(Darkest.Ui.UiSfx.Kind.Hurt);
                    }
                    else
                    {
                        Darkest.Ui.UiSfx.Play(Darkest.Ui.UiSfx.Kind.HitAlly);
                    }

                    break;
                case HealEvent { Target: { } ht, Amount: > 0 } heal:
                    PlayHitMotion(ht, $"+{heal.Amount}", Darkest.Ui.DdTheme.Hp, p);
                    break;
                case DeathDoorEvent { Unit: { } dd }:
                    PlayMoraleCrashMotion(dd, p);
                    Darkest.Ui.UiSfx.Play(Darkest.Ui.UiSfx.Kind.DeathDoor); // ② 死门
                    break;
                case DeathEvent { Unit: { } dead }:
                    Darkest.Ui.UiSfx.Play(Darkest.Ui.UiSfx.Kind.Death);     // ② 阵亡
                    UiMotion.ScreenFlash(_vignette, Darkest.Ui.UiMotion.DeathFlash, Darkest.Ui.UiMotion.MoraleSeconds); // 🔴 §12.3 闪白（整屏）
                    PlayMoraleCrashMotion(dead, p);
                    break;
            }
        }

        _seenEvents = events.Count;
    }

    private void PlayHitMotion(UnitId unitId, string text, Color color, BattleProjector p)
    {
        if (FindCard(unitId, p) is not { } found)
        {
            return;
        }

        UiMotion.Hit(found.Card);
        UiMotion.FloatText(_motionLayer, found.TextPos, text, color);
    }

    private void PlayMoraleCrashMotion(UnitId unitId, BattleProjector p)
    {
        UiMotion.MoraleCrash(_vignette, FindCard(unitId, p)?.Card);
    }

    /// <summary>把 `UnitId` 映射回它的卡片（下标编排见 `BuildBattlefield`：我方 4 → 敌方 4 → 支援 2）。</summary>
    private (Control Card, Vector2 TextPos)? FindCard(UnitId unitId, BattleProjector p)
    {
        for (int side = 0; side < 2; side++)
        {
            foreach (UnitProjection u in p.Units(player: side == 0))
            {
                if (u.UnitId != unitId.Value)
                {
                    continue;
                }

                int idx = u.IsPlayer ? (u.Slot >= 5 ? 8 + (u.Slot - 5) : 4 - u.Slot) : 4 + (u.Slot - 1);
                if (idx < 0 || idx >= _cards.Count)
                {
                    continue;
                }

                Control card = _cards[idx].card;
                Vector2 textPos = card.GlobalPosition - _motionLayer.GlobalPosition + new Vector2(12, -4); // 两 Control 的全局坐标之差 = 层内局部坐标
                return (card, textPos);
            }
        }

        return null;
    }

    /// <summary>某单位是否属于我方（音效/动效按阵营分岔用）。</summary>
    private static bool IsPlayerUnit(UnitId unitId, BattleProjector p)
        => p.Units(player: true).Any(u => u.UnitId == unitId.Value);

    /// <summary>
    /// 🔴 `§12.1` 的**取证**（冒烟打印）：动效播了几次 ／ 运行中几次 ／ **输入为什么不会被吞** ——
    /// 除了常量读数，还实测两件结构事实：动效层 `MouseFilter == Ignore`、且全屏**没有任何控件**被改成非继承 `ProcessMode`。
    /// </summary>
    public string MotionAudit()
    {
        bool ignore = _motionLayer is not null && _motionLayer.MouseFilter == Control.MouseFilterEnum.Ignore;
        int frozen = CountFrozenProcessMode(_uiRoot);
        return $"{UiMotion.Audit()}　动效层鼠标穿透实测={(ignore ? "✅ Ignore" : "🔴 会拦鼠标")}　" +
               $"被冻结 ProcessMode 的控件={frozen}（应为 0）";
    }

    private static int CountFrozenProcessMode(Node? root)
    {
        if (root is null)
        {
            return 0;
        }

        int n = root is Control { ProcessMode: not Node.ProcessModeEnum.Inherit } ? 1 : 0;
        foreach (Node child in root.GetChildren())
        {
            n += CountFrozenProcessMode(child);
        }

        return n;
    }

    /// <summary>
    /// D7（#209）三态反馈：按本回合事件流判定该单位刚承受的伤害类型——
    /// 精神伤害 = 紫（掉士气）／被暴击 = 橙·震慑（掉士气，独立标识）／普通物理 = 默认（不掉士气）。
    /// 只读事件流，不产生任何抽取。
    /// </summary>
    private (Color Bar, string? Tag) RecentHitFeedback(UnitId? id)
    {
        if (id is not { } unit || _host?.Director is null)
        {
            return (default, null);
        }

        int round = _host.Director.Round;
        bool mental = false;
        bool shock = false;
        foreach (BattleEvent e in _host.Director.Log.Events)
        {
            if (e.Round != round)
            {
                continue;
            }

            if (e is DamageEvent d && d.Target is { } t && t == unit && d.Amount > 0 && d.Axis == "mental")
            {
                mental = true;
            }

            if (e is EffectEvent ef && ef.EffectType == "crit_shock" && ef.Target is { } et && et == unit)
            {
                shock = true;
            }
        }

        if (shock)
        {
            return (Darkest.Ui.DdTheme.Shock, "震慑");
        }

        return mental ? (Darkest.Ui.DdTheme.Mental, null) : (default, null);
    }

    /// <summary>G3（O-56）：单位详情文本（含敌方全暴露：物防/速度/四抗/死门/buff/技能表）。</summary>
    private string DetailTooltip(bool player, int slot)
    {
        if (_host?.Projector is null || _host.Director is null)
        {
            return string.Empty;
        }

        UnitDetail d = _host.Projector.Detail(player, slot);
        if (d.UnitId == "-")
        {
            return $"[{slot}] 空位";
        }

        string buffs = string.Join("、", _host.Director.Buffs.Buffs(new UnitId(d.UnitId)).Select(BuffNameOf));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{d.Slot}] {NameOf(d.Archetype)}（{(player ? "我方" : "敌方")}）");
        sb.AppendLine($"HP {d.Hp}/{d.MaxHp}　士气 {d.Morale}{(d.Weak ? "　虚弱" : string.Empty)}");
        sb.AppendLine($"攻击 {d.Attack}　物防 {d.PhysDef}　速度 {d.Speed}　移动 {d.MoveDistance}");
        sb.AppendLine($"韧性 {d.Resilience}　眩晕 {d.StunResist}　流血 {d.BleedResist}　减益 {d.StatDebuffResist}　位移 {d.DisplaceResist}　死门 {d.DeathsDoorResist}");
        sb.AppendLine($"Buff：{(buffs.Length > 0 ? buffs : "无")}");
        sb.Append($"技能：{string.Join("、", d.SkillIds.Select(SkillName))}");
        return sb.ToString();
    }

    /// <summary>G3（O-56）：技能详情 + 对候选池每个目标的命中率/预估伤害（预估不掷骰、零副作用）。</summary>
    private string SkillTooltip(string skillId, UnitId actor, BattleDirector d)
    {
        SkillTemplateConfig s = SkillsCfg.Get(skillId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{SkillName(skillId)}");
        sb.AppendLine($"目标：{s.Target.Scope}{(s.Target.Side is { } side ? $"（{side}）" : string.Empty)}　命中+{s.HitMod}　暴击+{s.CritMod}");
        sb.AppendLine($"限用：{s.UseLimit.Type}{(s.UseLimit.Value is { } v ? $"（{v}）" : string.Empty)}　段数：{s.Damage?.Segments.Count ?? 0}");
        if (s.Damage is not null)
        {
            string segs = string.Join(" + ", s.Damage.Segments.Select(seg => seg.Type == DamageSegmentType.MissingHp
                ? $"失血 {seg.Base}+{seg.Coefficient}×已失"
                : $"×{seg.Multiplier}"));
            sb.AppendLine($"伤害：{segs}（{s.DamageAxis}）");
        }

        bool targetsEnemy = s.Target.Side == "enemy";
        int[] candidates = SkillTargetResolver.Resolve(s, actor, d.Player, d.Enemy).ToArray();
        foreach (int slot in candidates)
        {
            TargetEstimate est = _host!.Projector!.Estimate(skillId, actor, slot, targetIsPlayer: !targetsEnemy);
            sb.AppendLine($"　→ {slot} 位：命中 {est.HitRatePercent}%　预估 {est.EstimatedDamage} 伤害{(est.Segments > 1 ? $"（{est.Segments} 段）" : string.Empty)}");
        }

        return sb.ToString().TrimEnd();
    }

    private void ClearSkillButtons()
    {
        foreach (Button b in _skillButtons)
        {
            b.QueueFree();
        }

        _skillButtons.Clear();
    }

    private int[] MoveCandidates(UnitId actor, BattleDirector d)
    {
        if (d.Player.UnitAtPosition(actor) is not (>= 1 and <= 4))
        {
            return Array.Empty<int>();
        }

        return SkillTargetResolver.Resolve(SkillsCfg.Get("move"), actor, d.Player, d.Enemy).ToArray(); // F1：通用 move
    }

    private static string[] SkillPool(string archetype)
    {
        if (_poolCache.TryGetValue(archetype, out string[]? cached))
        {
            return cached;
        }

        string[] pool = SkillsCfg.Skills
            .Where(s => s.OwnerUnit == archetype && !s.PoolExternal) // F1/P12：按 pool_external 标志过滤（非 id 后缀）
            .Select(s => s.Id).ToArray();
        _poolCache[archetype] = pool;
        return pool;
    }

    private static string NameOf(string unitId)
    {
        if (_unitNames.Count == 0)
        {
            LoadNames();
        }

        return _unitNames.TryGetValue(unitId, out string? n) ? n : unitId;
    }

    private static string SkillName(string skillId)
    {
        if (_skillNames.Count == 0)
        {
            LoadNames();
        }

        return _skillNames.TryGetValue(skillId, out string? n) ? n : skillId;
    }

    /// <summary>G2：buff 中文名（buff_defs.json；缺失回落 id）。</summary>
    private static string BuffNameOf(string buffId)
    {
        if (_buffNames.Count == 0)
        {
            foreach (BuffDefConfig b in BuffDefsConfig.Parse(ReadData("buff_defs.json")).Buffs)
            {
                _buffNames[b.Id] = b.Name;
            }
        }

        return _buffNames.TryGetValue(buffId, out string? n) ? n : buffId;
    }

    private static void LoadNames()
    {
        foreach (UnitConfig u in UnitsConfig.Parse(ReadData("units.json")).Units)
        {
            _unitNames[u.Id] = u.Name;
        }

        foreach (SkillTemplateConfig s in SkillsCfg.Skills)
        {
            _skillNames[s.Id] = s.Name;
        }
    }

    /// <summary>
    /// 🔴 **`O-84` 修复**（架构 `#314` 之后的裁定 / 红线 26）：表现层读数据**一律 `FileAccess`**。
    /// 原实现用 `System.IO`（`AppContext.BaseDirectory` 逐级向上找 `data/`）——
    /// 🔴 **导出构建里 `data/*.json` 在 PCK 内、不是磁盘目录** ⇒ `File.Exists` 永远找不到
    /// ⇒ **单场战斗入口（`Battle.tscn`）在发行版直接 `FileNotFoundException` 崩溃** ⚠️
    /// ⇒ 改用 `FileAccess.GetFileAsString("res://data/…")`（与其余 20 处同法，导出安全 ✓）
    /// </summary>
    private static string ReadData(string name)
    {
        string path = $"res://data/{name}";
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"{path}: 数据文件不存在（表现层只走 FileAccess/res://，见 O-84）。");
        }

        return Godot.FileAccess.GetFileAsString(path);
    }
}