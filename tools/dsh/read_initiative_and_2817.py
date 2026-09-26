#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""read_initiative_and_2817.py —— 收两处待读：
   ① **`TotalInitiatives` 的写入点**（`idleUnit` 的判定依据）
   ② **`2817`/`2835` 的场合**（补齐 DoT 4 个结算点）

🔴 **为什么做**（承接 `95_*.md §7`）：
   · `TotalInitiatives == 0` ⇒ **空闲怪**（`×1.5` 那条）⇒ 📌 但要实现就**必须知道它怎么被写** ✓
   · `2817`/`2835` 是 4 个结算点里**唯一没读场合**的 ✓

🎖️ **判据（第 73 条）**：**"这个筛选字段在哪里被写？
   ⇒ 读【写入点】而不是只看【读取点】"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/read_initiative_and_2817.py
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


def main() -> int:
    print("=" * 76)
    print("① `TotalInitiatives` 的全部出现点（含写入）")
    print("=" * 76)
    for dirpath, _d, ns in os.walk(R):
        for f in ns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, R)
            t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            for i, l in enumerate(t.splitlines(), 1):
                if "TotalInitiatives" in l:
                    print(f"  {rel}:{i}")
                    print(f"      {l.strip()[:135]}")
    print()

    print("=" * 76)
    print("② `2817`/`2835` 的场合（±25 行）")
    print("=" * 76)
    SP = os.path.join(R, "Managers", "RaidSceneManager.cs")
    lines = io.open(SP, encoding="utf-8-sig", errors="replace").read().splitlines()
    for lo, hi in ((2790, 2845),):
        for i in range(lo - 1, min(hi, len(lines))):
            if lines[i].strip():
                print(f"  {i+1:5d}|{lines[i].rstrip()[:135]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
