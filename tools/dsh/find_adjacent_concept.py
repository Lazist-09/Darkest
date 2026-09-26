#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_adjacent_concept.py —— 查参考有没有 **「相邻友方」** 这个概念 ✓

🔴 **为什么做**（承接 `120_*.md §5`）：
   我方 `tank_guard_wall` 的 `scope` = **`adjacent_ally_and_self`**（**相对位置**）⇒
   📌 而参考的 `.target` **只能写绝对 rank** ⇒ ⚠️ **疑似无法表达** ✓
   ⇒ ✅ 本件去参考里找 **"相邻"这个概念在哪实现** ✓
   🎖️ **判据（第 153 条）**：**"我说'参考表达不了'——
      去找【它有没有别的地方实现】，而不是只看 `target` 字段"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_adjacent_concept.py
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

CODE = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"


def main() -> int:
    print("=" * 76)
    print("① 参考【代码】里找「相邻」相关")
    print("=" * 76)
    pats = ["Adjacent", "adjacent", "NextTo", "Neighbour", "Neighbor",
            "IsAdjacent", "GuardEffect", "Guard", "protect"]
    for pat in pats:
        hits = []
        for dp, _d, ns in os.walk(CODE):
            for f in ns:
                if not f.endswith(".cs"):
                    continue
                p = os.path.join(dp, f)
                t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                for i, l in enumerate(t.splitlines(), 1):
                    if pat in l:
                        hits.append((os.path.relpath(p, CODE), i, l.strip()[:100]))
        print(f"  `{pat}` ⇒ **{len(hits)}** 处")
        for h in hits[:4]:
            print(f"      {h[0]}:{h[1]}  {h[2]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
