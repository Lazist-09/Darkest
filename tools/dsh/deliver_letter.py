# -*- coding: utf-8 -*-
"""deliver_letter.py -- append one letter to one or more windows, with proof.

Why: the four windows are volatile channels; a delivery must be *provable*
after the fact (纪律 AK: "投递之后怎么证明投过").  So this tool always
prints, per target:  bytes before -> bytes after, and the read-back hit count
of the letter's own marker (the `ALL-CAPS-LINE` inside the code fence).

Usage:
  python tools/dsh/deliver_letter.py LETTER.md WINDOW.txt [WINDOW.txt ...]

Guarantees:
  * never overwrites or clears -- append only (收件箱纪律 v2: 只删已完成, 不整窗清空)
  * writes UTF-8 without BOM, and normalises the join to exactly one blank line
  * prints ASCII only, so a GBK console cannot kill it
"""
from __future__ import annotations

import os
import re
import sys

MARKER = re.compile(r"^[A-Z][A-Z0-9_-]{6,}$", re.M)


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: deliver_letter.py LETTER.md WINDOW.txt [WINDOW.txt ...]")
        return 2
    letter_path, targets = argv[0], argv[1:]

    # newline="" everywhere: universal-newline reads would silently rewrite the
    # target's CRLF line endings on write (a whole-file change nobody asked for).
    with open(letter_path, "r", encoding="utf-8", newline="") as fh:
        letter = fh.read()
    if not letter.endswith("\n"):
        letter += "\n"
    hits = MARKER.findall(letter)
    marker = hits[0] if hits else "<no marker found>"
    print("letter  : %s (%d bytes)" % (letter_path, len(letter.encode("utf-8"))))
    print("marker  : %s" % marker)

    rc = 0
    for t in targets:
        try:
            before = os.path.getsize(t)
            with open(t, "r", encoding="utf-8", errors="replace", newline="") as fh:
                old = fh.read()
            sep = "" if old.endswith("\n\n") or not old else ("\n" if old.endswith("\n") else "\n\n")
            with open(t, "w", encoding="utf-8", newline="") as fh:
                fh.write(old + sep + letter)
            after = os.path.getsize(t)
            with open(t, "r", encoding="utf-8", errors="replace", newline="") as fh:
                readback = fh.read()
            n = readback.count(marker)
            print("  OK   %-34s %8d -> %8d bytes   readback hits=%d" % (t, before, after, n))
            if n == 0:
                rc = 1
        except OSError as e:
            print("  FAIL %-34s %s" % (t, e))
            rc = 1
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
