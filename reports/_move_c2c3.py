#!/usr/bin/env python3
"""Goal-1 (C-2 / C-3): relocate MoraleLedger -> sim/morale, LightMeter -> sim/survival.

Zero behaviour change: only file location + namespace symbol + necessary using directives.
Dry-run by default; pass --apply to write.
"""
import pathlib, re, sys

ROOT = pathlib.Path(r"F:\GithubPro\Darkest\darkest")

MOVES = [
    dict(src="scripts/gameplay/sim/pipeline/MoraleLedger.cs",
         dst="scripts/gameplay/sim/morale/MoraleLedger.cs",
         old_ns="Darkest.Gameplay.Sim.Pipeline",
         new_ns="Darkest.Gameplay.Sim.Morale",
         syms=[r"MoraleLedger"]),
    dict(src="scripts/gameplay/sim/run/LightMeter.cs",
         dst="scripts/gameplay/sim/survival/LightMeter.cs",
         old_ns="Darkest.Gameplay.Sim.Run",
         new_ns="Darkest.Gameplay.Sim.Survival",
         syms=[r"LightMeter", r"ILightMeter", r"LightTier"]),
]

USE_RE = re.compile(r"^\s*using\s+(?:static\s+|[\w.]+\s*=\s*)?([\w.]+)\s*;")
NS_RE = re.compile(r"^\s*namespace\s+([\w.]+)\s*;", re.M)


def own_namespace(text: str) -> str:
    m = NS_RE.search(text)
    return m.group(1) if m else ""


def using_lines(lines):
    return [i for i, l in enumerate(lines) if USE_RE.match(l)]


def ensure_using(text: str, ns: str):
    """Return (new_text, changed). Insert ns into the top using block, sorted among Darkest.*."""
    lines = text.split("\n")
    idx = using_lines(lines)
    for i in idx:
        if USE_RE.match(lines[i]).group(1) == ns:
            return text, False
    line = f"using {ns};"
    if idx:
        darkest = [i for i in idx if USE_RE.match(lines[i]).group(1).startswith("Darkest.")]
        if darkest:
            ins = None
            for i in darkest:
                cur = USE_RE.match(lines[i]).group(1)
                if cur < ns:
                    ins = i + 1
                else:
                    break
            ins = darkest[0] if ins is None else ins
        else:
            ins = idx[-1] + 1
    else:
        m = NS_RE.search(text)
        ins = text[:m.start()].count("\n") if m else 0
    lines.insert(ins, line)
    return "\n".join(lines), True


def read(p: pathlib.Path) -> str:
    return p.read_text(encoding="utf-8-sig", newline="")


def all_cs():
    for p in ROOT.rglob("*.cs"):
        s = str(p.as_posix())
        if "/obj/" in s or "/bin/" in s or "/Godot/" in s:
            continue
        yield p


def main():
    apply = "--apply" in sys.argv
    plan_move, plan_edit = [], []

    # ---- pass 1: file moves (plus .uid siblings) ----
    for mv in MOVES:
        src, dst = ROOT / mv["src"], ROOT / mv["dst"]
        plan_move.append((src, dst))

    # ---- pass 2: using fixes on every referencing file ----
    edits = {}  # path -> list of ns to add
    for mv in MOVES:
        pat = re.compile(r"\b(" + "|".join(mv["syms"]) + r")\b")
        for p in all_cs():
            rel = p.relative_to(ROOT).as_posix()
            if rel == mv["dst"] or rel == mv["src"]:
                continue  # the moved file itself: handled by the namespace rewrite
            text = read(p)
            if not pat.search(text):
                continue
            if own_namespace(text) == mv["new_ns"]:
                continue  # same namespace resolves without a using
            new_text, changed = ensure_using(text, mv["new_ns"])
            if changed:
                edits.setdefault(p, []).append(mv["new_ns"])
                plan_edit.append((rel, mv["new_ns"]))

    print(f"=== plan: {len(plan_move)} moves, {len(plan_edit)} using insertions ===")
    for s, d in plan_move:
        print(f"  MOVE  {s.relative_to(ROOT).as_posix()} -> {d.relative_to(ROOT).as_posix()}")
    for rel, ns in plan_edit:
        print(f"  USING {rel}  <- {ns}")

    if not apply:
        print("\n(dry run; pass --apply to write)")
        return

    import shutil
    for s, d in plan_move:
        d.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(s), str(d))
        uid_s, uid_d = s.with_suffix(s.suffix + ".uid"), d.with_suffix(d.suffix + ".uid")
        if uid_s.exists():
            shutil.move(str(uid_s), str(uid_d))
        print("moved", d.relative_to(ROOT).as_posix(), "+uid" if uid_s.exists() else "(no uid)")

    # rewrite namespaces in moved files
    for mv in MOVES:
        p = ROOT / mv["dst"]
        text = read(p)
        new_text = text.replace(f"namespace {mv['old_ns']};", f"namespace {mv['new_ns']};", 1)
        if new_text == text:
            raise SystemExit(f"namespace line not found in {p}")
        p.write_text(new_text, encoding="utf-8", newline="")
        print("namespace ->", mv["new_ns"], "in", mv["dst"])

    for p, nss in edits.items():
        text = read(p)
        for ns in nss:
            text, _ = ensure_using(text, ns)
        p.write_text(text, encoding="utf-8", newline="")
    print(f"\napplied: {len(edits)} files patched")


if __name__ == "__main__":
    main()
