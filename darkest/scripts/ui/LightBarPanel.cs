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
/// 🔴 `#319` **改类（实机取证）**：本类**原来是 `CanvasLayer`** ⇒ **不是 `Control`** ⇒ 容器不排它、不受 Theme 管；
/// 更糟：`Expedition.tscn` 把这个节点声明为 `PanelContainer` ⇒ 引擎报
/// `Script inherits from native type 'CanvasLayer', so it can't be assigned to an object of type: 'PanelContainer'`
/// ⇒ **脚本根本没挂上 ⇒ 整个光照条是死的**（实测同类错误 3 条：本类 + `PathChoicePanel` + `InventoryPanel`）⚠️
/// ⇒ 正解 = **本类自己就是面板**（`PanelContainer`）+ 内部 `VBoxContainer` 堆叠 ✓
public partial class LightBarPanel : PanelContainer
{
    private ProgressBar _bar = null!;
    private Label _text = null!;

    /// <summary>
    /// 档位**边界视觉标记**（`#272` ③ / `m7_verification` V11 ④）：
    /// 在档位分界处画竖线 + 文字 —— 否则玩家看不出"**再走一步就进 Dark**"，
    /// 而"自选风险"要求玩家能**预判**（看不见边界就无法预判）。
    /// 🔴 **边界 = 数据**（策划 `#328`① 裁 (b)：**两侧都读 data** ⇒ 唯一真相）：
    ///    边界在 `tuning.json: light.tiers` 的每档 `min/max` 里；内核用 `LightMeter.BoundariesFrom(tiers)` 推导 ✓
    ///    ⇒ 本面板**不再自带任何边界常量**（此前抄过一份 `{25,50,75}`、后又引用过内核常量，两次都是"两处真值"）⚠️
    ///    ⇒ 现在由**调用方**把 `LightMeter.BoundariesFrom(Tuning.Light.Tiers)` 传进 <see cref="BuildMarks"/>；
    ///      拿不到就**不画刻度并留痕**（红线 21：不假装有刻度）✓
    /// </summary>
    private static readonly int[] NoBoundaries = System.Array.Empty<int>();

    private Control _barHost = null!;
    private int _marksBuiltFor = -1; // 已按哪一组边界画过刻度（避免重复建；也用于留痕）

    public override void _Ready()
    {
        // 🔴 本类**自己就是面板**（`PanelContainer`）⇒ 直接放 `VBox` 堆叠即可：
        //    Theme（不透明 panel 样式）由**容器树的根**（`UiMargin`）继承下来，不必自己挂 ✓
        var col = new VBoxContainer { Name = "LightBarCol" };
        col.AddThemeConstantOverride("separation", 4);
        AddChild(col);

        // 第 1 行：光照条 + 档位边界刻度（刻度是"相对条"的仪表 ⇒ 放在一个固定尺寸的宿主里，避免用屏幕坐标）
        _barHost = new Control { Name = "LightBarHost", CustomMinimumSize = new Vector2(360, 66) };
        col.AddChild(_barHost);

        _bar = new ProgressBar
        {
            Name = "LightBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            Position = new Vector2(0, 4),
            Size = new Vector2(360, 24),
        };
        _barHost.AddChild(_bar);

        // 🔴 边界刻度**不在这里写死**（见 `BuildMarks`）：由调用方传入 `LightMeter.BoundariesFrom(tiers)`（数据驱动）✓

        // 第 2 行：说明文本（当前值 + 档位 + 该档给敌人什么）
        _text = new Label
        {
            Name = "LightText",
            // 🔴 相机 720 口径（规则①）：**说明文本不换行 + 裁切** ——
            //    实测它在窄分配下换行把顶栏顶到 **209 高**（正常仅 ~50），进而让战斗屏需求 1019 > 720 ⚠️
            //    完整说明走 `TooltipText` ✓
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(0, 22),
        };
        col.AddChild(_text);
    }

    /// <summary>按内核读数刷新（**只渲染，不计算**）；`boundaries` 由调用方从**数据**推导（`LightMeter.BoundariesFrom(tiers)`）✓</summary>
    public void Refresh(LightMeter meter, int[]? boundaries = null)
    {
        _bar.Value = meter.Value;
        _text.Text = Describe(meter.Value, meter.Tier, meter.Effect);
        BuildMarks(boundaries);
    }

    /// <summary>
    /// 画档位边界刻度（**边界来自数据**，不在 UI 写死）。同一组边界只建一次；
    /// ⚠️ **拿不到边界 ⇒ 不画刻度并留痕**（红线 21：不假装有刻度、也不静默）✓
    /// </summary>
    private void BuildMarks(int[]? boundaries)
    {
        int[] bs = boundaries ?? NoBoundaries;
        int key = bs.Length == 0 ? 0 : bs[0] * 1000 + bs[^1] * 10 + bs.Length;
        if (key == _marksBuiltFor)
        {
            return;
        }

        foreach (Node child in _barHost.GetChildren())
        {
            if (child is ColorRect or Label && child.Name.ToString().StartsWith("LightMark", System.StringComparison.Ordinal))
            {
                _barHost.RemoveChild(child);
                child.QueueFree();
            }
        }

        _marksBuiltFor = key;
        if (bs.Length == 0)
        {
            GD.Print("[UI 光照条] **未提供档位边界** ⇒ 不画刻度（如实留痕；边界应由 `LightMeter.BoundariesFrom(tiers)` 传入）");
            return;
        }

        foreach (int boundary in bs)
        {
            var mark = new ColorRect
            {
                Name = $"LightMark{boundary}",
                Color = new Color(Darkest.Ui.DdTheme.Gold, 0.9f), // `§14.4`：边界刻度 = 强调金
                Position = new Vector2((int)(360 * boundary / 100.0) - 1, 0),
                Size = new Vector2(2, 32),
            };
            _barHost.AddChild(mark);

            var caption = new Label
            {
                Name = $"LightMarkText{boundary}",
                Text = boundary.ToString(),
                Position = new Vector2((int)(360 * boundary / 100.0) - 6, 32),
            };
            _barHost.AddChild(caption);
        }

        GD.Print($"[UI 光照条] 刻度已按**数据**建立：{string.Join("/", bs)}（来源 `LightMeter.BoundariesFrom(tiers)`）");
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
            $"光照 {value}/100　档位 {tierName}" +
            $"　给敌人：命中 +{e.EnemyAcc:0.#} / 伤害 +{e.EnemyDmgPct:0.#}% / 暴击 +{e.EnemyCritPct:0.#}%（**已生效**）" +
            $"　我方：士气伤害 +{e.OurMoraleDamagePct:0.#}% / 暴击 +{e.OurCritPct:0.#}%（**已生效**）" +
            $"· 被偷袭 +{e.OurAmbushPct:0.#}%（**未生效**：夜袭判定尚未接进生产路径，见 #302）" +
            $"　侦察 +{e.ScoutingPct:0.#}%（**已生效**）");
    }
}
