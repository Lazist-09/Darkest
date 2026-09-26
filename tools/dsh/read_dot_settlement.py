#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""read_dot_settlement.py —— 读 **DoT 的结算逻辑**（`PoisonEffect`/`BleedEffect`）✓

🔴 **为什么做**（承接 `93_*.md §6①`）：
   `.dotPoison N` ⇒ `new PoisonEffect(N)` ⇒ 📌 **那个 N 怎么用？** ✓
   ⇒ ✅ 本件读 `PoisonEffect` / `BleedEffect` 与**它们的基类** ✓
   🎖️ **这是"按参考逻辑对齐"的另一半**（数据那半已通）✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/read_dot_settlement.py
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

R = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"
ED = os.path.join(R, "Mechanics", "Skills", "Effects")


def dump(path, label, maxlines=200):
    lines = io.open(path, encoding="utf-8-sig", errors="replace").read().splitlines()
    print(f"=== {label}（{os.path.relpath(path, R)} · {len(lines)} 行）===")
    for i, l in enumerate(lines[:maxlines], 1):
        if l.strip() and not l.strip().startswith("using"):
            print(f"  {i:4d}|{l.rstrip()[:140]}")
    print()


def main() -> int:
    for f in ("PoisonEffect.cs", "BleedEffect.cs"):
        p = os.path.join(ED, f)
        if os.path.isfile(p):
            dump(p, f)
    # 找基类
    print("=== 🔴 它们的基类是什么？ ===")
    for f in ("PoisonEffect.cs", "BleedEffect.cs"):
        p = os.path.join(ED, f)
        t = io.open(p, encoding="utf-8-sig", errors="replace").read()
        m = re.search(r"class\s+(\w+)\s*:\s*(\w+)", t)
        if m:
            print(f"  `{m.group(1)}` : **{m.group(2)}**")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
