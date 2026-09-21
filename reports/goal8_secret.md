# 第 8 刀 · `D-6` 隐藏房（`Secret`）—— 已完成

> 目标来源：`doc/architecture/dd_replication_roadmap.md` §3.2 `D-6` 行（**P1**）
> 设计来源：`doc/architecture/dungeon_layer_design.md` §F3d
> 落地日期：2026-09-20 · 前置：`D-3` 三态揭示 ✅ · `D-4` 陷阱转真 ✅

---

## 1. 一句话结论

**隐藏房落地为「地图上根本不画、只有侦察成功才变成可进 rewards 房」** ——
并因此给了侦察一个**非信息类回报**（真金白银），
把 `D-3` 三态揭示 + `D-4` 陷阱拆除 + `D-6` 隐藏房 串成一条**玩家有理由去用**的侦察链路。

---

## 2. 验收总览

| 项 | 结果 |
|---|---|
| 内核工程 `Darkest.Tests.csproj` 构建 | **0 错** ✅ |
| 主工程 `Darkest.csproj` 构建 | **0 错**（14 警告，均为既有 CS8907） ✅ |
| 全量测试 | **739 通过 / 0 失败**（基线 709 ⇒ **净增 30**） ✅ |
| 三扫 `check_data_discipline.py --all` | **0 处**（numbers 0 / deadfuncs 0 / deadkeys 0） ✅ |
| 内核零 Godot `check_godot_refs.py` | **0 命中** ✅ |
| 新增测试文件 | `SecretTests.cs` 18 例 + `SecretFlowTests.cs` 12 例 = **30 例** ✅ |

---

## 3. DD 口径实据（本刀的设计依据）

| 项 | DD wiki 口径 | 本刀落地 |
|---|---|---|
| **地图显示** | 隐藏房**完全不显示**（不是"暗"，是**不画**） | `WalkMapView.FromTileWalk`：未揭示的 `Secret` 格 **`continue` 跳过**（不画格、不加连线、不加点击热区）|
| **揭示方式** | **只有侦察成功**才能把它变成可进的 rewards 房 | `ExpeditionFlow.RevealSecrets`（唯一入口），挂在既有 Curio `scout` 效果上 |
| **非信息类回报** | 揭示后成 rewards 房（给侦察一个**非信息**的回报） | `tuning.dungeon_layer.secrets.reward_gold` ⇒ 走 `Economy.AwardContent`（**真金币**）|
| **位置** | 走廊格可含隐藏房（与陷阱同类） | `ScatterCorridorContent`：**只在走廊格撒**，与陷阱**共用一趟扫描** |
| **走到才触发** | wiki ⑤ | 揭示后 `IsWalkable(Secret) == true`，玩家**自己走过去** |

---

## 4. 新建 / 改动清单

### 新建

| 文件 | 内容 |
|---|---|
| `darkest/tests/SecretTests.cs` | 18 例：字符往返 · P30② 通过/拦截 · 可通行 · `ToRows` 往返 · 撒布（opt-in/仅走廊/互斥/确定性/`RngDraw`/无 rng 抛错）· 加载期 fail-fast 五项 · 出货数据守门 |
| `darkest/tests/SecretFlowTests.cs` | 12 例：未配置即惰性 · 侦察真的给钱 · 无账本不谎报 · 揭示 = `Scouted` 且不写 `Visited` · 幂等 · 配额 + 留痕 · 越界忽略 · 未开走格惰性 · 揭示后可进 · 单值互斥 · 出货守门 |

### 改动

