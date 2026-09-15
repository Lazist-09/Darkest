using System;
using System.Collections.Generic;
using Darkest.Data;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 `#327` **片 2 #6：扎营技能面板的【内容】独立化**（从 `ExpeditionRoot` 的内联模态里抽出来）。
/// 目的：让**任何宿主**都能复用同一份内容（远征场景 / 战斗的"地图模式"）。
///
/// 🔴 分工（照 `§13.8` 抽取方案）：
/// · **模态/外框由宿主提供**（`ExpeditionRoot.MakeModal` 仍归它；地图模式由 `DungeonHost` 提供）
/// · 本类只管**内容**：状态行 + 逐技能按钮
/// · "谁的目标 / 能不能用 / 点了做什么"**全部由宿主回调决定** ⇒ 本类**不碰内核**、**不重算数字**（红线 25 / `#325` D6）✓
/// </summary>
public partial class CampSkillPanel : PanelContainer
{
    private Label _status = null!;
    private GridContainer _box = null!;   // 🔴 相机 720 口径：技能按钮排成网格（原 VBox 竖排 ⇒ 面板需 574 高）
    private readonly List<Button> _buttons = new();

    /// <summary>逐技能按钮（供宿主/冒烟按序点击；顺序与 `Refresh` 传入的列表一致）✓</summary>
    public IReadOnlyList<Button> Buttons => _buttons;

    /// <summary>状态行（宿主写文案；本类不改写）✓</summary>
    public Label Status => _status;

    public override void _Ready()
    {
        var col = new VBoxContainer { Name = "CampSkillCol" };
        col.AddThemeConstantOverride("separation", 6);
        AddChild(col);

        _status = new Label
        {
            Name = "CampSkillStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 26), // `§14.2④`：给最小尺寸防塌陷
        };
        col.AddChild(_status);

        // 🔴 相机 720 口径（规则①）：技能按钮**排成网格**（DD 图②的图标阵）——
        //    实测竖排 11 个 ⇒ 面板需 **574 高**，把战斗屏内容需求顶到 918 > 720 ⚠️
        _box = new GridContainer { Name = "CampSkillActions", Columns = 5 };
        _box.AddThemeConstantOverride("separation", 6);
        col.AddChild(_box);
        Visible = false;
    }

    /// <summary>
    /// 宿主无关刷新：把"该原型用谁 / 够不够点数 / 点了做什么"三个决策交给宿主；
    /// `heroOf` 返回 null ⇒ 该技能**不出现**（角色专属但该原型不在队里）✓
    /// </summary>
    public void Refresh(
        IReadOnlyList<CampSkillConfig> skills,
        Func<CampSkillConfig, string?> heroOf,
        Func<CampSkillConfig, bool> affordOf,
        Action<CampSkillConfig, string> useOf,
        string statusText)
    {
        ClearButtons();
        _status.Text = statusText;

        foreach (CampSkillConfig skill in skills)
        {
            string? hero = heroOf(skill);
            if (hero is null)
            {
                continue;
            }

            string target = hero;
            var b = new Button
            {
                Name = $"CampSkill_{skill.Id}",
                Text = $"{skill.Name}（{skill.Cost} 点）",
                CustomMinimumSize = new Vector2(96, 28),   // 🔴 相机 720：11 个技能 5 列 3 行 ⇒ 面板高约 130（原 3 列 4 行约 270）   // 🔴 网格单元（相机 720 口径）
                Disabled = !affordOf(skill),
            };
            b.Pressed += () => useOf(skill, target);
            _box.AddChild(b); // 🔴 进容器（不手摆坐标）✓
            _buttons.Add(b);
        }

        Visible = true;
    }

    /// <summary>清空按钮（内容层）—— 宿主决定是否同时收起外框 ✓</summary>
    public void ClearButtons()
    {
        foreach (Button b in _buttons)
        {
            if (GodotObject.IsInstanceValid(b))
            {
                _box.RemoveChild(b);
                b.QueueFree();
            }
        }

        _buttons.Clear();
    }

    /// <summary>清空并隐藏（宿主收起模态时调用）✓</summary>
    public void Clear()
    {
        ClearButtons();
        Visible = false;
    }
}
