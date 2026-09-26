#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""resolve_ten_curio_names.py —— 判那 10 个 `curio_name` 是【任务专用】还是【奇物】✓

🔴 **要判的问题**（承接 `74_*.md §4`）：
   A8 的 11 个 `curio_name` 里，**10 个不在 A12 的 60 个奇物里** ⚠️
   ⇒ 两种可能：
      ① **任务专用的交互物**（只在 `JsonQuests` + 本地化里）✓
      ② **本来也是奇物，只是 `Curios.csv` 没列** ⚠️
   ⇒ ✅ **判法**：查**参考的奇物数据文件**（`Curios/*.bytes`）里有没有它们 ✓

🎖️ **判据**：**"这个名字的对不上 —— 是【不同命名空间】还是【同一个东西的两种收录】？"
   ⇒ 去【目标那一边】的数据文件里搜"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/resolve_ten_curio_names.py
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
R = os.path.join(REPO, "reports", "unity_ref")
REFDATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
TEN = ["animalistic_shrine", "beacon", "chirurgeons_satchel", "corrupted_altar",
       "foodstuff_crate", "infected_corpse", "protective_ward", "reliquary",
       "shipment_crates", "teleporter"]
OUT = os.path.join(R, "ten_curio_names_resolved.json")


def main() -> int:
    # ① 参考的 `Curios/` 目录里有什么
    cd = os.path.join(REFDATA, "Curios")
    print(f"[10] 参考 `Curios/` ⇒ {sorted(os.listdir(cd)) if os.path.isdir(cd) else '🔴 不存在'}")
    print()

    # ② 在这些文件里搜那 10 个名字
    texts = {}
    if os.path.isdir(cd):
        for f in sorted(os.listdir(cd)):
            if f.endswith(".meta"):
                continue
            texts[f] = io.open(os.path.join(cd, f), encoding="utf-8-sig",
                               errors="replace").read()
    rows = []
    for n in TEN:
        hits = [f for f, t in texts.items() if re.search(r"\b" + re.escape(n) + r"\b", t)]
        rows.append((n, hits))
        mark = "✅" if hits else "🔴"
        print(f"  {mark} `{n}` ⇒ {hits if hits else '**参考的 Curios/ 里没有**'}")
    print()

    # ③ 再全 `Data/` 搜（看在哪个文件里）
    print("[10] 若不在 `Curios/`，那它们在哪？（全 `Data/` 搜）")
    for n, hits in rows:
        if hits:
            continue
        found = []
        for dirpath, _d, fs in os.walk(REFDATA):
            for f in fs:
                if f.endswith(".meta"):
                    continue
                p = os.path.join(dirpath, f)
                try:
                    t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                if re.search(r"\b" + re.escape(n) + r"\b", t):
                    found.append(os.path.relpath(p, REFDATA))
        print(f"  `{n}` ⇒ {found if found else '🔴 **全 Data/ 都没有**'}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "判那 10 个 curio_name 是任务专用还是奇物。只读不落库 ✓",
                   "in_curios_dir": {n: h for n, h in rows}},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[10] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
