#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_remaining_layers.py —— 核 **其余 10 条**原语的落点层（判"普遍误判"）✓

🔴 **为什么做**（承接 `147_*.md §6②` 的判据 247）：
   本件已确认**枚举混了轴/层**（有定义原文为证）⇒
   📌 **但"普遍误判"要有【全部取值的落点证据】** ⚠️
   ⇒ 已核 `MoraleMod`(6) ⇒ ✅ **还剩 `ProbMod`(5) / `HealMod`(3) / `DamageMod`(1) /
      `UnitResistance`(1) = 10 条** ✓
   🎖️ **判据（第 247 条）**：**"我核了 1 个取值就下结论 ——
      依据够吗？（够说'设计'，不够说'普遍误判'）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_remaining_layers.py
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
SC = os.path.join(REPO, "darkest", "scripts")

# 其余 10 条（含 Classify 特判的 2 条）
REST = {
    "DamMod": ["damage_received_percent"],
    "HealMod": ["hp_heal_received_percent", "hp_heal_amount"],   # hp_heal_percent 已激活
    "ProbMod": ["stun_chance", "poison_chance", "bleed_chance", "move_chance", "debuff_chance"],
    "UnitResistance": ["resistance"],
    "StatMod(特判)": ["combat_stat_add", "combat_stat_multiply"],
}


def main() -> int:
    files = {}
    for dp, _d, ns in os.walk(SC):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                files[os.path.relpath(p, REPO)] = io.open(
                    p, encoding="utf-8", errors="replace").read()

    print(f"[rl] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()
    print("[rl] 🔴 **其余 10 条的消费侧出现次数**（排除表自身）：")
    for grp, names in REST.items():
        print(f"[rl]   --- **{grp}** ---")
        for n in names:
            hits = []
            for rel, t in sorted(files.items()):
                if "BuffPrimitiveTranslation" in rel:
                    continue
                for i, l in enumerate(t.splitlines(), 1):
                    if re.search(r"\b" + re.escape(n) + r"\b", l):
                        hits.append((rel, i, l.strip()[:85]))
            tag = "✅" if hits else "🔴"
            print(f"[rl]     {tag} `{n}` ⇒ **{len(hits)}** 处")
            for h in hits[:2]:
                print(f"[rl]         {h[0]}:{h[1]}  {h[2]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
