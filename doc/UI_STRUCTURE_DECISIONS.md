# 三处结构/归属判定的证据裁定

> 版本 v1 · 2026-09-19 · 依据：E 盘 `.darkest` 源文件逐 section 直读 + `_layout_parsed_v3.json`
> 结论先行：**三处都不需要问你，源文件里全有答案。** 之前"未知"是因为解析器把父级丢了。

---

## 0. 根因：解析器 v2 丢失 section 名（已修复）

| 项 | v2（施工所依据） | v3（修正后） |
|---|---|---|
| 带字段的 section | 88 | **432** |
| 真实命名 section | 34（其余 54 个叫 `_root`） | **432 个全部有真名** |
| 父容器信息 | ❌ 全拍平进 `_root` | ✅ `parent` / `depth` / `line` 齐全 |

**后果举例（这就是你说"父级未知"的原因）**：

| 源文件真实结构 | v2 拍平后 | 造成的问题 |
|---|---|---|
| `heirloom_exchange_layout` → `heirloom_exchange_heirloom_from_layout` / `_to_layout` | 三个 section 合并成 1 个 `_root` | `arrow_offset` 等 5 处 offset 找不到父 ✗ |
| `building_base_layout` / `building_activity_layout` / `building_activity_slot_layout` / `building_store_list_layout` …（11 个独立 section） | 合并成 1 个 `_root` | 建筑屏结构被抹平 ✗ |
| `monster_info`（仅 `textWidth 180`）/ `quest_info`（30 字段） | 张冠李戴 | 施工挂错父节点 ✗ |

**修复后重新判定下面三题。**

---

## 1. 各栋建筑屏结构 → **选 A：每栋建独立 L2 子面板**

### 证据：DD 的建筑是"基座 + 各栋覆盖 + 活动列表"三层组合

`campaign/town/buildings/building.layout.darkest` 逐 section 直读，共 **11 个独立 section**：

```
building_base_layout                 ← 基座：name_pos / body_base_pos / upgrade_base_pos / close_pos
building_base_body_layout            ← 主体：info_text_offset 580,760
building_base_upgrade_layout         ← 升级区：upgrade_trees_offset 0,195 / upgrade_trees_spacing 0,160
building_base_upgrade_tree_layout    ← 升级树节点（guild/camping_trainer 各自覆盖此层）
building_upgrade_requirement_tooltip_layout
building_activity_list_layout        ← 活动列表：base_pos 70,50 / activity_spacing 0,230   ★
building_activity_layout             ← 活动条目：base_size 800,200 / slot_list_pos 440,119 ★
building_activity_slot_layout        ← 槽位：hero_slot_offset / cost_offset / confirm_button_offset
building_choice_activity_slot_layout ← 选项式槽位：choice_list_pos 0,250 / choice_hot_spot_size 120,20
building_store_list_layout           ← 商店列表：base_size 950,800 / store_spacing
building_animation
```

### 关键数据：各栋真的是独立 L2 面板

| 建筑 | 自有 section 名 | 自带尺寸 | 说明 |
|---|---|---|---|
| 坟场 | `dead_hero_layout` / `graveyard_main_layout` | `list_area_size 720×600` / `entry_size 1000×160` | 独立屏 |
| 雕像 | `statue_main_layout` / `statue_entry_layout` | `list_area_size 600×580` / `quote_width 505` | 独立屏 |
| 驿站 | `hero_recruit_store` / `hero_layout` / `add_hero_dialog_contents` / `stagecoach_layout` | `base_size 780×780` / `text_box_size 350×200` | 独立屏 |
| 英雄动作 | `hero_action_layout` / `hero_action_banner_layout` / `hero_action_verbose_layout` | `base_pos 220,44` + `base_size 100×100` | 独立屏 |
| 疗养院 | `quirk_treatment` / `sanitarium_activity_layout` / `sanitarium_activity_slot_layout` | `base_size 800×200` | 独立屏 + 覆盖活动层 |
| 公会 | `guild_layout` / `guild_upgrade_tree_layout` / … | `skill_pos 44,-4` / `skill_spacing 0,91` | 覆盖升级树层 |

**每栋都声明了自己的 `*_layout` 根 section** —— 这就是 DD 的"独立 L2 子面板"结构。

