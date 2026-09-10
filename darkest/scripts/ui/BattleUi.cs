using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;

/// <summary>
/// BattleUi：四分区控件层（blueprint §5d / ui_spec §1 / T-M5-05 剩余走查项）。
/// A 顶栏（回合+行动序列+撤退按钮）、B 战场（槽位方块：编号/单位/HP 条/士气条/虚弱标）、
/// D 技能栏（选中单位可用技能按钮 + 灰显 tooltip）、C 角色信息并入槽位方块。
/// 只经 BattleRoot 注入的只读投影刷新；命令经委托回调发给 BattleRoot→BattleDirector。
/// 状态真值永远在内核，控件只是投影（blueprint §4 B5）。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    private BattleDirector? _director;
    private BattleProjector? _projector;
    private RngProvider? _rng;
    private System.Action<UnitId, string>? _useSkill;
    private UnitId _selected = new("warrior");

    private Label _roundLabel = null!;
    private Label _actionOrderLabel = null!;
    private Button _retreatButton = null!;
    private readonly List<(Panel panel, Label text, ProgressBar hp, ProgressBar morale, Label tag)> _slots = new();
    private readonly List<Button> _skillButtons = new();
    private Container _skillBar = null!;
    private int _lastRefreshRound;

    public void Bind(BattleDirector director, BattleProjector projector, RngProvider rng,
        System.Action<UnitId, string> useSkill)
    {
        _director = director;
        _projector = projector;
        _rng = rng;
        _useSkill = useSkill;
        Build();
        Refresh();
    }

    private void Build()
    {
        var root = new VBoxContainer { AnchorsPreset = 15 }; // full rect
        AddChild(root);

        // A 顶栏：回合 + 行动序列 + 撤退按钮
        var top = new HBoxContainer();
        root.AddChild(top);
        _roundLabel = new Label { Text = "回合 0", CustomMinimumSize = new Vector2(140, 0) };
        top.AddChild(_roundLabel);
        _actionOrderLabel = new Label { Text = "行动序列: ", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(_actionOrderLabel);
        _retreatButton = new Button { Text = "撤退 0%", CustomMinimumSize = new Vector2(140, 0) };
        _retreatButton.Pressed += () => _director?.PlayerRetreat(_rng!);
        top.AddChild(_retreatButton);

        // 中部：左右两列槽位方块（我方 1~6 左 / 敌方 1~4 右），B 战场 + C 角色信息
        var mid = new HBoxContainer();
        root.AddChild(mid);

        var left = new VBoxContainer();
        mid.AddChild(left);
        for (int slot = 1; slot <= 6; slot++)
        {
            left.AddChild(BuildSlotPanel());
        }

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        mid.AddChild(right);
        for (int slot = 1; slot <= 4; slot++)
        {
            right.AddChild(BuildSlotPanel());
        }

        // D 技能栏：选中单位技能（按钮，灰显 tooltip 原因）
        _skillBar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        root.AddChild(_skillBar);
    }

    private Control BuildSlotPanel()
    {
        var panel = new Panel { CustomMinimumSize = new Vector2(320, 64) };
        var stack = new VBoxContainer { OffsetLeft = 8, OffsetTop = 4, OffsetRight = 312, OffsetBottom = 60 };
        panel.AddChild(stack);
        var text = new Label { Text = "[-] —" };
        var hp = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) };
        var morale = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) };
        var tag = new Label { Text = "" };
        stack.AddChild(text);
        stack.AddChild(hp);
        stack.AddChild(morale);
        stack.AddChild(tag);
        _slots.Add((panel, text, hp, morale, tag));
        return panel;
    }

    /// <summary>把内核投影刷新到控件（每帧/事件后由 BattleRoot 调用；无抽取）。</summary>
    public void Refresh()
    {
        if (_director is null || _projector is null)
        {
            return;
        }

        if (_lastRefreshRound != _director.Round)
        {
            _lastRefreshRound = _director.Round;
        }

        // A：回合 + 行动序列 + 撤退数字
        DecisionSupportProjection support = _projector.Support();
        _roundLabel.Text = $"回合 {support.Round}";
        _actionOrderLabel.Text = $"行动序列: {string.Join(" → ", support.ActionOrderThisRound)}";
        _retreatButton.Text = support.CanRetreat ? $"撤退 {support.RetreatRatePercent}%" : "本回合不可撤退";
        _retreatButton.Disabled = !support.CanRetreat;

        // B/C：槽位方块
        int idx = 0;
        foreach (UnitProjection u in _projector.Units(player: true))
        {
            FillSlot(_slots[idx], u, isPlayer: true);
            idx++;
        }

        foreach (UnitProjection u in _projector.Units(player: false))
        {
            FillSlot(_slots[idx], u, isPlayer: false);
            idx++;
        }

        // D：技能栏（选中单位）
        RefreshSkillBar();
    }

    private static void FillSlot((Panel panel, Label text, ProgressBar hp, ProgressBar morale, Label tag) slot, UnitProjection u, bool isPlayer)
    {
        slot.text.Text = u.UnitId == "-"
            ? $"[{u.Slot}] —"
            : $"[{u.Slot}] {u.UnitId}  HP {u.Hp}/{u.MaxHp}  士气 {u.Morale}{(u.Weak ? " 虚弱!" : "")}";
        slot.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        slot.hp.Value = u.Hp;
        slot.hp.Modulate = u.Weak ? new Color(1, 0.5f, 0.5f) : new Color(0.6f, 1, 0.6f);
        slot.morale.MaxValue = 100;
        slot.morale.Value = u.Morale;
        slot.morale.Modulate = isPlayer ? new Color(1, 1, 0.7f) : new Color(0.5f, 0.5f, 0.5f);
        slot.tag.Text = u.UnitId == "-" ? "" : (u.Weak ? "虚弱（治疗或士气回升可归队）" : (isPlayer ? "我方" : "敌方"));
    }

    private void RefreshSkillBar()
    {
        foreach (Button b in _skillButtons)
        {
            b.QueueFree();
        }

        _skillButtons.Clear();

        if (_director is null || _projector is null)
        {
            return;
        }

        UnitId selected = _selected;
        var pool = new HashSet<string>(SelectedSkills); // 战前携带 5 简化：原型全池（M6 策略同源）
        foreach (string skillId in SelectedSkills)
        {
            SkillProjection p = _projector.Skill(skillId, selected, _director.Player, _director.Enemy, pool);
            var b = new Button
            {
                Text = p.Reason == AvailabilityReason.Ok ? skillId : $"{skillId}（{p.Tooltip}）",
                Disabled = p.Reason != AvailabilityReason.Ok,
                CustomMinimumSize = new Vector2(140, 40),
            };
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(selected, captured);
            _skillBar.AddChild(b);
            _skillButtons.Add(b);
        }
    }

    private static IReadOnlyList<string> _selectedSkills = LoadSelectedSkills();

    private static IReadOnlyList<string> SelectedSkills => _selectedSkills;

    private static IReadOnlyList<string> LoadSelectedSkills()
    {
        var skills = Darkest.Data.SkillsConfig.Parse(ReadData("skills.json"));
        return skills.Skills.Where(s => s.OwnerUnit == "warrior").Select(s => s.Id).ToArray();
    }

    private static string ReadData(string name)
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "data", name);
            if (System.IO.File.Exists(candidate))
            {
                return System.IO.File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new System.IO.FileNotFoundException($"data/{name} 未找到。");
    }
}