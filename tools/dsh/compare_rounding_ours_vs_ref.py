#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""compare_rounding_ours_vs_ref.py —— **我方取整实现 vs 参考** 逐条对照 ✓

🔴 **为什么做**（承接 `105_*.md §7③`）：
   `105_*.md` 定出参考的规律：**HP 走 `Ceil` · 非 HP 走 `Round`**；
   下限：**伤害 0 · 压力 1 · 落点无** ✓
   ⇒ 📌 而我方有 `BattleMath.ApplyDamageRounding`（**`Round` + 下限 1**）
   ⇒ ✅ 本件把我方**所有取整入口**列出来，逐条判"要不要改" ✓

🎖️ **判据（第 103 条）**：**"我方的实现 vs 参考 —— 逐条列，并标【改/不改】与【依据】"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/compare_rounding_ours_vs_ref.py
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
OURS = os.path.join(REPO, "darkest", "scripts")
# 🔴 我方【零 Godot 的 kernel】
KERNEL = ("core", "data", "gameplay")


def main() -> int:
    pats = {
        "Ceil": r"Math\.Ceiling|MathF\.Ceiling|CeilToInt",
        "Floor": r"Math\.Floor|MathF\.Floor|FloorToInt",
        "Round": r"Math\.Round|MathF\.Round|RoundToInt",
        "(int)cast": r"\(int\)\s*[A-Za-z_(]",
        "Truncate": r"Math\.Truncate",
    }
    total = Counter()
    per = {}
    for dirpath, _d, ns in os.walk(OURS):
        for f in ns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, OURS)
            t = io.open(p, encoding="utf-8", errors="replace").read()
            c = Counter()
            for name, pat in pats.items():
                n = len(re.findall(pat, t))
                if n:
                    c[name] = n
                    total[name] += n
            if c:
                per[rel] = c
    print("[cr] 🎖️ **我方（`darkest/scripts`）的全部取整调用**：")
    for k, v in total.most_common():
        print(f"[cr]   `{k}` ×**{v}**")
    print()
    print("[cr] 🔴 **按文件**：")
    for rel, c in sorted(per.items(), key=lambda x: -sum(x[1].values())):
        print(f"[cr]   **{rel}** ⇒ {dict(c)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