### 裁定 A 与 B 的关系（不是二选一）

你说的 A（独立子面板）和 B（统一内容容器）其实是**同一结构的两层**，DD 两者都用：

```
BuildingPopup (L2 容器 · 基座)
└── BuildingContent  ← B：统一"建筑内容容器"，对应 building_base_body_layout
    ├── GraveyardPanel   ← A：每栋独立 L2 子面板（graveyard_main_layout）
    ├── StatuePanel      ← statue_main_layout
    ├── StageCoachPanel  ← hero_recruit_store
    ├── HeroActionPanel  ← hero_action_layout
    └── ActivityList     ← building_activity_list_layout（共用，activity_spacing 0,230）
```

**所以裁定：按 A 建设，用 B 做外框。** 即：`building_popup.tscn` 保留 `BuildingContent` 作为统一容器（对应 `building_base_body_layout`），每栋的专属块**移出到各自的 `XxxPanel` 子场景**，不再平铺在弹窗根。

### 你标注的 4 个"塞错位置"的块 → 正确归属

| 节点 | 原挂位置 | DD 真实出处 | 正确归属 |
|---|---|---|---|
| `BpGraveyardEntry` 1000×160 | building_popup 根 | `graveyard_main_layout.entry_size` | **GraveyardPanel** |
| `BpStatueList` 600×580 | building_popup 根 | `statue_main_layout.list_area_size` | **StatuePanel** |
| `BpStageCoachTextBox` 350×200 | building_popup 根 | `add_hero_dialog_contents.text_box_size` | **StageCoachPanel**（且属对话框子层，非主列表） |
| `BpHeroActionBase` 100×100 | building_popup 根 | `hero_action_layout.base_size` | **HeroActionPanel** |

**"框 523×86 vs DD 尺寸"的答案**：那个 523×86 是我们自己按 body 区约定画的临时框，DD 里**根本不存在这个尺寸**。它应该被替换为「`base_size 800×200` 的活动条目容器」（来自 `building_activity_layout`）。尺寸用 DD 原值，不是 523×86。

---

## 2. offset 未知父容器 → **选 A：建容器并注明；因为父级已从源文件还原**

### 五处逐一裁定（父容器全部找到）

| 节点 | 字段 | 源文件 section（真实父） | 父容器归属 |
|---|---|---|---|
| `RaidPos2` | `fourth_pos -255,0` | `camp_layout` | **营火布局 `CampLayout`**（与 `first_pos 255,0` / `second_pos 130,0` / `third_pos -130,0` 同族） |
| `RaidPos7` | `complete_return_to_hamlet_pos -170,98` | **`quest_info`**（非 `monster_info`） | **任务结算选择面板 `QuestInfoPanel`**（同族：`complete_mid_screen_pos 948,590` / `complete_continue_raid_pos 90,98`） |
| `BpTreesAnchor` | `upgrade_trees_offset 0,195` | `building_base_upgrade_layout` | **`BuildingBaseUpgrade`** 容器（偏移基准 = 升级基座） |
| `HxArrowAnchor` | `arrow_offset 148,80` | `heirloom_exchange_heirloom_to_layout` | **`HeirloomToPanel`**（"转出"列） |
| `HxIconOffsetAnchor` | `icon_offset 0,32` | **`heirloom_exchange_heirloom_from_layout`** | **`HeirloomFromPanel`**（"转入"列） |

**注意最后两个**：`icon_offset` 和 `arrow_offset` 分属**两个不同 section**（from / to）——v2 拍平后它们看起来像同一个 `_root` 的字段，这是最隐蔽的一处错误。修正后：`icon_offset 0,32` 属"转入"列，`arrow_offset 148,80` 属"转出"列。

另外 `heirloom_exchange_layout` 本体还有 `heirloom_from_pos 0,0` / `heirloom_to_pos 0,0` —— **这两个才是 from/to 两列的挂载锚点**，父容器就是它们。

### 裁定

