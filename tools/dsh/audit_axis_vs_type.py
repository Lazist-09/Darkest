#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_axis_vs_type.py —— 核我方 **`damage_axis`/`range_axis`** vs 参考 **`.type`** ✓

🔴 **为什么做**（承接 `125_*.md §6③`）：
   我方技能有 **`range_axis`**（`melee`/`ranged`/`none`）与 **`damage_axis`**
   （`physical`/`mental`/`none`）⇒ 📌 而**参考只有 `.type`**（`melee`/`ranged`）
   ⇒ ✅ 本件逐条对照，看**我方多出来的自由度**是什么 ✓
   🎖️ **判据（第 171 条）**：**"我方有两个轴 —— 参考有几个？
      多出来的那个是【自加】还是【参考用别的字段表达】"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_axis_vs_type.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF_ID = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info"


def main() -> int:
    sk = json.load(open(os.path.join(REPO, "darkest", "data", "skills.json"),
                        encoding="utf-8"))
    ra, da = Counter(), Counter()
    for s in sk["skills"]:
        ra[s.get("range_axis")] += 1
        da[s.get("damage_axis")] += 1
    print("[ax] 🎖️ **我方 `range_axis`**：", dict(ra.most_common()))
    print("[ax] 🎖️ **我方 `damage_axis`**：", dict(da.most_common()))
    print()

    # 参考的 .type
    tp = Counter()
    for f in sorted(os.listdir(REF_ID)):
        if not f.endswith(".bytes"):
            continue
        t = io.open(os.path.join(REF_ID, f), encoding="utf-8-sig",
                    errors="replace").read()
        for l in t.splitlines():
            if "combat_skill:" in l and ".level 0" in l:
                m = re.search(r"\.type\s+\"?([a-z_]+)\"?", l)
                if m:
                    tp[m.group(1)] += 1
    print("[ax] 🎖️ **参考 `.type`（105 条）**：", dict(tp.most_common()))
    print()

    print("[ax] 🔴 **两边的轴对照**：")
    print("  我方 `range_axis`（3 种） ↔ 参考 `.type`（%d 种）" % len(tp))
    print("  我方 `damage_axis`（%d 种） ↔ 参考【无对应字段？】" % len(da))
    print()
    # 参考有没有物理/精神之分
    print("[ax] 🔴 **参考里找 `physical`/`mental` 之分**：")
    for pat in ("physical", "mental", "magic"):
        n = 0
        for dp, _d, ns in os.walk(r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"):
            for f in ns:
                if f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                n += len(re.findall(pat, t, re.I))
        print(f"    `{pat}` ⇒ **{n}** 处")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
