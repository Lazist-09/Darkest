using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **跨场景步进的冒烟器**（`tasks/ui_three_screens.md` §3 / `#310`⑦）——
/// 解决"多场景冒烟只能靠帧数猜"的问题：把冒烟写成**一串显式步骤**，由本类按场景分派执行。
///
/// 机制（两段）：
/// ① **进场景消费一步**：各 Root 在 `_Ready` 末尾调用 <see cref="Step"/>；
/// ② 🔴 **场景内也能推进**：本类会在当前场景挂一个 0.2s 的 `Timer` ⇒ 反复尝试下一步
///    （否则"进房间 ⇒ 面板 ⇒ 下一步"会卡死 —— 我实测踩过一次）。
/// 🔴 **只在"场景匹配"时消费**：不匹配就**等**（不 dequeue），避免把战斗场景的步骤在远征场景里误判/误配；
///    等待有上限（100 次）⇒ 超时**报错退出**（`exit 3`），不静默卡死。
///
/// 用法：`--smoke=<步骤1>,<步骤2>,…`
/// 步骤：`main:N` ／ `map:N` ／ `camp` ／ `skill:N` ／ `finish` ／ `run-full` ／ `town` ／ `auto` ／
///       `curio:bare` ／ `curio:leave` ／ `curio:item:N` ／ `hover:<building>` ／ `row:N` ／ `back` ／
///       `embark` ／ `quit`。
/// 未知步骤 ⇒ **打印并 `exit 2`**（不静默跳过）。
/// </summary>
public static class SmokeScript
{
    private static readonly Queue<string> Steps = new();
    private static int _applied;

    /// <summary>
    /// 🔴 **还有待办步骤吗**（片 4 收口新增）：**同场景内**的模式切换（如"战后回地图模式"）**不会触发新的 `_Ready`**
    /// ⇒ 冒烟步骤会**永远停着** ⚠️（实测：`--smoke=main:1,town` 的 `town` 从未执行）⇒ 由场景侧在转换点**续跑一次** ✓
    /// </summary>
    public static bool HasPending => Steps.Count > 0;
    private static bool _enabled;
    private static int _stepCalls; // 🆕 仪表：`Step` 被调用次数（限流打印用）✓
    private static Node? _owner;
    private static Node? _autoFinishedFor; // 战斗场景"自动放行"只对同一实例触发一次
    private static Timer? _timer;
    private static int _waits;

    public static bool Enabled => _enabled;

    /// <summary>从命令行解析（只解析一次）。</summary>
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

