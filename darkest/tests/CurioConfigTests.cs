using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **Curio（可交互物体）** —— 契约 `doc/modules/curio.md`（`#313`）。
/// 本用例只锁**配置层**（数据契约 + 加载防线）；功能级（V2/V3/V4/V5/V7）由内核/UI 用例覆盖。
/// </summary>
[TestClass]
public sealed class CurioConfigTests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    [TestMethod]
    public void ShippedCurios_LoadSix_AndEveryBareHandsSumsTo100()
    {
        CuriosConfig cfg = CuriosConfig.Parse(ReadData("curios.json"));
        CurioConfig[] curios = cfg.RealCurios.ToArray();

        Assert.AreEqual(6, curios.Length, "首版 6 个（`curio.md` §3）");
        foreach (CurioConfig c in curios)
        {
            int sum = c.BareHands.Sum(b => b.Chance);
            Assert.AreEqual(100, sum, $"\"{c.Id}\" 的 bare_hands 概率之和必须 = 100（否则掷到未定义分支）");
            Assert.IsTrue(c.BareHands.All(b => !string.IsNullOrWhiteSpace(b.Text)),
                $"\"{c.Id}\" 每条空手结果都要有**描述文本**（V6：不是「获得 2 个口粮」）");
        }
    }

    [TestMethod]
    public void Curio_Six_DesignChecks_HoldOnData()
    {
        CuriosConfig cfg = CuriosConfig.Parse(ReadData("curios.json"));

        // ② 至少一个 Curio 的【错误道具】是【确定的坏结果】（骸骨堆 ＋ 口粮 ⇒ −10 士气）
        CurioConfig bones = cfg.RealCurios.Single(c => c.Id == "cur_bone_pile");
        CurioItemResultConfig wrong = bones.ItemResults.Single(r => r.Item == "food");
        Assert.AreEqual(-10, wrong.Amount, "🔴 用错道具 = 确定的坏结果（curio.md §1.2 / §3 检查②）");

        // ③ 不同道具给【不同等级的好结果】（圣坛：空手 +20 ／ 支援包 +30）
        CurioConfig altar = cfg.RealCurios.Single(c => c.Id == "cur_altar");
        Assert.AreEqual(20, altar.BareHands.Single().Amount, "空手 +20%");
        Assert.AreEqual(30, altar.ItemResults.Single().Amount, "用支援包 ⇒ +30%（同级不同量）");

        // ⑤ 道具路径【不掷骰】⇒ 数据上必须能"按道具直查"（不需要概率字段）
        Assert.IsTrue(altar.ItemResults.All(r => r.Item.Length > 0), "道具结果是按道具 id 直查的确定项");
    }

    [TestMethod]
    public void CurioConfig_GuardsRejectBadData()
    {
        // 概率和不等于 100 ⇒ 拒绝加载
        const string bad = """
        { "curios": [ { "id": "x", "name": "x", "type": "curio", "curio_type": "T",
          "bare_hands": [ { "chance": 60, "kind": "food", "amount": 1, "text": "t" } ], "item_results": [] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => CuriosConfig.Parse(bad),
            "概率和 ≠ 100 ⇒ 必须拒绝（红线 21：写错就炸）");

        // 未登记的 kind ⇒ 拒绝加载（与 BuffDefsConfig.ConsumedEffectNames 同一做法）
        const string bad2 = """
        { "curios": [ { "id": "y", "name": "y", "type": "curio", "curio_type": "T",
          "bare_hands": [ { "chance": 100, "kind": "summon_dragon", "amount": 1, "text": "t" } ], "item_results": [] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => CuriosConfig.Parse(bad2),
            "未登记的 kind ⇒ 必须拒绝（红线 21）");
    }

    [TestMethod]
    public void DeferredKinds_AreRegistered_SoStageTwoIsVisible()
    {
        // 🔴 阶段二（数据里有、内核未接线）：必须**登记**而不是静默（红线 21）
        //    ✅ 当前**为空** —— 9 种 kind **全部接线**（阶段二机制保留，供将来新增 kind 显式登记）
        Assert.AreEqual(0, CuriosConfig.DeferredKinds.Count,
            "9 种 kind 都已接线 ⇒ 阶段二应为空；若你新增了未接线的 kind，请登记到 DeferredKinds");

        // 且已实现清单与阶段二清单**互斥**
        Assert.IsFalse(CuriosConfig.ConsumedKinds.Overlaps(CuriosConfig.DeferredKinds),
            "两个清单必须互斥（我踩过一次：同一条目同时在两边 ⇒ 计数虚高）");

        // 🔴 九种 kind 逐一在位（防"悄悄少了一种"）
        string[] expected = { "none", "food", "firewood", "gold", "morale_team", "light", "scout",
            "damage_buff", "disease_one", "trait_positive" };
        foreach (string k in expected)
        {
            Assert.IsTrue(CuriosConfig.ConsumedKinds.Contains(k), $"kind 「{k}」应在已实现清单里");
        }
    }
}