| 文件 | 改法 |
|---|---|
| `DungeonGrid.cs` | `DungeonTileKind` **+`Secret`**（第 12 位）；`DungeonTileMap` **`'*'` ⇔ `Secret`** 双向登记；类注释补登记纪律 |
| `TuningConfig.ExpeditionAndCombat.cs` | **+`TuningSecretScatter`**（`corridor_chance_percent` / `reward_gold` / `max_rewards_per_run` / `note`）；`TuningDungeonLayer` **+`Secrets`** |
| `TuningConfig.cs` | `Validate` **+`D-6` 三条校验**：概率 ∈ [0,100] / **`reward_gold > 0`** / 配额 ≥ 0 |
| `DungeonGridDeriver.cs` | **+`SecretTiles`** 读数；`Derive` **+`secretChancePercent` 重载**；`ScatterTraps` ⇒ **`ScatterCorridorContent`**（陷阱+隐藏房**共用一趟**、每格至多一次掷骰） |
| `ExpeditionFlow.cs` | `EnableTileWalk` 接线两类撒布；**+`D-6` 段**：`RevealSecrets` / `RevealSecretsWithinRooms` / `_claimedSecrets` / `SecretRevealedCount` / `SecretGoldGranted` / `LastSecret` |
| `ExpeditionFlow.RoomInteractions.cs` | Curio `scout` 分支：揭示三态之后**追加** `RevealSecretsWithinRooms`（复用同一批 `RevealWithin` 结果，不重写距离） |
| `Economy.cs` | **+`AwardContent`**（内容回报的**唯一**记账通道；`amount ≤ 0` 拒绝且不扣） |
| `WalkMapView.cs` | `FromTileWalk`：未揭示的 `Secret` 格 **跳过不画** |
| `BattleRoot.cs` | **+`[片4·D-6]` 打印自证**（配置/撒布数/已揭示数/金币） |
| `darkest/data/tuning.json` | **+`dungeon_layer.secrets`**（12% / 250 金币 / 配额 2，全 `placeholder`） |

---

## 5. 本刀最重要的设计判断

### ① 为什么"非信息类回报"是**必须**的（不是锦上添花）

`D-3` 三态揭示给的是**信息**（地图上多出亮轮廓），`D-4` 陷阱给的是**避免损失**。
但**信息本身不改变玩家收益** ⇒ 若隐藏房"揭示了但没东西"，
玩家的最优解是**永不侦察**（省下光照）⇒ 整条侦察链路（含三态、含拆除）**沦为装饰** ⚠️

⇒ 本刀把"揭示"与"**真的给东西**"**绑死**：`reward_gold` 由加载期校验强制 `> 0`。
这是**唯一**一条"数值合法性取决于设计意图"的校验（不是边界检查，是**意图检查**）✓

### ② 陷阱与隐藏房必须**共用一趟**走廊扫描（否则密度不可解释）

`DungeonTileKind` 是**单值** ⇒ 同一格不可能既是陷阱又是隐藏房。
若分两趟各扫一遍，第二趟会**覆盖**第一趟的成果
⇒ "陷阱密度"会悄悄变成"隐藏房概率的函数"（不可解释）⚠️

⇒ `ScatterCorridorContent`：一次遍历、每格**至多一次**掷骰 —— **先判隐藏房**（更稀有），未中再判陷阱 ✓
`SecretFlowTests.SecretAndTrap_NeverShareATile_InTheDerivedGrid` 锁死这一结构 ✓

### ③ 揭示 = `Scouted`，**绝不写 `Visited` 集合**

`D-1` 回头代价按「**站过**」判定 ⇒ 若揭示写进 `Visited` 集合，
第一次走到那格会被当成「回头」多扣光 ⚠️（这是 `D-3` 最容易踩的坑，本刀再次守住）✓

### ④ 未揭示的隐藏房**不能画成"未知格"**

`Unexplored` 的普通格**是会画的**（`MapUnknown` 色块）。
若隐藏房也画成未知色，玩家只要**数格子**就能发现"这里多一块" ⇒ **隐藏房立刻被看穿**（等于地图上明示）⚠️

⇒ 必须与 `Wall` 同待遇：**连"未知格"都不给**，直接 `continue` ✓

---

## 6. 踩坑留档（本轮实测）

### 🔴 坑 1：`tuning.json` 被 `json.dumps` 重排 ⇒ **既有测试批量变红**

- **现象**：写入 `secrets` 段后，**19 个既有用例**失败（`P20_14_GapOrOverlap...` / `HungerTiers_*` / `Revisit*` 等）
- **根因**：这批用例是**文本级拼接**（`ReadData("tuning.json").Replace("...", ...)`）——
  它们依赖**原始紧凑格式**（`{ "battle_from": 1, ... }` 单行对象）与**键的物理位置**
  （`TuningJson` 把 `dungeon_layer` 插在 `"retreat_formula"` **之前**）。
  我用 `json.dumps(indent=2)` 重写整个文件 ⇒ ① 对象被展开成多行 ⇒ 子串匹配失败；
  ② `dungeon_layer` 被追加到**文件末尾** ⇒ 注入的第二份成了**重复键**（后写覆盖）⇒ 坏数据被忽略
