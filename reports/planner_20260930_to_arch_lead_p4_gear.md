---

### 信封：2026-09-30 · 【来自策划】🔴 **`P4 ①` 装备阶【入档】已落地（存档 v1⇒v2）—— 两处未缝在等你 · 另请裁 3 件**

> 🔴 **送达判据**：`Select-String -Path doc\windows\架构窗口.txt -Pattern 'DELIVERY-DESIGNER-P4-GEAR-PERSIST-20260930' -SimpleMatch` 命中即送达。
> 🔴 **同时投主程序窗**（接线与 UI 都在它手上）· **归档副本**：`reports/planner_20260930_to_arch_lead_p4_gear.md`
> 🔴🔴 **别用投递工具的回读判送达**：`tools/dsh/deliver_letter.py` 对 **CRLF 信件**的 marker 回读**恒 0**（缺陷我已登记 `doc/state.md #491①`，本件第三次复现）⇒ 我改用**字节算账**自证（读数列在 `reports/p4_gear_state_persistence_20260930.md` §7）✓
> 📄 报告 = `reports/p4_gear_state_persistence_20260930.md` · 提交 = `6465e88` · 决策 = `doc/state.md #492` · 开放题 = `doc/architecture/open_issues.md` 的 `O-101`（**追加 · 维持未结案**）

```
DELIVERY-DESIGNER-P4-GEAR-PERSIST-20260930
```

#### ✅ ① 一句话：阶以前**没进档** —— 而且丢得**一行日志都没有**

```
（病根 · 先证再改）`ExpeditionComposition.Bind` 里阶容器是这样建的：`if (hero_upgrades.json 存在) { … BindGear(new HeroGearState()); }`
   ⇒ 而 `SaveController` 的组装**在这个分支之前** ⇒ ①数据在 ⇒ 存档**拿不到它**；②数据不在 ⇒ 连容器都没有
⇒ 🔴 两条后果同一形态：**一存一读「金币花了、阶没了」**（玩家归因不到 · 日志里没有一行）✓
📌 判据（可复用）：**「这个状态跨趟 ⇒ 在【存的时刻】谁持有它？」** —— 持有者不在存档的构造清单里 ⇒ **必丢，且丢得没有一行日志** ✓
```

#### ✅ ② 改法（8 文件 · 一句话一条）

```
① 组合根：容器提到 `BindConfigs` 之后、**存档分支之前** ⇒ 存档与升级机制**共用同一实例**
   （`hero_upgrades.json` 缺失时容器仍在 ⇒ 空表 = 全第 0 阶 ⇒ 存档照样带 `gear` · 打印文案同步）
② 快照：`SaveSnapshot` 加第 5 份 `Gear`（`GearTierSnapshot` **具名元素** · 输出按 `heroId` 排序 ⇒ **文本可 diff**）
   🔴 字段**不给默认值** ⇒ 构造点被编译器点名（纪律：**加字段 = 加版本**）✓
③ 迁移：`CurrentVersion` **1⇒2** + **v1⇒v2 补空表** —— v1 那一版**结构上既没有 `gear`、也没有升级入口**
   ⇒「空表 = 全第 0 阶」是**那一版语义的唯一忠实读法**；一刀切按 v2 校验 ⇒ 会把老玩家的档**误判成损坏档**（比丢一个字段严重）✓
④ 校验：`SaveSerializer.Validate` **按版本分档**判 `gear`，且与迁移**共用同一常量** `GearFieldSinceVersion`（不各写字面量）✓
⑤ 状态 ⇄ 快照：🆕 `HeroGear.Save.cs`（`HeroGearState` 的 partial 另一半）—— **先全量校验、再落地**
   （`_tiers` 是 readonly ⇒ 边填边判会留「填了一半」的半截态）；缺 `tiers` / 条目缺 `heroId` / 阶越界 ⇒ **抛**（不静默钳制）✓
⑥ 编排：`SaveController` 构造器 +1 持有者 · `Save`/`Load` 变**五处** Capture/Restore · 回显带「**装备阶 N 条**」
   · 语义越界档 ⇒ `Fail`（**只回报、不删档** · 红线 18/21）✓
⑦ 守卫：v1 档无 `gear` **两臂**（值 `null` / 字段缺失）⇒ 迁移补空表 + 版本升 2；当前版本缺 `gear` / 阶越界 ⇒ **看得见** ✓
```

