using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;

/// <summary>
/// M8.0 ③（`#283`）：**回城场景 Hamlet**（局外养成层的入口）。
///
/// 🔴 职责边界（`blueprint §9.15`）：本类**只渲染 + 转发** —— 金钱来自**跨趟持有者**
/// （`ExpeditionContext.Gold`，与经济数据同源），减压/招募按钮在 ④/⑤ 落地前**置灰**
/// （**不假装可用**）。
/// 🔴 红线 18：Hamlet 必须**从启动场景可达**（`BattleRoot` 的按钮 / `--hamlet` CLI）。
/// </summary>
public partial class HamletRoot : Control, IUiPanel
{
    /// <summary>🔴 IUiPanel：声明本 panel 名（日志/断言）✓</summary>
    public string PanelName => "Hamlet";
    /// <summary>🔴 IUiPanel：城池屏需要常显 HUD（资源/名册计数）⇒ 开 ✓</summary>
    public bool WantsBaseHud => true;

    private Label _status = null!;
    private Label _hint = null!;
    private Label _upgradeStatus = null!;
    private Label _saniStatus = null!;
    // 🔴 片①（三界面卡 §1）：DD 式排布的新增元素
    private Label _banner = null!;
    private Label _rosterCount = null!;
    private Label _resourceBar = null!;

    // 🔴 用户要求（2026-09-16）：以下三块**搬进【建筑详情】**（主屏不再一眼可见）✓
    private HBoxContainer? _reliefRow;
    // 🆕 2026-10-01 M7u：驿站招募行（`_recruitRow`）已**搬家** ⇒ `HamletRoot.Recruit.cs`
    //    （竖列 + 懒建；字段声明随实现走，避免同一字段两处声明）✓
    private HBoxContainer? _saniRow;


    private Button? _menuButton;                 // 🔴 P2：底部"☰ 菜单"入口 ✓
    private PanelContainer? _hamletMenu;         // 🔴 P2：城池二级菜单（弹窗）✓
    private VBoxContainer? _hamletMenuBody;
    private string[] _buildingIds = System.Array.Empty<string>();     // 🔴 P3：左列切换用的同一份清单 ✓
    private string[] _buildingLabels = System.Array.Empty<string>();
    private Label _buildingInfo = null!;
    private Label _rosterTitle = null!;
    private VBoxContainer _rosterList = null!; // 🔴 §14：名册竖列的容器（行由 Refresh 填，不再写坐标）
    private Button _embark = null!;
    // 🔴 片②：详情面板的数据源（装配时读入）
    private RosterConfig? _rosterCfgForDetail;
    private SkillsConfig? _skillsCfg;
    private UnitsConfig? _unitsCfg;
    private CampSkillsConfig? _campSkills;
    private SanitariumConfig? _saniCfg;
    // 🆕 2026-10-01 M5u：怪癖库（`quirks.json` 170 条）—— 供【高级新兵掷签】与【详情显示分类/互斥】✓
    private QuirksConfig? _quirksCfg;
    private readonly Dictionary<string, Button> _saniButtons = new(); // M8.2：三项服务按钮（用于置灰）
    private EconomyConfig _cfg = null!;
    // 🆕 2026-10-01 M7u：解锁表（`Build` 解析后存下）—— 供 `RecomputeRosterCap` 按
    //    「单一来源 = 马车曲线」重算名册可用上限（`RunProgress.CurrentRosterCap`）✓
    private UnlocksConfig? _unlockCfg;
    private string? _selectedHero;                       // ② 选人权：玩家选中的被减压者
    private readonly List<Button> _heroButtons = new();  // 动态重建（士气 < 50 的人）
    private readonly Dictionary<string, Button> _upgradeButtons = new(); // M8.1：三栋升级按钮（用于置灰）
    // 🔴 **二级窗口（弹窗）**：建筑详情 —— 用户 2026-09-14 要求「弹窗要能打开也能关闭」「建筑详细使用走二级窗口」
    private PanelContainer? _buildingPopup;
    private Darkest.UI.OverlayLayer? _overlay;   // 🔴 Track 3：Overlay 层（模态统一住这里；缺失回落到旧父容器）✓
    private Label? _buildingPopupTitle;
    private VBoxContainer? _buildingPopupBody;      // 正文区（DD body_base_pos 596,102）
    private VBoxContainer? _buildingPopupUpgrade;   // 升级按钮区（DD upgrade_base_pos 172,259）
    private VBoxContainer? _buildingPopupTrees;     // 升级树区（DD upgrade_trees_offset → 172,454）· 宿主 = 数字链 ＋ M6u「code 全表」
    private string? _buildingPopupId;
    private readonly Darkest.Core.Events.CombatLog _log = new();
    private readonly Darkest.Core.Rng.RngProvider _rng = new(20260909);



