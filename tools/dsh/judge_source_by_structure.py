#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""judge_source_by_structure.py —— 用**结构**判每层抄自哪个源 ✓

🔴 **为什么做**（承接 `164_*.md` 的判据 330）：
   泛词判据失效（两源都命中）⇒ 📌 **该换判据，而不是放弃** ⚠️
   ⇒ ✅ **换法：拿我方的【顶层键/字段名】去两源里搜 —— 专有名词不会泛命中** ✓
   🎖️ **判据（第 330 条）**：**"我的判据失效了 ——
      是【放弃那 N 层】还是【换判据重来】？（要明说）"** ✓
   🎖️ **判据（第 328 条）**：**"两源都『有』⇒ 要比【结构】"** ✓

🔴 **做法**：对我方每个文件的**顶层键 + 首元素的字段名**（专有名词）⇒
   在两源各搜 ⇒ 哪源命中更多且结构更近 ⇒ 判 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/judge_source_by_structure.py
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
FIRST = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
SUBS = ["shared", "campaign", "upgrades", "dungeons", "heroes", "monsters", "scripts",
        "panels", "raid"]


def load_ref():
    blob = {}
    for dp, _d, ns in os.walk(REF):
        if dp[len(REF):].count(os.sep) > 5:
            _d[:] = []
            continue
        for f in ns:
            if f.endswith((".meta", ".dll")):
                continue
            p = os.path.join(dp, f)
            try:
                if os.path.getsize(p) > 6_000_000:
                    continue
                blob[os.path.relpath(p, REF)] = io.open(
                    p, encoding="utf-8-sig", errors="replace").read()
            except Exception:
                pass
    return blob


def load_first():
    blob = {}
    for sub in SUBS:
        root = os.path.join(FIRST, sub)
        if not os.path.isdir(root):
            continue
        for dp, _d, ns in os.walk(root):
            if dp[len(root):].count(os.sep) > 5:
                _d[:] = []
                continue
            for f in ns:
                if f.endswith((".meta", ".dll", ".pck", ".cdt", ".xml")):
                    continue
                p = os.path.join(dp, f)
                try:
                    if os.path.getsize(p) > 6_000_000:
                        continue
                    blob[f"{sub}/{os.path.relpath(p, root)}"] = io.open(
                        p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    pass
    return blob


def probe_keys(path):
    """取我方的专有名词：顶层键 + 首元素字段名（去通用词）"""
    GENERIC = {"id", "name", "note", "config", "version", "type", "side", "amount",
               "value", "text", "chance", "weight", "rules", "random",
               # 🔴 我方**自造的元数据键** —— 两源都不会有 ⇒ 会污染判据 ✓
               "source", "origin", "delta", "events", "traits", "rooms", "nodes",
               "options", "players", "enemy", "player", "curios", "encounters",
               "unlocks", "heroes", "services", "diseases", "traps", "currency",
               "experience", "obstacles", "modifiers", "hooks"}
    d = json.load(open(path, encoding="utf-8"))
    keys = [k for k in d if not k.startswith("_") and k not in GENERIC]
    for k, v in d.items():
        if isinstance(v, list) and v and isinstance(v[0], dict):
            keys += [x for x in v[0] if x not in GENERIC]
            break
        if isinstance(v, dict):
            sub = list(v.values())[0] if v else None
            if isinstance(sub, dict):
                keys += [x for x in sub if x not in GENERIC]
            break
    return sorted(set(k for k in keys if len(k) > 4))


def hit(texts, key):
    """🔴 用【带引号的键名】搜 —— 避免子串/散文命中（实测踩过）✓"""
    pat = '"' + key + '"'
    return sum(1 for t in texts if pat in t)


def main() -> int:
    print("[js] 载入两源 ...", flush=True)
    ref = load_ref()
    first = load_first()
    print(f"[js] 参考 **{len(ref)}** 份 · 一手 **{len(first)}** 份 ✓")
    print()

    LAYERS = ["camp_skills.json", "curios.json", "expedition_nodes.json",
              "formation.json", "morale_events.json", "tuning.json", "economy.json",
              "roster.json", "sanitarium.json", "unlocks.json", "encounters.json",
              "room_contents.json", "traits.json", "trap_defs.json"]
    for f in LAYERS:
        p = os.path.join(D, f)
        if not os.path.isfile(p):
            continue
        keys = probe_keys(p)
        if not keys:
            print(f"[js]   ⚪ `{f}` ⇒ 抽不出专有名词")
            continue
        rh = sum(1 for k in keys if hit(ref.values(), k))
        fh = sum(1 for k in keys if hit(first.values(), k))
        who = ("🎖️ **参考项目**" if rh > fh * 1.5 else
               "⚠️ **一手 E 盘**" if fh > rh * 1.5 else
               "🔴 **两源相当（判不出）**")
        print(f"[js]   `{f}` ⇒ {who}")
        print(f"[js]       专有名词 {len(keys)} 个：参考命中 {rh} / 一手命中 {fh}")
        print(f"[js]       键：{keys[:8]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
