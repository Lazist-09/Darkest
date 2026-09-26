#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_ref_monster_source.py —— 验证「参考按【清单】抓怪物」这个推断 ✓

🔴 **要验的推断**（`65_*.md §3`）：
   参考的 230 个怪物文件**恰好漏掉**同一目录层的 7 个 ⇒
   📌 推断：**参考不是【遍历 `monsters/` 目录】，而是【按某个清单】抓** ✓

🎖️ **可检验的预测**：若"按遭遇表清单"⇒ 则**参考的 94 base 应当 ⊇ 遭遇表里出现过的怪**，
   且**参考的 94 与"遭遇表怪集合"应高度重合** ✓
   ⇒ ✅ 本件算出**交集/差集**，给推断一个读数 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/verify_ref_monster_source.py
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
MON = os.path.join(REPO, "reports", "unity_ref", "monsters_from_ref.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "ref_monster_source_check.json")


def main() -> int:
    mon = json.load(open(MON, encoding="utf-8"))
    ref_base = {re.sub(r"_[A-Z]$", "", x["stem"]) for x in mon["monsters"]}
    print(f"[v] 参考的怪物 base：**{len(ref_base)}** ✓")

    # ① 一手 monsters/ 的目录名集合
    mp = os.path.join(ED, "monsters")
    hand = {x for x in os.listdir(mp) if os.path.isdir(os.path.join(mp, x))}
    print(f"[v] 一手 `monsters/` 目录：**{len(hand)}** ✓")

    # ② 遭遇表里出现过的怪名（变体名归一化）
    #    🔴 **格式实测**：`hall: .chance 1 .types rabid_dog_A rabid_dog_A cultist_witch_A` ⚠️
    #       我第一版写的是 `monster:\s+(\S+)` ⇒ **得 0** ⇒ ✅ 按【实测格式】重写 ✓
    #    🔴 **第二版又混进了非怪物项**（`.can_be_ambush` 等 —— 它们是 `.key value`
    #       被 `.types` 之后的行尾误吃）⇒ ✅ **只收【形如 `名字_X` 的 token】** ✓
    #    🎖️ 判据：**"我按猜的格式解析 —— 得 0 或混进垃圾 ⇒ 先【看一行原文】再改"** ✓
    mash_names = Counter()
    # 🔴 **必须 `re.M`**（第 23 条判据）：无它 ⇒ `$` 只匹配【整串末尾】⇒ **只中一行** ⚠️
    #    实测：全盘遭遇表怪 **26（缺 `re.M`）vs 139（有 `re.M`）** ✓
    #    ⇒ 📌 **本工具原来的两个附带数字（26 / 交集 13）偏低** ⇒ ✅ 现修 ✓
    TYPES = re.compile(r"\.types\s+(.*)$", re.M)
    MONSTER_TOKEN = re.compile(r"^[a-z][a-z0-9_]*_[A-Z]$")   # 🔴 只收 `xxx_A` 形
    for dirpath, _d, fs in os.walk(ED):
        for f in fs:
            if not (".mash.darkest" in f or ".wavemash.darkest" in f):
                continue
            t = io.open(os.path.join(dirpath, f), encoding="utf-8-sig",
                        errors="replace").read()
            for m in TYPES.finditer(t):
                for nm in m.group(1).split():
                    if MONSTER_TOKEN.match(nm):
                        mash_names[re.sub(r"_[A-Z]$", "", nm)] += 1
    print(f"[v] 遭遇表里出现过的怪 base：**{len(mash_names)}** ✓")
    print()

    a = ref_base & set(mash_names)
    b = ref_base - set(mash_names)
    c = set(mash_names) - ref_base
    print(f"[v] 🎖️ **参考 ∩ 遭遇表**：**{len(a)}**")
    print(f"[v] 🔴 **参考有、遭遇表无**：**{len(b)}**  {sorted(b)[:12]}")
    print(f"[v] 🔴 **遭遇表有、参考无**：**{len(c)}**  {sorted(c)[:12]}")
    print()
    print(f"[v] 一手目录 ∩ 参考：**{len(hand & ref_base)}**")
    print(f"[v] 一手目录 ∩ 遭遇表：**{len(hand & set(mash_names))}**")
    print()
    print(f"[v] 🎖️ 判定：参考是【遍历目录】还是【按清单】？")
    print(f"[v]   · 参考 ⊂ 一手目录？ **{ref_base <= hand}**（漏 7 个 ⇒ 否）")
    print(f"[v]   · 参考 ⊂ 遭遇表？   **{ref_base <= set(mash_names)}**"
          f"（差 {len(b)} 个 ⇒ {'是' if not b else '否'}）")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "验证「参考按清单抓怪物」的推断。只读不落库 ✓",
                   "ref_base": sorted(ref_base), "hand_dirs": sorted(hand),
                   "mash_names": dict(mash_names),
                   "ref_and_mash": sorted(a), "ref_not_mash": sorted(b),
                   "mash_not_ref": sorted(c)}, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[v] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
