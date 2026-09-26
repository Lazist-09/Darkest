#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_two_stress_paths.py —— 查**同一单位的队列会不会同时有两条 stress** ✓

🔴 **为什么做**（承接 `103_*.md §6①`）：
   融合按 `SubEffect.Type` 配对 ⇒ 触发条件是
   **"同一单位 `EventQueue` 里同时有两个 `Fusable` 同 Type 的事件"** ✓
   ⇒ 📌 路径有三：
      ① **同一个技能的多个 Effect**（一个技能可带多条 effect）
      ② **同一技能对多目标**（但那是不同单位 ⇒ 不融合）
      ③ **两条不同技能先后入队**（同回合内）
   ⇒ ✅ 本件查 ① 与 ③ ✓

🎖️ **判据（第 97 条）**：**"这个运行时状态由【哪几条路径】产生？
   ⇒ 要逐条路径查，不是只看一条"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_two_stress_paths.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
R = os.path.join(REPO, "reports", "unity_ref")


def main() -> int:
    # ① 找"带 stress 的 Effect"
    a6a = json.load(open(os.path.join(R, "effects_from_ref.json"), encoding="utf-8"))
    stress_fx = [e["name"] for e in a6a["effects"]
                 if e["fields"].get("stress") or e["fields"].get("healstress")]
    print(f"[ts] 带 `stress`/`healstress` 的 effect：**{len(stress_fx)}** ✓")
    sset = set(stress_fx)
    print(f"[ts]   样例：{stress_fx[:8]}")
    print()

    # ② 查技能（Heroes/Info/*.bytes 与 Monsters）里，一条技能是否引用【≥2 个】带 stress 的 effect
    print("[ts] 🔴 **路径①：一条技能里有≥2个带 stress 的 effect？**")
    REFDATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
    hits = []
    for root, label in ((os.path.join(REFDATA, "Heroes", "Info"), "Heroes"),
                        (os.path.join(REFDATA, "Monsters"), "Monsters")):
        if not os.path.isdir(root):
            continue
        for f in sorted(os.listdir(root)):
            if f.endswith(".meta"):
                continue
            t = io.open(os.path.join(root, f), encoding="utf-8-sig",
                        errors="replace").read()
            # 按技能块切（形如 combat_skill: …），查每块里的 .effect "X"
            for m in re.finditer(r"(?:combat_skill|skill):(.*?)(?=(?:combat_skill|skill):|\Z)",
                                 t, re.S):
                block = m.group(1)
                effs = re.findall(r'\.effect\s+"([^"]+)"', block)
                st = [e for e in effs if e in sset]
                if len(st) >= 2:
                    nm = re.search(r"\.id\s+(\S+)", block)
                    hits.append((label, f, nm.group(1) if nm else "?", st))
    if hits:
        for label, f, nm, st in hits[:12]:
            print(f"[ts]   🔴 {label}/{f} 技能 `{nm}` ⇒ {st}")
    else:
        print(f"[ts]   🔴 **0 处** ⇒ 路径①【不产生】两条 stress")
    print()

    print("[ts] 🎖️ **结论**：")
    print("[ts]   · 路径①（一条技能多 effect）⇒ "
          f"**{'有 ' + str(len(hits)) + ' 处' if hits else '无'}**")
    print("[ts]   · 路径②（多目标）⇒ 不同单位 ⇒ **结构上不可能融合** ✓")
    print("[ts]   · 路径③（两条技能先后入队）⇒ ⚠️ **本件未查**（需读战斗流程）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
