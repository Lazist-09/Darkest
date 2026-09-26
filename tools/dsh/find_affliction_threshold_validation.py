#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_affliction_threshold_validation.py —— 找 **"阈值必须 == morale.start"的加载期校验** ✓

🔴 **为什么做**（承接 `132_*.md §5` 的判据 193）：
   `tuning.json:111` 注释说：**"`morale_affliction_threshold` 必须 == `morale.start`
   （加载期校验强制）"** ⇒ 📌 **但那个校验【真的在】吗？** ✓
   ⇒ ✅ 本件去代码里找 ✓
   🎖️ **判据（第 193 条）**：**"注释说'加载期校验强制'——
      那个校验真的在吗？（第 190 条的教训：注释可能只写了计划）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_affliction_threshold_validation.py
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
    print(f"[av] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()
    for pat in ("AfflictionThreshold", "affliction_threshold", "MoraleStart",
                "morale_affliction"):
        print(f"=== 🔴 `{pat}` ===")
        n = 0
        for rel, t in sorted(files.items()):
            for i, l in enumerate(t.splitlines(), 1):
                if pat in l:
                    n += 1
                    print(f"  {rel}:{i}")
                    print(f"      {l.strip()[:130]}")
        if n == 0:
            print("  🔴 **0 处**")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
