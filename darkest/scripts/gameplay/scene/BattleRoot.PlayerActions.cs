// 🔴 从 BattleRoot.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）——
//    本文件 = **玩家操作与战斗驱动**（场景内起战斗/新局/过牌/用技能/增援/移动/点卡/撤退）· 只搬家、零行为改动 ✓
using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Darkest.UI;
using Godot;

namespace Darkest.Gameplay.Scene;

public partial class BattleRoot : Node2D
{
    /// <summary>
    /// 🔴🔴 **`#327` 片 3 主体：宿主在【本场景内】起一场【远征战斗】**
    ///    —— 这是"不再切场景"的核心一步：director 由**流程**给（`BeginExpeditionBattle` ⇒ 难度/光照/夜袭全按流程），
    ///    projector 用**同一套 data** 现造（不能借用单场那套 ⇒ 否则投影与 director 不匹配 ⚠️）。
    ///    返回 false = 无远征流程（单场战斗模式）⇒ **不改任何东西** ✓
    /// </summary>
    public bool StartExpeditionBattleInScene()
    {
        Darkest.Gameplay.Sim.Run.ExpeditionFlow? flow = ExpeditionFlowOrNull;
        if (flow is null)
        {
            GD.Print("[片3] 无远征流程 ⇒ 不起远征战斗（单场战斗模式，行为不变）✓");
            return false;
        }

        DirectorBridge.DirectorHandle support = DirectorBridge.BuildFromRes(this); // 取与远征同源的 balance/skills ✓
        Darkest.Core.Events.CombatLog log = ExpeditionContext.Log ?? new Darkest.Core.Events.CombatLog();
        int index = flow.Session.BattlesPlayed + 1;

        Director = flow.Session.BeginExpeditionBattle(index, log, flow.Tuning.Expedition.DifficultyTiers);
        Projector = new Darkest.Gameplay.Sim.Director.BattleProjector(
            Director, support.Balance, support.Skills, new Darkest.Gameplay.Sim.Skill.SkillRuntimeState());
        _skills = support.Skills;
        _rng = new RngProvider(++_seed);
        _awaitingPlayer = false;
        _gameOver = false;
        _activeActor = new("-");
        _pendingSkill = null;
        _reinforcePhase = 0;
        BindUi();
        _ui!.ExitMapMode(); // 进战斗 ⇒ 离开地图模式（片 4 会在战斗结束回地图模式）✓
        flow.Session.EnterPhase(Darkest.Gameplay.Sim.Run.FlowPhase.Battle);
        GD.Print($"[片3] ✅ **场景内起远征战斗**：第 {index} 场　Phase={flow.Session.Phase}　" +
                 $"CanShowCampUi={flow.Session.CanShowCampUi}（应为 False）✓");
        return true;
    }
    private void NewGame()
    {
        var handle = DirectorBridge.BuildFromRes(this);
        Director = handle.Core;
        Projector = handle.Projector;
        _skills = handle.Skills;
        _rng = new RngProvider(++_seed);
        _awaitingPlayer = false;
        _gameOver = false;
        _activeActor = new("-");
        _pendingSkill = null;
        _reinforcePhase = 0;

        if (_ui is null)
        {
            _ui = GetNode<BattleUI>("UILayer/BattleUI");
        }

        BindUi();
    }

    /// <summary>S5.2 待命：显式结束该单位本次行动（不消耗 SP、不算技能、不结算任何效果）。</summary>
    public void OnPassClicked()
    {
        if (!_awaitingPlayer)
        {
            return;
        }

        Director.PassTurn(_activeActor);
        _pendingSkill = null;
        _reinforcePhase = 0;
        _awaitingPlayer = false; // 放弃本次行动
        _ui.FlashHint($"{_ui.ArchetypeNameOf(_activeActor)} 待命（不消耗支援点）");
    }

