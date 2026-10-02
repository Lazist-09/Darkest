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

namespace Darkest.UI;

/// <summary>
/// BattleUI：暗黑地牢式排布（1280×720，中文）。单位**一字横排、两军对望**：
/// 我方 4·3·2·1（左，1 位贴近中线）｜敌方 1·2·3·4（右）；支援位 5·6 为我方后排小卡；
/// 顶部状态+回合条，底部当前行动者技能栏 + 增援/移动。
/// 高亮（修复）：① 当前行动者一律高亮（含支援位 5/6）；② 仅"需选目标"时高亮候选且**按阵营匹配**
/// （敌技亮敌卡 / 友技亮友卡；AOE·团队·自身不进入选目标 → 不会全亮）；③ 增援两步按阶段亮 5/6 → 1~4。
/// </summary>
public partial class BattleUI : Control, IUiPanel
{
    /// <summary>🔴 IUiPanel：声明本 panel 名（日志/断言）✓</summary>
    public string PanelName => "Battle";
    /// <summary>🔴 IUiPanel：战斗屏需要常显 HUD（资源/光照/回合）⇒ 开 ✓</summary>
    public bool WantsBaseHud => true;

    private const float CardW = 126f;    // 🔴 DD 1:1 ④-3b：132 → **84**（DD 英雄组 284→788 = 504px@1920 ⇒ ×0.667 ÷ 4 人 ≈ 84）⇒ 让两组能落进 DD 的 26.2% 带宽 ✓
    private const float CardH = 112f;   // 🔴 相机 720 口径：170→146→140→112（topology 路径仍超 62px）
    private const float GapX = 14f;        // 🔴 DD 1:1：DD hero_spacing 168 − 立绘宽 ≈154 = **间隙 14** ⇒ ×0.667 ≈ **9** ✓（更正：DD 立绘**并不重叠**，是我先前算错）
    private const float HeroX0 = 13f;
    private const float EnemyX0 = 653f;
    private const float StageY = 96f;
    private const float SupportY = 286f;
    private const float SupportW = 130f;
    private const float SupportH = 86f;
    private const float SkillTitleY = 400f;
    private const float SkillBarY = 428f;

    private BattleRoot? _host;
    private IBattleView? _view;
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
    private string _lastTileRowsSketch = string.Empty;   // 🔴 瓷砖网格字符留档的去重（`DungeonGrid.ToRows` 消费点）✓
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


    /// <summary>一侧的站位摘要（只读；阶段 2 去直读：走 _view.Units，不再触内核板）。</summary>
    private string DescribeSide(bool player)
    {
        var parts = new List<string>();
        foreach (var u in _view.Units(player))
        {
            parts.Add(u.UnitId == "-" ? $"{u.Slot}·空" : $"{u.Slot}·{NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId)}");
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
            _progressLabel.Text = $"[进度] 本场（线性 ／ 单场：无段数口径）　回合 {_view?.Support().Round ?? 0}";
            return;
        }

        int visited = flow.Map.Rooms.Count(r => flow.HasVisited(r.Id));
        _progressLabel.Text = $"[进度] 段 {flow.StepsDone}　房间 {visited}/{flow.Map.Rooms.Count}　" +
                              $"已胜 {flow.Wins}　终点 {flow.ReachedGoal}";
    }

    private Label _devLogLabel = null!;
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


}
