#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""count_features_monsters.py —— 清 `dlc/*/features/**` 下的**怪物**（补最后一块）✓

🔴 **为什么做**（承接 `69_*.md §5`）：
   已知 `arena_mp` 与 `crimson_court` **没有 `dlc/<id>/monsters/` 目录** ⇒
   ⇒ 📌 它们的怪可能在 **`features/`** 下（`63_*.md` 记过 `features` 是 CC 整包）✓
   ⇒ ✅ 本件沿着 `features/` 找**怪物目录**（形如 `<features>/<包>/monsters/<怪>/`）✓

🎖️ **判据（第 17 条升级版）**：**"这个类别的内容，只在一个位置吗？
→ 要把【所有可能位置】都列出来"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/count_features_monsters.py
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
OUT = os.path.join(REPO, "reports", "unity_ref", "features_monsters.json")


def variants(mon_dir: str) -> dict[str, list[str]]:
    out = {}
    if not os.path.isdir(mon_dir):
        return out
    for d in sorted(os.listdir(mon_dir)):
        dp = os.path.join(mon_dir, d)
        if not os.path.isdir(dp):
            continue
        vs = sorted(sub for sub in os.listdir(dp)
                    if os.path.isdir(os.path.join(dp, sub))
                    and re.fullmatch(re.escape(d) + r"_[A-Z]", sub))
        out[d] = vs
    return out


def main() -> int:
    mon = json.load(open(os.path.join(REPO, "reports", "unity_ref",
                                     "monsters_from_ref.json"), encoding="utf-8"))
    ref_base = {re.sub(r"_[A-Z]$", "", x["stem"]) for x in mon["monsters"]}
    ref_var = {x["stem"] for x in mon["monsters"]}

    dlc_root = os.path.join(ED, "dlc")
    found_all = {}
    for d in sorted(os.listdir(dlc_root)):
        base = os.path.join(dlc_root, d)
        if not os.path.isdir(base):
            continue
        # 🔴 沿 `features/` 找所有 `monsters/` 目录 ✓
        for dirpath, dirs, _fs in os.walk(base):
            if os.path.basename(dirpath) == "monsters":
                v = variants(dirpath)
                if v:
                    key = f"{d}::{os.path.relpath(dirpath, base)}"
                    found_all[key] = v
                    print(f"[fm] ✅ **{key}** ⇒ {len(v)} 怪 · "
                          f"{sum(len(x) for x in v.values())} 变体")
    print()

    n_mon = n_var = 0
    rows = []
    already = 0
    for key, v in found_all.items():
        for name, vs in sorted(v.items()):
            if name in ref_base:
                already += 1
                continue
            n_mon += 1
            n_var += len(vs)
            rows.append({"where": key, "monster": name, "variants": vs})
    for r in rows:
        print(f"[fm]   🔴 `{r['monster']}`（{r['where'].split('::')[0]}）⇒ "
              f"**{len(r['variants'])}** 变体：{r['variants']}")
    print()
    print(f"[fm] 这些位置里的怪：**{already + n_mon}** 个 ⇒ 参考已有 **{already}** · "
          f"🔴 参考没有 **{n_mon}**（{n_var} 变体）✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "`dlc/*/features/**/monsters/` 下的怪。只读不落库 ✓",
                   "locations": found_all, "missing_from_ref": rows,
                   "missing_monsters": n_mon, "missing_variants": n_var,
                   "already_in_ref": already}, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[fm] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
