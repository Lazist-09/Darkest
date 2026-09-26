#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""predict_formula_tests_all.py —— **`FormulaTests` 全部断言**在新旧口径下逐个算 ✓

🔴 **为什么做**（承接 `108_*.md §5`）：
   `108_*.md` 只核了 5 条**直接**调用 `ApplyDamageRounding` 的断言 ⚠️
   ⇒ 🔴 **但 `PhysicalHit`/`SpiritHit`【内部也调它】**（`BattleMath.cs:107`/`134`）
   ⇒ 📌 **那 7 条 `PhysicalHit` + 1 条 `SpiritHit` 也在影响面里** ✓
   ⇒ ✅ 本件把**全部断言**算一遍 ✓

🎖️ **判据（第 109 条）**：**"我核了'N 条直接调用'——
   有没有【间接受影响】的？⇒ 看被调函数【内部】还调了谁"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/predict_formula_tests_all.py
"""

from __future__ import annotations

import math
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass


def round_away(x):
    return math.floor(x + 0.5) if x >= 0 else math.ceil(x - 0.5)


def old_r(raw, fl=1):
    return max(fl, round_away(raw))


def new_r(raw, fl=0):
    return max(fl, math.ceil(raw))


def physical_mitigation(defense, divisor):
    # 🔴 **实测**（`BattleMath.cs:27-28`）：`physDef / (physDef + divisor)` ⚠️
    #    我第一版写 `min(1, def/div)` ⇒ **算错了**（L63 得 7，实际应是别的值）
    #    🎖️ 判据（第 110 条）：**"我复算用的公式，和源码【同一行】吗？"** ✓
    return defense / (defense + divisor)


def mental_mitigation(resilience, divisor, cap_pct):
    # `MentalMitigation`：`min(resilience/divisor, cap/100)` ✓
    return min(resilience / divisor, cap_pct / 100.0)


def spirit_hit(attack, skill_multiplier, resilience, divisor, cap_pct, rnd):
    base = attack * skill_multiplier
    mit = mental_mitigation(resilience, divisor, cap_pct)
    raw = base * (1.0 - mit)
    return rnd(raw)


def physical_hit(attack, skill_multiplier, defense, divisor, rnd):
    base = attack * skill_multiplier
    mit = physical_mitigation(defense, divisor)
    raw = base * (1.0 - mit)
    return rnd(raw)


PHYS = [
    (12, 1.0, 8, 30, 9, "L43"),
    (11, 1.0, 8, 30, 9, "L47"),
    (11, 0.95, 4, 30, 9, "L51"),
    (8, 0.9, 8, 30, 6, "L55"),
    (12, 1.0, 8, 30, 9, "L59"),
    (12, 1.0, 12, 30, 9, "L63"),
    (13, 0.9, 4, 30, 10, "L67"),
]
DIRECT = [(0.5, 1, "L92"), (1.49, 1, "L93"), (1.5, 2, "L94"),
          (-3.2, 1, "L95"), (7.68, 8, "L96")]

print("[pf] 🔴 **先看：`PhysicalHit` 那 7 条会不会红？**（它们【间接】走 ApplyDamageRounding）")
broken = []
for atk, sm, df, dv, exp, tag in PHYS:
    o = physical_hit(atk, sm, df, dv, old_r)
    n = physical_hit(atk, sm, df, dv, new_r)
    ok = "✅" if n == exp else "🔴"
    print(f"[pf]   {tag}  atk={atk:>2} sm={sm:<5} def={df:>2} ⇒ 旧 **{o}** · 新 **{n}** "
          f"（期望 {exp}）{ok}")
    if n != exp:
        broken.append((tag, o, n, exp))
print()

print("[pf] 🔴 **`SpiritHit` 那条（L74）**")
o = spirit_hit(12, 0.8, 50, 250, 40, old_r)
n = spirit_hit(12, 0.8, 50, 250, 40, new_r)
print(f"[pf]   L74  atk=12 sm=0.8 res=50 ⇒ 旧 **{o}** · 新 **{n}**（期望 8）"
      f"{'✅' if n == 8 else '🔴'}")
if n != 8:
    broken.append(("L74", o, n, 8))
print()

print("[pf] 🎖️ **`ApplyDamageRounding` 直调的 5 条**")
for raw, exp, tag in DIRECT:
    o, n = old_r(raw), new_r(raw)
    ok = "✅" if n == exp else "🔴"
    print(f"[pf]   {tag}  raw={raw:>6} ⇒ 旧 **{o}** · 新 **{n}**（期望 {exp}）{ok}")
    if o == exp and n != exp:
        broken.append((tag, o, n, exp))
print()
print(f"[pf] 🔴🔴 **合计会红：{len(broken)} 条** ⇒ {[b[0] for b in broken]}")
return_broken = broken
