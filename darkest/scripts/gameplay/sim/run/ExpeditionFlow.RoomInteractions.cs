// 🔴 从 ExpeditionFlow.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）——
//    本文件 = **房间交互**（拾取/待拾取重试/Curio 挑选·结算·离开·效果应用/扎营开合）· 只搬家、零行为改动 ✓
// ⚠️ 2026-10-02 拆分（M11 ① 预警面第十六件）：原 485 行 ⇒ 4 片 —— 本片 = **拾取族 ＋ 扎营开合 ＋ 状态属性**；
//    Curio 挑选 ⇒ `.CurioPick.cs` · 结算/走开 ⇒ `.CurioResolve.cs` · 效果应用 ⇒ `.CurioEffects.cs`（上两行原头注保留为历史留痕）✓
using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Survival;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
    /// <summary>背包界面在"选择丢弃"流程里调用：把一件补给收进背包。</summary>
    public bool TryCollectLoot(InventoryItem item) => Collect(item);

    /// <summary>收取一件补给（按类型进背包并同步会话计数）。</summary>
    public bool TryCollectLoot(ItemKind kind) => Collect(new InventoryItem(kind, $"loot_{StepsDone}_{_lootSeq++}"));

    /// <summary>把"待处理"补给再尝试收一次（玩家腾出格子后调用）。</summary>
    public bool RetryPendingLoot()
    {
        while (_pendingLoot.Count > 0)
        {
            InventoryItem next = _pendingLoot.Peek();
            if (!Collect(next))
            {
                return false;
            }

            _pendingLoot.Dequeue();
        }

        return true;
    }

    private bool Collect(InventoryItem item)
    {
        if (!_bag.TryAdd(item, out string reason))
        {
            if (reason == "full_choose_discard")
            {
                _pendingLoot.Enqueue(item); // 🔴 不静默丢：交由玩家选择丢弃哪一格
            }

            return false;
        }

        // 与远征会话的资源计数保持同步（背包是物品来源；会话计数用于资源收支读数）
        if (item.Kind == ItemKind.Firewood)
        {
            _session.Gain(_log, "firewood", 1, "loot");
        }
        else if (item.Kind == ItemKind.Food)
        {
            _session.Gain(_log, "food", 1, "loot");
        }

        return true;
    }

    /// <summary>最近一次 Curio 的**描述文本**（V6；供 UI 显示）。</summary>
    public string? LastCurioText { get; private set; }

    /// <summary>最近一次 Curio 是否命中**阶段二（未接线）**分支（UI 必须据此标注，红线 21）。</summary>
    public bool LastCurioDeferred { get; private set; }

    /// <summary>
    /// 🔴 `D-7`：最近一次 Curio 是否**被折磨拒绝用道具**（⇒ 已**被迫空手**）。
    /// UI 据此提示"他不是不想用，是**用不了**"（红线 21：不静默）✓
    /// </summary>
    public bool LastActOutRefused { get; private set; }

    /// <summary>🔴 `D-7`：上面那次的**文案**（`null` = 没被拒）✓</summary>
    public string? LastActOutText { get; private set; }

    /// <summary>🔴 `D-7` 读数：本趟**累计**被折磨拒绝用道具的次数（供冒烟/日志自证）✓</summary>
    public int ActOutCurioRefuseCount { get; private set; }

    /// <summary>扎营（柴火不足 ⇒ 拒绝；成功则光照回满）。**最小版：一调用到底**（供测试/旧路径）。</summary>
    public bool Camp()
    {
        if (!BeginCamp())
        {
            return false;
        }

        return FinishCamp();
    }

    /// <summary>
    /// 🔴 **拆开扎营（阶段一→二）**：`StartCamp`（扣柴火 + 给 Respite 点数）+ 选口粮 + 光照回满。
    /// 拆开的理由（红线 18/21）：**扎营技能必须让玩家【点得到】** —— 原 `Camp()` 是一调用到底的，
    /// UI 没有插"选技能"的位置 ⇒ 6 个已接线的扎营技能玩家永远碰不到 ⚠️
    /// 返回 false ⇒ 柴火不足（拒绝、不扣）。
    /// </summary>
    public bool BeginCamp()
    {
        if (!_session.StartCamp(_log, StepsDone, _tuning.Camp!.RespiteBase))
        {
            return false;
        }

        _meter.OnCamp(_log);
        string best = _session.CanAffordFood(_tuning.Camp, "feast") ? "feast"
            : _session.CanAffordFood(_tuning.Camp, "full") ? "full"
            : _session.CanAffordFood(_tuning.Camp, "half") ? "half" : "starve";
        _session.ChooseFood(_log, _tuning.Camp, best);
        return true;
    }

    /// <summary>
    /// 🔴 **结束扎营（阶段二→三）**：`EndCamp` ＋【阶段三：夜袭判定】（契约 `m7_expedition.md:143`）。
    /// 触发夜袭 ⇒ `LastCampAmbushed = true` ⇒ **本方法立刻经 `BeginAmbushBattle` 把额外战斗开出来** ✓
    /// <para>返回 `true` = 收营完成（`AmbushBattle` 非空 ⇒ 调用方应切进战斗场景）；</para>
    /// <para>`AmbushBattle is null` ⇒ 未触发 / 已插过 ⇒ **照常继续走图** ✓</para>
    /// </summary>
    public bool FinishCamp()
    {
        _session.EnterPhase(FlowPhase.Walking); // 🔴 收营 ⇒ 回【走图】相位 ✓
        _session.EndCamp(_log);
        LastCampAmbushed = RollAmbush();

        // 🔴 `#305`／契约 `m7_expedition.md:143` 的**生产接线**（2026-09-20）：
        //    此前 `FinishCamp` 只把 `LastCampAmbushed` 置真、**从不真的插战斗** ⇒
        //    `BeginAmbushBattle` 是死函数，而"扎营被夜袭"这个机制**在流程上从未发生** ⚠️
        //    ⇒ 这里触发即刻开战（`IsAmbush: true` ⇒ `OnBattleFinished` 的守卫会放行，因为不是节点步骤）✓
        //    ⚠️ **不在这里切场景**：切场景归宿主（`BattleRoot`）—— 内核只把"有一场夜袭要打"交出去 ✓
        AmbushBattle = LastCampAmbushed ? BeginAmbushBattle(_log) : null;

        // 🔴 `D-5`（2026-09-20）：**扎营后重置饥饿缓冲** —— DD 原文：
        //    "The party also gains this buffer when starting an expedition or **camping**" ✓
        //    ⚠️ 此时 `TileWalk` 未开启（线性模式）⇒ 缓冲留待 `EnableTileWalk` 按 `buffer_at_start` 初始化，
        //    这里的重置只对"已经开着走格又扎了营"的场景生效 ✓
        ResetHungerBuffer();
        return true;
    }

    /// <summary>
    /// 🔴 `D-5`：把饥饿缓冲重置为 `hunger.buffer_at_start`（**开局**与**扎营后**两处调用）。
    /// 未配置 `hunger` ⇒ 置 0（等价于不启用）✓
    /// </summary>
    public void ResetHungerBuffer()
        => _hungerBuffer = _tuning.DungeonLayer?.Hunger?.BufferAtStart ?? 0;

    /// <summary>
    /// 🔴 **本次扎营触发的夜袭战斗**（`null` = 未触发 / 已插过）—— 供宿主决定"是否切进战斗场景" ✓
    /// 由 `FinishCamp` 在夜袭判定为真时**立即开出**（`#305` 契约：夜袭战斗**计入胜场**）✓
    /// </summary>
    public Darkest.Gameplay.Sim.Director.BattleDirector? AmbushBattle { get; private set; }

    /// <summary>上一次扎营后是否触发夜袭（`#305`：触发 ⇒ 调用方插一场额外战斗，计入胜场）。</summary>
    public bool LastCampAmbushed { get; private set; }

}
// ⚠️ 以下为 2026-10-02 拆分时的旧 footer（原文保留为历史留痕；各片真实依赖见各片头注 ③ 行）——
//    🔴 本片（主片）实测：_bag x1 · _hungerBuffer x1 · _log x7 · _lootSeq x1 · _meter x1 · _pendingLoot x4 · _session x9 · _tuning x6（`_rng`/`_scout` 已随 Curio 三片迁出）✓
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_bag x1 · _log x30 · _lootSeq x2 · _meter x3 · _pendingLoot x4 · _rng x9 · _scout x1 · _session x13 · _tuning x5
