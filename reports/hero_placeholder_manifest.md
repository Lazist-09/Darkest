# 占位英雄清单（**本地资产 · 不进 git**）

> 🔴 **合规（契约 `doc/modules/hero_assets.md` §4 三条）**：①不进 git ②**不进 `resources/**`**（会被导出打包）③显式标注 `placeholder:true` + `source`
> 实测：`borrow/` 与 `darkest/assets/heroes_placeholder/` 的**被跟踪文件均为 0** ✓（`.gitignore` 已覆盖 ✓）
> ⚠️ 本文件只记录**清单与来源**（**不含任何素材字节**）⇒ 换机器/换人可照此重建 ✓

## 1. 生成方式（**读到的流水线** —— 用字节数逐一比对证明，不是猜的）
```
占位动作图 = 原样拷贝自：
  borrow/<mod>/heroes/<hero>/<hero>_A/anim/<hero>.sprite.<动作>.png      ⇒ <slot>.png
头像       = borrow/<mod>/heroes/<hero>/<hero>_A/<hero>_portrait_roster.png ⇒ portrait.png
（例：现有 attack.png ← …sprite.attack_gunfire.png ✓ 字节数完全一致；portrait.png ← playwright_portrait_roster.png ✓）
```

## 2. 按【我们的原型】生成的四份占位（`darkest/assets/heroes_placeholder/<archetype>/`）
| 原型 | 来源 mod | 来源英雄 | `attack` 槽取用的变体 | 文件 |
|---|---|---|---|---|
| `warrior` | 3440502939 | playwright | `attack_gunfire` | portrait + 7 动作 ✓ |
| `tank` | 3347307257 | buruaka_mine | `bash` | portrait + 7 动作 ✓ |
| `medic` | 3366899368 | snor_ui | `attack_artillery` | portrait + 7 动作 ✓ |
| `commissar` | 3424145711 | snor_wakamo | `shoot_base` | portrait + 7 动作 ✓ |

```
七个动作 = idle · combat · walk · defend · attack · afflicted · camp（契约 §3 的最小集 ✓）
每份 hero.json：`archetype` 用**我们的原型名** ✓ · 每槽**显式 anchor** {x:0.5,y:1.0} ✓ ·
                `placeholder:true` + `source`（写明 mod 编号 + 「无授权，仅本地占位，不发布」）✓
```

## 3. ⚠️ 映射是**占位载荷**，不是设计语义（可随时换）
```
四个「原型 ← 英雄」的搭配**不含语义**（目的只是"让四个原型各自一张图"）⇒
  换人只需替换对应目录里的 png + 改 hero.json 的 source ✓
`attack` 槽：五家 mod 都**没有**同名 `sprite.attack.png` ⇒ 只能按品种挑一个变体（上表已写明取的是哪个 ✓）
另有一个未用的英雄：`snor_kikyou`（mod 3460837438 · 16 槽：含 afflicted/camp/heroic/investigate 等 ✓）
```

## 4. 未接入的部分（如实标注，不粉饰）
```
· `fps` / `loop` 目前**无人消费**：契约 §3 已裁「V6 只证明【接口能装下 + UI 能显示】，**不证明动画能播**（Spine 本阶段裁掉）」
  ⇒ 它们属【说明 / 预留】字段（按"字段四分类"规矩已标注）✓
· 图像本身是 **Spine 图集页**（一整页一张 png）⇒ 现在 UI 显示的是**整页**，不是逐帧动画 ✓
· 正式（原创）资产目录 `assets/heroes/<archetype>/` 目前**为空** ⇒ 解析器会回落到占位并**显式说明"这不是正式资产"** ✓
```

## 5. 运行时解析（已实现，见 `darkest/scripts/data/HeroArtResolver.cs`）
```
① assets/heroes/<archetype>/hero.json               = 正式（原创）
② assets/heroes_placeholder/<archetype>/hero.json   = 占位（按原型 · 本轮新增布局）
③ assets/heroes_placeholder/hero.json               = 占位（旧单包 · 兼容早期"1 个英雄验证接口"）
④ 都没有 ⇒ None + **点名**（调用方回落"色块 + 首字"，**不静默** ✓ 红线 21）
```
