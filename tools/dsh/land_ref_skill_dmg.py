#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""land_ref_skill_dmg.py -- A2: land `dmg_pct` into darkest/data/skills.json FROM THE LOCAL REFERENCE.

RULING (user directive, 2026-09-25)
    "Study the local Unity reference project's logic and numbers first, then adopt that
     project's values wholesale."  => the local reference is the SOURCE OF TRUTH for numbers.
    This supersedes the older "primary E-drive first" order (dd1_baseline 32.1 / 32.3).

INPUT  reports/unity_ref/skill_dmg_mapping.json   (A2 study task: 44 rows, one per shipped skill)
       reports/unity_ref/hero_skills_from_ref.json (the reference's own 15 x 7 skills, per level)
       darkest/data/skills.json                    (what we ship today)

LANDING RULE
  For every shipped skill we ask: *what is the reference's MEASURED `.dmg` for the reference skill
  this row claims?*  There are two ways a row can claim one, and they are treated differently:

  (a) the study task rated the row 【明确】 AND gave an integer `ref_dmg_pct`
      => LAND that value; `_dmg_pct_source` becomes `ref:<Hero>/<skill> (<evidence>)`.
      This is a NEW claim, so it must clear the stricter bar.

  (b) the row is 【候选】/【无对应】 BUT the skill ALREADY ships a `dmg_pct` whose own
      `_dmg_pct_source` note NAMES a reference skill (e.g. `dd1:barbaric_yawp (语义同(战吼))`)
      => the CLAIM was already made and recorded (by the planner's section-43 table), so we do not
      re-litigate the mapping here.  We only check its NUMBER:
          stored == measured `.dmg` of the named reference skill  => KEEP (untouched)
          stored != measured                                       => CORRECT to the measured value
          named skill cannot be resolved                           => ERROR (refuse to write)
      WHY: the directive is about *numbers*.  Withdrawing a number that the reference confirms
      would drop correct data, and would make the 候选/明确 word -- which is about how sure we are
      of the MAPPING, not about the value -- silently decide a value.  A wrong number is
      corrected; a right number stays.
  (c) anything else => no `dmg_pct`.  A stored value with no resolvable claim IS withdrawn
      (planner #472(1): a value I guessed must not sit in darkest/data).

  Not decided here: the mapping itself for the 30 rows that neither (a) nor (b) can settle, and the
  reference's convention that a no-damage skill carries `.dmg -100%` (measured: 11/105 reference
  skills are -100 and each is a pure-utility skill).  Both go to the planner as a per-row request;
  the tool only REPORTS them.

WHY THIS IS STILL ZERO-BEHAVIOUR
    The damage path does not read `dmg_pct` yet (that is P7 / M1c stage 3).  Landing values changes
    no number in any run; it changes the data table only.  `M1cStage3MechanismTests` pins the stored
    count, so a count change is loud.

USAGE
    python tools/dsh/land_ref_skill_dmg.py --check     # measure only, write nothing
    python tools/dsh/land_ref_skill_dmg.py             # write darkest/data/skills.json + report
OUTPUT IS ASCII-ONLY (GBK console; printing CJK kills the tool).
EXIT: 0 = ok, 1 = input missing / schema problem / ids disagree / a named claim is unresolvable.
"""

import io
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MAPPING = os.path.join(REPO, "reports", "unity_ref", "skill_dmg_mapping.json")
REFHERO = os.path.join(REPO, "reports", "unity_ref", "hero_skills_from_ref.json")
SKILLS = os.path.join(REPO, "darkest", "data", "skills.json")
REPORT = os.path.join(REPO, "reports", "ref_skill_dmg_source.md")

TIER_OK = "明确"
TIERS = ("明确", "候选", "无对应")
# A stored note CLAIMS a reference skill in one of two shapes:
#   `dd1:<skill> (<why>)`        -- written by the planner's section-43 table (skill id only)
#   `ref:<Hero>/<skill> (<why>)` -- written by THIS tool
# Both must parse, otherwise the tool is not idempotent: after its own first run the note no longer
# starts with `dd1:` and the claim would look unresolvable, so the second run would withdraw the very
# values the first run just verified.  (Measured: that is exactly what happened -- 14 -> 12.)
SOURCE_RE = re.compile(r"^\s*(?:dd1|ref):(?:([A-Za-z][A-Za-z_]*)/)?([a-z_0-9]+)")


def load(path):
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def as_pct_int(value):
    """`-40` / `"-40"` / `"-40%"` => -40; anything else => None (defensive: the study task was told
    to emit an integer, but a percent-suffixed string must not silently become 0)."""
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, float) and value == int(value):
        return int(value)
    if isinstance(value, str):
        try:
            return int(value.strip().rstrip("%").strip())
        except ValueError:
            return None
    return None


def short(text, limit=48):
    """`_dmg_pct_source` must stay short -- a one-line provenance note, not a paragraph."""
    t = re.sub(r"\s+", " ", text or "effect 全串一致").strip()
    return t if len(t) <= limit else t[:limit] + "\u2026"


def ref_dmg_index(refhero):
    """reference skill id -> {hero: .dmg at level 0}.  Skill ids are shared across heroes only for
    the generic `move`, so look-ups must tolerate a repeated id."""
    index = {}
    for hero, v in refhero.items():
        for rec in v.get("stat_records", []):
            if str(rec.get("level")) != "0":
                continue
            index.setdefault(rec["id"], {})[hero] = as_pct_int(rec.get("dmg"))
    return index


def resolve(index, named, prefer_hero):
    """-> (value, hero) for a named reference skill; prefers `prefer_hero` when the id repeats."""
    hits = index.get(named)
    if not hits:
        return None, None
    for want in (prefer_hero, None):
        if isinstance(want, str) and want in hits:
            return hits[want], want
    if len(hits) == 1:
        hero = next(iter(hits))
        return hits[hero], hero
    return None, None


def seg_sum(skill):
    """Sum of our own `damage.segments[].multiplier` -- 0 means 'this skill deals no weapon damage'."""
    total = 0.0
    for seg in (skill.get("damage") or {}).get("segments") or []:
        try:
            total += float(seg.get("multiplier", 0))
        except (TypeError, ValueError):
            pass
    return total


def note_text(with_pct, total, clear_landed, kept_n, corrected_n, asked_n, withdrawn_n, errors_n):
    return (
        "A2 \u9876\u66ff:\u6280\u80fd dmg%% \u7684\u6765\u6e90=\u3010\u672c\u5730\u53c2\u8003\u9879\u76ee\u3011"
        "`Heroes/Info/*.bytes` \u7684 `.dmg` \u5b57\u6bb5(\u9010\u6280\u80fd 5 \u7ea7\u6052\u5b9a\u4e0d\u53d8 "
        "\u21d2 \u4e00\u4e2a\u6280\u80fd\u4e00\u4e2a\u503c)\u3002\u5b9e\u6d4b %d \u6761\u6280\u80fd\u91cc"
        "\u3010%d \u6761\u5df2\u586b / %d \u6761\u672a\u586b\u3011\u3002"
        "\u843d\u5e93\u53e3\u5f84\u4e24\u6761:(\u4e00)\u6620\u5c04\u4e3a\u3010\u660e\u786e\u3011\u4e14"
        "\u6709\u6574\u6570\u503c \u21d2 \u843d;\u5171 %d \u6761\u3002"
        "(\u4e8c)\u3010\u5019\u9009\u3011/\u3010\u65e0\u5bf9\u5e94\u3011\u4f46\u5e93\u91cc\u5df2\u6709\u503c"
        "\u4e14\u5176 `_dmg_pct_source` \u81ea\u5df1\u70b9\u540d\u4e86\u53c2\u8003\u6280\u80fd "
        "\u21d2 \u53ea\u6838\u6570\u503c:\u76f8\u7b49\u5219**\u4fdd\u7559**(\u5171 %d \u6761)"
        "\u3001\u4e0d\u7b49\u5219**\u7ea0\u6b63**(\u5171 %d \u6761)\u3002"
        "\u5269 %d \u6761\u65e0\u53ef\u843d\u4e4b\u503c(\u6620\u5c04\u5f85\u7b56\u5212\u9010\u884c\u88c1\u5b9a)"
        "\u21d2 \u9010\u884c\u8bf7\u6c42\u89c1 reports/ref_skill_dmg_source.md\u3002"
        "\u672c\u8f6e\u64a4\u51fa\u65e0\u4f9d\u636e\u65e7\u503c %d \u6761\u3001\u65e0\u6cd5\u89e3\u6790\u7684"
        "\u58f0\u660e %d \u6761\u3002"
        "\U0001f534 \u4f24\u5bb3\u8def\u5f84\u5c1a\u672a\u8bfb dmg_pct \u21d2 \u672c\u6279**\u96f6\u884c\u4e3a** "
        "\u2713"
        % (total, with_pct, total - with_pct, clear_landed, kept_n, corrected_n,
           total - with_pct, withdrawn_n, errors_n)
    )


def main(argv):
    check = "--check" in argv
    for p in (MAPPING, REFHERO, SKILLS):
        if not os.path.isfile(p):
            print("[a2] missing input: %s" % p)
            return 1

    mapping = load(MAPPING)
    rows = mapping.get("rows")
    if not isinstance(rows, list) or not rows:
        print("[a2] skill_dmg_mapping.json has no `rows` list")
        return 1
    refhero = load(REFHERO)
    shipped = load(SKILLS)
    skills = shipped.get("skills")
    if not isinstance(skills, list) or not skills:
        print("[a2] skills.json has no `skills` list")
        return 1

    ids = [s.get("id") for s in skills]
    row_ids = [r.get("our_id") for r in rows]
    if len(set(row_ids)) != len(row_ids):
        print("[a2] mapping has duplicate our_id")
        return 1
    if set(ids) != set(row_ids):
        print("[a2] mapping and skills.json disagree: only_in_mapping=%s only_in_data=%s"
              % (sorted(set(row_ids) - set(ids)), sorted(set(ids) - set(row_ids))))
        return 1

    index = ref_dmg_index(refhero)
    by_id = {r["our_id"]: r for r in rows}
    counts = {t: 0 for t in TIERS}
    bad_tier, errors = [], []
    land, kept, corrected, withdrawn, asked = [], [], [], [], []

    for s in skills:
        sid = s["id"]
        r = by_id[sid]
        tier = r.get("confidence")
        if tier not in TIERS:
            bad_tier.append((sid, tier))
            continue
        counts[tier] += 1
        stored = as_pct_int(s.get("dmg_pct"))
        claimed = as_pct_int(r.get("ref_dmg_pct"))

        # (a) the study task's own, stricter claim
        if tier == TIER_OK and claimed is not None:
            land.append((sid, claimed, stored, r.get("ref_hero"), r.get("ref_skill"),
                         "ref:%s/%s (%s)" % (r.get("ref_hero"), r.get("ref_skill"),
                                             short(r.get("evidence")))))
            continue

        # (b) a claim our own shipped note already named
        named = SOURCE_RE.match(str(s.get("_dmg_pct_source") or ""))
        if stored is not None and named:
            named_id = named.group(2)
            measured, hero = resolve(index, named_id, named.group(1) or r.get("ref_hero"))
            if measured is None:
                errors.append((sid, named_id, stored))
                continue
            if measured == stored:
                kept.append((sid, stored, named_id, hero))
            else:
                corrected.append((sid, stored, measured, named_id, hero))
                land.append((sid, measured, stored, hero, named_id,
                             "ref:%s/%s (\u7ea0\u6b63:\u65e7 %d)" % (hero, named_id, stored)))
            continue

        # (c) nothing to land
        if stored is not None:
            withdrawn.append((sid, stored, tier))
        asked.append((sid, tier, r, stored))

    if bad_tier:
        print("[a2] unknown confidence values: %s" % bad_tier)
        return 1
    if errors:
        print("[a2] REFUSING TO WRITE -- stored value whose own note names an unresolvable "
              "reference skill:")
        for sid, named_id, stored in errors:
            print("[a2]   %s: note says `%s`, stored %d, but no reference hero defines that id"
                  % (sid, named_id, stored))
        return 1

    before = sum(1 for s in skills if as_pct_int(s.get("dmg_pct")) is not None)
    after = len(land) + len(kept)

    print("[a2] rows=%d clear=%d candidate=%d none=%d" % (len(rows), counts["明确"], counts["候选"], counts["无对应"]))
    print("[a2] with dmg_pct: before=%d -> after=%d" % (before, after))
    print("[a2]   landed-by-(a)=%d  kept-by-(b)=%d  corrected-by-(b)=%d  withdrawn=%d  asked=%d"
          % (len(land) - len(corrected), len(kept), len(corrected), len(withdrawn), len(asked)))
    if corrected:
        print("[a2]   corrections: " + ", ".join("%s %d->%d (%s)" % (sid, old, new, n)
                                                 for sid, old, new, n, _ in corrected))
    if check:
        return 0

    # ---- write skills.json (same shape/indent as before; only the dmg_pct pair moves) ----
    landed = {x[0]: (x[1], x[5]) for x in land}
    # (b)-kept rows are NOT re-derived: they keep the value AND the provenance note they already
    # ship, because their note (a `dd1:<skill>` claim, i.e. the planner's section-43 mapping) is
    # the thing this tool just verified -- rewriting it would erase the record of what was checked.
    for sid, val, _named_id, _hero in kept:
        landed[sid] = (val, next(s.get("_dmg_pct_source") for s in skills if s["id"] == sid))
    new_skills = []
    for s in skills:
        sid = s["id"]
        head = {"id": sid}
        if sid in landed:
            val, note = landed[sid]
            head["dmg_pct"] = val
            head["_dmg_pct_source"] = note
        for k, v in s.items():
            if k in ("id", "dmg_pct", "_dmg_pct_source"):
                continue
            head[k] = v
        new_skills.append(head)

    out = {
        "_dmg_pct_note": note_text(after, len(skills), len(land) - len(corrected), len(kept),
                                   len(corrected), len(asked), len(withdrawn), len(errors)),
        "skills": new_skills,
    }
    with io.open(SKILLS, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(out, ensure_ascii=False, indent=2) + "\n")

    ref_src = "F:\\GithubPro\\Darkest-Dungeon-Unity\\Assets\\Resources\\Data\\Heroes\\Info\\*.bytes"
    lines = [
        "# \u6280\u80fd `dmg_pct`\uff1a\u6765\u6e90\u4e0e\u5b9e\u6d4b"
        "\uff08\u7531 `tools/dsh/land_ref_skill_dmg.py` \u751f\u6210 \u00b7 **\u672c\u5730\u53c2\u8003\u9879\u76ee**\uff09",
        "",
        "> \u6e90\uff1a`%s`\uff08**\u672c\u5730\u53c2\u8003\u9879\u76ee** \u2014\u2014 \u7528\u6237\u6307\u4ee4 "
        "2026-09-25\uff1a\u6570\u503c\u91c7\u7528\u8be5\u9879\u76ee\u7684\u6765\u6e90\uff09" % ref_src,
        "> \u6620\u5c04\u8868\uff1a`reports/unity_ref/skill_dmg_mapping.json`"
        "\uff08\u9010\u6761\u5e26 `effect` \u5168\u4e32\u4f9d\u636e\uff09\u00b7 \u53c2\u8003\u6280\u80fd\u5b9e\u6d4b`"
        "`\uff1a`reports/unity_ref/hero_skills_from_ref.json`",
        "> \u843d\u5e93\u53e3\u5f84\uff1a(\u4e00)\u3010\u660e\u786e\u3011+\u6574\u6570\u503c \u21d2 \u843d\uff1b"
        "(\u4e8c)\u5df2\u6709\u503c\u4e14**\u81ea\u5df1\u7684\u6ce8\u8bb0\u70b9\u540d\u4e86\u53c2\u8003\u6280\u80fd**"
        " \u21d2 \u53ea\u6838\u6570\u503c\uff08\u76f8\u7b49\u4fdd\u7559 / \u4e0d\u7b49\u7ea0\u6b63\uff09\uff1b"
        "\u5176\u4f59\u4e0d\u843d",
        "",
        "## \u5b9e\u6d4b\uff08\u6bcf\u6b21\u8fd0\u884c\u91cd\u65b0\u6d4b\uff09",
        "",
        "| \u8bfb\u6570 | \u503c |",
        "|---|---|",
        "| \u6280\u80fd\u603b\u6570 | **%d** |" % len(skills),
        "| \u6620\u5c04\u3010\u660e\u786e\u3011 | **%d** |" % counts["明确"],
        "| \u6620\u5c04\u3010\u5019\u9009\u3011 | **%d** |" % counts["候选"],
        "| \u6620\u5c04\u3010\u65e0\u5bf9\u5e94\u3011 | **%d** |" % counts["无对应"],
        "| \u843d\u5e93\u524d\u5e26 `dmg_pct` | **%d** |" % before,
        "| \u843d\u5e93\u540e\u5e26 `dmg_pct` | **%d** |" % after,
        "| \u5176\u4e2d\u7531 (a) \u843d\u5e93 | **%d** |" % (len(land) - len(corrected)),
        "| \u5176\u4e2d\u7531 (b) **\u4fdd\u7559**\uff08\u6570\u503c\u7ecf\u53c2\u8003\u6838\u5bf9\u76f8\u7b49\uff09"
        " | **%d** |" % len(kept),
        "| \u5176\u4e2d\u7531 (b) **\u7ea0\u6b63** | **%d** |" % len(corrected),
        "| **\u64a4\u51fa**\uff08\u65e0\u53ef\u89e3\u6790\u58f0\u660e\uff09 | **%d** |" % len(withdrawn),
        "| \u672a\u843d\u3001\u5f85\u7b56\u5212\u88c1\u5b9a | **%d** |" % len(asked),
        "",
        "## \u9010\u6761\u7ed3\u679c",
        "",
        "| \u6211\u65b9\u6280\u80fd | \u53c2\u8003\u82f1\u96c4 | \u53c2\u8003\u6280\u80fd | `dmg_pct` | \u843d\u5e93\u524d | \u5224\u5b9a |",
        "|---|---|---|---:|---:|---|",
    ]
    kept_set = {x[0] for x in kept}
    corr_set = {x[0] for x in corrected}
    for s in skills:
        sid = s["id"]
        r = by_id[sid]
        val = landed[sid][0] if sid in landed else None
        old = as_pct_int(s.get("dmg_pct"))
        if sid in corr_set:
            verdict = "**\u7ea0\u6b63**"
        elif sid in kept_set:
            verdict = "\u4fdd\u7559\uff08\u53c2\u8003\u6838\u5bf9\u76f8\u7b49\uff09"
        elif val is not None:
            verdict = "**\u843d\u5e93**"
        elif old is not None:
            verdict = "**\u64a4\u51fa**"
        else:
            verdict = "\u4e0d\u843d\uff08%s\uff09" % r.get("confidence")
        lines.append("| `%s` | %s | %s | %s | %s | %s |"
                     % (sid, r.get("ref_hero") or "-", r.get("ref_skill") or "-",
                        "**%d**" % val if val is not None else "\u2014",
                        "\u2014" if old is None else str(old), verdict))

    lines += ["", "## \u7ea0\u6b63\u6e05\u5355\uff08\u65e7\u503c\u4e0e\u53c2\u8003\u9879\u76ee\u5b9e\u6d4b\u4e0d\u7b26"
              " \u21d2 \u6309\u65b0\u6765\u6e90\u7ea0\u6b63\uff09", ""]
    if corrected:
        lines += ["| \u6280\u80fd | \u65e7\u503c | \u53c2\u8003\u5b9e\u6d4b | \u58f0\u660e\u70b9\u540d\u7684\u53c2\u8003\u6280\u80fd |",
                  "|---|---:|---:|---|"]
        for sid, old, new, named_id, hero in corrected:
            lines.append("| `%s` | %d | **%d** | `%s/%s` |" % (sid, old, new, hero, named_id))
    else:
        lines.append("\uff08\u65e0\uff09")

    lines += ["", "## \u4fdd\u7559\u6e05\u5355\uff08\u5e93\u91cc\u5df2\u6709\u503c\u3001\u4e14\u7b49\u4e8e"
              "\u81ea\u5df1\u6ce8\u8bb0\u6240\u70b9\u540d\u53c2\u8003\u6280\u80fd\u7684\u5b9e\u6d4b `.dmg`\uff09", ""]
    if kept:
        lines += ["| \u6280\u80fd | \u503c | \u58f0\u660e\u70b9\u540d\u7684\u53c2\u8003\u6280\u80fd |",
                  "|---|---:|---|"]
        for sid, val, named_id, hero in kept:
            lines.append("| `%s` | %d | `%s/%s` |" % (sid, val, hero, named_id))
    else:
        lines.append("\uff08\u65e0\uff09")

    lines += ["", "## \u64a4\u51fa\u6e05\u5355", ""]
    if withdrawn:
        lines += ["| \u6280\u80fd | \u65e7\u503c | \u6620\u5c04\u5224\u5b9a |", "|---|---:|---|"]
        for sid, old, tier in withdrawn:
            lines.append("| `%s` | %d | %s |" % (sid, old, tier))
    else:
        lines.append("\uff08\u65e0\uff09")

    lines += ["", "## \u5f85\u7b56\u5212\u9010\u884c\u88c1\u5b9a\uff08\u672c\u8f6e**\u672a\u843d\u5e93**\uff09", "",
              "\u53c2\u8003\u9879\u76ee\u5bf9\"\u7eaf\u529f\u80fd/\u65e0\u4f24\u5bb3\"\u6280\u80fd\u7684\u5199\u6cd5\u662f "
              "`.dmg -100%`"
              "\uff08\u5b9e\u6d4b\uff1a15 \u82f1\u96c4 \u00d7 7 \u6280\u80fd\u91cc **11** \u6761\u662f -100\uff0c"
              "\u4e14\u6bcf\u6761\u90fd\u662f\u7eaf\u529f\u80fd\uff09\uff1b"
              "\u666e\u901a\u4f24\u5bb3\u6280\u80fd\u5199 `.dmg 0%`\u3002\u4e0b\u8868 `\u03a3\u6bb5\u500d\u7387` "
              "\u662f\u6211\u4eec\u81ea\u5df1\u7684\u4f24\u5bb3\u5f62\u72b6\uff08\u6765\u81ea `damage.segments`\uff09"
              "\uff0c`\u5efa\u8bae\u503c` \u662f\u6309\u4e0a\u8ff0\u7ea6\u5b9a**\u63a8**\u7684\uff0c\u4e0d\u662f\u5b9e\u6d4b\u503c\u3002",
              "",
              "| \u6211\u65b9\u6280\u80fd | \u6b66\u5668\u6bb5\u500d\u7387 \u03a3 | \u5f53\u524d\u6620\u5c04\u5224\u5b9a | "
              "\u53c2\u8003\u5019\u9009 | \u5efa\u8bae\u503c |", "|---|---:|---|---|---:|"]
    for sid, tier, r, stored in asked:
        cand = "%s/%s=%s" % (r.get("ref_hero") or "-", r.get("ref_skill") or "-",
                             r.get("ref_dmg_pct")) if r.get("ref_skill") else "\u2014"
        tot = None
        for s in skills:
            if s["id"] == sid:
                tot = seg_sum(s)
        prop = -100 if (tot is not None and abs(tot) < 1e-9) else 0
        lines.append("| `%s` | %s | %s | %s | **%d** |"
                     % (sid, ("%g" % tot) if tot is not None else "?", tier, cand, prop))

    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[a2] wrote %s" % os.path.relpath(SKILLS, REPO))
    print("[a2] wrote %s" % os.path.relpath(REPORT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
