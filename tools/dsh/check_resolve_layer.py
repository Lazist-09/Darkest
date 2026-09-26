#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_resolve_layer.py —— 核 **`resolve_check_percent` / `resolve_xp_bonus_percent` 是否同族** ✓

🔴 **为什么做**（承接 `144_*.md §6` 的判据 237）：
   那 2 条归 `MoraleMod`，而 note 说「决心检定 ⇒ **士气系统**」/「决心经验加成 ⇒ **结算管线**」⇒
   📌 **可能同族**（枚举按轴、说明按层）⇒ ✅ 逐个去**消费侧**核 ✓
   🎖️ **判据（第 237 条）**：**"我改了 1 处分类 ——
      同族的【其它处】要一起看吗？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_resolve_layer.py
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
    print(f"[rs] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()

    for pat in ("resolve_check_percent", "resolve_xp_bonus_percent",
                "ResolveCheck", "ResolveXp", "resolve_xp", "AwardResolve",
                "ResolveLevel", "Resolve"):
        print(f"=== 🔴 `{pat}` ===")
        n = 0
        for rel, t in sorted(files.items()):
            for i, l in enumerate(t.splitlines(), 1):
                if pat in l:
                    n += 1
                    if n <= 8:
                        print(f"  {rel}:{i}")
                        print(f"      {l.strip()[:120]}")
        print(f"  ⇒ 共 **{n}** 处")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
