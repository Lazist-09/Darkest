#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_morale_vs_axis_coupling.py —— 核 **`morale_effects` 与 `damage_axis` 有无耦合** ✓

🔴 **为什么做**（承接 `127_*.md §5`）：
   可疑点：`ranged_intimidating_shot` 标 `damage_axis: mental` ⇒
   📌 **若"削士气"要求 `mental`** ⇒ 那条就**自洽**；**若无耦合** ⇒ ⚠️ **它是独立选择（更可疑）** ✓
   ⇒ ✅ 本件读我方代码，看两者是否在【同一处】被消费 ✓

🎖️ **判据（第 176 条）**：**"两个字段有没有【耦合】？
   ⇒ 看它们是否在【同一段代码】里一起出现"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_morale_vs_axis_coupling.py
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


def main() -> int:
    files = {}
    for dp, _d, ns in os.walk(SC):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                files[os.path.relpath(p, REPO)] = io.open(
                    p, encoding="utf-8", errors="replace").read()

    for pat in ("morale_effects", "MoraleEffects", "MoraleEffect",
                "damage_axis", "DamageAxis"):
        print(f"=== 🔴 `{pat}` ===")
        n = 0
        for rel, t in sorted(files.items()):
            for i, l in enumerate(t.splitlines(), 1):
                if pat in l:
                    n += 1
                    print(f"  {rel}:{i}")
                    print(f"      {l.strip()[:125]}")
        if n == 0:
            print("  🔴 **0 处**")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