**选 A。** 且不需要"注明未取得"——父级已取得。做法：
```
HeirloomExchangePanel          ← heirloom_exchange_layout
├── HxFromPanel                ← heirloom_from_pos 0,0 + heirloom_exchange_heirloom_from_layout
│   └── HxIconOffsetAnchor     ← icon_offset 0,32
└── HxToPanel                  ← heirloom_to_pos 0,0 + heirloom_exchange_heirloom_to_layout
    ├── HxArrowAnchor          ← arrow_offset 148,80
    └── HxChoiceStartAnchor    ← choice_start_offset 256,75
```

---

## 3. "15 个层级为空的块" → **误会：那是工程节点，不是 spec section**

### 核查结果

| 对象 | 数量 | 层级标注情况 |
|---|---|---|
| `ui_spec.json` 的 section | 88 | **全部有 `菜单层级` + `所属场景`，0 个为空** ✅ |
| `building_popup.tscn` 的顶层节点 | 13 | 工程节点，本来就不该进 spec ❌ |

你数的"15 个层级为空"实际是 **`building_popup.tscn` 的 13 个顶层块**（`BpNameAnchor` / `BpBodyAnchor` / `BpUpgradeAnchor` / `BpTreesAnchor` / `BpSlotListAnchor` / `BpChoiceListAnchor` / `BpGraveyardEntry` / `BpStatueList` / `BpUpgradeSlot` / `BpHeroActionBase` / `BpSanitariumChoice` / `BpStageCoachTextBox` + `BuildingSplit`）。

**这些是施工时手工新增的占位块，spec 里当然查不到 —— 不是 spec 缺标签。**

### 但确实需要一条"机械套用规则"

给工程节点定层级的规则（按**来源 section** 推导，不需要人判）：

```
规则 1：工程节点 → 查 tooltip_text 里的 DD 字段名
        例："DD graveyard .entry_size" → 字段 entry_size
            → 在 _layout_parsed_v3.json 搜该字段 → 得 section=graveyard_main_layout
            → 该 section 的 菜单层级 = L2

规则 2：若节点无 DD 字段引用（纯自造占位）
        → 层级 = 其父节点的层级
        → 父是 building_popup（L2）⇒ 它为 L2

规则 3：Anchor 类节点（*Anchor / *OffsetAnchor）
        → 层级 = 它承载的那个 offset 字段所属 section 的层级

规则 4：兜底：任何工程节点默认 L2（建筑/面板内元素），
        除非它显式引用 L3 来源（tooltip/confirm/dialog 类 section）
```

**自动标注脚本**（可直接跑）：

```python
# 工程节点 → 层级 自动推导
def infer_level(node_name, tooltip, v3_sections, parent_level='L2'):
    import re
    # 规则1：从 tooltip 抓 DD 字段名
    m = re.search(r'DD\s+\S+\s+\.?([a-z_][\w]*)', tooltip)
    if m:
        fld = m.group(1)
        for s in v3_sections:
            if fld in s['fields']:
                return lookup_level(s['file']), s['file'], s['section']
    # 规则3：Anchor 类
    if 'Anchor' in node_name:
        return infer_level_anchor(node_name, v3_sections) or parent_level
    # 规则2/4：继承父级
    return parent_level
```

**但更根本的建议**：这 13 个块**不该留在 `building_popup.tscn` 根上**。按第 1 题裁定，它们应各自移入对应建筑的子面板场景。移入后层级自然由「所属建筑」决定（全是 L2），这个"为空"的问题就消失了。

---

## 5. 两处面板尺寸：可以精确落位了（v5 补全）

你说这两处要如实标"未取得"——**布局文件确实没给 size，但面板自身美术资产的尺寸可读**，按我们既定的合规口径（尺寸 = 度量事实，非版权表达），这两处不必标"未取得"。

### 5.1 `heirloom_exchange` → **429 × 268**

| 项 | 值 |
|---|---|
| 来源 | `campaign/town/heirloom_exchange/heirloom_exchange.background.png` |
| 尺寸 | **429 × 268** |
| 可信度 | **高** |

**三项独立佐证**：
1. 文件名即 `.background`，是面板本体背景板
2. `heirloom_exchange_layout.title_pos 215,24` —— **x = 215 恰为 429/2**，标题正好水平居中 ✓
3. 全部 offset 落在 429×268 内：from 列选项区底沿 `110+94=204 < 268`；to 列最右 `256+112=368 < 429` ✓

