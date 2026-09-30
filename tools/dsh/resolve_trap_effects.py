#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""resolve_trap_effects.py —— 核 traps/obstacles 引用的 **Effect 名**是否都在 A6a 的表里 ✓

🔴 **为什么做**（承接 `89_*.md §5`）：
   陷阱的 `fail_effects`/`success_effects` 与障碍的 `fail_effects` **都是 Effect 名** ✓
   ⇒ 📌 那就该问：**它们都在 A6a 的 952 条 Effect 里吗？** ✓
   🎖️ **判据（第 58 条）**：**"这段数据引用了【哪张表】？那张表我方有吗？
      —— 更要问：【引用的名字都在表里吗】"** ✓

🎖️ **而已知样本**：
   · `"Heal Stress TrapD"`（4/4 陷阱的 success）✓
   · `"Stress 2"`（障碍 fail）· `"Blight 1/2/3"` · `"Bleed 1/2/3"` ·
     `"Lurker Trap Debuff 1"` ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/resolve_trap_effects.py
"""

from __future__ import annotations

import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
R = os.path.join(REPO, "reports", "unity_ref")
OUT = os.path.join(R, "trap_effects_resolved.json")


def main() -> int:
    a6a = json.load(open(os.path.join(R, "effects_from_ref.json"), encoding="utf-8"))
    names = {e["name"] for e in a6a["effects"]}
    print(f"[te] A6a 的 Effect 表：**{len(names)}** 条 ✓")
    print()

    a12 = json.load(open(os.path.join(R, "curios_from_ref.json"), encoding="utf-8"))
    refs = {}          # effect 名 → [(来源, 用途)]

    def add(n, src, use):
        refs.setdefault(n, []).append((src, use))

    for o in a12["obstacles"]["items"]:
        for e in o.get("fail_effects") or []:
            add(e, f"obstacle:{o['name']}", "fail")
    for t in a12["traps"]["items"]:
        for e in t.get("success_effects") or []:
            add(e, f"trap:{t['name']}", "success")
        for e in t.get("fail_effects") or []:
            add(e, f"trap:{t['name']}", "fail")
        for v in t.get("difficulty_variations") or []:
            for e in v.get("success_effects") or []:
                add(e, f"trap:{t['name']}@L{v.get('level')}", "success")
            for e in v.get("fail_effects") or []:
                add(e, f"trap:{t['name']}@L{v.get('level')}", "fail")

    print(f"[te] 引用的 Effect 名（去重）：**{len(refs)}** ✓")
    miss = 0
    for n in sorted(refs):
        hit = n in names
        if not hit:
            miss += 1
        print(f"[te]   {'✅' if hit else '🔴'} `{n}` ⇒ {len(refs[n])} 处引用")
        if not hit:
            for src, use in refs[n]:
                print(f"[te]         {src} ({use})")
    print()
    print(f"[te] 🎖️ **未命中 {miss} / {len(refs)}** ✓")
    if miss == 0:
        print(f"[te]   ⇒ ✅ **引用链完全闭合** —— 全部在 A6a 的 952 条里 ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "traps/obstacles 引用的 Effect 名 × A6a 952 条。只读不落库 ✓",
                   "effects_count": len(names),
                   "refs": {k: [list(x) for x in v] for k, v in refs.items()},
                   "unmatched": [n for n in refs if n not in names]},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[te] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