    /// <summary>**尝试执行下一步**（进场景时调用一次；之后由场景内计时器反复调用）。</summary>
    public static void Step(Node node)
    {
        if (!_enabled)
        {
            return;
        }

        EnsureTimer(node);

        // 🔴 **仪表（诊断用 · 限流）**：**每次** `Step` 都打一行（前 24 次）⇒ 直接回答"切场景后它还跑不跑"
        //    用途：定位"步骤登记了却永远不跑"（实测：`town` 在战后回地图模式后一直停着 ⚠️）
        if (_stepCalls < 24)
        {
            _stepCalls++;
            GD.Print($"[冒烟·仪表 #{_stepCalls}] Step　场景={node.GetType().Name}　待办={(Steps.Count == 0 ? "（空）" : Steps.Peek())}" +
                     $"　可执行={(Steps.Count > 0 && Applies(Steps.Peek(), node))} ✓");
        }

        // 🔴 **战斗场景自动放行**：脚本没显式要 `auto` 时，落到战斗场景就自动打完并返回
        //    （否则长链会在"走房间 ⇒ 撞上战斗房"处卡住 —— 脚本无法预知哪个房间是战斗房）。
        //    ⚠️ **每个场景实例只触发一次**：自动打完 ⇒ 场景切换是**延迟**的（下一帧才生效），
        //       若每 0.2s 重复触发，会在同一场战斗里反复"打完"⇒ 实测刷屏且切不出去（我踩过）。
        if (node is BattleRoot battleRoot && (Steps.Count == 0 || Steps.Peek() != "auto"))
        {
            if (_autoFinishedFor != node)
            {
                _autoFinishedFor = node;
                GD.Print("[冒烟] 落到战斗场景且下一步不是 auto ⇒ 自动打完并返回（脚本无需预知房间类型）");
                battleRoot.PressAutoFinish();
                return; // 第一次：先让这场打完；**本帧不消费步骤**（否则会在战斗未结束时就把下一步用掉）✓
            }

            // 🔴🔴 **片 4 收口修（仪表实测抓到，2026-09-16）**：这里原先**无条件 `return`** ⚠️
            //    ⇒ 同一场景实例的**后续每一次** `Step` 都被挡回 ⇒ **战斗之后的步骤永远吃不到**
            //    （实测：`--smoke=main:1,town` 的 `town` 被调用 16+ 次、`可执行=True`，却**从未执行** ✓）
            //    ⇒ 现在：**已放行过**就**不再 return** ⇒ 落到下面正常消费待办步骤 ✓
            GD.Print("[冒烟] 本场已放行过 ⇒ **不再挡住后续步骤**（修：此前无条件 return）✓");
        }

        if (Steps.Count == 0)
        {
            // 🔴 片 4 收口修（实测抓到）：`--e2e` / `--hamlet-next` / `--topology-auto` **有自己的生命周期**
            //    （`--e2e` 要跨"地牢 → 回城 → 再出发"三个阶段）⇒ 冒烟步骤走完**不得自动退出**
            //    ⚠️ 原行为：走完即 `Quit()` ⇒ **阶段2 的切场景被退出抢先**（实测：阶段2 永远跑不到）✓
            string[] selfManaged = { "--e2e", "--hamlet-next", "--topology-auto" };
            if (System.Array.Exists(OS.GetCmdlineArgs(), a => System.Array.IndexOf(selfManaged, a) >= 0))
            {
                GD.Print($"[冒烟] ✅ 全部 {_applied} 步已执行完 ⇒ **不自退**（该 CLI 有自己的生命周期，交给它）✓");
                return;
            }

            GD.Print($"[冒烟] ✅ 全部 {_applied} 步已执行完 ⇒ 退出");
            node.GetTree().Quit();
            return;
        }

        string step = Steps.Peek();
        if (!Applies(step, node))
        {
            // 🔴 场景不匹配 ⇒ **等**（不消费）。这是"场景内推进"与"跨场景"共存的关键。
            _waits++;
            if (_waits > 600)
            {
                GD.Print($"[冒烟] 🔴 等待「{step}」超时（{_waits} 次）—— 当前场景 {node.Name}／{node.GetType().Name}" +
                         $" ⇒ 报错退出（不静默卡死）");
                node.GetTree().Quit(exitCode: 3);
            }

            return;
        }

        Steps.Dequeue();
        _applied++;
        _waits = 0;
        GD.Print($"[冒烟] 第 {_applied} 步：{step}（场景 {node.Name} / {node.GetType().Name}）");
        Apply(step, node);
    }

    /// <summary>该步骤**是否属于当前场景**（不匹配则等，不消费）。</summary>
    private static bool Applies(string step, Node node) => step switch
    {
        "quit" => true,
        "main:0" or "main:1" or "main:2" => node is Darkest.Ui.MainMenuRoot,
        "map:0" or "map:1" or "map:2" or "camp" or "skill:0" or "skill:1" or "skill:2" or "finish"
            or "run-full" or "town" or "curio:bare" or "curio:leave" or "curio:item:0" or "curio:item:1"
            // 🔴 片 4 收尾：旧远征场景已退休 ⇒ 这些步骤现在落在**宿主**（`map:*` 已接宿主；
            //    其余由 `HostStepNotWired` **如实停步** ⇒ 绝不允许"因为不匹配而静默跳过" ✓
            => node is BattleRoot,
        "auto" => node is BattleRoot,
        "hover:tavern" or "hover:abbey" or "hover:stagecoach" or "row:0" or "row:1" or "row:2"
            or "back" or "embark" => node is Darkest.Ui.HamletRoot,
        _ => true, // 未知步骤 ⇒ 交给 Apply 报错退出
    };

