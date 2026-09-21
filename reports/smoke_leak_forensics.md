# 冒烟「引擎退出泄漏」**查清了**（2026-09-21 · 主程序）

> 🔴 **起因**：我第 75 轮看到冒烟里三例带"引擎退出泄漏"（`hamlet-next` 2 · `e2e` 3 · `town-step` 2 ✓），
>   当时**怀疑"这是我代码路径上的质量债"** ⇒ 本轮**真查了** ⇒ **结论与我的怀疑不同** ✓（如实更正 ✓）

## 1. 泄漏的**确切成分**（`--verbose` 实测）
```
ERROR: 20 RID allocations of type 'TextServerAdvanced::ShapedTextDataAdvanced' were leaked at exit.
ERROR:  1 RID allocations of type 'TextServerAdvanced::FontAdvanced' were leaked at exit.
WARNING: ObjectDB instances leaked at exit (run with --verbose for details).

Leaked instance: FontFile:...    - Reference count: 26
Leaked instance: DPITexture:...  - Reference count: 20
Leaked instance: StyleBoxFlat:... - Reference count: 20   （多条，20~26 ✓）
```
⇒ **全部是【主题 / 字体】资源**（`FontFile` = 字体文件 · `DPITexture` = 字体的 DPI 纹理 · `StyleBoxFlat` = 主题样式盒 ✓）
⇒ 🔴 **没有任何游戏对象**（`Node` / `BattleRoot` / `ExpeditionSession` / `BattleUI` …）出现在泄漏列表里 ✓

## 2. 因此两件事要更正 / 确认
| # | 说法 | 判定 |
|---|---|---|
| ① | 我第 75 轮："**这在我自己的代码路径上**（可能是我的债）" | 🔴 **更正：不是** ✓ —— 泄漏列表里**没有我的对象** ✓ |
| ② | 冒烟脚本把它与"非环境 ERROR"**分开计**、只作信息 | ✅ **它是对的** ✓（判据没错 ✓） |
| ③ | 但它**也不是"纯噪声"** | ⚠️ **引用计数 20~26** ⇒ **有活着的持有者** ⇒ 不是"创建后立刻丢"✓ |

## 3. 泄漏量随什么增长（用既有读数做的**相关性**，不需新跑）
```
`entry-main1`（1 屏：主菜单 → 单场战斗）⇒ **0**
`hamlet-next`（多屏：地牢 → 结算 → 城池）⇒ **2**
`town-step`（城池 + 建筑入口 + 二级菜单）⇒ **2**
`e2e`（全回路：地牢 → 回城 → 花钱 → 再出发）⇒ **3**  ← 屏最多 ✓
📌 **推论（可验证）**：泄漏量 ≈ **本趟创建过的**屏/主题资源数 ⇒ 与"跑了多少屏"**单调相关** ✓
```

## 4. 🔴 归属与建议（**我不越界** ✓）
```
🔴 **持有者最可能是【主题资源的常驻引用】** —— 启动日志里有 `[Theme] 字体：EBGaramond.ttf …` ✓
   ⇒ 也就是说：主题被**某个长生命周期对象**（静态字段 / autoload / 场景单例 ✓）持有着 ✓
🔴 **归属 = UI 域**（`scripts/ui/**` / `scenes/**` / `resources/**` ✓）⇒ **我不动** ✓
✅ **我能提供的**：这份**可复现的取证命令**（下面 ✓）⇒ UI 侧一条命令就能看到同一批数字 ✓
```
```
复现命令（与我看的完全一致）：
   Godot_v4.6.1-stable_mono_win64_console.exe --path darkest --headless --verbose \
        --hamlet-next --quit-after 900 | Select-String 'Leaked instance|leaked at exit'
```
## 5. 这件事的**方法论**价值（比结论本身重要）
```
🔴 我之前把它写成"我代码路径上的质量债" —— 那是**没验证的推断** ✗
   ⇒ 本轮只做了两件事就推翻它：① 抓**确切类型名**（`--verbose`）② 与**既有读数**做相关性
   ⇒ 📌 教训（同族于我这几轮记的几条）：**"看起来像我的问题"和"证据说是我的问题"是两回事** ✓
```
