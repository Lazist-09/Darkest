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
/// BattleUI：暗黑地牢式排布（1280×720，中文）。单位**一字横排、两军对望**：
/// 我方 4·3·2·1（左，1 位贴近中线）｜敌方 1·2·3·4（右）；支援位 5·6 为我方后排小卡；
/// 顶部状态+回合条，底部当前行动者技能栏 + 增援/移动。
/// 高亮（修复）：① 当前行动者一律高亮（含支援位 5/6）；② 仅"需选目标"时高亮候选且**按阵营匹配**
/// （敌技亮敌卡 / 友技亮友卡；AOE·团队·自身不进入选目标 → 不会全亮）；③ 增援两步按阶段亮 5/6 → 1~4。
/// </summary>
public partial class BattleUI : CanvasLayer
{
    private const float CardW = 132f;   // 🔴 相机 1280 口径：146 → 132（4v4 横排收窄，§14.0 规则①）
    private const float CardH = 112f;   // 🔴 相机 720 口径：170→146→140→112（topology 路径仍超 62px）
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
    private PanelContainer? _slotLeft;    // 🔴 P5：左长条框 = 5 号位（向左靠齐）✓
    private Darkest.UI.LightBarPanel? _topTorch;  // 🔴 P5：正上方火把条（光照，居中）✓
    private HBoxContainer? _topLeftGroup;         // 🔴 P5：左上"任务与撤退"组 ✓
    private Label? _missionLabel;                 // 🔴 P5：任务文案（只读内核进度）✓
    private string _lastEnemyTipLogged = string.Empty; // 🔴 P5：悬停敌人信息读数（变化才打，避免刷屏）✓
    private int _rowsPrinted; // 🔴 行级读数打 3 次（早/中/晚）⇒ 能看出"谁在中途长大" ✓
    private Label? _eAreaTitle;           // 🔴 P5：E 区显式标题（第0页=角色详情／其余=多功能）✓
    private HBoxContainer? _actorRow;     // 🔴 P5：橙框"当前角色"行（头像 + 名字）✓
    private PanelContainer? _actorDetailBox;   // 🔴 技能框下方的【角色详情框】（用户 2026-09-16）✓
    private Label? _actorDetail;
    private ColorRect? _actorPortrait;    // 🔴 P5：当前角色头像留框里的色块占位 ✓
    private Label? _actorName;
    private PanelContainer? _slotRight;   // 🔴 P5：右长条框 = 6 号位（向右靠齐）✓
    private Label _mfContent = null!;
    private Darkest.UI.BattleMiniMap? _mfMap;
    private Darkest.UI.WalkMapView? _mfMapWalk;      // 🔴 主程序 (A)：地图页的【格子主画面】（拓扑模式）✓
    private string _lastMapPageSketch = string.Empty;
    private int _mfPage;
    private readonly List<Button> _mfTabs = new();

    /// <summary>E 区当前分页（0 详情 ／ 1 日志 ／ 2 地图）—— 供冒烟断言。</summary>
    /// <summary>🔴 `#327` **片 1 第一步：模式状态机**（`Battle ⇄ Map`）——
    /// 铁律（架构 `S1`）：**切模式不得重建骨架** ⇒ 本实现**只切可见性/页签，不 `QueueFree` 任何节点** ✓
    /// 地图模式 = **复用 E 区地图页 + 右下角小地图**（不新造控制树 ⇒ 与"无缝"同向）✓
    /// 📌 读数：`ModeAudit()`（模式 + E 区页 + 骨架 id）⇒ 可断言"切模式后骨架 id 不变"</summary>
    public enum SceneMode
    {
        Battle,
        Map,
    }

    /// <summary>🔴 **地图页签索引的单一出处**（UI 侧）——
    /// 此前这里写死 `4`，而 `BattleRoot` 另写 `const MapPageIndex = 4` 并注明"改页签表必须同步" ⚠️
    /// ⇒ 两份真值（`#325` D6 同族）⇒ 现在 UI 侧只此一处；`BattleRoot` 应改引它（我已投窗口请他指过来）✓</summary>
    public const int MapPageIndex = 4;

