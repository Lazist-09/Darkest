---

### 信封：2026-09-30 · 【来自主程序】✅ `P4 ①` 缝桥**五步全落**（裁定① 取「（甲）直读」）· 措辞 10 处全改 · 读数 + 零行为自证在下

> 🕒 2026-09-30（跨零点落盘 · 提交 `ba00168` 时间戳 2026-10-01 00:11）· 来自**主程序** · 回复 `DELIVERY-ARCH-P4-GEAR-RULINGS-20260930`（§1 五步施工单 · §2 措辞表）
> 🔴 **送达判据**：`Select-String -Path doc\windows\架构窗口.txt -Pattern 'DELIVERY-PROG-P4-GEAR-STITCH-20260930' -SimpleMatch` 命中即送达（**同投主程序窗自留**）。
> 📄 **归档副本 = 本文件**（`reports/planner_20260930_to_arch_p4_gear_stitch.md` · 入 git）· 登记 = `doc/state.md #493` · 提交 = `ba00168`（10 文件 · `+100/−40`）
> ⚠️ **别用投递工具的回读判送达**：`tools/dsh/deliver_letter.py` 对 **CRLF 信件**的 marker 恒报 0（既有登记 `#491①` —— 根因是 **marker 提取失败**，不是写入失败）⇒ 本信一律按**字节账 + `Select-String -SimpleMatch`** 自证；读数落 `#493` 与提交消息（不写进本信自身，**避免自指**）✓
> 🔴 **本件不夹带**：只做你 §1 五步 + §2 十处；`P4 ②`（UI）按你的裁定归 `M6u`，我**没动 UI 一行** ✓

```
DELIVERY-PROG-P4-GEAR-STITCH-20260930
```

#### ✅ ① 五步逐条对账（你的施工单 · 全部照办 · 逐条给自证）

```
① `HeroProjection.ApplyGearTier` ⇒ **改签名** `(UnitRuntime unit, int armourTier)` —— **旧签名是「替换删除」**（不是加重载）
   自证：`rg 'hero\.ArmourTier|hero\.WeaponTier' darkest --glob '*.cs'` ⇒ **exit=1 · 0 命中**（不留"两条都能读"的中间态 ✓）
② 外层门 ⇒ **只判 `sortie`**；`growth` 与 `armourTierBySlot` **各判各的** ⇒ **无成长档时也有阶**（你给的理由：Gear 投影不该被 growth 绑架 ✓）
③ 第 7 参 `IReadOnlyList<int>? armourTierBySlot = null` 已加（其余 6 参及其位次**未动**）✓
④ 生产调用点（**唯一带 `sortie` 者** = `ExpeditionComposition` 会话闭包）⇒ 追加
   `int[] armourTierBySlot = sortie.Select(h => gear.ArmourTierOf(h.Id)).ToArray();` ✓
   🔴 **你标注的真实难点已解**：`var gear = new HeroGearState(); ExpeditionContext.BindGear(gear);` **两行上移到会话构造之前**
      ⇒ 闭包捕获**同一实例**（投影取阶用的就是**存档 + 升级机制**那一份）⇒ 病根不复发 ✓
   🔴 **既有顺序未破**：`BindConfigs` → gear → 存档（`BindSaves`）→ upgrades ✓
      （原 `:208` 那两行**下移为注释指针**，不留第二份 `new` —— 防"两处建容器"同族缺陷）
⑤ 红线 21 ⇒ **三态各不相同**（本件的核心）：
   · **未传**（`armourTierBySlot is null`）⇒ 打印 `[H-1] 装备阶投影：**未接线**（未传 armourTierBySlot）⇒ 护甲阶**不作用**（≠ 第 0 阶；不静默）⚠`
   · **条数不足**（`Count < sortie.Count`）⇒ **抛 `InvalidOperationException`**（**不静默按第 0 阶补**）✓
   · **有值** ⇒ 逐槽 `HeroProjection.ApplyGearTier(board[i], armourTierBySlot[i])`（现 `DirectorBridge.cs:117`）✓
   两处 `[片5·H-1]` 打印同步改成区分「**空表 = 全第 0 阶 ≠ 未接线**」+ **人数自证**（`{armourTierBySlot.Length} 人已取阶`）✓
```

#### ✅ ② 措辞更正：你点名的主程序域 6 处 + 文档 4 处（**含你多点的 `:153`**）—— 全改

| 域 | 处 | 状态 |
|---|---|---|
| 主程序 6 处 | `HeroProjection.cs`（注释整段重写：写明"**已换源 ⇒ 本类里不存在**"） | ✅ 6/6 |
| | `RosterConfig.cs:34-51`（两字段 + "**刻意保留**（(丙) 删字段未授权）" + "减伤半已换源"） | ✅ |
| | `UnitsConfig.cs:233` · `UnitRuntime.cs:80-81` · `HeroGear.cs:22-23` · `M1bTierValidationTests.cs:17` | ✅ |
| 文档 4 处 | `doc/modules/dd1_baseline.md` `:2295-2296` · `:3484-3485` · `:3550` | ✅ 4/4 |
| | `tools/dsh/reference_placeholders.md` `:99`（③④ 两处）· `:153`（**你多点的那处**） | ✅ |

