using System.Globalization;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M7.5 **光照条**（`ui_spec` 必显 **11 光照条 / 火把条**）。
///
/// 🔴 规格硬要求（`#262` 策划原话）：**必须同时显示「当前值 + 档位 + 该档给敌人什么」** ——
/// 只显示一根条不够，因为它要支撑玩家"**自选难度换收益**"的决策：
/// 玩家必须能**事先算得出**"再暗一档，敌人会强多少、我能多拿多少补给"。
///
/// 🔴 分工：数值全部来自内核（`LightMeter.Value` / `Tier` / `Effect`），本类**不做任何光照计算**。
/// </summary>
public partial class LightBarPanel : CanvasLayer
{
    private ProgressBar _bar = null!;
    private Label _text = null!;

    public override void _Ready()
    {
        _bar = new ProgressBar
        {
            Name = "LightBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            Position = new Vector2(24, 16),
            Size = new Vector2(360, 24),
        };
        AddChild(_bar);

        _text = new Label
        {
            Name = "LightText",
            Position = new Vector2(396, 14),
            Size = new Vector2(860, 60),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_text);
    }

    /// <summary>按内核读数刷新（**只渲染，不计算**）。</summary>
    public void Refresh(LightMeter meter)
    {
        _bar.Value = meter.Value;
        _text.Text = Describe(meter.Value, meter.Tier, meter.Effect);
    }

    /// <summary>
    /// 一行文本 = **当前值 + 档位 + 该档给敌人什么**（规格三要素；数值直接取自 `TuningLightEffect`）。
    /// </summary>
    public static string Describe(int value, LightTier tier, TuningLightEffect e)
    {
        string tierName = tier switch
        {
            LightTier.Radiant => "Radiant（明亮）",
            LightTier.Dim => "Dim（微暗）",
            LightTier.Shadowy => "Shadowy（昏暗）",
            LightTier.Dark => "Dark（黑暗）",
            _ => "Black（漆黑）",
        };

        return string.Create(CultureInfo.InvariantCulture,
            $"光照 {value}/100　档位 {tierName}　给敌人：命中 +{e.EnemyAcc:0.#} / 伤害 +{e.EnemyDmgPct:0.#}% / 暴击 +{e.EnemyCritPct:0.#}%" +
            $"　我方：士气伤害 +{e.OurMoraleDamagePct:0.#}% / 暴击 +{e.OurCritPct:0.#}% / 被偷袭 +{e.OurAmbushPct:0.#}% / 侦察 +{e.ScoutingPct:0.#}%");
    }
}
