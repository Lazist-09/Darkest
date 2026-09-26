#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""count_dlc_variants.py —— 算 **29 个 DLC 怪的变体数**（补全"漏了多少变体"）✓

🔴 **为什么做**（承接 `68_*.md §5`）：
   那件给出**本体侧**：参考漏 **24 个变体**（7 个整怪 22 + 2 个 `_E`）✓
   ⇒ 📌 而 **DLC 侧的 29 个怪【变体数未算】** ⇒ 本件补上 ✓
   ⇒ ✅ 然后才有**完整的"参考漏了多少变体"** ✓

🎖️ **判据**：**"我说'漏了 N 个'时，N 是【条目】还是【变体】？"**（第 20 条）——
   本件把两个粒度的数都补齐 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/count_dlc_variants.py
"""

from __future__ import annotations

import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ED = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
OUT = os.path.join(REPO, "reports", "unity_ref", "dlc_variant_counts.json")


def variants_of(root_monsters: str) -> dict[str, list[str]]:
    """⇒ {怪名: [变体名...]} —— 一手布局：`<怪>/<怪>_X/` ✓"""
    out = {}
    if not os.path.isdir(root_monsters):
        return out
    for d in sorted(os.listdir(root_monsters)):
        dp = os.path.join(root_monsters, d)
        if not os.path.isdir(dp):
            continue
        vs = []
        for sub in sorted(os.listdir(dp)):
            if os.path.isdir(os.path.join(dp, sub)) and re.fullmatch(
                    re.escape(d) + r"_[A-Z]", sub):
                vs.append(sub)
        if vs:
            out[d] = vs
    return out


def main() -> int:
    # 参考已有的 base
    mon = json.load(open(os.path.join(REPO, "reports", "unity_ref",
                                     "monsters_from_ref.json"), encoding="utf-8"))
    ref_base = {re.sub(r"_[A-Z]$", "", x["stem"]) for x in mon["monsters"]}
    ref_var = {x["stem"] for x in mon["monsters"]}

    # DLC 的怪
    dlc_root = os.path.join(ED, "dlc")
    dlc_all = {}
    for d in sorted(os.listdir(dlc_root)):
        mp = os.path.join(dlc_root, d, "monsters")
        v = variants_of(mp)
        if v:
            dlc_all[d] = v
            print(f"[dv] **{d}** ⇒ {len(v)} 个怪 · "
                  f"{sum(len(x) for x in v.values())} 个变体 ✓")
    print()

    # 只算"参考没有的"
    n_mon = n_var = 0
    rows = []
    for d, v in dlc_all.items():
        for name, vs in sorted(v.items()):
            if name in ref_base:
                continue
            n_mon += 1
            n_var += len(vs)
            rows.append({"dlc": d, "monster": name, "variants": vs})
            print(f"[dv]   🔴 `{name}` ⇒ **{len(vs)}** 变体：{vs}")
    print()
    print(f"[dv] 🎖️ **参考没有的 DLC 怪：{n_mon} 个条目 · {n_var} 个变体** ✓")
    print()
    print(f"[dv] 🎖️🎖️ **两个粒度的完整账**：")
    print(f"[dv]   · 本体侧：**7 个条目** / **24 个变体**（`68_*.md` 实测）✓")
    print(f"[dv]   · DLC 侧：**{n_mon} 个条目** / **{n_var} 个变体**（本件）✓")
    print(f"[dv]   · 合计：**{7 + n_mon} 个条目** / **{24 + n_var} 个变体** ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "DLC 怪的变体计数（参考没有的那些）。只读不落库 ✓",
                   "dlc_monsters": {k: v for k, v in dlc_all.items()},
                   "missing_from_ref": rows,
                   "missing_monsters": n_mon, "missing_variants": n_var},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[dv] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
