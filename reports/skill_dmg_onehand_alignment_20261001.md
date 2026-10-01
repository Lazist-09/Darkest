# 技能 `dmg%` 回一手对齐落库（2026-10-01 · `#490` 裁定的执行面）

**裁定**：`reports/planner_20260930_to_lead_skill_dmg_ruling.md`（标记 `DELIVERY-DESIGNER-SKILL-DMG-RULING-20260930` · 已落 `doc/state.md #490`）——
① `warrior_lunge` = **−50** ② `commissar_burst_fire` = **−50** ③ §5 那 23 条冲突**维持只登记不动** ⇒ 🔴 本件**不入 `§39`**（同型先例 `#486`：回一手 = **对齐** ≠ 平衡数值改动）。

## 一、落库（`darkest/data/skills.json` · 仅 4 行 · LF 保持）

| 行 | 改前 | 改后 |
|---|---|---|
| `:86` | `"dmg_pct": -55,` | `"dmg_pct": -50,` |
| `:87` | `"_dmg_pct_source": "ref:Hellion/breakthru (纠正:旧 -50)",` | `"_dmg_pct_source": "dd1:breakthru (一手 .dmg · hellion.info.darkest:34-38)",` |
| `:1374` | `"dmg_pct": -60,` | `"dmg_pct": -50,` |
| `:1375` | `"_dmg_pct_source": "ref:Highwayman/grape_shot_blast (纠正:旧 -50)",` | `"_dmg_pct_source": "dd1:grape_shot_blast (一手 .dmg · highwayman.info.darkest:28-32)",` |

**读数（复跑）**：

- `(Select-String -Path darkest\data\skills.json -Pattern '"_dmg_pct_source": "ref:').Count` ⇒ **6**；同法 `"dd1:` ⇒ **8**（= 裁定要求的 6 + 8 ✓）
- `git diff --stat` ⇒ **1 file changed, 4 insertions(+), 4 deletions(-)**
- 文件字节：**CRLF = 0**（LF 保持）· JSON 解析通过（`json.load` 后两条取回 −50 + `dd1:` 出处）

## 二、「零行为」自证（裁定 §3 三层 · 复跑命令）

| 层 | 命令 | 读数 |
|---|---|---|
| ① 新模型调用点 | `rg -n "WeaponRawDamage" darkest/` | 定义 **1 处** = `BattleMath.cs:173`；调用点 **全在 tests**（`M1cStage3DiffTableTests` ×2 · `M1cPilotComparisonTests` ×6 · `WeaponDamageModelStage1Tests` ×5）⇒ `darkest/scripts/` 内 **0 调用** |
| ② `dmg_pct` 谁读 | `rg -n "DmgPct" darkest/scripts/` | 只有 `BattleMath.cs:171/173/176`（**形参名 + XML 注释** · 与 `skills.json` 无耦合）＋ `SkillsConfig.cs:93/107/114`（**声明**）⇒ **无一处读值** |
| ③ 守卫测试 | `M1cStage3MechanismTests.cs:72-116` | 44 条覆盖 / 14 有出处 / 30 自加 ⇒ 按 `value_source`·`origin` 划分，两字段不动；有出处须 `_dmg_pct_source` 非空 ⇒ `ref:` 改 `dd1:` 后**仍非空**（逐条核过不破） |

## 三、读数（最低限度验证 · 用户约束 2026-10-01）

| 项 | 命令 | 读数 |
|---|---|---|
| 构建 | `dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` | **0 错**（71 警告 · 均既存 nullable / 未用字段） |
| 用例 | `dotnet test darkest/Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 -nodeReuse:false -tl:off -v:q --filter "FullyQualifiedName~M1cStage3MechanismTests|FullyQualifiedName~M1cDmgPctTableTests|FullyQualifiedName~M1cPilotComparisonTests"` | **失败 0 / 通过 7 / 总计 7**（2 s · exit 0） |

⇒ 按用户约束**只跑受影响的 3 个用例类**：未跑全量 · 未跑门禁 · 未跑蒙特卡洛。

## 四、边界与登记（只登记不动）

- **落库提交 = `439a912`**（数据 4 行）· 本报告 + `doc/state.md #497` = 同一链的下一笔。
- 🔴 **历史文本一处不改**：`doc/modules/dd1_baseline.md:3718/3769`（那条「`warrior_lunge` −50 → −55」的纠正记录）· `reports/INDEX_lead_programmer.md:40` · 请单与裁定留档 —— 项目惯例 = **映射 / 裁定为准，历史文本只作更正记录** ⇒ 它们里面的 `ref:` 字面量**留在原地**。
- 📌 裁定 §4 顺带登记的 3 处仍挂（**只登记不动** · 归解冻窗口同批改文本）：`skills.json:2 _dmg_pct_note` = `#473` 旧口径 · `:1800 _sigma_note` 留的偏是 `§61` 已否的「Σ 归一为 1」 · §5 差最远三条（`flare` −100vs0 / `heroic_end` +50vs+150 / `focus` −40vs−90）。
- ✅ 本件结清 ⇒ **无阻塞**；下一件 = `P2`（M1c 阶段 3 切默认 · 走解冻口径四件）。

📄 关联：`reports/planner_20260930_to_lead_skill_dmg_ruling.md` · `doc/state.md #490 / #497` · `doc/architecture/source_priority.md §1b` · `darkest/data/skills.json`

## 五、投递回执（复现 `#494` 的根因 · 一条精确补充）

- 本封回执的投递实测：`python tools/dsh/deliver_letter.py reports/lead_skill_dmg_onehand_landed_20261001.md doc/windows/策划窗口.txt`
  ⇒ 字节 **17414 → 19193**（+1779）· 但工具自报 **`marker: <no marker found>` / `readback hits=0`** —— **不是没投进去**：
  独立字节级复核 `open(...,'rb').read().count(b'DELIVERY-LEAD-SKILL-DMG-LANDED-20261001')` = **1** ✓
- 🔴 机制（对 `#494` 的精确补充）：`MARKER = re.compile(r"^[A-Z][A-Z0-9_-]{6,}$", re.M)` 在 **CRLF 文本**上**永远匹配不到** ——
  行尾的 `\r` 不在字符类 `[A-Z0-9_-]` 里，而 `$` 只认 `\n` 前的位点 ⇒ 整行不匹配 ⇒ 工具落进「`<no marker found>`」分支，
  随后拿**这个字符串**去数 ⇒ **必然 0**（`hits=0` 是**自证式空转**，不是投递失败）✓
  ⇒ 📌 判据（复跑）：以**字节数增量 + 独立 `count(b'MARKER')`** 为准，**不看**工具自报的 `hits` ✓
