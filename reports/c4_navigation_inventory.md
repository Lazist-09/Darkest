# C4 导航清单（形态 B：战斗不再是场景）—— **改动依据**（只读盘点 · 2026-09-21 实测）

> 🔴 **用途**：架构 `B-1/B-2/B-3/C4` 的落点与**归属**一次看清（我域 / UI 域 / 在飞）
> 🔴 **本清单不含改动**：它只列**调用点、形态、归属、状态**，用来给架构派工与判断"还差哪几屏" ✓

## 1. 导航调用点（全树实测 · 共 6 处 + `project.godot`）

| # | 位置 | 形态 | 现状 | 归属 | 形态 B 下的目标 |
|---|---|---|---|---|---|
| 1 | `scripts/gameplay/scene/BattleRoot.cs:450` | **面板** | `shell.ShowPanel<HamletRoot>(path)`（**已接** ✓） | 主程序（**在飞 `M`**） | 保持；并补 **B-1**：让**战斗**也成为 panel（现在是**场景根** ⇒ 两套驱动同活 ⚠️） |
| 2 | `scripts/gameplay/scene/BattleRoot.cs:457` | 场景（回落） | `change_scene_to_file(path)` | 主程序（在飞 `M`） | **B-3 留痕**：每次回落打印（现在是本行 + 一行 print ✓ 但**无计数**） |
| 3 | `scripts/gameplay/scene/DungeonRunDriver.cs:67` | 场景 | `change_scene_to_file(HamletScene)` | 主程序（**clean ✓**） | 与 #1 同源：改走外壳（**待 S4 口径**） |
| 4 | `scripts/gameplay/scene/DungeonRunDriver.cs:140` | 场景 | 同上（另一条回城路径） | 主程序（clean ✓） | 同上（**两条路径必须一起改**，否则又是一条能走一条不能） |
| 5 | `scripts/ui/HamletRoot.Build.cs:375 / :530` | 场景 | `ChangeSceneToFile(BattleScene)` ×2 | 🔴 **UI 域** | 进地牢 ⇒ 由外壳装 `BattleRoot` 为 panel（**S4 后**） |
| 6 | `scripts/ui/MainMenuRoot.cs:276 / :282 / :310` | 场景 | `change_scene_to_file(...)` ×3 | 🔴 **UI 域** | 主菜单三条 ⇒ 外壳导航 |
| 7 | `darkest/project.godot:14` | 启动 | `run/main_scene="res://scenes/main/MainMenu.tscn"` | 主程序（autoload 归我） | 🔴 **终态**：改成 `res://scenes/ui/ui_root.tscn` **并撤掉 `[autoload] UIRoot`**（否则两份外壳） |

## 2. 判据（**已做成可执行检查**，不再只写在散文里）
```
🆕 `tools/dsh/check_ui_shell_singleton.py`
   ① `main_scene == res://scenes/ui/ui_root.tscn`（外壳即入口）
   ② **没有** `[autoload] UIRoot="*res://scenes/ui/ui_root.tscn"`（否则 = 第二份外壳）
   ⇒ 两条都满足 ⇒「**外壳实例数 = 1**」✓（架构的原话判据）
   默认模式 = **信息型**（随时可跑，exit 0）· `--strict` = S4 声明完成后再接进 CI（那时未达即红）
```
## 3. C4 冒烟侧要改什么（我域：`SmokeScript.cs` **clean ✓**）
```
🔴 现在冒烟步骤用**场景名**导航（`--smoke=main:1,town` 之类）⇒ 形态 B 下"**战斗不再是场景**"
   ⇒ 需要：**步骤类型化**（scene 步 vs panel 步）＋ **每步后断言"当前屏是谁"**（而不是"当前场景是谁"）
   ⚠️ 但 `SmokeScript.Step` 的调用点在 `BattleRoot.cs`（**在飞 `M`**）⇒ 我**先不动**，等它脱手 ✓
✅ 我能在**不碰在飞文件**的前提下先做的：**步骤解析的单元测试**（纯内核、无 Godot）＋ 本文档 ✓
```
## 4. 与 B-2 绘层的关系（不是我能单独定的）
```
架构 B-2：`ui_root.tscn` 根是 **`Control`**、autoload 又**早于当前场景** ⇒ 当前场景会画在它**之上**
   ⇒ 需要把外壳放到 **`CanvasLayer`（或层号）** ⇒ ⚠️ **那是 `scenes/**` = UI 域** ⇒ 由 UI 设计师做 ✓
   ⇒ 我这边提供**判据**（`check_ui_shell_singleton.py` 与"三层都在当前场景之上"的说明），但不改场景文件 ✓
```
