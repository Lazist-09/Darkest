// 🔴 从 `SkillsConfig.cs` 拆出（用户红线 ≤600 行 · 架构 file_size_split §1.2 的 ③.数据层）
//    职责 = **加载级 fail-fast 校验**：技能数 ／ 唯一 id ／ owner_unit ／ target ／ self_slots ／ effects 成对 ／ use_limit ／ 伤害段 ／ 池外 move ／ 各原型计数
//    来源 = 原 `SkillsConfig` 记录体的 `Validate` 段（逐行搬家，**只搬家、零行为**）
//    依赖私有成员 = `ResPath` ／ `ExpectedCount`（同记录私有常量）· `_playerSet`（同记录私有静态字段，**本片声明**）· `PlayerOwner`
//    依赖公有面 = `SkillTemplateConfig` ／ `TargetSpec` ／ `EffectSpec` ／ `UseLimitSpec` ／ `DamageSegment` ／ `SelfSlots`
//    复核 = `tools/dsh/audit_split_integrity.py`（拆前后成员名比对 · 期望 no member lost）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

public sealed partial record SkillsConfig
{

    private static void Validate(SkillsConfig cfg)
    {
        if (cfg.Skills is null || cfg.Skills.Count != ExpectedCount)
        {
            throw new InvalidDataException(
                $"{ResPath}: 技能数必须为 {ExpectedCount}（O-16：36 我方 + 7 敌方），实际 {cfg.Skills?.Count}。");
        }

        var ids = new HashSet<string>();
        foreach (SkillTemplateConfig s in cfg.Skills)
        {
            if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id))
            {
                throw new InvalidDataException($"{ResPath}: 技能 id \"{s.Id}\" 缺失或重复（P1）。");
            }

