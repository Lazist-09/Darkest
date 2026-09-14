using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M8.0 收尾 ①（`#288` / V8）：**主菜单 = 启动入口的正解**。
///
/// 背景：`project.godot` 的 main_scene 原本是 `Battle.tscn`（那是 `#279` 孤岛的 **(b) 权宜**），
/// 玩家启动即进单场战斗，要手动点按钮才能去远征/回城。本场景把入口摆正：
/// **启动 ⇒ 主菜单 ⇒ 三选一（单场战斗 ／ 出发远征 ／ 回城）**。
///
/// 🔴 三条硬要求：
/// · **三个入口都保留** —— **单场战斗不能删**（它是 **A1 判定闸**的基础）；
/// · **CLI 全部保留**（`--expedition` / `--hamlet` / `--hamlet-next` / `--e2e`，冒烟依赖它们）⇒ 本场景**转发 CLI**；
/// · **只渲染 + 转发**（本类不持游戏状态）。
/// 验收（红线 18）：三项**从启动场景可达**（不是场景直载）。
/// </summary>
public partial class MainMenuRoot : Node2D
{
    public const string BattleScene = "res://scenes/battle/Battle.tscn";
    public const string ExpeditionScene = "res://scenes/expedition/Expedition.tscn";
    public const string HamletScene = "res://scenes/hamlet/Hamlet.tscn";

    private Label _title = null!;
    private Label _status = null!;

    /// <summary>🔴 `#319`⑤ 判据取证：**跨场景**定时审计当前界面（挂到场景树根 ⇒ 不随切场景丢失）。</summary>
    public void PrintUiAudit()
    {
        // ⚠️ 两个坑（我都踩过）：
        //    ① 必须"切到目标场景之后再审"：`--hamlet` 是 **CallDeferred 切场景** ⇒ 立即审会审到 MainMenu；
        //    ② 回调**不得捕获 `this`**（MainMenuRoot 在切场景时被释放 ⇒ 回调打到已释放对象 ⇒ 什么都不打印）
        //    ⇒ 挂到 `Root` 上按间隔审几次，回调只用 **Timer 自己**的 `GetTree()` ✓
        var timer = new Timer { Name = "UiAuditTick", WaitTime = 0.4, OneShot = false, Autostart = true };
        int fires = 0;
        timer.Timeout += () =>
        {
            SceneTree? tree = timer.GetTree();
            if (tree is null)
            {
                return;
            }

            fires++;
            Node target = tree.CurrentScene ?? tree.Root;
            (bool ok, string report) = Darkest.Ui.LayoutAudit.Check(target);
            GD.Print($"[UI 判据] 第 {fires} 次：{report}");

            // ⚠️ **不因"第一次通过"就停**：实测踩到 —— 界面刚建好时状态/进度 Label 还是**空文本**，
            //    判据跳过空 Label ⇒ 报"可见 Label 0 / ✅ 通过"（**过早判定**）⚠️
            //    ⇒ 一律跑满 5 次，**以最后一次为准** ✓
            if (fires >= 5)
            {
                timer.QueueFree();
            }
        };
        GetTree().Root.AddChild(timer);
    }

    /// <summary>🔴 `#319`⑤ 判据：供外部（冒烟/测试）调用。</summary>
    public (bool Ok, string Report) RunUiAudit() => Darkest.Ui.LayoutAudit.Check(GetTree().CurrentScene ?? this);

