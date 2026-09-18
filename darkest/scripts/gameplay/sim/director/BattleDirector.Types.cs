// 🔴 从 BattleDirector.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）：本文件 = 对外类型与决策记录 —— 顶层类型 => **零 partial、零行为改动**
using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Gameplay.Sim.Enemy;
using Darkest.Gameplay.Sim.Pipeline;
using Darkest.Gameplay.Sim.Skill;
using Darkest.Gameplay.Sim.Survival;
using Darkest.Gameplay.Sim.Turn;

namespace Darkest.Gameplay.Sim.Director;

public enum BattleOutcome { Ongoing, Victory, Defeat }

/// <summary>玩家行动决策（actor 节拍回调产物）：技能 / 增援两步（ReinforceB+ReinforceX）/ 目标指定（互斥）。</summary>
public sealed record PlayerDecision(string? SkillId, int? ReinforceB, int? ReinforceX, int? SkillTargetSlot = null)
{
    public static PlayerDecision None => new(null, null, null);
    public static PlayerDecision Skill(string id, int? target = null) => new(id, null, null, target);
    public static PlayerDecision Reinforce(int slotB, int slotX) => new(null, slotB, slotX);
}
