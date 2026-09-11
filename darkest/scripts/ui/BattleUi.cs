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
/// BattleUi：暗黑地牢式排布（1280×720，中文）。单位**一字横排、两军对望**：
/// 我方 4·3·2·1（左，1 位贴近中线）｜敌方 1·2·3·4（右）；支援位 5·6 为我方后排小卡；
/// 顶部状态+回合条，底部当前行动者技能栏 + 增援/移动。
/// 高亮（修复）：① 当前行动者一律高亮（含支援位 5/6）；② 仅"需选目标"时高亮候选且**按阵营匹配**
/// （敌技亮敌卡 / 友技亮友卡；AOE·团队·自身不进入选目标 → 不会全亮）；③ 增援两步按阶段亮 5/6 → 1~4。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    private const float CardW = 146f;
    private const float CardH = 170f;
    private const float GapX = 10f;
    private const float HeroX0 = 13f;
    private const float EnemyX0 = 653f;
    private const float StageY = 96f;
    private const float SupportY = 286f;
    private const float SupportW = 130f;
    private const float SupportH = 86f;
    private const float SkillTitleY = 400f;
    private const float SkillBarY = 428f;

    private BattleRoot? _host;
    private Action<UnitId, string>? _useSkill;
    private Action? _reinforce;
    private Action? _move;
    private Action? _retreat;

    private Label _statusLabel = null!;
    private Label _actionOrderLabel = null!;
    private Button _retreatButton = null!;
    // 卡序：0..3=我方 4,3,2,1；4..7=敌方 1,2,3,4；8..9=支援位 5,6
    private readonly List<(Panel card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer)> _cards = new();
    private readonly List<Label> _portraits = new();          // 立绘占位框文字（与 _cards 同序）
    private readonly List<(Panel panel, Label glyph)> _orderIcons = new(); // 顶部回合条头像
    private string _orderFor = "";
    private readonly List<Button> _skillButtons = new();
    private Button _reinforceButton = null!;
    private Button _moveButton = null!;
    private Panel _resultPanel = null!;
    private Label _resultLabel = null!;
    private Label _skillTitle = null!;
    private Label _hintLabel = null!;
    private double _hintTimer;
    private string _skillBarFor = "";
    private bool _skillBarWaiting;
    private static readonly Dictionary<string, string> _unitNames = new();
    private static readonly Dictionary<string, string> _skillNames = new();
    private static readonly Dictionary<string, string[]> _poolCache = new();
    private static SkillsConfig? _skillsCfg;

    public void Bind(BattleRoot host, Action<UnitId, string> useSkill, Action reinforce, Action move, Action retreat)
    {
        _host = host;
        _useSkill = useSkill;
        _reinforce = reinforce;
        _move = move;
        _retreat = retreat;
        foreach (Node child in GetChildren().ToArray())
        {
            child.QueueFree();
        }

        _cards.Clear();
        _portraits.Clear();
        _orderIcons.Clear();
        _orderFor = "";
        _skillButtons.Clear();
        _skillBarFor = "";
        _skillBarWaiting = false;
        Build();
        GD.Print("[BattleUi] 暗黑地牢式排布就绪（横排：我方 4321 ｜ 敌方 1234；支援位后排；底部技能栏）。");
    }

    private static SkillsConfig SkillsCfg => _skillsCfg ??= SkillsConfig.Parse(ReadData("skills.json"));

    private void Build()
    {
        var bg = new Panel { OffsetLeft = 0, OffsetTop = 0, OffsetRight = 1280, OffsetBottom = 720 };
        bg.Modulate = new Color(0.12f, 0.12f, 0.16f, 0.97f);
        AddChild(bg);

        _statusLabel = new Label { Position = new Vector2(16, 8), CustomMinimumSize = new Vector2(620, 26) };
        _statusLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.85f));
        AddChild(_statusLabel);
        _retreatButton = new Button { Position = new Vector2(1076, 6), Size = new Vector2(184, 32), Text = "撤退 0%" };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        AddChild(_retreatButton);
        _actionOrderLabel = new Label { Position = new Vector2(16, 40), CustomMinimumSize = new Vector2(80, 24), Text = "本回合顺序" };
        _actionOrderLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_actionOrderLabel);

        AddRowTitle("我方　4 · 3 · 2 · 1", HeroX0, StageY - 22);
        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(HeroX0 + i * (CardW + GapX), StageY, CardW, CardH));
        }

        var vs = new Label { Position = new Vector2(636, StageY + 70), CustomMinimumSize = new Vector2(20, 24), Text = "VS" };
        vs.AddThemeColorOverride("font_color", new Color(1, 0.6f, 0.6f));
        AddChild(vs);

        AddRowTitle("敌方　1 · 2 · 3 · 4", EnemyX0, StageY - 22);
        for (int i = 0; i < 4; i++)
        {
            AddChild(BuildCard(EnemyX0 + i * (CardW + GapX), StageY, CardW, CardH));
        }

        AddRowTitle("支援位 5 · 6", HeroX0, SupportY - 22);
        for (int i = 0; i < 2; i++)
        {
            AddChild(BuildCard(HeroX0 + i * (SupportW + GapX), SupportY, SupportW, SupportH));
        }

        _skillTitle = new Label { Position = new Vector2(16, SkillTitleY), CustomMinimumSize = new Vector2(760, 24), Text = "技能栏（轮到行动者时可用）" };
        _skillTitle.AddThemeColorOverride("font_color", new Color(0.9f, 1, 0.9f));
        AddChild(_skillTitle);
        _hintLabel = new Label { Position = new Vector2(790, SkillTitleY), CustomMinimumSize = new Vector2(470, 24), Text = "" };
        _hintLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.5f));
        AddChild(_hintLabel);

        _reinforceButton = new Button { Position = new Vector2(1004, SkillBarY), Size = new Vector2(116, 88), Text = "增援" };
        _reinforceButton.Pressed += () => _reinforce?.Invoke();
        AddChild(_reinforceButton);
        _moveButton = new Button { Position = new Vector2(1132, SkillBarY), Size = new Vector2(116, 88), Text = "移动" };
        _moveButton.Pressed += () => _move?.Invoke();
        AddChild(_moveButton);

        _resultPanel = new Panel { Position = new Vector2(340, 210), Size = new Vector2(600, 260), Visible = false };
        _resultPanel.Modulate = new Color(0.1f, 0.1f, 0.14f, 0.98f);
        AddChild(_resultPanel);
        _resultLabel = new Label { Position = new Vector2(24, 20), CustomMinimumSize = new Vector2(552, 220) };
        _resultLabel.AddThemeFontSizeOverride("font_size", 18);
        _resultPanel.AddChild(_resultLabel);
    }

    private void AddRowTitle(string title, float x, float y)
    {
        var label = new Label { Position = new Vector2(x, y), CustomMinimumSize = new Vector2(400, 22), Text = title };
        label.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 1f));
        AddChild(label);
    }

    private Control BuildCard(float x, float y, float w, float h)
    {
        var card = new Panel { Position = new Vector2(x, y), Size = new Vector2(w, h) };

        // ② 立绘占位框（色块 + 首字）
        var portraitBox = new Panel { Position = new Vector2(8, 6), Size = new Vector2(44, 44) };
        var glyph = new Label { Position = new Vector2(0, 8), CustomMinimumSize = new Vector2(44, 30), Text = "—", HorizontalAlignment = HorizontalAlignment.Center };
        glyph.AddThemeFontSizeOverride("font_size", 20);
        glyph.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        portraitBox.AddChild(glyph);
        card.AddChild(portraitBox);

        var name = new Label { Position = new Vector2(58, 8), CustomMinimumSize = new Vector2(w - 66, 24), Text = "[-]" };
        name.AddThemeFontSizeOverride("font_size", 15);
        var stats = new Label { Position = new Vector2(58, 30), CustomMinimumSize = new Vector2(w - 66, 20), Text = "" };
        stats.AddThemeFontSizeOverride("font_size", 12);
        var hp = new ProgressBar { Position = new Vector2(8, 58), Size = new Vector2(w - 16, 14), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var morale = new ProgressBar { Position = new Vector2(8, 78), Size = new Vector2(w - 16, 12), MinValue = 0, MaxValue = 100, ShowPercentage = false };
        var tag = new Label { Position = new Vector2(8, h - 28), CustomMinimumSize = new Vector2(w - 16, 20), Text = "" };
        tag.AddThemeFontSizeOverride("font_size", 12);
        card.AddChild(name);
        card.AddChild(stats);
        card.AddChild(hp);
        card.AddChild(morale);
        card.AddChild(tag);
        _portraits.Add(glyph);

        int slot = _cards.Count < 4 ? 4 - _cards.Count
            : _cards.Count < 8 ? _cards.Count - 3
            : _cards.Count - 8 + 5;
        bool isPlayer = _cards.Count < 4 || _cards.Count >= 8;
        int slotCaptured = slot;
        bool playerCaptured = isPlayer;
        card.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                _host?.OnCardClicked(slotCaptured, playerCaptured);
            }
        };
        _cards.Add((card, name, stats, hp, morale, tag, slot, isPlayer));
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
        // 队列只保留头像方块（下方 RefreshOrderStrip）；原文字队列与方块重合已移除
        _actionOrderLabel.Text = "本回合顺序";
        _retreatButton.Text = support.CanRetreat && !_host.GameOver ? $"撤退 {support.RetreatRatePercent}%" : "本回合不可撤退";
        _retreatButton.Disabled = !support.CanRetreat || _host.GameOver;

        int activeSlot = _host.IsAwaitingPlayer ? (d.Player.UnitAtPosition(_host.ActiveActor) ?? -1) : -1;
        UnitProjection[] player = p.Units(player: true).ToArray();
        UnitProjection[] enemy = p.Units(player: false).ToArray();

        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[i], player[3 - i], _portraits[i]); // 我方 4,3,2,1
        }

        for (int i = 0; i < 4; i++)
        {
            FillCard(_cards[4 + i], enemy[i], _portraits[4 + i]); // 敌方 1,2,3,4
        }

        FillCard(_cards[8], player[4], _portraits[8]);
        FillCard(_cards[9], player[5], _portraits[9]);

        RefreshOrderStrip(support.ActionOrderThisRound, d);

        int[] pending = _host.PendingCandidates;
        bool targeting = _host.IsTargeting;
        bool targetsEnemy = _host.PendingTargetsEnemy;
        int phase = _host.ReinforcePhase;
        foreach ((Panel card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c in _cards)
        {
            bool isActive = _host.IsAwaitingPlayer && c.isPlayer && c.slot == activeSlot;
            bool hl = false;
            if (phase == 1)
            {
                hl = c.isPlayer && c.slot is 5 or 6 && d.Player.UnitRuntimeAt(c.slot) is not null;
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
                ? new Color(1.2f, 1.2f, 0.7f)
                : hl ? new Color(0.6f, 0.88f, 1.3f) : new Color(1, 1, 1);
        }

        RefreshSkillBar(d, p);

        _resultPanel.Visible = _host.GameOver;
        if (_host.GameOver)
        {
            int[] c = _host.ResultCounts;
            _resultLabel.Text =
                $"{_host.ResultText}\n\n回合数：{_host.ResultRound}\n\n系统触发计数：\n" +
                $"　士气触底 {c[0]}　虚弱 {c[1]}　死门 {c[2]}\n　撤退 {c[3]}　美德 {c[4]}　折磨 {c[5]}\n　位移 {c[6]}\n\n按 R 重开（新 seed）";
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

    private static Color ArchetypeColor(string archetype, bool isPlayer) => archetype switch
    {
        "tank" => new Color(0.42f, 0.52f, 0.62f),
        "warrior" => new Color(0.62f, 0.35f, 0.32f),
        "commissar" => new Color(0.66f, 0.58f, 0.3f),
        "medic" => new Color(0.34f, 0.55f, 0.42f),
        "melee_soldier" => new Color(0.5f, 0.28f, 0.3f),
        "ranged_archer" => new Color(0.42f, 0.44f, 0.28f),
        "caster" => new Color(0.45f, 0.32f, 0.58f),
        _ => isPlayer ? new Color(0.4f, 0.45f, 0.55f) : new Color(0.5f, 0.35f, 0.35f),
    };

    /// <summary>① 顶部回合条：头像格（首字 + 阵营色，当前行动者金框），替代纯文字。</summary>
    private void RefreshOrderStrip(IReadOnlyList<string> order, BattleDirector d)
    {
        int activePos = d.Player.UnitAtPosition(_host!.ActiveActor) ?? d.Enemy.UnitAtPosition(_host.ActiveActor) ?? 0;
        string key = string.Join(",", order) + "|" + activePos + "|" + _host.IsAwaitingPlayer;
        if (key == _orderFor)
        {
            return;
        }

        _orderFor = key;
        foreach ((Panel panel, Label glyph) icon in _orderIcons)
        {
            icon.panel.QueueFree();
        }

        _orderIcons.Clear();
        float x = 96f;
        foreach (string id in order)
        {
            var unitId = new UnitId(id);
            bool isPlayer = d.Player.UnitAtPosition(unitId) is not null;
            string archetype = _host.ArchetypeOf(unitId);
            var panel = new Panel { Position = new Vector2(x, 34), Size = new Vector2(36, 30) };
            var glyph = new Label { Position = new Vector2(0, 2), CustomMinimumSize = new Vector2(36, 26), Text = NameOf(archetype).Substring(0, 1), HorizontalAlignment = HorizontalAlignment.Center };
            glyph.AddThemeFontSizeOverride("font_size", 14);
            panel.AddChild(glyph);
            bool isActive = _host.IsAwaitingPlayer && id == _host.ActiveActor.Value;
            panel.Modulate = isActive
                ? new Color(1.25f, 1.25f, 0.7f)
                : isPlayer ? new Color(0.62f, 0.72f, 0.95f) : new Color(0.95f, 0.6f, 0.6f);
            AddChild(panel);
            _orderIcons.Add((panel, glyph));
            x += 40f;
        }
    }

    private static void FillCard((Panel card, Label name, Label stats, ProgressBar hp, ProgressBar morale, Label tag, int slot, bool isPlayer) c, UnitProjection u, Label portrait)
    {
        bool empty = u.UnitId == "-";
        string display = NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId);
        c.name.Text = empty ? $"[{u.Slot}] 空位" : $"[{u.Slot}] {display}";
        c.stats.Text = empty ? "" : $"HP {u.Hp}/{u.MaxHp}　士气 {u.Morale}";
        // ② 立绘占位框：首字 + 阵营/原型色块
        portrait.Text = empty ? "—" : display.Substring(0, 1);
        if (portrait.GetParent() is Panel box)
        {
            box.Modulate = empty ? new Color(0.35f, 0.35f, 0.35f) : ArchetypeColor(u.Archetype.Length > 0 ? u.Archetype : u.UnitId, c.isPlayer);
        }

        c.hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        c.hp.Value = u.Hp;
        c.hp.Modulate = u.Weak ? new Color(1, 0.5f, 0.5f) : new Color(0.5f, 1, 0.6f);
        c.morale.MaxValue = 100;
        c.morale.Value = u.Morale;
        c.morale.Modulate = c.isPlayer ? new Color(1, 0.92f, 0.5f) : new Color(0.55f, 0.55f, 0.55f);
        c.tag.Text = empty ? "" : (u.Weak ? "虚弱" : (c.isPlayer ? "我方" : "敌方"));
    }

    private void RefreshSkillBar(BattleDirector d, BattleProjector p)
    {
        bool waiting = _host!.IsAwaitingPlayer;
        UnitId actor = _host.ActiveActor;

        bool combatActor = waiting && (d.Player.UnitAtPosition(actor) ?? -1) is >= 1 and <= 4;
        _reinforceButton.Disabled = !waiting || d.SwappedThisRound;
        _moveButton.Disabled = !combatActor || d.SwappedThisRound || MoveCandidates(actor, d).Length == 0;

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
        const int perRow = 8; // ③ 技能栏图标格（首 2 字为图标，悬停看全名/原因）
        for (int i = 0; i < poolIds.Length; i++)
        {
            string skillId = poolIds[i];
            SkillProjection sp = p.Skill(skillId, actor, d.Player, d.Enemy, pool);
            string full = SkillName(skillId);
            var b = new Button
            {
                Position = new Vector2(24f + (i % perRow) * 94f, SkillBarY + (i / perRow) * 94f),
                Size = new Vector2(88, 88),
                Text = full.Length <= 2 ? full : full.Substring(0, 2),
                Disabled = sp.Reason != AvailabilityReason.Ok,
                TooltipText = sp.Reason == AvailabilityReason.Ok ? full : $"{full}（{sp.Tooltip}）",
            };
            b.AddThemeFontSizeOverride("font_size", 20);
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

    private int[] MoveCandidates(UnitId actor, BattleDirector d)
    {
        if (d.Player.UnitAtPosition(actor) is not (>= 1 and <= 4))
        {
            return Array.Empty<int>();
        }

        return SkillTargetResolver.Resolve(SkillsCfg.Get("move"), actor, d.Player, d.Enemy).ToArray(); // F1：通用 move
    }

    private static string[] SkillPool(string archetype)
    {
        if (_poolCache.TryGetValue(archetype, out string[]? cached))
        {
            return cached;
        }

        string[] pool = SkillsCfg.Skills
            .Where(s => s.OwnerUnit == archetype && !s.PoolExternal) // F1/P12：按 pool_external 标志过滤（非 id 后缀）
            .Select(s => s.Id).ToArray();
        _poolCache[archetype] = pool;
        return pool;
    }

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

        foreach (SkillTemplateConfig s in SkillsCfg.Skills)
        {
            _skillNames[s.Id] = s.Name;
        }
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