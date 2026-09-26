#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_narrow_charclass.py —— 扫全部抽取器：**正则的字符类是否写窄了** ✓

🔴 **为什么做**（承接 `90_*.md §7`）：
   我的 `[a-z_]+` **不匹配大写** ⇒ 静默丢了 4 个驼峰字段（**142 处**）⚠️
   ⇒ 📌 而本任务写了 **40+ 个抽取/探测脚本** ⇒ ✅ **必须全扫** ✓

🎖️ **判据（第 62 条）**：**"凡是 `[a-z_]+` / `[a-z]+` 这种【窄字符类】当键名 ——
   都要问：原版有没有【大写或数字】的键？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_narrow_charclass.py
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
ROOTS = (os.path.join(REPO, "tools", "dsh"),
         os.path.join(REPO, "reports", "unity_ref", "_probe"))

# 🔴 窄字符类（可疑）
NARROW = re.compile(r"\[a-z_\]\+|\[a-z\]\+|\[a-z0-9_\]\+")
# 而"键名/标识符"上下文的证据：`case "…"` / `\.([a-z_]+)` / `(?P<key>...)`
KEYCTX = re.compile(r"\.\(|\(\?P<|case\s|KEY|key|token")


def main() -> int:
    total = flag = 0
    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for f in sorted(os.listdir(root)):
            if not f.endswith(".py"):
                continue
            total += 1
            p = os.path.join(root, f)
            lines = io.open(p, encoding="utf-8", errors="replace").read().splitlines()
            hits = []
            for i, l in enumerate(lines, 1):
                if not NARROW.search(l):
                    continue
                # 只报"看起来在抓键名"的
                if KEYCTX.search(l) or re.search(r"\\\.|\[A-Z", l):
                    hits.append((i, l.strip()[:120]))
            if hits:
                flag += 1
                print(f"[n] 🔴 **{f}**")
                for i, l in hits:
                    print(f"[n]     L{i}: `{l}`")
    print()
    print(f"[n] 扫了 **{total}** 个脚本 ⇒ 🔴 **{flag}** 个含可疑窄字符类 ✓")
    print("[n] 📌 逐条判定：**这个窄字符类是抓【原版键名】的吗？**")
    print("[n]    · 是 ⇒ 🔴 要按原版实测（有没有大写/数字）")
    print("[n]    · 否（抓我方自造 id / 纯小写域）⇒ ✅ 安全")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
