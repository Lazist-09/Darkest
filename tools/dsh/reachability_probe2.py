#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""reachability_probe2.py —— 可达性探针**修正版**：遭遇表要扫【全盘】（含 DLC 的副本）

🔴 **为什么改**（第 22 条判据）：
   我第一版 `reachability_probe.py` 判 CC 的 23 怪"只有 7 个在遭遇表"⚠️
   ⇒ ✅ **错因**：它扫到了 `*.mash.darkest`，**但 `cc_entry.py` 发现 CC 的怪在
      `features/crimson_court/dungeons/courtyard/*.mash.darkest`** ⇒
      📌 **那【也是遭遇表】，只是在 DLC 的 `features/` 底下** ✓
   ⇒ 🎖️ **判据**：**"我扫的【文件名模式】够全，但【路径深度】够吗？"** ✓
      📌 **这正是第 21 条的同一根因**（`features/` 比 `dlc/<id>/` 深一层）⚠️

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/reachability_probe2.py [怪物名...]
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
ED = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
SKIP_F = {"compromised.json"}
SKIP_DIR = ("\\anim", "\\fx", "\\audio", "\\fonts", "\\shaders", "\\video",
            "\\panels", "\\ui", "\\colours", "\\loading_screen", "\\props")
DATA_EXT = (".json", ".darkest", ".txt", ".csv", "")
MON_TOK = re.compile(r"^[a-z][a-z0-9_]*_[A-Z]$")
# 🔴 **必须加 `re.M`**（第 23 条判据）：
#    无 `re.M` ⇒ `$` 只匹配【整个文件末尾】⇒ **只抓到最后一行的 `.types`** ⚠️
#    实测：同一文件无 `re.M` 命中 **1**、加 `re.M` 命中 **61** ⇒
#    📌 **全盘只收到 26 个怪，实际 139 个** ⇒ 🎖️ **假"无途径"，真"我没读到"** ✓
TYPES = re.compile(r"\.types\s+(.*)$", re.M)


def build_mash_index() -> set[str]:
    """🔴 **全盘**收遭遇表里的怪（不限路径深度）✓"""
    out = set()
    for dirpath, _d, fs in os.walk(ED):
        low = dirpath.lower()
        if any(x in low for x in SKIP_DIR):
            continue
        for f in fs:
            if not (".mash.darkest" in f or ".wavemash.darkest" in f):
                continue
            t = io.open(os.path.join(dirpath, f), encoding="utf-8-sig",
                        errors="replace").read()
            for m in TYPES.finditer(t):
                for nm in m.group(1).split():
                    if MON_TOK.match(nm):
                        out.add(re.sub(r"_[A-Z]$", "", nm))
    return out


def other_paths(name: str, own_prefix: str) -> Counter:
    c = Counter()
    for dirpath, _d, fs in os.walk(ED):
        low = dirpath.lower()
        if any(x in low for x in SKIP_DIR):
            continue
        for f in fs:
            if f.endswith(".meta") or f in SKIP_F:
                continue
            if os.path.splitext(f)[1].lower() not in DATA_EXT:
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, ED)
            if rel.startswith(own_prefix):
                continue
            try:
                t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            except Exception:
                continue
            if re.search(r"\b" + re.escape(name) + r"(?:_[A-Z])?\b", t):
                c[rel] += 1
    return c


def main(names: list[str]) -> int:
    mash = build_mash_index()
    print(f"[r2] 🎖️ **全盘遭遇表**里的怪 base：**{len(mash)}** ✓")
    print()
    ok = extra = no = 0
    rows = []
    for m in names:
        in_mash = m in mash
        oth = other_paths(m, "")
        if in_mash:
            ok += 1
        elif oth:
            extra += 1
        else:
            no += 1
        mark = "✅" if in_mash else ("⚠️" if oth else "🔴")
        print(f"  {mark} `{m}` ⇒ 遭遇表 {'有' if in_mash else '无'} · 别的途径 {len(oth)}")
        if not in_mash:
            for k in list(oth)[:3]:
                print(f"        {k}")
        rows.append({"monster": m, "in_mash": in_mash, "other": list(oth)})
    print()
    print(f"[r2] ✅ 在遭遇表：**{ok}** · ⚠️ 仅别的途径：**{extra}** · 🔴 都没有：**{no}**")
    return 0


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        fj = json.load(open(os.path.join(REPO, "reports", "unity_ref",
                                        "features_monsters.json"), encoding="utf-8"))
        args = sorted({r["monster"] for r in fj["missing_from_ref"]})
        print(f"[r2] 默认探 `features/` 的 {len(args)} 个怪 ✓\n")
    raise SystemExit(main(args))
