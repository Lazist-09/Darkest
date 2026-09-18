using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 刷新族**（`Refresh()`：只读跨趟状态重画顶栏/名册行/左栏状态/资源条，不自己算账）✓
/// ③ 🔴 依赖主类私有成员/状态：`_banner`/`_rosterCount`/`_status`/`_hint`/`_resourceBar`/`_rosterList`/`_rosterTitle`/
///    `_heroButtons`/`_upgradeStatus`/`_saniStatus`/`_saniButtons`/`_selectedHero`/`_cfg`/`_buildingIds`/`_buildingLabels` ✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>刷新（只读跨趟状态，不自己算账）。</summary>
    public void Refresh()
    {
        int gold = ExpeditionContext.Gold?.Gold ?? 0;
        int cost = ExpeditionContext.Gold?.StressReliefCost ?? 0;
        int affordable = cost <= 0 ? 0 : gold / cost;
        Roster? roster = ExpeditionContext.Roster;
        string moraleLine = roster is null
            ? "名册：未加载"
            : "名册士气：" + string.Join("、", roster.Heroes
                .OrderByDescending(h => roster.MoraleOf(h.Id))
                .Select(h => $"{h.Name}{roster.MoraleOf(h.Id)}"));
        _status.Text =
            $"【Hamlet 回城】金钱 {gold}　一次减压 {cost} ⇒ 现在能减 {affordable} 次\n" +
            moraleLine + "\n" +
            "④ 减压已可用（Tavern 快而不稳 ／ Abbey 慢而稳，**同价同效**）；⑤ 招募随后落地。";

        // ② 选人权：刷新"可减压者"按钮（名册里**士气低于基准 50** 的人）
        foreach (Button b in _heroButtons)
        {
            b.QueueFree();
        }

        _heroButtons.Clear();
        if (roster is not null)
        {
            // 🔴 片① ④：**右侧名册竖列**（照 DD）—— 每行 = 缩写头像 + 名字 + **士气点阵** + 装备位占位
            //    ⚠️ 红线 21：装备位显式标「未实现」；「可减压」标记保留（士气 < 基准者才可减压）
            int row = 0;
            foreach (HeroConfig h in roster.Heroes)
            {
                string id = h.Id;
                int morale = roster.MoraleOf(id);
                string dots = new string('●', Math.Clamp(morale / 10, 0, 10)).PadRight(10, '○');
                string abbrev = h.Name.Length > 0 ? h.Name[..1] : "?";
                bool canRelief = morale < RosterConfig.RookieMorale;
                int lv = LevelOfHero(id);
                string dodge = DodgeOfHero(h);
                // 🔴 DD 式紧凑行（用户参考图①）：**立绘留框（色块占位） + 等级 + 压力点阵 + 防御** ✓
                //    框 = `PanelContainer`（主题不透明面板样式 ⇒ 自带 1px 边框）⇒ 以后放立绘只换里面那格 ✓
                //    文字走**子 Label**（按钮自身 `Text` 置空，避免与子控件叠字）✓
                // 🔴 用户要求（2026-09-17）：「UI 要能在编辑器里直接干预」⇒ 名册行改为
                //    **实例化模板场景** `scenes/ui/roster_row.tscn`（含 `[Tool]` 预览 ⇒ 编辑器里改外观即生效）✓
                //    ⚠️ 场景不可用 ⇒ **回落代码构建**（不崩、不空、留痕）✓
                //    🔴 节点名保持 `RosterRowBody` / `PortraitFrame` / `PortraitPlaceholder` / `RosterInfo`（验收锚点）✓
                Button b;
                HBoxContainer rowBody;
                PanelContainer frame;
                ColorRect ph;
                if (Darkest.UI.RosterRowTemplate.TryInstantiate() is Darkest.UI.RosterRowTemplate tpl)
                {
                    b = tpl;
                    rowBody = tpl.FindChild("RosterRowBody", true, false) as HBoxContainer ?? new HBoxContainer { Name = "RosterRowBody" };
                    if (rowBody.GetParent() is null) { tpl.AddChild(rowBody); }
                    frame = tpl.FindChild("PortraitFrame", true, false) as PanelContainer ?? new PanelContainer { Name = "PortraitFrame", CustomMinimumSize = new Vector2(26, 26) };
                    if (frame.GetParent() is null) { rowBody.AddChild(frame); }
                    ph = frame.FindChild("PortraitPlaceholder", true, false) as ColorRect ?? new ColorRect { Name = "PortraitPlaceholder" };
                    if (ph.GetParent() is null) { frame.AddChild(ph); }
                }
                else
                {
                    b = new Button();
                    rowBody = new HBoxContainer { Name = "RosterRowBody" };
                    rowBody.AddThemeConstantOverride("separation", 6);
                    rowBody.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                    b.AddChild(rowBody);
                    frame = new PanelContainer { Name = "PortraitFrame", CustomMinimumSize = new Vector2(26, 26) };
                    rowBody.AddChild(frame);
                    ph = new ColorRect { Name = "PortraitPlaceholder" };
                    frame.AddChild(ph);
                }

                b.Name = $"RosterRow_{id}";
                // 🔴 2026-09-21：宽度仍按相机 1280 口径收窄到 232；**高度改为按行内实际需求算**
                //    （用户在编辑器里把名册行改成**两行结构** ⇒ 写死 32px 会把上下两行压叠：实测 6~7 对重叠）✓
                // 🔴 2026-09-21：行高**量到内层 VBox（用户两行结构的 RosterRowBody2）**，缺失则退回外层需求；宽度仍按相机 1280 口径 232 ✓
                Control? inner = b.FindChild("RosterRowBody2", true, false) as Control;
                // 🔴 2026-09-21 DD 1:1 还原 #1（Town+Roster）：行高取 DD 的 **97** 作为下限（动态需求更大时仍取更大）✓
                float needH = Math.Max(97f, (inner ?? rowBody).GetCombinedMinimumSize().Y);   // 🔴 DD 真机 roster.layout：**行高 97**（取下限 ⇒ 既贴 DD 又不复活旧重叠）✓
                b.CustomMinimumSize = new Vector2(232, needH);
                b.TooltipText = $"{h.Name}　Lv{lv}　士气 {morale}　防御 {dodge}{(canRelief ? "　·可减压" : string.Empty)}";
                ph.Color = WithPlaceholderAlpha(Darkest.UI.DdTheme.ArchetypeColor(h.Archetype, isPlayer: true));   // 🔴 规则②：α 取调色板

                if (Darkest.UI.HeroArt.PortraitTexture() is Texture2D pTex)
                {
                    var pArt = new TextureRect
                    {
                        Name = "PortraitArt",
                        Texture = pTex,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        MouseFilter = Control.MouseFilterEnum.Ignore, // 右键头像开详情 ⇒ 热区在 frame 上，别被抢 ✓
                    };
                    pArt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                    frame.AddChild(pArt);
                }

                // 🔴 **用户要求（2026-09-15）：角色详情 = 【右键头像】点开**（左键点行仍是"选中"，供减压用）✓
                frame.MouseFilter = Control.MouseFilterEnum.Stop; // 头像要自己收鼠标事件（否则被按钮吃掉）
                frame.TooltipText = "右键 ⇒ 打开角色详情";
                frame.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                    {
                        GD.Print($"[HamletRoot] **右键头像** ⇒ 打开角色详情：{id}");
                        OpenHeroDetail(id);
                    }
                };
                // 🔴 信息行：**模板里已有 `RosterInfo`** ⇒ 复用它（只填数据）；回落路径才新建 ✓
                Label info = b.FindChild("RosterInfo", true, false) as Label ?? new Label { Name = "RosterInfo" };
                if (info.GetParent() is null)
                {
                    rowBody.AddChild(info);
                }

                info.Text = $"Lv{lv}　{dots}　防{dodge}{(canRelief ? "　·可减压" : string.Empty)}";
                info.VerticalAlignment = VerticalAlignment.Center;
                // 🔴 相机 1280 口径（规则①）：行内文本**可收缩 + 裁切**（否则长文本把整行撑宽 ⇒ 实测长文本下 4 处越界）✓
                info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                info.ClipText = true;
                info.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                info.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);

                // 🔴 Track 4(b)（架构诊断：消灭"角色详情孤岛"）：**名册行上给【可见入口】**（右键头像仍保留）✓
                var detailBtn = new Button
                {
                    Name = $"RosterDetail_{id}",
                    Text = "›",
                    CustomMinimumSize = new Vector2(22, 22),
                    TooltipText = "打开角色详情（提示：左键点行=选中，供减压用）",
                    FocusMode = Control.FocusModeEnum.None,
                };
                detailBtn.AddThemeFontSizeOverride("font_size", 16);
                detailBtn.Pressed += () =>
                {
                    GD.Print($"[HamletRoot] **可见入口**（行尾 ›）⇒ 打开角色详情：{id}");
                    OpenHeroDetail(id);
                };
                rowBody.AddChild(detailBtn);
                b.Pressed += () =>
                {
                    SelectHero(id);          // 左键 = **选中**（减压按人选）
                    GD.Print($"[HamletRoot] 左键选中 {id}（角色详情请**右键头像**打开 —— 用户 2026-09-15 要求）✓");
                };
                _rosterList.AddChild(b); // 🔴 §14：填进名册容器（容器自动堆叠 ⇒ 不可能重叠）✓
                _heroButtons.Add(b);
                row++;
            }
        }

        _hint.Text = roster is null
            ? "减压：名册未加载"
            : _selectedHero is null
                ? "减压：请先在右侧名册点一位【可减压】的人，再点酒馆/修道院（同价同效、风险不同）"
                : $"减压对象：{_selectedHero}（士气 {roster.MoraleOf(_selectedHero)}）⇒ 请点酒馆或修道院";

        // 🔴 片①：**名册计数 / 资源条 / 建筑信息默认行**（都真读跨趟持有者，不写死）
        _rosterCount.Text = roster is null
            ? "名册 -/-"
            : $"名册 {roster.Heroes.Count} / {((roster.CurrentCap > 0) ? roster.CurrentCap : roster.Cap)}" +
              $"（上限 {roster.Cap}）";
        HeirloomStock? resHeirlooms = ExpeditionContext.Heirlooms;
        Economy? resGold = ExpeditionContext.Gold;
        _resourceBar.Text = resGold is null || resHeirlooms is null
            ? "资源：未加载"
            : $"💰 金钱 {resGold.Gold}　｜　传家宝：" +
              string.Join("　", resHeirlooms.Kinds.Select(k => $"{k} {resHeirlooms.Count(k)}"));
        if (string.IsNullOrEmpty(_buildingInfo.Text))
        {
            _buildingInfo.Text = "建筑：悬停/点击某一栋 ⇒ 显示名称 + 功能 + 当前等级 + 下一级所需传家宝";
        }

        // 🔴 M8.2 / V15：Sanitarium 三服务的**成本显示 + 可用性置灰**（红线 21 (b)：由内核回答）
        HeirloomStock? saniHeirlooms = ExpeditionContext.Heirlooms;
        if (_saniCfg is not null && saniHeirlooms is not null && ExpeditionContext.Gold is not null)
        {
            int saniAffordable = 0;
            foreach ((string s, Button btn) in _saniButtons)
            {
                SanitariumService svc = _saniCfg.Service(s);
                string svcCost = $"{svc.Gold}金＋{string.Join("/", svc.Heirlooms.Select(k => $"{k.Key}×{k.Value}"))}";
                bool can = Sanitarium.CanAfford(_saniCfg, s, ExpeditionContext.Gold, saniHeirlooms);
                btn.Disabled = !can;
                btn.Text = $"Sanitarium·{s}（{svcCost}）";
                saniAffordable += can ? 1 : 0;
            }

            int sick = roster?.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0) ?? 0;
            _saniStatus.Text = $"Sanitarium：可支付 {saniAffordable}/3 项服务（不足即置灰）　患病英雄 {sick} 人" +
                               $"　负面特质可除 {roster?.Heroes.Count(h => roster.FindRemovableNegativeTrait(h.Id) is not null) ?? 0} 人";
        }

        // 🔴 M8.1：传家宝库存 + 三栋建筑的等级与**生效值**（升级真的改变数字）
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (heirlooms is not null)
        {
            string stock = string.Join(" ／ ", heirlooms.Kinds.Select(k => $"{k}×{heirlooms.Count(k)}"));
            string levels = string.Join(" ／ ", new[] { "tavern", "abbey", "stagecoach" }
                .Select(b => $"{b} Lv{heirlooms.LevelOf(b)}"));
            _upgradeStatus.Text =
                $"传家宝：{stock}\n建筑：{levels}　⇒ 减压价 {heirlooms.EffectiveReliefCost(_cfg.StressReliefCost)}" +
                $"　恢复量 {heirlooms.EffectiveMoraleRestore("tavern", _cfg.StressRelief!.Buildings[0].MoraleRestore)}" +
                $"　新兵起始等级 {heirlooms.EffectiveRookieLevel(_cfg.Coach.RookieLevel)}";

            // 🔴 红线 21 (b)：**按钮可用性由内核回答**（传家宝不足或已满级 ⇒ 置灰；不假装可用）
            foreach ((string b, Button btn) in _upgradeButtons)
            {
                bool can = heirlooms.CanUpgrade(b);
                btn.Disabled = !can;
                UpgradeLevel? next = heirlooms.NextLevel(b);
                if (ReferenceEquals(btn, _buildingEntry)) { continue; } // 🔴 入口按钮文案由摘要统一写（不在按栋循环里覆盖）

                // 🔴 按钮文案带上【当前等级】（玩家一眼看得到），详细使用仍走点击后的二级窗口 ✓
                btn.Text = next is null
                    ? $"🏛 {b}　Lv{heirlooms.LevelOf(b)}（已满级）"
                    : $"🏛 {b}　Lv{heirlooms.LevelOf(b)} ⇒ Lv{heirlooms.LevelOf(b) + 1}（需 {string.Join("/", next.Cost.Select(k => $"{k.Key}×{k.Value}"))}）";
            }
        }
    }
}
