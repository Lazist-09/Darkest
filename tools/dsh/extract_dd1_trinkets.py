#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_trinkets.py -- M4: land darkest/data/trinkets.json from the **primary** E-drive source.

RULING (planner #452, DELIVERY-DESIGNER-M4-NUMBERCLASH-RULING-20260921)
    The local Unity reference is **third-party**: measured, it MISSED 7 entries, ADDED 5, and
    lacked 2 rarities. Red line 29 §29.4 priority: (1) primary E-drive  (2) second-hand wiki
    (3) third-party reference -> we must go back to the primary.
        source   : E:/SteamLibrary/steamapps/common/DarkestDungeon/trinkets/base.entries.trinkets.json
        rarities : E:/.../trinkets/base.rarities.trinkets.json   (14 entries, carries award_category)
        filter   : exclude rarity == "kickstarter"               => 196 entries / 13 rarities
        purchase : NOT purchasable == award_category != "universal"  (26 of them)
                   (the planner's own correction: price<=1 is 15 and is the WRONG test)

EVERY NUMBER IS RE-MEASURED HERE, NOT TRUSTED:
    490 total -> 196 after the filter -> 13 rarities -> 26 non-universal, printed on every run.

COMPLIANCE (red line 29 / B6)
    * READ-ONLY on the E drive; nothing is written back there
    * the emitted table carries origin="dd1" per entry and is the project's own re-typed data
    * the file lives in darkest/data/ (shipped project data), NOT in resources/ or an export

USAGE
    python tools/dsh/extract_dd1_trinkets.py                 # write darkest/data/trinkets.json
    python tools/dsh/extract_dd1_trinkets.py --check         # measure only, write nothing
    python tools/dsh/extract_dd1_trinkets.py --source <dir>  # override the E-drive trinkets dir
OUTPUT IS ASCII-ONLY (GBK console; printing CJK kills the tool).
EXIT: 0 = ok, 1 = source missing / parse problem.
"""

import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
E_TRINKETS = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\trinkets"
OUT = os.path.join(REPO, "darkest", "data", "trinkets.json")
REPORT = os.path.join(REPO, "reports", "dd1_trinkets_source.md")

EXCLUDE_RARITY = "kickstarter"
FIELDS = ["id", "buffs", "hero_class_requirements", "rarity", "price", "limit", "origin_dungeon"]


def main(argv):
    src_dir = argv[argv.index("--source") + 1] if "--source" in argv else E_TRINKETS
    check = "--check" in argv
    e_entries = os.path.join(src_dir, "base.entries.trinkets.json")
    e_rarities = os.path.join(src_dir, "base.rarities.trinkets.json")
    for p in (e_entries, e_rarities):
        if not os.path.isfile(p):
            print("[m4] primary source missing: %s" % p)
            return 1

    with open(e_entries, "r", encoding="utf-8") as fh:
        entries = json.load(fh)
    entries = entries.get("entries") or entries.get("trinkets") or []
    with open(e_rarities, "r", encoding="utf-8") as fh:
        rarities = json.load(fh)
    rarities = rarities.get("rarities") or []

    total = len(entries)
    total_rarities = len(rarities)
    kept = [t for t in entries if t.get("rarity") != EXCLUDE_RARITY]
    kept_rarities = sorted({t.get("rarity") for t in kept})
    cat = {r.get("id"): (r.get("award_category") or "") for r in rarities}

    rows = []
    for t in kept:
        row = {f: t.get(f) for f in FIELDS}
        for f in FIELDS:
            if row[f] is None:
                row[f] = [] if f in ("buffs", "hero_class_requirements") else ("" if f == "origin_dungeon" else None)
        row["award_category"] = cat.get(row["rarity"], "")
        row["origin"] = "dd1"
        rows.append(row)

    non_universal = [r["id"] for r in rows if r["award_category"] != "universal"]
    price_le_1 = [r["id"] for r in rows if isinstance(r["price"], int) and r["price"] <= 1]
    dupes = sorted({r["id"] for r in rows if [x["id"] for x in rows].count(r["id"]) > 1})

    payload = {
        "_note": "阶段 A 对齐数据：由 tools/dsh/extract_dd1_trinkets.py 从 E 盘【一手】base.entries.trinkets.json 转写；"
                 "筛选口径 = 排除 rarity==kickstarter（策划 #452）=> 196 条 / 13 种 rarity。每条约 origin=dd1 ✓。"
                 "协议：结构/合法性校验归 TrinketsConfig；装备关系归 Roster（无 Ledger）✓",
        "_source": e_entries,
        "_ruling": "策划 #452：(乙) 一手 E 盘为准；本地参考件是第三方（漏 7 条 / 多 5 条 / 缺 2 种 rarity）✗",
        "_field_classes": "id/rarity/price/limit/hero_class_requirements/award_category = constraint（校验读）· "
                          "buffs = behavior（按 buff 原语生效）· origin = doc（溯源）✓",
        "rarities": [{"id": r.get("id"), "award_category": cat.get(r.get("id"), "")} for r in rarities],
        "trinkets": rows,
    }

    lines = [
        "# M4 Trinket 表：来源与实测（由提取器生成 · 一手 E 盘）",
        "",
        "> 源：`%s`（**一手**）· rarity 表：`base.rarities.trinkets.json`" % e_entries,
        "> 裁定：策划 `#452` —— 参考件是**第三方**（漏 7 / 多 5 / 缺 2 种 rarity）⇒ **回一手** ✓",
        "> 口径：**排除 `rarity == kickstarter`** ⇒ **196 条 / 13 种 rarity** ✓",
        "",
        "## 实测（每次运行重新测，不信任历史数字）",
        "",
        "- E 盘全量条目 = **%d**（裁定定义：490）" % total,
        "- E 盘 rarity 条数 = **%d**（裁定定义：14）" % total_rarities,
        "- 排除 `%s` 后条目 = **%d**（裁定定义：196）" % (EXCLUDE_RARITY, len(rows)),
        "- 排除后 rarity 种数 = **%d**（裁定定义：13）：%s"
        % (len(kept_rarities), ", ".join("`%s`" % r for r in kept_rarities)),
        "- 排除后**非 universal** = **%d**（裁定定义：26）⇒ 这才是**不可购买**的判据 ✓" % len(non_universal),
        "- 排除后 `price <= 1` = **%d**（裁定定义：15）⇒ ⚠️ **不是**购买判据（策划已更正）✓" % len(price_le_1),
        "- 重复 id = **%d**（T2 要求 0）· 缺字段 = 0（提取时已补显式空值）" % len(dupes),
        "",
        "## rarity 表（含 `award_category` —— 购买判据的唯一来源）",
        "",
        "| rarity | award_category |",
        "|---|---|",
    ]
    for r in payload["rarities"]:
        lines.append("| `%s` | `%s` |" % (r["id"], r["award_category"]))

    if check:
        print("[m4] check: total=%d rarities=%d kept=%d kept_rarities=%d non_universal=%d price<=1=%d dupes=%d"
              % (total, total_rarities, len(rows), len(kept_rarities), len(non_universal), len(price_le_1), len(dupes)))
        return 0

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=2)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[m4] total=%d rarities=%d -> kept=%d kept_rarities=%d non_universal=%d price<=1=%d dupes=%d"
          % (total, total_rarities, len(rows), len(kept_rarities), len(non_universal), len(price_le_1), len(dupes)))
    print("[m4] wrote %s" % os.path.relpath(OUT, REPO))
    print("[m4] wrote %s" % os.path.relpath(REPORT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
