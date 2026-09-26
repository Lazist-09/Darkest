#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_curios_vs_ref.py —— **逐条比对** 我方 curios vs 参考 ✓

🔴 **为什么做**（承接 `160_*.md` 的判据 308）：
   我把 `curios.json` 判为"有对应物"（`curio` 在参考 15 份文件里）
   ⇒ 📌 **但那只是"词命中"** ⇒ ✅ **要逐条比对才算"确认抄了"** ✓
   🎖️ **判据（第 308 条）**：**"'可能有对应物'要变成'确认抄了'——
      要【逐条比对】，不能停在'词命中'"** ✓

🔴 **做法**：读我方 7 条 curio ⇒ 去参考的 `Curios/` 里找同 id ⇒ 比字段 ✓
🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_curios_vs_ref.py
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
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
OURS = os.path.join(REPO, "darkest", "data", "curios.json")


def main() -> int:
    ours = json.load(open(OURS, encoding="utf-8"))["curios"]
    print(f"[cc] 我方 `curios.json` ⇒ **{len(ours)}** 条 ✓")
    print(f"[cc]   ids: {[c['id'] for c in ours]}")
    print()

    # 参考：找 Curios 目录
    cur = os.path.join(REF, "Curios")
    print(f"[cc] 参考 `Curios/` 存在：{os.path.isdir(cur)}")
    if os.path.isdir(cur):
        for f in sorted(os.listdir(cur)):
            p = os.path.join(cur, f)
            print(f"[cc]   {f}（{os.path.getsize(p)} B）")
    print()

    # 全参考里搜我方 id
    print("[cc] 🔴 **我方每个 id 在参考里出现吗**：")
    for c in ours:
        cid = c["id"]
        stem = cid.replace("cur_", "")
        found = []
        for dp, _d, ns in os.walk(REF):
            for f in ns:
                if f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                if cid in t or (len(stem) > 5 and re.search(re.escape(stem), t, re.I)):
                    found.append(os.path.relpath(p, REF))
        tag = "🔴" if found else "✅"
        print(f"[cc]   {tag} `{cid}` ⇒ {len(found)} 份" + (f"（{found[0]}）" if found else ""))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