    private SceneMode _mode = SceneMode.Battle;

    /// <summary>当前模式（供冒烟/读数）。</summary>
    public SceneMode Mode => _mode;

    /// <summary>🔴 切到**地图模式**：只切页签与可见性 —— **不重建任何节点**（`S1`）✓</summary>
    /// <summary>🔴 `#327` **S1 断言**（比"只打印 id"更进一步）：**切模式前后骨架 id 必须完全相同**。
    /// 这是"无缝"的**可测定义**：变了 ⇒ 说明发生了场景切换或整体重建 ✗（`--battle-map-mode` 冒烟可复现）✓</summary>
    private string _skeletonAtBind = string.Empty;

    /// <summary>重绑次数（`#327` S1 的多入口断言用：第 2 次起才有"是否被重建"的对比）✓</summary>
    private int _bindCount;

    /// <summary>记录绑定时刻的骨架指纹（`Bind()` 末尾调用）。</summary>
    private void CaptureSkeleton() => _skeletonAtBind = SkeletonFingerprint();

    /// <summary>骨架指纹（只含**必须存活**的骨架节点 id，不含可重建内层）。</summary>
    private string SkeletonFingerprint()
        => $"{IdOf(_uiRoot)}|{IdOf(_bg)}|{IdOf(_topRow)}|{IdOf(_midRow)}|{IdOf(_bottomRow)}|{IdOf(_mfMap)}";

    /// <summary>🔴 切模式后的 **S1 判定**（可断言）：指纹与绑定时刻一致 ⇒ ✅ 未重建；否则 🔴。</summary>
    public string SkeletonVerdict()
    {
        bool same = _skeletonAtBind.Length > 0 && _skeletonAtBind == SkeletonFingerprint();
        return same
            ? "✅ S1 通过：切模式后骨架**未重建**（id 与绑定时完全一致）"
            : $"🔴 S1 未通过：骨架 id 变了（绑定 {_skeletonAtBind} ⇒ 现在 {SkeletonFingerprint()}）⇒ 查重建/场景切换";
    }

    // 🔴 `#327` **片 2 第一步：地图模式的地牢面板宿主**（架构分片：6 个地牢面板逐个迁进地图模式）
    //    契约：**骨架（根/背景/三行/E 区/地图）只建一次**；地牢面板挂在这个宿主里
    //    ⇒ 模式切换只**增删/切可见性**，**从不重建骨架** ⇒ S1 断言仍成立 ✓
    private Control? _dungeonHost;


    /// <summary>
    /// 🔴 P5（用户参考图②）：**左右长条框 = 5／6 号位（后排）** —— 数据来自内核投影（`UnitProjection.Slot`）；
    /// **空位如实显示"（空）"**（红线 21：不留不可解释的空）✓
    /// </summary>
    private void RefreshBackSlots()
    {
        if (_host?.Projector is null)
        {
            return;
        }

        UnitProjection[] players = _host.Projector.Units(player: true).ToArray();
        FillBackSlots(_slotLeft, players, new[] { 5, 6 });   // 🔴 5/6 号位同框（用户要求）✓
    }

