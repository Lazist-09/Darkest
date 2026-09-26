# A12：地牢 / 地图 —— 抽取与形态判定

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_dungeons.py`（可重跑）·
> 产物 `reports/unity_ref/dungeons_from_ref.json`（177,892 bytes）✓
> 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

## 1. 🎖️ 形态判定（**两个目录形态相反**）

```
🔴 **同一后缀 `.bytes`，同一数据根下，两种形态并存** ⚠️
   `Dungeons/*.bytes` —— **7 个全是【DD1 文本】**（前 72 字节可打印率 > 85%）✓
      head = `b'id: 0\r\nis_released: true\r\nhall_variants: 7\r\nroom_var'`
   `Maps/*.bytes`     —— **7 个全是【Unity 二进制】** ⚠️
      head = `b'\x0edarkestdungeon1\x00\x00\x00\r\x00\x00\x00\x05entry…'`（含长度前缀 + NUL）
⇒ ✅ **`Dungeons/` 可抽；`Maps/` 不可抽（与 `PLAN_adoption §9⑤` 的判断一致）** ✓
📌 **判据**：**"`.bytes` 后缀不能用来选解析器 —— 要先读前 64 字节判形态"** ✓
   （这条在本任务里**第 2 次**用到：A10 的 `Items.bytes` 也是文本 ✓）
```

## 2. 🔴 我在这里被 **fail-fast 挡下两次**（都记下来，因为都改变了结构判断）

```
🔴 **第 1 次**：我第一版把条目 kind **硬编码成 4 种**（`named`/`boss`/`hall`/`room`）
   ⇒ 被 `Cove.bytes:48` 的 **`stall:`** 挡下（"未识别的行"）⚠️
   ⇒ ✅ **实测 kind 有 13 种**（见 §4）—— 我的先验**错了一倍多** ✓
🔴 **第 2 次（更有价值）**：我把 `props:` 当"裸列表"⇒ 里面的 `hall_curios: .chance 10 .types …`
   被判成"条目不在 mash 段内" ⇒ ✅ **又响了** ✓
   ⇒ 🔴 **实测：`props:` 本身是一个【段名】**，段内条目**仍带 `.chance`/`.types`** ⚠️
   ⇒ 📌 **即 DD1 的内容分两大段**：
      `mash:`（**遭遇** —— 怪物组合）· `props:`（**奇物/宝藏/陷阱/障碍**）✓
🎖️ **两次都是"响亮失败"而不是"静默少收"** —— 这正是我给自己定的规矩
   （**解析失败必须 `exit 1`，不许静默跳过**）✓
   📌 反例对照：A4 那次我**没有**让 `name:`/`type:` 失败，而是**静默收成空**
      ⇒ 230/230 全空才发现 ⚠️ ⇒ **这次的写法明显更强** ✓
```

## 3. 抽取读数（可复算）

```
`Dungeons/*.bytes` **7 个文件** ⇒ 头部 + **18 个 `mash` 段 / 744 个条目** + `props` 段条目 ✓

逐文件：
| 文件 | id | hall_variants | room_variants | mash 段 | 条目 |
|---|---|---|---|---|---|
| `Cove` | 0 | 7 | city coral grotto handtree shipwreck temple whale | 3 | 156 |
| `Crypts` | 1 | 7 | altar barrels drain empty entrance library torture | 3 | 174 |
| `Weald` | 3 | 10 | clearing corruptedcabin crypt gate poisonriver shroomland effigy_0 effigy_1 | 3 | 173 |
| `Warrens` | 2 | 7 | duct effigy ghetto grate meatlocker shrine sluice | 3 | 169 |
| `Darkest` | 4 | 1 | temple | 1 | 43 |
| `Town` | 5 | 5 | altar square start | 1 | 21 |
| `Shared` | 100 | 1 | heartroom secretroom starfield | 4 | 8 |
✅ 头部字段 **7/7 全有**（`id` / `is_released` / `hall_variants` / `room_variants`）✓
```

## 4. 🎖️ 条目 kind：**13 种，分属两段**

```
**`mash` 段**（遭遇 · 5 种 · 744 条）：
   `hall` **355** · `room` **231** · `named` **70** · `stall` **61** · `boss` **27** ✓