    public override void _Ready()
    {
        // 🔴 `#319`⑤ 判据必须在 `_Ready` 的【第一句】建起来：冒烟步骤（`SmokeScript.Step`）在后面，
        //    它可能提前 `return`（`--hamlet-embark` 那条路径就是）⇒ 放在后面会**整段被跳过**（实测无输出）⚠️
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--ui-audit"))
        {
            PrintUiAudit();
        }
        _title = new Label
        {
            Name = "MenuTitle",
            Text = "【主菜单】选择去向（三选一；单场战斗保留 —— 它是 A1 判定闸的基础）",
            Position = new Vector2(24, 24),
            Size = new Vector2(1200, 40),
        };
        AddChild(_title);

        AddMenuButton("单场战斗（A1 判定闸）", BattleScene, 0);
        AddMenuButton("出发远征（地牢层）", ExpeditionScene, 1);
        AddMenuButton("回城（Hamlet）", HamletScene, 2);

        // 跨趟状态在这里就先确保（菜单要显示金钱/名册概况）
        Economy economy = ExpeditionContext.EnsureEconomy(
            EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath)));
        Roster roster = ExpeditionContext.EnsureRoster(
            RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath)));

        // 🔴 合并包片 D + `next_round` ③：**解锁阈值表**（P27）—— 用**真实目录**校验引用（C2：内核也要拦）
        //    · 建筑目录 = `HeirloomConfig.AllowedBuildings`（tavern/abbey/stagecoach）
        //    · Curio 目录 = `curios.json`
        //    · 名册**硬上限** = `roster.Cap`（= 12；C1：解锁只抬高【当前可用上限】，起手 8）
        CuriosConfig curiosCfg = CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath));
        UnlocksConfig unlocks = UnlocksConfig.Parse(FileAccess.GetFileAsString(UnlocksConfig.ResPath),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curiosCfg.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            roster.Cap);

        // 🔴 C1 的消费点：**把"当前可用上限"写进名册**（招募的满员判定按它；硬上限仍 12）
        roster.CurrentCap = ExpeditionContext.Progress.CurrentRosterCap(unlocks, roster.Cap);
        GD.Print($"[MainMenuRoot] 解锁阈值表：{unlocks.Unlocks.Count} 条　" +
                 $"起手可用上限 {unlocks.RosterBaseCap}（硬上限 {roster.Cap}）　" +
                 $"{ExpeditionContext.Progress.Audit(unlocks, roster.Cap)}");

        _status = new Label
        {
            Name = "MenuStatus",
            Position = new Vector2(24, 220),
            Size = new Vector2(1200, 80),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_status);
        _status.Text = $"跨趟状态：金钱 {economy.Gold}　名册 {roster.Heroes.Count}/{roster.Cap}　" +
                       $"最低士气 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}";

        // 🔴 Godot 内置清单 ②（本轮轴：**字体/颜色集中**）：**中央 Theme 挂到引擎根 Window**
        //    ⇒ 之后所有场景的控件**自动继承**（Theme 沿 Control/Window 祖先链传播）✓ 不必逐屏设置 ✓
        GetTree().Root.Theme = Darkest.Ui.DdTheme.Shared;
        _title.Theme = null; // （保持可读性：显式声明"标题不另设 Theme"，样式来自中央 Theme + 语义色 override）

        // 🔴 审计清单② 取证：`--theme-audit` ⇒ 打印中央 Theme 读数 + **从真实控件读出的生效值**（证明继承成功）
        //    并把 Theme 存一份 `.tres` 到契约的落点目录 `resources/theme/`（编辑器里可见；后续可改成资源加载）
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--theme-audit"))
        {
            Error err = Darkest.Ui.DdTheme.DumpTo();
            GD.Print($"[Theme审计] {Darkest.Ui.DdTheme.Audit()}");
            GD.Print($"[Theme审计] 落点：res://resources/theme/dd_theme.tres ⇒ {err}");

            // 🔴 **如实报**：生效值**不等于**中央 Theme ⇒ **继承没生效**
            //    根因：本屏根是 `Node2D`（`BattleUi` 是 `CanvasLayer`）—— **都不是 `Control`**
            //    ⇒ Godot 的主题查找沿 **Control/Window 祖先链**走，链上没有我们的 Theme ⇒ 落到引擎默认 16
            //    ⇒ 📌 **下一轮（架构清单② 的"容器+锚点"）就是修这个**：给每屏加一个满屏根 `Control` 并挂 Theme ✓
            int effective = _title.GetThemeFontSize("font_size");
            GD.Print($"[Theme审计] 生效值（从 _title 读出）：font_size = {effective}　" +
                     $"中央 Theme 期望 = {Darkest.Ui.DdTheme.FontBody}　" +
                     $"=> {(effective == Darkest.Ui.DdTheme.FontBody ? "✅ 继承生效" : "🔴 未生效（根不是 Control ⇒ 主题链断）")}");
        }

        // 🔴 跨场景步进冒烟：**先解析步骤**（只解析一次）—— 解析后本场景也要消费一步
        Darkest.Gameplay.Scene.SmokeScript.InitFromArgs();
        Darkest.Gameplay.Scene.SmokeScript.Step(this);

        // 🔴 `ui_spec §14.5` / `#319`⑤ 的**两条自动判据**：`--ui-audit` ⇒ 遍历当前界面
        //    ① 可见 Label 两两不相交 ② 每个 Panel 的 BgColor.a == 1.0
        //    （把"文字重叠 / 框透明"从"看起来还行"变成**可断言**）✓
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--ui-audit"))
        {
            // ⚠️ **直接调用**（不要再 `CallDeferred`）：切场景现在也是 deferred ⇒ 本节点会**先被释放**
            //    ⇒ 延后的调用就永远不会发生（实测：改了切场景之后判据一行都不输出）⚠️
            PrintUiAudit();
        }

        // 🔴 输入审计（附 B ① 的例行项）：`--input-audit` ⇒ 打印自定义动作与绑定键
        //    （证据用途：任务动化是否真的生效 —— 不靠"我改了代码"自证）
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--input-audit"))
        {
            foreach (string action in new[] { "dd_restart", "dd_toggle_log", "ui_cancel" })
            {
                if (!InputMap.HasAction(action))
                {
                    GD.Print($"[输入审计] 🔴 {action}：**未注册**（裸键改动作化未生效）");
                    continue;
                }

                string[] keys = InputMap.ActionGetEvents(action)
                    .OfType<InputEventKey>()
                    .Select(k => $"physical={k.PhysicalKeycode}")
                    .ToArray();
                GD.Print($"[输入审计] {action}：已注册，键 = {(keys.Length == 0 ? "（无按键）" : string.Join("/", keys))}");
            }
        }

        // 🔴 CLI 全保留：启动即按参数直达（冒烟依赖；这些参数此前挂在 BattleRoot 上）
        string[] args = OS.GetCmdlineArgs();
        if (Array.Exists(args, a => a == "--hamlet"))
        {
            GD.Print("[MainMenuRoot] --hamlet ⇒ 直达回城（冒烟路径：启动 到 回城）");
            GetTree().CallDeferred("change_scene_to_file", HamletScene);
        }
        else if (Array.Exists(args, a => a == "--expedition" || a == "--e2e" || a == "--hamlet-next" || a == "--topology"))
        {
            GD.Print("[MainMenuRoot] --expedition/--e2e/--hamlet-next/--topology ⇒ 直达地牢层（冒烟路径）");
            GetTree().CallDeferred("change_scene_to_file", ExpeditionScene);
        }

        // 🔴 V8 点击路径冒烟：`--click-menu=N` ⇒ **发出真实 Pressed 信号**（验证按钮真的接上了）
        string? click = Array.Find(args, a => a.StartsWith("--click-menu=", StringComparison.Ordinal));
        if (click is not null && int.TryParse(click.Substring("--click-menu=".Length), out int menuIndex))
        {
            CallDeferred(nameof(PressMenu), menuIndex);
        }
    }

    private void AddMenuButton(string text, string scenePath, int index)
    {
        var button = new Button
        {
            Name = $"Menu{index}",
            Text = text,
            Position = new Vector2(24, 80 + (index * 44)),
            Size = new Vector2(420, 38),
        };
        // 🔴 必须 **deferred**：冒烟会在 `_Ready` 里直接按下菜单键 ⇒ 同步切场景会报
        //    `Parent node is busy adding/removing children`（实测抓到的真凶就在这一行）
        button.Pressed += () => GetTree().CallDeferred("change_scene_to_file", scenePath);
        AddChild(button);
    }

    /// <summary>
    /// 🔴 V8 补条（`#289`）：**点击路径**的验收 —— `--e2e` 走的是 CLI 分支，**"CLI 能过 ≠ 点得动"**。
    /// 本方法**发出真实的 `Pressed` 信号**（走按钮 → 回调这条链路），供 headless 冒烟验证按钮真的接上了。
    /// </summary>
    public void PressMenu(int index)
    {
        Button? button = GetNodeOrNull<Button>($"Menu{index}");
        if (button is null)
        {
            GD.Print($"[MainMenuRoot] PressMenu({index})：**找不到按钮**（说明按钮没挂上 —— 红线 21）");
            return;
        }

        GD.Print($"[MainMenuRoot] PressMenu({index})：发出真实 Pressed 信号（按钮「{button.Text}」）");
        button.EmitSignal(BaseButton.SignalName.Pressed); // 走真实回调，不是直接切场景
    }
}
