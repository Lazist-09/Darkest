#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""decode_target_encoding.py —— 解 **参考 `.target` 的数字编码**（`43`/`~1234`/`4321`）✓

🔴 **为什么做**（承接 `118_*.md §6`）：
   参考怪技能写 `.target 43` / `~1234` / `.launch 4321` ⇒ 📌 **那是"打哪些 rank"** ✓
   ⇒ ✅ 本件**从代码里读出编码规则**，而不是猜 ✓
   🎖️ **判据（第 145 条）**：**"这个数字编码的规则在哪？
      ⇒ 找【解析它的代码】，而不是从样例反推"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/decode_target_encoding.py
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

CODE = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"


def main() -> int:
    print("=" * 76)
    print("① 找解析 `.target` 的代码")
    print("=" * 76)
    for pat in (r'\.target', r'"\.target"', r'TargetRanks', r'ParseTarget'):
        hits = []
        for dp, _d, ns in os.walk(CODE):
            for f in ns:
                if not f.endswith(".cs"):
                    continue
                p = os.path.join(dp, f)
                t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                for i, l in enumerate(t.splitlines(), 1):
                    if re.search(pat, l):
                        hits.append((os.path.relpath(p, CODE), i, l.strip()[:110]))
        print(f"  `{pat}` ⇒ **{len(hits)}** 处")
        for h in hits[:6]:
            print(f"      {h[0]}:{h[1]}  {h[2]}")
    print()

    print("=" * 76)
    print("② 找 `TargetRanks` 的定义")
    print("=" * 76)
    for dp, _d, ns in os.walk(CODE):
        for f in ns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dp, f)
            t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            if "class TargetRanks" in t or "struct TargetRanks" in t:
                rel = os.path.relpath(p, CODE)
                lines = t.splitlines()
                for i, l in enumerate(lines, 1):
                    if "TargetRanks" in l and ("class" in l or "struct" in l):
                        print(f"  **{rel}:{i}**")
                        for j in range(i - 1, min(i + 45, len(lines))):
                            if lines[j].strip():
                                print(f"      {j+1:4d}|{lines[j].rstrip()[:120]}")
                        break
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
