#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_quest_heirloom_rewards.py -- step 1 of the planner's tier_drop ruling.

PLANNER RULING (DELIVERY-DESIGNER-HEIRLOOM-RULING-20260922 item 2), split per discipline AY:
  step 1 = ADD the quest-reward channel      <-- THIS TOOL (structure only, zero behaviour)
  step 2 = DELETE `tier_drop` + its 3 P23 checks  <-- a separate round (that one changes behaviour)

SOURCE: PRIMARY (E-drive) campaign/quest/quest.generation.json -> generation.rewards
  heirloom_type_map   : crypts/warrens/weald/cove each give ["bust","portrait","deed","crest"]
  heirloom_amount_table: 6 difficulty tiers x 4 quest lengths; ONLY tiers 1/3/5 carry values
                        (index 0 is the placeholder for "no such tier", index 0 inside each row = length 0)
Marks every entry origin="dd1" + placeholder=true (per #422 wording).

SAFETY: it edits darkest/data/heirlooms.json TEXTUALLY (inserts one block); it never rewrites the file,
  so the existing formatting survives (lesson: json.dump would reformat and break string-surgery tests).

USAGE
  python tools/dsh/extract_dd1_quest_heirloom_rewards.py [--edrive <path>] [--apply]
EXIT: 0 ok, 1 primary/data not readable.
"""

from __future__ import annotations

import io
import json
import sys as _sys

try:
    _sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # Windows 控制台默认 GBK ⇒ 防止 ✓/中文把工具打崩 ✓
except Exception:
    pass
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
HEIRLOOMS = os.path.join(REPO, "darkest", "data", "heirlooms.json")
MARK = '"quest_reward"'


def main() -> int:
    argv = sys.argv[1:]
    edrive = argv[argv.index("--edrive") + 1] if "--edrive" in argv else EDRIVE
    apply = "--apply" in argv

    src = os.path.join(edrive, "campaign", "quest", "quest.generation.json")
    if not os.path.isfile(src):
        print("[quest-reward] primary not readable at %s" % src)
        return 1
    rewards = json.load(io.open(src, encoding="utf-8"))["generation"]["rewards"]

    type_map = {}
    for row in rewards["heirloom_type_map"]:
        type_map[row["dungeon"]] = list(row["types"])
    amounts = {}
    for row in rewards["heirloom_amount_table"]:
        amounts[row["type"]] = row["amounts"]

    print("[quest-reward] dungeons  = %s" % ", ".join(sorted(type_map)))
    print("[quest-reward] per-dungeon types all-equal = %s"
          % (len({tuple(v) for v in type_map.values()}) == 1))
    for t in sorted(amounts):
        nonempty = [i for i, a in enumerate(amounts[t]) if a]
        print("[quest-reward] %-9s tiers with values = %s ; rows = %s"
              % (t, nonempty, [a for a in amounts[t] if a]))

    block = {
        "quest_reward": {
            "_note": ("步骤 ①（策划 «传家宝改任务奖励» 的第一步）：**只加通道、零行为** ✓ "
                      "由 tools/dsh/extract_dd1_quest_heirloom_rewards.py 从 E 盘【一手】"
                      "campaign/quest/quest.generation.json 转写；每条约 origin=dd1 ✓"),
            "_source": "E:\\SteamLibrary\\steamapps\\common\\DarkestDungeon\\campaign\\quest",
            "_origin": "dd1",
            "_placeholder": True,
            "dungeon_types": type_map,
            "amount_table": amounts,
        }
    }
    inner = json.dumps(block["quest_reward"], ensure_ascii=False, indent=4)
    text = '  "quest_reward": ' + inner.replace("\n", "\n  ")

    current = io.open(HEIRLOOMS, encoding="utf-8").read()
    if MARK in current and apply:
        print("[quest-reward] already present -> nothing inserted (idempotent) ✓")
        return 0
    if not apply:
        print("[quest-reward] dry run -- pass --apply to insert the block (no file written)")
        return 0

    anchor = '  "drop_note":'
    i = current.index(anchor)
    j = current.index("\n", i) + 1
    inserted = current[:j] + text + ",\n" + current[j:]
    io.open(HEIRLOOMS, "w", encoding="utf-8").write(inserted)
    print("[quest-reward] INSERTED the quest_reward block into %s (tier_drop untouched ✓)"
          % os.path.relpath(HEIRLOOMS, REPO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
