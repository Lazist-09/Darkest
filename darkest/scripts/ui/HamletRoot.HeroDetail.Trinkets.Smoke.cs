using System;
using Darkest.Gameplay.Scene;   // ExpeditionContext（名册/存档入口，与 `HamletRoot.HeroDetail.cs` 同源）✓
using Darkest.Gameplay.Sim.Run; // Roster（TrinketsOf ／ MaxTrinketSlots）✓
using Darkest.Core.Events;      // HeroTrinketUnequippedEvent（拖动点数判定的归属校验）✓
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// 🆕 **M4u · 饰品装卸 · UI 冒烟族**（2026-10-02）—— 独立成片（用户红线：程序文件 ≤600 行）✓
///
/// <para>🔴 **两条旗标都走真实控件**（红线 26：功能级验收走玩家路径）：</para>
/// <list type="bullet">
/// <item>`--hamlet-trinket-drop=&lt;英雄&gt;:&lt;饰品&gt;` ⇒ 找那个**孔**，走 `GearHeroSlot.TryDropPayload`
///       （与引擎拖动**同一入口**：先 `_CanDropData` 再 `_DropData`）✓</item>
/// <item>`--hamlet-trinket-unequip=&lt;英雄&gt;:&lt;饰品&gt;` ⇒ 找那个**方块**，按它的**真实矩形中心**合成一次
///       真实鼠标点击（引擎 `Viewport.PushInput`：按下 ＋ 抬起 ⇒ 走引擎命中测试）
///       （= 玩家点一下方块卸下；不是直调回调 ⇒ 连「点得到吗」一起验）✓</item>
/// </list>
///
/// <para>⚠️ **位置纪律**：本族**必须**排在 `--hamlet-hero-detail` **之后** —— 2 个孔/方块是「详情打开那一刻」
/// 建的（与 `HamletRoot.Build.SmokeFlags.cs` 的**播种族**正好相反：那族必须排在前）✓</para>
/// <para>⚠️ 详情没开 ／ 开的不是这个英雄 ／ 孔模板缺失 ⇒ **如实拒绝并留痕**，不静默换人 ✓</para>
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>饰品 UI 冒烟族入口（`HamletRoot.Build.cs` 在 `--hamlet-hero-detail` 之后调用）✓</summary>
    private void HandleTrinketUiSmokeFlags(string[] hamletArgs)
    {
        if (FindSmokeArg(hamletArgs, "--hamlet-trinket-drop=") is { } dropSpec)
        {
            TrinketDropSmoke(dropSpec);
        }

        if (FindSmokeArg(hamletArgs, "--hamlet-trinket-unequip=") is { } unequipSpec)
        {
            TrinketUnequipSmoke(unequipSpec);
        }

        if (FindSmokeArg(hamletArgs, "--hamlet-trinket-dragout=") is { } dragOutSpec)
        {
            TrinketDragOutSmoke(dragOutSpec);
        }
    }

    /// <summary>`--hamlet-trinket-dragout=&lt;英雄&gt;:&lt;饰品&gt;` ⇒ **拖出孔外松手**（合成真拖动：按下 ⇒ 越过引擎阈值 ⇒ 落在孔外松手）✓</summary>
    private void TrinketDragOutSmoke(string spec)
    {
        if (!TryResolveDetailHero(spec, "--hamlet-trinket-dragout", out string heroId, out string trinketId))
        {
            return;
        }

        QueueTrinketDrag(heroId, trinketId, 1);
    }

    /// <summary>排队**下一帧**再拖（同点击：装卸后重建 ／ 容器排版都是 deferred）✓</summary>
    private void QueueTrinketDrag(string heroId, string trinketId, int attempt)
        => Callable.From(() => TrinketDragStep(heroId, trinketId, attempt)).CallDeferred();

    /// <summary>
    /// 真拖动的**单次尝试**：现找方块 ⇒ 从方块中心按下、越过阈值拖到**孔外**松手 ⇒ 现读持有点数；
    /// 引擎据此走完整拖放（源 `_GetDragData` ⇒ 落点 `_CanDropData` ⇒ 没人收 ⇒ `NotificationDragEnd` ⇒ `DraggedOut`）✓
    /// </summary>
    private void TrinketDragStep(string heroId, string trinketId, int attempt)
    {
        if (_detailTrinketGrid is null)
        {
            GD.Print("[M4u·饰品·冒烟] 详情格未建 ⇒ 拖出卸下不通过（如实留痕）✓");
            return;
        }

        GearHeroSquare? square = FindTrinketSquare(trinketId);
        if (square is null)
        {
            if (attempt < TrinketClickAttempts)
            {
                QueueTrinketDrag(heroId, trinketId, attempt + 1);
                return;
            }

            GD.Print($"[M4u·饰品·冒烟] 详情整棵子树里找不到「{trinketId}」的方块（本来就没装 ／ 已卸下）⇒ 拖出卸下不通过（如实留痕）✓");
            return;
        }

        Rect2 rect = square.GetGlobalRect();
        if (rect.Size.X < 1f || rect.Size.Y < 1f)
        {
            GD.Print($"[M4u·饰品·冒烟] 方块「{trinketId}」第 {attempt} 次：矩形还没落定 {rect} ⇒ 再等一帧（容器排版是 deferred）✓");
            QueueTrinketDrag(heroId, trinketId, attempt + 1);
            return;
        }

        // 🔴 拖动比点击多一道门：**连续两帧读到同一个矩形**才动手 —— 布局未落定那一帧按下的是旧实例，
        //    会把**另一件**拖出去（2026-10-02 实测踩过：拖 crow_tailfeather 却卸了 crow_wingfeather）✓
        if (!_trinketDragRectSeen || !_trinketDragLastRect.Equals(rect))
        {
            _trinketDragLastRect = rect;
            _trinketDragRectSeen = true;
            GD.Print($"[M4u·饰品·冒烟] 方块「{trinketId}」第 {attempt} 次：矩形 {rect} 还没连续两帧不变" +
                     "（按下会拖错那件 ⇒ 等布局落定）✓");
            QueueTrinketDrag(heroId, trinketId, attempt + 1);
            return;
        }

        _trinketDragRectSeen = false;   // 动手前清门：重试时重新攒「两帧同矩形」✓
        int before = ExpeditionContext.Roster?.TrinketsOf(heroId).Count ?? -1;
        Vector2 from = rect.GetCenter();
        Vector2 to = new(Math.Max(8f, rect.Position.X - 240f), from.Y);   // 🔴 孔外的空白处：只有孔收 trinket 族载荷 ⇒ 没人收 = 拖出 ✓
        GD.Print($"[M4u·饰品·冒烟] 第 {attempt} 次：从方块中心 {from} 拖到孔外 {to} 松手" +
                 "（引擎 Viewport.PushInput：按下 ＋ 越过阈值 ＋ 抬起 ⇒ 走完整拖放）✓");
        DragFromTo(from, to);
        // 🔴 引擎的拖放收尾（`NotificationDragEnd` ⇒ `DraggedOut`）不在这一次 push 里同步落地
        //    ⇒ 立刻读数会读到「还没卸」的假阴性（2026-10-02 实测踩过）⇒ **下一帧再核** ✓
        Callable.From(() => TrinketDragCheck(heroId, trinketId, attempt, before)).CallDeferred();
    }

    /// <summary>拖动的**下一帧核对**：点数真降了 ⇒ 报读数；还没降 ⇒ 再等一帧重试（上限），用完如实上报 ✓</summary>
    private void TrinketDragCheck(string heroId, string trinketId, int attempt, int before)
    {
        int after = ExpeditionContext.Roster?.TrinketsOf(heroId).Count ?? -1;
        // 🔴 点数降了 ≠ 就是这一次拖动干的（同屏另一件也可能被卸）⇒ 再加一道**事件归属**校验 ✓
        if (after == before - 1 && LastTrinketUnequipMatches(heroId, trinketId))
        {
            GD.Print($"[M4u·饰品·冒烟] 拖出后现持 {after}/{Roster.MaxTrinketSlots}（拖前 {before}）" +
                     $"　事件={LastTrinketEventText()} ✓");
            return;
        }

        if (attempt < TrinketClickAttempts)
        {
            GD.Print($"[M4u·饰品·冒烟] 第 {attempt} 次拖出还没生效（现持仍 {after}）⇒ 再等一帧重试 ✓");
            QueueTrinketDrag(heroId, trinketId, attempt + 1);
            return;
        }

        GD.Print($"[M4u·饰品·冒烟] 拖了 {attempt} 次，现持仍 {after}/{Roster.MaxTrinketSlots}（拖前 {before}）" +
                 "　🔴 引擎拖放没走通（拖动阈值 ／ 命中未通过）⇒ 如实上报，不假装卸下");
    }

    /// <summary>
    /// 最近一条卸下事件是不是**这一次那一件**（点数降了不等于就是拖动干的 ⇒ 归属校验；红线 26）✓
    /// </summary>
    private bool LastTrinketUnequipMatches(string heroId, string trinketId)
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            if (_log.Events[i] is HeroTrinketUnequippedEvent u)
            {
                return u.HeroId == heroId && u.TrinketId == trinketId;
            }
        }

        return false;
    }

    /// <summary>合成一次**真拖动**：按下 ⇒ 三小步（累计远超引擎阈值）⇒ 在目标点抬起 ✓</summary>
    private void DragFromTo(Vector2 from, Vector2 to)
    {
        Viewport vp = GetViewport();
        vp.PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left,
            Pressed = true,
            Position = from,
            GlobalPosition = from,
        }, true);

        Vector2 step = (to - from) / 3f;
        Vector2 at = from;
        for (int i = 0; i < 3; i++)
        {
            at += step;
            vp.PushInput(new InputEventMouseMotion
            {
                Position = at,
                GlobalPosition = at,
                Relative = step,   // 🔴 引擎靠 relative 累积越过拖动阈值（不给我们就不算拖）✓
                ButtonMask = MouseButtonMask.Left,
            }, true);
        }

        vp.PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            ButtonMask = (MouseButtonMask)0,
            Pressed = false,
            Position = to,
            GlobalPosition = to,
        }, true);
    }

    /// <summary>
    /// `--hamlet-trinket-drop=` ⇒ **落孔**（玩家路径：孔自己的 `TryDropPayload`）✓
    /// <para>🔴 值可用 `;` 串多步（冒烟要一次跑完一条链：装上 ⇒ 再投同一件拿内核「重复」理由）；按序执行 ✓</para>
    /// </summary>
    private void TrinketDropSmoke(string spec)
    {
        foreach (string one in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            TrinketDropStep(one);
        }
    }

    /// <summary>落孔的**单步**（解析 ＋ 选孔 ＋ 走孔自己的入口）✓</summary>
    private void TrinketDropStep(string spec)
    {
        if (!TryResolveDetailHero(spec, "--hamlet-trinket-drop", out string heroId, out string trinketId))
        {
            return;
        }

        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            GD.Print("[M4u·饰品·冒烟] 名册未加载 ⇒ 落孔不通过（如实留痕）✓");
            return;
        }

        // 🔴 选孔规则与玩家一致：**下一格空位**；两格已满 ⇒ 仍投满格（拿内核「槽满」原样理由 ⇒ T4 读数）✓
        int slot = Math.Min(roster.TrinketsOf(heroId).Count + 1, Roster.MaxTrinketSlots);
        if (_detailTrinketGrid?.GetNodeOrNull($"HeroTrinketSlot{slot}") is not GearHeroSlot hole)
        {
            GD.Print($"[M4u·饰品·冒烟] 详情里没有可落孔的 GearHeroSlot{slot}" +
                     "（详情未开 ／ 孔模板缺失已回落读数框）⇒ 落孔不通过（如实留痕）✓");
            return;
        }

        string payload = DragPayload.Encode(DragPayload.TrinketTag, trinketId);
        bool accepted = hole.TryDropPayload(payload);
        GD.Print($"[M4u·饰品·冒烟] 落孔载荷「{payload}」⇒ 孔{(accepted ? "收下" : "拒收")}" +
                 $"（现持 {roster.TrinketsOf(heroId).Count}/{Roster.MaxTrinketSlots}）" +
                 $"　事件={LastTrinketEventText()} ✓");
    }

    /// <summary>
    /// `--hamlet-trinket-unequip=` ⇒ **点方块卸下**（玩家路径：**合成真实鼠标点击**，引擎 `Viewport.PushInput`）✓
    /// <para>⚠️ **必须延迟一帧**：本方法在 `_Ready` 里跑，此时容器还没排完版（`GridContainer` 的 sort 是 deferred）
    /// ⇒ 立刻取 `GetGlobalRect` 只会拿到旧矩形、点空。延迟一帧后布局已定，再按方块的**真实矩形中心**点下去 ✓</para>
    /// </summary>
    private void TrinketUnequipSmoke(string spec)
    {
        if (!TryResolveDetailHero(spec, "--hamlet-trinket-unequip", out string heroId, out string trinketId))
        {
            return;
        }

        QueueTrinketClick(heroId, trinketId, 1);
    }

    /// <summary>
    /// 冒烟**帧数**上限 —— 装卸后的重建 ／ 容器排版都是 deferred ⇒ 一帧不够就再等一帧，但**有限**（含等布局的帧，不只按压次数）✓
    /// <para>⚠️ 拖动与点击**共用**这一份预算，而拖动还要先攒「连续两帧同矩形」（见下）⇒ 10 帧才够用：
    /// 2026-10-02 实测一次拖动前等了 4 帧布局（卸下重建会把整格重排）✓</para>
    /// </summary>
    private const int TrinketClickAttempts = 10;

    // 🔴 拖动的「布局落定」门（见 `TrinketDragStep`）：连续两帧读到同一个矩形才动手 —— 只给拖动用：
    //    点击点错位置最多是没命中（下一帧重试即可），拖动点错位置会**拖错那件**（2026-10-02 实测踩过）✓
    private Rect2 _trinketDragLastRect;
    private bool _trinketDragRectSeen;

    /// <summary>排队**下一帧**再点（装卸后孔/方块会被重建、容器排版也是 deferred ⇒ 必须等布局落定）✓</summary>
    private void QueueTrinketClick(string heroId, string trinketId, int attempt)
        => Callable.From(() => TrinketClickStep(heroId, trinketId, attempt)).CallDeferred();

    /// <summary>
    /// 真点击的**单次尝试**：现找方块（每次重找 —— 重建会换实例）⇒ 按真实矩形中心合成「按下 ＋ 抬起」⇒ 现读持有点数；
    /// 方块还没排版（矩形退化）／ 没命中 ⇒ **再等一帧重试**（上限 `TrinketClickAttempts`），用完就如实上报 ✓
    /// </summary>
    private void TrinketClickStep(string heroId, string trinketId, int attempt)
    {
        if (_detailTrinketGrid is null)
        {
            GD.Print("[M4u·饰品·冒烟] 详情格未建 ⇒ 卸下不通过（如实留痕）✓");
            return;
        }

        GearHeroSquare? square = FindTrinketSquare(trinketId);
        if (square is null)
        {
            if (attempt < TrinketClickAttempts)
            {
                QueueTrinketClick(heroId, trinketId, attempt + 1);   // 装卸后的重建可能还没落定 ⇒ 再等一帧 ✓
                return;
            }

            GD.Print($"[M4u·饰品·冒烟] 详情整棵子树里找不到「{trinketId}」的方块（本来就没装 ／ 已卸下）⇒ 卸下不通过（如实留痕）✓");
            return;
        }

        int before = ExpeditionContext.Roster?.TrinketsOf(heroId).Count ?? -1;
        Rect2 rect = square.GetGlobalRect();
        if (rect.Size.X < 1f || rect.Size.Y < 1f)
        {
            GD.Print($"[M4u·饰品·冒烟] 方块「{trinketId}」第 {attempt} 次：矩形还没落定 {rect} ⇒ 再等一帧（容器排版是 deferred）✓");
            QueueTrinketClick(heroId, trinketId, attempt + 1);
            return;
        }

        Vector2 at = rect.GetCenter();
        GD.Print($"[M4u·饰品·冒烟] 第 {attempt} 次：方块「{square.Text}」矩形 {rect} ⇒ 点它的中心 {at}" +
                 "（引擎 Viewport.PushInput：按下＋抬起 ⇒ 走引擎命中测试，不是直调回调）✓");
        ClickAt(at);
        int after = ExpeditionContext.Roster?.TrinketsOf(heroId).Count ?? -1;
        if (after == before - 1)
        {
            GD.Print($"[M4u·饰品·冒烟] 点击后现持 {after}/{Roster.MaxTrinketSlots}（点击前 {before}）" +
                     $"　事件={LastTrinketEventText()} ✓");
            return;
        }

        if (attempt < TrinketClickAttempts)
        {
            GD.Print($"[M4u·饰品·冒烟] 第 {attempt} 次没命中（现持仍 {after}）⇒ 再等一帧重试 ✓");
            QueueTrinketClick(heroId, trinketId, attempt + 1);
            return;
        }

        GD.Print($"[M4u·饰品·冒烟] 点了 {attempt} 次，现持仍 {after}/{Roster.MaxTrinketSlots}（点击前 {before}）" +
                 "　🔴 方块收不到真实点击 ⇒ 如实上报，不假装卸下");
    }

    /// <summary>
    /// 在详情格**整棵子树**里找那件方块 —— 🔴 方块是嵌在孔（`GearHeroSlot.SetItem`）里的，
    /// 只扫格的直接子节点会漏（2026-10-02 冒烟实测踩过）；遍历走引擎内建 `FindChildren`（不自己写递归）✓
    /// <para>⚠️ `owned: false` 必须给：代码建的节点没有 owner，`owned: true` 会把它们全过滤掉 ✓</para>
    /// </summary>
    private GearHeroSquare? FindTrinketSquare(string trinketId)
    {
        if (_detailTrinketGrid is null)
        {
            return null;
        }

        foreach (Node node in _detailTrinketGrid.FindChildren("*", string.Empty, true, false))
        {
            if (node is GearHeroSquare square && string.Equals(square.Payload, trinketId, StringComparison.Ordinal))
            {
                return square;
            }
        }

        return null;
    }

    /// <summary>
    /// 合成一次**左键点击**（按下 ＋ 抬起）—— 走引擎内建输入管线（`Viewport.PushInput`；
    /// `localCoords: true` = 点位已是视口坐标 ⇒ 跳过窗口→视口的拉伸换算）：命中测试由引擎做，
    /// UI 侧一句判据都不写（红线 21 (b)）✓
    /// </summary>
    private void ClickAt(Vector2 at)
    {
        Viewport vp = GetViewport();
        foreach (bool down in new[] { true, false })
        {
            vp.PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                ButtonMask = down ? MouseButtonMask.Left : (MouseButtonMask)0,
                Pressed = down,
                Position = at,
                GlobalPosition = at,
            }, true);
        }
    }

    /// <summary>
    /// 解析 `&lt;英雄&gt;:&lt;饰品&gt;` 并**校验详情正开着这个英雄**（红线 26：不静默换人）——
    /// 任一条不成立 ⇒ 打印原样理由并返回 `false` ✓
    /// </summary>
    private bool TryResolveDetailHero(string spec, string flagName, out string heroId, out string trinketId)
    {
        heroId = string.Empty;
        trinketId = string.Empty;
        int sep = spec.IndexOf(':');
        if (sep <= 0 || sep >= spec.Length - 1)
        {
            GD.Print($"[M4u·饰品·冒烟] {flagName}=「{spec}」不是「英雄:饰品」⇒ 拒绝（不猜）✓");
            return false;
        }

        heroId = spec[..sep];
        trinketId = spec[(sep + 1)..];
        if (!DetailOpen || _detailHeroId != heroId)
        {
            GD.Print($"[M4u·饰品·冒烟] {flagName}：详情={_detailHeroId ?? "（关）"}（要的是 {heroId}）⇒ 拒绝" +
                     "（详情里的孔/方块只属于当前这个英雄 ⇒ 不静默换人）✓");
            return false;
        }

        return true;
    }
}
