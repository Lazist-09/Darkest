// 🔴 从 ExpeditionFlow.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）——
//    本文件 = **房间交互**（拾取/待拾取重试/Curio 挑选·结算·离开·效果应用/扎营开合）· 只搬家、零行为改动 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

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

    /// <summary>
    /// 🔴 **片 C：由【内容表】决定该房间放哪个 Curio**（取代原先的临时确定性映射 `roomId % N`）。
    ///
    /// 读法（`tasks/merged_content_layer_pack.md` §4）：
    /// · `roomType` 行的 `curio_pool` 组成候选池；**支路房**再并入 `branch` 行的池（支路专用覆盖键）✓
    /// · 抽取按**组内权重**（行 `weight`）⇒ 🔴 **写 `RngDraw`**（随机必须留痕：可审计、可复现）✓
    /// · 池为空 ⇒ 返回 `null`（**该房间没有内容** ⇒ 调用方走既有回退，不静默造一个）✓
    /// 🔴 主 = 内容表；`branch_battle_weight` 只是**过渡覆盖项**（默认 0），不得与主混（契约 §3 尾注）。
    /// </summary>
    public string? PickCurioForRoom(Darkest.Data.RoomContentsConfig contents, string roomType, bool isBranch,
        IReadOnlySet<string>? allowedCurios = null)
    {
        // 🔴 `O-86` / C2 消费点 (b) 的**内部门禁**（修一个真缺陷）：
        //    生产调用点（UI）一度**没传** `allowedCurios` ⇒ 未解锁的 Curio 也能被抽到 ⚠️
        //    ⇒ 于是一旦组合根注入了 `Unlocks` + `Progress`，**这里自动按"当前可用 Curio"过滤**，
        //      调用方无需记得传参（少一个"必须记得"的接口 = 少一个漏接的机会）✓
        if (allowedCurios is null && Unlocks is not null && Progress is not null)
        {
            allowedCurios = Progress.AvailableCurios(Unlocks);
        }

        // 候选 = 该类型的行 ∪（支路房）branch 行；权重取【行 weight】（组内权重）
        var candidates = new List<(int Weight, string CurioId)>();
        void Collect(IReadOnlyList<Darkest.Data.RoomContentEntry> rows)
        {
            foreach (Darkest.Data.RoomContentEntry row in rows)
            {
                foreach (string id in row.CurioPool ?? System.Array.Empty<string>())
                {
                    // 🔴 `$pool:<name>`：**校验期已允许，但解析【未实现】** ⇒ 这里**显式跳过并留痕**
                    //    （不静默当成一个 curio id —— 那会在抽取时给出不存在的东西，红线 21）✓
                    if (id.StartsWith("$pool:", StringComparison.Ordinal))
                    {
                        _log.Append(new Darkest.Core.Events.EffectEvent(default,
                            $"curio_pool_unresolved:{id}", 0.0, Triggered: false));
                        continue;
                    }

                    // 🔴 消费点 (b)：**只从【已解锁】的 Curio 里抽**（`O-86` 起手 4 种 → 解锁后 6 种）——
                    //    这是**内核级**拦截（C2：不能只在 UI 上"锁着"）✓ `allowedCurios == null` ⇒ 不限制（测试/旧路径）
                    if (allowedCurios is not null && !allowedCurios.Contains(id))
                    {
                        continue;
                    }

                    candidates.Add((row.Weight, id));
                }
            }
        }

        Collect(contents.ForType(roomType));
        if (isBranch)
        {
            Collect(contents.ForType("branch"));
        }

        if (candidates.Count == 0)
        {
            return null; // 内容表没给这个房间任何内容 ⇒ 交回调用方（不静默造）
        }

        int total = candidates.Sum(c => c.Weight);
        int roll = _rng.NextInt(0, total); // 组内加权抽取
        _log.Append(new RngDraw(_rng.DrawCount, roll)); // 🔴 随机留痕

        int acc = 0;
        foreach ((int weight, string curioId) in candidates)
        {
            acc += weight;
            if (roll < acc)
            {
                _log.Append(new EventNodeResolvedEvent(curioId, "room_content",
                    $"picked:type={roomType}:branch={isBranch}:roll={roll}"));
                return curioId;
            }
        }

        return candidates[^1].CurioId; // 理论到不了（roll < total）；兜底也**不静默**：上面已写留痕
    }

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
                LastScout = _scout.Roll(_log, _rng, _meter.Value, "curio");
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

    /// <summary>最近一次 Curio 的**描述文本**（V6；供 UI 显示）。</summary>
    public string? LastCurioText { get; private set; }

    /// <summary>最近一次 Curio 是否命中**阶段二（未接线）**分支（UI 必须据此标注，红线 21）。</summary>
    public bool LastCurioDeferred { get; private set; }

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
    /// 触发夜袭 ⇒ `LastCampAmbushed = true` ⇒ 调用方插一场额外战斗（计入胜场）。
    /// </summary>
    public bool FinishCamp()
    {
        _session.EnterPhase(FlowPhase.Walking); // 🔴 收营 ⇒ 回【走图】相位 ✓
        _session.EndCamp(_log);
        LastCampAmbushed = RollAmbush();
        return true;
    }

    /// <summary>上一次扎营后是否触发夜袭（`#305`：触发 ⇒ 调用方插一场额外战斗，计入胜场）。</summary>
    public bool LastCampAmbushed { get; private set; }

}