#### 📊 ③ 读数（最低必要 · 不重跑全量 —— 用户本轮口径「只允许最低限度的必要性测试」）

```
· `dotnet test darkest/Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~SaveSystemTests|FullyQualifiedName~HeroGearTests"` ⇒ **12/12 绿**（SaveSystemTests **8** = 原 6 + 新 2 · HeroGearTests 4）
· `dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0` ⇒ **0 错**（71 警告 · 既有族）
   ⚠️ **口径**：`Darkest.Tests.csproj` **只直编 `core/sim/data`**（scene 层不在其编译面）⇒ 上面那条 build 是 `SaveController` / `ExpeditionComposition` 改动的**唯一**编译证据 ✓
· 夹具**走真升级**（`economy.AwardContent(log, 5000, …)` + `gear.TryUpgrade(…, GearAxis.Weapon, buildingLevel: 4)` 买 `hellion.weapon` code 0 = **750 金** ⇒ 阶 1）
   ⇒ 若该升级被静默拒绝，断言会看到 0 阶 ⇒ **「夹具其实没造出内容」会被测出来** ✓
```

#### 🔴 ④ 两处【未缝】（我实测 · 都不在本件范围）—— 建议按这个顺序处理

```
① **战斗读点还没换源**：`HeroProjection.ApplyGearTier` 取的是 `hero.ArmourTier`（`HeroProjection.cs:57` · 唯一调用点 `DirectorBridge.cs:92`）
   而 `weapon_tier` / `armour_tier` **全仓 0 个写者**（只有 `RosterConfig.cs:42/45` 的默认值 `= 0`；`new HeroConfig(` 只有 `Roster.cs:362` 的新兵一处，不带 tier）
   ⇒ 🔴 **战斗读数恒第 0 阶** —— 我今天入档的 `HeroGearState` 与那条读点**之间还没有桥** ✓
② **入口还没接（= P4 ②）**：`HeroGearState.TryUpgrade` **生产调用 0 处**（只有 `HeroGearTests`）⇒ 玩家今天**买不到**阶 ✓
⇒ ✅ **建议顺序：先缝 ①、后做 ②** —— ①换源是**零行为**（今天 `HeroGearState` 空表 ⇒ 全第 0 阶 ⇒ 读数逐字节不变 · 可证）；
   而**②一旦落地，阶会真的 >0 ⇒ 那时读数变化是【有意的行为变更】**（顶层 `prot` 8/12/4/5 → 第 0 阶 0 的差已登记 `reports/top_level_vs_tier0_consistency.md` §1）
   ⇒ 📌 UI 做完而桥没缝 ⇒ **玩家买了阶、读数不变 = 更难归因**（同族：静默失效）✓
```

#### 🧾 ⑤ 请裁 3 件

```
🔴 **①（架构域 · 契约）战斗侧读点怎么换源？** 三条候选：
   （甲）**直读**：`DirectorBridge.cs:92` 那里从 `ExpeditionContext.Gear.ArmourTierOf(hero.Id)` 取阶传进投影（`HeroProjection.ApplyGearTier` 加一参或新增重载）
         ⚠️ `Gear` 是 `HeroGearState?`（可空）⇒ 组合根未接时**不许静默回 0 阶**（红线 21）⇒ 要么打印一行区分「未接线」与「第 0 阶」，要么例外
   （乙）**回写**：升级成功时把阶写回 `HeroConfig.WeaponTier` / `ArmourTier` —— 🔴 但 `HeroConfig` 是 record、`Roster.Heroes` 是 `IReadOnlyList`
         ⇒ 要重建实例，且造出**同一概念两处真值**（`HeroGearState` + `HeroConfig`）⇒ 按 `#487` 同族判负 ✓
   （丙）**删掉** `HeroConfig` 那两个字段 —— 前置 = 先出一张「读侧清单」（`UnitStats` / `WeaponBaseDamage` / `UnitTiers` / `TierDefence` / `UnitStatsMapper` 都在读）
   ⇒ 📌 **我推荐（甲）**：真值只有一处（`HeroGearState`）· 不动 record 不可变性 · 今天**零行为可证** ✓
   🔴 **并请顺手裁一句措辞更正**：`O-101` 那条裁定写「由 `Roster`/`Hero` 持有」，而 H-1 实际落成 **`HeroGearState`**（与 `Economy` / `HeirloomStock` 同层 · 按英雄 id）
      ⇒ 我倾向改成「由 `HeroGearState` 持有」，**理由正是** record 不可变 ⇒ 阶涨不了 ✓

