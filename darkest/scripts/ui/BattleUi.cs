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
/// BattleUi：战斗界面（1280×720，canvas_items 伸展；文字中文）。
/// 对峙式横向布局（用户方案）：
///   A 顶栏（y≈8 状态 · y≈44 行动序列整行 · 右上撤退按钮）
///   敌方行：1 2 3 4（左→右，y≈96）
///   我方行：4 3 2 1（左→右，y≈236）——两军前排相对，视觉对峙
///   支援位：5 6 小卡（y≈376）
///   D 技能栏（y≈500 起）：当前轮次角色的技能横排 + 换位←5/←6
/// 增量刷新（技能栏仅行动者变化时重建）；每帧只更新文案/条/禁用态。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    private const float CardW = 236f;
    private const float CardH = 96f;
    private const float CardGap = 14f;
    private const float RowLeft = 24f;
    private const float EnemyRowY = 96f;
    private const float PlayerRowY = 240f;
    private const float SupportRowY = 384f;
    private const float MinorW = 180f;
    private const float MinorH = 72f;
    private const float SkillTitleY = 500f;
    private const float SkillBarY = 530f;

    private BattleRoot? _host;
    private Action<UnitId, string>? _useSkill;
    private Action<UnitId, int>? _swap;
    private Action? _retreat;

    private Label _statusLabel = null!;
    private Label _actionOrderLabel = null!;
    private Button _retreatButton = null!;
    // 卡片槽位顺序：0..3=敌方1..4，4..7=我方4,3,2,1（我方读投影按槽1→4再逆序放置），8,9=支援位5,6
    private readonly List<(Panel card, Label text, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer)> _cards = new();
    private readonly List<Button> _skillButtons = new();
    private Button _swap5 = null!;
    private Button _swap6 = null!;
    private Label _skillTitle = null!;
    private Label _hintLabel = null!;
    private double _hintTimer;
    private string _skillBarFor = "";
    private bool _skillBarWaiting;
    private static readonly Dictionary<string, string> _unitNames = new();
    private static readonly Dictionary<string, string> _skillNames = new();

    public void Bind(BattleRoot host, Action<UnitId, string> useSkill, Action<UnitId, int> swap, Action retreat)
    {
        _host = host;
        _useSkill = useSkill;
        _swap = swap;
        _retreat = retreat;
        foreach (Node child in GetChildren().ToArray())
        {
            child.QueueFree();
        }

        _cards.Clear();
        _skillButtons.Clear();
        _skillBarFor = "";
        _skillBarWaiting = false;
        Build();
        GD.Print("[BattleUi] 对峙布局就绪（敌方 1234 / 我方 4321，中文）。");
    }

    private void Build()
    {
        var bg = new Panel { OffsetLeft = 0, OffsetTop = 0, OffsetRight = 1280, OffsetBottom = 720 };
        bg.Modulate = new Color(0.15f, 0.15f, 0.19f, 0.95f);
        AddChild(bg);

        _statusLabel = new Label { Position = new Vector2(16, 10), CustomMinimumSize = new Vector2(420, 26) };
        _statusLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.85f));
        AddChild(_statusLabel);
        _retreatButton = new Button { Position = new Vector2(1076, 6), Size = new Vector2(184, 34), Text = "撤退 0%" };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        AddChild(_retreatButton);
        _actionOrderLabel = new Label { Position = new Vector2(16, 44), CustomMinimumSize = new Vector2(1248, 22) };
        _actionOrderLabel.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_actionOrderLabel);

        AddRowTitle("敌方（1 2 3 4）", RowLeft, EnemyRowY - 26);
        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(RowLeft + i * (CardW + CardGap), EnemyRowY));
        }

        AddRowTitle("我方（4 3 2 1）", RowLeft, PlayerRowY - 26);
        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(RowLeft + i * (CardW + CardGap), PlayerRowY));
        }

        AddRowTitle("支援位", RowLeft, SupportRowY - 26);
        for (int i = 0; i < 2; i++)
        {
            AddChild(BuildCard(RowLeft + i * (MinorW + CardGap), SupportRowY, MinorW, MinorH));
        }

        _skillTitle = new Label { Position = new Vector2(16, SkillTitleY), CustomMinimumSize = new Vector2(600, 26), Text = "技能栏（轮到行动者时可用）" };
        _skillTitle.AddThemeColorOverride("font_color", new Color(0.9f, 1, 0.9f));
        AddChild(_skillTitle);
        _hintLabel = new Label { Position = new Vector2(16, SkillTitleY + 26), CustomMinimumSize = new Vector2(900, 24), Text = "" };
        _hintLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.5f));
        AddChild(_hintLabel);
        _swap5 = new Button { Position = new Vector2(1010, SkillBarY), Size = new Vector2(118, 44), Text = "换位 ←5" };
        _swap5.Pressed += () => _swap?.Invoke(_host!.ActiveActor, 5);
        AddChild(_swap5);
        _swap6 = new Button { Position = new Vector2(1138, SkillBarY), Size = new Vector2(118, 44), Text = "换位 ←6" };
        _swap6.Pressed += () => _swap?.Invoke(_host!.ActiveActor, 6);
        AddChild(_swap6);
    }

    private void AddRowTitle(string title, float x, float y)
    {
        var label = new Label { Position = new Vector2(x, y), CustomMinimumSize = new Vector2(300, 24), Text = title };
        label.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 1f));
        AddChild(label);
    }

    private Control BuildCard(float x, float y, float w = CardW, float h = CardH)
    {
        var card = new Panel { Position = new Vector2(x, y), Size = new Vector2(w, h) };
        var text = new Label { Position = new Vector2(10, 6), CustomMinimumSize = new Vector2(w - 20, 26) };
        text.AddThemeFontSizeOverride("font_size", 15);
        var hp = new ProgressBar { Position = new Vector2(10, 36), Size = new Vector2(w - 20, 14), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var morale = new ProgressBar { Position = new Vector2(10, 54), Size = new Vector2(w - 20, 14), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var tag = new Label { Position = new Vector2(10, 72), CustomMinimumSize = new Vector2(w - 20, 18) };
        tag.AddThemeFontSizeOverride("font_size", 12);
        card.AddChild(text);
        card.AddChild(hp);
        card.AddChild(morale);
        card.AddChild(tag);
        // 记录卡片身份（槽位/阵营），供单体系目标选择点击（#178/#179）
        int slot = _cards.Count < 4 ? _cards.Count + 1
            : _cards.Count < 8 ? 4 - (_cards.Count - 4)
            : _cards.Count - 8 + 5;
        bool isPlayer = _cards.Count >= 4;
        int slotCaptured = slot;
        bool playerCaptured = isPlayer;
        card.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                _host?.OnCardClicked(slotCaptured, playerCaptured);
            }
        };
        _cards.Add((card, text, hp, morale, tag, slot, isPlayer));
        return card;
    }

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
        _actionOrderLabel.Text = "行动序列: " + string.Join(" → ", support.ActionOrderThisRound.Select(id => NameOf(_host.ArchetypeOf(new UnitId(id)))));
        _retreatButton.Text = support.CanRetreat && !_host.GameOver ? $"撤退 {support.RetreatRatePercent}%" : "本回合不可撤退";
        _retreatButton.Disabled = !support.CanRetreat || _host.GameOver;

        int activeSlot = _host.IsAwaitingPlayer ? (d.Player.UnitAtPosition(_host.ActiveActor) ?? -1) : -1;

        // 敌方 1..4 → 卡 0..3
        UnitProjection[] enemy = p.Units(player: false).ToArray();
        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[i], enemy[i], isPlayer: false, false);
        }

        // 我方 4 3 2 1 → 卡 4..7（投影按槽 1..6 升序；战斗位取前 4 逆序摆放）
        UnitProjection[] playerUnits = p.Units(player: true).ToArray();
        for (int i = 0; i < 4; i++)
        {
            UnitProjection u = playerUnits[i]; // 槽 1..4
            FillCard(_cards[4 + (3 - i)], u, isPlayer: true, u.Slot == activeSlot);
        }

        // 支援位 5、6 → 卡 8、9
        FillCard(_cards[8], playerUnits[4], isPlayer: true, false);
        FillCard(_cards[9], playerUnits[5], isPlayer: true, false);

        // 单体/any_ally 选一（#178/#179）：候选目标卡高亮蓝色并接收点击
        int[] pending = _host.PendingCandidates;
        foreach ((Panel card, Label text, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c in _cards)
        {
            c.card.MouseFilter = Control.MouseFilterEnum.Stop; // 卡片收点击（目标选择/无操作）
            if (_host.IsTargeting && System.Array.IndexOf(pending, c.slot) >= 0)
            {
                c.card.Modulate = new Color(0.7f, 0.85f, 1.2f);
            }
        }

        RefreshSkillBar(d, p);

        // 提示语（2 秒后自动清）
        if (_hintTimer > 0)
        {
            _hintTimer -= 1.0 / 60.0;
            if (_hintTimer <= 0)
            {
                _hintLabel.Text = "";
            }
        }
    }

    /// <summary>临时提示（换位被拒等），显示约 2 秒。</summary>
    public void FlashHint(string text)
    {
        _hintLabel.Text = text;
        _hintTimer = 2.0;
    }

    private static void FillCard((Panel card, Label text, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c, UnitProjection u, bool isPlayer, bool active)
    {
        string title = u.UnitId == "-" ? $"[{u.Slot}] 空位" : $"[{u.Slot}] {NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId)}  HP {u.Hp}/{u.MaxHp}  士气 {u.Morale}";
        c.text.Text = title;
        c.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        c.hp.Value = u.Hp;
        c.hp.Modulate = u.Weak ? new Color(1, 0.55f, 0.55f) : new Color(0.5f, 1, 0.6f);
        c.morale.MaxValue = 100;
        c.morale.Value = u.Morale;
        c.morale.Modulate = isPlayer ? new Color(1, 0.95f, 0.55f) : new Color(0.55f, 0.55f, 0.55f);
        c.tag.Text = u.UnitId == "-" ? "" : (u.Weak ? "虚弱（换位/治疗/回升可救）" : (isPlayer ? "我方" : "敌方"));
        c.card.Modulate = active ? new Color(1.1f, 1.1f, 0.72f) : new Color(1, 1, 1);
    }

    private void RefreshSkillBar(BattleDirector d, BattleProjector p)
    {
        bool waiting = _host!.IsAwaitingPlayer;
        UnitId actor = _host.ActiveActor;

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

        _skillTitle.Text = $"轮到 {NameOf(_host.ActiveArchetype)} — 选择技能或换位";
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
        int perRow = 6; // (990−24)/158
        float startX = 24f;
        for (int i = 0; i < poolIds.Length; i++)
        {
            string skillId = poolIds[i];
            SkillProjection sp = p.Skill(skillId, actor, d.Player, d.Enemy, pool);
            string name = SkillName(skillId);
            var b = new Button
            {
                Position = new Vector2(startX + (i % perRow) * 158f, SkillBarY + (i / perRow) * 52f),
                Size = new Vector2(150, 44),
                Text = sp.Reason == AvailabilityReason.Ok ? name : $"{name}（{sp.Tooltip}）",
                Disabled = sp.Reason != AvailabilityReason.Ok,
            };
            string captured = skillId;
            b.Pressed += () => _useSkill?.Invoke(actor, captured);
            AddChild(b);
            _skillButtons.Add(b);
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

    // ---------- 中文名映射（数据驱动，静态缓存） ----------

    private static string NameOf(string unitId)
    {
        if (_unitNames.Count == 0)
        {
            LoadNames();
        }

        return _unitNames.TryGetValue(unitId, out string? n) ? n : unitId;
    }

    private static string SkillName(string skillId)
    {
        if (_skillNames.Count == 0)
        {
            LoadNames();
        }

        return _skillNames.TryGetValue(skillId, out string? n) ? n : skillId;
    }

    private static void LoadNames()
    {
        foreach (UnitConfig u in UnitsConfig.Parse(ReadData("units.json")).Units)
        {
            _unitNames[u.Id] = u.Name;
        }

        foreach (SkillTemplateConfig s in SkillsConfig.Parse(ReadData("skills.json")).Skills)
        {
            _skillNames[s.Id] = s.Name;
        }
    }

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

    private static readonly Dictionary<string, string[]> _poolCache = new();

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