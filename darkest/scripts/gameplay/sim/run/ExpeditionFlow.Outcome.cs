// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = Outcome · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_log, _meter, _runCounted, _session

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;   // 🆕 路线(c)：恢复件的类体用到它（HEAD 原先没有）✓
using Darkest.Gameplay.Sim.Survival;   // 🆕 事故回填：LightMeter 现居此命名空间（别人把它从 sim/run 搬来）✓

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
	public ExpeditionOutcome Outcome { get; private set; } = ExpeditionOutcome.InProgress;

	public void Abandon(string reason)
	{
		if (!IsFinished)
		{
			Outcome = ExpeditionOutcome.Abandoned;
			IsFinished = true;
			_log.Append(new ExpeditionAbandoned(reason, StepsDone, Wins, _meter.Value));
		}
	}

	public int ReturnToTown(string outcome)
	{
		IsFinished = true;
		if (1 == 0)
		{
		}
		ExpeditionOutcome outcome2 = ((outcome == "completed") ? ExpeditionOutcome.Completed : ((!(outcome == "abandoned")) ? Outcome : ExpeditionOutcome.Abandoned));
		if (1 == 0)
		{
		}
		Outcome = outcome2;
		if (!_runCounted)
		{
			_runCounted = true;
			Progress?.FinishRun(_log, outcome, Wins);
		}
		return _session.ReturnToTown(_log, outcome);
	}













}