- **修法**：从 `git show HEAD:darkest/data/tuning.json` 取回原始文本，
  用**与原风格一致**的自定义渲染器（短对象单行、长对象展开）+ **保持键序**
  （把 `dungeon_layer` 放在 `camp` 之后、`retreat_formula` 之前）重写 ⇒ 709 全绿
- **教训**：**改 `tuning.json` 前先看有没有测试在拼接它**；
  文本级替换的用例 = **格式契约**，"格式化整个文件"是**破坏性改动**

### 🔴 坑 2：`ExpeditionSession.Gain` 会把 `"gold"` **当成口粮**

- `Gain(log, kind, amount, reason)` 的分支是 `if (firewood) { Firewood += } else { Food += }`
  ⇒ 传 `"gold"` 会**加口粮**（静默串账）⚠️
- **修法**：金币的**单一真值**是 `Economy.Gold`、**唯一通道**是 `GoldChangedEvent`
  ⇒ 走新增的 `Economy.AwardContent`（`#325` D6：不得为同一语义造第二份）✓

### 🟡 坑 3：`vision.radius = 2` 会让"揭示 ⇒ `Scouted`"的用例读到 `Visited`

- 出货数据里 `D-3` 的 `vision` 是**开着**的 ⇒ `Scouted` 格一旦进入视野就被**升为 `Visited`**（**正确**语义）
- ⇒ 要测"揭示本身给了什么态"，必须**显式关掉 `vision`**（否则测的是两条通道的合成结果）✓

### 🟡 坑 4：脆断言 —— 用"全流程掷骰总数"证明"关闭时不掷"

- 初版用 `logOn.RngDraw > logOff.RngDraw` 断言 ⇒ **失败**（两次派生本身的掷骰量级相近）
- **修法**：改**结构性判据** —— 同图同种子的 关/开 两次 `Derive`，
  关闭那次 `RngDraw == 0`（关闭时确实一次不掷 ✅），
  且**关→开 的唯一差别只允许是 `*` 覆盖 `C`**（逐格比对，顺带证明"只动被选中的走廊格"）✓

---

## 7. 诚实缺口（有界、自证，非静默）

| # | 缺口 | 性质 | 为什么不补 |
|---|---|---|---|
| ① | 隐藏房**只给金币**，不给战利品/奇物 | 本刀范围 | "里面是**哪件**东西"属**内容表**（`loot` / `curios.json`），是 `D-9` 光照战利品的同族问题 ⇒ 本刀只给**资源回报**（数值层），不越界造内容 |
| ② | 未注入 `Economy` ⇒ 揭示但**金币不计**，写 `secret_no_economy_gold_uncredited` | **留痕不静默** | 钱无处可记 ⇒ **不谎报**（读数 0）。`BattleRoot` 已注入 ⇒ 生产路径正常 |
| ③ | 撒布概率/回报量级全是 `placeholder` | 数值纪律 `#307` | 待 M8 内容层定实（与 `D-4` / `D-5` 同） |
| ④ | `max_rewards_per_run` 的"配额"是**每趟**口径，不是每图 | 设计粒度 | 派生图阶段"一图一趟" ⇒ 两者等价；真关卡（`D-8`）落地时需复核 |

---

## 8. 下一刀

按 `dd_replication_roadmap.md` §5 第 5 刀排期：

> ~~D-3 三态揭示~~（✅）→ ~~D-4 陷阱转真~~（✅）→ ~~D-6 隐藏房~~（✅ 本刀）→ **`D-7` 探索层 act-out**

**`D-7` 探索 act-out**（P2，依赖 `D-5`）：
折磨 ⇒ **拒绝摸奇物 / 拒绝进食** ⇒ `CurioResolver` **前置门禁**：
带特定 Affliction ⇒ 概率拒绝；拒绝**必须有文案事件**（写 `CombatLog`，不静默）。

此后：`D-8` 手写瓷砖图 → `C-1` Stealth → `D-9` 战利品 → `H-5` 元层失败条件。
