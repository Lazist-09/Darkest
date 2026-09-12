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
    string Status,
    int CurrentBattleIndex = 0,
    double CurrentDifficultyMultiplier = 1.0,
    string? CurrentDifficultyRange = null,
    double? NextDifficultyMultiplier = null,
    string? NextDifficultyRange = null)
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
        ExpeditionSession session, TuningCamp camp, int targetBattles,
        IReadOnlyList<TuningDifficultyTier>? difficultyTiers = null)
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
            affordable, pathTypes, ambushes, retreats, session.Survivors, status,
            CurrentBattleIndex: session.BattlesPlayed + 1,
            CurrentDifficultyMultiplier: MultiplierFor(difficultyTiers, session.BattlesPlayed + 1),
            CurrentDifficultyRange: RangeFor(difficultyTiers, session.BattlesPlayed + 1),
            NextDifficultyMultiplier: NextMultiplierFor(difficultyTiers, session.BattlesPlayed + 1),
            NextDifficultyRange: NextRangeFor(difficultyTiers, session.BattlesPlayed + 1));
    }

    /// <summary>第 N 场的难度档（#250；缺省 1.0）。</summary>
    public static double MultiplierFor(IReadOnlyList<TuningDifficultyTier>? tiers, int battleIndex)
        => tiers?.FirstOrDefault(t => battleIndex >= t.BattleFrom && battleIndex <= t.BattleTo)?.Multiplier ?? 1.0;

    /// <summary>第 N 场所处的档位区间文本（UI 必显 ⑧）。</summary>
    public static string? RangeFor(IReadOnlyList<TuningDifficultyTier>? tiers, int battleIndex)
    {
        TuningDifficultyTier? t = tiers?.FirstOrDefault(x => battleIndex >= x.BattleFrom && battleIndex <= x.BattleTo);
        return t is null ? null : $"第 {t.BattleFrom}~{t.BattleTo} 场";
    }

    /// <summary>**后续难度预告**（UI 必显 ⑧：三档"可预告"优于线性递增）。</summary>
    public static double? NextMultiplierFor(IReadOnlyList<TuningDifficultyTier>? tiers, int battleIndex)
    {
        TuningDifficultyTier? cur = tiers?.FirstOrDefault(t => battleIndex >= t.BattleFrom && battleIndex <= t.BattleTo);
        if (tiers is null || cur is null)
        {
            return null;
        }

        TuningDifficultyTier? next = tiers.Where(t => t.BattleFrom > cur.BattleTo).OrderBy(t => t.BattleFrom).FirstOrDefault();
        return next?.Multiplier;
    }

    /// <summary>后续档位的场序区间（UI 必显 ⑧）。</summary>
    public static string? NextRangeFor(IReadOnlyList<TuningDifficultyTier>? tiers, int battleIndex)
    {
        TuningDifficultyTier? cur = tiers?.FirstOrDefault(t => battleIndex >= t.BattleFrom && battleIndex <= t.BattleTo);
        if (tiers is null || cur is null)
        {
            return null;
        }

        TuningDifficultyTier? next = tiers.Where(t => t.BattleFrom > cur.BattleTo).OrderBy(t => t.BattleFrom).FirstOrDefault();
        return next is null ? null : $"第 {next.BattleFrom}~{next.BattleTo} 场";
    }

    /// <summary>事件流复算是否与会话持有值一致（UI 数字可信性校验；不一致即事件流缺口）。</summary>
    public static bool Reconciles(ExpeditionViewState view, ExpeditionSession session)
        => view.Firewood == session.Firewood && view.Food == session.Food;

    /// <summary>
    /// **最小列表式远征界面**（E2/E3 UI，`expedition.md` §5.5 必显 7 条）——产出 7 行文本，
    /// Godot 面板只需逐行渲染（**数字全部来自本投影 = 事件流**）。
    /// ① 资源 ② 进度 ③ 6 人 HP/士气 ④ 节点类型+代价 ⑤ 扎营入口（柴火不足灰显） ⑥ 事件二选一（不许跳过） ⑦ 回城结算。
    /// </summary>
    public static IReadOnlyList<string> RenderList(ExpeditionViewState v, ExpeditionSession session,
        int targetBattles, bool ambushTriggered, TuningCamp camp, TownReturnEvent? townReturn = null)
    {
        var lines = new List<string>
        {
            // ① 资源
            $"① 资源：柴火 {v.Firewood}　口粮 {v.Food}",
            // ② 进度
            $"② 进度：第 {Math.Min(session.BattlesPlayed + 1, targetBattles)}/{targetBattles} 场" +
            $"　夜袭{(ambushTriggered ? "已触发" : "未触发")}（累计 {v.Ambushes} 次）",
            // ③ 6 人 HP 条 + 士气条（🔴 跨场累积唯一可感知处）
            "③ 队伍：" + string.Join("　", session.Roster().Select(u =>
                $"{u.Id} {u.Hp}/{u.MaxHp}HP {u.Morale}士气")),
            // ④ 节点类型 + 代价提示
            "④ 本步节点：" + (v.PathNodeTypes.Count == 0 ? "（待选路）"
                : string.Join(" / ", v.PathNodeTypes.Select(t => t == "battle" ? "普通战（有伤亡风险）" : "事件（二选一，有代价）"))),
            // ⑤ 扎营入口 + 柴火余量（不足灰显）
            $"⑤ 扎营：{(v.CanCamp ? $"可扎营（柴火 {v.Firewood} → 剩 {v.Firewood - 1}）" : "灰显：柴火不足")}" +
            $"　Respite {v.RespiteLeft}",
            // ⑥ 事件二选一（不允许跳过）
            "⑥ 事件选项：二选一（**无跳过**）",
            // ⑦ 回城结算面板（士气前后值 + 是否触发惩罚）
            townReturn is null
                ? "⑦ 回城结算：（尚未回城）"
                : $"⑦ 回城结算：{townReturn.Outcome}　士气 {townReturn.MoraleBefore} → {townReturn.MoraleAfter}" +
                  $"（**完全不恢复** #245）　惩罚{(townReturn.PenaltyApplied ? "已触发" : "未触发")}",
        };

        // 灰显依据（档位不足列出禁用项，供 UI 画灰）
        lines.Add("④b 食物档位：" + string.Join("　", FoodTiers.Select(t =>
            $"{t}{(v.IsFoodTierDisabled(t) ? "（灰显：口粮不足）" : $"（需 {ExpeditionCampMath.FoodRequired(camp.FoodTiers, t, session.Survivors)}）")}")));

        // ⑧ 难度档位（v0.92 必显：**当前档位 + 后续难度预告**；乘数施加对象待 O-70 ① 裁定，暂未施加）
        lines.Add(v.NextDifficultyMultiplier is null
            ? $"⑧ 难度：第 {v.CurrentBattleIndex} 场 ×{v.CurrentDifficultyMultiplier:F2}（{v.CurrentDifficultyRange}）" +
              "　后续：无（末档）　［乘数施加对象待 O-70 ① 裁定 → **当前不施加**］"
            : $"⑧ 难度：第 {v.CurrentBattleIndex} 场 ×{v.CurrentDifficultyMultiplier:F2}（{v.CurrentDifficultyRange}）" +
              $"　后续预告：×{v.NextDifficultyMultiplier:F2}（{v.NextDifficultyRange}）　［待 O-70 ① 裁定 → **当前不施加**］");
        return lines;
    }
}
