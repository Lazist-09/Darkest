namespace Darkest.Core.Contracts;

/// <summary>
/// 槽类型（data_schema §2.1 `SlotKind`）：战斗位 / 支援位。
/// “支援位是位置不是职业”（#139）：槽对任意角色开放，可用性限制归 M3 判定。
/// </summary>
public enum SlotKind
{
    Combat,
    Support,
}