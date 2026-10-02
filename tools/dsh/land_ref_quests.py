#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""land_ref_quests.py —— M14：把【参考项目降级源】的 quests 段落库到 darkest/data/quests.json ✓

源（**降级**）：reports/unity_ref/quests_loot_narration_from_ref.json 的 "quests" 段
  （上游 = F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonQuests.json）
  🔴 E 盘一手（campaign/quest/JsonQuests.json）**当前不在原位**（只剩 app.log + mods）
     ⇒ 按 doc/architecture/source_priority.md §2/§3 降级到参考件，并把实际路径写进 _source ✓

三处**有意规范化**（其余逐字转写，保证与参考件可逐键对照）：
  ① generation.rewards.heirloom_amount_table **不落库** —— 唯一真相已在 heirlooms.json 的
     quest_reward.amount_table（纪律 AB）⇒ 本工具**每次运行都断言两者逐值相同**，不同即红 ✓
  ② 英文注解 comment ⇒ _comment（我方注解键约定：下划线前缀 = 给人读的字符串，不进死键报告）✓
  ③ restriction 的 99 哨兵 ⇒ null（原值 + 语义写进 _ruling；QuestsConfig 的 P 校验拒收 99/-1）✓

🔴 **语义出处（不许我推）**：reports/unity_ref/04b_quests_loot_narration.md §5.4 实测 ——
   RaidPartyPanel.IsResolveEligible 读 LevelRestrictions[Quest.Difficulty] 并与 hero.Resolve.Level
   比较 ⇒ 这张表是**上限表**（"该难度允许的**最高** resolve level"），
   99 = **「无等级上限」哨兵**（不是"不可接受/禁用"—— 早期推断已被 §5.4 更正）✓

