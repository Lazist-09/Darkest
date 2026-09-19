# `assets/heroes/` —— **正式英雄资产**的落点（原创 · 进 git · 可发布）

> 🔴 本目录是**正式路径**。运行时会**优先**读这里：`assets/heroes/<archetype>/hero.json`
> （见 `darkest/scripts/data/HeroArtResolver.cs`：① 本目录 → ② `assets/heroes_placeholder/<archetype>/`（占位）→ ③ 旧单包占位 → ④ 都没有则点名回落）
> ⚠️ 所以：**只要这里出现某个原型的 `hero.json`，那个原型就立刻改用正式资产**（占位被绕过 ✓）

## 1. 目录形状（一个原型一个目录，名字 = `units.json` 里的原型 id）

```
darkest/assets/heroes/<archetype>/          # archetype ∈ warrior / tank / medic / commissar
├── hero.json                               # 描述符（见下 §2；唯一入口）
├── portrait.png                            # 名册头像（`hero.json` 的 `portrait` 字段引用它）
└── sprite/
    ├── idle.png        # 必备 5 槽之一
    ├── combat.png      # 必备
    ├── attack.png      # 必备
    ├── defend.png      # 必备
    ├── walk.png        # 必备
    ├── afflicted.png   # 可选（折磨态；状态类）
    └── camp.png        # 可选（扎营）
```

## 2. `hero.json` 模板（**照抄即可**；去掉以 // 开头的说明行）

```jsonc
{
  "archetype": "warrior",                  // 🔴 必须与目录名一致，且是 units.json 的玩家原型 id
  "portrait": "portrait.png",              // 相对本目录（也允许子路径）
  "actions": {
    // 🔴 frames 是【引用】不是内联（P31 ①）；相对本目录解析
    "idle":   { "frames": ["sprite/idle.png"],   "fps": 8,  "loop": true,  "anchor": { "x": 0.5, "y": 1.0 } },
    "combat": { "frames": ["sprite/combat.png"], "fps": 8,  "loop": true,  "anchor": { "x": 0.5, "y": 1.0 } },
    "attack": { "frames": ["sprite/attack.png"], "fps": 12, "loop": false, "anchor": { "x": 0.5, "y": 1.0 } },
    "defend": { "frames": ["sprite/defend.png"], "fps": 8,  "loop": true,  "anchor": { "x": 0.5, "y": 1.0 } },
    "walk":   { "frames": ["sprite/walk.png"],   "fps": 8,  "loop": true,  "anchor": { "x": 0.5, "y": 1.0 } }
  }
  // 若某槽本期确实没有：**必须显式声明**，不留静默默认（P31 ②）：
  // "afflicted": { "frames": [], "missing_reason": "原创资产尚未产出该动作 ✓" }
}
```

## 3. 硬规则（加载器 `HeroAssets.Parse` 是唯一真相；这里只是摘要）

```
① **`frames` 必须是引用**（路径或 id），不得内联定义（P31 ①）
② **缺动作槽 ⇒ 显式声明**（写 `missing_reason`），不留静默默认（P31 ②）
③ 🔴 **每个"有帧的槽"必须写 `anchor`，或显式写 `"anchor": "inherit"`**（P31 ③）
   —— 锚点不一致会产生"肉眼才发现的错位/抖动"；一"省"就得猜，而猜错不在任何判据的红灯里
④ **必备 5 槽**：`idle` · `combat` · `attack` · `defend` · `walk`（缺 ⇒ 启动报错并点名 ✓）
⑤ 🔴 **正式路径里不得出现占位**：本目录的 `hero.json` **不得**带 `"placeholder": true`
   ⇒ 打包核对 `python tools/dsh/verify_hero_placeholders.py` 会把它判红 ✓
```

## 4. 占位（`assets/heroes_placeholder/**`）与这里的关系

```
· 占位是【一次性的接口验证载荷】：来源 = Steam Workshop mod（**无授权**）⇒ **不进 git、不进 `resources/**`、不发布**
  ⇒ 它只服务"证明接口能装下一个真实英雄"（见 `doc/modules/hero_assets.md` §1/§4 + `reports/hero_placeholder_manifest.md`）
· ✅ **换正式资产的操作**：把原创资产按 §1 放进 `assets/heroes/<archetype>/` 并写好 `hero.json`
  ⇒ 运行时会**自动优先**用它（占位被绕过）✓ 不需要改任何代码 ✓
· ⚠️ **绝对不要**把占位图拷进本目录来"先跑起来"—— 那会把它带进 git 与发布包（合规红线 ✗）
```
