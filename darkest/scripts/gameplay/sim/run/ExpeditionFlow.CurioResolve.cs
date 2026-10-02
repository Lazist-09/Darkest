// ① 来源：从 `ExpeditionFlow.RoomInteractions.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **Curio 结算 · 走开 · 夜袭判定**（`RollAmbush`/`ResolveCurio`/`LeaveCurio`，原 `:143-238` 逐字节；第十六件·第三片）✓
// ② 职责：`D-7` act-out 门禁（折磨 ⇒ **被迫空手**；门禁必须在**掷骰之前** —— 算晚会让随机流与「接受」错位）
//    ＋ 空手/道具两条解析路（`CurioResolver`）＋ 阶段二 kind 显式 `Deferred`（红线 21）＋ 效果转发 `ApplyCurioEffect` ✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_log` x9 · `_rng` x3 · `_session` x2 · `_tuning` x1
//    ＋ 主类属性 `LastActOut*`/`LastCurio*`（本片写 · UI 读）与 `ApplyCurioEffect`（`.CurioEffects` 声明）✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪：本片无 `List<`/`Math`/`RngDraw` 实体引用 ⇒ 只留 `Events` ＋ `Data`）✓

using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
    /// <summary>夜袭判定（扎营后调用；触发则插一场额外战斗，计入完成）。</summary>
    public bool RollAmbush() => _session.RollAmbush(_log, _rng);

    /// <summary>
    /// 🔴 **Curio 结算**（`doc/modules/curio.md` / `#313`）—— 流程层负责它持有的两种 kind：
    /// `light`（`LightMeter`）与 `scout`（`Scouting`）；其余走会话（资源/士气）。
    /// · `itemUsed is null` ⇒ **空手**（内核掷骰 + 写 `RngDraw`）
    /// · 否则 ⇒ **道具直查**（不掷骰）；数据没定义该道具 ⇒ 返回 `null`（调用方应拒绝，V7）
    /// · 命中**阶段二 kind** ⇒ **不施加效果**，以 `LastCurioDeferred = true` 显式告知（红线 21，不静默）
    /// · **走开**走 <see cref="LeaveCurio"/>（零变化）
    /// </summary>
    public CurioOutcome? ResolveCurio(Darkest.Data.CurioConfig curio, string? itemUsed,
        Roster? roster = null, Darkest.Data.SanitariumConfig? diseases = null)
    {
        // 🔴🔴 `D-7`（2026-09-20）：**探索层 act-out 门禁**（DD ⑩ / `curio.md §1.2 ⑤`）。
        //
        //   DD 原文：带**折磨**的英雄会「**自动空手碰**」某些 Curio（**绝不用道具**）
        //     ⇒ 本刀落成「**拒绝用道具、被迫空手**」（不是"什么都不做"）。
        //
        //   🔴 **为什么必须挂在这里（最前面）**：
        //      · 若挂在 `ResolveBare` **之后**，被拒的道具请求已经消耗掉一次 `RngDraw` ⇒ 随机流与"接受"不同步
        //        （`D-4` 踩过同源坑：门禁算晚了 ⇒ 恒 `Consumed` ⇒ 整个机制**静默失效**）⚠️
        //      · 本门禁只**改写** `itemUsed`（拒绝 ⇒ 传 `null` 走下去，即**被迫空手**），
        //        **不**新增随机抽取、**不**改 `CurioResolver` 的任何纪律（它仍只管"解析"）✓
        //
        //   🔴 **判据在会话**（`LowestSurvivorMorale`：只算存活者、取最低者 —— 折磨是**个体**状态）✓
        //   🔴 **未配置 `exploration` ⇒ 一次都不判**（`RollCurioRefuse` 内部直接返回 `None`，不掷不写）✓
        if (_tuning.DungeonLayer?.Exploration is { } actOut
            && _session.LowestSurvivorMorale() is { } lowestMorale)
        {
            ActOutRollResult refused = ExplorationActOut.RollCurioRefuse(_log, _rng, actOut, lowestMorale);
            if (refused.Refused)
            {
                // 🔴 **拒绝必须有文案事件**（红线 21：绝不静默失败）—— 与 `curio_deferred` 同族的手法 ✓
                _log.Append(new EventNodeResolvedEvent(curio.Id, "act_out_refuse",
                    $"curio_use_item_refused:morale={lowestMorale};item={itemUsed ?? "none"}"));
                _log.Append(new EffectEvent(default, "act_out_curio_refuse", 100.0, Triggered: true));
                LastActOutRefused = true;
                LastActOutText = refused.Text;
                ActOutCurioRefuseCount++;
                itemUsed = null; // 🔴 **被迫空手**：往下走空手路径（照常掷骰、照常承担代价）✓
            }
            else
            {
                LastActOutRefused = false;
                LastActOutText = null;
            }
        }
        else
        {
            LastActOutRefused = false;
            LastActOutText = null;
        }

        CurioOutcome? outcome = itemUsed is null
            ? CurioResolver.ResolveBare(curio, _rng, _log)
            : CurioResolver.ResolveItem(curio, itemUsed);
        if (outcome is null)
        {
            LastCurioDeferred = false;
            LastCurioText = null;
            return null; // 该道具对此 Curio 未定义 ⇒ 拒绝（V7：UI 不该列它）
        }

        LastCurioDeferred = outcome.Deferred;
        LastCurioText = outcome.Text;
        if (outcome.Deferred)
        {
            // 🔴 阶段二：**显式不生效**（写可审计事件 + UI 标注"未接线"）
            _log.Append(new Darkest.Core.Events.EffectEvent(default,
                $"curio_deferred:{curio.Id}:{outcome.Kind}", 0.0, Triggered: false));
            _log.Append(new EventNodeResolvedEvent(curio.Id, outcome.Route, $"deferred:{outcome.Kind}"));
            return outcome;
        }

        ApplyCurioEffect(outcome.Kind, outcome.Amount, roster, diseases);
        if (outcome.ExtraKind is { } extra && outcome.ExtraAmount != 0)
        {
            ApplyCurioEffect(extra, outcome.ExtraAmount, roster, diseases);
        }

        _log.Append(new EventNodeResolvedEvent(curio.Id, outcome.Route,
            $"{outcome.Kind}:{outcome.Amount}" +
            (outcome.ItemUsed is null ? string.Empty : $";item:{outcome.ItemUsed}")));
        return outcome;
    }

    /// <summary>🔴 **走开**（V4）：零变化、不掷骰、不阻塞 —— 只写一条"未交互"事件 + 文本。</summary>
    public CurioOutcome LeaveCurio(Darkest.Data.CurioConfig curio)
    {
        CurioOutcome outcome = CurioResolver.Leave(curio);
        LastCurioDeferred = false;
        LastCurioText = outcome.Text;
        _log.Append(new EventNodeResolvedEvent(curio.Id, "leave", "none:0"));
        return outcome;
    }
}
