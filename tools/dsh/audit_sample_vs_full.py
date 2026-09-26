#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_sample_vs_full.py —— 审 **我方报告里"报数"的抽样/全量标注** ✓

🔴 **为什么做**（承接 `157_*.md` 的判据 294）：
   我在 `156_*.md` 报"3 处"、`157_*.md` 全量扫出 **16 处** ⚠️
   ⇒ 📌 **那是一次抽样被当成全量** ⇒ ✅ **该问：这种事【犯过几次】？** ✓
   🎖️ **判据（第 294 条）**：**"我上一轮报的一个数 ——
      这一轮有机会【全量核】时，核了吗？"** ✓

🔴 **做法**：扫 `reports/unity_ref/*.md` 里带 ★标记的"读数" ⇒
   找形如"**N 处**"/"**N 个**"的断言 ⇒ 看同句有没有"全仓/全量/抽/first N"字样 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_sample_vs_full.py
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
R = os.path.join(REPO, "reports", "unity_ref")


def main() -> int:
    files = sorted(f for f in os.listdir(R) if f.endswith(".md") and f[0].isdigit())
    print(f"[sv] 扫 **{len(files)}** 份编号报告 ✓")
    print()

    quant = re.compile(r"\*\*(\d+)\s*(处|个|条|项|次|行|份)\*\*")
    scope_ok = re.compile(r"全仓|全量|全扫|扫了|全部|逐条|逐个|全表|一共|共\s*\d+\s*个文件")
    sample = re.compile(r"抽样|样本|前\s*\d+\s*处|前\s*\d+\s*条|部分|仅看|只看了|只见")

    n_all = n_scoped = n_unscoped = 0
    unscoped_files = Counter()
    for f in files:
        t = io.open(os.path.join(R, f), encoding="utf-8", errors="replace").read()
        for m in quant.finditer(t):
            n_all += 1
            # 取该行
            ls = t.rfind("\n", 0, m.start()) + 1
            le = t.find("\n", m.end())
            line = t[ls:le if le > 0 else len(t)]
            if scope_ok.search(line):
                n_scoped += 1
            else:
                n_unscoped += 1
                unscoped_files[f] += 1

    print(f"[sv] 🎖️ **带范围标注的数**（全仓/全量/逐个…）⇒ **{n_scoped}** ✓")
    print(f"[sv] 🔴 **无范围标注的数** ⇒ **{n_unscoped}** ⚠️")
    print(f"[sv] 合计 **{n_all}**")
    print()
    print("[sv] 🔴 **无标注最多的 12 份报告**：")
    for f, n in unscoped_files.most_common(12):
        print(f"[sv]     `{f}` ⇒ {n} 处")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
