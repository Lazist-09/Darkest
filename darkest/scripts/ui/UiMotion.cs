using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 `ui_spec §12.1` **动效四个** —— 参数来自策划 `#321`⑤（**可测常量，一处定义**，不在调用点散写）：
/// ```
/// ① 出现（伤害数字／事件条目）：**上浮 8px + 淡出 0.30s**
/// ② 受击（单位）           ：**抖动 ±4px · 0.15s · 2 次往返** + **闪白 #FFFFFF@60%**
/// ③ 士气崩溃（进入折磨／死门）：**暗角 40%** + **单位框红 #C0202A** · **0.50s**
/// ④ 结算（面板出现）        ：**淡入 0.20s**
/// ```
/// 🔴 **两条可测约束**（`#321`⑤ 写死）：
/// ① **动效期间【输入不得被吞】** —— 实现纪律：动效层与瞬态元素一律 `MouseFilter = Ignore`（不拦鼠标），
///    且**绝不改 `ProcessMode`**（不冻结逻辑）；本类只写 `Modulate`/`Position`，**不碰任何玩法状态** ✓
/// ② **动效不得延迟【可操作时刻】** —— 所有时长 ≤0.5s 且**不阻塞任何状态机**（没有 `await`/回调门控）✓
/// 🔴 **明确不做**（`§12.1`）：常驻待机／转场过场／技能特效（那属美术）✓
/// </summary>
public static class UiMotion
{
    // ---- `#321`⑤ 常量（动效参数的唯一出处）----
    public const float AppearSeconds = 0.30f;
    public const float AppearRisePx = 8f;
    public const float HitSeconds = 0.15f;
    public const float HitShakePx = 4f;
    public const int HitShakeTrips = 2;
    public static readonly Color HitFlash = new(1f, 1f, 1f, 0.60f);      // #FFFFFF @ 60%
    public const float MoraleSeconds = 0.50f;
    public const float VignetteAlpha = 0.40f;                            // 暗角 40%
    public static readonly Color MoraleFrame = new(0.75f, 0.13f, 0.16f); // #C0202A
    public const float SettleSeconds = 0.20f;

    private static int _played;
    private static int _active;

    /// <summary>已播动效次数（取证）。</summary>
    public static int Played => _played;

    /// <summary>正在运行的动效数（取证；归零 = 全部结束）。</summary>
    public static int Active => _active;

    /// <summary>🔴 **可断言的取证行**（冒烟打印）：播了几次 ／ 还在跑几次 ／ 输入为什么不会被吞。</summary>
    public static string Audit()
        => $"动效（§12.1）：已播 {_played} 次 ／ 运行中 {_active} ／ 动效层 MouseFilter=Ignore 且不改 ProcessMode ⇒ 输入不被吞 ✓";

    /// <summary>建动效层（**满屏 + 鼠标穿透**；伤害数字 / 暗角都画在它上面）。挂到满屏 `Control` 根后要 `FullRect`。</summary>
    public static Control MakeLayer(string name)
        => new() { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>① 出现：一段文字**上浮 + 淡出**（伤害数字 / 事件条目）。</summary>
    public static void FloatText(Control layer, Vector2 localPos, string text, Color color)
    {
        if (layer is null)
        {
            return;
        }

        var label = new Label
        {
            Name = "FloatText",
            Text = text,
            Position = localPos,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", color);
        layer.AddChild(label);

        _played++;
        _active++;
        Tween t = label.CreateTween();
        t.TweenProperty(label, "position:y", localPos.Y - AppearRisePx, AppearSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        t.Parallel().TweenProperty(label, "modulate:a", 0.0f, AppearSeconds * 0.6f).SetDelay(AppearSeconds * 0.4f);
        t.Chain().TweenCallback(Callable.From(() =>
        {
            label.QueueFree();
            _active--;
        }));
    }

    /// <summary>② 受击：**抖动 ±4px · 0.15s · 2 次往返** + **闪白**。</summary>
    public static void Hit(Control card)
    {
        if (card is null)
        {
            return;
        }

        Vector2 basePos = card.Position;
        float step = HitSeconds / (HitShakeTrips * 2f);
        _played++;
        _active++;

        Tween t = card.CreateTween();
        for (int i = 0; i < HitShakeTrips; i++)
        {
            t.TweenProperty(card, "position:x", basePos.X + HitShakePx, step);
            t.TweenProperty(card, "position:x", basePos.X - HitShakePx, step);
        }

        t.TweenProperty(card, "position:x", basePos.X, step * 0.5f);
        t.TweenCallback(Callable.From(() =>
        {
            card.Position = basePos; // 收尾归位（防止容器重排后残留偏移）
            _active--;
        }));

        Flash(card, HitFlash, HitSeconds);
    }

    /// <summary>③ 士气崩溃：**暗角 40%** + **单位框红 #C0202A**，0.50s。</summary>
    public static void MoraleCrash(TextureRect? vignette, Control? card)
    {
        if (vignette is not null)
        {
            _played++;
            _active++;
            vignette.Show();
            Tween v = vignette.CreateTween();
            v.TweenProperty(vignette, "modulate:a", VignetteAlpha, MoraleSeconds * 0.3f);
            v.TweenProperty(vignette, "modulate:a", 0.0f, MoraleSeconds * 0.7f);
            v.TweenCallback(Callable.From(() =>
            {
                vignette.Hide();
                _active--;
            }));
        }

        if (card is not null)
        {
            Tween c = card.CreateTween();
            c.TweenProperty(card, "modulate", MoraleFrame, MoraleSeconds * 0.3f);
            c.TweenProperty(card, "modulate", Colors.White, MoraleSeconds * 0.7f);
        }
    }

    /// <summary>④ 结算：面板**淡入 0.20s**（只改透明度 —— 面板的可见性与可操作性**不受动效门控**）✓</summary>
    public static void Settle(Control panel)
    {
        if (panel is null)
        {
            return;
        }

        _played++;
        panel.Modulate = new Color(1, 1, 1, 0);
        Tween t = panel.CreateTween();
        t.TweenProperty(panel, "modulate:a", 1.0f, SettleSeconds);
    }

    /// <summary>暗角贴图：**径向渐变**（中心透明 → 边缘黑）；`§12.3` 之后可换成 `ShaderMaterial`。</summary>
    public static GradientTexture2D MakeVignetteTexture()
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(0, 0, 0, 0));
        grad.SetColor(1, new Color(0, 0, 0, 1));
        return new GradientTexture2D
        {
            Gradient = grad,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1.0f, 0.5f),
            Width = 256,
            Height = 256,
        };
    }

    /// <summary>卡内叠一层纯色（闪白）。**不是 `Label`** ⇒ 不参与"文字重叠"判据，也不拦鼠标 ✓</summary>
    private static void Flash(Control card, Color color, float seconds)
    {
        var rect = new ColorRect { Name = "HitFlash", Color = color, MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddChild(rect);
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Tween t = rect.CreateTween();
        t.TweenProperty(rect, "modulate:a", 0.0f, seconds);
        t.TweenCallback(Callable.From(rect.QueueFree));
    }
}
