using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Skill;
using Darkest.UI;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// BattleRoot：主战斗场景组合根（blueprint §5d / T-M5-05）——装配 + 游玩状态机。
/// 游玩循环（与 headless 同源，blueprint §10 镜像）：StartTurn（回合钩子+行动序列重掷）
/// → NextActor 逐个出列：我方 → 等待玩家输入（技能/换位/撤退）；敌方 → 自动 EnemyAct；
/// 队列空 → 下回合。全部事件写内核 CombatLog；UI 只读投影 + 命令门面。
/// </summary>
public partial class BattleRoot : Node2D, IBattleView
{
    public BattleDirector Director { get; private set; } = null!;
    public BattleProjector Projector { get; private set; } = null!;

    private Darkest.Core.Events.CombatLog? _xpLog;   // 🆕 升级通道：常驻经验日志（事件可审计 ✓）
    private Darkest.Data.RosterConfig? _rosterCfgForXp;   // 🆕 A10 升级读数：懒解析一次（只读，不重算数字）✓
    private RngProvider _rng = null!;

    /// <summary>
    /// 🆕 **敌方意图预览（显示用）** —— 给表现层的**唯一口子**：
    /// 🔴 **不需要（也不该要）战斗 RNG**：`BattleProjector.IntentPreview` 的实现里写着
    ///    `_ = battleRng; // 只读：战斗用 RNG 一律不参与预览` ⇒ 预览**绝不消耗抽数** ⇒ 确定性不变 ✓
    ///    （若把 `_rng` 泄给表现层，迟早有人用它"顺手预览" ⇒ **抽数被吃 ⇒ 回放/复现全崩** ⚠️）
    /// ⇒ 这里传一个**固定种子的预览专用 RNG**，与战斗抽数完全隔离 ✓
    /// </summary>
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // 🔴 **`#327` 片 3 第一步：宿主侧的【流程驱动口】**（`tasks/seamless_single_scene.md` 片 3 = 我的域）
    //    现状：流程驱动（`Advance`/`OnBattleFinished`）写在 `ExpeditionRoot`（UI 侧，片 2 期间归他）
    //    ⇒ 片 3 的终态是"**由宿主（本文件）驱动流程**"，但那需要**场景内切战斗**（片 4 同批）⚠️
    //    ⇒ 本步只做**加法**：把"宿主能驱动流程"的口开出来并**如实报告**，
    //      不改现有生命周期（远征场景仍负责切场景）⇒ 现有游戏行为**零变化** ✓
    //    📌 判据（红线 25）：这一步**不是**片 3 完成 —— 只是"口开在这里、由谁调用"已定 ✓
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>🔴 片 3：当前是否有**远征流程**可驱动（没有 ⇒ 单场战斗模式，一切谓词/面板都不适用）✓</summary>
    public Darkest.Gameplay.Sim.Run.ExpeditionFlow? ExpeditionFlowOrNull
        => ExpeditionContext.IsActive ? ExpeditionContext.Flow : null;

    /// <summary>
    /// 🔴 片 3：宿主侧**推进流程一步**（地图模式下"选路"的落点）。
    /// 返回 = 这一步是否为**战斗步骤**（若是，片 4 会在这里**场景内切战斗**，而不是切场景）✓
    /// </summary>
    public bool MapModeAdvance(int optionIndex)
    {
        Darkest.Gameplay.Sim.Run.ExpeditionFlow? flow = ExpeditionFlowOrNull;
        if (flow is null)
        {
            return false;
        }

        Darkest.Gameplay.Sim.Run.FlowStep step = flow.Advance(optionIndex);
        bool isBattle = step.Kind == Darkest.Gameplay.Sim.Run.FlowStepKind.Battle;
        GD.Print($"[片3] 宿主驱动流程：Advance({optionIndex}) ⇒ 步骤 {step.Kind}（节点 {step.NodeId}）" +
                 (isBattle ? "　🔴 战斗步骤：**片 4 起在本场景内切战斗**（当前仍由远征场景切场景）✓" : string.Empty));
        return isBattle;
    }
    public Darkest.Gameplay.Sim.Director.IntentProjection PreviewIntent(Darkest.Core.Contracts.UnitId actor)
        => Projector.IntentPreview(actor, _previewRng, enabled: true);

    /// <summary>预览专用 RNG（固定种子；**只喂预览**，永不参与战斗抽数）✓</summary>
    private readonly RngProvider _previewRng = new(20260915);
    private BattleUI _ui = null!;
    private SkillsConfig _skills = null!;
    private bool _awaitingPlayer;
    private UnitId _activeActor = new("-");
    private bool _gameOver;
    private long _seed = 20260909L;
    private string? _pendingSkill; // 实机单体选一：选定技能后等待玩家点目标
    private int _reinforcePhase;   // 增援两步（#181）：0=无 1=选B(支援位) 2=选X(战斗位)
    private int _reinforceB;

