#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_hero_upgrades.py -- M8 prep: measure the 15 hero upgrade trees from the primary E drive.

WHY THIS DOES NOT LAND DATA (yet)
    Card M8 declares the dependency out loud:  M8 depends on **M1 (属性) / M2 (buff primitive layer)**.
    Both are still in flight (M1a partially landed; M2 awaits the mapping ruling), so landing
    `units.json` / `skills.json` expansions now would create data whose consumers do not exist.
    => This tool only MEASURES and writes a report; it never touches darkest/data/.

WHAT IT MEASURES (primary E drive; the M4 lesson: never assume the third-party reference is primary)
    E:/SteamLibrary/steamapps/common/DarkestDungeon/upgrades/heroes/*.upgrades.json   (15 files)
      * the hero class list (which 15)
      * per hero: the trees (weapon / armour / skill_levels / ...), level counts
      * the extra field the building files did NOT have: `prerequisite_resolve_level`
        (a resolve-level gate -> if M8/UI needs it, it must be transcribed; recording it now)
      * currency types used (compare with the M6 building set)

OUTPUT
    reports/dd1_hero_upgrades_source.md + .json   (derived tables; not shipped content)
OUTPUT IS ASCII-ONLY (GBK console).
EXIT: 0 = ok, 1 = source missing / parse problem.
"""

import collections
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
E_DIR = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\upgrades\heroes"
REPORT_MD = os.path.join(REPO, "reports", "dd1_hero_upgrades_source.md")
REPORT_JSON = os.path.join(REPO, "reports", "dd1_hero_upgrades_source.json")


def main(argv):
    src_dir = argv[argv.index("--source") + 1] if "--source" in argv else E_DIR
    if not os.path.isdir(src_dir):
        print("[m8] primary source missing: %s" % src_dir)
        return 1

    files = sorted(f for f in os.listdir(src_dir) if f.endswith(".upgrades.json"))
    heroes = {}
    currencies = collections.Counter()
    resolve_gates = 0
    tree_kinds = collections.Counter()

    for fn in files:
        hero = fn[: -len(".upgrades.json")]
        with open(os.path.join(src_dir, fn), "r", encoding="utf-8") as fh:
            data = json.load(fh)

        trees = []
        for t in data.get("trees") or []:
            tid = t.get("id") or ""
            tree_kinds[tid.split(".")[-1] if "." in tid else tid] += 1
            levels = []
            for r in t.get("requirements") or []:
                for c in r.get("currency_cost") or []:
                    currencies[c.get("type")] += 1
                if "prerequisite_resolve_level" in r:
                    resolve_gates += 1
                levels.append({
                    "code": r.get("code"),
                    "currency_cost": r.get("currency_cost") or [],
                    "prerequisites": r.get("prerequisite_requirements") or [],
                    "prerequisite_resolve_level": r.get("prerequisite_resolve_level"),
                })
            trees.append({
                "id": tid,
                "is_instanced": bool(t.get("is_instanced")),
                "tags": list(t.get("tags") or []),
                "level_codes": [lv["code"] for lv in levels],
                "levels": levels,
            })
        heroes[hero] = {"trees": trees}

    total_trees = sum(len(h["trees"]) for h in heroes.values())
    total_levels = sum(len(t["levels"]) for h in heroes.values() for t in h["trees"])
    no_resolve = sorted(h for h, d in heroes.items()
                        if not any(lv.get("prerequisite_resolve_level") is not None
                                   for t in d["trees"] for lv in t["levels"]))

    payload = {
        "source": src_dir,
        "note": "REFERENCE MEASUREMENT ONLY (primary E drive). M8 depends on M1/M2 -> nothing is landed yet.",
        "hero_count": len(heroes),
        "heroes": heroes,
    }
    with open(REPORT_JSON, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False, indent=1)

    lines = [
        "# M8 职业对齐：一手结构测量（**只测量、不落库**）",
        "",
        "> 源：`%s`（**一手** · %d 个文件）" % (src_dir, len(files)),
        "> 🔴 **为什么不落库**：卡 M8 明写依赖 **M1（属性）/ M2（buff 原语层）** ⇒ 两者未到位时落库会产生",
        ">    **没有消费者的数据**（阶段 A 的纪律：先有消费点再落）✓",
        "",
        "## 实测",
        "",
        "- 职业数 = **%d**：%s" % (len(heroes), ", ".join("`%s`" % h for h in sorted(heroes))),
        "- 升级树 = **%d** 棵 · 等级条目 = **%d** 条" % (total_trees, total_levels),
        "- 树种类（按 id 后缀）：%s" % ", ".join("`%s`=%d" % (k, v) for k, v in tree_kinds.most_common()),
        "- `currency_cost` 资源类型：%s" % ", ".join("`%s`=%d" % (k, v) for k, v in currencies.most_common()),
        "- 🔴 **`prerequisite_resolve_level` 出现 %d 次** ⇒ 这是**建筑文件里没有**的字段（决心等级门槛）"
        % resolve_gates,
        "- 不含决心门槛的职业 = **%d** %s" % (len(no_resolve), ("：" + ", ".join("`%s`" % h for h in no_resolve)) if no_resolve else "✓"),
        "",
        "## 每职业一览（树 · 档位数）",
        "",
        "| 职业 | 树（档位数） |",
        "|---|---|",
    ]
    for hero in sorted(heroes):
        desc = " · ".join("%s(%d)" % (t["id"], len(t["levels"])) for t in heroes[hero]["trees"])
        lines.append("| `%s` | %s |" % (hero, desc))

    lines += [
        "",
        "## 落库前必须先解决（如实列出）",
        "",
        "```",
        "① 依赖 M1（属性：weapon/armour 5 阶已在 units.json，但 M1c 伤害模型未切）+ M2（buff 原语层未落）",
        "② 落库口径要定：15 职业是【全部落】还是像 M7 那样分批（卡说起点 crusader/vestal/plague_doctor）",
        "③ 我方 4 职业/36 技能要【保留】并标 origin:ours；原版 15 职业标 origin:dd1 ⇒ 命名冲突要先查",
        "④ `prerequisite_resolve_level` 是新字段：M6 的建筑形状没有它 ⇒ units.json/skills.json 的形状要一并定",
        "```",
    ]

    with open(REPORT_MD, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[m8] heroes=%d trees=%d levels=%d resolve_gates=%d currencies=%s"
          % (len(heroes), total_trees, total_levels, resolve_gates,
             ",".join(k for k, _ in currencies.most_common())))
    print("[m8] wrote %s" % os.path.relpath(REPORT_MD, REPO))
    print("[m8] wrote %s" % os.path.relpath(REPORT_JSON, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
