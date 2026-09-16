namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **一趟远征的结局**（策划 `retreat.md §4④` 的语义拆分 · `#352`）：
/// **一趟只有三类结局** —— **走完主干到终点 / 放弃远征 / 全灭** ✓
///
/// ⚠️ 为什么要有它（而不是一个 `IsFinished` 布尔）：
///   · 旧实现：**撤退 = 本趟结束**（`IsFinished = true`）⇒ 与用户裁定的 **DD 形态**不符 ⚠️
///   · 新语义：**撤退是【一场】的选择**（退出本场战斗 ⇒ 回地图当前格继续走）
///             **放弃远征才是【一趟】的选择**（结束本趟 ⇒ 回城，未完成）✓
///   ⇒ 📌 一个布尔**表达不了"三种结局"** ⇒ 用枚举，**单一写者** = `ExpeditionFlow.Outcome` ✓
///
/// ⚠️ **命名避让**：本项目已有一个**同名层的** `RunOutcome`（`RunSession.cs` 里的 **record**，
///    用于"逐场曲线 + 完成口径"）⇒ 本枚举**故意叫 `ExpeditionOutcome`**，不与它撞名 ✓
/// </summary>
public enum ExpeditionOutcome
{
    /// <summary>本趟进行中 —— 🔴 **撤退后仍是它**（这正是本次拆分的要点 ✓）</summary>
    InProgress,

    /// <summary>走完主干到终点 ⇒ 回城（完成）✓</summary>
    Completed,

    /// <summary>放弃远征（**地图层**动作）⇒ 回城（未完成）✓</summary>
    Abandoned,

    /// <summary>全灭（**战斗层**结果）⇒ 回城（未完成）✓</summary>
    Wiped,
}
