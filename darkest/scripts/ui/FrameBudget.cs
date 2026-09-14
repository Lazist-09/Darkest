using System;
using System.Collections.Generic;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 架构审计清单 **⑨ 帧预算基线**（`godot_builtins_audit.md` §4；"不需要规格"的一项）——
/// 此前全项目 `Performance.GetMonitor` 用量 = **0** ⇒ **表现层没有任何性能观测手段**（架构原话）。
///
/// 用法：`--frame-audit`（与 `--ui-audit` 同一套模式：**跨场景存活**，装一次即可）。
/// 口径：**丢弃前 `WarmupFrames` 帧**（场景刚建、首次绘制、GC 抖动）⇒ 再采 `SampleFrames` 帧 ⇒ 打印均/P95/峰 + 余量。
/// ⚠️ 它是**观测**，不改任何玩法/表现行为（零数值改动）✓
/// </summary>
public static class FrameBudget
{
    public const string Flag = "--frame-audit";

    private static FrameBudgetProbe? _probe;

    /// <summary>`--frame-audit` 是否开启。</summary>
    public static bool Requested => Array.Exists(OS.GetCmdlineArgs(), a => a == Flag);

    /// <summary>安装探针（**幂等**；挂 `SceneTree.Root` ⇒ 跨场景存活）。</summary>
    public static void InstallIfRequested(Node context)
    {
        if (!Requested || _probe is not null)
        {
            return;
        }

        SceneTree? tree = context.GetTree();
        if (tree is null)
        {
            return;
        }

        _probe = new FrameBudgetProbe { Name = "FrameBudgetProbe" };

        // 🔴 **不能**在 `_Ready` 里直接 `Root.AddChild(...)`：`Root` 此刻正在 `add_child` 本场景（busy）
        //    ⇒ 引擎报 `Parent node is busy setting up children, add_child() failed` ⇒ 探针根本没进树（实测踩过，见 `UiAuditHook`）
        tree.Root.CallDeferred(Node.MethodName.AddChild, _probe);
    }
}

/// <summary>帧预算探针本体（`_Process` 采样；采满即打印并停止采样）。</summary>
public partial class FrameBudgetProbe : Node
{
    /// <summary>丢弃前多少帧（场景刚建/首次绘制/GC 抖动不算基线）。</summary>
    private const int WarmupFrames = 60;

    /// <summary>采样帧数（300 帧 ≈ 5s @60fps）。</summary>
    private const int SampleFrames = 300;

    /// <summary>60fps 的每帧预算（ms）。</summary>
    private const double BudgetMs = 1000.0 / 60.0;

    /// <summary>
    /// 🔴 **架构裁定的可断言基线**（`next_round §4.1` / `godot_builtins_audit` ⑨）：
    /// **战斗屏 UI 节点（Control）基线 195，允许 ±10%**（&gt; 上限需解释：泄漏 / 未回收 / 重建）。
    /// ⚠️ 只对**战斗屏**成立（它是 UI 最重的屏；其余屏 12/47/54，天然在带内）。
    /// </summary>
    public const int ControlBaseline = 195;

    /// <summary>基线容差（±10%）。</summary>
    public const double ControlTolerance = 0.10;

    private readonly List<double> _ms = new();
    private int _frames;
    private bool _done;
    private string _sceneName = "?";
    private string _samplingScene = "?";

    public override void _Process(double delta)
    {
        if (_done)
        {
            return;
        }

        // 🔴 **每进一个场景重新预热**：否则"切场景 + 建树"的尖峰会被算进基线（实测：那样算出来 120ms/帧，与同日志 FPS 144 自相矛盾）⚠️
        string scene = GetTree()?.CurrentScene?.Name ?? "?";
        if (scene != _samplingScene)
        {
            _samplingScene = scene;
            _sceneName = scene;
            _frames = 0;
            _ms.Clear();
        }

        _frames++;
        if (_frames <= WarmupFrames)
        {
            return;
        }

        _ms.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0);
        if (_ms.Count < SampleFrames)
        {
            return;
        }

        _done = true;
        GD.Print($"[帧预算] {Report()}");
        SetProcess(false); // 一次性：采满即停（不刷屏）
    }

    private string Report()
    {
        _ms.Sort();
        double sum = 0;
        foreach (double v in _ms)
        {
            sum += v;
        }

        double avg = sum / _ms.Count;
        double min = _ms[0];
        double p95 = _ms[Math.Min(_ms.Count - 1, (int)(_ms.Count * 0.95))];
        double max = _ms[^1];
        double monitorFps = Performance.GetMonitor(Performance.Monitor.TimeFps);
        int engineFps = (int)Engine.GetFramesPerSecond();
        int drawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        int objects = (int)Performance.GetMonitor(Performance.Monitor.ObjectCount);
        double memMb = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024.0 * 1024.0);
        int controls = CountControls(GetTree()?.CurrentScene);
        int upper = (int)(ControlBaseline * (1 + ControlTolerance));
        string budgetVerdict = controls <= upper
            ? $"✅ 在带内（基线 {ControlBaseline}，上限 {upper}）"
            : $"🔴 超基线（{controls} > {upper}）⇒ **需解释**：泄漏 / 未回收 / 重建";

        return $"基线（场景 {_sceneName} ／ {_ms.Count} 帧，进场景后丢弃前 {WarmupFrames} 帧）：" +
               $"进程耗时 均 {avg:0.00}ms ／ 最小 {min:0.00}ms ／ P95 {p95:0.00}ms ／ 峰 {max:0.00}ms" +
               $"　（60fps 预算 {BudgetMs:0.00}ms ⇒ 余量 {(1 - (avg / BudgetMs)) * 100:0}%）" +
               $"　FPS 引擎 {engineFps} ／ 监视器 {monitorFps:0}　绘制调用 {drawCalls}　对象 {objects}　静态内存 {memMb:0.0}MB" +
               $"　**UI 节点（Control）{controls} 个** ⇒ {budgetVerdict}" +
               $"　⚠️ 口径：headless 沙箱的 wall-clock **仅【同环境同口径】可比**（实测与 FPS 自相矛盾 ⇒ 不作性能结论）；" +
               $"节点数/对象/内存为**确定性指标**";
    }

    /// <summary>数当前场景里的 `Control` 节点（UI 节点数 = UI 性能的一个可跟踪指标）。</summary>
    private static int CountControls(Node? root)
    {
        if (root is null)
        {
            return 0;
        }

        int n = root is Control ? 1 : 0;
        foreach (Node child in root.GetChildren())
        {
            n += CountControls(child);
        }

        return n;
    }
}
