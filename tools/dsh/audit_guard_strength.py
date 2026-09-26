#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_guard_strength.py —— 审 **我方现有防线的【断言强度】** ✓

🔴 **为什么做**（承接 `155_*.md` 的判据 280/282）：
   我实测发现：**`Contains(子串)` 式断言会被"同形串"骗过** ⚠️
   ⇒ 📌 **那要问：现有防线里有多少是这种弱形式？** ✓
   🎖️ **判据（第 282 条）**：**"我加的防线 —— 它断言的那个串，
      在【手写】与【插值】两种情况下一样吗？一样 ⇒ 防不住漂移"** ✓

🔴 **做法**：扫 `darkest/tests/**` 里的**断言** ⇒ 按强度分三级：
   · 🔴 **弱**：`StringAssert.Contains(msg, 单字面量)`（易被同形/子串骗过）
   · 🎖️ **强**：`AreEqual(期望, 实际)` · `CollectionAssert` · `IsFalse/IsTrue(精确条件)`
   · ⚪ **中**：`Contains(msg, string.Join(…))`（读常量 ⇒ 能发现漂移）✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_guard_strength.py
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
T = os.path.join(REPO, "darkest", "tests")


def main() -> int:
    files = {}
    for f in sorted(os.listdir(T)):
        if f.endswith(".cs"):
            files[f] = io.open(os.path.join(T, f), encoding="utf-8",
                               errors="replace").read()
    print(f"[gs] 扫了 **{len(files)}** 个测试文件 ✓")
    print()

    weak, joined, strong = [], [], []
    for f, t in files.items():
        for i, l in enumerate(t.splitlines(), 1):
            s = l.strip()
            if s.startswith("//"):
                continue
            if "StringAssert.Contains" in s:
                if "string.Join" in s:
                    joined.append((f, i, s[:100]))
                else:
                    weak.append((f, i, s[:100]))
            elif re.search(r"Assert\.(AreEqual|AreNotEqual|IsTrue|IsFalse|"
                           r"IsNotNull|IsNull|ThrowsException)", s):
                strong.append((f, i, s[:100]))

    print(f"[gs] 🎖️ **强断言**（AreEqual/IsTrue/Throws…）⇒ **{len(strong)}** ✓")
    print(f"[gs] ⚪ **读常量的 Contains**（`string.Join(…)`）⇒ **{len(joined)}** ✓")
    for f, i, s in joined[:8]:
        print(f"[gs]     {f}:{i}  {s}")
    print(f"[gs] 🔴 **单字面量 Contains**（弱）⇒ **{len(weak)}** ✓")
    c = Counter(f for f, _, _ in weak)
    for f, n in c.most_common(12):
        print(f"[gs]     `{f}` ⇒ {n} 处")
    print()
    print("[gs] 🔴 **弱断言逐条**（前 20）：")
    for f, i, s in weak[:20]:
        print(f"[gs]     {f}:{i}")
        print(f"[gs]         {s}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
