// ① 来源：从 `ExpeditionFlow.RoomInteractions.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **Curio 效果应用**（`ApplyCurioEffect` 的 12 分支 switch，原 `:240-389` 逐字节；第十六件·第四片）✓
// ② 职责：资源类（food/firewood/gold/support_pack/morale_team）走会话 · `light`/`scout` 走流程层持有者（含 `D-3`/`D-6`
//    侦察落格＋隐藏房回报）· 圣坛 `damage_buff` · `disease_one`/`trait_positive`（随机 ⇒ 各写 `RngDraw`；缺目录/全员已有均留痕）·
//    未登记 kind ⇒ **抛异常**（加载期就该炸 · 红线 21）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_log` x17 · `_rng` x5 · `_session` x3 · `_map` x2 · `_meter` x2 · `_currentRoomId`/`_lootSeq`/`_scout` 各 x1
//    ＋ 主类成员 `Collect`/`StepsDone`/`LastScout`/`TileWalk`/`TileWalkEnabled`/`RevealScoutedRooms`/`RevealSecretsWithinRooms` ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪：`EffectEvent` 全限定，但 `RngDraw` 走 `Darkest.Core.Events` ⇒ 保留；`System` 供 `Math.Max`/`InvalidOperationException`）✓

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
    /// <summary>施加一种 Curio 效果（`light`/`scout` 走流程层持有者；其余走会话）。</summary>
    private void ApplyCurioEffect(string kind, int amount, Roster? roster = null,
        Darkest.Data.SanitariumConfig? diseases = null)
    {
        switch (kind)
        {
            case "none":
                break;
            case "food":
            case "firewood":
            case "gold":
                _session.Gain(_log, kind, amount, "curio");
                break;
            case "support_pack":
                // 🔴 策划 #329/#332：补给箱（空手）给**支援包** ⇒ 与战利品**同一条收取路径**
                //    （包满 ⇒ 进"待处理"队列，**不静默丢弃**）✓ 让"用支援包"那条已接好的链路真正可达 ✓
                for (int i = 0; i < Math.Max(1, amount); i++)
                {
                    Collect(new InventoryItem(ItemKind.SupportPack, $"curio_supply_{StepsDone}_{_lootSeq++}"));
                }

                break;
            case "morale_team":
                _session.ApplyTeamMorale(_log, amount, "curio");
                break;
            case "light":
                _meter.TryAdvanceBy(_log, amount, "curio"); // 🔴 流程层持有光照计
                break;
            case "scout":
                // 🔴 `D-3`（2026-09-20）：侦察的**段级**结果必须落到**格级**表示上（否则"侦察了但地图没变"）⚠️
                //    · `Scouting.Roll` 决定"这次侦察成不成功 / 揭示哪个节点的类型"（**它的口径是真值**）✓
                //    · 走格开启时，**额外**把"当前房间沿线可见的房间格"标为 `Scouted`（暗 + 亮轮廓）——
                //      这是 `D-3` 三态里中间态的**唯一生产来源** ✓
                //    ⚠️ **不重写距离口径**：借用 `MapScouting.RevealWithin`（既有段级口径、按房间）再经
                //       `RevealScoutedRooms` 落到格上 ⇒ 只有**一份**"能看多远"的真值（纪律 `#325` D6）✓
                //    ⚠️ 未开走格 ⇒ `RevealScoutedRooms` 明确忽略（有 `ScoutedTileCount` 可自证）✓
                LastScout = _scout.Roll(_log, _rng, _meter.Value, "curio");
                if (LastScout.Success && _map is not null && TileWalkEnabled)
                {
                    int depth = TileWalk is { } tw
                        ? Math.Max(1, tw.Segments.Count > 0 ? Math.Max(1, tw.TrunkSegments) : 1)
                        : 1;
                    IReadOnlyList<MapRoom> seen = MapScouting.RevealWithin(_map, _currentRoomId, depth);
                    RevealScoutedRooms(seen.Select(r => r.Id));
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        $"grid_scout_reveal:{depth}", 100.0, true));

                    // 🔴🔴 `D-6`（2026-09-20）：**侦察的"非信息类"回报** —— 隐藏房。
                    //    `D-3` 的三态揭示给的是**信息**（地图上多出亮轮廓），但那**不改变玩家收益**
                    //    ⇒ 若侦察只给信息，玩家的最优解是**永不侦察**（省光照）⇒ 整条链路沦为装饰 ⚠️
                    //    DD 的口径（`§F3d`）：隐藏房**地图上不显示**，**只有侦察成功**才变成可进的 rewards 房。
                    //    🔴 **不重写距离口径**：仍用上面**同一批** `RevealWithin` 的结果（`#325` D6）✓
                    //    🔴 未配置 `secrets` ⇒ `RevealSecrets` 直接返回 0（不揭示、不给钱、不写日志）✓
                    int found = RevealSecretsWithinRooms(seen.Select(r => r.Id));
                    if (found > 0)
                    {
                        _log.Append(new Darkest.Core.Events.EffectEvent(default,
                            $"secret_scout_found:{found}", 100.0, true));
                    }
                }

                break;
            case "damage_buff":
                // 🔴 圣坛（`curio.md` §3 #5）：**本趟 +N% 伤害，到扎营** —— 跨场祝福（取大）+ 扎营清 ✓
                _session.GrantCurioDamageBlessing(amount);
                _log.Append(new Darkest.Core.Events.EffectEvent(default,
                    $"curio_damage_blessing:{amount}", 100.0, true));
                break;
            case "disease_one":
                // 🔴 骸骨堆（`curio.md` §3 #6）：**一人患病** —— 走既有 `Roster.Infect`（与回城患病同一通道）
                //    ⚠️ 受害者是**随机**的 ⇒ **必须写 `RngDraw`**（红线：随机留痕）；
                //    疾病种类取目录第一条（**确定性**，已记档：契约只写"一人患病"，未指定病种）
                if (roster is null || diseases is null || diseases.Diseases.Count == 0)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_disease_no_catalog", 0.0, Triggered: false)); // 不静默：缺目录就留痕
                    break;
                }

                string[] candidates = roster.Heroes.Select(h => h.Id).ToArray();
                if (candidates.Length == 0)
                {
                    break;
                }

                int pick = _rng.NextInt(0, candidates.Length);
                _log.Append(new RngDraw(_rng.DrawCount, pick)); // 🔴 随机留痕
                string victim = candidates[pick];
                string diseaseId = diseases.Diseases[0].Id;
                bool infected = roster.Infect(_log, victim, diseaseId, "curio");
                _log.Append(new Darkest.Core.Events.EffectEvent(default,
                    $"curio_disease:{victim}:{diseaseId}:{infected}", 100.0, true));
                break;
            case "trait_positive":
                // 🔴 书堆（`curio.md` §3 #4）：25% ⇒ **随机正面特质**
                //    · 目录 = **名册里出现过的特质**（不新增数据文件 ✓）；正/负判定沿用既有口径
                //      （与 `FindLockablePositiveTrait` 同一判据：`DamagePct > 0 || MoraleDamagePct < 0`）
                //    · **两处随机**（谁 + 哪个特质）⇒ **各写一条 `RngDraw`**（红线：随机留痕）
                if (roster is null)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_trait_no_roster", 0.0, Triggered: false));
                    break;
                }

                Darkest.Data.HeroTraitConfig[] pool = roster.Heroes
                    .SelectMany(h => h.Traits)
                    .Where(t => t.DamagePct > 0 || t.MoraleDamagePct < 0)
                    .GroupBy(t => t.Id)
                    .Select(g => g.First())
                    .ToArray();
                if (pool.Length == 0)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_trait_no_catalog", 0.0, Triggered: false));
                    break;
                }

                // 🔴 只在**合法对**（该英雄**还没有**的特质）里选 —— 否则会选到已有的 ⇒ `AddTrait` no-op
                //    ⇒ **分支静默无效果**（我实测踩到：seed=17 时没人涨特质）⚠️
                var pairs = new List<(string Hero, Darkest.Data.HeroTraitConfig Trait)>();
                foreach (Darkest.Data.HeroConfig h in roster.Heroes)
                {
                    var owned = roster.TraitsOf(h.Id).Select(t => t.Id).ToHashSet();
                    foreach (Darkest.Data.HeroTraitConfig t in pool.Where(t => !owned.Contains(t.Id)))
                    {
                        pairs.Add((h.Id, t));
                    }
                }

                if (pairs.Count == 0)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_trait_all_owned", 0.0, Triggered: false)); // 全都有 ⇒ 留痕（不静默）
                    break;
                }

                int pickPair = _rng.NextInt(0, pairs.Count);
                _log.Append(new RngDraw(_rng.DrawCount, pickPair)); // 🔴 随机留痕
                (string who, Darkest.Data.HeroTraitConfig what) = pairs[pickPair];
                bool added = roster.AddTrait(_log, who, what, "curio");
                _log.Append(new Darkest.Core.Events.EffectEvent(default,
                    $"curio_trait:{who}:{what.Id}:{added}", 100.0, true));
                break;
            default:
                // 已登记的阶段二 kind 不会走到这里（上面已提前返回）；走到这里说明数据用了**未登记**kind
                // ⇒ 加载期就该炸（`CuriosConfig.Parse`）⇒ 这里也不静默：
                throw new InvalidOperationException($"Curio 效果 kind \"{kind}\" 没有消费通道（红线 21）。");
        }
    }
}
