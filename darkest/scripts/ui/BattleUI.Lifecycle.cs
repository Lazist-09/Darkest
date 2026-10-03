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
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **战斗 · 绑定生命周期与输入族**（`Bind`（重绑即重录骨架指纹；`--battle-support`／`--battle-map-mode*` 冒烟入口）· `FocusAudit`（审计清单③）· `_UnhandledInput`（`dd_toggle_log`）· `_lastIntentLogged`）✓
/// ③ 🔴 依赖主类私有成员：`_host`／`_useSkill`／`_reinforce`／`_move`／`_retreat`／`_pass`／`_cards`／`_portraits`／`_orderIcons`／`_orderFor`／`_skillButtons`／`_skillBarFor`／`_skillBarWaiting`／`_skeletonAtBind`／`_bindCount`／`_pendingMapMode`；调 `Build()`／`CaptureSkeleton()`／`EnterMapMode()`／`ExitMapMode()`／`PressSupportPackButton()`／`ToggleDevLog()`／`RootAudit()`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
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

        // 🔴🔴 2026-10-03 修（真缺陷 · 玩家路径 · **1799 条/秒** `ObjectDisposedException` 的根因）：
        //    `QueueFree()` **本帧不销毁**（延到帧尾）⇒ 惰性面板的判据 `is null || !IsInstanceValid`
        //    在本帧内**仍然为假**（旧节点还「有效」）⇒ 不重建、不重挂；帧尾旧节点真死 ⇒ 字段**悬空** ⚠️
        //    ⇒ `Refresh()` 每帧访问已释放节点（实测对象 = `ExpeditionListPanel`，`BattleUI.Refresh.cs:138`）
        //    ⇒ 正解：**重建前先「忘掉」全部惰性节点字段**（它们都挂在刚被 `QueueFree` 的子树里）——
        //       之后各创建点的 `is null` 判据自然重建、`Refresh()` 也不再碰悬空引用 ✓
        ForgetLazyPanels();

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

    /// <summary>
    /// 🔴🔴 2026-10-03（根因修复）：**重建前作废全部惰性节点字段**（详见 `Bind()` 内注释）——
    ///   这些字段**不在 `Build()` 的赋值表里**（惰性建一次、之后只刷新）⇒ 必须在此显式置空，
    ///   否则 `QueueFree()` 的「帧尾销毁」会让它们悬空（= 1799 条 `ObjectDisposedException` 的来源）✓
    ///   纪律 ⚠️：**新增惰性面板**（`is null || !IsInstanceValid` 判据 + `AddChild`）时**必须登记到这里**。
    /// </summary>
    private void ForgetLazyPanels()
    {
        _dungeonHost = null;          // 地牢面板宿主（`DungeonHost()` 惰性建）
        _mapModeList = null;          // 本趟投影列表（崩溃现场对象）
        _mapModeCamp = null;          // 扎营面板
        _walkMap = null;              // 行走地图（格子主画面）
        _walkHud = null;              // 行走 HUD
        _mapModeCurio = null;         // Curio（事件房）
        _mapModeHunger = null;        // 饥饿面板
        _statusTray = null;           // 4v4 状态托盘（**屏幕空间覆盖层**内 ⇒ 随子树一起死；2026-10-03 搬出底栏）
        _banner = null;               // 战斗横幅（挂 `_uiRoot`）
        _mapModeScoutMark = null;     // 侦察标记（当前不创建：登记以免将来复活时悬空）
        _mapModeInventory = null;     // 背包（同上）
        _mapModeLightBar = null;      // 光照条（同上）
    }

    /// <summary>
    /// 🔴🔴 2026-10-03 修（真缺陷 · 玩家路径）：**接 ／ 断「本场战斗只读视图」** ——
    ///   实测（`.tmp_battle_base.txt:259`）：`_view` **全项目无人赋值**（`rg` 搜赋值零命中）
    ///   ⇒ `Refresh()` 首行 `if (_host is null || _view is null) return;` **整条刷新路径空转**
    ///   （卡片 ／ 技能栏 ／ 行动顺序条 ／ 5·6 号位都不更新），且 E 区【序列】页直接 `NullReferenceException` ⚠️
    ///   口径：**只在「战斗成立」时接**（`StartExpeditionBattleInScene` ／ `NewGame`）；
    ///   地图模式 ／ 未起战斗 **保持 `null`** ⇒ 空转是预期（E 区三页走空态，见 `RefreshMultiFunctionContent`）✓
    /// </summary>
    public void SetBattleView(IBattleView? view)
    {
        _view = view;
        GD.Print(view is null
            ? "[UI 视图] 🔻 已断开本场只读视图（战斗结束 ⇒ 地图模式里卡片 ／ 技能栏保持空态）✓"
            : "[UI 视图] ✅ 已接入本场只读视图（卡片 ／ 技能栏 ／ 顺序条 ／ 5·6 号位恢复刷新）✓");
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



    public override void _UnhandledInput(InputEvent e)
    {
        // 🔴 动作化（附 B ①）：`dd_toggle_log` 见 `project.godot [input]`（玩家可重映射）
        if (e.IsAction("dd_toggle_log"))
        {
            ToggleDevLog();
        }
    }


    private string _lastIntentLogged = string.Empty;

}
