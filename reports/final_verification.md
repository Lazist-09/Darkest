# 终检快照 · 主程序（2026-09-21 · **当场重测**）

> 🔴 **本文件 = 终检的读数快照**（不是计划 ✓）。逐项都可复跑 ✓
> 配套：`INDEX_lead_programmer.md`（交付索引 · 每项读数 + 提交号）· `HANDOVER_lead_programmer.md`（交接）

## §1 六项硬读数
| # | 项 | 命令 | **读数** |
|---|---|---|---|
| 1 | 构建 | `dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore -m:1 -nodeReuse:false -tl:off -v:q` | **0 错误**（警告 198）✓ |
| 2 | 全量单测 | `dotnet test Darkest.Tests.csproj … -v:n` | **测试总数 809 · 通过 809 · 失败 0** ✓ |
| 3 | 文件规模门禁 | `python tools/check_file_size.py` | **OK: all 425 scanned program files <= 600 lines (0 allowlisted)** ✓ |
| 4 | B6 外源门禁 | `python tools/check_no_external_assets.py` | **OK**（扫描面内无 E 盘/原版来源标记 · **0 条白名单**）✓ |
| 5 | godot 引用门禁 | `python tools/check_godot_refs.py` | **OK: 0 hits**（内核零 Godot ✓ · `System.IO` 在 ui/scene 也 0 ✓）✓ |
| 6 | 数据纪律 | `python tools/check_data_discipline.py` | 三扫合计可疑 **41 处**（⚠️ **供人判读的清单，不是自动判罪** ✓ 结构性常量可入白名单，平衡数字必须搬 `data/*.json` ✓）|
| ＋ | 拆分完整性 | `python tools/dsh/audit_split_integrity.py --baseline … --parts …` | 两批实测**零缺失**（`b94947d` · `41272b3` ✓）；工具本身含 `--selfcheck` ✓ |
| ＋ | 布局判据 | `--ui-audit`（冒烟用例里跑） | **真错误 0** ✓（静态版明确"运行时才是权威" ✓）|

## §2 冒烟（本机 **Godot 4.6.1 mono** 真跑 · 13 例）
```
✅ **13/13 例「真错误 = 0」**（entry-main1 · topology-auto · hamlet-next · e2e · map-mode · town-step ·
   ui-audit · dungeon-in-scene · tile-walk · battle-panel-nav · battle-panel · abandon …）
✅ `PROBE-EXIT bad=0 code=0` · 总退出码 0 ✓
⚠️ 三例带「引擎退出泄漏」（hamlet-next 2 / e2e 3 / town-step 2）—— **已查清**：全是**主题/字体资源**
   （`FontFile`/`DPITexture`/`StyleBoxFlat` ✓ 没有我们的对象 ✓）⇒ 归属 **UI 域** ⇒ 见 `smoke_leak_forensics.md` ✓
```

## §3 三列定稿（**做完 / 没做 / 等裁定**）
### ✅ 做完（我域 · 有读数 + 提交号 ⇒ 见 `INDEX_lead_programmer.md`）
```
P0-1 六件全真拆（白名单清零）· P0-2 形态 B 我域那一半（B-1 面板 + C4 导航 + B-2/B-3/C4终态判据 + 一键状态）·
B6 门禁 · DD1：M5 170 条 / M4 196 条 / M6 8-20-99 / M7 三步 / M8 15-135-645 / M9 影响面+6 槽回归+改名 /
M1a tier+三轴+prot 归位 / M1c 阶段 1-3 机制+就绪度 / M2 激活 1 条+前提发现 / M3 第 1 步 7 条 ✓
＋ 工具与审计：拆分完整性 · 孤儿批核准表（237 文件）· 无注释整类体清单 · 泄漏取证 · 交付索引 ✓
```
### 🔴 没做（**有明确前置，不是我漏了**）
```
① UI 域三件：外壳 `CanvasLayer` 抬层 · hamlet 等屏面板化 · `main_scene`→`ui_root.tscn` + 撤 autoload（**同批**）
② M3 第 2 步（带行为 · 8 消费点）：建议与 ②③ 口径一并确认后**一次做完** ✓
③ 注释审校：`ExpeditionFlow` 那批的"**为什么**"真的丢了 ⇒ 只能补"这段在做什么" ✓
```
### ⏳ 等裁定（**数值/平衡一类，我一律不动**）
```
① 技能 `dmg%` 23 条（策划已给 2 条试点：`smite 0%` / `zealous_accusation −40%` ✓）
② `prot` 对齐原版（四原型减伤 8/12/4/5 → **0**）⇒ 入解冻清单 ✓
③ **阶数进入伤害**（架构已裁"要" ⇒ 两步走：数据已能表达阶 ✓ / 伤害读阶待落地 ✓）
④ `def` 平衡项（(丙) 结构已做 ✓ (甲/乙) 交策划 ✓）· 4v4 名单 4 人 + 是否保留待命位
⑤ `O-95` 4v4 技能 SP 剂量 ⇒ 入解冻清单 ✓
⑥ M2 余 6 条（架构已裁 **(乙) 留冻结**、等 M4 需求驱动 ✓）
```

## §4 安全网
```
备份 **4 份**：`Darkest-backup-20260921_000640`(4376) · `_011818`(4415) · `_124311`(4474 全量) ·
`_003105-expflow`（专项：拆分前快照 + units/UnitStats/UnitsConfig 的 prot 归位前快照 ✓）
我域在飞 **0** ✓（其余为策划/架构/UI 正在写的文档 ✓）
```

---

## §8 末次复跑（2026-09-21 23:47 · R87 终检 · **当场取真值**）
| # | 项 | **读数** |
|---|---|---|
| 1 | 构建 | **0 错误** ✓ |
| 2 | 全量单测 | **809 / 809 · 失败 0** ✓ |
| 3 | 文件规模门禁 | **OK: all 425 scanned program files ≤600 · 0 allowlisted** ✓ |
| 4 | B6 外源门禁 | **OK**（0 allowlisted）✓ |
| 5 | **数据纪律** | **三扫合计 0 处 · 退出码 0** ✓ ← 🎉 **从 41 处降下来的**（R85 逐条判读 + 带理由登记 ✓ 见 `data_discipline_triage.md`） |
| 6 | godot 引用门禁 | **OK: 0 hits** ✓ |
| 7 | 外壳单例门禁 | `NOT-YET (informational)` ⚠️ = **已知的 UI 域项**（S4 终态落地后要转 strict ✓）|
| 8 | **冒烟 13 例** | **13/13 全绿**：「真错误 0」逐例 ✓ · `PROBE-EXIT bad=0 code=0` · 总退出码 **0** ✓（留档 `reports/smoke_summary_20260921_2347.txt`）|

**结论**：**我域可自证的部分全绿** ✓；唯一 `NOT-YET` 是**工具明说 informational** 的那条（外壳单例 ⇒ 属 UI 域 ✓）。
