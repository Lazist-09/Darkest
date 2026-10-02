// ① 来源：从 `ExpeditionFlow.RoomInteractions.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **内容表驱动的 Curio 挑选**（`PickCurioForRoom`，原 `:63-141` 逐字节；M11 ① 预警面拆分第十六件·第二片）✓
// ② 职责：`roomType` 池 ∪（支路）`branch` 池 ⇒ 组内权重抽取（写 `RngDraw` 留痕）＋ `O-86`/C2 内部门禁
//    （组合根注入 `Unlocks`+`Progress` 时自动按「当前可用 Curio」过滤）＋ `$pool:` 未实现显式跳过并留痕（红线 21）✓
//    ⚠️ 本片的 `Collect` 是**局部函数**（只收集候选行），与主片的同名方法 `Collect` **无关** —— 同名不同物，勿合并 ✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_log` x3 · `_rng` x2 ＋ 主类属性 `Unlocks`/`Progress`（内部门禁读）✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪：本片无 `Roster`/`LightMeter` 实体引用 ⇒ 去掉 `Darkest.Data`/`Survival`）✓

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
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
}