**配套资产（同目录）**：`frame 189×56` / `frame_invalid 189×56` / `arrow_0..3 72×180` / `arrow_up 32×30` / `arrow_down 32×30` / `confirm 48×24` / `selected_overlay 52×52` / `he_icon_idle|selected 58×59`

### 5.2 `building_popup` → **662 × 764**（主体）

| 项 | 值 |
|---|---|
| 主体背景 | `campaign/town/buildings/blgupgradebg.png` = **662 × 764** |
| 名称底 | `blg_name_background.png` = **208 × 224**（挂 `name_pos 104,126`） |
| 升级花费框 | `blg_townupgrade_costframe.png` = **103 × 139** |
| 场景背景 | 各栋 `*.character_background.png` = **1395 × 776** |
| 可信度 | **中高** |

**佐证**：
- `building_base_layout` 的绝对锚点（`name_pos 104,126` / `body_base_pos 596,102` / `upgrade_base_pos 172,259` / `close_pos 1496,144`）全部落在 1920×1080 画布内 ✓
- 以 `upgrade_base_pos 172,259` 为父，`building_base_upgrade_layout` 的 6 个 offset 换算成绝对坐标（154,144 → 652,321 → 172,454）全部落在 662×764 的合理邻域内 ✓
- 各栋 `character_background.png` 的 **1395 宽 = DD `character_panel_size 1395×1080` 的宽**，互相印证 ✓

**注意坐标空间差异**（v2 修正）：
- `heirloom_exchange`：面板内部用 **429×268 局部空间**（from / to 两列各以自己的 `*_pos 0,0` 为原点）
- `building_popup`：`building_base_layout` 的 `*_pos` 是 **662×764 局部空间**（原点左上）

### 5.2.1 坐标空间逐字段判别（2026-09-19 实测修正）

初判曾写"`building_base_layout.*_pos` 是 1920×1080 全局绝对锚点"——**这是错的**。逐字段代入 662×764 判别后结论如下：

| 字段 | 值 | 在 662×764 内？ | 判定 |
|---|---|---|---|
| `name_pos` | 104, 126 | ✅ 左内 104 / 上内 126 | **662×764 局部**（左上原点） |
| `body_base_pos` | 596, 102 | ✅ 右内 66px / 上内 102px | **662×764 局部**（贴右偏上） |
| `upgrade_base_pos` | 172, 259 | ✅ 左内 172 / 上内 259 | **662×764 局部**（左列中下） |
| `close_pos` | 1496, 144 | ❌ 超出 662 宽 +834 | **属屏幕空间 1920×1080**，不在面板内 |

⇒ 前三者分布合理（左列名称 / 右列主体 / 左中升级区），**同一坐标空间**；只有 `close_pos` 是非面板级（DD 的关闭按钮画在整个屏幕上）。落码时 `close_pos` 不应进 `PanelFrame`。

### 5.2.2 `frame_offset -18,-115` 的语义佐证

`building_base_upgrade_layout.frame_offset -18,-115` 是**负值 offset**（见 §1.6 负值三语义：相对偏移可为负）：

- 既是负值 ⇒ 说明它是"**相对已存在的框向外扩张**"，而非"从原点偏移"
- ⇒ 反证 662×764（`blgupgradebg.png`）是**含外框的整体面板尺寸**，升级内容框在其内再内缩
- ⇒ 与 `heirloom_exchange` 的 429×268（`*.background.png`）**同一模式**：`*background*/*bg*` 资产 = 面板整体尺寸

### 5.2.3 溢出块的 DD 行为（不是我们放错）

以下块**尺寸大于面板**，这是 DD 原设计，行为是**被父面板裁剪**（非错误）：

| 块 | 尺寸 | 溢出原因 |
|---|---|---|
| `building_activity_layout.base_size` | 800×200 | 800 > 662（面板宽） |
| `graveyard.entry_size` | 1000×160 | 1000 > 662，且宽于列表 720 |
| `graveyard.list_area_size` | 720×600 | 720 > 662 |
| `hero_recruit_store.base_size` | 780×780 | 780 > 662 |

⇒ 工程实现必须给 `PanelFrame` 加 `clip_contents = true`，否则会画出面板外。**这是 DD 行为一致性的要求，不是折衷。**

