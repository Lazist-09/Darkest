using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 远征层的 **UI 读模型**（E2/E3 UI 的唯一数据来源）：
/// 资源 / 可扎营 / Respite / 可选食物档位（**不足的档位不在列 = 灰显依据**）/ 路径节点类型 / 夜袭次数 / 状态。
/// 🔴 纪律：**UI 显示的每个数字都必须能从事件流复算** —— 故本投影只吃 `CombatLog` + 起手值。
/// </summary>
public sealed record ExpeditionViewState(
    int Firewood,
    int Food,
    bool CanCamp,
    int RespiteLeft,
    IReadOnlyList<string> AffordableFoodTiers,
    IReadOnlyList<string> PathNodeTypes,
    int Ambushes,
    int Retreats,
    int SurvivingUnits,
    string Status)
{
    /// <summary>该档位是否灰显（口粮不足）。</summary>
    public bool IsFoodTierDisabled(string tier) => !AffordableFoodTiers.Contains(tier);
}

/// <summary>远征读模型投影（纯函数；不含 Godot 依赖）。</summary>
public static class ExpeditionProjector
{
    /// <summary>四档固定顺序（UI 展示顺序；灰显由 AffordableFoodTiers 决定）。</summary>
    public static readonly IReadOnlyList<string> FoodTiers = new[] { "feast", "full", "half", "starve" };

    /// <summary>
    /// 从**事件流**复算资源（起手值 + 所有 ResourceChangedEvent，**拒绝事件不入账**），
    /// 并组装 UI 读模型。若复算值与会话持有值不一致，说明事件流有缺口（UI 不可信）。
    /// </summary>
    public static ExpeditionViewState Project(CombatLog log, int startFirewood, int startFood,
        ExpeditionSession session, TuningCamp camp, int targetBattles)
    {
        int firewood = startFirewood;
        int food = startFood;
        foreach (ResourceChangedEvent e in log.Events.OfType<ResourceChangedEvent>())
        {
            if (e.Reason == "rejected")
            {
                continue; // 🔴 P20 ②：拒绝不入账（UI 数字同样不算它）
            }

            if (e.Kind == "firewood")
            {
                firewood += e.Delta;
            }
            else
            {
                food += e.Delta;
            }
        }

        var affordable = new List<string>();
        foreach (string tier in FoodTiers)
        {
            int need = ExpeditionCampMath.FoodRequired(camp.FoodTiers, tier, session.Survivors);
            if (need <= food)
            {
                affordable.Add(tier);
            }
        }

        var pathTypes = log.Events.OfType<PathChosenEvent>().Select(e => e.NodeType).ToList();
        int ambushes = log.Events.OfType<AmbushTriggeredEvent>().Count();
        int retreats = log.Events.OfType<TownReturnEvent>().Count(e => e.PenaltyApplied);
        string status = log.Events.OfType<TownReturnEvent>().Any(e => e.Outcome == "completed") ? "completed"
            : log.Events.OfType<TownReturnEvent>().Any() ? "returned"
            : session.BattlesPlayed >= targetBattles ? "path_done" : "ongoing";

        return new ExpeditionViewState(firewood, food, session.CanCamp, session.RespiteLeft,
            affordable, pathTypes, ambushes, retreats, session.Survivors, status);
    }

    /// <summary>事件流复算是否与会话持有值一致（UI 数字可信性校验；不一致即事件流缺口）。</summary>
    public static bool Reconciles(ExpeditionViewState view, ExpeditionSession session)
        => view.Firewood == session.Firewood && view.Food == session.Food;
}
