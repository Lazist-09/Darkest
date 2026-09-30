# -*- coding: utf-8 -*-
"""add_discipline_block.py -- append one discipline block to N skill files, with proof.

Why: a discipline that is *announced* in a letter but never written into
skills/*/SKILL.md does not exist for the next reader (纪律 BV family: 立了纪律
≠ 用得上它).  So landing a block must print, per target: bytes before/after and
the read-back count of EVERY discipline letter named in the block.

Usage:
  python tools/dsh/add_discipline_block.py BLOCK.md TARGET.md [TARGET.md ...]

Append-only; UTF-8 without BOM; ASCII-only stdout (GBK console safe).
"""
from __future__ import annotations

import os
import re
import sys

LETTERS = re.compile(r"纪律 ([A-Z]{1,2})\b")


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: add_discipline_block.py BLOCK.md TARGET.md [TARGET.md ...]")
        return 2
    block_path, targets = argv[0], argv[1:]
    # newline="" everywhere: universal-newline reads would silently rewrite the
    # target's CRLF line endings on write (a whole-file change nobody asked for).
    with open(block_path, "r", encoding="utf-8", newline="") as fh:
        block = fh.read()
    if not block.startswith("\n"):
        block = "\n" + block
    if not block.endswith("\n"):
        block += "\n"
    letters = sorted(set(LETTERS.findall(block)))
    print("block   : %s (%d bytes)  disciplines=%s" % (
        block_path, len(block.encode("utf-8")), ",".join(letters) or "?"))
    print("last line of block: %s" % block.rstrip().splitlines()[-1][:70].encode("ascii", "replace").decode())

    rc = 0
    for t in targets:
        try:
            old = open(t, encoding="utf-8", newline="").read()
            before = os.path.getsize(t)
            last = old.rstrip().splitlines()[-1][:50].encode("ascii", "replace").decode()
            sep = "" if old.endswith("\n") else "\n"
            with open(t, "w", encoding="utf-8", newline="") as fh:
                fh.write(old + sep + block)
            after = os.path.getsize(t)
            rb = open(t, encoding="utf-8", newline="").read()
            counts = " ".join("%s=%d" % (l, len(re.findall(r"纪律 %s\b" % l, rb))) for l in letters)
            print("  OK   %-44s %7d -> %7d  %s" % (t, before, after, counts))
            print("       was last line: %s" % last)
            if any(len(re.findall(r"纪律 %s\b" % l, rb)) == 0 for l in letters):
                rc = 1
        except OSError as e:
            print("  FAIL %-44s %s" % (t, e))
            rc = 1
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
