using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 `#319`⑤ / `ui_spec §14.5` 布局判据的【跨场景取证钩子】（`--ui-audit`）。
///
/// 为什么单独一个类：**实测抓到的真因**（不是推测）——
/// ```
/// 在 `_Ready` 内直接 `GetTree().Root.AddChild(timer)` ⇒ 引擎报：
///   ERROR: Parent node is busy setting up children, `add_child()` failed.
///          Consider using `add_child.call_deferred(child)` instead.
///   [2] MainMenuRoot.PrintUiAudit() (MainMenuRoot.cs:63)
///   [3] MainMenuRoot._Ready()      (MainMenuRoot.cs:75)
/// ⇒ `Root` 此刻正在 `add_child` 本场景（busy）⇒ **定时器根本没进树** ⇒ `--ui-audit` 一行都不输出 ⚠️
/// ```
/// ⇒ 🔴 **取不到数 = 取证失败，不是"通过"**（红线 25）⇒ 本类把两个坑一次性按正确姿势解决：
///   · **坑① busy** ⇒ 用 `CallDeferred` 入树（延后到"父节点不再增删子节点"之后）；
///   · **坑② 拥有者被释放** ⇒ 挂在 **`SceneTree.Root`**（不随切场景释放），且**回调不捕获任何会被释放的节点**
///     （只用 `Timer` 自己的 `GetTree()`）✓
///
/// 判据口径（`LayoutAudit` 内实现）：
/// 🔴 判据 1 = 可见 `Label` 两两不相交；🔴 判据 2 = `Panel`/`PanelContainer` 的 `BgColor.a == 1.0`。
/// ⚠️ **每屏跑满 5 次、以最后一次为准**：界面刚建好时 Label 还是**空文本**，判据会跳过它
/// ⇒ 只报一次就可能"过早判定 ⇒ 假通过"（主程序实测踩过）。
/// </summary>
public static class UiAuditHook
{
    private const string Flag = "--ui-audit";

    /// <summary>每屏打印几次（都以**最后一次**为准；防"过早判定的假通过"）。</summary>
    private const int FiresPerScene = 5;

    /// <summary>每屏最多打印多少条（含"读数变化"的追加打印；防刷屏）。</summary>
    private const int MaxPrintsPerScene = 30;

    private static bool _installed;
    private static Node? _lastScene;
    private static string _lastReport = string.Empty;
    private static int _printsThisScene;

    /// <summary>`--ui-audit` 是否开启（唯一开关，读命令行）。</summary>
    public static bool Requested => System.Array.Exists(OS.GetCmdlineArgs(), a => a == Flag);

    /// <summary>
    /// 若命令行带 `--ui-audit` ⇒ 安装（**幂等**：多次调用只装一个定时器，挂在 `Root` 上跨场景存活）。
    /// 调用点必须在场景根 `_Ready` 的**第一句**（后面的冒烟步骤可能提前 `return`）。
    /// </summary>
    public static void InstallIfRequested(Node context)
    {
        if (!Requested || _installed)
        {
            return;
        }

        SceneTree? tree = context.GetTree();
        if (tree is null)
        {
            return;
        }

        _installed = true;

        var timer = new Timer
        {
            Name = "UiAuditTick",
            WaitTime = 0.4,
            OneShot = false,
            Autostart = true,
        };

        int fires = 0;
        timer.Timeout += () =>
        {
            SceneTree? t = timer.GetTree(); // ⚠️ 只用 timer 自己取树 ⇒ 不捕获会被释放的节点
            if (t is null)
            {
                return;
            }

            Node target = t.CurrentScene ?? t.Root;

            // 换了场景 ⇒ 计数重来（同一趟里每屏各跑满 5 次）✓
            if (!ReferenceEquals(target, _lastScene))
            {
                _lastScene = target;
                fires = 0;
                _printsThisScene = 0;
                _lastReport = string.Empty;

                // 🔴 `§12.4` i18n 验收：`--ui-longtext` ⇒ 先把文本膨胀 ~40%，再照跑同一套判据 ✓
                if (LongTextProbe.Requested)
                {
                    int inflated = LongTextProbe.Inflate(target);
                    GD.Print($"[UI i18n] 长文本压力（§12.4）：已膨胀 {inflated} 个 Label（+~40% 宽字）⇒ 判据随后照跑");
                }
            }

            fires++;
            (bool ok, string report) = LayoutAudit.Check(target);

            // 🔴 打印口径（两条，缺一不可）：
            //   ① 每屏前 5 次**必打**（"以最后一次为准"，防"过早判定的假通过"）；
            //   ② 之后**读数一变就打** —— 同场景内换面板/开模态（Curio ／ 扎营 ／ 详情…）也会被审到
            //      ⚠️ 我第一版只做 ① ⇒ 同场景内的面板切换**完全审不到**（漏审 = 假绿）
            bool reportChanged = report != _lastReport;
            bool withinBurst = fires <= FiresPerScene;
            if (!withinBurst && !reportChanged)
            {
                return;
            }

            if (_printsThisScene++ > MaxPrintsPerScene)
            {
                return; // 防刷屏
            }

            _lastReport = report;
            GD.Print($"[UI 判据] {target.Name} 第 {fires} 次：{report}");
            if (fires == FiresPerScene)
            {
                GD.Print($"[UI 判据] {target.Name} 结论（以最后一次为准）：{(ok ? "✅ 两条判据通过" : "🔴 未通过")}");
            }
        };

        // 🔴 关键：deferred 入树 + 挂在 Root（见类注释的两个坑）
        tree.Root.CallDeferred(Node.MethodName.AddChild, timer);
    }
}
