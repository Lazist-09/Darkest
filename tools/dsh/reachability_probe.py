#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""reachability_probe.py —— 核怪物是否【真的在遭遇表里】（可遭遇）✓

🔴 **为什么做**（承接 `65_*.md`）：
   那 7 个被参考丢掉的怪 ⇒ 用**变体名**搜 ⇒ **7/7 在 `*.mash.darkest` 里** ✓
   ⇒ 📌 那就该问 **DLC 的 29 个怪【是不是也一样】** ✓
      · 若一样 ⇒ ⚠️ **也是"漏收的有用内容"** ⇒ 归策划
      · 若不在表里 ⇒ ✅ **那是 DLC 独有副本**（本来就只在 DLC 里出现）

🎖️ **判据**：**"这个怪物【凭什么进游戏】？"** ——
   遭遇表（`*.mash`/`*.wavemash`）· 召唤 · 事件 · 脚本 ⇒ **要指出一种** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/reachability_probe.py [怪物名...]
"""

from __future__ import annotations

import io
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

ED = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
SKIP_F = {"compromised.json"}
SKIP_DIR = ("\\anim", "\\fx", "\\audio", "\\fonts", "\\shaders", "\\video",
            "\\panels", "\\ui", "\\colours", "\\loading_screen", "\\props")
DATA_EXT = (".json", ".darkest", ".txt", ".csv", "")


def main(names: list[str]) -> int:
    print(f"[reach] 探 **{len(names)}** 个怪物名的可达性 ✓")
    print("[reach] 口径：搜【变体名】（`<名>_A`…`_D`）＋ 基础名；文件限数据类 ✓")
    print()
    rows = []
    for m in names:
        files = Counter()
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
                try:
                    t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                pat = r"\b" + re.escape(m) + r"(?:_[A-Z])?\b"
                if re.search(pat, t):
                    rel = os.path.relpath(p, ED)
                    files[rel] = 1
        # 分类：遭遇表 vs 其它
        mash = [f for f in files if ".mash.darkest" in f or ".wavemash.darkest" in f]
        own = [f for f in files if f.startswith("monsters" + os.sep) or "/monsters/" in f]
        other = [f for f in files if f not in mash and f not in own]
        rows.append((m, len(files), len(mash), len(own), len(other), other[:3]))
        mark = "✅" if mash else ("⚠️" if other else "🔴")
        print(f"  {mark} `{m}` ⇒ 共 {len(files)} 文件 · **遭遇表 {len(mash)}** · "
              f"自身 {len(own)} · 其它 {len(other)}")
        for f in other[:3]:
            print(f"        {f}")
    print()
    n_mash = sum(1 for r in rows if r[2])
    print(f"[reach] 🎖️ **在遭遇表里的：{n_mash} / {len(rows)}**")
    return 0


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        # 默认：DLC 的 29 个怪
        import json
        REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
        mon = json.load(open(os.path.join(REPO, "reports", "unity_ref",
                                          "monsters_from_ref.json"), encoding="utf-8"))
        have = {re.sub(r"_[A-Z]$", "", x["stem"]) for x in mon["monsters"]}
        dlc = set()
        for d in sorted(os.listdir(os.path.join(ED, "dlc"))):
            mp = os.path.join(ED, "dlc", d, "monsters")
            if os.path.isdir(mp):
                dlc |= {x for x in os.listdir(mp)
                        if os.path.isdir(os.path.join(mp, x))}
        args = sorted(dlc - have)
        print(f"[reach] 默认探 DLC 的 {len(args)} 个怪（参考没有的那些）✓\n")
    raise SystemExit(main(args))
