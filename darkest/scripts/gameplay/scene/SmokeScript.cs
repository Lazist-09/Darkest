using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **跨场景步进的冒烟器**（`tasks/ui_three_screens.md` §3 / `#310`⑦）——
/// 解决"多场景冒烟只能靠帧数猜"的问题：把冒烟写成**一串显式步骤**，
/// 每进入一个场景（各 Root 的 `_Ready` 末尾）就**消费下一步**，用完即退出。
///
/// 用法：`--smoke=<步骤1>,<步骤2>,…`（逗号分隔；空步骤忽略）
/// 支持的步骤（都走**真实 `Pressed`**，红线 26）：
///   · `main:0/1/2`      主菜单三选一（单场战斗 / 出发远征 / 回城）
///   · `map:N`           地图视图：真实点击第 N 个可选房间（0 = 第一个）
///   · `camp`            真实点击"扎营"（阶段一→二；若触发夜袭 ⇒ 切战斗）
///   · `skill:N`         真实点击第 N 个扎营技能
///   · `finish`          真实点击"结束扎营"（阶段三：夜袭判定）
///   · `auto`            战斗场景：用"自动玩家"打完本场 + 自动点【继续（回远征）】
///   · `town`            真实点击"回城（完成本趟）" ⇒ 切 Hamlet
///   · `hover:<building>` Hamlet：悬停某栋建筑（真读库存）
///   · `row:N`           Hamlet：真实点击第 N 行名册 ⇒ 打开角色详情
///   · `back`            Hamlet：关闭角色详情（回城池）
///   · `embark`          Hamlet：真实点击 Embark（再出发）
///   · `run-full`        Expedition：跑完整趟的一步（等价于既有 `--run-full`）
///   · `quit`            立即退出（用于收口）
/// 未知步骤 ⇒ **打印并停下**（不静默跳过 —— 否则冒烟会"看起来过了"）。
/// </summary>
public static class SmokeScript
{
    private static readonly Queue<string> Steps = new();
    private static int _applied;
    private static bool _enabled;

    public static bool Enabled => _enabled;

    /// <summary>从命令行解析（在 MainMenuRoot 最先调用；只解析一次）。</summary>
    public static void InitFromArgs()
    {
        if (_enabled)
        {
            return;
        }

        string? spec = OS.GetCmdlineArgs().FirstOrDefault(a => a.StartsWith("--smoke=", StringComparison.Ordinal));
        if (spec is null)
        {
            return;
        }

        foreach (string s in spec["--smoke=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Steps.Enqueue(s);
        }

        _enabled = true;
        GD.Print($"[冒烟] 步骤驱动已启用：共 {Steps.Count} 步 —— {string.Join(" → ", Steps)}");
    }

    /// <summary>
    /// **消费下一步**（各 Root 在 `_Ready` 末尾调用；`node` 用来按类型分派到该场景的真实点击入口）。
    /// </summary>
    public static void Step(Node node)
    {
        if (!_enabled)
        {
            return;
        }

        if (Steps.Count == 0)
        {
            GD.Print($"[冒烟] ✅ 全部 {_applied} 步已执行完 ⇒ 退出");
            node.GetTree().Quit();
            return;
        }

        string step = Steps.Dequeue();
        _applied++;
        GD.Print($"[冒烟] 第 {_applied} 步：{step}（场景 {node.Name} / {node.GetType().Name}）");

        switch (step)
        {
            case "main:0":
            case "main:1":
            case "main:2":
                PressMainMenu(node, step[^1] - '0');
                break;
            case "map:0":
            case "map:1":
            case "map:2":
                PressExpedition(node, c => c.PressMapRoom(step[^1] - '0'));
                break;
            case "camp":
                PressExpedition(node, c => c.PressCampAndMaybeRouteToBattle());
                break;
            case "skill:0":
            case "skill:1":
            case "skill:2":
                PressExpedition(node, c => c.PressCampSkill(step[^1] - '0'));
                break;
            case "finish":
                PressExpedition(node, c => c.PressFinishCamp());
                break;
            case "run-full":
                PressExpedition(node, c => c.RunFullSmokeStep());
                break;
            case "town":
                PressExpedition(node, c => c.PressReturnToTown());
                break;
            case "auto":
                PressBattle(node);
                break;
            case "hover:tavern":
            case "hover:abbey":
            case "hover:stagecoach":
                PressHamlet(node, c => c.ShowBuildingInfo(step["hover:".Length..]));
                break;
            case "row:0":
            case "row:1":
            case "row:2":
                PressHamlet(node, c => c.PressRosterRow(step[^1] - '0'));
                break;
            case "back":
                PressHamlet(node, c => c.CloseHeroDetail());
                break;
            case "embark":
                PressHamlet(node, c => c.PressEmbark());
                break;
            case "quit":
                GD.Print($"[冒烟] quit 步 ⇒ 共执行 {_applied} 步后退出");
                node.GetTree().Quit();
                break;
            default:
                GD.Print($"[冒烟] 🔴 未知步骤「{step}」⇒ **停下**（不静默跳过；红线 21）");
                node.GetTree().Quit(exitCode: 2);
                break;
        }
    }

    private static void PressMainMenu(Node node, int index)
    {
        if (node is Darkest.Ui.MainMenuRoot root)
        {
            root.PressMenu(index);
            return;
        }

        GD.Print("[冒烟] 🔴 main:N 步骤不在主菜单场景里 ⇒ 停下");
    }

    private static void PressExpedition(Node node, Action<Darkest.Ui.ExpeditionRoot> act)
    {
        if (node is Darkest.Ui.ExpeditionRoot root)
        {
            act(root);
            return;
        }

        GD.Print("[冒烟] 🔴 该步骤需要远征场景 ⇒ 停下");
    }

    private static void PressHamlet(Node node, Action<Darkest.Ui.HamletRoot> act)
    {
        if (node is Darkest.Ui.HamletRoot root)
        {
            act(root);
            return;
        }

        GD.Print("[冒烟] 🔴 该步骤需要 Hamlet 场景 ⇒ 停下");
    }

    private static void PressBattle(Node node)
    {
        if (node is Darkest.Gameplay.Scene.BattleRoot root)
        {
            root.PressAutoFinish(); // 真实"自动打完 + 继续（回远征）"
            return;
        }

        GD.Print("[冒烟] 🔴 auto 步骤需要战斗场景 ⇒ 停下");
    }
}
