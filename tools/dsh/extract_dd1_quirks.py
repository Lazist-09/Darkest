#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_quirks.py -- M5: land darkest/data/quirks.json from the **primary** E-drive library.

WHY (planner card M5: "Quirk 系统（从零建）: schema / 库（170 条，先 ~20） / 互斥必须可断言")

SOURCE (primary, read-only -- the M4 lesson: do NOT assume the third-party reference is primary)
    E:/SteamLibrary/steamapps/common/DarkestDungeon/shared/quirk/quirk_library.json
    measured: 170 entries -- which is exactly the count the contract pins (no DLC filter needed).

WHAT IT MEASURES (every run; numbers are never trusted from history)
    * entry count, field set
    * the distribution of is_positive / is_disease / classification
    * the incompatible_quirks graph:
        - how many entries declare incompatibilities, how many pairs in total
        - **dangling references** (an id that is not in the library)
        - **asymmetry** (a -> b but not b -> a): the contract requires 互斥 to be ASSERTABLE,
          so asymmetry is reported as a finding rather than silently accepted.

OUTPUT (reports/ + darkest/data/quirks.json)
    every entry carries origin="dd1"; the table is our own re-typed data (red line 29 / B6).
OUTPUT IS ASCII-ONLY (GBK console).
EXIT: 0 = ok, 1 = source missing / parse problem.
"""

import collections
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
E_LIB = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\shared\quirk\quirk_library.json"
OUT = os.path.join(REPO, "darkest", "data", "quirks.json")
REPORT = os.path.join(REPO, "reports", "dd1_quirks_source.md")


def main(argv):
    src = argv[argv.index("--source") + 1] if "--source" in argv else E_LIB
    check = "--check" in argv
    if not os.path.isfile(src):
        print("[m5] primary source missing: %s" % src)
        return 1

    with open(src, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    qs = data.get("quirks") or []
    ids = {q.get("id") for q in qs}

    pos = collections.Counter(bool(q.get("is_positive")) for q in qs)
    dis = collections.Counter(bool(q.get("is_disease")) for q in qs)
    cls = collections.Counter(q.get("classification") or "" for q in qs)

    edges = []
    for q in qs:
        for other in q.get("incompatible_quirks") or []:
            edges.append((q.get("id"), other))
    dangling = sorted({b for _a, b in edges if b not in ids})
    eset = set(edges)
    asym = sorted({tuple(sorted((a, b))) for a, b in edges if (b, a) not in eset})

    rows = []
    for q in qs:
        row = dict(q)
        row["origin"] = "dd1"
        rows.append(row)

    payload = {
        "_note": "阶段 A 对齐数据：由 tools/dsh/extract_dd1_quirks.py 从 E 盘【一手】shared/quirk/quirk_library.json 转写；"
                 "每条约 origin=dd1 ✓。协议：结构与合法性（含互斥）归 QuirksConfig；运行时状态归 Roster（沿用 Trinket 的处置）✓",
        "_source": src,
        "_field_classes": "id/classification/is_positive/is_disease = constraint+behavior（判定与分类）· "
                          "incompatible_quirks = constraint（**互斥必须可断言**）· buffs = behavior（按原语生效）· origin = doc ✓",
        "quirks": rows,
    }

    lines = [
        "# M5 Quirk 表：来源与实测（一手 E 盘）",
        "",
        "> 源：`%s`（**一手**）" % src,
        "> 契约（卡 M5）：schema = `is_positive`/`is_disease`/`classification`/`incompatible_quirks`/`curio_tag` · **库 170 条** · **互斥必须可断言** ✓",
        "",
        "## 实测（每次运行重测）",
        "",
        "- 条目数 = **%d**（契约定义：170）" % len(rows),
        "- 字段数 = **%d**：%s" % (len(rows[0]) if rows else 0,
                                   ", ".join("`%s`" % k for k in sorted(rows[0].keys())) if rows else "-"),
        "- `is_positive`：正 **%d** / 负 **%d**" % (pos.get(True, 0), pos.get(False, 0)),
        "- `is_disease`：疾病 **%d** / 非疾病 **%d**" % (dis.get(True, 0), dis.get(False, 0)),
        "- `classification`：%s" % ", ".join("`%s`=%d" % (k or "(空)", v) for k, v in cls.most_common()),
        "- 声明互斥的条目 = **%d** · 互斥边总数 = **%d**" % (len([q for q in qs if q.get("incompatible_quirks")]), len(edges)),
        "- 🔴 **悬空互斥引用**（指向不存在的 id）= **%d** %s" % (len(dangling), ("：" + ", ".join("`%s`" % d for d in dangling[:12])) if dangling else "✓"),
        "- 🔴 **非对称互斥**（`a→b` 但无 `b→a`）= **%d** 对 %s"
        % (len(asym), ("：例 " + ", ".join("`%s↔%s`" % (a, b) for a, b in asym[:6])) if asym else "✓（全对称）"),
        "",
        "## 互斥可断言的口径（给实现用）",
        "",
        "```",
        "① 引用完整性：`incompatible_quirks` 里每个 id 必须在库中（悬空即红）",
        "② 对称性：本次实测给出**真实结果**（上面那行）—— 若不为 0 ⇒ 校验**不能假设对称**，必须按【边】判",
        "③ 判定口径：互斥 = 存在边（单向即算）；`QuirksConfig.AreIncompatible(a, b)` 单点实现 ✓",
        "```",
    ]

    if check:
        print("[m5] check: quirks=%d fields=%d pos=%d/%d disease=%d edges=%d dangling=%d asym=%d"
              % (len(rows), len(rows[0]) if rows else 0, pos.get(True, 0), pos.get(False, 0),
                 dis.get(True, 0), len(edges), len(dangling), len(asym)))
        return 0

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=2)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[m5] quirks=%d fields=%d | pos=%d neg=%d | disease=%d | edges=%d | dangling=%d | asym=%d"
          % (len(rows), len(rows[0]) if rows else 0, pos.get(True, 0), pos.get(False, 0),
             dis.get(True, 0), len(edges), len(dangling), len(asym)))
    print("[m5] wrote %s" % os.path.relpath(OUT, REPO))
    print("[m5] wrote %s" % os.path.relpath(REPORT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
