# 架构拆分·交付入口（architecture/README.md）

> **编号**：ARCH-INDEX · **类型**：entry · **状态**：草案 v0.1（等待 O 项拍板后转定稿）
> **上游**：[设计] doc/README.md（策划交付入口，含 M1~M6 实现顺序与 10 条开放题）· 本套 _conventions.md
> **最近更新**：2026-09-09 · **维护**：架构师（交付清单在 §5 留痕，禁止并行编辑）

**一句话定位**：本目录是「策划交付 → 可落地代码」的**技术拆分层**。策划文档（`doc/`）回答"做什么、为什么"；
本套文档回答"拆成哪些系统、数据长什么样、按什么顺序做到什么算过"。主程序从**本文件**起步，按 §3 看板推进。

---

## 0. 权威性指引（别读错层）

| 主题 | 唯一权威 | 说明 |
|---|---|---|
| 字段级数据（JSON key / C# 类型 / 数值出处） | `data_schema.md` | 未列字段禁止自行加入（data_schema §0） |
| 开放题 / 挂起清单 | `open_issues.md` | O-01~O-10 = 策划开放题；O-11~O-34 = 架构/实现侧 |
| 总体架构（目录/分层/接口/确定性/测试钩子） | `blueprint.md` | 与 data_schema 冲突时：字段级以 data_schema 为准 |
| 玩法/数值本身 | `doc/`（策划层） | 冲突时以 `doc/modules/` 与 `doc/state.md`（#N）为准 |
| 写作规范（引用/模板/铁律） | `_conventions.md` | 所有写作者必须先读 |

> 交叉引用写法：`[设计] doc/modules/xxx.md §y`（玩法规则）、`#N`（state.md 决策）、`O-nn`（开放题）、`T-Mm-nn`（任务卡）。

---

## 1. 文档地图（本套 10 个文件）

| 文件 | 类型 | 职责 | 必读 |
|---|---|---|---|
| `_conventions.md` | 规范 | 产出约定 / 命名 / 任务卡模板 / 目录铁律 | ✅ 全员 |
| `blueprint.md` | blueprint | 总体技术蓝图：五层边界、目录树、4 张 Mermaid、事件流、数据管线、确定性设计、接口契约、测试钩子、风险 | ✅ 主程序 |
| `data_schema.md` | schema | `res://data/` 7 个 JSON 的字段级 Schema + C# 模型 + 校验 P1~P10 + JSON 示例 | ✅ 主程序 |
| `open_issues.md` | issue | 开放题 O-01~O-34：策划拍板 / 架构裁定 / 实现期待定 | ✅ 全员 |
| `tasks/m0_bootstrap.md` | task | M0 工程引导：5 张卡（T-M0-01~05） | ✅ 先做 |
| `tasks/m1_formation.md` | task | M1 阵型骨架：5 张卡（T-M1-01~05） | ✅ |
| `tasks/m2_combat_core.md` | task | M2 结算核心：10 张卡（T-M2-01~10） | ✅ |
| `tasks/m3_skills_units.md` | task | M3 技能与角色：8 张卡（T-M3-01~08） | ✅ |
| `tasks/m4_morale_survival.md` | task | M4 士气与生存：9 张卡（T-M4-01~09） | ✅ |
| `tasks/m5_enemy_ui.md` | task | M5 敌人与 UI：9 张卡（T-M5-01~09） | ✅ |
| `tasks/m6_verification.md` | task | M6 验收：7 张卡（T-M6-01~07） | ✅ 收口 |
| `README.md` | entry | 本文件：入口 + 看板 + 交付清单 | ✅ 全员 |

> 任务卡合计 **53 张**（5+5+10+8+9+9+7）。

---

## 2. 与策划文档的对应（M0 为架构新增）

| 里程碑 | 策划依据（doc/） | 架构文档 | 一句话完成判据 |
|---|---|---|---|
| **M0 工程引导** | —（架构新增，承接 blueprint §2/§3/§12 风险 8） | blueprint §2/§3/§10/§11；tasks/m0 | `darkest/` 在 Godot 4.6 .NET 打开即用，`dotnet build`+`dotnet test` 通过，`using Godot` 白名单 0 命中 |
| M1 阵型骨架 | README §2 M1；formation.md | tasks/m1 | 推一个人能看到整条交换链结果、无空位；死亡靠齐同一时刻完成（formation.md §6 判定式） |
| M2 结算核心 | README §2 M2；combat_math.md | tasks/m2 | 用 combat_math §7.1 八个样例能复算出同样数字；同 seed 同命令流事件日志一致 |
| M3 技能与角色 | README §2 M3；skill.md / skill_data.md / character.md | tasks/m3 + data_schema §3.2 | 导入断言 43 条技能；每角色每位置 ≥2 可用技能（data_schema P3）；灰显原因正确 |
| M4 士气与生存 | README §2 M4；morale.md / buff.md / GDD §3 | tasks/m4 | 走通"受伤→虚弱→死门→死亡/靠齐"全链，仅精神伤害掉士气（#157） |
| M5 敌人与 UI | README §2 M5；enemy.md / ui_spec.md | tasks/m5 | 9 条必显示齐全；位移预览=内核 dry-run（玩家可预判整条交换链） |
| M6 验收 | verification.md（#119~#122） | tasks/m6 | `--runs 300` 胜率落 40~70%、六项 KPI 全达标、手感 3 指标过、无软锁 |

