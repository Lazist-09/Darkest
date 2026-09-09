namespace Darkest.Core.Contracts;

/// <summary>
/// 槽位三态（data_schema §2.1 `SlotState` / glossary §1）：任一时刻一槽只落一态。
/// JSON 词汇 `occupied` / `empty` / `blocked`（与枚举名小写一一对应）。
/// </summary>
public enum SlotState
{
    /// <summary>有角色（Occupied）</summary>
    Occupied,

    /// <summary>空（Empty）——三态之一；区别于「空位」（离场后留在队尾的连续空槽，glossary §1 辨析 2）</summary>
    Empty,

    /// <summary>被障碍占据（Blocked）</summary>
    Blocked,
}