    // ------------------------------------------------------------------
    // 🔴 片② 角色详情（`tasks/ui_three_screens.md` §2）—— **城池右侧名册点行 ⇒ 打开**（唯一入口）
    //    实现取舍：**用 Hamlet 内的覆盖面板**（不新建场景）⇒ 无需场景路由；
    //    "返回城池"= 关闭面板（红线 18：不是孤岛）。数据全部真读既有持有者。
    // ------------------------------------------------------------------

    private PanelContainer? _detailPanel; // 🔴 §14：详情面板 = 满屏不透明 PanelContainer（不再是 Node2D 浮层）
    private Label? _detailLeft;
    private Label? _detailRight;
    private Label? _detailCampSkills;
    private Label? _detailQuirks;                   // 🆕 2026-10-01 M5u：怪癖区（分类 + 互斥 + 悬停全文）✓
    private HBoxContainer? _detailSkills;          // 🔴 P4：技能图标行（图标 + tooltip 讲解）✓
    private PanelContainer? _detailRecommend;      // 🔴 P4：右上"推荐位置"留框 ✓
    private string? _detailHeroId;

    /// <summary>详情面板是否已打开（供冒烟断言）。</summary>
    public bool DetailOpen => _detailPanel is not null && _detailPanel.Visible;

    /// <summary>🔴 供冒烟/自检：**建筑详情弹窗（二级窗口）是否打开** —— 判据"能开也能关"的可断言读数 ✓</summary>
    public bool BuildingPopupOpen => _buildingPopup is not null && _buildingPopup.Visible;

    /// <summary>当前详情显示的是谁（供冒烟断言"显示的是被点的那个人"）。</summary>
    public string? DetailHeroId => _detailHeroId;


    /// <summary>🔴 片① ③：**悬停/点击某栋建筑 ⇒ 显示名称 + 功能 + 当前等级 + 下一级所需传家宝** ——
    /// **真读 `HeirloomStock`（`LevelOf` / `NextLevel().Cost`）**，不是写死文本（卡 §1.3 的验收要求）。
    /// </summary>
    /// <summary>
    /// 🔴 `next_round` ③ 辅助：某个解锁目标（如 `building:tavern`）需要**第几趟** ——
    /// 用于给"锁着的建筑"写出**可解释的**解锁条件（红线 21：不留不可解释的禁用）✓
    /// </summary>
    internal static int RunsRequiredFor(UnlocksConfig cfg, string target)
    {
        foreach (UnlockEntry e in cfg.Unlocks)
        {
            if (e.Unlocks.Contains(target))
            {
                return e.RequiredRunsFinished > 0 ? e.RequiredRunsFinished : 1;
            }
        }

        return 0;
    }





    /// <summary>
    /// 🔴 M8.1 / 红线 21 (b)：**升级按钮的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    /// 🔴 M8.1 / 红线 21 (b)：**升级的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// 🔴 用户 2026-09-14 改版后（建筑详细使用走**二级窗口**），本方法 = **完整玩家两步路径**：
    ///    ① 真实按下【建筑按钮】⇒ 打开建筑详情弹窗　② 再真实按下【弹窗里的升级按钮】⇒ 真正升级 ✓
    ///    ⚠️ 不能只按第一步（那只开弹窗、不升级）——否则 e2e 那句"升级后减压价变了"的证据会**静默失真**（红线 25）✓
    /// </summary>
    public void PressUpgrade(string building)
    {
        if (!_upgradeButtons.TryGetValue(building, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：找不到按钮（红线 21：按钮没挂上）");
            return;
        }

        GD.Print($"[HamletRoot] PressUpgrade({building})：① 发出真实 Pressed（按钮「{btn.Text}」，置灰={btn.Disabled}）⇒ 开二级窗口");
        btn.EmitSignal(BaseButton.SignalName.Pressed);

        Button? up = _buildingPopupUpgrade?.GetNodeOrNull<Button>("PopupUpgrade");
        if (up is null)
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：弹窗里没有升级按钮（没打开？）⇒ 未升级");
            return;
        }

        if (up.Disabled)
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：② 升级按钮**置灰**（传家宝不足/已满级）⇒ 不改等级（红线 21）");
            return;
        }

        GD.Print($"[HamletRoot] PressUpgrade({building})：② 发出真实 Pressed（弹窗按钮「{up.Text}」）⇒ 升级");
        up.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