🆕 **审计口径（2026-10-02 · `O-118`）**：上表 4 个溢出块在 `--ui-audit`【无覆盖层 ⇒ `scope=root` 全界面口径】下会被计为「重叠对」（实测 19 对中 5 对出自此处 · 前 6 对已逐对核）——**这是判据口径缺口，不是布局缺陷**：`LayoutAudit` 现有三条跳过（`MotionLayer` ／ `Window` ／ `clip_contents` 裁到空）里没有「被不透明祖先完全遮住 ⇒ 跳过」，覆盖层口径又只认「恰好一个满屏不透明 Panel」⇒ **全界面口径不作逐屏验收口** ✓
📄 登记 `doc/architecture/open_issues.md O-118` · 读数 `reports/ui_audit_fullscene_20261002.md` ✓

### 5.3 已写回 spec

`ui_spec.json` 的 `meta.面板尺寸补全.panels` 已记录两处，对应 section 也补了 `面板尺寸` 字段。**你可以撤掉"未取得"标注，直接精确落位。**

---

## 6. 一处更正：`heirloom_exchange_layout` 的三个 section 关系

补尺寸时顺带确认了父子链（修正 §2）：

```
heirloom_exchange_layout               ← 面板根，title_pos 215,24
├── heirloom_exchange_heirloom_from_layout   ← 转入列，父锚点 heirloom_from_pos 0,0
│   └── icon_offset 0,32 / arrow_up_offset 0,-10 / arrow_down_offset 0,84 / text_offset 0,36
└── heirloom_exchange_heirloom_to_layout     ← 转出列，父锚点 heirloom_to_pos 0,0
    ├── arrow_offset 148,80
    ├── frame_offset -32,-8 / frame_invalid_offset -32,-8
    └── heirloom_icon_offset 0,20 / heirloom_amount_offset 52,4 / confirm_offset 112,20
```

**关键**：`icon_offset`（属 from）与 `arrow_offset`（属 to）**分属两列**，v2 拍平后曾被误认为同一层字段。落码时务必分开挂。

---

## 4. 修复清单（施工进度）

| # | 动作 | 依据 | 状态 |
|---|---|---|---|
| 1 | 用 `_layout_parsed_v3.json` 替换 `ui_spec.json` 的 sections 数据源（88 → 432） | §0 | ✅ 完成 |
| 2 | `building_popup.tscn` 拆出 6 个建筑子面板（Graveyard/Statue/StageCoach/Sanitarium/HeroAction/Upgrade） | §1 | ✅ 完成 |
| 3 | `BpGraveyardEntry` 等 4 块尺寸 & 归属改正（删掉自造的 523×86，改 `entry_size 1000×160`） | §1 | ✅ 完成 |
| 4 | 新增 `HeirloomExchangePanel` + `HxFromLayer`/`HxToLayer` 两层，重挂 5 处 offset | §2 | ✅ 完成 |
| 5 | 修正 `RaidPos7` 归属：从 `monster_info` 改挂 `QuestInfoPanel` | §2 | ⬜ 待做 |
| 6 | 修正 `RaidPos2` 归属：挂 `CampLayout`（与 first/second/third_pos 同族） | §2 | ⬜ 待做 |
| 7 | 新增 `BuildingBaseUpgrade` 容器承载 `BpTreesAnchor` | §2 | ✅ 完成（并入第 2 条） |
| 8 | 工程节点层级按 §3 规则自动标注 | §3 | ✅ 完成（L2 标签已入场景） |
| 9 | `heirloom_exchange` 外框改用 **429×268**，两列按 from/to 各挂各的 `*_pos 0,0` 原点 | §5.1 / §6 | ✅ 完成 |
| 10 | `building_popup` 主体改用 **662×764**，名称底 208×224、花费框 103×139 | §5.2 | ✅ 完成 |

### 第 2/3/10 条施工明细（2026-09-19）

**`building_popup.tscn` 重构后结构**（67 节点，`F:/GithubPro/Darkest/darkest/scenes/ui/building_popup.tscn`）：

