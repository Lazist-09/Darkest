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
/// ② 本文件 = **战斗 · 卡牌建树与敌方意图族**（`BuildCard` 卡牌模板实例化/回落 · `RefreshEnemyIntent` 意图预览）✓
/// ③ 🔴 依赖主类私有成员：`_playerCards`/`_enemyCards`/`_playerSupport`/`_cards`/`_portraits`/`_host`/`CardW` 等常量 ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
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
        if (_host is null || _intentText is null)
        {
            return;
        }

        var parts = new List<string>();
        foreach (string id in _view.Support().ActionOrderThisRound)
        {
            var unitId = new UnitId(id);
            if (_view.IsPlayerUnit(unitId))
            {
                continue; // 只问敌方（内核也会对非敌方回 `not_an_enemy`）
            }

            var p = _view.IntentPreview(unitId, enabled: true);
            string what = p.SkillId is null ? "—" : SkillName(p.SkillId);
            string slots = p.TargetSlots.Length == 0 ? "无目标" : "槽位 " + string.Join(",", p.TargetSlots);
            parts.Add($"{id}：{what} → {slots}（{p.Status}）");
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
}
