#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ui_layout_audit.py -- static layout audit for the UI scenes (the card's `--ui-audit` sibling).

🔴 MEASURED CONCLUSION (2026-09-21): a STATIC audit can never be the layout authority here.
    Measured across darkest/scenes/**: only 54 nodes carry anchor_left and just 13 carry the full
    anchor+offset set => the static coverage CEILING is ~24%. The rest is laid out by containers
    (HBox/VBox/PanelContainer...) at runtime. That is why this tool is fail-closed and why the
    runtime `--ui-audit` (inside Godot) remains the acceptance authority for "0 overlap / in camera".
    => Do NOT invest further in static resolution; use this tool as a diagnostic only.

WHY (the dispatch card names it as an acceptance tool)
    M8 acceptance: "每批：构建绿 + `--ui-audit` + 平衡读数"
    M9u acceptance: "`--ui-audit` 0 重叠 · 镜头内可见（对齐原版）"
    The runtime audit needs Godot; this static one answers the *overlap* question from the .tscn
    files themselves, so it can run in CI and while the tree is red.

WHAT IT DOES (read-only, no Godot)
    * parses every `darkest/scenes/**/*.tscn`
    * collects Control nodes that carry explicit offsets (offset_left/top/right/bottom), which is
      how this project's skeleton scenes are laid out
    * resolves each node's ABSOLUTE rect by walking the parent chain inside the same scene
      (parent rect origin + own offsets), skipping nodes whose parent cannot be resolved
    * reports overlapping sibling pairs (same parent) and the overlapping area

HONEST LIMITS (stated so nobody reads more into it than it delivers)
    * anchors-based layout is NOT resolved (if a node uses anchor_* without offsets, it is skipped)
    * containers (HBoxContainer/... ) place children at runtime => children are skipped when the
      parent is a container
    * it audits ONE scene at a time; overlaps across scenes are meaningless
    * so "0 overlaps" here means "no overlaps among the explicit-offset controls it could resolve",
      NOT a pixel-perfect guarantee -- the runtime `--ui-audit` remains the authority

OUTPUT IS ASCII-ONLY (GBK console).
USAGE
    python tools/dsh/ui_layout_audit.py [--root <dir>] [--json <path>] [--quiet]
EXIT: 0 = no overlaps found · 1 = overlaps found (usable as a gate)
"""

import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENES = os.path.join(REPO, "darkest", "scenes")

CONTAINERS = (
    "HBoxContainer", "VBoxContainer", "GridContainer", "CenterContainer",
    "MarginContainer", "ScrollContainer", "TabContainer", "FlowContainer",
)

NODE_RE = re.compile(r'^\[node name="([^"]+)"(?:\s+type="([^"]+)")?(?:\s+parent="([^"]+)")?', re.M)
OFFSET_RE = re.compile(r'offset_(left|top|right|bottom)\s*=\s*(-?[\d.]+)')


def parse_scene(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        text = fh.read()

    # split on [node ...] headers, keep each node's own block
    blocks = []
    for m in NODE_RE.finditer(text):
        start = m.end()
        nxt = text.find("[node ", start)
        block = text[start: nxt if nxt != -1 else len(text)]
        name, ntype, parent = m.group(1), m.group(2), m.group(3)
        offs = {k: float(v) for k, v in OFFSET_RE.findall(block)}
        blocks.append({"name": name, "type": ntype, "parent": parent, "offsets": offs})
    return blocks


def audit_scene(path, path_label):
    nodes = parse_scene(path)
    by_path = {("." if n["parent"] is None else "%s/%s" % (n["parent"], n["name"])): n for n in nodes}

    rects = {}
    for key, n in by_path.items():
        if not all(k in n["offsets"] for k in ("left", "top", "right", "bottom")):
            continue
        # resolve parent origin
        origin_x, origin_y = 0.0, 0.0
        parent = n["parent"]
        resolved = True
        while parent and parent != ".":
            pk = parent if parent.startswith(".") else parent
            pn = by_path.get(pk)
            if pn is None or not all(k in pn["offsets"] for k in ("left", "top", "right", "bottom")):
                resolved = False
                break
            if pn["type"] in CONTAINERS:
                resolved = False          # container places children at runtime
                break
            origin_x += pn["offsets"]["left"]
            origin_y += pn["offsets"]["top"]
            parent = pn["parent"]
        if not resolved:
            continue
        rects[key] = (
            origin_x + n["offsets"]["left"],
            origin_y + n["offsets"]["top"],
            origin_x + n["offsets"]["right"],
            origin_y + n["offsets"]["bottom"],
        )

    overlaps = []
    groups = {}
    for key, r in rects.items():
        parent = key.rsplit("/", 1)[0] if "/" in key else "."
        groups.setdefault(parent, []).append((key, r))

    for parent, items in groups.items():
        for i in range(len(items)):
            for j in range(i + 1, len(items)):
                a, ra = items[i]
                b, rb = items[j]
                ox = min(ra[2], rb[2]) - max(ra[0], rb[0])
                oy = min(ra[3], rb[3]) - max(ra[1], rb[1])
                if ox > 0 and oy > 0:
                    overlaps.append({
                        "scene": path_label,
                        "parent": parent,
                        "a": a,
                        "b": b,
                        "overlap_area": round(ox * oy, 1),
                    })
    return len(rects), overlaps


def main(argv):
    root = argv[argv.index("--root") + 1] if "--root" in argv else SCENES
    quiet = "--quiet" in argv
    if not os.path.isdir(root):
        print("[ui-audit] scenes dir not found: %s" % root)
        return 1

    total_scenes = 0
    total_rects = 0
    all_overlaps = []
    skipped = []

    for folder, _dirs, files in os.walk(root):
        for name in sorted(files):
            if not name.endswith(".tscn"):
                continue
            path = os.path.join(folder, name)
            rel = os.path.relpath(path, REPO)
            try:
                n_rects, ov = audit_scene(path, rel)
            except OSError:
                skipped.append(rel)
                continue
            total_scenes += 1
            total_rects += n_rects
            all_overlaps.extend(ov)

    if "--json" in argv:
        out = argv[argv.index("--json") + 1]
        with open(out, "w", encoding="utf-8") as fh:
            json.dump({"scenes": total_scenes, "rects": total_rects, "overlaps": all_overlaps}, fh,
                      ensure_ascii=False, indent=1)

    if not quiet:
        for o in all_overlaps[:25]:
            print("[ui-audit] OVERLAP %s  %s  <-> %s  (area %s)"
                  % (o["scene"], o["a"], o["b"], o["overlap_area"]))
        if len(all_overlaps) > 25:
            print("[ui-audit] ... and %d more" % (len(all_overlaps) - 25))

    # 🔴 **覆盖率闸（fail-closed）**：解析不到足够多的矩形时，"0 重叠"**不是结论** ⇒ 不许报绿 ✓
    #    理由（我实测踩到的）：本项目场景用 anchors/container 布局 ⇒ 我的静态解析只认出 2 个矩形，
    #    若此时报 "OK / 0 overlaps" 就是**假绿**（与本项目学过的"v2 假绿/门禁假红"同一族）✓
    total_controls = 0
    for folder, _dirs, files in os.walk(root):
        for name in sorted(files):
            if name.endswith(".tscn"):
                try:
                    with open(os.path.join(folder, name), "r", encoding="utf-8", errors="replace") as fh:
                        total_controls += len(re.findall(r"^\[node ", fh.read(), re.M))
                except OSError:
                    pass
    coverage = (total_rects / total_controls) if total_controls else 0.0
    min_cov = 0.5

    print("[ui-audit] scenes=%d resolvable_rects=%d overlaps=%d control_nodes=%d coverage=%.1f%%"
          % (total_scenes, total_rects, len(all_overlaps), total_controls, coverage * 100))
    print("[ui-audit] LIMITS: anchors/containers are not resolved (see the module docstring)")

    if coverage < min_cov:
        print("[ui-audit] RESULT: INCONCLUSIVE (coverage %.1f%% < %.0f%% => a clean report would be a FALSE GREEN)"
              % (coverage * 100, min_cov * 100))
        print("[ui-audit] => the runtime --ui-audit remains the authority for layout acceptance")
        return 1

    print("[ui-audit] RESULT: %s" % ("OK (no overlaps among resolvable rects, coverage %.1f%%)" % (coverage * 100)
                                     if not all_overlaps
                                     else "BAD (%d overlapping sibling pair(s))" % len(all_overlaps)))
    return 0 if not all_overlaps else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
