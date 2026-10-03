using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.Build.cs` 拆出（用户红线：程序文件 ≤600 行 · M11 ① 预警面拆分第二十件 · 2026-10-02）✓
/// ② 本文件 = **战斗 · 建树行构建族**（`BuildTopRow` 顶栏 ／ `BuildBattlefield` 主体 ／ `BuildBottomRow` 底栏；
///    原 `BattleUI.Build.cs` :166-466 逐字节 · 骨架优先/回落分支一字未改）✓
/// ③ 🔴 实测依赖主类私有成员/状态（搬家后逐条扫描 · `_x` 引用点 40 个 · x = 引用次数）：
///    `_statusLabel` x8 ／ `_progressLabel` x8 ／ `_topRow` x8 ／ `_actionButtons` x7 ／ `_topLeftGroup` x7 ／
///    `_intentText` x6 ／ `_midRow` x6 ／ `_missionLabel` x6 ／ `_orderBox` x6 ／ `_actorDetailBox` x5 ／ `_actorRow` x5 ／
///    `_bottomBarSkel` x5 ／ `_cArea` x5 ／ …（其余 27 个成员见 `reports/p6_m11_split20_20261002.md` §一）✓
///    根片私有口：`BuildCard` x3 ／ `MakePanelStyle` x3 ／ `Refresh` x2 ／ `BuildMultiFunctionBox` x1 ／ `ShowBanner` x1 ／ `PressSupportPack` x1 ✓
/// ④ **只搬家、零行为改动**（using 1 条：`Godot` —— 新片 using 逐条须由实体引用自证；原主片 `Darkest.Gameplay.Scene`
///    在本片仅注释提及 `BattleRoot.PreviewIntent`、无实体引用 ⇒ 未随迁 · 已构建验证）✓
/// </summary>
public partial class BattleUI : Control
{

    /// <summary>A 顶栏：状态（回合·支援点）／行动顺序头像／进度／日志／撤退。</summary>
    private void BuildTopRow()
    {
        _statusLabel = new Label { Text = "" };
        _statusLabel.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextPrimary);
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // 占满剩余宽度
        // 🔴 相机 1280 口径（规则①）：**长文本 Label 的最小宽 = 文本宽** ⇒ 会把整行撑宽 ⇒ 必须**裁切**（不换行、超出省略）✓
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _statusLabel.ClipText = true;
        _statusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _statusLabel.CustomMinimumSize = new Vector2(0, 22);
        // 🔴 P5（用户参考图②）：**左上 = 任务与撤退** —— 独立成组放在顶栏最左；其余项在其右 ✓
        _topLeftGroup = _topBarSkel?.TopLeftGroup ?? new HBoxContainer { Name = "TopLeftGroup", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        if (_topLeftGroup.GetParent() is null)
        {
            _topLeftGroup.AddThemeConstantOverride("separation", 8);
            _topRow.AddChild(_topLeftGroup);
        }

        _missionLabel = new Label { Name = "MissionLabel", VerticalAlignment = VerticalAlignment.Center };
        // 🔴 2026-09-21 相机纠偏：任务标签允许收缩 + 裁切（顶栏横向预算 ≤1280）✓
        _missionLabel.ClipText = true;
        _missionLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _missionLabel.CustomMinimumSize = Vector2.Zero;
        _missionLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        // 🔴 `retreat.md §8`（用户裁定 DD 形态）：**放弃远征** = 地图层动作（结束本趟·回城）；
        //    与【撤退】（战斗内·只退本场）**不得同屏/同位置** ⇒ 本按钮**只在行走模式可见** ✓
        //    ⚠️ 没拿到宿主回调（`SetAbandonAction` 未被调用）⇒ **不显示**（红线 21：不留"点了没用"的控件）✓
        _abandonButton = new Button
        {
            Name = "AbandonExpedition",
            Text = "放弃远征",
            CustomMinimumSize = new Vector2(112, 30),
            TooltipText = "放弃远征（结束本次远征）　⇒ 回城·本趟未完成·**不可逆**（会先二次确认）",
        };
        _abandonButton.Pressed += PressAbandon;
        _abandonButton.Visible = false; // 默认隐藏，等宿主把动作交给我 ✓
        _topLeftGroup.AddChild(_abandonButton);
        _topLeftGroup.AddChild(_missionLabel);

        _topRow.AddChild(_statusLabel);

        var orderLabel = new Label { Text = "本回合顺序", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        orderLabel.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
        _topRow.AddChild(orderLabel);

        _orderBox = new HBoxContainer { Name = "OrderBox", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };

        // 🔴 2026-09-21 相机纠偏：顶栏右上组（OrderBox 576 宽）把整行顶到 1372 ⇒ 允许收缩 + 裁切 ✓
        _orderBox.CustomMinimumSize = Vector2.Zero;
        _orderBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _orderBox.ClipContents = true;
        _orderBox.AddThemeConstantOverride("separation", 4);
        _topRow.AddChild(_orderBox);

        // 🔴 主程序清单第 3 条：**敌方意图预览**（`BattleRoot.PreviewIntent` ← `_view.IntentPreview`）
        //    内部用**固定种子的预览专用 RNG**（与战斗抽数完全隔离）⇒ 预览**绝不消耗抽数**（确定性不变）✓
        _intentText = new Label
        {
            Name = "EnemyIntent",
            // 🔴 **禁止 autowrap**：实测它在窄分配下折成几百行 ⇒ 把顶栏撑到 3377px ⇒ **整屏 UI 被拉伸到看不见** ⚠️
            //    ⇒ 单行 + **裁切**（`§14.6`：clip_text / 防溢出防撑爆）；完整内容走 `TooltipText` ✓
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(0, 22),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };

        // 🔴 2026-09-21 相机纠偏：顶栏右侧需求 1388 > 相机 1280（实测"内容需求超出相机 4"）⇒ 允许收缩 + 裁切 ✓
        _intentText.ClipText = true;
        _intentText.CustomMinimumSize = Vector2.Zero;
        _intentText.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _intentText.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextInfo);
        _topRow.AddChild(_intentText);

        // 🔴 P5（用户参考图②）：**正上方 = 火把条**（光照既是机制、也要"看得见"）—— **居中**放置；
        //    数据只读本趟 `Flow.Meter`（无本趟 ⇒ 隐藏并留痕）✓
        //    ⚠️ 与地图模式里那条光照条是**同一个信息** ⇒ **只保留这一条**（地图模式那条收起，避免两处显示同一读数）✓
        CenterContainer torchWrap = _topBarSkel?.TorchWrap ?? new CenterContainer { Name = "TorchWrap", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        if (torchWrap.GetParent() is null)
        {
            _topRow.AddChild(torchWrap);
        }
        _topTorch = new Darkest.UI.LightBarPanel { Name = "TopTorchBar" };
        torchWrap.AddChild(_topTorch);

        _progressLabel = new Label { Text = "" };
        _progressLabel.AddThemeFontSizeOverride("font_size", 13);
        _progressLabel.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextInfo);
        _progressLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _progressLabel.AutowrapMode = TextServer.AutowrapMode.Off;   // 🔴 同上：长文本裁切，不撑宽整行 ✓
        _progressLabel.ClipText = true;
        _progressLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _topRow.AddChild(_progressLabel);

        _devLogButton = new Button { Text = "日志 F1" };
        _devLogButton.Pressed += ToggleDevLog;
        _topRow.AddChild(_devLogButton);

        // 🔴 `retreat.md §8`（主程序已拆 `IsFinished`：撤退后仍是 `InProgress`）⇒ **现在可以如实改文案**：
        //    **撤退 = 退出【本场】战斗 ⇒ 回地图当前格继续走**；
        //    与【放弃远征】（结束本趟·回城）**不得混用**，且两者**不得同屏**（放弃远征只在行走模式）✓
        _retreatButton = new Button
        {
            Text = "撤退（退出这场战斗）",
            TooltipText = "撤退 ⇒ **退出本场战斗**、回到地图当前格**继续走**" +
                          "（该格算已处理；有士气惩罚、**没有胜利收益**）\n" +
                          "⚠️ 若要**结束本趟**回城，请用【放弃远征】（在行走模式）",
        };
        _retreatButton.Pressed += () => _retreat?.Invoke();
        _topLeftGroup!.AddChild(_retreatButton); // 🔴 P5：撤退归入**左上"任务与撤退"组**（DD 图②）✓

        // 兼容既有刷新路径：状态/顺序文案仍由 `Refresh()` 写
        _actionOrderLabel = orderLabel;
    }

    /// <summary>主体：我方（前排 4 ＋ 支援位 2）←→ 敌方 4。卡片**均分宽**（`#321`③：位置编号要稳定映射横坐标）。</summary>
    private void BuildBattlefield()
    {
        var playerArea = new VBoxContainer { Name = "PlayerArea", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        playerArea.AddThemeConstantOverride("separation", 4);
        _midRow.AddChild(new Control { Name = "MidPadLeft", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.148f });   // DD 左空 284/1920 ✓
        // 🔴 2026-10-03 布局审计：纵向锚点 0.6297~0.95 原本对的是**满屏 1080**，但父级 `StageLayer` 实测高 **0**
        //    ⇒ 带子落空、卡片溢出钻到底栏之下（既看不见也点不动）⇒ 纵向改为**吃满舞台带**（0~1），
        //    带高由 `BattleUI.Build.cs` 的 `stageBandMinH` 保证；**横向仍守 DD 1:1 带宽**（284→788 = 26.2%）✓
        playerArea.AnchorLeft = 0.148f; playerArea.AnchorRight = 0.410f; playerArea.AnchorTop = 0f; playerArea.AnchorBottom = 1f;   // C2a：DD overlays（hero band 284-788 · 26.2% 宽）
        _midRow.AddChild(playerArea);   // 🔴 DD 1:1 ④-3a：英雄 band 284→788 = 26.2% ✓
        playerArea.SizeFlagsStretchRatio = 0.262f;

        playerArea.AddChild(TitleLabel("我方　战 4 · 3 · 2 · 1　｜　辅 5 · 6"));

        _playerCards = new HBoxContainer { Name = "PlayerCards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _playerCards.AddThemeConstantOverride("separation", 6);
        playerArea.AddChild(_playerCards);

        _playerSupport = new HBoxContainer { Name = "PlayerSupport", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _playerSupport.AddThemeConstantOverride("separation", 6);
        playerArea.AddChild(_playerSupport);

        var vs = new Label { Text = "VS", CustomMinimumSize = new Vector2(24, 24), VerticalAlignment = VerticalAlignment.Center };
        vs.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.Danger);
        vs.HorizontalAlignment = HorizontalAlignment.Center;   // 🔴 2026-10-03 布局审计：本 Label 的矩形**按设计**吃满中缝（259 宽 × 舞台带高 246），
                                                              //    默认左对齐会把「VS」画到中缝左边缘（贴着我方卡名）⇒ 居中才对得上 DD 语义 ✓
        vs.MouseFilter = Control.MouseFilterEnum.Ignore;       // 纯展示：逐控件写（父级 Ignore 不豁免子节点）✓
        vs.AnchorLeft = 0.41f; vs.AnchorRight = 0.547f; vs.AnchorTop = 0f; vs.AnchorBottom = 1f;   // C2a：VS 分隔居中于两 band 之间（纵向吃满舞台带，同 PlayerArea）
        _midRow.AddChild(vs);
        _midRow.AddChild(new Control { Name = "MidPadCenter", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.137f });   // DD 中缝 (1050-788)/1920 ✓

        var enemyArea = new VBoxContainer { Name = "EnemyArea", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        enemyArea.AddThemeConstantOverride("separation", 4);
        enemyArea.AnchorLeft = 0.547f; enemyArea.AnchorRight = 0.809f; enemyArea.AnchorTop = 0f; enemyArea.AnchorBottom = 1f;   // C2a：DD overlays（monster band 1050-1554 · 26.2% 宽）
        _midRow.AddChild(enemyArea);   // 🔴 DD 1:1 ④-3a：怪物 band 1050→1554 = 26.2% ✓
        GD.Print("[UI-TRACE] stage-layer-ready");   // C2a/C4：舞台层与 DD 锚点就位（供 ui_sweep 观察）
        ShowBanner();   // 阶段2 施工④：显示 DD panel.banner 战斗横幅（骨架优先/缺失回落）
        enemyArea.SizeFlagsStretchRatio = 0.262f;
        enemyArea.AddChild(TitleLabel("敌方　1 · 2 · 3 · 4"));
        _midRow.AddChild(new Control { Name = "MidPadRight", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.191f });   // DD 右空 (1920-1554)/1920 ✓

        _enemyCards = new HBoxContainer { Name = "EnemyCards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _enemyCards.AddThemeConstantOverride("separation", 6);
        enemyArea.AddChild(_enemyCards);

        // 卡片**创建时**就进各自的容器（顺序保持：我方 4 → 敌方 4 → 支援 2，`Refresh()` 的下标依赖它）
        for (int i = 0; i < 4; i++)
        {
            _playerCards.AddChild(BuildCard(CardW, CardH));
        }

        for (int i = 0; i < 4; i++)
        {
            _enemyCards.AddChild(BuildCard(CardW, CardH));
        }

        for (int i = 0; i < 2; i++)
        {
            _playerSupport.AddChild(BuildCard(SupportW, SupportH));
        }
    }

    /// <summary>底栏：C 区（固定宽，**技能栏在 C 区内**）＋ E 区（多功能框，**唯一 ExpandFill**）。</summary>
    private void BuildBottomRow()
    {
        _cArea = _bottomBarSkel?.CArea ?? new PanelContainer
        {
            Name = "CArea",
            CustomMinimumSize = new Vector2(260, 0),   // 🔴 再收（相机 1280 口径：底栏多项最小宽之和曾超额）                    // 固定宽 ≈ 30%（`#321`③）
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,        // 不 ExpandFill
        };
        var cCol = new VBoxContainer { Name = "CCol" };
        cCol.AddThemeConstantOverride("separation", 6);
        // 🔴 相机 720 口径：**C 区（橙框：当前角色 + 技能选择）内容会"中途长大"** ——
        //    实测 底栏/ C区 需 162 → **438**（我方回合技能填入后）⇒ 底栏整行被顶高 ⚠️
        //    ⇒ 用 `ScrollContainer` 兜住高度（技能再多也只在框内滚动，不再撑行）✓
        // 🔴 用户要求（2026-09-16）：**技能区不能滚动** ⇒ 去掉滚动容器（改靠"小方块"自然装下）✓
        _cArea.AddChild(cCol);

        // 🔴 P5（用户参考图②）：**左右各一个长条框放 5／6 号位，向两侧靠齐；其余部分向右靠** ✓
        _slotLeft = _bottomBarSkel?.BackSlot5 ?? new PanelContainer
        {
            Name = "BackSlot5",
            CustomMinimumSize = new Vector2(72, 112),   // 🔴 再收（相机 1280 口径）
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,   // 左条：**向左靠齐**
        };
        if (_slotLeft.GetParent() is null)
        {
            _bottomRow.AddChild(_slotLeft);      // 先加左条 ⇒ 它在最左
            _bottomRow.MoveChild(_slotLeft, 0);
        }

        // 🔴 P5：**橙框分区** —— ⚠️ 只能用【只染边框】的手段：`Modulate` 是**乘法**，会把整个子树（含文字）压暗
        //    （实测教训：一度用 `Modulate` 上色 ⇒ "战斗 UI 啥也看不见"）⇒ 正解 = `panel` 样式覆盖，仅换边框色 ✓
        // ⚠️ 教训：`MakePanelStyle(null, color)` 的 `null` 会落到**更暗的** `PanelBg` ⇒ 整块发黑（用户实测："紫色框为什么这么黑"）
        //    ⇒ 正解：**底色保持 raised（与其它面板一致），只换边框色** ✓
        ((Control)_cArea).AddThemeStyleboxOverride("panel",
            Darkest.UI.DdTheme.MakePanelStyle(Darkest.UI.DdTheme.PanelBgRaised, Darkest.UI.DdTheme.Gold));

        // 🔴 P5：**橙框 = 当前角色头像 + 技能选择**（头像留框+色块占位+名字；技能栏在其下）✓
        // 🔴 2026-10-03 布局审计：橙框这一行 = **纯展示**（头像占位色块 + 名字 Label，无任何可点控件）
        //    ⇒ 三件（行/框/色块）一律 `Ignore`：`mouse_filter` 是**逐控件**的，父级 `Ignore` 不豁免子节点
        //    ⇒ 必须逐件写，否则仍会吞掉下方卡牌的点击（实测 5/7 条「点不动」出自这里）✓
        _actorRow = new HBoxContainer { Name = "CurrentActorRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        _actorRow.AddThemeConstantOverride("separation", 6);
        cCol.AddChild(_actorRow);
        var actorFrame = new PanelContainer { Name = "CurrentActorFrame", CustomMinimumSize = new Vector2(36, 36), MouseFilter = Control.MouseFilterEnum.Ignore };
        _actorRow.AddChild(actorFrame);
        _actorPortrait = new ColorRect { Name = "CurrentActorPlaceholder", Color = Darkest.UI.DdTheme.PanelBgRaised, MouseFilter = Control.MouseFilterEnum.Ignore };
        actorFrame.AddChild(_actorPortrait);
        _actorName = new Label { Name = "CurrentActorName", VerticalAlignment = VerticalAlignment.Center };
        _actorRow.AddChild(_actorName);
        // 🔴 用户要求：**下方给英雄详情留空间** ⇒ 橙框（角色+技能）在上、紫框（详情）在下 ✓
        VBoxContainer leftStack = _bottomBarSkel?.LeftStack ?? new VBoxContainer { Name = "LeftStack", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftStack.AddThemeConstantOverride("separation", 6);
        if (_cArea.GetParent() is null)
        {
            leftStack.AddChild(_cArea);
        }

        // 🔴 用户更正（2026-09-16）：**技能框下方 = 角色详情框**（紫边；多功能框回右侧原位）✓
        _actorDetailBox = _bottomBarSkel?.ActorDetailBox ?? new PanelContainer { Name = "ActorDetailBox", CustomMinimumSize = new Vector2(0, 84) };
        // 🔴 2026-10-03 布局审计：本框内部只有 1 个 Label（`_actorDetail`）⇒ **纯展示** ⇒ `Ignore`
        //    （此前 Stop ⇒ 盖住 2 张支援位卡，判据报 2 条「点不动」）✓
        ((Control)_actorDetailBox).MouseFilter = Control.MouseFilterEnum.Ignore;
        ((Control)_actorDetailBox).AddThemeStyleboxOverride("panel",
            Darkest.UI.DdTheme.MakePanelStyle(Darkest.UI.DdTheme.PanelBgRaised.Lightened(0.22f), Darkest.UI.DdTheme.Mental));
        _actorDetail = new Label
        {
            Name = "ActorDetail",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _actorDetail.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
        _actorDetailBox.AddChild(_actorDetail);
        if (_actorDetailBox.GetParent() is null)
        {
            leftStack.AddChild(_actorDetailBox);
        }
        if (leftStack.GetParent() is null)
        {
            leftStack.AddThemeConstantOverride("separation", 6);
            _bottomRow.AddChild(leftStack);
        }

        _skillTitle = new Label { Text = "技能栏（轮到行动者时可用）", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _skillTitle.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextSkill);
        cCol.AddChild(_skillTitle);

        _skillBar = new GridContainer
        {
            Name = "SkillBar",
            Columns = Darkest.UI.DdTheme.SkillBarColumns,               // 🔴 `#325` D5：常量集中在 DdTheme（不是局部 const）
        };
        _skillBar.AddThemeConstantOverride("h_separation", 6);
        _skillBar.AddThemeConstantOverride("v_separation", 6);
        cCol.AddChild(_skillBar);

        _hintLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hintLabel.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.TextHint);
        cCol.AddChild(_hintLabel);

        _actionButtons = new HBoxContainer { Name = "ActionButtons" };
        _actionButtons.AddThemeConstantOverride("separation", 6);
        cCol.AddChild(_actionButtons);

        _reinforceButton = new Button { Text = "增援", CustomMinimumSize = new Vector2(116, 44) };
        _reinforceButton.Pressed += () => _reinforce?.Invoke();
        _actionButtons.AddChild(_reinforceButton);

        _moveButton = new Button { Text = "移动", CustomMinimumSize = new Vector2(116, 44) };
        _moveButton.Pressed += () => _move?.Invoke();
        _actionButtons.AddChild(_moveButton);

        _passButton = new Button { Text = "待命", CustomMinimumSize = new Vector2(116, 44) };
        _passButton.Pressed += () => _pass?.Invoke();
        _actionButtons.AddChild(_passButton);

        // 🔴 主程序清单第 1 条（**使用支援包**）：扣 1 个支援包 ⇒ +SP（数量由**内核**决定，UI 不写死 2）
        //    ⚠️ 它需要"本趟的背包"：单场战斗没有远征流程 ⇒ 无背包 ⇒ 按钮**置灰 + 说明原因**（红线 21：可解释）
        _supportButton = new Button { Text = "用支援包", CustomMinimumSize = new Vector2(116, 44) };
        _supportButton.Pressed += () => PressSupportPack();
        _actionButtons.AddChild(_supportButton);

        _eArea = _bottomBarSkel?.EArea ?? new PanelContainer
        {
            Name = "EArea",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,          // 🔴 **唯一 ExpandFill**（`#321`③）
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        if (_eArea.GetParent() is null) { _bottomRow.AddChild(_eArea); }

        BuildMultiFunctionBox(); // E 区多功能框（内部自带容器；**创建时进 `_eArea`**）
    }
}
