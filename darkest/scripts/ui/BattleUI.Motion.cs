using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求按职责切）✓
/// ② 本文件 = **战斗 · 动效与审计族**（新事件→动效、命中/士气崩溃动效、卡牌定位、支援包入口、骨架/动效审计读数）✓
/// ③ 🔴 依赖主类私有成员/状态：`_motionLayer`/`_vignette`/`_seenEvents`/`_cards`/`_portraits`/`_host`/`_skeletonAtBind`/`_bindCount` ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : CanvasLayer
{
    /// <summary>
    /// 🔴 `ui_spec §12.1` ① ② ③：**只对【新事件】播动效**（事件流 = 唯一事实来源，不另造状态）：
    /// 伤害 ⇒ 受击（抖动闪白）+ 上浮伤害数字；治疗 ⇒ 上浮绿色数字；进死门 ⇒ 士气崩溃（暗角 + 单位框红）✓
    /// ⚠️ 动效**不改任何玩法状态**（只写 `Modulate`/`Position`）⇒ 不吞输入、不延迟可操作时刻（`#321`⑤）✓
    /// </summary>
    private void PlayMotionFromNewEvents(BattleDirector d, BattleProjector p)
    {
        if (_motionLayer is null)
        {
            return;
        }

        IReadOnlyList<BattleEvent> events = d.Log.Events;
        if (events.Count < _seenEvents)
        {
            _seenEvents = 0; // 重开/换局 ⇒ 归零（事件流被重建）
        }

        for (int i = _seenEvents; i < events.Count; i++)
        {
            switch (events[i])
            {
                case DamageEvent { Target: { } dt, Amount: > 0 } dmg:
                    bool targetIsPlayer = IsPlayerUnit(dt, p);
                    PlayHitMotion(dt, $"-{dmg.Amount}",
                        dmg.Axis == "mental" ? Darkest.UI.DdTheme.Mental : Darkest.UI.DdTheme.Danger, p);
                    // 🔴 `§12.2` ① 命中（**区分我/敌**）+ ② 受击：
                    //    打敌人 ⇒ 我方命中音（高音方波）；**敌方打出** ⇒ 敌方命中音（低音方波）**＋** 我方受击音（噪声）
                    //    ⚠️ 这让 `HitEnemy` 有真实触发点（红线 21：**枚举项没有触发点 = 死声明**）；
                    //       若策划认为"敌方打出"只该有一种音，删掉其中一条即可（口径待确认，已投窗口）
                    if (targetIsPlayer)
                    {
                        Darkest.UI.UiSfx.Play(Darkest.UI.UiSfx.Kind.HitEnemy);
                        Darkest.UI.UiSfx.Play(Darkest.UI.UiSfx.Kind.Hurt);
                    }
                    else
                    {
                        Darkest.UI.UiSfx.Play(Darkest.UI.UiSfx.Kind.HitAlly);
                    }

                    break;
                case HealEvent { Target: { } ht, Amount: > 0 } heal:
                    PlayHitMotion(ht, $"+{heal.Amount}", Darkest.UI.DdTheme.Hp, p);
                    break;
                case DeathDoorEvent { Unit: { } dd }:
                    PlayMoraleCrashMotion(dd, p);
                    Darkest.UI.UiSfx.Play(Darkest.UI.UiSfx.Kind.DeathDoor); // ② 死门
                    break;
                case DeathEvent { Unit: { } dead }:
                    Darkest.UI.UiSfx.Play(Darkest.UI.UiSfx.Kind.Death);     // ② 阵亡
                    UiMotion.ScreenFlash(_vignette, Darkest.UI.UiMotion.DeathFlash, Darkest.UI.UiMotion.MoraleSeconds); // 🔴 §12.3 闪白（整屏）
                    PlayMoraleCrashMotion(dead, p);
                    break;
            }
        }

        _seenEvents = events.Count;
    }

    private void PlayHitMotion(UnitId unitId, string text, Color color, BattleProjector p)
    {
        if (FindCard(unitId, p) is not { } found)
        {
            return;
        }

        UiMotion.Hit(found.Card);
        UiMotion.FloatText(_motionLayer, found.TextPos, text, color);
    }

    private void PlayMoraleCrashMotion(UnitId unitId, BattleProjector p)
    {
        UiMotion.MoraleCrash(_vignette, FindCard(unitId, p)?.Card);
    }

    /// <summary>把 `UnitId` 映射回它的卡片（下标编排见 `BuildBattlefield`：我方 4 → 敌方 4 → 支援 2）。</summary>
    private (Control Card, Vector2 TextPos)? FindCard(UnitId unitId, BattleProjector p)
    {
        for (int side = 0; side < 2; side++)
        {
            foreach (UnitProjection u in p.Units(player: side == 0))
            {
                if (u.UnitId != unitId.Value)
                {
                    continue;
                }

                int idx = u.IsPlayer ? (u.Slot >= 5 ? 8 + (u.Slot - 5) : 4 - u.Slot) : 4 + (u.Slot - 1);
                if (idx < 0 || idx >= _cards.Count)
                {
                    continue;
                }

                Control card = _cards[idx].card;
                Vector2 textPos = card.GlobalPosition - _motionLayer.GlobalPosition + new Vector2(12, -4); // 两 Control 的全局坐标之差 = 层内局部坐标
                return (card, textPos);
            }
        }

        return null;
    }

    /// <summary>某单位是否属于我方（音效/动效按阵营分岔用）。</summary>
    private static bool IsPlayerUnit(UnitId unitId, BattleProjector p)
        => p.Units(player: true).Any(u => u.UnitId == unitId.Value);

    /// <summary>
    /// 🔴 **使用支援包**（主程序清单"等界面接线"第 1 条）：**扣 1 个支援包 ⇒ +SP**。
    /// 口径：**扣格与加 SP 都由内核决定**（`Inventory.TryUseSupportPack` / `BattleDirector.TryUseSupportPackForSp`
    /// 的参数由其默认值给 ⇒ **UI 不写死 2**）；UI 只做"入口 + 如实报告" ✓
    /// ⚠️ 无背包（单场战斗没有远征流程）⇒ **置灰 + tooltip 说明原因**（红线 21：不留不可解释的禁用）✓
    /// </summary>
    public bool PressSupportPack()
    {
        if (_host?.Director is null)
        {
            GD.Print("[UI 支援包] 无战斗导演 ⇒ 拒绝（如实报）");
            return false;
        }

        Darkest.Gameplay.Sim.Run.Inventory? bag = Darkest.Gameplay.Scene.ExpeditionContext.Flow?.Bag;
        if (bag is null)
        {
            GD.Print("[UI 支援包] 本场没有背包（单场战斗无远征流程）⇒ 无支援包可用（按钮置灰，红线 21）");
            return false;
        }

        if (!bag.TryUseSupportPack(out Darkest.Gameplay.Sim.Run.InventoryItem? used))
        {
            GD.Print("[UI 支援包] 背包里没有支援包 ⇒ 拒绝，不扣任何东西（红线 21：不部分扣）");
            return false;
        }

        int before = _host.Director.SupportPoints;
        bool ok = _host.Director.TryUseSupportPackForSp(); // 🔴 数量由内核默认值给（不在 UI 写死）
        GD.Print($"[UI 支援包] 已用 {used?.Kind.ToString() ?? "支援包"} ⇒ SP {before} → {_host.Director.SupportPoints}" +
                 $"（内核受理={ok}）　背包剩余 {bag.Slots.Count}/{bag.SlotCap}");
        Refresh(); // 顶栏 SP 与背包读数都由投影刷新（UI 不自己算）
        return true;
    }

    /// <summary>🔴 供冒烟：**真实点击【用支援包】**（走与玩家完全相同的 `Pressed` 路径）✓</summary>
    public void PressSupportPackButton()
    {
        GD.Print("[UI 支援包] 发出真实 Pressed（用支援包）");
        _supportButton.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// 🔴 `#327` **S1 的可断言读数**（"无缝"的可测定义）：骨架节点（背景 / 队伍区 / E 区 / 右下角地图宿主）的实例 id。
    /// 判据形态：**进战斗前后这些 id 不变 ⇒ 无缝**；变了 ⇒ 说明发生了场景切换或整体重建 ✗
    /// ⚠️ **现状如实报**：`Build()` 目前一次性全建 ⇒ 每次 `Bind()` 这些 id 都会变 —— 这正是迁移（片 1/2）的目标 ✓
    /// </summary>
    public string SkeletonAudit()
        => $"S1 骨架存活读数：根={IdOf(_uiRoot)} 背景={IdOf(_bg)} 顶栏={IdOf(_topRow)} 主体={IdOf(_midRow)} 底栏={IdOf(_bottomRow)} 地图({IdOf(_mfMap)})" +
           "（**进战斗前后应相同**；当前每次 Bind 会重建 ⇒ 迁移目标）";

    private static string IdOf(Node? n) => n is null || !GodotObject.IsInstanceValid(n)
        ? "—"
        : $"{n.Name}#{n.GetInstanceId()}";

    /// <summary>🔴 `§12.1` 的**取证**（冒烟打印）：动效播了几次 ／ 运行中几次 ／ **输入为什么不会被吞** ——
    /// 除了常量读数，还实测两件结构事实：动效层 `MouseFilter == Ignore`、且全屏**没有任何控件**被改成非继承 `ProcessMode`。
    /// </summary>
    public string MotionAudit()
    {
        bool ignore = _motionLayer is not null && _motionLayer.MouseFilter == Control.MouseFilterEnum.Ignore;
        int frozen = CountFrozenProcessMode(_uiRoot);
        return $"{UiMotion.Audit()}　动效层鼠标穿透实测={(ignore ? "✅ Ignore" : "🔴 会拦鼠标")}　" +
               $"被冻结 ProcessMode 的控件={frozen}（应为 0）";
    }

    private static int CountFrozenProcessMode(Node? root)
    {
        if (root is null)
        {
            return 0;
        }

        int n = root is Control { ProcessMode: not Node.ProcessModeEnum.Inherit } ? 1 : 0;
        foreach (Node child in root.GetChildren())
        {
            n += CountFrozenProcessMode(child);
        }

        return n;
    }
}