> **硬门槛**：M0 + M1 + M2 未完成前，M3 之后的技能数值没有意义（策划 README §2 口径）。

---

## 3. 任务总看板与实施顺序

```
M0 工程引导 ──► M1 阵型骨架 ──► M2 结算核心 ──► M3 技能与角色 ──► M4 士气与生存 ──► M5 敌人与 UI ──► M6 验收
     ▲              ▲               │                 ▲                 ▲                ▲
     └── 全部任务卡 ─┴───────────────┴──► M3/M4/M5 可依内核就绪后并行推进（依赖见各卡）
```

| 里程碑 | 前置 | 建议并行 | 完成判据位置 |
|---|---|---|---|
| M0 | — | — | m0 §1（5 项） |
| M1 | M0 | — | m1 §1.1~§1.3 |
| M2 | M1 | — | m2（T-M2-10 收口） |
| M3 | M2 | M4（数据导入） | m3 T-M3-02/08 |
| M4 | M2、M3 数据 | M5（数据与 UI 分工） | m4 §里程碑判据 |
| M5 | M2、M3、M4 | — | m5 判据 M-A~M-D |
| M6 | M0~M5 全部 | — | m6 §1 三组判据 A/B/C |

> 依赖纪律：所有任务卡内的随机必须走内核 `IRngProvider` 固定调用点（blueprint §8.2 九层映射表）；
> 任何新增骰子必须回填该表，否则判架构违规。

---

## 4. 实施前必须知道的 8 件事（FAST START）

1. **先读** `_conventions.md`（写作规则）→ `blueprint.md`（架构全景）→ `data_schema.md`（数据字段）。
2. **目录铁律**：res:// 顶层只有 `scenes/ scripts/ data/ resources/ tests/`；`core / gameplay/sim / data` 零 `using Godot`（M0-04 静态检查）。
3. **数据唯一落点**：`res://data/` 七个 JSON（data_schema §1）；数值不当即改，走 `--override` 覆盖集，不改代码。
4. **确定性**：每场一个 seed；固件层 9 层随机在固定调用点（blueprint §8.2）；headless 与实机共用同一 BattleDirector（M6 前提）。
5. **位移预览 = 内核 dry-run**：预览与结算同源（blueprint §5d），禁止 UI 自己推演"只推 1 格"。
6. **UI 是机制的一部分**：9 条必显示信息（ui_spec §2）缺一不可，尤其行动序列 / 撤退成功率数字 / 灰显原因 / 位移预览。
7. **开放题处置**：任务卡内遇到的歧义先查 open_issues.md；未覆盖的 → 挂 O-nn（O-35 起），**不得静默改设计/拍数值**。
8. **先读后写**：任何文件修改前先 read 全文；同文件禁止并行编辑（策划纪律第 4 条）。

---

## 5. 交付清单与留痕（架构师维护）

> 每次新增/修订本套文档，在此追加一行。格式：`YYYY-MM-DD | 文件 | 变更 | 状态`。

| 日期 | 文件 | 变更 | 状态 |
|---|---|---|---|
| 2026-09-09 | `_conventions.md` | 新建：产出规范 | 定稿 v1 |
| 2026-09-09 | `blueprint.md` | 新建：总体蓝图（631 行） | 草案 v0.1（大纲已审校，O 编号已统一） |
| 2026-09-09 | `data_schema.md` | 新建：数据 Schema（638 行，7 配置分片 + P1~P10 校验） | 草案 v0.1 |
| 2026-09-09 | `open_issues.md` | 新建：O-01~O-34 | 滚动维护 |
| 2026-09-09 | `tasks/m0_bootstrap.md` | 新建 5 卡 | 草案 |
| 2026-09-09 | `tasks/m1_formation.md` | 新建 5 卡 | 草案 |
| 2026-09-09 | `tasks/m2_combat_core.md` | 新建 10 卡 | 草案 |
| 2026-09-09 | `tasks/m3_skills_units.md` | 新建 8 卡 | 草案 |
| 2026-09-09 | `tasks/m4_morale_survival.md` | 新建 9 卡 | 草案 |
| 2026-09-09 | `tasks/m5_enemy_ui.md` | 新建 9 卡 | 草案 |
| 2026-09-09 | `tasks/m6_verification.md` | 新建 7 卡 | 草案 |

---

## 6. 交接语（按架构师协议 Loop C）

策划已交付完整设计规格（`doc/`，v0.39，可做垂直切片）；本套技术拆分已就绪。
**主程序可从 `tasks/m0_bootstrap.md` 开始落地**，途中随卡引用对应开放题编号回查 `open_issues.md`。