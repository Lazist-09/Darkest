namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **远征相位**（架构 `blueprint §9.17.0`；`#327` 的无缝单场景前置）：
/// 它是**规则状态**（与光照/资源/进度同层），**不是表现层状态** ⚠️
/// ⇒ 单一真值放在内核；表现层**只读派生谓词**（`CanShowCampUi` 等），**绝不自己推断相位** ✓
///
/// 为什么要它（红线 25 的典型）：`ExpeditionSession.CanCamp` 原先**只看 `Firewood`** ⇒
/// **内核并不约束"战斗中不许扎营"** ⇒ 面板一旦挂进地图模式，玩家就能**战斗中扎营** ⚠️
/// </summary>
public enum FlowPhase
{
    /// <summary>在地图上走（可扎营 / 可看路径选择 / 可遇 Curio）✓</summary>
    Walking,

    /// <summary>正在扎营（可继续用扎营技能；**不可**进战斗/再次扎营）✓</summary>
    Camp,

    /// <summary>战斗中（**不可**扎营、**不可**选路、**不可**开 Curio）✓</summary>
    Battle,

    /// <summary>本趟已结算（回城/团灭/完成）✓</summary>
    Resolved,
}