    /// <summary>增援两步阶段（0 none / 1 选B / 2 选X）与已选 B 槽（UI 高亮与点击路由）。</summary>
    public int ReinforcePhase => _reinforcePhase;
    public int ReinforceB => _reinforceB;

    /// <summary>🔴 片 3.1：**是否正在进行一场战斗**（`Director` 只在 `NewGame()` 后非空）⇒ 战斗专用路径必须先问它 ✓</summary>
    public bool HasActiveBattle => Director is not null;

    private bool _noBattleNoticeShown; // 只提示一次（防刷屏）✓

    public override void _UnhandledInput(InputEvent e)
    {
        // 🔴 动作化（附 B ①）：`dd_restart` 见 `project.godot [input]`（玩家可重映射）
        if (e.IsAction("dd_restart"))
        {
            if (_dungeonHostedInScene)
            {
                // 🔴 片 3.1：**地牢模式**下 `R` **不重开单场战斗**（否则又会"进地牢就起战斗"）
                GD.Print("[片3.1] 地牢模式：`R` 不重开单场战斗（要重开请退出本趟）✓");
                return;
            }

            NewGame();
            GD.Print($"[BattleRoot] 重开（seed={_seed}）");
            return;
        }

        // F0（#189）：Esc / 右键取消选目标（不消耗行动）—— Esc 走**引擎内置** `ui_cancel` ✓
        bool cancel = e.IsAction("ui_cancel")
                      || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
        if (cancel && (_pendingSkill is not null || _reinforcePhase != 0))
        {
            _pendingSkill = null;
            _reinforcePhase = 0;
            _ui.FlashHint("已取消选择（不消耗行动）");
        }
    }

    /// <summary>
    /// 🔴🔴 **`#327` 片 4：在【本场景内】进入地牢**（组装流程 + 切地图模式，**不切场景**）
    /// 组装交给 `ExpeditionComposition.BuildInScene`（从 `ExpeditionRoot` 搬过来的那一段）✓
    /// </summary>
    /// <summary>🔴 片 4 过渡标记：本趟地牢是**宿主内进的**（true）还是**从远征场景进来的**（false）——
    /// 战后"继续"按钮据此分流：宿主内 ⇒ 回地图模式（不切场景）；旧路径 ⇒ 仍回远征场景（**保住旧冒烟循环**）✓</summary>
    private bool _dungeonHostedInScene;

    /// <summary>
    /// 🆕 **回城入口（UI Track 1 · 事项 B）**：`UIRoot` 单外壳优先，**缺失/未就绪就回落**到原来的硬切场景 ✓
    /// 🔴 为什么写成"外壳优先 + 回落"：`UIRoot` 是 UI 域的可选外壳（他们另有 `TryInstantiate()` 回落）⇒
    ///    主程序侧**不假设它一定在**（无 autoload / 未注册 / 场景缺失 ⇒ 都不会崩，且行为与改造前一致 ✓）。
    /// ⚠️ 本方法**只用于玩家路径**；`DungeonRunDriver`（e2e 驱动器）**故意不动** —— 不把测试驱动与玩家路径搅在一起 ✓
    /// </summary>

    /// <summary>把 UI 绑到当前 `Director`（`NewGame` 与"场景内起远征战斗"共用同一条绑定 ⇒ 不会两处各写一份）✓</summary>
    private void BindUi()
    {
        if (_ui is null)
        {
            _ui = GetNode<BattleUI>("UILayer/BattleUI");
        }

        _ui.Bind(host: this, useSkill: (actor, skillId) => DoUseSkill(actor, skillId),
            reinforce: () => OnReinforceClicked(),
            move: () => OnMoveClicked(),
            retreat: () => DoRetreat(),
            pass: () => OnPassClicked());
    }


    public UnitId ActiveActor => _activeActor;

    /// <summary>当前行动者原型 id（技能池/中文名按原型匹配；实例 id 已唯一化）。</summary>
    public string ActiveArchetype
        => Director.Player.UnitsInSlotOrder().FirstOrDefault(u => u.Id == _activeActor)?.ArchetypeId
           ?? Director.Enemy.UnitsInSlotOrder().FirstOrDefault(u => u.Id == _activeActor)?.ArchetypeId
           ?? _activeActor.Value;

    /// <summary>实例 id → 原型 id（行动序列中文名映射用）。</summary>
    public string ArchetypeOf(UnitId id)
        => Director.Player.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id)?.ArchetypeId
           ?? Director.Enemy.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id)?.ArchetypeId
           ?? id.Value;

    public bool IsAwaitingPlayer => _awaitingPlayer;
    public bool GameOver => _gameOver;

    /// <summary>结算面板数据（P0④）：结果文案 / 回合数 / 7 项系统触发计数。</summary>
    public string ResultText { get; private set; } = "";

    public int ResultRound { get; private set; }
    public int[] ResultCounts { get; private set; } = new int[7];
}
