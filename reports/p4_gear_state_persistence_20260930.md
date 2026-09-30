# P4 ① · 装备阶入档（存档 v1 ⇒ v2）

> 日期：2026-09-30　·　提交：`6465e88`
> 前置：`reports/goal11_hero_gear.md`（H-1 装备阶 · 2026-09-27 · `O-101` 裁定 = 升级树等级驱动）
> 读数（最低必要）：`SaveSystemTests` + `HeroGearTests` **12/12 绿** · `darkest/Darkest.csproj` 构建 **0 错**

---

## 一句话

**H-1 的「阶」此前没有任何容器是【存档看得见】的** —— 容器建在 `hero_upgrades.json` 的分支里，
`SaveController` 在更早的位置组装 ⇒ **构造时看不到它** ⇒ 存盘写不进阶、读档也不恢复
⇒ **一存一读「金币花了、阶没了」**（静默丢进度：玩家归因不到，日志里一行都没有）。
本件把容器提到存档分支之前**共用同一实例**，并把阶写进快照（`gear`）+ 开一条 **v1 ⇒ v2 迁移**。

---

## §1 病根（先证，再改）

`ExpeditionComposition.Bind` 里原本的形状是：

```csharp
if (FileAccess.FileExists(HeroUpgradesConfig.ResPath))
{
    // ... 备齐铁匠铺前置
    ExpeditionContext.BindGear(new HeroGearState());   // 🔴 只在【这个分支里】建
}
```

而 `SaveController` 的组装在**这个分支之前**（`save.json` 存在即接线）⇒ 两条后果：

1. `hero_upgrades.json` 在 ⇒ 阶容器有了，但**存档没拿到** ⇒ 阶不进档；
2. `hero_upgrades.json` 不在 ⇒ 连容器都没有 ⇒ 存档里更是没有这一项。

📌 **判据（可复用）**：**"这个状态是跨趟的 ⇒ 在【存的时刻】谁持有它？"** ——
持有者不在存档的构造清单里 ⇒ **必丢**，且丢得没有一行日志。

---

## §2 改法（代码面 8 个文件 · 见提交 `6465e88`）

| 面 | 改法 |
|---|---|
| 组合根 | 阶容器提到 `BindConfigs` 之后、**存档分支之前** ⇒ 存档与升级机制**共用同一实例**；`hero_upgrades.json` 缺失时容器仍在（空表 = 全第 0 阶）⇒ 存档**照样带 `gear`**（打印文案同步）|
| 快照 | `SaveSnapshot` 加第 5 份 `Gear`（`GearTierSnapshot` **具名元素** · 输出按 `heroId` Ordinal 排序 ⇒ 文本可 diff）；字段**不给默认值** ⇒ 构造点被编译器点名（纪律：**加字段 = 加版本**）|
| 迁移 | `SaveMigrator.CurrentVersion` **1 ⇒ 2** + **v1 ⇒ v2** 补空表，且**如实回报**（消息里写明补了什么 · 迁移**不碰磁盘**）|
| 校验 | `SaveSerializer.Validate` **按版本分档**判 `gear` —— 与 `SaveMigrator` **共用同一常量** `GearFieldSinceVersion`（不各写字面量）|
| 状态 ⇄ 快照 | 🆕 `HeroGear.Save.cs`（`HeroGearState` 的 partial 另一半）：**先全量校验、再落地**（`_tiers` 是 readonly ⇒ 边填边判会让坏档留下「清了一半」的半截态）；缺 `tiers` / 条目缺 `heroId` / 阶越界 ⇒ `InvalidDataException`，**不静默钳制** |
| 编排 | `SaveController` 构造器 +1 持有者 · `Save`/`Load` 变**五处** Capture/Restore · 回显带「装备阶 N 条」；语义越界档 ⇒ `Fail`（**只回报、不删档** · 红线 18/21）|

---

## §3 判据：v1 补空表**不是**「静默兜底」

v1 那一版**结构上既没有 `gear` 字段、也没有升级入口**（升级机制是 H-1 才接的）
⇒ 「空表 = 全部英雄第 0 阶」是**那一版语义的唯一忠实读法**。
反例才是错的：一刀切按 v2 校验 ⇒ **老玩家的档被判成损坏档**（比丢一个字段严重得多）。

📌 **判据（可复用）**：**"缺字段"判损坏的前提是【这一版本该有它】** —— 按版本分档，不是一刀切。

