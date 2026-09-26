#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_unwired_landing.py —— 逐个核那 17 条"未接线"的**真实落点** ✓

🔴 **为什么做**（承接 `142_*.md §6` 的判据 228）：
   我把 17 条判为"未接线（落点在战斗结算，只差连上）"⇒
   📌 **而那是从 `Target` 枚举【推】的，不是逐个查落点** ⚠️
   ⇒ ✅ 本件查：`DestinationNote` 里对每条写的落点，看**是不是真在战斗层** ✓
   🎖️ **判据（第 228 条）**：**"我按【枚举值】分类 ——
      那个枚举是【权威】吗？（它有可能是粗的）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_unwired_landing.py
"""

from __future__ import annotations

import io
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, "darkest", "scripts", "data", "BuffPrimitiveTranslation.cs")


def main() -> int:
    t = io.open(SRC, encoding="utf-8", errors="replace").read()

    # 抽 DestinationNote 的 case → 文本
    m = re.search(r"DestinationNote\(string statType\) => statType switch\s*\{(.*?)\n    \};",
                  t, re.S)
    notes = {}
    if m:
        for mm in re.finditer(r'"([a-z_0-9]+)"\s*=>\s*"([^"]*)"', m.group(1)):
            notes[mm.group(1)] = mm.group(2)
    print(f"[uw] `DestinationNote` 抽到 **{len(notes)}** 条 ✓")
    print()

    # 那 17 条"未接线"
    UNWIRED = ["combat_stat_add", "combat_stat_multiply", "resistance",
               "stress_dmg_received_percent", "debuff_chance", "resolve_check_percent",
               "hp_heal_received_percent", "stress_heal_received_percent",
               "resolve_xp_bonus_percent", "stun_chance", "poison_chance", "move_chance",
               "bleed_chance", "hp_heal_amount", "damage_received_percent",
               "stress_heal_percent", "stress_dmg_percent"]
    print(f"[uw] 🔴 **逐个看那 {len(UNWIRED)} 条的落点说明**：")
    bad = []
    for n in UNWIRED:
        note = notes.get(n, "**(无 note)**")
        # 判它说的落点是不是"战斗层"
        combat = any(k in note for k in ("战斗", "结算", "SkillExecutor", "DamagePipeline",
                                         "MoraleLedger", "HealAmount", "BuffLedger",
                                         "combat", "units.json"))
        tag = "✅" if combat else "🔴"
        if not combat:
            bad.append((n, note))
        print(f"[uw]   {tag} `{n}`")
        print(f"[uw]        {note[:120]}")
    print()
    print(f"[uw] 🔴 **落点说明里【看不出是战斗层】的：{len(bad)}** ✓")
    for n, note in bad:
        print(f"[uw]     `{n}` ⇒ {note[:100]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