    /// <summary>填一个长条框：标题 + 立绘留框(色块占位) + 名字；空位 ⇒ 如实"（空）"✓</summary>
    private void FillBackSlots(PanelContainer? box, UnitProjection[] players, int[] slots)
    {
        if (box is null || !GodotObject.IsInstanceValid(box))
        {
            return;
        }

        foreach (Node old in box.GetChildren().ToArray())
        {
            box.RemoveChild(old);
            old.QueueFree();
        }

        var col = new VBoxContainer { Name = "BackSlotsCol" };
        col.AddThemeConstantOverride("separation", 6);
        box.AddChild(col);

        foreach (int slot in slots)
        {
            // 5/6 号位行（2 处同构）实例化模板 scenes/ui/slot_row.tscn（用户 2026-09-17 复用规则）
            Darkest.UI.SlotRowTemplate? slotRow = Darkest.UI.SlotRowTemplate.TryCreate(slot);
            VBoxContainer row = slotRow ?? new VBoxContainer { Name = $"BackSlot{slot}Row" };
            if (slotRow is null)
            {
                row.AddThemeConstantOverride("separation", 2);
            }

            col.AddChild(row);

            var title = new Label { Name = $"BackSlot{slot}Title", Text = $"{slot} 号位" };
            title.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
            row.AddChild(title);

            UnitProjection? u = players.FirstOrDefault(x => x.Slot == slot);
            if (u is null || u.UnitId == "-")
            {
                row.AddChild(new Label { Name = $"BackSlot{slot}Empty", Text = "（空）" });
                continue;
            }

        string display = NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId);
        var frame = new PanelContainer { Name = $"BackSlot{slot}Frame", CustomMinimumSize = new Vector2(26, 26) };
        row.AddChild(frame);
        frame.AddChild(new ColorRect
        {
            Name = "Placeholder",
            Color = WithPlaceholderAlpha(Darkest.UI.DdTheme.ArchetypeColor(u.Archetype.Length > 0 ? u.Archetype : u.UnitId, isPlayer: true)),   // 🔴 规则②：α 取调色板
        });
        var nameLabel = new Label { Name = $"BackSlot{slot}Name", Text = display, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        nameLabel.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
        row.AddChild(nameLabel);
        box.TooltipText = $"{slot} 号位：{display}　HP {u.Hp}/{u.MaxHp}　士气 {u.Morale}";
        }
    }

    /// <summary>🔴 策划 `#348`③：战斗单帧占位 —— 统一走共享入口 `HeroArt.CombatTexture()`（**只从 PlaceholderRoot 读**）✓
    /// ⚠️ V6 纪律：只证明"接口能装下 + UI 能显示"，**不证明**"动画能播"（需 Spine，本阶段裁掉）✓</summary>
    private static Texture2D? PlaceholderCombatTexture() => Darkest.UI.HeroArt.CombatTexture();
    /// <summary>🔴 用户规则②：**保留色相、只把 α 换成调色板里的占位透明度**（空闲位半透明 ⇒ 一眼看出"待填"）✓</summary>
    private static Color WithPlaceholderAlpha(Color hue)
    {
        hue.A = Darkest.UI.DdTheme.PlaceholderFill.A;
        return hue;
    }
    /// <summary>🔴 `retreat.md §8`：**放弃远征**（地图层·结束本趟·回城·不可逆）—— 由宿主注入（未注入 ⇒ 按钮不显示）✓</summary>
    private Action? _abandonExpedition;

    private Button? _abandonButton;
    private PanelContainer? _abandonConfirm;
    private bool _abandonWarned;
    private bool _labelProofed;   // 🔴 文本级自证只打一次 ✓
    private bool _abandonSmoked;  // 🔴 冒烟 --abandon 只按一次 ✓


    /// <summary>模式读数（**可断言**）：模式 ＋ E 区页 ＋ 骨架 id（切模式前后骨架 id 应不变 ⇒ 无缝）✓</summary>
    public string ModeAudit() => $"模式={_mode}　E区页={_mfPage}（地图页={MapPageIndex}）　{SkeletonAudit()}";

    public int MultiFunctionPage => _mfPage;

    /// <summary>🔴 地图页的**可断言摘要**（headless 冒烟：地图与远征侧读数同源）。</summary>
    public string DescribeMiniMap() => _mfMap?.Describe() ?? "mini-map: 未建";


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
        GD.Print("[BattleUI] 暗黑地牢式排布就绪（横排：我方 4321 ｜ 敌方 1234；支援位后排；底部技能栏）。");

