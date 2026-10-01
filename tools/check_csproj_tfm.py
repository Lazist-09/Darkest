#!/usr/bin/env python3
# csproj TFM gate -- maintenance discipline 2026-09-09, machine-enforced 2026-10-02 (O-114).
#
# WHY THIS GATE EXISTS (a comment was not enough -- it regressed twice):
#   The Godot 4.6 C# plugin rewrites `darkest/Darkest.csproj` when it saves the project,
#   and it MATERIALISES the indirect form
#       <TargetFramework>$(DarkestTargetFramework)</TargetFramework>
#   into a literal
#       <TargetFramework>net8.0</TargetFramework>
#   (leaving a stale `*.csproj.old` beside it).  That damage is INVISIBLE: the build still
#   succeeds, so `-p:DarkestTargetFramework=net10.0` -- the documented local override on a box
#   with no .NET 8 SDK -- silently stops applying, and the next "verified locally" claim is about
#   an assembly nobody meant to build.
#   History: `7425e35` restored it; `9d42a8f` (2026-09-10) re-broke it; O-114 (2026-10-02)
#   restored it again and added this gate, so a third attempt fails in CI instead of in a reader's head.
#
# CONTRACT: every csproj that defines <DarkestTargetFramework> MUST take <TargetFramework> from it.
#           A literal net<major>.<minor> in that element is a failure.
#           A csproj without <DarkestTargetFramework> is out of scope (not ours to police).
#
# Usage:  python tools/check_csproj_tfm.py              # exit 1 on violation
#         python tools/check_csproj_tfm.py --selfcheck  # negative probe must be caught
#
# Gate invariant (red line 20 item 6): verified in BOTH directions -- clean tree passes, a
# materialised probe fails.  That is exactly what --selfcheck exercises.

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCAN_ROOT = "darkest"
SKIP_DIRS = (".godot", "obj", "bin")
INDIRECT = "$(DarkestTargetFramework)"
DEF_RE = re.compile(r"<DarkestTargetFramework([^<]*)</DarkestTargetFramework>", re.S)
TFM_RE = re.compile(r"<TargetFramework([^<]*)</TargetFramework>", re.S)


def problems(name, text):
    """Violations for one csproj body (empty list = clean, or out of scope)."""
    if not DEF_RE.search(text):
        return []
    found = [m.split(">", 1)[-1].strip() for m in TFM_RE.findall(text)]
    if not found:
        return ["%s: defines DarkestTargetFramework but has no <TargetFramework>" % name]
    bad = sorted(set(v for v in found if v != INDIRECT))
    if bad:
        return ["%s: <TargetFramework> materialised to '%s' -- must stay '%s' (O-114)"
                % (name, bad[0], INDIRECT)]
    return []


def iter_csproj(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS)
        for fname in sorted(filenames):
            if fname.endswith(".csproj"):
                yield os.path.join(dirpath, fname)


def main(argv):
    if "--selfcheck" in argv:
        probe = ("<Project><PropertyGroup>"
                 "<DarkestTargetFramework Condition='$(DarkestTargetFramework)' == ''>net8.0"
                 "</DarkestTargetFramework>"
                 "<TargetFramework>net8.0</TargetFramework>"
                 "</PropertyGroup></Project>")
        hits = problems("<probe>", probe)
        print("selfcheck: materialised probe -> %d hit(s)" % len(hits))
        for h in hits:
            print("   %s" % h)
        return 0 if hits else 1

    scanned, bad = [], []
    for path in iter_csproj(os.path.join(REPO, SCAN_ROOT)):
        with open(path, "r", encoding="utf-8-sig", errors="replace") as fh:
            text = fh.read()
        if not DEF_RE.search(text):
            continue
        rel = os.path.relpath(path, REPO).replace(os.sep, "/")
        scanned.append(rel)
        bad += problems(rel, text)

    if bad:
        print("FAIL: csproj TargetFramework must stay indirect (O-114):")
        for b in bad:
            print("   %s" % b)
        print("   -> the Godot 4.6 C# plugin materialises it on save: restore the line to")
        print("      <TargetFramework>$(DarkestTargetFramework)</TargetFramework> and delete")
        print("      the *.csproj.old backup it leaves behind.")
        return 1

    print("OK: %d csproj(s) take TargetFramework from %s" % (len(scanned), INDIRECT))
    for rel in scanned:
        print("   %s" % rel)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
