using System;
using System.Collections.Generic;
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
/// 🔴 `#319` **改类（第五屏补齐）**：本类原来是 `Node2D` ⇒ **不是 `Control`** ⇒
/// ① 锚点算不出父矩形（`Node2D` 没有 `get_anchorable_rect()`）⇒ 只能手摆坐标；② Theme 链不经过它。
/// ⇒ 改成 **`Control`**（场景节点类型同步改为 `Control` + FullRect）后，才能按 `§14.2/§14.3` 用容器 + 不透明 Panel ✓
public partial class MainMenuRoot : Control
{
    public const string BattleScene = "res://scenes/battle/Battle.tscn";
    // 🔴 片 4①：远征入口**改指唯一宿主** `Battle.tscn`（原 `Expedition.tscn` 退休）⇒ 配合 `ExpeditionContext.RequestDungeon()` 进入**地图模式** ✓
    public const string ExpeditionScene = BattleScene;
    public const string HamletScene = "res://scenes/hamlet/Hamlet.tscn";

    private Label _title = null!;
    private Label _status = null!;
    private VBoxContainer _menuCol = null!;   // 主菜单的纵向容器（标题 / 三选一 / 状态）
    private VBoxContainer _optionsCol = null!; // 三选一按钮的容器
    private readonly List<Button> _menuButtons = new(); // 🔴 按钮现在在容器里（不再是场景根的直接子节点）⇒ 用列表按下标取

    public override void _Ready()
    {
        // 🔴 `#319`⑤ 判据取证必须在 `_Ready` 的【第一句】装起来：冒烟步骤（`SmokeScript.Step`）在后面，
        //    某些路径会提前 `return`（`--hamlet-embark` 那条就是）⇒ 放在后面会**整段被跳过**（实测）✓
        //  ⚠️ 安装逻辑移进 `UiAuditHook`（它按正确姿势 **deferred 入树到 `Root`**）——
        //     旧实现在 `_Ready` 里直接 `GetTree().Root.AddChild(timer)` ⇒ 引擎报
        //     `Parent node is busy setting up children, add_child() failed` ⇒ **定时器根本没进树**
        //     ⇒ `--ui-audit` 一行都不输出（**取证失败 ≠ 通过**，红线 25）—— 已修 ✓
        UiAuditHook.InstallIfRequested(this);

        // 🔴 架构清单 ⑨：**帧预算基线**（`Performance.GetMonitor`；此前全项目 0 处 ⇒ 表现层无性能观测）
        FrameBudget.InstallIfRequested(this);

        // 🔴 `ui_spec §14.2/§14.3`（`#319` 第五屏补齐）：**主菜单也是"容器 + 不透明 Panel"** ——
        //    它是玩家**第一眼**看到的一屏（此前是手摆坐标、一个 `Panel` 都没有 ⇒ 四屏审计没覆盖到它）✓
        Darkest.Ui.DdTheme.Apply(this); // 本类现在是 `Control` ⇒ 主题沿祖先链继承
        var menuMargin = new MarginContainer { Name = "MenuMargin" };
        menuMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            menuMargin.AddThemeConstantOverride(side, 24);
        }

        AddChild(menuMargin);

        _menuCol = new VBoxContainer { Name = "MenuCol" };
        _menuCol.AddThemeConstantOverride("separation", 12);
        menuMargin.AddChild(_menuCol);

