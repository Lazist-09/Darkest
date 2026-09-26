#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_handwritten_lists.py —— 找 **报错/注释里的【手写清单】**（会漂移的那类）✓

🔴 **为什么做**（承接 `153_*.md` 的判据 272）：
   `HeirloomConfig` 的报错用 `string.Join(" / ", AllowedBuildings)` ⇒ **不会漂移** ✓
   而我修的那处是**手写** ⇒ ⚠️ **一类问题** ⇒ ✅ 本件全仓找**手写清单** ✓
   🎖️ **判据（第 272 条）**：**"报错里的清单 ——
      是【手写的】还是【从常量插值的】？（后者不会漂移）"** ✓

🔴 **判据形态**：报错字符串里出现 **≥2 个**「`x` ／ `y`」或「A / B」式并列 ⇒
   视为手写清单 ⇒ 再看**同文件有没有对应的常量**（有常量却手写 = 🔴 漂移风险）✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_handwritten_lists.py
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
SC = os.path.join(REPO, "darkest", "scripts")


def main() -> int:
    files = {}
    for dp, _d, ns in os.walk(SC):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                files[os.path.relpath(p, REPO)] = io.open(
                    p, encoding="utf-8", errors="replace").read()
    print(f"[hl] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()

    # 手写清单：一行里 ≥2 个「/」或「／」分隔的 `xx` 或 "xx"
    sep = re.compile(r"`[A-Za-z_0-9:]+`\s*(?:/|／)\s*`[A-Za-z_0-9:]+`|"
                     r"\"[A-Za-z_0-9:]+\"\s*(?:/|／)\s*\"[A-Za-z_0-9:]+\"")
    total = 0
    risky = []
    for rel, t in sorted(files.items()):
        # 同文件里有没有「集合式常量」（数组/列表）
        has_const = bool(re.search(r"readonly\s+(?:IReadOnlyList|string\[\]|\w+\[\])\s*\w+\s*=", t))
        for i, l in enumerate(t.splitlines(), 1):
            if l.strip().startswith("//") or l.strip().startswith("///"):
                continue  # 注释里的清单（文档，不算报错）
            if sep.search(l):
                total += 1
                kind = "🔴 有常量却手写" if has_const else "· 手写（无同源常量）"
                risky.append((rel, i, kind, l.strip()[:100]))
    print(f"[hl] 🔴 **非注释行里的手写并列清单 ⇒ {total} 处** ✓")
    print()
    for rel, i, kind, txt in risky:
        print(f"[hl] {kind}")
        print(f"[hl]   {rel}:{i}")
        print(f"[hl]       {txt}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
