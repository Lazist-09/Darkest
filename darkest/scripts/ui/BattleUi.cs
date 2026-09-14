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
    private readonly List<(Panel card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer)> _cards = new();
    private readonly List<Label> _portraits = new();          // 立绘占位框文字（与 _cards 同序）
    private readonly List<(Panel panel, Label glyph)> _orderIcons = new(); // 顶部回合条头像
    private string _orderFor = "";
    private readonly List<Button> _skillButtons = new();
    private Button _reinforceButton = null!;
    private Button _moveButton = null!;
    private Button _passButton = null!; // S5.2 待命
    private Panel _resultPanel = null!;
    private Label _resultLabel = null!;
    private Panel _devLogPanel = null!;   // G2：开发者日志面板（F1 开关）

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
        _mfPanel = new Panel { Position = new Vector2(640, 556), Size = new Vector2(628, 156) };
        _mfPanel.Modulate = new Color(0.09f, 0.1f, 0.14f, 0.98f);
        AddChild(_mfPanel);

        // 🔴 `ui_spec.md` §1.2：**E 区多功能框 = 可切换分页**（DD 式）——
        //    规格列的是 详情 ／ 日志 ／ 序列 ／ 编成；我再加【地图】（片③ 用户点名要的）
        string[] tabs = { "详情", "日志", "序列", "编成", "地图" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            var b = new Button { Position = new Vector2(8 + (i * 84), 6), Size = new Vector2(80, 26), Text = tabs[i] };
            b.Pressed += () => SetMultiFunctionPage(idx);
            _mfPanel.AddChild(b);
            _mfTabs.Add(b);
        }

        _mfContent = new Label
        {
            Position = new Vector2(10, 38),
            CustomMinimumSize = new Vector2(606, 110),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _mfContent.AddThemeFontSizeOverride("font_size", 12);
        _mfPanel.AddChild(_mfContent);

        _mfMap = new Darkest.Ui.BattleMiniMap { Position = new Vector2(10, 34), Size = new Vector2(606, 116) };
        _mfMap.Visible = false;
        _mfPanel.AddChild(_mfMap);

        SetMultiFunctionPage(0);
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
            _mfTabs[i].Modulate = i == page ? new Color(1f, 0.95f, 0.7f) : new Color(0.75f, 0.75f, 0.8f);
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
        var bg = new Panel { OffsetLeft = 0, OffsetTop = 0, OffsetRight = 1280, OffsetBottom = 720 };
        bg.Modulate = new Color(0.12f, 0.12f, 0.16f, 0.97f);
        AddChild(bg);

        _statusLabel = new Label { Position = new Vector2(16, 8), CustomMinimumSize = new Vector2(620, 26) };
        _statusLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.85f));
        AddChild(_statusLabel);
        _retreatButton = new Button { Position = new Vector2(1076, 6), Size = new Vector2(184, 32), Text = "撤退 0%" };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        AddChild(_retreatButton);

        // 🔴 片③（`ui_three_screens.md` §3）：**顶部队伍进度条：段数**（**不是 HP 条**）——
        //    线性模式没有"段"，此时显示战斗目标胜场（如实标注口径，不假装有总段数）。
        _progressLabel = new Label { Position = new Vector2(560, 8), CustomMinimumSize = new Vector2(320, 24) };
        _progressLabel.AddThemeFontSizeOverride("font_size", 13);
        _progressLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 1f));
        AddChild(_progressLabel);

        // 🔴 片③：**E 区 · 多功能框**（`ui_spec.md` §1.2：右 · **可切换分页**：详情 ／ 日志 ／ 地图）
        BuildMultiFunctionBox();
        _actionOrderLabel = new Label { Position = new Vector2(16, 40), CustomMinimumSize = new Vector2(80, 24), Text = "本回合顺序" };
        _actionOrderLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_actionOrderLabel);

        AddRowTitle("我方　4 · 3 · 2 · 1", HeroX0, StageY - 22);
        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(HeroX0 + i * (CardW + GapX), StageY, CardW, CardH));
        }

        var vs = new Label { Position = new Vector2(636, StageY + 70), CustomMinimumSize = new Vector2(20, 24), Text = "VS" };
        vs.AddThemeColorOverride("font_color", new Color(1, 0.6f, 0.6f));
        AddChild(vs);

        AddRowTitle("敌方　1 · 2 · 3 · 4", EnemyX0, StageY - 22);
        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(EnemyX0 + i * (CardW + GapX), StageY, CardW, CardH));
        }

        AddRowTitle("支援位 5 · 6", HeroX0, SupportY - 22);
        for (int i = 0; i < 2; i++)
        {
            AddChild(BuildCard(HeroX0 + i * (SupportW + GapX), SupportY, SupportW, SupportH));
        }

        _skillTitle = new Label { Position = new Vector2(16, SkillTitleY), CustomMinimumSize = new Vector2(760, 24), Text = "技能栏（轮到行动者时可用）" };
        _skillTitle.AddThemeColorOverride("font_color", new Color(0.9f, 1, 0.9f));
        AddChild(_skillTitle);
        _hintLabel = new Label { Position = new Vector2(790, SkillTitleY), CustomMinimumSize = new Vector2(470, 24), Text = "" };
        _hintLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.5f));
        AddChild(_hintLabel);

        _reinforceButton = new Button { Position = new Vector2(1004, SkillBarY), Size = new Vector2(116, 88), Text = "增援" };
        _reinforceButton.Pressed += () => _reinforce?.Invoke();
        AddChild(_reinforceButton);
        _moveButton = new Button { Position = new Vector2(1132, SkillBarY), Size = new Vector2(116, 88), Text = "移动" };
        _moveButton.Pressed += () => _move?.Invoke();
        AddChild(_moveButton);

        // S5.2 待命（放弃本次行动；不消耗 SP）
        _passButton = new Button { Position = new Vector2(1132, SkillBarY + 96), Size = new Vector2(116, 44), Text = "待命" };
        _passButton.Pressed += () => _pass?.Invoke();
        AddChild(_passButton);

        _resultPanel = new Panel { Position = new Vector2(340, 210), Size = new Vector2(600, 260), Visible = false };        _resultPanel.Modulate = new Color(0.1f, 0.1f, 0.14f, 0.98f);
        AddChild(_resultPanel);
        _resultLabel = new Label { Position = new Vector2(24, 20), CustomMinimumSize = new Vector2(552, 220) };
        _resultLabel.AddThemeFontSizeOverride("font_size", 18);
        _resultPanel.AddChild(_resultLabel);

        // G2（O-55）：开发者日志面板（默认隐藏，F1 开关）——直接读事件流（唯一事实来源）
        _devLogButton = new Button { Position = new Vector2(890, 6), Size = new Vector2(180, 32), Text = "日志 F1" };
        _devLogButton.Pressed += ToggleDevLog;
        AddChild(_devLogButton);

        _devLogPanel = new Panel { Position = new Vector2(16, 96), Size = new Vector2(1248, 326), Visible = false };
        _devLogPanel.Modulate = new Color(0.06f, 0.07f, 0.1f, 0.97f);
        AddChild(_devLogPanel);
        _devLogLabel = new Label { Position = new Vector2(12, 8), CustomMinimumSize = new Vector2(1224, 310) };
        _devLogLabel.AddThemeFontSizeOverride("font_size", 12);
        _devLogPanel.AddChild(_devLogLabel);

        // 🔴 Godot 内置清单 ②（本轮轴：**容器 + 锚点** 的第一步）：**给本屏一个满屏 `Control` 根**
        //    为什么必须有它：① **Theme 只沿 Control/Window 祖先链继承** —— 本类是 `CanvasLayer`、
        //    不是 Control ⇒ 上一轮实测"中央 Theme 未生效（落在引擎默认 16）" **根因就在这**；
        //    ② 有了 `Control` 根才能谈**锚点/容器**（分辨率与多语言文本长度无关的布局）✓
        //    做法（最小改动）：建满屏根 ⇒ 把**直接挂在 CanvasLayer 下**的顶层控件**收编**进去
        //    （子控件随父一起移动；各处持有的引用是对象引用 ⇒ 不受影响）✓
        _uiRoot = new Control { Name = "UiRoot" };
        // ⚠️ 只 `SetAnchorsPreset`（或 `SetAnchorsAndOffsetsPreset`）在**入树前**算不出正确尺寸
        //    ⇒ 实测解成 1280×1280（应 1280×720）⇒ 显式取**视口可见矩形**，与项目基准分辨率一致 ✓
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // ⚠️ **不要**再显式赋 `Size`：锚点（0→1）之下 Godot 每个布局帧都会用**父矩形**重算 ⇒ 赋值必被覆盖 = 死代码。
        //    实测（headless）该环境的视口是 2560×2000；窗口模式下 = 基准 1280×720 × `stretch=canvas_items` 缩放
        //    ⇒ 我们只需断言"**锚点 1/1 且 size 跟随视口**"（见 `RootAudit`），不必自己算尺寸 ✓
        Darkest.Ui.DdTheme.Apply(_uiRoot);
        AddChild(_uiRoot);
        foreach (Node child in GetChildren().ToArray())
        {
            if (!ReferenceEquals(child, _uiRoot) && child is Control ctl)
            {
                RemoveChild(ctl);
                _uiRoot.AddChild(ctl);
            }
        }

        GD.Print($"[BattleUi] 满屏 Control 根就绪（size={_uiRoot.Size}）⇒ ① Theme 可继承 ② 后续锚点/容器的落点 ✓");
    }

    private Control _uiRoot = null!;

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
        return $"Control 根：size={_uiRoot.Size}　Theme={(_uiRoot.Theme is null ? "（无）" : "已挂中央 Theme")}　" +
               $"锚点={(int)_uiRoot.AnchorRight}/{(int)_uiRoot.AnchorBottom}　{inherited}";
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

    private void AddRowTitle(string title, float x, float y)
    {
        var label = new Label { Position = new Vector2(x, y), CustomMinimumSize = new Vector2(400, 22), Text = title };
        label.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 1f));
        AddChild(label);
    }

    private Control BuildCard(float x, float y, float w, float h)
    {
        var card = new Panel { Position = new Vector2(x, y), Size = new Vector2(w, h) };

        // ② 立绘占位框（色块 + 首字）
        var portraitBox = new Panel { Position = new Vector2(8, 6), Size = new Vector2(44, 44) };
        var glyph = new Label { Position = new Vector2(0, 8), CustomMinimumSize = new Vector2(44, 30), Text = "—", HorizontalAlignment = HorizontalAlignment.Center };
        glyph.AddThemeFontSizeOverride("font_size", 20);
        glyph.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        portraitBox.AddChild(glyph);
        card.AddChild(portraitBox);

        var name = new Label { Position = new Vector2(58, 8), CustomMinimumSize = new Vector2(w - 66, 24), Text = "[-]" };
        name.AddThemeFontSizeOverride("font_size", 15);
        var stats = new Label { Position = new Vector2(58, 30), CustomMinimumSize = new Vector2(w - 66, 20), Text = "" };
        stats.AddThemeFontSizeOverride("font_size", 12);
        var hp = new ProgressBar { Position = new Vector2(8, 58), Size = new Vector2(w - 16, 14), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var morale = new ProgressBar { Position = new Vector2(8, 78), Size = new Vector2(w - 16, 12), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var tag = new Label { Position = new Vector2(8, h - 28), CustomMinimumSize = new Vector2(w - 16, 20), Text = "" };
        tag.AddThemeFontSizeOverride("font_size", 12);
        card.AddChild(name);
        card.AddChild(stats);
        card.AddChild(hp);
        card.AddChild(morale);
        card.AddChild(tag);
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

        int[] pending = _host.PendingCandidates;
        bool targeting = _host.IsTargeting;
        bool targetsEnemy = _host.PendingTargetsEnemy;
        int phase = _host.ReinforcePhase;
        foreach ((Panel card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c in _cards)
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
                ? new Color(1.2f, 1.2f, 0.7f)
                : hl ? new Color(0.6f, 0.88f, 1.3f) : new Color(1, 1, 1);

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
                c.tag.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.2f));
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

    private static Color ArchetypeColor(string archetype, bool isPlayer) => archetype switch
    {
        "tank" => new Color(0.42f, 0.52f, 0.62f),
        "warrior" => new Color(0.62f, 0.35f, 0.32f),
        "commissar" => new Color(0.66f, 0.58f, 0.3f),
        "medic" => new Color(0.34f, 0.55f, 0.42f),
        "melee_soldier" => new Color(0.5f, 0.28f, 0.3f),
        "ranged_archer" => new Color(0.42f, 0.44f, 0.28f),
        "caster" => new Color(0.45f, 0.32f, 0.58f),
        _ => isPlayer ? new Color(0.4f, 0.45f, 0.55f) : new Color(0.5f, 0.35f, 0.35f),
    };

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
        foreach ((Panel panel, Label glyph) icon in _orderIcons)
        {
            icon.panel.QueueFree();
        }

        _orderIcons.Clear();
        float x = 96f;
        foreach (string id in order)
        {
            var unitId = new UnitId(id);
            bool isPlayer = d.Player.UnitAtPosition(unitId) is not null;
            string archetype = _host.ArchetypeOf(unitId);
            var panel = new Panel { Position = new Vector2(x, 34), Size = new Vector2(36, 30) };
            var glyph = new Label { Position = new Vector2(0, 2), CustomMinimumSize = new Vector2(36, 26), Text = NameOf(archetype).Substring(0, 1), HorizontalAlignment = HorizontalAlignment.Center };
            glyph.AddThemeFontSizeOverride("font_size", 14);
            panel.AddChild(glyph);
            bool isActive = _host.IsAwaitingPlayer && id == _host.ActiveActor.Value;
            panel.Modulate = isActive
                ? new Color(1.25f, 1.25f, 0.7f)
                : isPlayer ? new Color(0.62f, 0.72f, 0.95f) : new Color(0.95f, 0.6f, 0.6f);
            AddChild(panel);
            _orderIcons.Add((panel, glyph));
            x += 40f;
        }
    }

    private void FillCard((Panel card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c, UnitProjection u, Label portrait)
    {
        bool empty = u.UnitId == "-";
        string display = NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId);
        c.name.Text = empty ? $"[{u.Slot}] 空位" : $"[{u.Slot}] {display}";
        c.stats.Text = empty ? "" : $"HP {u.Hp}/{u.MaxHp}　士气 {u.Morale}";
        // ② 立绘占位框：首字 + 阵营/原型色块
        portrait.Text = empty ? "—" : display.Substring(0, 1);
        if (portrait.GetParent() is Panel box)
        {
            box.Modulate = empty ? new Color(0.35f, 0.35f, 0.35f) : ArchetypeColor(u.Archetype.Length > 0 ? u.Archetype : u.UnitId, c.isPlayer);
        }

        c.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        c.hp.Value = u.Hp;
        c.hp.Modulate = u.Weak ? new Color(1, 0.5f, 0.5f) : new Color(0.5f, 1, 0.6f);
        c.morale.MaxValue = 100;
        c.morale.Value = u.Morale;
        c.morale.Modulate = c.isPlayer ? new Color(1, 0.92f, 0.5f) : new Color(0.55f, 0.55f, 0.55f);
        c.tag.Text = empty ? "" : (u.Weak ? "虚弱" : (c.isPlayer ? "我方" : "敌方"));
        // D4（#206）：死门后遗症必须显著标注（橙字）
        if (!empty && _host?.Director is { } dir && dir.Buffs.Has(new UnitId(u.UnitId), "deaths_door_recovery"))
        {
            c.tag.Text = "死门后遗症（伤+10% 命中−5 速−1）";
            c.tag.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.2f));
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
        const int perRow = 8; // ③ 技能栏图标格（首 2 字为图标，悬停看全名/原因）
        for (int i = 0; i < poolIds.Length; i++)
        {
            string skillId = poolIds[i];
            SkillProjection sp = p.Skill(skillId, actor, d.Player, d.Enemy, pool);
            string full = SkillName(skillId);
            var b = new Button
            {
                Position = new Vector2(24f + (i % perRow) * 94f, SkillBarY + (i / perRow) * 94f),
                Size = new Vector2(88, 88),
                Text = full.Length <= 2 ? full : full.Substring(0, 2),
                Disabled = sp.Reason != AvailabilityReason.Ok,
                TooltipText = sp.Reason == AvailabilityReason.Ok ? SkillTooltip(skillId, actor, d) : $"{full}（{sp.Tooltip}）",
            };
            b.AddThemeFontSizeOverride("font_size", 20);
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            AddChild(b);
            _skillButtons.Add(b);
        }
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
            return (new Color(1f, 0.62f, 0.25f), "震慑");
        }

        return mental ? (new Color(0.78f, 0.55f, 1f), null) : (default, null);
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