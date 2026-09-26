#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_s43_mapping.py —— 核 **`§43` 技能映射的置信度画像** ✓

🔴 **为什么做**（承接 `115_*.md §7④`）：
   `§43` 把 44 条技能映射到参考 ⇒ 📌 但**从未统计过**置信度分布 ✓
   ⇒ 📌 已知：`medic` 4/9 语义对上 · `commissar` 4/9 ⇒ ⚠️ **其余 3 个原型呢？**
   ⇒ ✅ 本件把 `dd1_baseline §43` 的映射**逐条抽出来统计** ✓

🎖️ **判据（第 132 条）**：**"一份映射表的【置信度分布】是多少？
   ⇒ 只看几条会高估对齐程度"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_s43_mapping.py
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
BASE = os.path.join(REPO, "doc", "modules", "dd1_baseline.md")


def main() -> int:
    lines = io.open(BASE, encoding="utf-8", errors="replace").read().splitlines()
    # 找 §43 范围
    start = next((i for i, l in enumerate(lines)
                  if l.startswith("## ") and "§43" in l), None)
    end = next((i for i, l in enumerate(lines) if start is not None and i > start
                and l.startswith("## ")), len(lines))
    print(f"[s43] §43 范围：L{start+1} ~ L{end}（**{end-start}** 行）✓")
    print()

    rows = []
    for i in range(start, end):
        l = lines[i]
        # 表格行：| 序号 | 我方 | 原版 | dmg% | 依据 | 判 |
        if not l.strip().startswith("|"):
            continue
        cells = [c.strip() for c in l.strip().strip("|").split("|")]
        if len(cells) < 5:
            continue
        if cells[0] in ("#", "---") or set(cells[0]) <= set("-: "):
            continue
        rows.append((i + 1, cells))
    print(f"[s43] 🎖️ **表格行：{len(rows)}** ✓")
    print()

    verdicts = Counter()
    conf = Counter()
    no_corresp = []
    for ln, c in rows:
        last = c[-1]
        verdicts[last] += 1
        if "无" in c[2] or "❌" in c[2]:
            no_corresp.append((ln, c[1], c[2]))
    print("[s43] 🔴 **末列（判定）的分布**：")
    for k, v in verdicts.most_common():
        print(f"[s43]   {k} ⇒ **{v}**")
    print()
    print(f"[s43] 🔴 **原版为「无」的：{len(no_corresp)}** 条")
    for ln, ours, ref in no_corresp[:20]:
        print(f"[s43]   L{ln}  `{ours}` ⇒ 原版 {ref}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
