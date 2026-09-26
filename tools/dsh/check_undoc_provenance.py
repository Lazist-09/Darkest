#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_undoc_provenance.py —— 判 **7 个无说明文件** 属「自造」还是「抄了没记」✓

🔴 **为什么做**（承接 `159_*.md` 的判据 302）：
   "没标来源"有两种 ⇒ ✅ **判法：拿内容去参考里搜** ✓
   🎖️ **判据（第 302 条）**：**"'没标来源'有两种 ——
      【自造】与【抄了没记】；判法是【内容能不能在参考里找到】"** ✓

🔴 **做法**：对每个无说明文件的**顶层键名** ⇒ 去参考的 `Data/` 里搜同名 ⇒
   找到 ⇒ ⚠️ 可能"抄了没记" · 找不到 ⇒ ✅ 自造 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_undoc_provenance.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
D = os.path.join(REPO, "darkest", "data")
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"

UNDOC = ["camp_skills.json", "curios.json", "enemy_ai.json", "expedition_nodes.json",
         "formation.json", "morale_events.json", "units.json"]


def main() -> int:
    # 预读参考全文（一次性）
    ref_blob = []
    for dp, _d, ns in os.walk(REF):
        for f in ns:
            if f.endswith(".meta"):
                continue
            p = os.path.join(dp, f)
            try:
                ref_blob.append((os.path.relpath(p, REF),
                                 io.open(p, encoding="utf-8-sig", errors="replace").read()))
            except Exception:
                pass
    print(f"[up] 参考文件 **{len(ref_blob)}** 份已读入内存 ✓")
    print()

    for f in UNDOC:
        d = json.load(open(os.path.join(D, f), encoding="utf-8"))
        keys = [k for k in d if not k.startswith("_")] if isinstance(d, dict) else []
        print(f"--- 🔴 `{f}`（顶层键 {keys[:8]}）---")
        for k in keys[:8]:
            hits = [rel for rel, txt in ref_blob if re.search(r"\b" + re.escape(k) + r"\b", txt)]
            tag = "⚠️ 参考里有" if hits else "✅ 参考里无"
            print(f"    {tag}  `{k}` ⇒ {len(hits)} 份" + (f"（如 {hits[0]}）" if hits else ""))
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