---

## §4 读数（最低必要 · 不重跑全量）

| 命令 | 读数 |
|---|---|
| `dotnet test darkest/Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~SaveSystemTests|FullyQualifiedName~HeroGearTests"` | **12/12 绿**（SaveSystemTests 8（新增 2）· HeroGearTests 4）|
| `dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0` | **0 错**（71 警告 · 既有族）|

⚠️ **口径提醒**：`Darkest.Tests.csproj` **只直编 `core/sim/data`**（scene 层不在其编译面）
⇒ 上面那条 build 是 scene 层（`SaveController` / `ExpeditionComposition`）改动的**唯一**编译证据。

新增守卫（防回归）：

- **⑦** v1 档无 `gear`（**值 `null`** 与 **字段缺失**两臂）⇒ 迁移成功、版本升到 2、补出**空表**；
- **⑧** 当前版本的档**缺 `gear`** ⇒ 抛；阶**越界**（0~4 之外）⇒ `RestoreFrom` 抛（不静默钳制）；
- **①** 往返：夹具走**真升级**（花 750 金买 `hellion.weapon` code 0 ⇒ 阶 1）⇒ 恢复后阶必须仍是 1
  （若该升级被静默拒绝，断言会看到 0 阶 ⇒ "夹具其实没造出内容"**会被测出来**）。

---

## §5 谁消费它（落地证据）

- `SaveController.Save` 回显「**装备阶 N 条**」⇒ 存盘结果**肉眼可核**（不用拆 JSON）；
- `ExpeditionComposition` 打印「阶容器与存档**共用同一实例**」（`hero_upgrades.json` 缺失时也照打）；
- `HeroGearState.Audit(heroes)`（既有）⇒ 一行 `id[武N/甲M]`，可供 UI / 日志自证。

---

## §6 剩余（**本件不含** · 已逐条实测）

1. 🔴 **战斗侧的缝还没缝**：`HeroProjection.ApplyGearTier` 取的是 `hero.ArmourTier`
   （`HeroProjection.cs:57`），而 `weapon_tier` / `armour_tier` 全仓**0 个写者**
   （`new HeroConfig(` 只有 `Roster.cs:362` 的新兵一处，不带 tier）⇒ 战斗读数**恒第 0 阶**。
   ⇒ 本件入档的 `HeroGearState` 与那条读点**之间还没有桥**。
2. ⚠️ **入口未接（P4 ②）**：`HeroGearState.TryUpgrade` 生产调用 **0 处**（只有 `HeroGearTests`）
   ⇒ 玩家今天**买不到**阶。
3. ⇒ 📌 **建议顺序：先 1 后 2**（先缝桥、可测；UI 做完而桥没缝 ⇒ 玩家买了阶、读数不变 = 更难归因）。
4. **P4 ③**：伤害半（`WeaponBaseDamage`）仍被守卫钉住 ⇒ 挂「待 `dmg%` 一手值」的显式自证。
5. **旧档提示**：v1 ⇒ v2 的迁移消息目前只进 `SaveLoadResult.Message`，是否弹给玩家由 UI 域定。

---

## §7 投递自证（**字节算账** · 不采信工具回读）

`tools/dsh/deliver_letter.py` 对 **CRLF 信件**的 marker 回读**恒 0**
（缺陷已登记 `doc/state.md #491①` · 本件**第三次复现**：实测 `marker=<no marker found>` / `readback hits=0` / `exit=1`）
⇒ 🔴 **不得把 `hits=0` 读成“没送到”**（否则下一轮会**重复投递**）。改用**字节算账**独立验证：

| 窗口 | 投递前（字节）| 投递后（字节）| 差 |
|---|---|---|---|
| `doc/windows/架构窗口.txt` | 25831 | 36417 | **+10586 = 信 10585 + 分隔1** |
| `doc/windows/主程序窗口.txt` | 788523 | 799109 | **+10586 = 信 10585 + 分隔1** |

另用 `Select-String -SimpleMatch` 复核标记命中（投递前两窗均为 **0 处**）：
`架构窗口` 第 **399 / 405** 行 · `主程序窗口` 第 **10836 / 10842** 行 ⇒ **两窗各 2 处**（正文 1 + 送达判据行 1）✓

📌 **判据（可复用）**：**工具自报的“0”要先问“它的判据在当前形态下成立吗”** —— 同族 `#491①` / `BH` / `BU`。