        // 🔴 审计清单③：**进场就给焦点**（否则键盘/手柄用户"没有起点"，方向键无处可动）
        if (_cards.Count > 0 && _cards[0].card is Control first)
        {
            first.GrabFocus();
        }

        // 🔴 `#327` S1（**多入口断言**，架构 `…S1-APPROVED…` ③ 的口径升级：
        //    "凡必须有某个性质的东西，都要在【各入口/模式】下各断言一次"）——
        //    入口 ① 切模式（`EnterMapMode`/`ExitMapMode`）② **重入 `Bind()`**（再战 / 换一场）
        //    ⇒ 后者正是"骨架被重建"的历史靶子：这里**每次重绑都自动判定并留痕** ✓
        {
            string before = _skeletonAtBind;
            CaptureSkeleton();
            _bindCount++;
            if (before.Length > 0)
            {
                bool same = before == _skeletonAtBind;
                GD.Print(same
                    ? $"✅ S1（重入 Bind 第 {_bindCount} 次）骨架**未重建**（id 与上次绑定一致）"
                    : $"🔴 S1（重入 Bind 第 {_bindCount} 次）骨架**被重建**（{before} ⇒ {_skeletonAtBind}）" +
                      "　⇒ 这正是迁移要消灭的那条（架构 `§9.17` S1）");
            }
            else
            {
                GD.Print($"[UI S1] 首次绑定（第 {_bindCount} 次）：已记录骨架指纹，重绑/切模式时自动判定 ✓");
            }
        }
        // 🔴 冒烟：`--battle-support` ⇒ **真实点击【用支援包】**（与玩家同一条 `Pressed` 路径；红线 26）
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-support"))
        {
            CallDeferred(nameof(PressSupportPackButton));
        }

        // 🔴 `#327` 片 1 冒烟：`--battle-map-mode` ⇒ 走**真实模式切换**（不重建骨架）⇒ 打印模式 + 骨架 id 读数 ✓
        if (_pendingMapMode) { _pendingMapMode = false; GD.Print("[UI 模式] `Bind()` 完成 ⇒ 补进【地图模式】（此前因 UI 未建而延后）✓"); EnterMapMode(); }

        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-map-mode"))
        {
            CallDeferred(nameof(EnterMapMode));
        }

