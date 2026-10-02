// 🔴 从 BattleRoot.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3：只抽【冒烟与审计入口】）——只搬家、零行为改动 ✓
//    本文件 = 脚本/命令行驱动的取证入口：自动打完 + 自动点继续 · 焦点/容器审计 · E 区页签与卡序 · 放弃远征透传
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_ui x6 · _autoContinue x1（本片声明，EndGame 读取）
//    【依赖主类公有面】HasActiveBattle · Director · EndGame（结算片）

using Godot;

namespace Darkest.Gameplay.Scene;

public partial class BattleRoot
{
    /// <summary>
    /// 🔴 供**跨场景步进冒烟**：自动打完本场 + 自动点【继续（回远征）】（真实路径）。
    /// ⚠️ **必须延迟调用**（与 `--battle-auto-finish` 同路径）：直接调用会在"信号/`_Ready` 内改场景"时踩坑
    /// （实测：直接调用 ⇒ 战斗打不完、也切不出去）。
    /// </summary>
    public void PressAutoFinish()
    {
        // 🔴 片 3.1：**地图模式没有战斗** ⇒ 冒烟/脚本的"自动打完"必须**如实拒绝**（否则刷屏 NRE ⚠️）✓
        if (!HasActiveBattle)
        {
            GD.Print("[片3.1] 无活动战斗 ⇒ 拒绝「自动打完」（这是地图模式，不是战斗）✓");
            return;
        }

        _autoContinue = true; // 🔴 让 `EndGame` 里的"点继续"也生效（不再依赖命令行旗标）
        CallDeferred(nameof(AutoFinishBattle));
    }

    /// <summary>🔴 冒烟用：把"按下【放弃远征】"透到 UI（**真实 `Pressed`** ⇒ 走玩家路径，红线 18）✓</summary>
    public void PressAbandonUi() => _ui?.PressAbandon();

    /// <summary>本实例是否要"自动点继续"（由 `PressAutoFinish` 置位；命令行旗标仍并行生效）。</summary>
    private bool _autoContinue;

    /// <summary>🔴 审计清单③ 冒烟：打印焦点审计（键盘/手柄导航的取证）。</summary>
    private void PrintFocusAudit()
    {
        GD.Print($"[焦点审计] {_ui.FocusAudit()}");
        GD.Print($"[容器审计] {_ui.ContainerAudit()}"); // 清单②（容器+锚点）的取证
    }

    /// <summary>🔴 片③ 冒烟：**切到 E 区第 N 页**（0 详情 ／ 1 日志 ／ 2 序列 ／ 3 编成 ／ 4 地图）。</summary>
    public void ShowTab(int page)
    {
        _ui.SetMultiFunctionPage(page);
        GD.Print($"[片③] 战斗界面：E 区当前页 = {_ui.MultiFunctionPage}（请求 {page}）");
        GD.Print($"[片③·页内容] {_ui.DescribeCurrentPage()}");
    }

    /// <summary>
    /// 🔴 片③ 冒烟：**真实点击第 N 张我方卡**（`N` = **卡序，0 基**）⇒ 应锁进 E 区详情页。
    /// ⚠️ 卡序 ≠ 槽位：我方卡按 DD 式从左到右显示 **4 · 3 · 2 · 1** ⇒ 卡序 0 = **槽位 4** ✓
    ///    我原先把 `N` 直接当槽位用 ⇒ `--battle-card=0` 触发 `ArgumentOutOfRangeException: 槽位 0 越界 [1,6]` ⚠️
    ///    （由 UI 设计师在窗口指出，附证据）⇒ 现按【卡序 → 槽位】映射，且**越界只打印不抛异常** ✓
    /// </summary>
    private void ShowCardDetail(int cardIndex)
    {
        const int PlayerCombatCards = 4; // 我方战斗位 4 张（显示顺序 4·3·2·1）
        if (cardIndex < 0 || cardIndex >= PlayerCombatCards)
        {
            GD.Print($"[片③] --battle-card={cardIndex} 越界：卡序合法范围 0..{PlayerCombatCards - 1}" +
                     "（我方卡从左到右显示 4·3·2·1；卡序 0 = 槽位 4）—— 不抛异常，仅提示 ✓");
            return;
        }

        int slot = PlayerCombatCards - cardIndex; // 卡序 0 → 槽 4；1 → 3；2 → 2；3 → 1 ✓
        _ui.PressCard(slot, isPlayer: true);
        GD.Print($"[片③] 点单位卡 ⇒ 卡序 {cardIndex} 映射到槽位 {slot}；E 区详情页锁定槽位 = {_ui.LockedSlot}");
    }

    /// <summary>
    /// 🔴 片③ 冒烟：**切到 E 区多功能框的【地图】页**（真实走 `SetMultiFunctionPage` 同一入口）。
    /// ⚠️ 页签顺序 = { 详情 0 ／ 日志 1 ／ 序列 2 ／ 编成 3 ／ **地图 4** } ⇒ 这里必须是 **4**。
    ///    我原先写 2（= 序列）⇒ 实测 `--battle-map` 落在【序列】页，**地图页冒烟根本走不到** ⚠️
    ///    （由 UI 设计师在窗口指出，附证据：`--battle-tab=4` 才是地图页）—— 已修 ✓
    /// </summary>
    private void ShowMapPage()
    {
        const int MapPageIndex = Darkest.UI.BattleUI.MapPageIndex; // 🔴 单一出处：引用 UI 的页签表常量（原另写一份 4 ⇒ 两处真值）✓
        _ui.SetMultiFunctionPage(MapPageIndex);
        GD.Print($"[片③] 战斗界面：E 区当前页 = {_ui.MultiFunctionPage}（{MapPageIndex} = 地图）");
        GD.Print($"[片③] {_ui.DescribeMiniMap()}");
    }

    /// <summary>冒烟用：用**小型自动玩家**把本场**真的打完**（走真实战斗规则）⇒ 再走既有 `EndGame` 路径。</summary>
    private void AutoFinishBattle()
    {
        if (!HasActiveBattle)
        {
            GD.Print("[片3.1] 无活动战斗 ⇒ 不自动打完（地图模式）✓");
            return;
        }
        DirectorBridge.DirectorHandle handle = DirectorBridge.BuildFromRes(this);
        var auto = new Darkest.Gameplay.Sim.Run.SimplePlayerAuto(handle.Skills);
        var rng = new Darkest.Core.Rng.RngProvider(20260909);

        int round = 1;
        for (; round <= 60 && !Director.IsBattleOver; round++)
        {
            Director.RunFullRound(rng, u => auto.Decide(u, Director));
        }

        bool win = Director.Enemy.OccupiedPositions(false).Count == 0;
        GD.Print($"[BattleRoot] --battle-auto-finish ⇒ **自动玩家打完**：{(win ? "胜" : "败")}　回合 {Director.Round}");
        EndGame(win ? "我方胜利（自动玩家）" : "敌方胜利（自动玩家）");
    }
}
