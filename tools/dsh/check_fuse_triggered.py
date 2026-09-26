#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_fuse_triggered.py —— 查**数据里有没有"同一条 Effect 里多条 stress"** ✓
   （那决定融合系统是否被实际触发）

🔴 **为什么做**（承接 `101_*.md §6①`）：
   融合只在 **`StressEffect`/`StressHealEffect`** 上实现 ⇒
   📌 **它被触发的条件**：**同一次施加里，同一目标【收到多条 stress】**
   ⇒ ✅ 本件去 **`Effects.txt` 的 952 条**里查 ✓
   🎖️ **判据（第 92 条）**：**"这个能力的触发条件在数据里出现过吗？
      ⇒ 没出现 ⇒ 它是【预留机制】（可不实现）；出现 ⇒ 必须实现"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_fuse_triggered.py
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
SRC = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt"


def main() -> int:
    lines = io.open(SRC, encoding="utf-8-sig", errors="replace").read().splitlines()
    eff = [l.strip() for l in lines
           if l.strip().startswith("effect:")]

    multi_stress, multi_healstress = [], []
    stress_counts = Counter()
    for l in eff:
        name = re.search(r'\.name\s+"([^"]*)"', l)
        nm = name.group(1) if name else "?"
        n_s = len(re.findall(r"\.stress\s+", l))
        n_h = len(re.findall(r"\.healstress\s+", l))
        stress_counts[(n_s, n_h)] += 1
        if n_s > 1:
            multi_stress.append((nm, n_s))
        if n_h > 1:
            multi_healstress.append((nm, n_h))

    print(f"[fz] `Effects.txt` 的 effect：**{len(eff)}** 条 ✓")
    print()
    print("[fz] 🎖️ `(stress 个数, healstress 个数)` 的分布：")
    for k, v in sorted(stress_counts.items(), key=lambda x: -x[1])[:12]:
        print(f"[fz]   {k} ⇒ **{v}** 条")
    print()
    print(f"[fz] 🔴 **`stress` 出现 >1 次的 effect：{len(multi_stress)}**")
    for nm, n in multi_stress[:12]:
        print(f"[fz]     `{nm}` ⇒ {n} 次")
    print()
    print(f"[fz] 🔴 **`healstress` 出现 >1 次的 effect：{len(multi_healstress)}**")
    for nm, n in multi_healstress[:12]:
        print(f"[fz]     `{nm}` ⇒ {n} 次")
    print()
    tot = len(multi_stress) + len(multi_healstress)
    if tot == 0:
        print("[fz] ⇒ ✅ **数据里【没有】多条 ⇒ 融合【不会被触发】**")
        print("[fz]   ⇒ 📌 **即：它是【预留机制】⇒ 采用时【不用实现】** ✓")
    else:
        print(f"[fz] ⇒ 🔴 **有 {tot} 条** ⇒ 融合【会被触发】⇒ **必须实现** ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
