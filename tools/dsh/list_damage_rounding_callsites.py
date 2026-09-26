#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""list_damage_rounding_callsites.py —— 列 **我方 `ApplyDamageRounding` 的全部调用点** ✓

🔴 **为什么做**（承接 `106_*.md §6`）：
   `106_*.md` 定出 ① `BattleMath.ApplyDamageRounding` 要改（`Round`→`Ceil` · 下限 1→0）
   ⇒ 📌 **但改动前必须知道【谁在用它】** ⇒ ✅ 本件列出 ✓
   🎖️ **判据（第 105 条）**：**"改一个共用函数之前 ——
      先列【全部调用点】与【各自的语义】"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/list_damage_rounding_callsites.py
"""

from __future__ import annotations

import io
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOTS = (os.path.join(REPO, "darkest", "scripts"),
         os.path.join(REPO, "darkest", "tests"))
NAMES = ("ApplyDamageRounding", "ApplyHealRounding", "ApplyRounding")


def main() -> int:
    found = Counter()
    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, _d, ns in os.walk(root):
            for f in ns:
                if not f.endswith(".cs"):
                    continue
                p = os.path.join(dirpath, f)
                rel = os.path.relpath(p, REPO)
                t = io.open(p, encoding="utf-8", errors="replace").read()
                for i, l in enumerate(t.splitlines(), 1):
                    for nm in NAMES:
                        if nm in l:
                            kind = "定义" if re.search(r"(public|private|internal|static).*\b"
                                                       + nm + r"\s*\(", l) else "调用"
                            found[(nm, kind)] += 1
                            print(f"  [{kind}] **{rel}**:{i}")
                            print(f"           {l.strip()[:128]}")
    print()
    print("[dc] 🎖️ 汇总：")
    for (nm, kind), n in sorted(found.items()):
        print(f"[dc]   `{nm}`（{kind}）×**{n}**")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