    public override void _Process(double delta)
    {
        _ = delta;

        // 🔴🔴 **片 3.1 连带修**（实测抓到刷屏：24143 行 / 2000 条 NRE ⚠️）：
        //    **没有活动战斗时**（= 地牢/地图模式）`Director` 仍是 `null` ⇒ 战斗专用路径**不得**在此解引用 ✓
        if (!HasActiveBattle)
        {
            if (!_noBattleNoticeShown)
            {
                _noBattleNoticeShown = true;
                GD.Print("[片3.1] 无活动战斗 ⇒ `_Process` 早退（地图模式由地图/UI 驱动）✓");
            }

            return;
        }
        if (_gameOver)
        {
            _ui.Refresh(status: "按 R 重开（新 seed）");
            return;
        }

        // P0/O-47：每轮推进先查结果——任一方归零立即收束（不区分先后，不再多打）
        BattleOutcome outcome = Director.Outcome;
        if (outcome == BattleOutcome.Victory)
        {
            EndGame("胜利：敌方全灭");
            return;
        }

        if (outcome == BattleOutcome.Defeat)
        {
            EndGame("失败：我方全灭");
            return;
        }

        if (_awaitingPlayer)
        {
            _ui.Refresh(status: $"回合 {Director.Round} · 轮到 {_activeActor}");
            return; // 等玩家输入
        }

        // 非玩家阶段（回合开始 / 敌方行动）自动推进
        UnitId? actor = Director.NextActor();
        if (actor is null)
        {
            // 本回合队列耗尽（结果已在顶部检查）→ 下回合
            Director.StartTurn(_rng);
            _ui.Refresh(status: $"回合 {Director.Round} 开始");
            return;
        }

        UnitRuntime? playerUnit = FindPlayerUnit(actor.Value);
        if (playerUnit is not null)
        {
            _awaitingPlayer = true;
            _activeActor = actor.Value;
            _ui.Refresh(status: $"回合 {Director.Round} · 轮到 {_activeActor}");
            return;
        }

        Director.EnemyAct(actor.Value, _rng); // 敌方自动
        _ui.Refresh(status: $"回合 {Director.Round}");
    }

    private void DoUseSkill(UnitId actor, string skillId)
    {
        if (!_awaitingPlayer || actor != _activeActor)
        {
            return;
        }

        // F2（#193）恐惧 proc：拒放时技能灰掉、不消耗行动、须重选
        if (Director.TryFearRefusal(actor, _rng))
        {
            _ui.FlashHint("恐惧发作：技能被拒绝（不消耗行动，请重选）");
            return;
        }

        SkillTemplateConfig skill = _skills.Get(skillId);
        int[] candidates = SkillTargetResolver.Resolve(skill, actor, Director.Player, Director.Enemy).ToArray();

        // F0（#189）：一律进入选目标——候选池非空就必须点卡确认（单体/AOE/team/self/any_ally 无例外）
        if (candidates.Length == 0)
        {
            _ui.FlashHint("没有合法目标（技能应已灰显）");
            return;
        }

        if (_pendingSkill == skillId)
        {
            _pendingSkill = null; // 再点同一技能 = 取消（不消耗行动）
            _ui.FlashHint("已取消选择");
            return;
        }

        _pendingSkill = skillId;
        _reinforcePhase = 0;
        _ui.FlashHint($"请选择目标：点亮 {candidates.Length} 张卡（点任意亮卡确认，Esc 取消）");
    }

    /// <summary>「增援」按钮（#181 两步）：未开始 → 选 B（高亮支援位）；再点 → 取消。</summary>
    public void OnReinforceClicked()
    {
        if (!_awaitingPlayer)
        {
            return;
        }

        _pendingSkill = null;
        _reinforcePhase = _reinforcePhase == 0 ? 1 : 0;
        _ui.FlashHint(_reinforcePhase == 1 ? "增援：先选支援位 B（点 5/6 槽）" : "增援已取消");
    }

    /// <summary>「移动」按钮（#180 常驻）：当前战斗位行动者 → 进入移动目标选择（move_range 候选高亮）。</summary>
    public void OnMoveClicked()
    {
        if (!_awaitingPlayer)
        {
            return;
        }

        int pos = Director.Player.UnitAtPosition(_activeActor) ?? -1;
        if (pos is < 1 or > 4)
        {
            _ui.FlashHint("仅战斗位（1~4）可移动");
            return;
        }

        string moveId = "move"; // F1（#191）：池外通用移动技能（距离从单位 move_distance 读）
        SkillTemplateConfig move = _skills.Get(moveId);
        if (SkillTargetResolver.Resolve(move, _activeActor, Director.Player, Director.Enemy).Count == 0)
        {
            _ui.FlashHint("移动：周围无可交换位置");
            return;
        }

        _pendingSkill = moveId;
        _reinforcePhase = 0;
        _ui.FlashHint("选择移动目标（交换位置）");
    }

