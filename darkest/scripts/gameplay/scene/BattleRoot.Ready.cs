// 🔴 从 BattleRoot.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3：只抽【启动装配】）——只搬家、零行为改动 ✓
//    本文件 = 场景启动装配（_Ready）：面板托管分流 · 外壳审计旗标 · 进地牢/战斗请求分流 · 冒烟触发器 · 入口按钮
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_dungeonHostedInScene x1
//    【依赖主类公有面/同域】NewGame（玩家操作片）· EnterDungeonInScene / StartExpeditionBattleInScene / GoToHamlet（流程桥片）
//      · AutoFinishBattle / PrintFocusAudit / ShowTab / ShowCardDetail / ShowMapPage（冒烟片）· HostedInPanelMeta（本片声明）

using System;
using Godot;

namespace Darkest.Gameplay.Scene;

public partial class BattleRoot
{
    /// <summary>🔴 **B-1 标记**：本实例是「被面板托管的战斗」（防止面板宿主自己再造面板 ⇒ 递归）✓</summary>
    private const string HostedInPanelMeta = "d87_battle_panel_hosted";

    public override void _Ready()
    {
        // 🔴 **B-1（形态 B · S4 第一步）**：--battle-panel ⇒ **本场景根不再自己驱动战斗**，
        //    而是**造一个战斗面板挂进外壳**（面板内那个 BattleRoot 会正常启动 ✓）
        //    ⚠️ 防递归：面板内那个实例带 meta 标记，不会再进这个分支 ✓

        // 🔴 **B-2 判据（我域）**：`--shell-layer` ⇒ 打印「外壳有效绘层 vs 当前场景有效绘层」✓
        //    （架构要的"外壳三层都在当前场景之上"的**可核对读数** ⇒ 判据本体在 `ShellLayerAudit` ✓）

        // 🔴 **C4 终态判据（我域）**：`--shell-count` ⇒ 数外壳实例数 + 看 `main_scene`/`autoload` ✓
        //    （架构原话：判据「外壳现在有几个实例？」**必须是 1** ✓）
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--shell-count"))
        {
            ShellInstanceAudit.Run(this);
        }

        // 🔴 **形态 B 一键状态（我域）**：`--shell-status` ⇒ 绘层 + 回落账本 + 实例数 一次看全 ✓
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--shell-status"))
        {
            ShellStatus.Run(this);
        }
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--shell-layer"))
        {
            ShellLayerAudit.Run(this);
        }
        bool hosted = HasMeta(HostedInPanelMeta);
        bool wantsPanel = !hosted && System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-panel");
        if (wantsPanel)
        {
            GD.Print("[BattleRoot] 🔴 --battle-panel ⇒ **走面板路径**（本场景根只做宿主，不再自己驱动战斗）✓");
            BattlePanel panel = BattlePanel.Create("BattlePanel");
            panel.Root?.SetMeta(HostedInPanelMeta, true);
            if (!panel.TryMountIntoShell())
            {
                GD.Print("[BattleRoot] 🔴 面板未挂进外壳 ⇒ 如实停在这里（不假装成功 ✓）");
            }

            return;
        }
        // 🔴🔴 **相位在【两个场景】之间的修补**（UI 实测报告：战斗场景里 `Phase` 仍是 `Walking` ⇒ 三谓词全 True ⚠️）
        //   根因：战斗是**另一个场景**，而 `ExpeditionSession.Phase` 只由远征那条循环推动 ⇒ 战斗期间它"诚实但过时" ✓
        //   ⇒ 现在进战斗就显式推进到 `Battle`（片 3 把两场景并成一个状态机后，这行会被状态机自然取代）✓
        if (ExpeditionContext.IsActive)
        {
            ExpeditionContext.Flow!.Session.EnterPhase(Darkest.Gameplay.Sim.Run.FlowPhase.Battle);
        }

        // 🔴 片 3 冒烟触发器：`--battle-piece3-exp` ⇒ 由宿主**在场景内**起流程战斗（验证"不再切场景"这条路）✓
        // 🔴 片 4④：`--dungeon-in-scene` ⇒ **宿主直接进地牢**（用新组装 `ExpeditionComposition`，不经过远征场景）✓
        // 🔴 片 4①：**入口请求**（主菜单/Hamlet 的"出发远征"）⇒ 进地图模式（**默认路径**，不再切远征场景）✓
        if (ExpeditionContext.ConsumeRequestDungeon())
        {
            EnterDungeonInScene();
        }

        // 🔴 片 4：远征驱动类 CLI（`--topology-auto` / `--hamlet-next` / `--e2e`）**已搬进宿主侧**
        //    （原在 `ExpeditionRoot`；搬走它，退休旧场景才不留悬空依赖 ✓）
        if (ExpeditionContext.IsActive && ExpeditionContext.Flow is { } drvFlow)
        {
            DungeonRunDriver.TryHandle(this, drvFlow, ExpeditionContext.Log ?? new Darkest.Core.Events.CombatLog());
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--dungeon-in-scene"))
        {
            EnterDungeonInScene();
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-piece3-exp"))
        {
            StartExpeditionBattleInScene();
        }

        // 🔴🔴 **片 3.1（用户裁定"进地牢不该一进来就是战斗" · 策划 `#384` 第 2 件）**：
        //    旧行为：无论从哪个入口进来，`_Ready` 末尾都**无条件** `NewGame()` ⇒ **进地牢 = 立刻起一场战斗** ❌
        //    新行为：**按【请求】分流** —— 已经进了地牢（有活动流程）⇒ **不起单场战斗**，只进 Walking（地图模式）✓
        //    其它入口（单场战斗 / `--battle-*` 冒烟 / 试验）⇒ 才 `NewGame()` ✓
        bool hostedInDungeon = _dungeonHostedInScene && ExpeditionContext.IsActive;
        if (hostedInDungeon)
        {
            GD.Print($"[片3.1] ✅ **进地牢 = 不起战斗**（按请求分流）：Phase={ExpeditionContext.Flow!.Session.Phase}（应为 Walking）" +
                     $"　CanShowPathChoice={ExpeditionContext.Flow.Session.CanShowPathChoice}（应为 True）" +
                     $"　战斗触发改为【踏进 Battle 格】（策划 #379）✓");
        }
        else
        {
            NewGame();
            GD.Print("[BattleRoot] 战斗就绪：轮到行动者时技能栏/换位可操作；敌方阶段自动结算；R 重开（新 seed）。");
        }

        // 🔴 O-74（阻塞级，`#279/#280`）：**地牢层的入口** ——
        // 此前 `Expedition.tscn` 不可达（`project.godot` 的 main_scene = Battle.tscn，
        // 而 `ExpeditionContext.Begin()` 只在远征场景内部调用 ⇒ `IsActive` 恒 false ⇒ 玩家看不到 M7.5 的任何东西）。
        // 这里给出**启动即可达**的入口：① 界面按钮 ② `--expedition` 命令行直达（供**端到端冒烟**用）。
        var startExpedition = new Button
        {
            Name = "StartExpedition",
            Text = "出发远征（地牢层）",
            Position = new Vector2(520, 700),
            Size = new Vector2(240, 40),
        };
        startExpedition.Pressed += () => EnterDungeonInScene(); // 🔴 片 4：场景内进地牢（不再切场景）✓
        AddChild(startExpedition);

        // 🔴 M8.0 ③（红线 18）：**回城入口也必须从启动场景可达**
        var toHamlet = new Button
        {
            Name = "ToHamlet",
            Text = "回城（Hamlet）",
            Position = new Vector2(280, 700),
            Size = new Vector2(220, 40),
        };
        toHamlet.Pressed += GoToHamlet;   // 🆕 外壳优先、缺失回落（见 GoToHamlet）
        AddChild(toHamlet);
        GD.Print("[BattleRoot] 地牢层入口就绪：StartExpedition 按钮（或 --expedition 命令行）⇒ **本场景内进地牢**（片 4：唯一宿主）✓");
        GD.Print("[BattleRoot] 回城入口就绪：ToHamlet 按钮（或 --hamlet 命令行）⇒ res://scenes/hamlet/Hamlet.tscn");

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--hamlet"))
        {
            GD.Print("[BattleRoot] --hamlet ⇒ 直接回城（端到端冒烟路径：启动 → 回城）");
            GoToHamlet();
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--expedition" || a == "--e2e"))
        {
            GD.Print("[BattleRoot] --expedition/--e2e ⇒ 直接进入地牢层（端到端冒烟路径：启动 → 进入地牢层选路）");
            // 🔴 必须 **deferred**：`_Ready` 期间父节点正在增删子节点，直接 ChangeSceneToFile 会报
            // 「Parent node is busy adding/removing children」（实测 exit 1）
            EnterDungeonInScene(); // 🔴 片 4：场景内进地牢（不再切场景）✓
        }

        // 🔴 冒烟（`#307`⑤ 流程闭环）：`--battle-auto-finish` ⇒ **自动结束本场并自动点【继续（回远征）】**
        //    目的：把「战斗 → 返回远征地图」这段**真实场景往返**变成可 headless 验证的一步。
        //    做法：敌方 HP 清零（= 胜利）⇒ 走**既有结束路径** `EndGame` ⇒ 触发远征回灌 + 继续按钮。
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-auto-finish"))
        {
            GD.Print("[BattleRoot] --battle-auto-finish ⇒ 自动结束本场（判定胜利）并自动返回远征");
            CallDeferred(nameof(AutoFinishBattle));
        }

        // 🔴 审计清单③ 冒烟：`--focus-audit` ⇒ 打印焦点所有者与可聚焦控件数（键盘/手柄导航的取证）
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--focus-audit"))
        {
            CallDeferred(nameof(PrintFocusAudit));
        }

        // 🔴 片③ 冒烟：`--battle-map` ⇒ **切到 E 区的【地图】页**（验"战斗里能看到同一趟的地图"）
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--battle-map"))
        {
            CallDeferred(nameof(ShowMapPage));
        }

        // 🔴 片③ 冒烟：`--battle-tab=N` ⇒ 切到 E 区第 N 页（0 详情 ／ 1 日志 ／ 2 序列 ／ 3 编成 ／ 4 地图）
        string? tabArg = System.Array.Find(OS.GetCmdlineArgs(), a => a.StartsWith("--battle-tab=", StringComparison.Ordinal));
        if (tabArg is not null && int.TryParse(tabArg["--battle-tab=".Length..], out int tabIdx))
        {
            CallDeferred(nameof(ShowTab), tabIdx);
        }

        // 🔴 片③ 冒烟：`--battle-card=N` ⇒ **真实点击第 N 张我方卡**（验"点单位 ⇒ 锁进详情页"）
        string? cardArg = System.Array.Find(OS.GetCmdlineArgs(), a => a.StartsWith("--battle-card=", StringComparison.Ordinal));
        if (cardArg is not null && int.TryParse(cardArg["--battle-card=".Length..], out int cardSlot))
        {
            CallDeferred(nameof(ShowCardDetail), cardSlot);
        }

        // 🔴 跨场景步进冒烟（`ui_three_screens.md` §3 / `#310`⑦）：每进一个场景消费一步
        SmokeScript.Step(this);
    }
}