    private static void EnsureTimer(Node node)
    {
        if (_owner == node && _timer is not null && GodotObject.IsInstanceValid(_timer))
        {
            return;
        }

        _owner = node;
        var t = new Timer
        {
            Name = "SmokeTick",
            WaitTime = 0.2,
            OneShot = false,
            Autostart = true,
        };
        t.Timeout += () => Step(node);
        node.AddChild(t);
        _timer = t;
    }

    private static void Apply(string step, Node node)
    {
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
                // 🔴 片 4④：**宿主内**（`BattleRoot`）的行走步骤 —— 选第 N 条出路；若该步是战斗步骤 ⇒ **场景内起战斗** ✓
                if (node is Darkest.Gameplay.Scene.BattleRoot hostMap)
                {
                    if (hostMap.MapModeAdvance(step[^1] - '0'))
                    {
                        hostMap.StartExpeditionBattleInScene();
                    }

                    break;
                }

                HostStepNotWired("PressMapRoom");
                break;
            case "camp":
                HostStepNotWired("PressCampAndMaybeRouteToBattle");
                break;
            case "skill:0":
            case "skill:1":
            case "skill:2":
                HostStepNotWired("PressCampSkill");
                break;
            case "finish":
                HostStepNotWired("PressFinishCamp");
                break;
            case "curio:bare":
                HostStepNotWired("PressCurioBare");
                break;
            case "curio:leave":
                HostStepNotWired("PressCurioLeave");
                break;
            case "curio:item:0":
            case "curio:item:1":
                HostStepNotWired("PressCurioButton");
                break;
            case "run-full":
                HostStepNotWired("RunFullSmokeStep");
                break;
            case "retreat":
                // 🔴 §11 的冒烟步骤（`retreat.md §11`）：撤退 ⇒ **回地图当前格继续走**（不是结束本趟）
                // ⚠️ 必须走**真实 `Pressed`**（红线 18）⇒ 但 UI 侧目前**没有公共入口**（`PressAbandon` 等都是 private）
                //    ⇒ 该步骤**登记在案、如实停步**；已投请求给 UI：请暴露公共入口（或加 `--retreat` 旗标，与 `--abandon` 同形）✓
                HostStepNotWired("PressRetreat（需 UI 暴露公共入口，或加 --retreat 旗标）");
                break;

            case "town":
                // 🔴 片 4 收尾：`town` = **结算回城**（纯流程 + 切场景，**不依赖 UI 面板**）⇒ 旧场景退休后已接上 ✓
                if (node is BattleRoot townHost && Darkest.Gameplay.Scene.ExpeditionContext.Flow is { } townFlow)
                {
                    Darkest.Gameplay.Scene.DungeonRunDriver.ReturnToTown(townHost, townFlow);
                }
                else
                {
                    HostStepNotWired("PressReturnToTown");
                }

                break;
                break;
            // 🔴 片 4④：**宿主内的行走步骤**（`dungeon` = 进地牢；`map:N` = 选第 N 条出路 ⇒ 战斗步骤自动起战斗）✓
            case "dungeon":
                if (node is Darkest.Gameplay.Scene.BattleRoot br0)
                {
                    br0.EnterDungeonInScene();
                }

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

    /// <summary>
    /// 🔴 **片 4 收尾：旧远征场景已退休** ⇒ 那些"只在旧场景里有实现"的冒烟步骤改为**诚实的停步**：
    /// 打印"尚未在宿主侧接线"并停下（红线 21：**不静默、也不假装跑过**）✓
    /// 📌 宿主侧的对应步骤属**片 2 尾部**（扎营/Curio/选路面板搬进 `BattleUi` 之后）⇒ 届时在此接上 ✓
    /// </summary>
    private static void HostStepNotWired(string step)
        => GD.Print($"[冒烟] 🔴 步骤「{step}」尚未在宿主侧接线（属片 2 尾部：扎营/Curio/选路面板）⇒ 如实停下 ✓");

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
        if (node is BattleRoot root)
        {
            root.PressAutoFinish(); // 真实"自动打完 + 继续（回远征）"
            return;
        }

        GD.Print("[冒烟] 🔴 auto 步骤需要战斗场景 ⇒ 停下");
    }
}
