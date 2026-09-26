#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_tier_dimension.py —— 核 **我方有没有"每职业 5 阶"这个维度** ✓

🔴 **为什么做**（承接 `166_*.md` 的判据 342）：
   参考的 `.def` 挂在 **【每职业 × 5 阶】**（每阶 +5pp）⇒
   📌 **而我方是不是只有"单值 `prot`"？** ⇒ ✅ 本件核实 ✓
   🎖️ **判据（第 342 条）**：**"参考的数值挂在【几阶】上 ——
      而我方的同一个量【有没有那个维度】？（没有 ⇒ 先补维度再抄值）"** ✓

🔴 **做法**：查 `units.json` 的英雄条目 ⇒ 有没有 `tiers`/`armour` 数组 ⇒
   以及 `UnitTiers` 的载体 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_tier_dimension.py
"""

from __future__ import annotations

import io
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
D = os.path.join(REPO, "darkest", "data")


def main() -> int:
    d = json.load(open(os.path.join(D, "units.json"), encoding="utf-8"))
    units = d["units"]
    print(f"[td] `units.json` ⇒ **{len(units)}** 单位 ✓")
    print()
    for u in units:
        keys = list(u)
        has_tiers = any(k for k in keys if "tier" in k.lower() or "armour" in k.lower()
                        or "armor" in k.lower())
        tag = "🎖️ 有阶" if has_tiers else "🔴 **无阶维度**"
        print(f"[td]   {tag}  `{u['id']}`")
        print(f"[td]       键：{keys}")
        if has_tiers:
            for k in keys:
                if "tier" in k.lower() or "armour" in k.lower():
                    print(f"[td]       `{k}` ⇒ {json.dumps(u[k], ensure_ascii=False)[:200]}")
    print()
    print("[td] 🎖️ **顶层有没有 tiers 段**：")
    print(f"[td]   顶层键：{list(d)}")
    for k in d:
        if k != "units" and not k.startswith("_"):
            print(f"[td]     `{k}` ⇒ {json.dumps(d[k], ensure_ascii=False)[:200]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
