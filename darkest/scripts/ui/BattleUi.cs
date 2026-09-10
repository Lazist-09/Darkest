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
/// 布局 = 手动绝对定位（CanvasLayer 直挂 Control 用视口坐标系，避免 container 尺寸漂移）：
///   A 顶栏（y≈8）：状态文本（回合/轮到谁/结束）· 行动序列 · 撤退按钮
///   B 战场（y≈48 起）：我方六卡左列（x≈16）· 敌方四卡右列（x≈336）；卡 = 单位名/HP 文本 + HP 条 + 士气条 + 状态标
///   D 技能栏（y≈632）：当前行动者技能按钮（随行动者重建，平时只更新禁用态）+ 换位←5/←6
/// 刷新为增量：槽位卡与顶栏每帧更新文本；技能栏仅当行动者/等待态变化时重建一次。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    private const float CardW = 300f;
    private const float CardH = 86f;
    private const float CardGap = 8f;
    private const float LeftX = 20f;
    private const float RightX = 350f;
    private const float TopY = 46f;
    private const float SkillBarY = 636f;

    private BattleRoot? _host;
    private Action<UnitId, string>? _useSkill;
    private Action<UnitId, int>? _swap;
    private Action? _retreat;

    private Label _statusLabel = null!;
    private Label _actionOrderLabel = null!;
    private Button _retreatButton = null!;
    private readonly List<(Panel card, Label text, ProgressBar hp, ProgressBar morale, Label tag)> _cards = new();
    private readonly List<Button> _skillButtons = new();
    private Button _swap5 = null!;
    private Button _swap6 = null!;
    private Label _skillTitle = null!;
    private string _skillBarFor = "";   // 技能栏当前渲染的行动者
    private bool _skillBarWaiting;
    private static readonly Dictionary<string, string[]> _poolCache = new();

    public void Bind(BattleRoot host, Action<UnitId, string> useSkill, Action<UnitId, int> swap, Action retreat)
    {
        _host = host;
        _useSkill = useSkill;
        _swap = swap;
        _retreat = retreat;
        // 幂等：重开（R）时清掉旧控件再重建，避免叠加残留
        foreach (Node child in GetChildren().ToArray())
        {
            child.QueueFree();
        }

        _cards.Clear();
        _skillButtons.Clear();
        _skillBarFor = "";
        _skillBarWaiting = false;
        Build();
        GD.Print("[BattleUi] 四分区控件就绪（手动布局，增量刷新）。");
    }

    private void Build()
    {
        // 背景（承接整体排版感，MouseFilter=Stop 不影响子控件事件）
        var bg = new Panel { OffsetLeft = 0, OffsetTop = 0, OffsetRight = 1280, OffsetBottom = 720 };
        bg.Modulate = new Color(0.16f, 0.16f, 0.2f, 0.92f);
        AddChild(bg);

        // A 顶栏
        _statusLabel = new Label { Position = new Vector2(16, 10), CustomMinimumSize = new Vector2(320, 28) };
        _statusLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.85f));
        AddChild(_statusLabel);
        _actionOrderLabel = new Label { Position = new Vector2(360, 10), CustomMinimumSize = new Vector2(560, 28) };
        AddChild(_actionOrderLabel);
        _retreatButton = new Button { Position = new Vector2(1080, 6), Size = new Vector2(180, 32), Text = "撤退 0%" };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        AddChild(_retreatButton);

        // B 战场卡片（我方 6 + 敌方 4，按索引排列）
        for (int i = 0; i < 6; i++)
        {
            AddChild(BuildCard(LeftX, TopY + i * (CardH + CardGap)));
        }

        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(RightX, TopY + i * (CardH + CardGap)));
        }

        // D 技能栏
        _skillTitle = new Label { Position = new Vector2(16, SkillBarY - 26), CustomMinimumSize = new Vector2(320, 24), Text = "技能（轮到行动者时可用）" };
        AddChild(_skillTitle);
        _swap5 = new Button { Position = new Vector2(1000, SkillBarY), Size = new Vector2(120, 44), Text = "换位←5" };
        _swap5.Pressed += () => _swap?.Invoke(_host!.ActiveActor, 5);
        AddChild(_swap5);
        _swap6 = new Button { Position = new Vector2(1130, SkillBarY), Size = new Vector2(120, 44), Text = "换位←6" };
        _swap6.Pressed += () => _swap?.Invoke(_host!.ActiveActor, 6);
        AddChild(_swap6);
    }

    private Control BuildCard(float x, float y)
    {
        var card = new Panel { Position = new Vector2(x, y), Size = new Vector2(CardW, CardH) };
        var text = new Label { Position = new Vector2(10, 6), CustomMinimumSize = new Vector2(CardW - 20, 24) };
        var hp = new ProgressBar { Position = new Vector2(10, 34), Size = new Vector2(CardW - 20, 14), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var morale = new ProgressBar { Position = new Vector2(10, 52), Size = new Vector2(CardW - 20, 14), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var tag = new Label { Position = new Vector2(10, 70), CustomMinimumSize = new Vector2(CardW - 20, 14) };
        card.AddChild(text);
        card.AddChild(hp);
        card.AddChild(morale);
        card.AddChild(tag);
        _cards.Add((card, text, hp, morale, tag));
        return card;
    }

    /// <summary>增量刷新（每帧）：文本/条只更新内容；技能栏仅行动者变化时重建。无抽取、零写。</summary>
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
            ? $"战斗结束（第 {support.Round} 回合）：{status}"
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
            FillCard(_cards[idx], u, isPlayer: true, u.Slot == activeSlot);
            idx++;
        }

        foreach (UnitProjection u in p.Units(player: false))
        {
            FillCard(_cards[idx], u, isPlayer: false, false);
            idx++;
        }

        RefreshSkillBar(d, p);
    }

    private static void FillCard((Panel card, Label text, ProgressBar hp, ProgressBar morale, Label tag) c, UnitProjection u, bool isPlayer, bool active)
    {
        c.text.Text = u.UnitId == "-" ? $"[{u.Slot}] 空位" : $"[{u.Slot}] {u.UnitId}  HP {u.Hp}/{u.MaxHp}  士气 {u.Morale}";
        c.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        c.hp.Value = u.Hp;
        c.hp.Modulate = u.Weak ? new Color(1, 0.55f, 0.55f) : new Color(0.55f, 1, 0.6f);
        c.morale.MaxValue = 100;
        c.morale.Value = u.Morale;
        c.morale.Modulate = isPlayer ? new Color(1, 0.95f, 0.55f) : new Color(0.55f, 0.55f, 0.55f);
        c.tag.Text = u.UnitId == "-" ? "" : (u.Weak ? "虚弱（换位/治疗/回升可救）" : (isPlayer ? "我方" : "敌方"));
        c.card.Modulate = active ? new Color(1.1f, 1.1f, 0.75f) : new Color(1, 1, 1);
    }

    private void RefreshSkillBar(BattleDirector d, BattleProjector p)
    {
        bool waiting = _host!.IsAwaitingPlayer;
        UnitId actor = _host.ActiveActor;

        // 换位按钮：仅等待玩家且本回合未换且支援位占用时可用（每帧更新，不重建）
        _swap5.Disabled = !waiting || d.SwappedThisRound || d.Player.UnitRuntimeAt(5) is null;
        _swap6.Disabled = !waiting || d.SwappedThisRound || d.Player.UnitRuntimeAt(6) is null;

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

        _skillTitle.Text = $"{actor} 的行动 — 选择技能或换位";
        // 行动者变化才重建一次（修复"每帧重建导致点击无反应"）
        if (_skillBarFor == actor.ToString() && _skillBarWaiting)
        {
            return;
        }

        _skillBarFor = actor.ToString();
        _skillBarWaiting = true;
        ClearSkillButtons();
        var pool = new HashSet<string>(SkillPool(actor.ToString()));
        float x = 20f;
        foreach (string skillId in SkillPool(actor.ToString()))
        {
            SkillProjection sp = p.Skill(skillId, actor, d.Player, d.Enemy, pool);
            var b = new Button
            {
                Position = new Vector2(x, SkillBarY),
                Size = new Vector2(150, 44),
                Text = sp.Reason == AvailabilityReason.Ok ? skillId : $"{skillId}（{sp.Tooltip}）",
                Disabled = sp.Reason != AvailabilityReason.Ok,
            };
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            AddChild(b);
            _skillButtons.Add(b);
            x += 158f;
        }
    }

    private void ClearSkillButtons()
    {
        foreach (Button b in _skillButtons)
        {
            b.QueueFree();
        }

        _skillButtons.Clear();
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