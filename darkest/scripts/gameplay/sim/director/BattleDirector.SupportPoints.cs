using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;

namespace Darkest.Gameplay.Sim.Director;

// ① 来源：从 `BattleDirector.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **支援点 SP 族**（原 `:320-380` 逐字节；M11 ① 预警面第二十一件·第二片）✓
// ② 职责：SP 读数口（`SupportPoints`／`SupportCap`／`SupportRegenPreview`／`SupportCostSkill`／`SupportCostReinforce`）／
//    `TrySpendSupportPoints`（扣点 ＋ `rejected` 记录）／`IsSupportSlotActor`（支援位 5/6 判定）／`TryUseSupportPackForSp`（M7.5 D2 支援包结算 ＋ cap 钳制）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片 · 代码区计数含声明行）：`_supportPoints` x11 ／ `_balance` x5 ／ `_log` x3 ／ `_player` x2；
//    本片成员自引用：`SupportPoints` x6 ／ `SupportCap` x2 ／ `SupportCostSkill` x1 ／ `SupportCostReinforce` x1 ／ `TrySpendSupportPoints` x1 ／
//    `TryUseSupportPackForSp` x1 ／ `IsSupportSlotActor` x1 ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 4 条；`Godot` 零引用 —— 构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
public sealed partial class BattleDirector
{
    // ------------------------------------------------------------------
    // #211（S0）支援点 SP：战斗级资源（全队共享；不挂单位、不吃驱散、不随死亡改变）
    // ------------------------------------------------------------------

    /// <summary>当前支援点（投影只读；UI 不得自行扣点/缓存）。</summary>
    public int SupportPoints => _supportPoints;

    /// <summary>支援点上限。</summary>
    public int SupportCap => _balance.Tuning.SupportPoints.Cap;

    /// <summary>下回合开始的恢复预览（UI 必显 #10：含本回合恢复预览）。</summary>
    public int SupportRegenPreview
        => Math.Min(_supportPoints + _balance.Tuning.SupportPoints.RegenPerRound, _balance.Tuning.SupportPoints.Cap);

    /// <summary>支援位技能消耗。</summary>
    public int SupportCostSkill => _balance.Tuning.SupportPoints.CostSkill;

    /// <summary>增援消耗。</summary>
    public int SupportCostReinforce => _balance.Tuning.SupportPoints.CostReinforce;

    /// <summary>
    /// 扣点（#211）：不足 → 返回 false 并记 `rejected`（调用方负责"被拒不吞行动"）。
    /// 硬提醒②：**每次变动必须写 SupportPointEvent**（UI 数字与统计的唯一来源）。
    /// </summary>
    public bool TrySpendSupportPoints(int cost, string reason)
    {
        if (_supportPoints < cost)
        {
            _log.Append(new SupportPointEvent(0, _supportPoints, "rejected"));
            return false;
        }

        _supportPoints -= cost;
        _log.Append(new SupportPointEvent(-cost, _supportPoints, reason));
        return true;
    }

    /// <summary>该单位是否位于支援位（5/6）——只有支援位技能与增援消耗 SP。</summary>
    public bool IsSupportSlotActor(UnitId actor)
        => _player.UnitAtPosition(actor) is { } pos && _player.Layout.ExtensionSlots.Contains(pos);

    /// <summary>
    /// 🔴 M7.5 D2 / `#268`（架构裁定）：**支援包（`support_pack`，消耗品）的 SP 结算入口**。
    /// · 语义 = **「用库存换资源」**，**不是技能** ⇒ **不消耗行动**（支援位已受 SP 成本 + Pass −5 士气双约束）；
    /// · 🔴 **SP 是战斗级资源（`blueprint` §9.11），只有本处一个写入口** —— 远征层只当**库存**扣物品，
    ///      加值一律由本方法结算（否则出现两套 SP 台账：本项目已多次栽在"双源"上）；
    /// · 🔴 **加到 cap 后必须钳制**（P19 ⑧；cap 来自 `tuning.support_points.cap`）；
    /// · **每次变动必写 `SupportPointEvent(reason:"item")`**（UI 数字与 ⑲ 归因的唯一来源）。
    /// </summary>
    public bool TryUseSupportPackForSp(int amount = 2, string reason = "item")
    {
        if (amount <= 0)
        {
            return false;
        }

        int before = _supportPoints;
        _supportPoints = Math.Min(_supportPoints + amount, SupportCap); // 🔴 钳 cap（P19 ⑧）
        _log.Append(new SupportPointEvent(_supportPoints - before, _supportPoints, reason));
        return true;
    }

}