```
BuildingPopupBody          [Control]  662x764 居中承载（offset -331,-382,+331,+382）
└ PanelFrame               [Control]  clip_contents = true（溢出块裁剪，DD 行为一致）
  ├ PanelBackground        [ColorRect] 662x764 占位
  ├ BpNameBack             [PanelContainer] 208x224 @ (104,126)   ← blg_name_background.png
  ├ BpBodyLayout           [Control]  DD building_base_body_layout
  │ └ BpBodyAnchor         [PanelContainer] 220x96 @ (430,102)    ← body_base_pos 596,102 原点向左收
  ├ BpUpgradeLayout        [Control]  DD building_base_upgrade_layout
  │ ├ BpUpgradeAnchor      [PanelContainer] 200x180 @ (172,259)   ← upgrade_base_pos
  │ └ BpTreesAnchor        [PanelContainer] 180x120 @ (172,454)   ← upgrade_trees_offset 0,195
  ├ BpActivityLayout       [Control]  DD building_activity_layout
  │ ├ BpActivityBase       [PanelContainer] 800x200 @ (70,50)     ← base_size 800x200（溢出裁剪）
  │ │ └ BpSlotListAnchor   [PanelContainer] 135x80 @ (440,119)    ← slot_list_pos（条目局部空间）
  │ └ BpChoiceListAnchor   [PanelContainer] 120x20 @ (0,250)      ← choice_hot_spot_size
  ├ BpCostFrame            [PanelContainer] 103x139 右下角对齐     ← blg_townupgrade_costframe.png
  ├ GraveyardPanel         [Control]  坟场    · dead_hero_layout
  │ └ BpGraveyardList      [PanelContainer] 720x600 @ (148,148)   ← list_area_size
  │   └ BpGraveyardEntry   [PanelContainer] 1000x160              ← entry_size（被列表裁剪）
  ├ StatuePanel            [Control]  雕像    · statue_main_layout
  │ └ BpStatueList         [PanelContainer] 600x580 @ (270,170)
  ├ StageCoachPanel        [Control]  驿站    · hero_recruit_store
  │ ├ BpHeroRecruitStore   [PanelContainer] 780x780               ← base_size
  │ └ BpStageCoachTextBox  [PanelContainer] 350x200 @ (40,620)    ← text_box_size
  ├ SanitariumPanel        [Control]  疗养院  · quirk_treatment
  │ └ BpSanitariumChoice   [PanelContainer] 120x20 @ (70,300)     ← choice_list_pos 相对活动条目
  ├ HeroActionPanel        [Control]  英雄动作 · hero_action_layout
  │ └ BpHeroActionBase     [PanelContainer] 100x100 @ (220,44)    ← base_pos + base_size
  └ UpgradePanel           [Control]  升级需求 · upgrade_requirement_layout
    └ BpUpgradeSlot        [PanelContainer] 102x72 @ (172,454)    ← base_size
```

**校验结果**：16 个 `PanelContainer` 全部有实际尺寸（零尺寸 = 0 个）；11 个"DD 给定尺寸"的块 100% 覆盖；4 个溢出块均已确认是 DD 原设计（被面板裁剪）。

**同步改动**：`BuildingPopupSkeleton.cs` 由 `VBoxContainer` 改为 `Control`，节点路径全部改 `PanelFrame/*`，新增 3 个尺寸常量（`PanelSize 662x764` / `NameBackSize 208x224` / `CostFrameSize 103x139`）与 6 个建筑子面板属性访问器。

---

## 7. L2/L3 布局规格（2026-09-20 · 位置整改）

### 7.1 此前的问题

| # | 问题 | 实证 |
|---|---|---|
| ① | **所有 L2/L3 都是满屏大框** | `MakePopup` / `MakeOpaqueModal` / `HeroDetail` 一律 `SetAnchorsAndOffsetsPreset(FullRect)` ⇒ 铺满 1920×1080，背景全被挡住 |
| ② | **绕过了已有的模态栈** | `OverlayLayer.OpenModal` **全仓零调用** —— `MakePopup` 直接 `AddChild(panel)` ⇒ `_modals` 恒空 ⇒ `Esc` 兜底失效、无遮罩 |
| ③ | **每次开屏都新建** | `QuestSelect` / `Provision` / `HeirloomExchange` / `LootOverlay` 每次 `MakePopup` 新建 ⇒ 四屏互相重叠堆积 |
| ④ | **关不掉** | `body.GetParent()?.GetParent() as PanelContainer` 在代码回落路径下取到的是 **MarginContainer** ⇒ cast 失败 ⇒ ✕ 无效 |
| ⑤ | **无遮罩、无层级** | 全仓无遮罩；无一处 `ZIndex` / `.Layer=` ⇒ 层级完全靠 AddChild 先后 |
| ⑥ | **骨架错挂** | `building_popup.tscn` / `heirloom` 的 Center 锚点被塞进 `VBoxContainer` ⇒ 锚点与 offsets 全废 |