用法：python tools/dsh/land_ref_quests.py
"""

from __future__ import annotations

import copy
import io
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = os.path.join(REPO, "reports", "unity_ref", "quests_loot_narration_from_ref.json")
HEIRLOOMS = os.path.join(REPO, "darkest", "data", "heirlooms.json")
OUT = os.path.join(REPO, "darkest", "data", "quests.json")

# restriction.difficulty.resolve_level_threshold_table 里原版的"不可接受"哨兵 ✓
NOT_ACCEPTABLE_SENTINEL = 99

_NOTE = (
    "M14 任务表（阶段 A 对齐）：由 tools/dsh/land_ref_quests.py 从【参考项目降级源】转写 —— "
    "E 盘一手 campaign/quest/JsonQuests.json 当前不在原位（只剩 app.log + mods）"
    "⇒ 按 doc/architecture/source_priority.md §2/§3 降级到 reports/unity_ref/quests_loot_narration_from_ref.json "
    "的 quests 段，实际路径见 _source ✓ "
    "与任务卡（E 盘时期读数）的差异：plot_quests 34→30 · goals 48→45 · "
    "number_of_quests_per_town_visit_table [2,6,8,9,10,11,12,13]→[2,5,7,8,9,10,11,12]"
    "（疑似 DLC/版本差）⇒ 已登记 O-116，交策划裁 ✓ "
    "三处有意规范化：① heirloom_amount_table 不重复落库（唯一真相 = heirlooms.json 的 "
    "quest_reward.amount_table，本工具每次运行都断言逐值相同）② 英文注解 comment ⇒ _comment "
    "③ restriction 的 99 哨兵 ⇒ null（原值见 _ruling）✓"
)

_SOURCE = (
    "F:\\GithubPro\\Darkest-Dungeon-Unity\\Assets\\Resources\\Data\\JsonQuests.json"
    "（经 reports/unity_ref/quests_loot_narration_from_ref.json 的 quests 段转写；E 盘一手缺失 ⇒ 降级）"
)

_FIELD_CLASSES = (
    "plot_quests = behavior（真任务 30 条；quest.goal_ids 指向 goals 表）· "
    "goals = behavior+constraint（目标定义；每条恰好 1 个 goal_id 是参考侧硬约束）· "
    "types = constraint+behavior（任务类型枚举 6 个；plot_quests[].quest.type 必须命中）· "
    "restriction.resolve_level_threshold_table = constraint（**必须可断言**：null 或正整数；"
    "**它是上限表** = 该难度允许的最高 resolve level，见 _ruling）· "
    "generation.* = behavior（任务生成 / 奖励 / 传家宝归属表）· stress_damage = behavior ✓"
)

_RULING = (
    "restriction.difficulty.resolve_level_threshold_table 原值 [2,2,3,4,5,99,99]："
    "🔴 **它是上限表**（不是下限）—— 参考侧 RaidPartyPanel.IsResolveEligible 实测："
    "LevelRestrictions[quest.Difficulty] = 该难度允许的**最高** resolve level，"
    "英雄 resolve 高于它 ⇒ 不可参战（出处 reports/unity_ref/04b_quests_loot_narration.md §5.4）✓ "
    "99 = 原版「**无等级上限**」哨兵（⚠️ 早期「不可接受/禁用」推断已被该节更正）⇒ 落库裁成 null；"
    "QuestsConfig 的 P 校验拒收 99/-1（见 QuestsConfig.Validate.cs）✓"
)


def load(path: str):
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def heirloom_table_as_map(rows):
    """参考件形状 = [{type, amounts}] ⇒ 我方形状 = {type: amounts}（唯一真相在 heirlooms.json）✓"""
    return {row["type"]: row["amounts"] for row in rows}


def normalize(quests, heirlooms):
    out = {
        "stress_damage": quests["stress_damage"],
        "goals": quests["goals"],
        "town_progression_goal_ids": quests["town_progression_goal_ids"],
        "types": quests["types"],
        "plot_quests": quests["plot_quests"],
    }

    gen = copy.deepcopy(quests["generation"])
    rewards = gen["rewards"]

    # ① 唯一真相断言：参考件与 heirlooms.json 必须逐值相同 ⇒ 才有资格"不重复落库" ✓
    ref_map = heirloom_table_as_map(rewards.pop("heirloom_amount_table"))
    our_map = heirlooms["quest_reward"]["amount_table"]
    if ref_map != our_map:
        raise SystemExit(
            "🔴 heirloom_amount_table 与 heirlooms.json 的 quest_reward.amount_table **不一致** ⇒ "
            "唯一真相被破，先对账再落库：\n  ref = %s\n  our = %s" % (ref_map, our_map))

    # ② 英文注解 ⇒ _comment（下划线前缀 = 给人读的字符串，不进死键报告）✓
    if "comment" in rewards:
        rewards["_comment"] = rewards.pop("comment")
    for row in rewards["trinket_chance_table"]:
        if "comment" in row:
            row["_comment"] = row.pop("comment")
    out["generation"] = gen

    # ③ 99 哨兵 ⇒ null ✓
    restriction = copy.deepcopy(quests["restriction"])
    table = restriction["difficulty"]["resolve_level_threshold_table"]
    restriction["difficulty"]["resolve_level_threshold_table"] = [
        None if v == NOT_ACCEPTABLE_SENTINEL else v for v in table
    ]
    out["restriction"] = restriction
    return out


def main() -> int:
    ref = load(REF)
    quests = normalize(ref["quests"], load(HEIRLOOMS))

    payload = {
        "_note": _NOTE,
        "_source": _SOURCE,
        "_field_classes": _FIELD_CLASSES,
        "_ruling": _RULING,
    }
    payload.update(quests)

    text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)

    print("[M14] 落库 %s" % os.path.relpath(OUT, REPO))
    print("  · stress_damage=%s · goals=%d · types=%d · plot_quests=%d · town_progression_goal_ids=%d"
          % (quests["stress_damage"], len(quests["goals"]), len(quests["types"]),
             len(quests["plot_quests"]), len(quests["town_progression_goal_ids"])))
    print("  · 阈值表=%s（99 ⇒ null）" % quests["restriction"]["difficulty"]["resolve_level_threshold_table"])
    print("  · 行数=%d · 字节=%d" % (text.count("\n"), len(text.encode("utf-8"))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
