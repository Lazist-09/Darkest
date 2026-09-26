#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_morale_mod_layer.py —— 把 `MoraleMod` 的 **6 条全核一遍**（轴 vs 层）✓

🔴 **为什么做**（承接 `145_*.md §6③`）：
   已核出 **4 条"枚举说战斗、实现在趟层"**（`stress_heal_*` ×2 · `resolve_*` ×2）⇒
   📌 而 `MoraleMod` 一共 **6 条** ⇒ ✅ **剩下 2 条也要核** ✓
   🎖️ **判据（第 237 条·彻底形式）**：**"我改了 1 处分类 ——
      同族的【其它处】要一起看吗？"** ⇒ ✅ **全族核完才算** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_morale_mod_layer.py
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

# MoraleMod 的 6 条（从 ByStatType 抽）
MORALE_MOD = ["stress_dmg_percent", "stress_dmg_received_percent", "stress_heal_percent",
              "stress_heal_received_percent", "resolve_check_percent",
              "resolve_xp_bonus_percent"]


def main() -> int:
    files = {}
    for dp, _d, ns in os.walk(SC):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                files[os.path.relpath(p, REPO)] = io.open(
                    p, encoding="utf-8", errors="replace").read()

    print(f"[ml] `MoraleMod` 共 **{len(MORALE_MOD)}** 条 ⇒ 逐个核消费点 ✓")
    print()
    for n in MORALE_MOD:
        hits = []
        for rel, t in sorted(files.items()):
            for i, l in enumerate(t.splitlines(), 1):
                if re.search(r"\b" + re.escape(n) + r"\b", l):
                    hits.append((rel, i))
        # 排除表自身
        real = [h for h in hits if "BuffPrimitiveTranslation" not in h[0]]
        tag = "✅ 有消费点" if real else "🔴 **纯未接线**"
        print(f"[ml]   {tag}  `{n}` ⇒ 总 {len(hits)} · 非表 {len(real)}")
        for h in real[:3]:
            print(f"[ml]        {h[0]}:{h[1]}")
    print()
    print("[ml] 🎖️ **注**：'纯未接线'【不代表】落点不存在 —— 落点可能在"
          "【同轴的不同名字】下（如`IsAfflicted`/`AwardExperienceForBattle`）✓")
    print("[ml]    ⇒ 要按【语义】找落点，不是按字面名 ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
