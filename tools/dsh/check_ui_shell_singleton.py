#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_ui_shell_singleton.py -- B-2 / form-B end-state criteria, as a runnable check.

WHY (architect: "判据「外壳现在有几个实例？」必须是 1" + B-2 "外壳三层都在当前场景之上")
    Under form B the UI shell must be THE application root, and there must be exactly ONE shell:
        *     main_scene == res://scenes/ui/ui_root.tscn      (the shell is the entry)
        * NO  [autoload] UIRoot="*res://scenes/ui/ui_root.tscn"  (otherwise a second instance)
    Both facts are readable from `darkest/project.godot`, so the criterion is machine-checkable --
    which is the whole point: a criterion that only exists in prose drifts.

CURRENT STATE IS EXPECTED TO BE "NOT YET": the end-state lands with S4 (battle becomes a panel).
    => default mode reports and exits 0 (informational; safe to run any time)
    => --strict exits 1 while the criteria are unmet  (wire this into CI when S4 is declared done)

WHAT IT ALSO REPORTS (helps the reader see how far along form B is, from the same file)
    * which screen the game boots into
    * whether the autoload shell is registered
    * the presence of the two known transition call sites (scene-style navigation) -- counted, not judged

OUTPUT IS ASCII-ONLY (GBK console).
USAGE
    python tools/dsh/check_ui_shell_singleton.py [--strict] [--project <path>]
EXIT: 0 = reported (or criteria met) · 1 = --strict and criteria unmet / file unreadable
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROJECT = os.path.join(REPO, "darkest", "project.godot")
SHELL_SCENE = "res://scenes/ui/ui_root.tscn"

# Scene-style navigation call sites that form B eventually replaces with shell panels.
# Counted, never judged here (the owning files are UI-domain / in flight).
SCENE_STYLE_CALL_RE = re.compile(r"change_scene_to_file|ChangeSceneToFile")


def main(argv):
    project = argv[argv.index("--project") + 1] if "--project" in argv else PROJECT
    strict = "--strict" in argv
    if not os.path.isfile(project):
        print("[shell] project file not found: %s" % project)
        return 1

    with open(project, "r", encoding="utf-8", errors="replace") as fh:
        text = fh.read()

    main_scene = None
    for line in text.splitlines():
        if line.strip().startswith("run/main_scene"):
            main_scene = line.split("=", 1)[1].strip().strip('"')
            break

    autoload_shell = bool(re.search(r'^\s*UIRoot\s*=\s*"\*?res://scenes/ui/ui_root\.tscn"', text, re.M))

    # count scene-style call sites in the C# tree (context only)
    scene_style = 0
    scripts = os.path.join(REPO, "darkest", "scripts")
    for folder, _dirs, files in os.walk(scripts):
        for name in files:
            if not name.endswith(".cs"):
                continue
            try:
                with open(os.path.join(folder, name), "r", encoding="utf-8", errors="replace") as fh:
                    scene_style += len(SCENE_STYLE_CALL_RE.findall(fh.read()))
            except OSError:
                pass

    c1 = main_scene == SHELL_SCENE
    c2 = not autoload_shell
    met = c1 and c2

    print("[shell] main_scene         = %s" % main_scene)
    print("[shell] autoload UIRoot     = %s" % ("YES (=> a second shell instance)" if autoload_shell else "no"))
    print("[shell] scene-style calls   = %d (context only; form B replaces them with panels)" % scene_style)
    print("[shell] criterion 1 (shell is the entry)      : %s" % ("MET" if c1 else "NOT YET"))
    print("[shell] criterion 2 (no autoload shell)       : %s" % ("MET" if c2 else "NOT YET"))
    print("[shell] => shell instance count              : %s" % ("1 (end state)" if met else "NOT 1 YET"))

    if met:
        print("[shell] RESULT: OK (form-B end state reached)")
        return 0

    if strict:
        print("[shell] RESULT: BAD (--strict: form-B end state not reached)")
        return 1

    print("[shell] RESULT: NOT-YET (informational; run with --strict after S4 lands)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
