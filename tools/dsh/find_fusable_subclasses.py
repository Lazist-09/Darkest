#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_fusable_subclasses.py —— 查**哪些 SubEffect 子类覆盖了 `Fusable`/`Fuse`** ✓

🔴 **为什么做**（承接 `100_*.md §6①`）：
   `SubEffect.Fusable` 默认 **`false`** · `Fuse()` 默认返回 **`0`** ⇒
   📌 **融合系统的实际用点【未查】** ✓
   ⇒ ✅ 本件找出**覆盖者**，并量出**规模** ✓

🎖️ **判据（第 87 条）**：**"这个基类能力有 N 处覆盖 ——
   是【广泛使用】还是【个别特例】？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_fusable_subclasses.py
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

R = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts\Mechanics\Skills\Effects"


def main() -> int:
    files = sorted(f for f in os.listdir(R) if f.endswith(".cs"))
    print(f"[fu] `Effects/` 目录：**{len(files)}** 个文件 ✓")
    print()

    fusable, fuse, applyfused, subclass = [], [], [], []
    for f in files:
        t = io.open(os.path.join(R, f), encoding="utf-8-sig", errors="replace").read()
        if re.search(r"class\s+\w+\s*:\s*SubEffect", t):
            subclass.append(f)
        if re.search(r"override\s+bool\s+Fusable", t):
            fusable.append(f)
        if re.search(r"override\s+int\s+Fuse\s*\(", t):
            fuse.append(f)
        if re.search(r"override\s+bool\s+ApplyFused", t):
            applyfused.append(f)

    print(f"[fu] `SubEffect` 的子类：**{len(subclass)}** 个 ✓")
    print(f"[fu] 🎖️ **覆盖 `Fusable`**：**{len(fusable)}** 个 {fusable}")
    print(f"[fu] 🎖️ **覆盖 `Fuse(...)`**：**{len(fuse)}** 个 {fuse}")
    print(f"[fu] 🎖️ **覆盖 `ApplyFused(...)`**：**{len(applyfused)}** 个 {applyfused}")
    print()
    if not fusable and not fuse and not applyfused:
        print("[fu] 🔴 **一个都没有** ⇒ 融合系统【完全未使用】")
        print("[fu]   ⇒ 📌 **即：它是「预留的机制」，数据与代码都没启用** ✓")
        print("[fu]   ⇒ ✅ **采用时【不用实现】**（但要记下它存在）✓")
    else:
        allf = sorted(set(fusable) | set(fuse) | set(applyfused))
        print(f"[fu] ⇒ 涉及 **{len(allf)}** 个子类 ⇒ "
              f"{'个别特例' if len(allf) < 5 else '较广泛'} ✓")
        for f in allf:
            tags = []
            if f in fusable:
                tags.append("Fusable")
            if f in fuse:
                tags.append("Fuse")
            if f in applyfused:
                tags.append("ApplyFused")
            print(f"[fu]     **{f}** ⇒ {tags}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