            if (string.IsNullOrWhiteSpace(s.Name) || (s.OwnerUnit is null && !s.PoolExternal) || s.OwnerUnit is "")
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{s.Id}\" owner_unit 非法（池内必填、池外 move 不填；P1/F1；跨文件存在性由 P1 校验/数据门禁用例核对）。");
            }

            ValidateTarget(s);
            ValidateSelfSlotsRange(s);
            ValidateEffectPairing(s);
            ValidateUseLimit(s);
            ValidateSegments(s);
            ValidatePoolExternalMove(s); // P12（v0.45 #180）
        }

        ValidateCounts(cfg);
        ValidateMoveCount(cfg); // P12：池外移动恰 4 条
    }

    private static void ValidateTarget(SkillTemplateConfig s)
    {
        TargetSpec t = s.Target;
        if (t.Scope == SkillTargetScope.Slots)
        {
            int max = t.Side switch { "player" => 6, "enemy" => 4, _ => 0 };
            if (max == 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" target.side 必须为 player/enemy（P2）。");
            }

            if (t.Slots is null || t.Slots.Count == 0 || t.Slots.Any(pos => pos is < 1 or > 6))
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" target.slots 越界（P2；敌方 ≤4、我方 ≤6）。");
            }
        }
    }

    private static void ValidateSelfSlotsRange(SkillTemplateConfig s)
    {
        if (s.SelfSlots.IsAll)
        {
            return;
        }

        int max = PlayerOwner(s.OwnerUnit) ? 6 : 4; // 我方原型 1~6、敌方原型 1~4
        if (s.SelfSlots.Slots.Any(pos => pos < 1 || pos > max))
        {
            throw new InvalidDataException(
                $"{ResPath}: \"{s.Id}\" self_slots 越界（{s.OwnerUnit} 侧合法 [1,{max}]，P2 同级）。");
        }
    }

    private static bool PlayerOwner(string owner)
        => _playerSet is not null ? _playerSet.Contains(owner) : owner is "warrior" or "tank" or "medic" or "commissar";

    /// <summary>F1（#190）：调用方注入的数据派生我方原型集合（null = 未注入，按旧白名单；用于 self_slots 上限与池内计数）。</summary>
    private static IReadOnlyCollection<string>? _playerSet;

    private static void ValidateEffectPairing(SkillTemplateConfig s)
    {
        // O-24 成对规则：probability 与 resist_axis 必须同有同无（无概率 = 直挂、不过抗性）
        foreach (EffectSpec e in s.Effects)
        {
            bool hasP = e.Probability is not null;
            bool hasR = !string.IsNullOrEmpty(e.ResistAxis);
            if (hasP != hasR)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{s.Id}\" effects[{e.Type}] 的 probability 与 resist_axis 必须同有同无（O-24）。");
            }
        }
    }

    private static void ValidateUseLimit(SkillTemplateConfig s)
    {
        UseLimitSpec u = s.UseLimit;
        if (u.Type == UseLimitType.EveryNRounds)
        {
            throw new InvalidDataException($"{ResPath}: \"{s.Id}\" every_n_rounds 为模板预留，切片不得启用。");
        }

        if ((u.Type is UseLimitType.Cooldown or UseLimitType.PerBattle) && (u.Value is null or <= 0))
        {
            throw new InvalidDataException($"{ResPath}: \"{s.Id}\" use_limit.value 必须 > 0。");
        }
    }

    private static void ValidateSegments(SkillTemplateConfig s)
    {
        if (s.Damage is null)
        {
            return;
        }

        foreach (DamageSegment seg in s.Damage.Segments)
        {
            if (seg.Type == DamageSegmentType.Flat && seg.Multiplier is null)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" flat 段缺 multiplier。");
            }

            if (seg.Type == DamageSegmentType.MissingHp && (seg.Base is null || seg.Coefficient is null))
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" missing_hp 段缺 base/coefficient。");
            }
        }
    }

    private static void ValidateCounts(SkillsConfig cfg)
    {
        if (_playerSet is not null)
        {
            // F1（#190）：按数据分组——每个我方原型池内恰 9（不写死 4 个键）；其余 owner 组不得为空
            var groups = cfg.Skills.Where(s => !s.PoolExternal)
                .GroupBy(s => s.OwnerUnit)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (string owner in _playerSet)
            {
                if (!groups.TryGetValue(owner, out int n) || n != 9)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 我方原型 \"{owner}\" 池内技能数必须为 9（实际 {(groups.TryGetValue(owner, out int m) ? m : 0)}，P3/F1）。");
                }
            }

            foreach ((string owner, int n) in groups.Where(kv => !_playerSet.Contains(kv.Key)))
            {
                if (n < 1)
                {
                    throw new InvalidDataException($"{ResPath}: \"{owner}\" 池内技能数为 0（P3）。");
                }
            }

            return;
        }

        int[] player = { 0, 0, 0, 0 };
        int[] enemy = { 0, 0, 0 };
        foreach (SkillTemplateConfig s in cfg.Skills)
        {
            switch (s.OwnerUnit)
            {
                case "warrior": player[0]++; break;
                case "tank": player[1]++; break;
                case "medic": player[2]++; break;
                case "commissar": player[3]++; break;
                case "melee_soldier": enemy[0]++; break;
                case "ranged_archer": enemy[1]++; break;
                case "caster": enemy[2]++; break;
            }
        }

        // F1（#191）：我方各原型池内 9（移动改为通用池外 1 条，不再逐原型各 1 条）；敌方 2/3/2 不变
        if (player.Any(c => c != 9) || enemy is not [2, 3, 2])
        {
            throw new InvalidDataException(
                $"{ResPath}: 各原型池内技能数必须为 9/9/9/9 + 2/3/2（实际 {string.Join("/", player)} + {string.Join("/", enemy)}，F1）。");
        }
    }

    /// <summary>P12（#180）：池外移动一致性——pool_external ⇒ move_range+战斗位+无伤害+无效果+无 CD+distance∈{1,2}；反之亦然。</summary>
    private static void ValidatePoolExternalMove(SkillTemplateConfig s)
    {
        bool isMove = s.Id == "move"; // F1（#191）：池外技能唯一 id = "move"（禁止 _move 后缀命名约定）
        if (s.PoolExternal)
        {
            if (!isMove || s.Target.Scope != SkillTargetScope.MoveRange)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 池外技能必须是 * _move 且 target.scope=move_range（P12）。");
            }

            if (!s.SelfSlots.IsAll && s.SelfSlots.Slots is { Count: 4 } sl && sl[0] == 1 && sl[3] == 4)
            {
                // 通过
            }
            else if (s.SelfSlots.IsAll)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 移动 self_slots 必须为 [1,2,3,4]（P12）。");
            }
            else
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 移动 self_slots 必须为 [1,2,3,4]（P12）。");
            }

            if (s.Damage is not null || s.Effects.Count > 0 || s.Displacement is not null
                || s.UseLimit.Type != UseLimitType.None)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 移动必须无伤害/无效果/无位移/无 CD（P12）。");
            }

            if (s.OwnerUnit is not null)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 池外通用技能不得绑定 owner_unit（P12/F1）。");
            }

            if (s.Target.Distance is not null)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 移动距离必须从 units.move_distance 读，技能不得自带 distance（P14/F1）。");
            }

            if (s.Id.EndsWith("_move", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{ResPath}: 禁止使用 _move 后缀命名池外技能（P12/F1）。");
            }
        }
        else if (s.Target.Scope == SkillTargetScope.MoveRange)
        {
            throw new InvalidDataException($"{ResPath}: \"{s.Id}\" 非池外技能不得使用 move_range（P12）。");
        }
    }

    private static void ValidateMoveCount(SkillsConfig cfg)
    {
        int moves = cfg.Skills.Count(s => s.PoolExternal);
        if (moves != 1)
        {
            throw new InvalidDataException($"{ResPath}: 池外技能必须恰 1 条（通用 move，实际 {moves}，P12/F1）。");
        }
    }
}
