#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_morale_events_table.py —— 数 **`morale_events.json` 的完整条数与字段全集** ✓

🔴 **为什么做**（承接 `129_*.md §6`）：
   我只读了其中 **3 条**（`mental_*`）⇒ ⚠️ **拿样本当全量** ✓
   ⇒ ✅ 本件**全数** + **字段全集** + **取值分布** ✓
   🎖️ **判据（第 183 条）**：**"我读了一个表的 3 条 ——
      表里一共几条？（别拿样本当全量）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_morale_events_table.py
"""

from __future__ import annotations

import json
import os
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
P = os.path.join(REPO, "darkest", "data", "morale_events.json")


def main() -> int:
    d = json.load(open(P, encoding="utf-8"))
    print(f"[me] 顶层键：{list(d)}")
    ev = d["events"]
    print(f"[me] 🎖️ **共 {len(ev)} 条** ✓")
    print()

    keys = Counter()
    scopes = Counter()
    occ = Counter()
    for e in ev:
        keys.update(e.keys())
        scopes[e.get("scope")] += 1
        occ[e.get("occurrence")] += 1
    print(f"[me] 🎖️ **字段全集（{len(keys)} 种）**：")
    for k, v in keys.most_common():
        print(f"[me]   `{k}` ×{v}")
    print()
    print(f"[me] 🎖️ **`scope` 取值**：{dict(scopes.most_common())}")
    print(f"[me] 🎖️ **`occurrence` 取值**：{dict(occ.most_common())}")
    print()
    print("[me] 🔴 **逐条（id / delta / scope / occurrence）**：")
    for e in ev:
        print(f"[me]   `{e.get('id'):28s}` Δ={e.get('delta'):>5} "
              f"scope={str(e.get('scope')):12s} occ={e.get('occurrence')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
