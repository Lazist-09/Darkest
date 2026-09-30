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
using Darkest.Gameplay.Sim.Survival;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求按相位/职责切）✓
/// ② 本文件 = **战斗 · 主刷新族**（`Refresh(status)` 总刷新 + `FlashHint` 提示；只读内核投影重画三行）✓
/// ③ 🔴 依赖主类私有成员/状态：`_statusLabel`/`_progressLabel`/`_actorRow`/`_actorName`/`_actorDetail`/`_skillBar`/`_hintLabel`/`_hintTimer`/
///    `_orderIcons`/`_cards`/`_portraits`/`_mode`/`_host` 以及 `Refresh*` 各分族方法 ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
    public void Refresh(string status = "")
    {
        if (_host is null || _view is null)
        {
            return;
        }

        RefreshEnemyIntent(); // 🔴 敌方意图预览（只渲染）✓
        // 🔴 P5：左上"任务"文案 —— **只读内核进度**（无本趟 ⇒ 如实写"单场战斗"）✓
        if (_missionLabel is not null)
        {
            Darkest.Gameplay.Sim.Run.ExpeditionFlow? mFlow = Darkest.Gameplay.Scene.ExpeditionContext.Flow;
            _missionLabel.Text = mFlow is null
                ? "任务：单场战斗（无本趟进度）"
                : $"任务：本趟 第 {mFlow.StepsDone} 步　已胜 {mFlow.Wins}" +
                  (mFlow.IsFinished ? "　（本趟已结束）" : string.Empty);
        }

        // 🔴 P5：橙框"当前角色"（只读内核 `IsAwaitingPlayer` / `ActiveActor`）✓
        if (_actorName is not null && _host is not null)
        {
            if (_host.IsAwaitingPlayer)
            {
                var actor = _view.Units(true)
                    .FirstOrDefault(x => x.UnitId == _host.ActiveActor.Value);
                string actorName = actor is null ? _host.ActiveActor.Value
                    : NameOf(actor.Archetype.Length > 0 ? actor.Archetype : actor.UnitId);
                _actorName.Text = $"当前角色：{actorName}（请选技能）";

                if (_actorDetail is not null && actor is not null)
                {
                    string buffs = actor.Buffs.Count == 0 ? "无" : string.Join("、", actor.Buffs);
                    _actorDetail.Text = $"【角色详情】{actorName}（{actor.Archetype}）\n" +
                                        $"HP {actor.Hp}/{actor.MaxHp}　士气 {actor.Morale}　槽位 {actor.Slot}" +
                                        (actor.Weak ? "　**虚弱**" : string.Empty) + $"\n状态：{buffs}";
                }
                if (_actorPortrait is not null && actor is not null)
                {
                    _actorPortrait.Color = Darkest.UI.DdTheme.ArchetypeColor(
                        actor.Archetype.Length > 0 ? actor.Archetype : actor.UnitId, isPlayer: true);
                }
            }
            else
            {
                _actorName.Text = "当前角色：等待中（敌方行动）";
            }
        }

        // 🔴 行级高度读数（诊断工具，用户规则①）：`--ui-rows` ⇒ 打印三行 + 地牢宿主各自的最小尺寸需求
        //    用途：当"整屏需求 > 相机"时，**一眼看出是哪一行在撑**（不靠猜）✓
        if (Array.Exists(OS.GetCmdlineArgs(), x => x == "--ui-rows") && _rowsPrinted < 3 && Engine.GetProcessFrames() % 240 == 1)
        {
            _rowsPrinted++;
            GD.Print($"[UI 行读数] 顶栏 需 {_topRow.GetCombinedMinimumSize()}　主体 需 {_midRow.GetCombinedMinimumSize()}" +
                     $"　底栏 需 {_bottomRow.GetCombinedMinimumSize()}" +
                     (_dungeonHost is not null && GodotObject.IsInstanceValid(_dungeonHost)
                         ? $"　地牢宿主 需 {_dungeonHost.GetCombinedMinimumSize()}" : string.Empty) +
                     $"　C区 需 {_cArea.GetCombinedMinimumSize()}　E区 需 {(_eArea as Control)?.GetCombinedMinimumSize()}");

            // 🔴 逐行点名（诊断）：`BattleCol` 的每个直接子节点各需多少 ⇒ 定位"三行之和与整列需求差 230px"的谜团 ✓
            if (_uiRoot is not null)
            {
                Control? col = _uiRoot.GetNodeOrNull<Control>("BattleMargin/BattleCol");
                if (col is not null)
                {
                    GD.Print($"[UI 列自身] BattleCol 自身最小={col.CustomMinimumSize} 合计={col.GetCombinedMinimumSize()}" +
                             $"　BattleMargin 自身最小={(_uiRoot.GetNodeOrNull<Control>("BattleMargin")?.CustomMinimumSize.ToString() ?? "-")}" +
                             $"　帧={Engine.GetProcessFrames()}");
                }
                if (col is not null)
                {
                    foreach (Node ch in col.GetChildren())
                    {
                        if (ch is Control cc)
                        {
                            GD.Print($"[UI 列读数] {cc.Name} 需 {cc.GetCombinedMinimumSize()}　可见={cc.Visible}");
                        }
                    }
                }
            }

            // 🔴 逐面板点名（诊断）：地牢宿主里每个子面板各需多少高 ⇒ 超相机时**直接指出是谁**✓
            if (_dungeonHost is not null && GodotObject.IsInstanceValid(_dungeonHost))
            {
                foreach (Node ch in _dungeonHost.GetChildren())
                {
                    if (ch is Control cc)
                    {
                        GD.Print($"[UI 面板读数] {cc.Name} 需 {cc.GetCombinedMinimumSize()}　可见={cc.Visible}");
                    }
                }
            }
        }

        RefreshBackSlots();    // 🔴 P5：左右长条框（5／6 号位）✓

        // 🔴 相机 1280 口径：**多功能分区标题只在战斗模式显示** —— 地图模式下底栏已由地牢内容占用，
        //    该标题会与右长条框同排争空间（实测 1 对重叠：`EAreaTitle` ⟷ `BackSlot6Title`，均 y=683）✓
        if (_eAreaTitle is not null)
        {
            _eAreaTitle.Visible = _mode == SceneMode.Battle;
        }

        // 🔴 相机 720 口径 + DD 图②：**地图模式下隐藏"战斗专用"顶栏项** ——
        //    实测地图模式（含本趟流程）下顶栏需求 **1396×105** ⇒ 整屏 1428×907 > 相机 720 ⚠️
        //    DD 的地图模式本就不显示"本回合顺序/意图/进度/日志" ⇒ 按模式切可见性 ✓
        bool battleMode = _mode == SceneMode.Battle;

        // 🔴 相机 720 口径：**背包/投影列表也由"模式"统一裁决** ——
        //    实测 `MapModeInventory 需 298×312 且可见`（我在 `HostDungeonPanels` 里的隐藏被 `Initialize` 的
        //    `Visible = true` 覆盖了）⇒ 把模式可见性**集中到这里**（唯一权威处），否则"设了又被覆盖" ⚠️
        if (_mapModeInventory is not null) { _mapModeInventory.Visible = false; }
        if (_mapModeList is not null) { _mapModeList.Visible = false; }
        // 🔴 用户要求：5/6 号位**两种模式都显示**（原先只战斗模式 ⇒ "看不见 6 号位"）✓
        if (_slotLeft is not null) { _slotLeft.Visible = true; }

        // 🔴 `§8`①（**文本自证抓到的**）：**撤退（战斗内）与放弃远征（地图层）必须不同屏** ⇒
        //    实测地图模式里"撤退"仍可见 ⇒ 与放弃远征同屏（手滑 = 一趟白跑）⚠️ ⇒ 按模式裁决 ✓
        if (_retreatButton is not null) { _retreatButton.Visible = _mode == SceneMode.Battle; }   // 🔴 用字段 _mode（两处同名代码都能编译）

        // 🔴 `§8`①：**放弃远征（地图层）与撤退（战斗内）不得同屏** ⇒ 前者只在行走模式可见（且需宿主已注入动作）✓
        // 🔴 冒烟：`--abandon` ⇒ 按下【放弃远征】（**只弹出二次确认，不确认**，避免真结束一趟）✓
        if (_abandonButton is not null && _abandonExpedition is not null
            && _mode == SceneMode.Map && !_abandonSmoked
            && Array.Exists(OS.GetCmdlineArgs(), x => x == "--abandon"))
        {
            _abandonSmoked = true;
            PressAbandon();
        }

        if (_abandonButton is not null)
        {
            _abandonButton.Visible = _mode == SceneMode.Map && _abandonExpedition is not null;

            // 🔴 文本级自证（一次性）：**撤退/放弃远征两个按钮的"界面用词 + 可见性"**都留痕
            //    （§8 要求两个词不得混用；红线 21 要求"可见/不可见"都可解释）✓
            if (!_labelProofed)
            {
                _labelProofed = true;
                GD.Print($"[UI §8 用词] 撤退按钮文案=「{_retreatButton?.Text}」　可见={_retreatButton?.Visible}" +
                         $"　｜　放弃远征按钮文案=「{_abandonButton.Text}」　可见={_abandonButton.Visible}" +
                         $"（模式={(_mode == SceneMode.Map ? "Map" : "Battle")}　宿主已注入放弃动作={_abandonExpedition is not null}）✓");
            }

            // 🔴 红线 21（不留不可解释的状态）：**按钮为什么没出现**必须留痕一次 ✓
            if (_abandonExpedition is null && !_abandonWarned)
            {
                _abandonWarned = true;
                GD.Print("[UI 撤退/放弃] 宿主**未注入**放弃远征动作 ⇒ 该按钮**不显示**（不假装可用，红线 21）✓");
            }
        }   // 🔴 紫框内 6 号位：地图模式让位给地牢面板（实测曾与 ScoutMark/CampStatus 相压）
        _orderBox.Visible = battleMode;
        if (_intentText is not null) { _intentText.Visible = battleMode; }
        if (_progressLabel is not null) { _progressLabel.Visible = battleMode; }
        if (_devLogButton is not null) { _devLogButton.Visible = battleMode; }
        if (_statusLabel is not null) { _statusLabel.Visible = battleMode; }

        // 🔴 P5：正上方火把条（只读本趟 `Flow.Meter`；无本趟 ⇒ 隐藏 + 留痕，不编数字）✓
        Darkest.Gameplay.Sim.Run.ExpeditionFlow? torchFlow = Darkest.Gameplay.Scene.ExpeditionContext.Flow;
        if (_topTorch is not null)
        {
            if (torchFlow is not null)
            {
                _topTorch.Visible = true;
                _topTorch.Refresh(torchFlow.Meter,
                    Darkest.Gameplay.Sim.Survival.LightMeter.BoundariesFrom(torchFlow.Tuning.Light!.Tiers));
            }
            else
            {
                _topTorch.Visible = false; // 单场战斗（无本趟）⇒ 如实隐藏
            }
        }

        var support = _view.Support();

        _statusLabel.Text = _host.GameOver
            ? $"战斗结束（第 {support.Round} 回合）：{status}"
            : status.Length > 0 ? status : $"回合 {support.Round}";
        // #211（S0）必显 #10：支援点常驻显示（含本回合恢复预览）；数字只来自投影（UI 不得自行扣点）
        _statusLabel.Text += $"　　支援点 {support.SupportPoints}/{support.SupportCap}（下回合 {support.SupportRegenPreview}）";
        // 队列只保留头像方块（下方 RefreshOrderStrip）；原文字队列与方块重合已移除
        _actionOrderLabel.Text = "本回合顺序";
        _retreatButton.Text = support.CanRetreat && !_host.GameOver ? $"撤退（退出这场战斗） {support.RetreatRatePercent}%" : "本回合不可撤退";   // 🔴 §8 用词统一
        _retreatButton.Disabled = !support.CanRetreat || _host.GameOver;

        // 🔴 支援包按钮的可用性（**由内核持有者回答** → 置灰 + tooltip 说明；红线 21 不留不可解释的禁用）✓
        Darkest.Gameplay.Sim.Run.Inventory? supportBag = Darkest.Gameplay.Scene.ExpeditionContext.Flow?.Bag;
        bool hasPack = supportBag is not null &&
                       supportBag.Slots.Any(s => s.Kind == Darkest.Gameplay.Sim.Run.ItemKind.SupportPack);
        _supportButton.Disabled = _host.GameOver || !hasPack;
        _supportButton.TooltipText = supportBag is null
            ? "本场没有背包（单场战斗没有远征流程）⇒ 用不了支援包"
            : hasPack ? "用 1 个支援包换支援点（数量由内核决定）" : "背包里没有支援包";

        int activeSlot = _view.ActiveActorSlot();
        UnitProjection[] player = _view.Units(true).ToArray();
        UnitProjection[] enemy = _view.Units(false).ToArray();

        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[i], player[3 - i], _portraits[i]); // 我方 4,3,2,1
        }

        // 🔴 DD 1:1 ④-2：把 4v4 的 HP/压力条绑进 DD 托盘（与卡牌同源投影）✓
        FillStatusTray(player, enemy);

        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[4 + i], enemy[i], _portraits[4 + i]); // 敌方 1,2,3,4

            // 🔴 P5（用户参考图②）：**悬停敌人 ⇒ 显示敌人信息** —— 走 `TooltipText`（悬停即现），
            //    内容 = 名字/HP/士气/状态 + **意图**（`PreviewIntent`，隔离 RNG 不吃抽数）✓
            UnitProjection eu = enemy[i];
            if (eu.UnitId != "-" && _host is not null)
            {
                var ip = _view.IntentPreview(new UnitId(eu.UnitId), enabled: true);
                string intent = ip.SkillId is null
                    ? $"意图：{ip.Status}"
                    : $"意图：{SkillName(ip.SkillId)} → 槽位 {(ip.TargetSlots.Length == 0 ? "无" : string.Join(",", ip.TargetSlots))}";
                string buffs = eu.Buffs.Count == 0 ? "无" : string.Join("、", eu.Buffs);
                string tip = $"{NameOf(eu.Archetype.Length > 0 ? eu.Archetype : eu.UnitId)}（{eu.UnitId}）\n" +
                             $"HP {eu.Hp}/{eu.MaxHp}　士气 {eu.Morale}　状态：{buffs}\n{intent}";
                _cards[4 + i].card.TooltipText = tip;
                if (_lastEnemyTipLogged != tip)
                {
                    _lastEnemyTipLogged = tip;
                    GD.Print($"[UI 敌人信息·悬停] 槽位 {eu.Slot} ⇒ " + tip.Replace("\n", " ｜ "));
                }
            }
        }

        FillCard(_cards[8], player[4], _portraits[8]);
        FillCard(_cards[9], player[5], _portraits[9]);

        // 🔴 用户要求（2026-09-15）：**主体先做 4v4** —— 我方后备 2 位（5／6 号位）**不出现在主体相机里**，
        //    它们**只出现在左右长条框**（DD 图②的做法）⇒ 主体保持干净的我方 4 ↔ 敌方 4 ✓
        //    ⚠️ 只**隐藏**不删：`_cards` 下标映射（Refresh 依赖）保持不变 ⇒ 不牵动任何既有逻辑 ✓
        _cards[8].card.Visible = false;
        _cards[9].card.Visible = false;

        RefreshOrderStrip(support.ActionOrderThisRound);
        PlayMotionFromNewEvents();

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
            Darkest.UI.UiSfx.Play(Darkest.UI.UiSfx.Kind.Settle); // ③ 结算（胜/败）
            GD.Print($"[UI 动效] {MotionAudit()}");
            GD.Print($"[UI 音效] {Darkest.UI.UiSfx.Audit()}");
            GD.Print($"[UI S1] {SkeletonAudit()}"); // 🔴 `#327` S1：骨架 id 读数（无缝的可测定义）
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
                hl = c.isPlayer && c.slot is 5 or 6 && _view.Units(true).Any(u => u.Slot == c.slot && u.UnitId != "-");
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
                ? Darkest.UI.DdTheme.Highlight
                : hl ? Darkest.UI.DdTheme.Ally : Darkest.UI.DdTheme.TextPrimary;

            // G3（O-56）：悬停单位卡 → 详情（属性/士气/buff/技能表；敌方同样全暴露）
            c.card.TooltipText = DetailTooltip(c.isPlayer, c.slot);

            // D7（#209）三态可读性：普通物理（默认掉血条）/ 精神（紫）/ 被暴击（橙·震慑）
            UnitId? runtimeId = c.isPlayer
                ? (_view.Units(true).FirstOrDefault(u => u.Slot == c.slot && u.UnitId != "-") is { } pu ? new UnitId(pu.UnitId) : null)
                : (_view.Units(false).FirstOrDefault(u => u.Slot == c.slot && u.UnitId != "-") is { } eu ? new UnitId(eu.UnitId) : null);
            (Color barColor, string? tagOverride) = RecentHitFeedback(runtimeId);
            if (barColor != default)
            {
                c.morale.Modulate = barColor;
            }

            if (tagOverride is not null)
            {
                c.tag.Text = tagOverride;
                c.tag.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.Shock);
            }
        }

        RefreshSkillBar();

        // 🔴 走模态栈（此前直接赋 Visible ⇒ 遮罩不同步、Esc 关不掉）
        if (_host.GameOver && !_resultPanel.Visible) { _overlay?.OpenModal(_resultPanel); }
        else if (!_host.GameOver && _resultPanel.Visible) { _resultPanel.Visible = false; _overlay?.CloseModal(_resultPanel); }
        if (_host.GameOver)
        {
            int[] c = _host.ResultCounts;
            _resultLabel.Text =
                $"{_host.ResultText}\n\n回合数：{_host.ResultRound}\n\n系统触发计数：\n" +
                $"　士气触底 {c[0]}　虚弱 {c[1]}　死门 {c[2]}\n　撤退 {c[3]}　美德 {c[4]}　折磨 {c[5]}\n　位移 {c[6]}\n\n按 R 重开（新 seed）";
        }

        // G2：日志面板可见时，仅在事件数变化时重绘（取尾部 20 行，避免每帧重建）
        if (_devLogPanel.Visible)
        {
            int count = _view.LogEvents().Count;
            if (count != _devLogRendered)
            {
                _devLogRendered = count;
                IReadOnlyList<string> lines = CombatLogText.Render(_view.LogEvents(), includeRng: false, NameOf, SkillName, BuffNameOf);
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
}
