# DLC 缺口量化：一手把数据散在**本体 + `dlc/` + `modes/` 三处** —— 只扫本体**漏 3 英雄 + 29 怪物**

> 🕒 2026-09-26 · 承接 `60_version_diff_ruled_out.md §5` 的"`musketeer` 漏收"⇒ **本件量化全貌** ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴 一手的数据**分三处**（这是我方与参考都踩到的坑）

```
🎖️ **实测一手的布局**：
   ① **本体** ⇒ `E:\…\DarkestDungeon\<类别>\`（如 `upgrades/heroes/` · `monsters/`）✓
   ② **DLC** ⇒ `E:\…\DarkestDungeon\dlc\<id>\<类别>\`（**5 个 DLC**）✓
   ③ **游戏模式** ⇒ `E:\…\DarkestDungeon\modes\<mode>\<类别>\`（`new_game_plus` · `radiant`）✓
⇒ 🎖️ **即：`upgrades/heroes/` 里【只有本体 15 个】** ⇒
   ⚠️ **只扫它会漏掉【全部 DLC 内容】** ✓
```

## 2. 📊 5 个 DLC 的数据量（实测）

| DLC | `.json` | `.darkest` |
|---|---|---|
| **1117860_arena_mp** | 27 | 75 |
| **445700_musketeer** | 8 | 3 |
| **580100_crimson_court** | **70** | **213** |
| **702540_shieldbreaker** | 22 | 49 |
| **735730_color_of_madness** | 37 | 142 |
| **合计** | **164** | **482** |

## 3. 🔴 英雄：一手 **18** · 参考 **16** · 我方 **15**

```
一手【本体】(15)：abomination · antiquarian · arbalest · bounty_hunter · crusader ·
   grave_robber · hellion · highwayman · houndmaster · jester · leper · man_at_arms ·
   occultist · plague_doctor · vestal ✓
一手【+DLC】(18)：上面 15 ＋ **flagellant** · **musketeer** · **shieldbreaker** ✓
    （`flagellant` 在 `1117860_arena_mp`/heroes · `musketeer` 在 `445700_musketeer` ·
      `shieldbreaker` 在 `702540_shieldbreaker`）✓
参考 (16)：15 ＋ **musketeer** ✓
我方 (15)：15（**无 DLC**）✓
```

| 对比 | 结果 |
|---|---|
| 🔴 **一手有、参考没有** | **`flagellant`** · **`shieldbreaker`** ⇒ **参考也漏了 2 个** ⚠️ |
| 🔴 **一手有、我方没有** | **`flagellant`** · **`musketeer`** · **`shieldbreaker`** ⇒ **我方漏 3 个** ✓ |
| 🔴 **参考有、我方没有** | **`musketeer`** ⇒ 采用参考会补上它 ✓ |

⇒ 🎖️ **即：三方【各不相同】** —— 一手 18 · 参考 16 · 我方 15 ✓
   📌 **而参考【扫了 DLC】**（收了 `musketeer`）⇒ ✅ **不是"参考没扫"**，
      而是**参考只扫了一部分 DLC**（收了 Musketeer DLC 的英雄，没收 Shieldbreaker 的）⚠️
   🎖️ **判据**：**"上游的东西，是【一份】还是【散在多处】？"** ——
      散在多处 ⇒ ⚠️ **"我扫了目录"这句话【必须指明扫了哪些】** ✓
      📌 与第 12 条（先列目录再取件）**同族，但更细**：
         **不只列一次目录 —— 要列【所有可能位置】** ✓

## 4. 🔴 怪物：DLC 里还有 **29 个文件**

```
**702540_shieldbreaker**/monsters ⇒ **3** 个：`snake_big_adder` · `snake_cobra` · `snake_rattler` ✓
**735730_color_of_madness**/monsters ⇒ **26** 个：`cocoon` · `com_bulrush` · `com_cattail` ·
   `com_crocodile` · `corpse_crystal` · `farmer` … ✓
⇒ 合计 **29** 个怪物文件（**我方 A4 抽的 230 个是否含它们 ⇒ 记待核**）✓
   🎖️ **判据**：**"我抽取时用的路径，覆盖了【所有存放位置】吗？"** ✓
```

## 5. 🎖️ 这条对采用的**影响**（两个层面）

```
① 🔴 **补齐方向**：按用户指令"采用参考" ⇒ **参考的 16 个是我方的目标** ✓
   ⇒ 📌 **补 `musketeer`**（参考有）✓
   ⇒ ⚠️ 而 **`flagellant` / `shieldbreaker`** ⇒ **参考也没有** ⇒
      ✅ **不在"采用参考"范围内** ⇒ 归**策划决定要不要额外补** ✓
      🎖️ 判据：**"参考没有的东西，要不要补？"** —— 那是**超出指令**的决定 ✓

② 🔴 **核对方法要改**：以后凡"一手有而我没有"的结论，**必须说明扫了哪几处** ✓
   📌 否则**"漏扫 DLC"会被误报成"参考/一手没有"** ⚠️
   🎖️ **这正是我这几件反复踩的"覆盖缺口"** ⇒ ✅ **本件把它固化** ✓
```

## 6. 诚实边界

```
✅ **能验**：**5 个 DLC 的数据量（164 json + 482 darkest）** ·
   **一手 18 / 参考 16 / 我方 15 的确切名单** · **三方的差集（逐个列出）** ·
   **DLC 里 29 个怪物文件的路径与名字** · **`flagellant` 在 `arena_mp` 下** ——
   **全部当场跑出** ✓
🎖️ 并**把"漏收 `musketeer`"从单点扩成了全貌**（三方各不同）✓
🔴 **不能验**：**我方 A4 的 230 个怪物是否含 DLC 那 29 个** ⇒ 记**待核** ✓
   📌 （A4 抽的是**参考**的怪物目录 ⇒ 若参考也漏扫 DLC ⇒ **我方那 230 个也不含**）⚠️
🔴 **不能验**：**DLC 里的 `campaign/` `inventory/` 等数据**（不止英雄与怪物）⇒
   📌 本件**只清了 `heroes/` 与 `monsters/`** ⇒ 记**未穷举** ✓
🔴 **不能验**：**`modes/` 那两套是否也是独立数据源**（`new_game_plus` / `radiant`）⇒
   📌 已知它们**含升级树**（`60_*.md` 比过）⇒ 记**部分覆盖** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/dlc_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **DLC 缺口【量化】**：一手散在三处 ⇒ 三方英雄各不同（18/16/15）· DLC 另有 29 怪物 ✓
🆕 **可做**：① 核 A4 的 230 怪物是否含 DLC（**若参考也漏扫 ⇒ 我方也缺**）
   ② 清 DLC 里**别的类别**（`campaign/` `inventory/` `dungeons/` …）⇒ 补全缺口清单
   ③ 把"一手散在本体 + dlc + modes"补进 `observe_list` D11 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
