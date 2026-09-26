#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_reM.py —— 扫全部探测脚本：**哪些正则用了 `$`/`^` 却没加 `re.M`** ✓

🔴 **为什么做**（承接 `71_*.md §7`）：
   我一条 `\.types\s+(.*)$` 少写 `re.M` ⇒ **把 139 报成 26** ⚠️
   ⇒ 📌 而本任务写了 **40+ 个探测脚本** ⇒ ⚠️ **可能有别的也犯了** ✓

🎖️ **判据（第 23 条）**：**"我的正则里的 `$`/`^` 加了 `re.M` 吗？"** ✓
   · 不加 ⇒ `$` = 整串末尾 · `^` = 整串开头 ⇒ ⚠️ **逐行匹配只中一行** ✓
   · **症状：读数偏低但不像 0** ⇒ 比得 0 更危险 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_reM.py
"""

from __future__ import annotations

import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOTS = (os.path.join(REPO, "tools", "dsh"),
         os.path.join(REPO, "reports", "unity_ref", "_probe"))

# 🔴 找 `re.compile(...)` / `re.finditer/findall/match/search(pattern, …)` 里
#    含 `$` 或 `^` 的【字面量模式】，且【同一调用里没有 re.M / re.MULTILINE】
CALL = re.compile(r"re\.(compile|finditer|findall|match|search|sub|split)\s*\(", re.S)


def scan(path: str) -> list[tuple[int, str]]:
    src = open(path, encoding="utf-8", errors="replace").read()
    lines = src.splitlines()
    hits = []
    for m in re.finditer(r"r?(['\"])(.*?)\1", src, re.S):
        pat = m.group(2)
        if "$" not in pat and "^" not in pat:
            continue
        # 该模式所在行，及向后 3 行内有没有 re.M
        ln = src[:m.start()].count("\n") + 1
        window = "\n".join(lines[max(0, ln - 1):ln + 3])
        if "re.M" in window or "re.MULTILINE" in window:
            continue
        # 只报"看起来是正则"的（含 \s \d \w .* [ ] ( )）
        if not re.search(r"\\[sdwn]|\.\*|\[|\]|\(\?|\$$|\^", pat):
            continue
        hits.append((ln, pat[:80]))
    return hits


def main() -> int:
    total = 0
    flagged = 0
    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for f in sorted(os.listdir(root)):
            if not f.endswith(".py"):
                continue
            p = os.path.join(root, f)
            total += 1
            hits = scan(p)
            if hits:
                flagged += 1
                print(f"[reM] 🔴 **{f}**")
                for ln, pat in hits:
                    print(f"[reM]     L{ln}: `{pat}`")
    print()
    print(f"[reM] 扫了 **{total}** 个脚本 ⇒ 🔴 **{flagged}** 个含可疑模式 ✓")
    print(f"[reM] 📌 注意：**可疑 ≠ 一定有错** —— 要逐条看【那个模式是拿谁去匹配的】")
    print(f"[reM]    · 匹配【整份文本】且要逐行 ⇒ 必须 `re.M` ✓")
    print(f"[reM]    · 匹配【单行字符串】（如 `re.fullmatch(name)`）⇒ 不需要 ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
