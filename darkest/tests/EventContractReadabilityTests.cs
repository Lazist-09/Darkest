using System;
using System.Linq;
using Darkest.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// P21 ⑩⑪⑫（**加载级可读性底线**，架构 v1.03 裁定「两级分工」）：
/// 运行时语义查不到（那是执行级的事），但**字段齐备性**必须静态可查 ——
/// 防"事件名对但字段改名/缺字段"再次让 V5 / ㉑㉒㉕ 变空（与当年缺 `SkillUseEvent` 同类事故）。
///
/// 契约字段（`data_schema` §3.11，JSON 小写）↔ C# 属性（PascalCase）：
/// · `LightChangedEvent`：`{from, to, tier, reason}` ↔ `{From, To, Tier, Reason}`
/// · `ScoutResultEvent`：`{roll, success, revealedNodeType}` ↔ `{Roll, Success, RevealedNodeType}`
/// </summary>
[TestClass]
public sealed class EventContractReadabilityTests
{
    [TestMethod]
    public void P21_10_11_LightChangedEvent_FieldNamesExactly()
    {
        string[] names = typeof(LightChangedEvent).GetProperties()
            .Where(p => p.DeclaringType == typeof(LightChangedEvent)).Select(p => p.Name).OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(
            new[] { "From", "Reason", "Tier", "To" }, names,
            "LightChangedEvent 字段必须恰为 {from, to, tier, reason}（P21 ⑩⑪；tier 为 O-72 的归因必需项）");
    }

    [TestMethod]
    public void P21_12_ScoutResultEvent_FieldNamesExactly()
    {
        string[] names = typeof(ScoutResultEvent).GetProperties()
            .Where(p => p.DeclaringType == typeof(ScoutResultEvent)).Select(p => p.Name).OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(
            new[] { "RevealedNodeType", "Roll", "Success" }, names,
            "ScoutResultEvent 字段必须恰为 {roll, success, revealedNodeType}（P21 ⑫；否则 V5/㉕ 会变空）");
    }

    [TestMethod]
    public void P21_12_ScoutResult_FailureMeansNullReveal_OnType()
    {
        // 执行级语义的**类型面**校验（运行时保证在 ScoutingTests 里）：
        // 字段可空 ⇒ 允许 null；此处锁"成功才带类型"的契约注释不会漂移
        var ok = new ScoutResultEvent(0.5, true, "battle");
        var fail = new ScoutResultEvent(0.5, false, null);
        Assert.AreEqual("battle", ok.RevealedNodeType);
        Assert.IsNull(fail.RevealedNodeType, "失败 ⇒ null（策划 #263 / 执行级）");
    }
}
