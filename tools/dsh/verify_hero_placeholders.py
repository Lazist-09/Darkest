#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_hero_placeholders.py -- packaging check for the local placeholder hero packs.

SCOPE (read this before adding rules here)
    This is a PACKAGING check only:
      * every required action slot has a file on disk
      * every framed slot declares an anchor (P31 rule ③ at the JSON level)
      * `placeholder: true` + a non-empty `source` (compliance section 4 of hero_assets.md)
      * `portrait` resolves, and the descriptor parses
    🔴 The BEHAVIOURAL rules (slot vocabulary, resolution, root, missing_reason) live in ONE place:
       `darkest/scripts/data/HeroAssets.cs` (+ HeroArtResolver). Do NOT re-implement them here --
       a second implementation would be a "two sources of truth" violation.

WHY IT EXISTS
    The placeholder packs are gitignored (they contain borrowed mod art, hero_assets.md section 4),
    so a normal test cannot depend on them. This runner keeps the local packs honest and prints
    the readout the task card asks for ("4 archetypes, slots / frames / anchors / source").

USAGE
    python tools/dsh/verify_hero_placeholders.py
    python tools/dsh/verify_hero_placeholders.py --root darkest/assets/heroes_placeholder
EXIT: 0 = every pack is complete, 1 = at least one problem (each problem is printed with its path).
"""

import json
import os
import sys

REQUIRED = ["idle", "combat", "attack", "defend", "walk"]
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_ROOT = os.path.join(REPO, "darkest", "assets", "heroes_placeholder")


def check_pack(pack_dir, descriptor_path, problems, rows):
    with open(descriptor_path, "r", encoding="utf-8") as fh:
        try:
            cfg = json.load(fh)
        except Exception as exc:  # noqa: BLE001 - report, do not crash
            problems.append("%s: JSON parse failed: %s" % (descriptor_path, exc))
            return

    arch = cfg.get("archetype") or "(缺失)"
    actions = cfg.get("actions") or {}
    framed = 0
    missing_slots = [s for s in REQUIRED if s not in actions]
    for slot in missing_slots:
        problems.append("%s: missing required slot `%s`" % (descriptor_path, slot))

    for slot, spec in sorted(actions.items()):
        frames = spec.get("frames") or []
        if not frames:
            if not spec.get("missing_reason"):
                problems.append("%s: slot `%s` no frames and no missing_reason (P31 2)" % (descriptor_path, slot))
            continue
        framed += 1
        if not spec.get("anchor"):
            problems.append("%s: slot `%s` framed but no anchor (P31 3)" % (descriptor_path, slot))
        for ref in frames:
            full = os.path.join(pack_dir, ref)
            if not os.path.isfile(full):
                problems.append("%s: slot `%s` frame not found: %s" % (descriptor_path, slot, ref))

    portrait = cfg.get("portrait")
    if portrait and not os.path.isfile(os.path.join(pack_dir, portrait)):
        problems.append("%s: portrait not found: %s" % (descriptor_path, portrait))

    if not cfg.get("placeholder"):
        problems.append("%s: missing `placeholder: true` (compliance item 3)" % descriptor_path)
    if not (cfg.get("source") or "").strip():
        problems.append("%s: missing `source` (compliance item 3)" % descriptor_path)

    rows.append((arch, os.path.basename(pack_dir), len(actions), framed,
                 "yes" if portrait else "no", (cfg.get("source") or "")[:46]))


FORMAL_ROOT = os.path.join(REPO, "darkest", "assets", "heroes")


def check_formal(formal_root, problems, rows):
    """Check the FORMAL path: it must never carry placeholder markers (compliance red line)."""
    if not os.path.isdir(formal_root):
        return
    for arch in sorted(d for d in os.listdir(formal_root) if os.path.isdir(os.path.join(formal_root, d))):
        descriptor = os.path.join(formal_root, arch, "hero.json")
        if not os.path.isfile(descriptor):
            continue
        with open(descriptor, "r", encoding="utf-8") as fh:
            try:
                cfg = json.load(fh)
            except Exception as exc:  # noqa: BLE001
                problems.append("%s: JSON parse failed: %s" % (descriptor, exc))
                continue
        if cfg.get("placeholder"):
            problems.append("%s: FORMAL path must not set 'placeholder: true' (put borrowed art in "
                            "assets/heroes_placeholder instead)" % descriptor)
        for slot in REQUIRED:
            if slot not in (cfg.get("actions") or {}):
                problems.append("%s: missing required slot '%s'" % (descriptor, slot))
        rows.append((cfg.get("archetype") or "?", "FORMAL:" + arch, len(cfg.get("actions") or {}), 0,
                     "yes" if cfg.get("portrait") else "no", "(formal asset)"))


def main(argv):
    root = DEFAULT_ROOT
    if "--root" in argv:
        root = argv[argv.index("--root") + 1]

    if not os.path.isdir(root):
        print("[hero-placeholder] directory absent (packs not generated) -- nothing to check: %s" % root)
        return 0

    problems, rows = [], []
    check_formal(FORMAL_ROOT, problems, rows)
    packs = sorted(d for d in os.listdir(root) if os.path.isdir(os.path.join(root, d)))
    for pack in packs:
        descriptor = os.path.join(root, pack, "hero.json")
        if not os.path.isfile(descriptor):
            # A payload dir may legitimately have no descriptor of its own (the legacy single pack
            # keeps hero.json at the root). Report it as a note, not a failure.
            print("  (note) %s has no hero.json -- treated as payload of the legacy root descriptor" % pack)
            continue
        check_pack(os.path.join(root, pack), descriptor, problems, rows)

    legacy = os.path.join(root, "hero.json")
    if os.path.isfile(legacy):
        check_pack(root, legacy, problems, rows)

    print("[hero-placeholder] PACKAGING check (behaviour rules live in HeroAssets.Parse / HeroArtResolver)")
    print("  %-12s %-14s %-6s %-7s %-9s %s" % ("archetype", "pack", "slots", "framed", "portrait", "source"))
    for arch, pack, slots, framed, portrait, source in rows:
        print("  %-12s %-10s %-6d %-6d %-8s %s" % (arch, pack, slots, framed, portrait, source))

    if problems:
        print("[hero-placeholder] RESULT: BAD (%d problem(s))" % len(problems))
        for p in problems:
            print("  - %s" % p)
        return 1

    print("[hero-placeholder] RESULT: OK (%d pack(s) complete)" % len(rows))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