        // 🔴 **往返冒烟**：`--battle-map-mode-exit` ⇒ 进地图模式**再回战斗模式**（两个方向都要验证不重建；
        //    这同时给 `ExitMapMode()` 一个**真实调用者** —— 否则它就是我自己的"死声明"（红线 21）⚠️）✓
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-map-mode-exit"))
        {
            CallDeferred(nameof(EnterMapMode));
            CallDeferred(nameof(ExitMapMode)); // 顺序执行 ⇒ 得到"进→出"完整往返 ✓
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
        // 🔴 用户要求（2026-09-17）：「重复的 UI 元素记得能复用就建成能复用的」⇒ **战斗卡牌（10 张同构）抽模板**
        //    改为实例化 `scenes/ui/unit_card.tscn`（`[Tool]` ⇒ **编辑器里改一处 = 10 张卡一起变**）✓
        //    ⚠️ 场景缺失/类型不符 ⇒ **回落代码构建**（不崩、不静默）；🔴 节点名保持一致（`FillCard` 按名取）✓
        PanelContainer card;
        Label name;
        Label stats;
        ProgressBar hp;
        ProgressBar morale;
        Label tag;
        Label glyph;
        if (Darkest.UI.UnitCardTemplate.TryInstantiate() is Darkest.UI.UnitCardTemplate unitCard)
        {
            card = unitCard;
            name = unitCard.NameLabel!;
            stats = unitCard.StatsLabel!;
            glyph = unitCard.GlyphLabel!;
            hp = unitCard.HpBar!;
            morale = unitCard.MoraleBar!;
            tag = unitCard.TagLabel!;
            glyph.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextPrimary);
        }
        else
        {
            card = new PanelContainer();
            var col = new VBoxContainer { Name = "CardCol" };
            col.AddThemeConstantOverride("separation", 2);
            card.AddChild(col);

            var head = new HBoxContainer { Name = "CardHead" };
            head.AddThemeConstantOverride("separation", 4);
            col.AddChild(head);

            // ② 立绘占位框（色块 + 首字）—— 🔴 **必须是 `PanelContainer`**：`Panel` 不是容器 ⇒ Label 变宽会溢出压邻居（同上）
            var portraitBox = new PanelContainer { Name = "portraitBox", CustomMinimumSize = new Vector2(44, 44) };
            head.AddChild(portraitBox);
            glyph = new Label
            {
                Name = "glyph",
                Text = "—",
                CustomMinimumSize = new Vector2(44, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true, // 🔴 长文本裁切（`§14.6`）
            };
            glyph.AddThemeFontSizeOverride("font_size", 20);
            glyph.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextPrimary);
            portraitBox.AddChild(glyph);

            var nameCol = new VBoxContainer { Name = "nameCol", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameCol.AddThemeConstantOverride("separation", 2);
            head.AddChild(nameCol);

            name = new Label { Name = "name", Text = "[-]", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            name.AddThemeFontSizeOverride("font_size", 15);
            nameCol.AddChild(name);

            stats = new Label { Name = "stats", Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            stats.AddThemeFontSizeOverride("font_size", 12);
            nameCol.AddChild(stats);

            hp = new ProgressBar { Name = "hp", MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
            col.AddChild(hp);
            morale = new ProgressBar { Name = "morale", MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) };
            col.AddChild(morale);

            tag = new Label { Name = "tag", Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            tag.AddThemeFontSizeOverride("font_size", 12);
            col.AddChild(tag);
        }

        card.CustomMinimumSize = new Vector2(w, h);
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // 均分宽 ⇒ 位置编号稳定映射横坐标（`#321`③）
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

    /// <summary>
    /// 🔴 主程序清单第 3 条：**敌方意图预览** —— 逐敌方单位问内核"下一步想做什么"，**只渲染、不推断**：
    /// `IntentProjection(SkillId, TargetSlots, Status)`；`Status` **照显**（`not_an_enemy` / `disabled` 等）
    /// ⇒ 红线 21：不留不可解释的空态 ✓（预览用隔离 RNG ⇒ **不吃战斗抽数**）
    /// </summary>
    private void RefreshEnemyIntent()
    {
        if (_host?.Director is null || _intentText is null)
        {
            return;
        }

        var parts = new List<string>();
        foreach (Darkest.Core.Contracts.UnitId id in _host.Director.LastRoundOrder)
        {
            if (_host.Director.Enemy.UnitAtPosition(id) is null)
            {
                continue; // 只问敌方（内核也会对非敌方回 `not_an_enemy`）
            }

            Darkest.Gameplay.Sim.Director.IntentProjection p = _host.PreviewIntent(id);
            string what = p.SkillId is null ? "—" : SkillName(p.SkillId);
            string slots = p.TargetSlots.Length == 0 ? "无目标" : "槽位 " + string.Join(",", p.TargetSlots);
            parts.Add($"{id.Value}：{what} → {slots}（{p.Status}）");
        }

        _intentText.Text = parts.Count == 0
            ? "敌方意图：（当前无敌方单位）"
            : "敌方意图：" + string.Join("　｜　", parts);

        // 🔴 读数自证（红线 25：不是"看起来像"）：把意图行**原文**打一次（内容变化时才打，避免刷屏）✓
        if (_intentText.Text != _lastIntentLogged)
        {
            _lastIntentLogged = _intentText.Text;
            GD.Print($"[UI 意图] {_intentText.Text}");
        }
    }

    private string _lastIntentLogged = string.Empty;


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
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(34, 34) };   // 🔴 2026-09-21 相机纠偏：54/42→34 ✓
            var glyph = new Label
            {
                CustomMinimumSize = new Vector2(34, 26),
                Text = NameOf(archetype).Substring(0, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true, // 🔴 长文本**裁切**而非溢出（`§14.6`）
            };
            glyph.AddThemeFontSizeOverride("font_size", 14);
            panel.AddChild(glyph);
            bool isActive = _host.IsAwaitingPlayer && id == _host.ActiveActor.Value;
            panel.Modulate = isActive
                ? Darkest.UI.DdTheme.Highlight
                : isPlayer ? Darkest.UI.DdTheme.Ally : Darkest.UI.DdTheme.Danger;
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

        // 🔴 策划 `#348`③：**战斗里能看见这个角色（单帧）** —— 玩家卡画占位 `sprite/combat.png` ✓
        //    ⚠️ 只画**单帧静态**（动画需 Spine，本阶段裁掉）；取不到图 ⇒ 保留"色块+首字" ✓
        if (c.isPlayer && !empty && portrait.GetParent() is PanelContainer artBox
            && PlaceholderCombatTexture() is Texture2D heroTex)
        {
            var art = new TextureRect
            {
                Name = "HeroArtPlaceholder",
                Texture = heroTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            art.MouseFilter = Control.MouseFilterEnum.Ignore; // 不抢卡片的悬停/点击 ✓
            artBox.AddChild(art);
            portrait.Visible = false; // 有立绘就不叠"首字"（否则糊成一团）✓
        }
        if (portrait.GetParent() is PanelContainer box)
        {
            box.Modulate = empty ? Darkest.UI.DdTheme.Muted : Darkest.UI.DdTheme.ArchetypeColor(u.Archetype.Length > 0 ? u.Archetype : u.UnitId, c.isPlayer);
        }

        c.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        c.hp.Value = u.Hp;
        c.hp.Modulate = u.Weak ? Darkest.UI.DdTheme.HpWeak : Darkest.UI.DdTheme.Hp;
        c.morale.MaxValue = 100;
        c.morale.Value = u.Morale;
        c.morale.Modulate = c.isPlayer ? Darkest.UI.DdTheme.Morale : Darkest.UI.DdTheme.MoraleEnemy;
        c.tag.Text = empty ? "" : (u.Weak ? "虚弱" : (c.isPlayer ? "我方" : "敌方"));
        // D4（#206）：死门后遗症必须显著标注（橙字）
        if (!empty && _host?.Director is { } dir && dir.Buffs.Has(new UnitId(u.UnitId), "deaths_door_recovery"))
        {
            c.tag.Text = "死门后遗症（伤+10% 命中−5 速−1）";
            c.tag.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.Shock);
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
            // 🔴 UI 编辑器化 B（用户 2026-09-17）：技能方块改为**实例化模板场景** `scenes/ui/skill_box.tscn`
            //    ⇒ 尺寸/字号/样式**在编辑器里改**（这就是"能在编辑器里直接干预"）；
            //    ⚠️ 场景不可用 ⇒ **回落代码构建**（不崩、不静默）✓
            Button b = Darkest.UI.SkillBoxTemplate.TryInstantiate() is Darkest.UI.SkillBoxTemplate box
                ? box
                : new Button { CustomMinimumSize = new Vector2(48, 48) };
            if (b.CustomMinimumSize.X <= 0f)
            {
                b.CustomMinimumSize = new Vector2(48, 48); // 兜底尺寸（模板若没设）✓
            }

            // 数据仍由代码填（**模板只管外观**）✓
            b.Text = full.Length <= 2 ? full : full.Substring(0, 2);
            b.Disabled = sp.Reason != AvailabilityReason.Ok;
            b.TooltipText = sp.Reason == AvailabilityReason.Ok ? SkillTooltip(skillId, actor, d) : $"{full}（{sp.Tooltip}）";
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            _skillBar.AddChild(b); // 🔴 §14：技能键进【C 区的技能栏容器】（不再手摆坐标）
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
            return (Darkest.UI.DdTheme.Shock, "震慑");
        }

        return mental ? (Darkest.UI.DdTheme.Mental, null) : (default, null);
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
