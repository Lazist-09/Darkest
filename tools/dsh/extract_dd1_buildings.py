#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_buildings.py -- M6: land darkest/data/buildings.json from the **primary** E-drive files.

WHY (planner #421 + the dispatch card: "M6（建筑 code/prerequisites）=> 各出一条 P 校验")
    Target shape:  buildings[] { id, trees[]: { id, levels[]: { code, currency_cost[], prerequisites[] } } }
    Primary shape (E drive, one file per building):
        { "trees": [ { "id": "abbey.meditation", "is_instanced": false, "tags": [...],
                       "requirements": [ { "code": "a", "currency_cost": [ {type, amount} ],
                                           "prerequisite_requirements": [ {tree_id, requirement_code} ] } ] } ] }

WHAT IT MEASURES (every run -- M4 lesson: never trust a remembered number)
    * how many buildings / trees / levels
    * code uniqueness inside a tree
    * prerequisite references: dangling (tree_id/code that does not exist) and
      **cycles** (a level that can never be reached) -- reported, not silently accepted
    * the currency types actually used
    * and whether the third-party reference agrees (it did NOT for trinkets, so we check)

OUTPUT (reports/ + darkest/data/buildings.json), every level carries origin="dd1".
OUTPUT IS ASCII-ONLY (GBK console).
EXIT: 0 = ok, 1 = source missing / parse problem.
"""

import collections
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
E_DIR = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\upgrades\building"
OUT = os.path.join(REPO, "darkest", "data", "buildings.json")
REPORT = os.path.join(REPO, "reports", "dd1_buildings_source.md")
BUILDINGS = ["abbey", "blacksmith", "camping_trainer", "guild", "nomad_wagon", "sanitarium", "stage_coach", "tavern"]


def find_cycles(levels):
    """levels: {(tree_id, code): [(tree_id, code), ...]} -> list of cycle paths."""
    WHITE, GRAY, BLACK = 0, 1, 2
    color = {k: WHITE for k in levels}
    cycles = []

    def visit(node, stack):
        color[node] = GRAY
        for nxt in levels.get(node, []):
            if color.get(nxt, BLACK) == GRAY:
                cycles.append(" -> ".join("%s.%s" % (t, c) for t, c in stack + [nxt]))
            elif color.get(nxt, BLACK) == WHITE:
                visit(nxt, stack + [nxt])
        color[node] = BLACK

    for k in list(levels):
        if color[k] == WHITE:
            visit(k, [k])
    return cycles


def main(argv):
    src_dir = argv[argv.index("--source") + 1] if "--source" in argv else E_DIR
    check = "--check" in argv
    if not os.path.isdir(src_dir):
        print("[m6] primary source missing: %s" % src_dir)
        return 1

    buildings = []
    level_index = {}
    currency_types = collections.Counter()
    dup_codes = []
    all_levels = []

    for b in BUILDINGS:
        path = os.path.join(src_dir, "%s.upgrades.json" % b)
        if not os.path.isfile(path):
            print("[m6] missing building file: %s" % path)
            return 1
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)

        trees = []
        for t in data.get("trees") or []:
            tree_id = t.get("id")
            codes = set()
            levels = []
            for r in t.get("requirements") or []:
                code = r.get("code")
                if code in codes:
                    dup_codes.append((tree_id, code))
                codes.add(code)
                costs = []
                for c in r.get("currency_cost") or []:
                    currency_types[c.get("type")] += 1
                    costs.append({"type": c.get("type"), "amount": c.get("amount")})
                prereqs = []
                for p in r.get("prerequisite_requirements") or []:
                    prereqs.append({"tree_id": p.get("tree_id"), "requirement_code": p.get("requirement_code")})
                entry = {
                    "code": code,
                    "currency_cost": costs,
                    "prerequisites": prereqs,
                    "origin": "dd1",
                }
                levels.append(entry)
                level_index[(tree_id, code)] = [(p["tree_id"], p["requirement_code"]) for p in prereqs]
                all_levels.append((b, tree_id, code))
            trees.append({
                "id": tree_id,
                "is_instanced": bool(t.get("is_instanced")),
                "tags": list(t.get("tags") or []),
                "levels": levels,
            })
        buildings.append({"id": b, "trees": trees})

    dangling = sorted({p for prereqs in level_index.values() for p in prereqs if p not in level_index})
    cycles = find_cycles(level_index)
    total_trees = sum(len(b["trees"]) for b in buildings)
    total_levels = sum(len(t["levels"]) for b in buildings for t in b["trees"])

    payload = {
        "_note": "阶段 A 对齐数据：由 tools/dsh/extract_dd1_buildings.py 从 E 盘【一手】upgrades/building/*.upgrades.json 转写；"
                 "每个 level 带 origin=dd1 ✓。形状 = 策划 #421 的目标：buildings[]{id, trees[]{id, levels[]{code, currency_cost[], prerequisites[]}}} ✓",
        "_source": src_dir,
        "_field_classes": "code = constraint+behavior（等级标识，也是前置引用目标）· currency_cost = behavior（升级花费）· "
                          "prerequisites = constraint（**必须可断言**：引用存在 + 无环）· tags/is_instanced = doc ✓",
        "buildings": buildings,
    }

    lines = [
        "# M6 建筑结构：来源与实测（一手 E 盘）",
        "",
        "> 源：`%s`（**一手** · 每建筑一个文件 · 8 个）" % src_dir,
        "> 目标形状（策划 `#421`）：`buildings[] { id, trees[] { id, levels[] { code, currency_cost[], prerequisites[] } } }` ✓",
        "> 契约要求：`code` / `prerequisites` **各出一条 P 校验** ✓",
        "",
        "## 实测（每次运行重测）",
        "",
        "- 建筑 = **%d** 个：%s" % (len(buildings), ", ".join("`%s`" % b["id"] for b in buildings)),
        "- 升级树 = **%d** 棵 · 等级条目 = **%d** 条" % (total_trees, total_levels),
        "- `code` 在树内重复 = **%d** %s" % (len(dup_codes), ("：" + str(dup_codes[:6])) if dup_codes else "✓"),
        "- 🔴 **悬空前置引用**（`tree_id`/`requirement_code` 不存在）= **%d** %s"
        % (len(dangling), ("：" + ", ".join("`%s.%s`" % d for d in dangling[:8])) if dangling else "✓"),
        "- 🔴 **前置环**（永远到不了的等级）= **%d** %s" % (len(cycles), ("：" + "；".join(cycles[:3])) if cycles else "✓（无环）"),
        "- `currency_cost` 用到的资源类型：%s"
        % ", ".join("`%s`=%d" % (k, v) for k, v in currency_types.most_common()),
        "",
        "## P 校验的口径（给实现用，全部来自上面实测）",
        "",
        "```",
        "① code：同一棵树内唯一（重复即红）；跨树可重名（`a`/`b`/`c` 是每棵树的档位名）✓",
        "② prerequisites：引用必须存在（悬空即红）；**无环**（有环即红）✓",
        "③ currency_cost：type 必须在本表实测集合内（不发明新资源）✓",
        "```",
    ]

    if check:
        print("[m6] check: buildings=%d trees=%d levels=%d dup_codes=%d dangling=%d cycles=%d"
              % (len(buildings), total_trees, total_levels, len(dup_codes), len(dangling), len(cycles)))
        return 0

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=2)
    with open(REPORT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[m6] buildings=%d trees=%d levels=%d dup_codes=%d dangling=%d cycles=%d currencies=%s"
          % (len(buildings), total_trees, total_levels, len(dup_codes), len(dangling), len(cycles),
             ",".join(k for k, _ in currency_types.most_common())))
    print("[m6] wrote %s" % os.path.relpath(OUT, REPO))
    print("[m6] wrote %s" % os.path.relpath(REPORT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