### 7.2 DD 权威尺寸（实测自 `/e/SteamLibrary/steamapps/common/DarkestDungeon/`）

| 面板 | DD layout 文件 | DD 位置 | DD 尺寸 | 背景资产实测 |
|---|---|---|---|---|
| 角色/整屏页 | `shared/character/character.layout.darkest` | 144,132 | 1395×776 | `characterpanel_bg.png` |
| 任务选择 | `campaign/town/quest_select/…` | 0,0 | 1920×1080 | `quest_select.background.png` |
| 供应 | `campaign/town/provision/…` | 0,0 | 1920×1080 | `provision.background.png` |
| 库存 | `campaign/town/realm_inventory/…` | 881,128 | 667×780 | `realminv_bg.png` |
| 传家宝兑换 | `campaign/town/heirloom_exchange/…` | 340,708 | 429×268 | `heirloom_exchange.background.png` |
| 建筑（升级树块） | `campaign/town/buildings/building.layout.darkest` | 172,259 | 662×764 | `blgupgradebg.png` |
| 确认框 | `shared/confirm_dialog/…` | 540,200 | 840×600 | `confirm_dialog.background.png` |
| 模态框 | `shared/modal_dialog/…` | 540,308 | 840×464 | `modal_dialog.background.png` |

⚠️ **DD 无黑遮罩**：用 `town.anim.darkest` 的 `building_zoom_anim`（推近 + 模糊 + `colours/town_screen_colour_inblg.png` LUT）代替。本项目改用**半透明黑遮罩**（Godot 全屏模糊需自研 shader，违反红线 26）。

### 7.3 本轮裁定

**布局基准 = 混合**（用户裁定）：
- **整屏页跟 DD**：`HeroDetail` = 1395×776 @ (144,132)，**左偏不居中**
- **小盒居中**：建筑 / 传家宝 / 库存 / 确认框 / 模态框
- **满屏仅保留 DD 真满屏的两个**：任务选择、供应（各有 1920×1080 背景图实测）

**装饰开销修正**（重要）：`MakePopup` 会给面板套 `MarginContainer(16)` + 标题行 + 分隔线。
若面板尺寸直接给 DD 内容尺寸，内容会被挤出并被 `clip_contents` 裁掉 ⇒
- 建筑面板 = 662×764 + 装饰 = **694×858**
- 传家宝面板 = 429×268 + 装饰 = **461×362**
- 其余（Sheet / Confirm / Modal）本就是 DD **整框**尺寸，不再加装饰

**新增** `scripts/ui/UILayoutSpec.cs`（`PopupLayout` 枚举 + `Resolve()` + `Centered()` + `Place()`）——
尺寸/位置**一处定义**，调用点不再写 `new Vector2(840, 464)` 之类字面量。

### 7.4 落码要点

1. **`Place()` 必须清锚点**：`SetAnchorsAndOffsetsPreset(TopLeft, keepOffsets: false)` ⇒ 之后 `Position`/`Size` 才是干净像素直落。父节点必须是**非容器** `Control`（如 `OverlayLayer.ModalHost`），否则锚点被容器接管而失效（这就是 ⑥ 的根因）。
2. **入/出栈时机**：`OpenModal` 会把面板设为可见 ⇒ **Build 期创建、之后才显示**的面板（`ResultPanel`/`DevLogPanel`）**不能**在工厂里入栈，入栈交给调用方。
3. **✕ 必须出栈**：只 `Visible = false` 会让栈里留"隐形层" ⇒ 遮罩不消失、`Esc` 关到空层。
4. **骨架最小尺寸**：`building_popup.tscn` 根与 `heirloom` `PanelFrame` 补 `custom_minimum_size` ⇒ 挂进容器时不塌成 0×0。
