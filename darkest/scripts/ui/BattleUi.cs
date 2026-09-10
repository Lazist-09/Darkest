using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;

/// <summary>
/// BattleUi：四分区控件层（blueprint §5d / ui_spec §1）。
/// A 顶栏（状态文本=回合/轮到谁/结束横幅 + 行动序列 + 撤退按钮）、
/// B 战场（槽位方块：编号/单位/HP 条/士气条/虚弱标/当前行动者高亮）、
/// D 技能栏（当前行动者技能按钮 + 换位入口；不可用灰显 tooltip 原因）。
/// 只读投影 + 命令门面；状态真值在内核与 BattleRoot 游玩状态机。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    private BattleRoot? _host;
    private Action<UnitId, string>? _useSkill;
    private Action<UnitId, int>? _swap;
    private Action? _retreat;

    private Label _statusLabel = null!;
    private Label _actionOrderLabel = null!;
    private Button _retreatButton = null!;
    private readonly List<(Panel panel, Label text, ProgressBar hp, ProgressBar morale, Label tag)> _slots = new();
    private readonly List<Button> _skillButtons = new();
    private HBoxContainer _skillBar = null!;
    private Button _swap5 = null!;
    private Button _swap6 = null!;
    private static readonly Dictionary<string, string[]> _poolCache = new();

    public void Bind(BattleRoot host, Action<UnitId, string> useSkill, Action<UnitId, int> swap, Action retreat)
    {
        _host = host;
        _useSkill = useSkill;
        _swap = swap;
        _retreat = retreat;
        Build();
        GD.Print("[BattleUi] 四分区控件就绪（槽位/行动序列/技能栏/换位）。");
    }

    private void Build()
    {
        var root = new VBoxContainer { AnchorsPreset = 15 };
        AddChild(root);

        var top = new HBoxContainer();
        root.AddChild(top);
        _statusLabel = new Label { Text = "战斗就绪", CustomMinimumSize = new Vector2(300, 0) };
        top.AddChild(_statusLabel);
        _actionOrderLabel = new Label { Text = "行动序列: ", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(_actionOrderLabel);
        _retreatButton = new Button { Text = "撤退 0%", CustomMinimumSize = new Vector2(140, 0) };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        top.AddChild(_retreatButton);

        var mid = new HBoxContainer();
        root.AddChild(mid);
        var left = new VBoxContainer();
        mid.AddChild(left);
        for (int i = 0; i < 6; i++)
        {
            left.AddChild(BuildSlotPanel());
        }

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        mid.AddChild(right);
        for (int i = 0; i < 4; i++)
        {
            right.AddChild(BuildSlotPanel());
        }

        // D 技能栏：当前行动者技能 + 换位入口
        var bottom = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        root.AddChild(bottom);
        _skillBar = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        bottom.AddChild(_skillBar);
        _swap5 = new Button { Text = "换位←5", CustomMinimumSize = new Vector2(90, 40) };
        _swap5.Pressed += () => _swap?.Invoke(_host!.ActiveActor, 5);
        bottom.AddChild(_swap5);
        _swap6 = new Button { Text = "换位←6", CustomMinimumSize = new Vector2(90, 40) };
        _swap6.Pressed += () => _swap?.Invoke(_host!.ActiveActor, 6);
        bottom.AddChild(_swap6);
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

    /// <summary>把内核投影 + 游玩状态刷新到控件（每帧；无抽取、零写）。</summary>
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
            ? $"战斗结束（第 {support.Round} 回合）：{(support.CanRetreat ? "" : "")}{status}"
            : status.Length > 0 ? status : $"回合 {support.Round}";
        _actionOrderLabel.Text = $"行动序列: {string.Join(" → ", support.ActionOrderThisRound)}";
        _retreatButton.Text = support.CanRetreat && !_host.GameOver ? $"撤退 {support.RetreatRatePercent}%" : "本回合不可撤退";
        _retreatButton.Disabled = !support.CanRetreat || _host.GameOver;

        int activeSlot = -1;
        if (_host.IsAwaitingPlayer)
        {
            activeSlot = d.Player.UnitAtPosition(_host.ActiveActor) ?? -1;
        }

        int idx = 0;
        foreach (UnitProjection u in p.Units(player: true))
        {
            FillSlot(_slots[idx], u, isPlayer: true, u.Slot == activeSlot);
            idx++;
        }

        foreach (UnitProjection u in p.Units(player: false))
        {
            FillSlot(_slots[idx], u, isPlayer: false, false);
            idx++;
        }

        RefreshSkillBar(d, p);
    }

    private static void FillSlot((Panel panel, Label text, ProgressBar hp, ProgressBar morale, Label tag) slot, UnitProjection u, bool isPlayer, bool active)
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
        slot.tag.Text = u.UnitId == "-" ? "" : (u.Weak ? "虚弱（治疗/换位可救）" : (isPlayer ? "我方" : "敌方"));
        slot.panel.Modulate = active ? new Color(1.05f, 1.05f, 0.7f) : new Color(1, 1, 1);
    }

    private void RefreshSkillBar(BattleDirector d, BattleProjector p)
    {
        foreach (Button b in _skillButtons)
        {
            b.QueueFree();
        }

        _skillButtons.Clear();

        bool playerTurn = _host!.IsAwaitingPlayer;
        UnitId actor = _host.ActiveActor;

        // 换位入口：仅等待玩家时启用（战斗位发起、支援位占用、本回合未换）
        _swap5.Disabled = !playerTurn || d.SwappedThisRound || d.Player.UnitRuntimeAt(5) is null;
        _swap6.Disabled = !playerTurn || d.SwappedThisRound || d.Player.UnitRuntimeAt(6) is null;

        if (!playerTurn)
        {
            return;
        }

        var pool = new HashSet<string>(SkillPool(actor.ToString()));
        foreach (string skillId in SkillPool(actor.ToString()))
        {
            SkillProjection sp = p.Skill(skillId, actor, d.Player, d.Enemy, pool);
            var b = new Button
            {
                Text = sp.Reason == AvailabilityReason.Ok ? skillId : $"{skillId}（{sp.Tooltip}）",
                Disabled = sp.Reason != AvailabilityReason.Ok,
                CustomMinimumSize = new Vector2(150, 40),
            };
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            _skillBar.AddChild(b);
            _skillButtons.Add(b);
        }
    }

    /// <summary>原型技能池（静态缓存；实机携带 5 由后续 BattleSetup 替换，当前全池）。</summary>
    private static string[] SkillPool(string owner)
    {
        if (_poolCache.TryGetValue(owner, out string[]? cached))
        {
            return cached;
        }

        SkillsConfig skills = SkillsConfig.Parse(ReadData("skills.json"));
        string[] pool = skills.Skills.Where(s => s.OwnerUnit == owner).Select(s => s.Id).ToArray();
        _poolCache[owner] = pool;
        return pool;
    }

    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }
}