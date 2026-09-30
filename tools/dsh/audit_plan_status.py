#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_plan_status.py —— 审 **`PLAN_adoption` 里每一行的【状态声明】** 是否与数据一致 ✓

🔴 **为什么做**（承接 `111_*.md`）：
   A2 行宣称"只落 14/44 · 卡在策划"，而**实测 44/44** ⇒ 🔴 **陈述过期** ⚠️
   ⇒ 📌 **那一行之外，还有多少行是过期的？** ⇒ ✅ 本件把全部状态列出来核 ✓

🎖️ **判据（第 123 条）**：**"一份进度表里有一行过期 ——
   那就该【全表核一遍】，而不是只修那一行"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_plan_status.py
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

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PLAN = os.path.join(REPO, "reports", "unity_ref", "PLAN_adoption.md")


def main() -> int:
    d = io.open(PLAN, encoding="utf-8", errors="replace").read()
    lines = d.splitlines()
    print(f"[ps] `PLAN_adoption.md`：**{len(lines)}** 行 ✓")
    print()
    # 🔴 找每一项表格行（形如 `| **A?** | ... |`）
    rows = []
    for i, l in enumerate(lines, 1):
        m = re.match(r"\|\s*\*\*([A-Za-z0-9.+ ]+?)\*\*\s*\|(.*)", l)
        if m:
            rows.append((i, m.group(1), m.group(2)))
    print(f"[ps] 🎖️ **表格项：{len(rows)}** ✓")
    print()
    for i, name, rest in rows:
        # 取【状态列】（第二列）
        cols = [c.strip() for c in rest.split("|")]
        status = cols[0] if cols else ""
        # 状态里有没有"完成/待裁/卡住"等词
        tag = "·"
        if "✅" in status:
            tag = "✅ 完成"
        elif "🔄" in status:
            tag = "🔄 进行中"
        elif "⏸" in status or "待裁" in status or "待" in status:
            tag = "⏸ 待处置"
        print(f"[ps]   L{i:<4} **{name}** ⇒ {tag}")
        print(f"[ps]        {status[:190]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