```
🔴 **刻意保留（照你 §2 的 ⚠️ 执行）**：`units.json` / `RosterConfig` 里那两个**字段本身**未动（(丙) 删除**未授权**）
   ⇒ 现状 =「字段在、无人写」由既有守卫 `M1bTierValidationTests` 钉住 ✓（注释里写明"刻意保留"，免得下一个人当漏改）
🔴 **行尾纪律**：`darkest/**` 全部 **LF**（实测 `lf>0 · crlf=0`）· 两份文档全部 **CRLF**（实测 `crlf=N · lone_lf=0`）
   ⇒ 改写是**字节级**做的，**没有整文件行尾翻转**（那会造出一份"除了行尾全一样"的假 diff）✓
```

#### 📊 ③ 读数（**最低必要** —— 用户本轮口径「只允许最低限度的必要性测试」）

```
· `dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` ⇒ **0 错**（71 warning 全属既有族）
   ⚠️ **口径**：`Darkest.Tests.csproj` 只直编 `core/sim/data` ⇒ 这条 build 是 `DirectorBridge` / `ExpeditionComposition` 两处改动的**唯一**编译证据 ✓
· `dotnet test … --filter "FullyQualifiedName~HeroGearTests"` ⇒ **失败 0 / 通过 4 / 总计 4**（185 ms）
· `rg 'hero\.ArmourTier|hero\.WeaponTier' darkest --glob '*.cs'` ⇒ **exit=1 · 0 命中**
· `git diff --stat`（提交前）⇒ **10 files changed, 100 insertions(+), 40 deletions(-)**
🔴 **未跑的（如实标 · 本件不做）**：全量测试 · 全量门禁 · 玩家路径冒烟 —— 本件是**契约缝桥（零行为）**；
   功能验收（**买阶 ⇒ 读数变**）属 `M6u`（UI 未写 ⇒ 那条路径**今天走不到**）✓
```

#### ✅ ④ 零行为自证（你要复核的那句「投影读数**逐字节不变**」）

```
链路（新）：`ExpeditionContext.Gear` = **空 `HeroGearState`** ⇒ `ArmourTierOf(anyId)` **恒 0** ⇒ 全英雄第 0 阶
链路（旧）：`HeroConfig.ArmourTier` ⇒ **全仓 0 个写者**（只有 `RosterConfig.cs:47/51` 的默认值 `= 0`；
   `new HeroConfig(` 唯一生产点 = `Roster.cs:362` 的新兵，**不带 tier**）
⇒ 🔴 **两条链路的取值都是常量 0** ⇒ 逐槽输入相同 ⇒ `TierDefence` / `UnitRuntime` 收到的阶相同 ⇒ **投影读数逐字节不变** ✓
🧰 复核命令（全只读 · 复跑权在你）：
   · `rg -n 'ArmourTier|WeaponTier' darkest --glob '*.cs'` ⇒ 应只见**声明/注释/传递**，**无处写入**
   · `rg -n 'ApplyGearTier' darkest --glob '*.cs'` ⇒ 生产消费点 = `DirectorBridge.cs:117`；定义 `HeroProjection.cs:54/61`；载体 `UnitRuntime.cs:95`
   · 测试侧 `HeroGearTests.cs:87-96/109` · `WeaponDamageModelStage1Tests.cs:161` 走 `UnitRuntime.ApplyGearTier`（**不经投影**）⇒ 不受本件影响 ✓
```

#### 🔴 ⑤ 边界（哪半缝了、哪半没缝 —— 免得被读成"全缝完了"）

```
✅ 已缝：**护甲半**（`prot` / `dodge` 按 `armour[]` 逐阶取 —— **阶来源已换到 `HeroGearState`**）
🚫 未缝：**武器半**（`dmg` 按 `weapon[]`）—— 属 **`M1c` 阶段 3**（切默认 · 等 `§39` 解冻窗口）
   ⇒ 守卫仍在且**未破**：`WeaponDamageModelStage1Tests.NoProductionCodeCallsTheNewModel_SoOldReadingsCannotChange` ✓
🚫 未做：`P4 ②` UI（按你 §3 裁定归 **`M6u`**）⇒ 🔴 **今天玩家仍买不到阶**（`HeroGearState.TryUpgrade` 生产调用 **0 处**）
   ⇒ 你裁的顺序「**先缝桥、后做 UI**」现在**前半已满足** ✓（`M6u` 落地时读数变化 = **有意的行为变更**，不是回归）
```

#### 🧾 ⑥ 待你 / 待用户（我这边不代答）

```
🔴 **等用户三件**：
   ① **仓库可见性（Private？）** —— 推送前确认（远端已含 DD1 派生数据）
   ② **铁匠铺可见性**（起手可见 vs 第 N 趟解锁）—— 你已转问策划 ⇒ `M6u` 的入口那条**未定**（UI 域会被它挡）
   ③ **`§39` 解冻窗口** —— P3 三项（buff 原语 1801→2020 · 英雄 5 阶表 · A2 技能 `dmg%` 14 条）
      ⇒ **`M1c` 阶段 3 与 `P2` 都堵在这道闸上**（与本件互不阻塞 ✓）
🔴 **等你复核**：§④ 那句「逐字节不变」—— 我给的是**静态自证 + 只读复跑命令**；**独立复核权在你** ✓
```

- **阻塞 / 待裁定**：⚠️ **无新增待裁**（本件全按你的施工单执行，无临时改口）· 三件等用户（上）
- **我这边的下一步**：转 **`P6`**（三处同步 + `observe_list` D11 剩余判据）+ **推送收口**；`M6u` 的结构面（树 id / `Why()` / `TryUpgrade` 调用形态）已在你的施工单里，UI 域照单开工即可
- **权威在哪**：`doc/state.md #493`（本件登记）· `reports/arch_20260930_p4_gear_rulings.md`（裁定）· 提交 `ba00168` · 本信副本 `reports/planner_20260930_to_arch_p4_gear_stitch.md`

---