🔴 **②（主程序 + UI 域）`P4 ②` 铁匠铺 UI 的验收口径** —— 我的建议（红线 18 加强版：走**玩家路径**，不以「编译通过 + 测试全绿」当功能验收）：
   进城 → 铁匠铺 → 对某英雄点「升武器 / 升护甲」⇒ ① 金币减少 ② `gear_upgraded` 事件可见
   ③ **退出重进后仍是高阶**（**这一条才是本件的主人**）④ 前置不满足时**看得见理由**（`HeroGear.Why` 的人话串直接显示，**UI 不许自己造文案**）✓
   ⇒ 要你裁的是：**谁写这个 UI**（UI 域 or 主程序）· 以及**它放哪个包**（单独 `P4 ②` 还是并入 `P5` 的 M6u 建筑升级树）✓

🔴 **③（UI 域）v1⇒v2 的迁移消息要不要弹给玩家？** 现状 = 只进 `SaveLoadResult.Message`（不弹）· 三条候选：
   （甲）不弹（现状）（乙）首次载入弹一次（丙）**不打断、但落进常驻可见的「存档信息」面**
   ⇒ 我倾向 **（丙）**：迁移**不是损坏**（不该报警），但「你的档被改过」这件事**应当可查**（可查 > 弹窗）✓
```

#### 🧰 ⑥ 复跑与自证（全只读）

```
· 复跑读数：§③ 那两条（filter 版本即可，不必全量）· scene 层那条 build 不许省（它才是 `SaveController` 的编译证据）✓
· 存档面自证：`SaveController.Save` 回显「**装备阶 N 条**」· `ExpeditionComposition` 打印「阶容器与存档**共用同一实例**」· `HeroGearState.Audit(heroes)` 一行 `id[武N/甲M]` ✓
· 门禁：本轮**无新增文件类型**（`check_file_size.py` 的 ≤400 目标 / 600 红线不变）⇒ 我未重跑全量门禁；如要，`python tools/check_file_size.py` ✓
```

#### 🔴 ⑦ 诚实边界（能验的与不能验的分开写）

```
✅ **能验（当场跑出）**：12/12 绿 · scene 层 build 0 错 · v1 两臂迁移（值 null / 字段缺失）· 缺 `gear` 与阶越界**抛** · **真升级往返**（花 750 金 ⇒ 阶 1 ⇒ 存 ⇒ 读 ⇒ 仍 1）✓
🔴 **不能验（如实标）**：
   ① **玩家路径未验**：UI 未接 ⇒ 我**没有办法从玩家路径**走到「买阶」这一步 —— 而红线 18 加强版要的正是玩家路径 ⇒ **这条只能等 `P4 ②`** ✓
   ② **战斗侧读数未验**：桥未缝 ⇒ 它今天是**恒第 0 阶**，验了也只是验「恒 0」⇒ 不写进读数 ✓
   ③ **我没跑全量测试**（本轮口径）⇒ 这份读数是**本件的读数**，**不是回归基线** ✓
🚫 **本件没有动任何数值**（`#307` 冻结不破）：新增的只有**结构**（快照字段 + 迁移 + 校验 + 编排）✓
```

- **阻塞 / 待裁定**：🔴 **三件**（§⑤ ① 接线口径 · ② UI 验收口径 · ③ 迁移提示口径）
- **我这边的下一步（已认领）**：① `P4 ②` 的**卡片**（「谁写 UI」一定下来就能发）· ② `§39` 解冻窗口（`P3` 三项：buff 原语 1801→2020 · 英雄 5 阶表 · A2 技能 `dmg%` 14 条）**仍在等你解冻** ✓
- **权威在哪**：`doc/state.md #492` · `reports/p4_gear_state_persistence_20260930.md` · `doc/architecture/open_issues.md` 的 `O-101` 追加面 · 本信副本 `reports/planner_20260930_to_arch_lead_p4_gear.md`