    /// <summary>卡片点击（UI 回调）：增援两步（#181）优先；否则单体/移动选一（#178/#180）。</summary>
    public void OnCardClicked(int slot, bool isPlayer)
    {
        // 🔴 `ui_spec` §1.1：**点击单位 ⇒ 锁定到 E 区多功能框（详情页）** —— 任何时刻都能看（只读）
        //    ⚠️ 必须在 `_awaitingPlayer` 早退**之前**做，否则"非我方回合时点单位看详情"会被吞掉
        _ui.ShowUnitDetail(slot, isPlayer);

        if (!_awaitingPlayer)
        {
            return;
        }

        // 增援两步路由
        if (_reinforcePhase == 1)
        {
            if (slot is 5 or 6 && Director.Player.UnitRuntimeAt(slot) is not null && !isPlayer == false)
            {
                _reinforceB = slot;
                _reinforcePhase = 2;
                _ui.FlashHint($"增援：已选支援位 {slot}，再选目标战斗位 X（点 1~4）");
            }
            else
            {
                _ui.FlashHint("增援：请点支援位 5/6 中被占用的槽");
            }

            return;
        }

        if (_reinforcePhase == 2)
        {
            if (slot is >= 1 and <= 4)
            {
                bool ok = Director.Reinforce(_activeActor, _reinforceB, slot);
                _ui.FlashHint(ok ? $"增援完成（{_reinforceB} → {slot}）" : "增援被拒（槽位/此回合已增援）");
                if (ok || Director.SwappedThisRound)
                {
                    _awaitingPlayer = false; // 发起者消耗本次行动
                }
            }
            else
            {
                _ui.FlashHint("增援：目标 X 须为战斗位 1~4");
            }

            _reinforcePhase = 0;
            return;
        }

        // 单体/移动选一
        if (_pendingSkill is null)
        {
            return;
        }

        string skillId = _pendingSkill;
        int[] candidates = SkillTargetResolver.Resolve(_skills.Get(skillId), _activeActor, Director.Player, Director.Enemy).ToArray();
        if (Array.IndexOf(candidates, slot) < 0)
        {
            _ui.FlashHint("该目标不在候选中，请点候选卡");
            return;
        }

        ExecutePlayerSkill(_activeActor, skillId, new[] { slot });
    }

    /// <summary>单体选一阶段的候选槽（UI 高亮用）。</summary>
    public int[] PendingCandidates
    {
        get
        {
            if (_pendingSkill is null)
            {
                return Array.Empty<int>();
            }

            return SkillTargetResolver.Resolve(_skills.Get(_pendingSkill), _activeActor, Director.Player, Director.Enemy).ToArray();
        }
    }

    public bool IsTargeting => _pendingSkill is not null;

    /// <summary>待选目标是否在敌方侧（高亮分阵营用：敌方技能=true；any_ally/move 等友方=false）。</summary>
    public bool PendingTargetsEnemy
        => _pendingSkill is not null && _skills.Get(_pendingSkill).Target.Side == "enemy";

    private void ExecutePlayerSkill(UnitId actor, string skillId, int[]? chosen)
    {
        _pendingSkill = null;
        _reinforcePhase = 0;
        Director.PlayerUseSkill(actor, skillId, _rng, chosen);
        _awaitingPlayer = false;
        GD.Print($"[BattleRoot] {actor} 使用 {skillId}" + (chosen is not null ? $" → 槽 {chosen[0]}" : ""));
    }

    private void DoRetreat()
    {
        if (_gameOver)
        {
            return;
        }

        bool success = Director.PlayerRetreat(_rng);
        if (success || Director.IsBattleOver)
        {
            EndGame(success ? "撤退成功" : "撤退失败");
        }
        else
        {
            _ui.Refresh(status: $"回合 {Director.Round} · 撤退失败，本回合不可再试");
        }
    }

}
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_activeActor x14 · _awaitingPlayer x13 · _gameOver x4 · _noBattleNoticeShown x2 · _pendingSkill x16 · _reinforceB x3 · _reinforcePhase x13 · _rng x7 · _seed x2 · _skills x7 · _ui x25
