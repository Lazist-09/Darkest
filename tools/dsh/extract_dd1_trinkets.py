#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_trinkets.py -- M4: generate darkest/data/trinkets.json from the local reference.

WHY (planner spec doc/modules/trinkets.md, M4 / #437)
    The spec pins the target table: **196 entries**, 7 fields each
        id / buffs / hero_class_requirements / rarity / price / limit / origin_dungeon
    plus acceptance:
        T1 196 entries, all fields present
        T2 id unique, buffs must exist in the primitive layer (M2, optional for now),
           rarity in the known set, price >= 0
        T5 price <= 1 entries (26 of them) are NOT purchasable (shop must not list them)

SOURCE (read-only; the user pointed the team at this local reference)
    F:\\GithubPro\\Darkest-Dungeon-Unity\\Assets\\Resources\\Data\\JsonTrinkets.json

WHAT IT DOES NOT DO
    * it never invents a value: every field is transcribed
    * it never touches the reference
    * it marks every entry with origin="dd1" so phase-A provenance is auditable
    * it does not decide anything about DLC: if a rarity only exists in an expansion, the
      report says so instead of silently dropping or admitting it (B6 / red line 29 family)

USAGE
    python tools/dsh/extract_dd1_trinkets.py [--ref <path>] [--out <path>] [--check]
      --check : do not write, only report what would change
EXIT: 0 = ok, 1 = reference missing / parse problem.
"""

import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonTrinkets.json"
# 🔴 默认**不写进 darkest/data**：参考件实测 488 条 ≠ 契约 T1 的 196 条 ⇒ **裁定前不落库** ✓
#    （裁定后：`--out darkest/data/trinkets.json` 即可 ✓）
DEFAULT_OUT = os.path.join(REPO, "reports", "dd1_trinkets_from_ref.json")
REPORT = os.path.join(REPO, "reports", "dd1_trinkets_source.md")

FIELDS = ["id", "buffs", "hero_class_requirements", "rarity", "price", "limit", "origin_dungeon"]


def main(argv):
    ref = argv[argv.index("--ref") + 1] if "--ref" in argv else DEFAULT_REF
    out = argv[argv.index("--out") + 1] if "--out" in argv else DEFAULT_OUT
    check = "--check" in argv
    if not os.path.isfile(ref):
        print("[m4] reference not found: %s" % ref)
        return 1

    with open(ref, "r", encoding="utf-8") as fh:
        src = json.load(fh)

    declared_rarities = src.get("rarities", [])
    items = src.get("trinkets", [])

    rows = []
    missing = []
    for t in items:
        row = {f: t.get(f) for f in FIELDS}
        # normalise: keep the original shape, but make absence explicit
        for f in FIELDS:
            if row[f] is None:
                row[f] = [] if f in ("buffs", "hero_class_requirements") else ("" if f == "origin_dungeon" else None)
                missing.append((row.get("id"), f))
        row["origin"] = "dd1"   # phase-A provenance marker (spec: origin field)
        rows.append(row)

    used_rarities = sorted({r["rarity"] for r in rows if r["rarity"]})
    price_le_1 = [r["id"] for r in rows if isinstance(r["price"], int) and r["price"] <= 1]
    class_req = [r["id"] for r in rows if r["hero_class_requirements"]]
    dupes = sorted({r["id"] for r in rows if [x["id"] for x in rows].count(r["id"]) > 1})

    payload = {
        "_note": "阶段 A 对齐数据：由 tools/dsh/extract_dd1_trinkets.py 从本地参考项目 JsonTrinkets.json 逐字段转写；"
                 "每个条目带 origin=dd1。协议：结构/合法性校验归 TrinketsConfig；装备关系归 Roster（无 Ledger）✓",
        "_source": ref,
        "_field_classes": "id/rarity/price/limit/hero_class_requirements = constraint（校验读）· buffs = behavior（运行时按 buff 原语生效）· origin = doc（溯源）✓",
        "rarities": declared_rarities,
        "trinkets": rows,
    }

    lines = [
        "# M4 Trinket 表：来源与实测结构（由提取器生成）",
        "",
        "> 来源：`%s`（本地参考项目，用户指定可参考）" % ref,
        "> 契约：`doc/modules/trinkets.md`（策划 `#437`）⇒ T1 196 条 / T2 校验 / T5 `price<=1` 不可购买 ✓",
        "",
        "## 实测",
        "",
        "- 条目数 = **%d**（契约 T1 期望 **196**）" % len(rows),
        "- 参考件 `rarities` 声明 **%d** 种：%s" % (len(declared_rarities), ", ".join("`%s`" % r for r in declared_rarities)),
        "- 条目里**实际用到**的 rarity **%d** 种：%s" % (len(used_rarities), ", ".join("`%s`" % r for r in used_rarities)),
        "- `price <= 1` 的条目 = **%d** 条（契约 T5 期望 **26**）" % len(price_le_1),
        "- 带 `hero_class_requirements` 的条目 = **%d** 条" % len(class_req),
        "- 重复 id = **%d**（T2 要求 0）" % len(dupes),
        "- 缺字段 = **%d** 处" % len(missing),
        "",
        "## 与契约的差异（如实列出，不静默）",
        "",
    ]
    if len(rows) != 196:
        lines.append("- ⚠️ 条目数 %d ≠ 契约 196 ⇒ **请策划裁**：是参考件少/多，还是契约数字需更新 ✓" % len(rows))
    if len(declared_rarities) != 13:
        lines.append("- ⚠️ `rarities` 声明 **%d** 种 ≠ 契约 T2 的 **13** ⇒ 请裁（多/少的那一种是什么）✓" % len(declared_rarities))
    if len(price_le_1) != 26:
        lines.append("- ⚠️ `price<=1` **%d** 条 ≠ 契约 T5 的 **26** ⇒ 请裁 ✓" % len(price_le_1))
    if not lines[-1].startswith("-"):
        lines.append("- （无差异）")

    if check:
        print("[m4] check only: rows=%d rarities_used=%d price<=1=%d dupes=%d missing_fields=%d"
              % (len(rows), len(used_rarities), len(price_le_1), len(dupes), len(missing)))
        return 0

    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=2)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[m4] wrote %s (%d entries)" % (os.path.relpath(out, REPO), len(rows)))
    print("[m4] rarities declared=%d used=%d | price<=1=%d | class_req=%d | dupes=%d | missing_fields=%d"
          % (len(declared_rarities), len(used_rarities), len(price_le_1), len(class_req), len(dupes), len(missing)))
    print("[m4] wrote %s" % os.path.relpath(REPORT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
