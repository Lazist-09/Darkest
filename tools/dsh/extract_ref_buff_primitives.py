#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_buff_primitives.py -- A1: land darkest/data/buff_primitives.json from the LOCAL REFERENCE.

RULING (user directive, 2026-09-25, supersedes dd1_baseline 32.1 / 32.3)
    "Study the local Unity reference project's logic and numbers first, then adopt that
     project's values wholesale."  => the local reference is now the SOURCE OF TRUTH for
     numbers, not merely a third-party cross-check.  Conflicts that used to be listed as
     "conflicts" become a TO-CHANGE list.

WHY THIS FILE EXISTS
    Measured before this tool: darkest/data/*.json referenced 578 distinct buff-id strings
    (quirks 182 + trinkets 374 + our own inline 22) and the reference-side definitions did
    not exist in our repo at all => the buff reference chain was 100% dangling; a quirk or
    trinket effect could not resolve to anything.  This tool lands the primitive pool.

WHAT IT EMITS
    1. darkest/data/buff_primitives.json   -- the reference's JsonBuffs.json, re-typed 1:1
    2. reports/ref_buff_primitives_source.md -- every number RE-MEASURED on each run, plus
       the resolution table (which of OUR references resolve, and which do not)

FIELD CLASSES (planner #408 / data_schema 3.4: every field must be classified)
    behavior   : id / stat_type / stat_sub_type / amount / rule_type / is_false_rule /
                 rule_data.{float,string} / duration_type / duration
                 (they are what a modifier is computed from -- the primitive's whole point)
    to-wire    : remove_if_not_active -- TRUE in exactly 1 of 1801 entries.  We import it
                 faithfully and expose it, but NO consumer reads it yet.  It is NOT silently
                 dropped; the count of TRUE entries is asserted by the test so a change is loud.
    doc        : _note / _source / _ruling / _field_classes

USAGE
    python tools/dsh/extract_ref_buff_primitives.py              # write the data + the report
    python tools/dsh/extract_ref_buff_primitives.py --check      # measure only, write nothing
    python tools/dsh/extract_ref_buff_primitives.py --source <JsonBuffs.json>
OUTPUT IS ASCII-ONLY (GBK console; printing CJK kills the tool).
EXIT: 0 = ok, 1 = source missing / parse problem / measured number drifted from the ruling.
"""

import io
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF_ROOT = r"F:\GithubPro\Darkest-Dungeon-Unity"
REF_DATA = os.path.join(REF_ROOT, "Assets", "Resources", "Data")
DEFAULT_SRC = os.path.join(REF_DATA, "JsonBuffs.json")
OUT = os.path.join(REPO, "darkest", "data", "buff_primitives.json")
REPORT = os.path.join(REPO, "reports", "ref_buff_primitives_source.md")

FIELDS = ["id", "stat_type", "stat_sub_type", "amount", "remove_if_not_active",
          "rule_type", "is_false_rule", "rule_data", "duration_type", "duration"]
OPTIONAL = ("duration_type", "duration")

# Measured on the reference (2026-09-25) -- every one of these is re-derived below and a
# mismatch is a hard failure, so a silent upstream edit cannot pass unnoticed.
RULING = {
    "primitives": 1801,
    "distinct_stat_type": 25,
    "distinct_stat_combo": 41,
    "distinct_rule_type": 23,
    "with_duration": 63,
    "is_false_rule_true": 60,
    "remove_if_not_active_true": 1,
    "amount_zero": 6,
}


def load_json(path):
    """utf-8-sig aware load; the reference ships 7 files with trailing commas."""
    raw = io.open(path, encoding="utf-8-sig", errors="strict").read()
    try:
        return json.loads(raw)
    except ValueError:
        return json.loads(re.sub(r",(\s*[\]}])", r"\1", raw))


def ref_file(name):
    p = os.path.join(REF_DATA, name)
    return load_json(p) if os.path.isfile(p) else None


def own_buff_refs():
    """(file, owner_id, [buff id, ...]) for every string reference in our own tables."""
    out = []
    for name, root, idkey in (("quirks.json", "quirks", "id"), ("trinkets.json", "trinkets", "id")):
        data = load_json(os.path.join(REPO, "darkest", "data", name))
        for row in data.get(root, []):
            out.append((name, row.get(idkey), [b for b in (row.get("buffs") or []) if isinstance(b, str)]))
    return out


def ref_buff_refs():
    """owner_id -> reference buff list, for the two tables we mirror."""
    by = {}
    for name, root, idkey in (("JsonQuirks.json", "quirks", "id"), ("JsonTrinkets.json", "trinkets", "id")):
        data = ref_file(name)
        if not isinstance(data, dict):
            continue
        for row in data.get(root, []):
            by.setdefault(name, {})[row.get(idkey)] = [b for b in (row.get("buffs") or []) if isinstance(b, str)]
    return by


def main(argv):
    src = argv[argv.index("--source") + 1] if "--source" in argv else DEFAULT_SRC
    check = "--check" in argv
    if not os.path.isfile(src):
        print("[a1] reference source missing: %s" % src)
        return 1
    try:
        raw = load_json(src)
    except Exception as ex:  # noqa: BLE001 - report and exit, never traceback on a data problem
        print("[a1] parse problem in %s: %s" % (src, ex))
        return 1

    prims = raw["primitives"] if isinstance(raw, dict) and "primitives" in raw else raw["buffs"]
    rows = []
    for p in prims:
        row = {}
        for f in FIELDS:
            row[f] = p.get(f)
        row["rule_data"] = {"float": p["rule_data"].get("float", 0), "string": p["rule_data"].get("string", "")}
        rows.append(row)

    ids = [r["id"] for r in rows]
    measured = {
        "primitives": len(rows),
        "distinct_stat_type": len({r["stat_type"] for r in rows}),
        "distinct_stat_combo": len({(r["stat_type"], r["stat_sub_type"]) for r in rows}),
        "distinct_rule_type": len({r["rule_type"] for r in rows}),
        "with_duration": sum(1 for r in rows if r["duration_type"] is not None),
        "is_false_rule_true": sum(1 for r in rows if r["is_false_rule"]),
        "remove_if_not_active_true": sum(1 for r in rows if r["remove_if_not_active"]),
        "amount_zero": sum(1 for r in rows if r["amount"] == 0),
    }
    drift = {k: (RULING[k], v) for k, v in measured.items() if RULING[k] != v}
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    both = [r for r in rows if (r["duration_type"] is None) != (r["duration"] is None)]

    known = set(ids)
    refs = own_buff_refs()
    flat = sorted({b for _f, _o, bs in refs for b in bs})
    unresolved = [b for b in flat if b not in known]
    refby = ref_buff_refs()
    renamed, uncovered = [], []
    for b in unresolved:
        owners = [(f, o) for f, o, bs in refs if b in bs]
        cand = []
        for f, o in owners:
            key = "JsonQuirks.json" if f == "quirks.json" else "JsonTrinkets.json"
            if isinstance(refby.get(key), dict) and o in refby[key] and refby[key][o] != [b]:
                cand.append((o, refby[key][o]))
        (renamed if cand else uncovered).append((b, owners, cand))

    lines = [
        "# buff 原语层：来源与实测（由提取器生成 · 参考项目）",
        "",
        "> 源：`%s`（**本地参考项目** —— 用户指令 2026-09-25：数值采用该项目的来源）" % src,
        "> 用途：`darkest/data/*.json` 里 578 个 buff 引用**此前一个都解析不到**；本层给出定义池 ✓",
        "> 解析器：`darkest/scripts/data/BuffPrimitivesConfig.cs`（零 Godot）· 用语料：`BuffPrimitiveTranslation.cs`",
        "",
        "## 实测（每次运行重新测，不信任历史数字）",
        "",
        "| 读数 | 实测 | 指令定义 | 一致 |",
        "|---|---|---|---|",
    ]
    for k in RULING:
        lines.append("| %s | **%d** | %d | %s |"
                     % (k, measured[k], RULING[k], "OK" if RULING[k] == measured[k] else "DRIFT"))
    lines += [
        "- 重复 id = **%d**（要求 0）· duration_type 与 duration 不同时出现/缺失的条目 = **%d**（要求 0）"
        % (len(dupes), len(both)),
        "",
        "## 词表（实测闭集 —— 解析器按它 fail-fast）",
        "",
        "### `stat_type` ×%d" % measured["distinct_stat_type"],
        "",
    ]
    st = {}
    for r in rows:
        st[r["stat_type"]] = st.get(r["stat_type"], 0) + 1
    lines.append("| stat_type | 条数 |")
    lines.append("|---|---|")
    for k in sorted(st, key=lambda x: (-st[x], x)):
        lines.append("| `%s` | %d |" % (k, st[k]))
    rt = {}
    for r in rows:
        rt[r["rule_type"]] = rt.get(r["rule_type"], 0) + 1
    lines += ["", "### `rule_type` ×%d" % measured["distinct_rule_type"], "", "| rule_type | 条数 |", "|---|---|"]
    for k in sorted(rt, key=lambda x: (-rt[x], x)):
        lines.append("| `%s` | %d |" % (k, rt[k]))
    lines += ["", "### `duration_type`（仅 %d 条有）" % measured["with_duration"], "", "| duration_type | duration | 条数 |",
              "|---|---|---|"]
    dd = {}
    for r in rows:
        if r["duration_type"] is not None:
            dd[(r["duration_type"], r["duration"])] = dd.get((r["duration_type"], r["duration"]), 0) + 1
    for (t, n) in sorted(dd, key=lambda x: (-dd[x], str(x))):
        lines.append("| `%s` | %d | %d |" % (t, n, dd[(t, n)]))
    lines += [
        "",
        "## 我方引用的解析率（实测）",
        "",
        "- 我方 `quirks.json` + `trinkets.json` 引用的**去重 buff id** = **%d**" % len(flat),
        "- 在参考项目定义池里能找到 = **%d**" % (len(flat) - len(unresolved)),
        "- 找不到 = **%d** ⇒ 分成两类（下表逐条列出，**不静默跳过**）" % len(unresolved),
        "  - **参考项目已改名**（同一 quirk/trinket 在参考项目里有另一套 buff 列表）= **%d**" % len(renamed),
        "  - **参考项目未覆盖**（我方有、参考项目连条目都没有）= **%d**" % len(uncovered),
        "",
    ]
    if renamed:
        lines += ["### 参考项目已改名（A7 待改清单 —— 按指令应采用参考项目的列表）", "",
                  "| 我方引用 | 属主 | 参考项目的列表 |", "|---|---|---|"]
        for b, owners, cand in sorted(renamed):
            ow = " · ".join("%s:%s" % (f, o) for f, o in owners[:3])
            cc = " · ".join("%s ⇒ %s" % (o, ", ".join("`%s`" % x for x in bs) or "(空)") for o, bs in cand[:2])
            lines.append("| `%s` | %s | %s |" % (b, ow, cc))
        lines.append("")
    if uncovered:
        lines += ["### 参考项目未覆盖（需策划裁：保留我方 / 删除）", "",
                  "| 我方引用 | 属主 |", "|---|---|"]
        for b, owners, _c in sorted(uncovered):
            lines.append("| `%s` | %s |" % (b, " · ".join("%s:%s" % (f, o) for f, o in owners[:3])))
        lines.append("")

    payload = {
        "_note": "阶段 A 对齐数据（A1 · buff 原语层）：由 tools/dsh/extract_ref_buff_primitives.py 从"
                 "【本地参考项目】JsonBuffs.json 逐字段转写 ⇒ %d 条。此前我方 578 个 buff 引用全部解析不到 ✓"
                 % measured["primitives"],
        "_source": src,
        "_ruling": "用户指令 2026-09-25：不论逻辑/数值，先学本地参考项目，数值采用该项目的来源 "
                   "（顶替 dd1_baseline 32.1/32.3 的一手优先；冲突表转为待改清单）✓",
        "_field_classes": "behavior = id/stat_type/stat_sub_type/amount/rule_type/is_false_rule/"
                          "rule_data.{float,string}/duration_type/duration（modifier 就是由它们算出的）· "
                          "to-wire = remove_if_not_active（1801 条里只有 1 条 true；已如实导入并暴露，暂无消费点 ⇒ "
                          "用例把 true 的条数钉住，改了就红）· doc = _note/_source/_ruling/_field_classes ✓",
        "primitives": rows,
    }

    if check:
        print("[a1] check: primitives=%d stat_type=%d combos=%d rule_type=%d with_duration=%d "
              "false_rule=%d remove_if_not_active=%d amount_zero=%d dupes=%d duration_pairs=%d "
              "own_refs=%d resolved=%d renamed=%d uncovered=%d"
              % (measured["primitives"], measured["distinct_stat_type"], measured["distinct_stat_combo"],
                 measured["distinct_rule_type"], measured["with_duration"], measured["is_false_rule_true"],
                 measured["remove_if_not_active_true"], measured["amount_zero"], len(dupes), len(both),
                 len(flat), len(flat) - len(unresolved), len(renamed), len(uncovered)))
        if drift:
            print("[a1] DRIFT: %s" % drift)
            return 1
        return 0

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[a1] primitives=%d stat_type=%d combos=%d rule_type=%d with_duration=%d false_rule=%d "
          "remove_if_not_active=%d amount_zero=%d dupes=%d duration_pairs=%d"
          % (measured["primitives"], measured["distinct_stat_type"], measured["distinct_stat_combo"],
             measured["distinct_rule_type"], measured["with_duration"], measured["is_false_rule_true"],
             measured["remove_if_not_active_true"], measured["amount_zero"], len(dupes), len(both)))
    print("[a1] own refs=%d -> resolved=%d / renamed=%d / uncovered=%d"
          % (len(flat), len(flat) - len(unresolved), len(renamed), len(uncovered)))
    print("[a1] wrote %s" % os.path.relpath(OUT, REPO))
    print("[a1] wrote %s" % os.path.relpath(REPORT, REPO))
    return 1 if drift else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
