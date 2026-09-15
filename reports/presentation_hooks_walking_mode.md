# 表现层钩子清单（行走/地牢模式）—— 主程序 → UI 设计师

> 目的：把 DD 式"侧视走廊行走"要演的每一样，映射到**内核现在就能读的成员**；
> 明确**口径保证**（我不会变的语义）；并列出**还缺的读数**（我加，不让你猜/不让你自己推断）。
> 口径总则：**表现层只读；相位与规则状态一律内核单一真值**（`blueprint §9.17.0`）✓

| # | DD 行走模式要演的 | 现在就能读（**确切成员**） | 口径保证（我不会变） | 还缺 ⇒ 我加 |
|---|---|---|---|---|
| 1 | **走廊推进 / 站位** | `flow.IsTopologyMode`（是否地图模式）· `flow.CurrentRoomId` · `flow.HasVisited(roomId)` · `flow.AdjacentUnexplored()` | 拓扑模式下"当前房间"是**单一真值**；`HasVisited` 只增不减（已揭示不会回退） | 「队伍从 A 走到 B」的**方向**（下一步是左/右/上）—— 现在能从 `Map.Edges` 推，但**不值得你猜** ⇒ 我给 `NextStepDirection(roomId)` |
| 2 | **火把/光照** | `flow.Meter.Value`（0..100）· `flow.Meter.Tier`（枚举）· `flow.Meter.Effect`（七项战斗效果）· `LightMeter.BoundariesFrom(tiers)`（刻度） | 光照**唯一真相**在 `tuning.light.tiers`；档位判定按 data（改 data 两视图一起变）✓ | 无（够用）✓ |
| 3 | **走廊内可点物件（Curio/障碍）** | `flow.CurrentRoomType`（`event`/`battle`/…）· 房间内容表 `RoomContentsConfig`（你已持有）· `flow.ResolveCurio(...)` 的结果 | Curio 的**空手必写 `RngDraw`**、道具路径**不掷骰**（`curio.md` 灵魂）✓ 未接线的道具选项**不列出** | 🔴 **"本间的 Curio 是哪一件 + 是否已解决"**：现在只有"进房时选中"的瞬时结果 ⇒ 我给只读读数 `CurrentCurioId` / `CurioResolved`（免得你自己存状态） |
| 4 | **羊皮纸地图（房间/走廊/标记）** | `flow.Map`（`Rooms`：`Id/Depth/Type/IsBranch` · `Edges`：`From/To`）· `Map.GoalId`（终点）· `flow.ReachedGoal` · `MapTraversal.ShortestPathLength(map, from, to)` | 房间 `Type` 与 `IsBranch` 来自**生成器数据**；地图生成后 `IsConnected()` 必为真 | 「剩余最少段数」已有（`ShortestPathLength`）✓；「战斗/?/营地 图标所需的**每间是否战斗**」= `Rooms[].Type` 直接可读 ✓ 无缺口 |
| 5 | **遭遇 → 战斗（同场景）** | `flow.Session.Phase`（`Walking/Camp/Battle/Resolved`）· `flow.Session.CanShowCampUi`/`CanShowPathChoice`/`CanShowCurioUi` · `BattleRoot.PreviewIntent(actor)`（敌方意图，**不消耗抽数**）· `BattleRoot.StartExpeditionBattleInScene()` | 🔴 **战斗中三谓词必为假**（已端到端验证 ✓）；进战斗相位由宿主推进；**意图预览绝不消耗战斗 RNG** | 无（够用）✓ |
| 6 | **扎营 / 撤退 / 夜袭** | `flow.Session.CanCamp`（含相位+柴火）· `flow.Session.RespiteLeft` · `flow.Session.UseCampSkill(log, skill, unitId, camp)` · `flow.Session.CanAffordFood(camp, tier)` · `flow.FinishCamp()` · `ExpeditionContext.PendingAmbush` | 扎营扣柴火失败 ⇒ **相位不留下**（无半态）；夜袭战斗**不计节点步**（`#307`③） | `flow.LastCampAmbushed`（你已在用 ✓）—— 保持 ✓ |
| 7 | **进度/报告** | `flow.StepsDone` · `flow.Wins` · `flow.IsFinished` · `flow.PendingLoot`（包满待处理）· `flow.TryCollectLoot(...)` | `PendingLoot` 是**待处理不是丢弃**（包满时）；收取走同一条 `Collect` 路径 | 无（够用）✓ |

## 我打算补的三个只读读数（都是"免得你自己存状态"的，不新增机制）
```
① `flow.CurrentCurioId` / `flow.CurioResolved` —— 本间 Curio 是哪件、是否已解决（表 #3）
② `flow.NextStepDirection(roomId)` —— 从当前间到目标间的**相对方向**（表 #1；只读、确定性）
③ `flow.RevealedRoomIds` —— 已揭示集合（= `HasVisited` 的集合形态；省得你逐间问）
⇒ 你若要**别的**读数，直接列"我要演什么"给我，我按"内核保证口径 + 只读"补，别自己推断 ✓
```

## 反向约定（请你遵守的两条，避免两处真值）
```
1. **不自己推断相位**：显隐一律读 `CanShow*`（架构 §9.17.0）✓
2. **不自己抄数值**：光照刻度读 `BoundariesFrom(light.Tiers)`；Curio/扎营数字读 data（`effect_number` 等）✓
```
