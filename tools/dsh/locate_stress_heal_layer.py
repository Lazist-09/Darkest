#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""locate_stress_heal_layer.py —— 定 **`stress_heal_*` 的落点到底是哪一层** ✓

🔴 **为什么做**（承接 `143_*.md` 与窗口第 ⑬ 件）：
   `stress_heal_percent`/`stress_heal_received_percent` 的 note 说「城镇/扎营」，
   而枚举归 `Target.MoraleMod`（战斗级）⇒ 🔴 **两处指向不同的层** ⚠️
   ⇒ ✅ **本件去【我方代码】里找"士气恢复"到底在哪算** ✓
   🎖️ **判据（第 233 条）**：**"这个原语的落点有争议 ——
      那就去【消费侧】看它实际被用在哪，而不是看说明"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/locate_stress_heal_layer.py
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
    print(f"[sh] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()

    for pat in ("StressHeal", "stress_heal", "HealStress", "IncreaseMorale",
                "MoraleHeal", "ApplyTeamOnce"):
        print(f"=== 🔴 `{pat}` ===")
        n = 0
        for rel, t in sorted(files.items()):
            for i, l in enumerate(t.splitlines(), 1):
                if pat in l:
                    n += 1
                    print(f"  {rel}:{i}")
                    print(f"      {l.strip()[:125]}")
        if n == 0:
            print("  🔴 **0 处**")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