**`props` 段**（内容 · 6 种）：★ 我方**完全没有**这一块 ⚠️
   `hall_curios` **66** · `room_curios` **27** · `room_treasures` **18** ·
   `traps` **6** · `obstacles` **6** · `secret_room_treasures` **5** ✓
📌 加上段名 `mash`/`props` 与 4 个头部字段 = 13 个前缀 ✓
```

## 5. 🎖️🎖️ 与 A4 的**交叉校验**：`mash` 段的 **186 个怪物名全部命中**

```
**`mash` 段**引用的怪物名**去重 186 个** ⇒ 拿 A4 的 `monsters_from_ref.json`（230 条）逐个查：
   🔴 **未命中 0** ✓
⇒ 🎖️ **这是 A4 的一次独立验证**：**地牢的遭遇表与怪物表完全对得上** ✓
   📌 而 `props` 段引用的是**奇物/宝藏/陷阱/障碍**（**去重 57 个**）——
      ⚠️ **它们不在 A4 的怪物表里**（A4 只管怪物）⇒ **要另找奇物表** ✓
      📌 而 `PLAN_adoption §9①` 已记：**奇物在 `Curios/Curios.csv`（60 条）** ⇒
         A 系列**缺一项 csv 形态**（我方现有抽取器只处理 json/bytes/txt）⚠️
⇒ ✅ **校验方法本身也值得记**：**"按段分开验"** ——
   我第一版把两段的 `types` 混在一起验 ⇒ **必然出现假命中/假未命中** ⚠️
```

## 6. 🔴 `Maps/*.bytes` 是二进制 ⇒ **A12 只能作参考**（与预案一致）

```
7 个文件全部是 Unity 二进制序列化（长度前缀 + NUL + 内嵌可打印串）✓
   实测能看到的可打印串：`darkestdungeon1` · `entry` · `plot…` · `room:16/11` · `room:1/11` ·
      `weald` · `town` · `crypts` · `effigy_0` · `star…` ✓
   ⇒ 🔴 与 `PLAN_adoption §9⑤` 的判断**一致**：**是"已烘焙布局"，与我方参数化
      `expedition_map.json` 语义不对等** ⇒ ✅ **只能作参考，不能当数据源** ✓
📌 我方现状：`darkest/data/expedition_map.json`（1,317 B）+ `expedition_nodes.json`（1,809 B）·
   **40 个文件**引用 `expedition_map`（含 `ExpeditionMapConfig.cs` 与 10+ 个用例）✓
```

## 7. 诚实边界

```
✅ **能验**：两目录形态判定 · 7 文件头部 · **18 mash 段 / 744 条目** · 13 种 kind 分布 ·
   **mash 段 186 个怪物名全部命中 A4** · props 段 57 个名字（另表） —— **全部当场跑出** ✓
   🎖️ 并**两次 fail-fast**（硬编码 4 种 kind · 把 `props` 当裸列表）都留了痕 ✓
🔴 **不能验**：**没有落任何地牢数据** ⇒ "用参考遭遇表跑图会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：`props` 段的 **57 个奇物/陷阱/障碍名【未与任何表校验】**
   （我方无奇物表、参考的 `Curios/Curios.csv` 未抽）⇒ 记为**未验** ✓
🔴 **不能验**：`Maps/*.bytes` 的**内容未解**（只读了可打印串）⇒ **刻意不深挖**（预案已判"不可用"）✓
🔴 **不能验**：`hall_variants` / `room_variants` 的**语义未查**（推测是"走廊/房间外观变体"）⚠️
   ⇒ 记为**推断**，不是结论 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a12_*.py` **不入库**（scratch）✓
```

## 8. 下一步

```
✅ **A12 抽取完成**（`Dungeons/` 可抽 · `Maps/` 不可抽，与预案一致）✓
🆕 **新暴露的一项缺口（可做）**：🔴 **奇物表** ——
   ① 参考侧：`Curios/Curios.csv`（**60 行 / 18 列**，`PLAN_adoption §9①` 已记）
   ② 我方侧：`darkest/data/curios.json`（**只有 7 条**）⚠️
   ③ 而 `props` 段的 **57 个名字**要它来校验 ⇒ 📌 **这是 A12 的直接下游** ✓
   ⇒ ✅ **下一轮做它**（含 csv 形态的解析器 —— 我方现有抽取器**只处理 json/bytes/txt**）✓
⏸️ **等策划**：三张单（Σ/A6/A9+A10+总口径）已投递 ✓
```
