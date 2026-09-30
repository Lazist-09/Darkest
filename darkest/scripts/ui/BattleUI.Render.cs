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
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **战斗 · 渲染族**（行动顺序条 `RefreshOrderStrip` · 卡牌填数 `FillCard` · 技能栏 `RefreshSkillBar`）✓
/// ③ 🔴 依赖主类私有成员：`_orderIcons`/`_orderBox`/`_cards`/`_portraits`/`_skillButtons`/`_skillBar`/`_skillTitle`/`_host` ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
    /// <summary>① 顶部回合条：头像格（首字 + 阵营色，当前行动者金框），替代纯文字。</summary>
    private void RefreshOrderStrip(IReadOnlyList<string> order)
    {
        int activePos = _view.ActiveActorSlot();
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
            bool isPlayer = _view.IsPlayerUnit(unitId);
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
        c.name.Text = string.Empty;   // (B) 用户授权 A2：DD 立绘层无文字 ⇒ 卡内文字不再显示，信息改由卡 Tooltip 承载 ✓
        c.stats.Text = string.Empty;   // (B)：同上（信息进 Tooltip）
        // ② 立绘占位框：首字 + 阵营/原型色块
        portrait.Text = string.Empty;   // (B)：DD 立绘上无首字 ⇒ 清空，保留色块 ✓

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
        string tagText = empty ? string.Empty : (u.Weak ? "虚弱" : (c.isPlayer ? "我方" : "敌方"));
        // D4（#206）：死门后遗症必须显著标注（橙字）
        if (!empty && _view.Buffs(new UnitId(u.UnitId)).Contains("deaths_door_recovery"))
        {
            c.card.TooltipText += "　⚠ 死门后遗症（伤+10% 命中−5 速−1）";   // (B)：关键告警保留在 Tooltip（不隐藏信息）✓
            // (B)：tag 文字已清空 ⇒ 不再设字色（告警进 Tooltip）
        }
    }

    private void RefreshSkillBar()
    {
        bool waiting = _host!.IsAwaitingPlayer;
        UnitId actor = _host.ActiveActor;

        bool combatActor = waiting && _view.ActiveActorSlot() is >= 1 and <= 4;
        _reinforceButton.Disabled = !waiting || _view.SwappedThisRound || _host.SupportPoints < _view.SupportCostReinforce;
        _reinforceButton.TooltipText = _host.SupportPoints < _view.SupportCostReinforce
            ? $"支援点不足（当前 {_host.SupportPoints} / 需要 {_view.SupportCostReinforce}）"
            : $"增援：调动支援位上场（消耗 {_view.SupportCostReinforce} 点）";
        _passButton.Disabled = !waiting;
        _passButton.TooltipText = $"待命：放弃本次行动（不消耗支援点）";
        _moveButton.Disabled = !combatActor || _view.SwappedThisRound || MoveCandidates(actor).Length == 0;

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
            SkillProjection sp = _view.Skill(skillId, actor, pool);
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
            b.TooltipText = sp.Reason == AvailabilityReason.Ok ? SkillTooltip(skillId, actor) : $"{full}（{sp.Tooltip}）";
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            _skillBar.AddChild(b); // 🔴 §14：技能键进【C 区的技能栏容器】（不再手摆坐标）
            _skillButtons.Add(b);
        }
    }
}
