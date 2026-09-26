#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""map_our_target_vs_ref.py —— 做 **我方 `target` ↔ 参考后缀编码** 的映射表 ✓

🔴 **为什么做**（承接 `119_*.md §6②`）：
   我方用 `{"scope":…, "side":…, "slots":[…]}`，参考用 `@`/`~`/`?` + 数字 ✓
   ⇒ 📌 **语义同构、表示不同** ⇒ ✅ 本件**逐条列出**我方的 target 写法，
      并给出参考的等价编码 ✓
   🎖️ **判据（第 149 条）**：**"两边表示不同 —— 那就逐个映射，
      并标出【无法映射】的项"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/map_our_target_vs_ref.py
"""

from __future__ import annotations

import json
import os
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def to_ref_encoding(t: dict) -> str:
    """把我们的 target 结构翻成参考的后缀编码。"""
    if not t:
        return "(无)"
    scope = t.get("scope")
    if scope == "self":
        return "(空串 ⇒ 打自己)"
    if scope == "move_range":
        return "🔴 参考无对应（移动范围 ⇒ 走 MoveSkill）"
    side = t.get("side")
    slots = t.get("slots") or []
    if not slots:
        return "🔴 无 slots ⇒ 无法映射"
    digits = "".join(str(s) for s in sorted(slots))
    return ("@" if side == "player" else "") + digits


def main() -> int:
    sk = json.load(open(os.path.join(REPO, "darkest", "data", "skills.json"),
                        encoding="utf-8"))
    print("[mt] 🎖️ **我方 44 条技能的 target → 参考编码**：")
    print(f"[mt]   {'技能':30s} {'我方写法':38s} 参考编码")
    enc = Counter()
    unmapped = []
    for s in sk["skills"]:
        t = s.get("target") or {}
        e = to_ref_encoding(t)
        enc[e] += 1
        ours = json.dumps(t, ensure_ascii=False, separators=(",", ":"))
        print(f"[mt]   {s['id']:30s} {ours[:38]:38s} {e}")
        if e.startswith("🔴"):
            unmapped.append((s["id"], ours, e))
    print()
    print("[mt] 🎖️ **参考编码的分布**：")
    for k, v in enc.most_common():
        print(f"[mt]   `{k}` ⇒ **{v}** 条")
    print()
    print(f"[mt] 🔴 **无法映射的：{len(unmapped)}** ✓")
    for sid, ours, e in unmapped:
        print(f"[mt]     `{sid}` ⇒ {ours} ⇒ {e}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