        var titlePanel = new PanelContainer { Name = "TitlePanel" };
        _menuCol.AddChild(titlePanel);
        _title = new Label
        {
            Name = "MenuTitle",
            Text = "【主菜单】选择去向（三选一；单场战斗保留 —— 它是 A1 判定闸的基础）",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        titlePanel.AddChild(_title);

        var optionsPanel = new PanelContainer { Name = "OptionsPanel", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _menuCol.AddChild(optionsPanel);
        _optionsCol = new VBoxContainer { Name = "OptionsCol" };
        _optionsCol.AddThemeConstantOverride("separation", 8);
        optionsPanel.AddChild(_optionsCol);

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

        var statusPanel = new PanelContainer { Name = "StatusPanel" };
        _menuCol.AddChild(statusPanel);
        _status = new Label
        {
            Name = "MenuStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        statusPanel.AddChild(_status);
        _status.Text = $"跨趟状态：金钱 {economy.Gold}　名册 {roster.Heroes.Count}/{roster.Cap}　" +
                       $"最低士气 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}";

        // 🔴 Godot 内置清单 ②（本轮轴：**字体/颜色集中**）：**中央 Theme 挂到引擎根 Window**
        //    ⇒ 之后所有场景的控件**自动继承**（Theme 沿 Control/Window 祖先链传播）✓ 不必逐屏设置 ✓
        GetTree().Root.Theme = Darkest.Ui.DdTheme.Shared;
        _title.Theme = null; // （保持可读性：显式声明"标题不另设 Theme"，样式来自中央 Theme + 语义色 override）
        _title.ThemeTypeVariation = Darkest.Ui.DdTheme.TitleVariation; // 🔴 架构裁定②：标题用 Bold（字号 × 字重双轴）

        // 🔴 架构裁定（`DELIVERY-ARCH-UI-RULINGS2-20260915` ①）：**调色板两视图一致性检查挪到【必经路径】**
        //    —— `UiPalette.Default()`（C# 兜底）与 `resources/theme/ui_palette.tres`（数据源）**不得分叉**（`#325` D6）。
        //    理由（架构原话）：**"不可被忘"优先于"便宜"** —— 本项目已被"靠人记得"坑过三次
        //    （`O-84` 导出崩溃 / `O-82` 光照六字段 / `AvailableCurios` 从未被生产调用）⇒ 检查放进启动路径 ✓
        //    ⚠️ 差异时**打印显著警告、不崩**（保红线 21：缺文件不崩），但**不允许静默分叉** ✓
        {
            (bool paletteOk, string paletteRep) = Darkest.Ui.UiPalette.AuditFile();
            if (!paletteOk)
            {
                GD.PrintErr($"[启动自检·调色板] {paletteRep}");
                GD.PrintErr("[启动自检·调色板] 🔴 两份真值已分叉 ⇒ 请同步 `UiPalette.Default()` 与 `.tres`（#325 D6）");
            }
            else
            {
                GD.Print($"[启动自检·调色板] {paletteRep}");
            }
        }

        // 🔴 审计清单② 取证：`--theme-audit` ⇒ 打印中央 Theme 读数 + **从真实控件读出的生效值**（证明继承成功）
        //    并把 Theme 存一份 `.tres` 到契约的落点目录 `resources/theme/`（编辑器里可见；后续可改成资源加载）
        if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--theme-audit"))
        {
            Error err = Darkest.Ui.DdTheme.DumpTo();
            GD.Print($"[Theme审计] {Darkest.Ui.DdTheme.Audit()}");
            GD.Print($"[Theme审计] 落点：res://resources/theme/dd_theme.tres ⇒ {err}");

            // 🔴 `#325` D6 + 架构裁定（第 5 条）：**兜底值不得与 `.tres` 分叉** ⇒ `--palette-audit` 逐字段比对
            if (Array.Exists(OS.GetCmdlineArgs(), a => a == "--palette-audit"))
            {
                (bool pOk, string pRep) = Darkest.Ui.UiPalette.AuditFile();
                GD.Print($"[调色板审计] {pRep}");
                GD.Print($"[调色板审计] 结论：{(pOk ? "✅ 通过" : "🔴 未通过（两视图分叉）")}");
            }

            string[] dumpArgs = OS.GetCmdlineArgs();
            if (Array.Exists(dumpArgs, a => a == "--dump-palette"))
            {
                Error perr = Darkest.Ui.DdTheme.DumpPalette();
                GD.Print($"[Theme审计] 调色板导出：{Darkest.Ui.UiPalette.ResPath} ⇒ {perr}" +
                         "（**之后改它即生效**，不必改代码 —— `#325` D5）");
            }

            // 🔴 **如实报**：生效值**不等于**中央 Theme ⇒ **继承没生效**
            //    根因：本屏根是 `Node2D`（`BattleUi` 是 `CanvasLayer`）—— **都不是 `Control`**
            //    ⇒ Godot 的主题查找沿 **Control/Window 祖先链**走，链上没有我们的 Theme ⇒ 落到引擎默认 16
            //    ⇒ 📌 **下一轮（架构清单② 的"容器+锚点"）就是修这个**：给每屏加一个满屏根 `Control` 并挂 Theme ✓
            int effective = _title.GetThemeFontSize("font_size");
            GD.Print($"[Theme审计] 生效值（从 _title 读出）：font_size = {effective}　" +
                     $"中央 Theme 期望 = {Darkest.Ui.DdTheme.FontBody}　" +
                     $"=> {(effective == Darkest.Ui.DdTheme.FontBody ? "✅ 继承生效" : "🔴 未生效（根不是 Control ⇒ 主题链断）")}");

            // 🔴 `§1.4`① / `§12.3` 取证：**文字描边**也必须是**引擎内置项**的生效值（不是自研 shader、不是逐节点 override）
            int outline = _title.GetThemeConstant("outline_size");
            Color outlineColor = _title.GetThemeColor("font_outline_color");
            GD.Print($"[Theme审计] 生效值（从 _title 读出）：outline_size = {outline}（期望 {Darkest.Ui.DdTheme.OutlineSize}）　" +
                     $"描边色 = {outlineColor.ToHtml()}（期望 {Darkest.Ui.DdTheme.Outline.ToHtml()}）　" +
                     $"=> {(outline == Darkest.Ui.DdTheme.OutlineSize ? "✅ 文字描边（深色粗描边）继承生效" : "🔴 描边未生效")}");
        }

        // 🔴 跨场景步进冒烟：**先解析步骤**（只解析一次）—— 解析后本场景也要消费一步
        Darkest.Gameplay.Scene.SmokeScript.InitFromArgs();
        Darkest.Gameplay.Scene.SmokeScript.Step(this);

        // 🔴 `ui_spec §14.5` / `#319`⑤ 的两条自动判据（可见 Label 两两不相交 · Panel/PanelContainer 的 a == 1.0）
        //    已由**本方法开头**的 `UiAuditHook.InstallIfRequested(this)` 统一装上 ⇒ 这里**不再重复安装**
        //    （旧实现在此第二次调用，既重复又会踩同一个 `Root` busy 坑 —— 已删 ✓）

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
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4①：回退路径同样置"请求地牢"（否则会变成单场战斗 ⚠️）
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
            CustomMinimumSize = new Vector2(420, 38), // 🔴 容器排布 ⇒ 只给最小尺寸（不再手摆 `Position/Size`）
        };
        // 🔴 必须 **deferred**：冒烟会在 `_Ready` 里直接按下菜单键 ⇒ 同步切场景会报
        //    `Parent node is busy adding/removing children`（实测抓到的真凶就在这一行）
        button.Pressed += () =>
        {
            if (index == 1)
            {
                Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4①：出发远征 ⇒ 宿主进地牢 ✓
            }

            GetTree().CallDeferred("change_scene_to_file", scenePath);
        };
        _optionsCol.AddChild(button); // 🔴 `§14.2`①：**创建时进容器**
        _menuButtons.Add(button);
    }

    /// <summary>
    /// 🔴 V8 补条（`#289`）：**点击路径**的验收 —— `--e2e` 走的是 CLI 分支，**"CLI 能过 ≠ 点得动"**。
    /// 本方法**发出真实的 `Pressed` 信号**（走按钮 → 回调这条链路），供 headless 冒烟验证按钮真的接上了。
    /// ⚠️ 按钮已进容器树 ⇒ 不能用 `GetNodeOrNull("Menu{i}")`（那假设它是场景根的**直接**子节点）⇒ 按列表下标取 ✓
    /// </summary>
    public void PressMenu(int index)
    {
        if (index < 0 || index >= _menuButtons.Count)
        {
            GD.Print($"[MainMenuRoot] PressMenu({index})：**找不到按钮**（当前 {_menuButtons.Count} 个 —— 红线 21：不留不可解释的状态）");
            return;
        }

        Button button = _menuButtons[index];
        GD.Print($"[MainMenuRoot] PressMenu({index})：发出真实 Pressed 信号（按钮「{button.Text}」）");
        button.EmitSignal(BaseButton.SignalName.Pressed); // 走真实回调，不是直接切场景
    }
}
