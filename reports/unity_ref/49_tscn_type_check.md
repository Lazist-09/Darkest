# `.tscn` 类型检查：`GetNodeOrNull<T>` 的 T —— **138 条受检 · 4 条"不匹配"全是假报**

> 🕒 2026-09-26 · 补齐 `47_tscn_node_audit.md §6` 的"未比类型" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 📊 检查方法

```
① 从 **31 个 `.tscn`** 收 `[node name="X" type="Y"]` ⇒ **434 条**（去重名 367）✓
② 从代码收 `GetNodeOrNull<T>("path")` ⇒ 取末段为节点名 ✓
③ 只查 **`.tscn` 里确实存在**的节点（**运行时创建的已由 `47_*.md` 处理**）⇒ **138 条受检** ✓
④ 用**继承表**判兼容（`VBoxContainer` 是 `Control` 等）✓
```

## 2. 📊 结果：**138 条受检 · 4 条报"不匹配"**

| 位置 | 代码要 | `.tscn` 里是 |
|---|---|---|
| `gameplay/scene/BattleRoot.cs:410` | `BattleUI` | `Control` |
| `gameplay/scene/BattleRoot.FlowBridge.cs:27` | `BattleUI` | `Control` |
| `gameplay/scene/BattleRoot.PlayerActions.cs:70` | `BattleUI` | `Control` |
| `ui/UIRoot.cs:86` | `OverlayLayer` | `Control` |

```
🔴 **若不深究，会写成"4 处类型不匹配"** ⚠️ —— **而那是错的** ✓
```

## 3. 🎖️🎖️ 查证后：**4/4 全是【脚本类】，`type="Control"` 是正确的**

```
🎖️ **`BattleUI`**（`ui/BattleUI*.cs` · **13 个 partial 文件**）：
   `partial class BattleUI : Control` ⇒ **基类是 `Control`** ✓
   `.tscn` 里：`[node name="BattleUI" type="Control" parent="UILayer"]`
   ＋ `[ext_resource type="Script" path="res://scripts/ui/BattleUI.cs" id="2_ui"]` ✓
🎖️ **`OverlayLayer`**（`ui/OverlayLayer.cs`）：
   `partial class OverlayLayer : Control` ✓
   `.tscn` 里两处：`overlay_layer.tscn`（**定义场景**，`type="Control"` + script）
     与 `ui_root.tscn`（**实例化**，`instance=ExtResource("2_ov")`）✓
⇒ 🎖️ **即：Godot 场景里【自定义脚本类】的节点写成 `type=<基类>` + 另挂 `script`** ✓
   ⇒ 📌 **`GetNodeOrNull<BattleUI>()` 是【正确的写法】** ——
      运行时 Godot 会把脚本挂上 ⇒ **节点确实是 `BattleUI` 实例** ✓
```

## 4. 🎖️ 判据（本件新增，第 10 条）

```
🎖️ **"这个类型是【引擎内置类】还是【项目脚本类】？"** ——
   · **引擎内置**（`Control`/`VBoxContainer`/`ProgressBar`…）⇒ `.tscn` 里**直接写它** ⇒ 可比 ✓
   · **项目脚本类**（`BattleUI`/`OverlayLayer`…）⇒ `.tscn` 里写的是**基类名** ⚠️
     ⇒ 🔴 **拿脚本类名去比 `.tscn` 的 `type` ⇒ 必然"不匹配"** ⇒ **假报** ✓
   ✅ **正确做法**：**去 `.tscn` 找 `script=` 的 `ext_resource`**（或看 `.cs` 的基类）✓
📌 **与 §3 那条同族**（"名字有几种来源"），这次是**"类型有几种写法"** ✓
   🎖️ **共同形状**：**同一个东西在【两个地方】用【两套名字】** ——
      而**我的比对只认其中一套** ⇒ 误报 ✓
```

## 5. 🎖️ 所以这一块的总账：**138 条全部正确**

```
📊 **Step 5 的两项检查合起来**：
   · **`47_*.md`**：140 条引用 ⇒ **8 条"节点名不存在"** ⇒ ✅ **全是运行时创建**（非缺陷）✓
   · **本件**：138 条受检 ⇒ **4 条"类型不匹配"** ⇒ ✅ **全是脚本类**（非缺陷）✓
   ⇒ 🎖️ **即：我方 UI 的"代码 ↔ 场景"接线【完全正确】** ✓
      📌 **这是个正面结论**（与前面几件同类：`tree_id` 155/0 · `skill_id` 7/7 · 亮度 5 档）
      ⇒ ✅ **而正面结论也要报**（否则会让人以为处处是坑）✓
```

## 6. 诚实边界

```
✅ **能验**：**434 条 `.tscn` 节点 type** · **138 条受检引用** · **4 条报不匹配** ·
   **`BattleUI` 的 13 个 partial + 基类 `Control`** · **两处 `ext_resource` 的 script 路径** ·
   **`OverlayLayer` 的"定义场景 + 实例化"两处** —— **全部当场跑出** ✓
🎖️ 并**避免了一次误报**（4 处"类型不匹配"实为"脚本类"）✓
🔴 **不能验**：**继承表是【我手写的简化表】**（只列了本次用到的 15 个类）⇒
   ⚠️ 若某个节点用了**我没列的类型** ⇒ 会**漏报**（不是误报）⇒ 记**可能不全** ✓
🔴 **不能验**：**运行时的实际挂载未验证**（`ext_resource` 在不在、脚本是否真挂上）⇒ 记**未测** ✓
🔴 **不能验**：那 **434 条里未受检的节点**（代码没引用的）⇒ 本件不涉及 ✓
🔴 **不能验**：**没有改任何代码/场景** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/node_type*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **UI 接线这条线【查完】**：节点名 + 类型，两项都过 ✓
🆕 **可做**：① 抽 `Upgrades/Buildings` 完整表（A9 换源前置 —— A9 是"我方==一手、参考==0/99"那块）
   ② 把第 10 条判据补进 `observe_list` D11
   ③ 核 A9 的 99 等级与我方逐值（**已知"全部不同"** ⇒ 换源要动 99 个值）✓
⏸️ **等策划**：八张单已投递 · **窗口里【无回复】** ⇒ 我继续做不依赖裁定的抽取/核对 ✓
```
