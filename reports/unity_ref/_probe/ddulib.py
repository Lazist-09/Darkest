"""Tolerant JSON loader for the DDU reference dataset.

The reference project ships JSON with trailing commas (and possibly // comments),
which Python's json module rejects.  We sanitise the text with a small
string-aware state machine and then use the stdlib parser.
"""
import io, json, os, re, sys

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
REF_SCRIPTS = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"


def out(*a):
    s = " ".join(str(x) for x in a)
    sys.stdout.write(s.encode("ascii", "backslashreplace").decode("ascii") + "\n")


def outfile(path, text):
    with io.open(path, "w", encoding="utf-8") as f:
        f.write(text)


def _sanitize(text):
    res = []
    i = 0
    n = len(text)
    in_str = False
    esc = False
    while i < n:
        c = text[i]
        if in_str:
            res.append(c)
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                in_str = False
            i += 1
            continue
        if c == '"':
            in_str = True
            res.append(c)
            i += 1
            continue
        # line comment
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            j = text.find("\n", i)
            if j < 0:
                break
            i = j
            continue
        # block comment
        if c == "/" and i + 1 < n and text[i + 1] == "*":
            j = text.find("*/", i + 2)
            i = (j + 2) if j >= 0 else n
            continue
        if c == ",":
            # look ahead: skip whitespace/comments, if next significant is } or ] drop the comma
            j = i + 1
            while j < n and text[j] in " \t\r\n":
                j += 1
            while j < n and text[j] == "/" and j + 1 < n and text[j + 1] in "/*":
                if text[j + 1] == "/":
                    k = text.find("\n", j)
                    j = (k + 1) if k >= 0 else n
                else:
                    k = text.find("*/", j + 2)
                    j = (k + 2) if k >= 0 else n
                while j < n and text[j] in " \t\r\n":
                    j += 1
            if j < n and text[j] in "}]":
                i += 1
                continue
        res.append(c)
        i += 1
    return "".join(res)


def loads(text):
    return json.loads(_sanitize(text))


def load(path):
    with io.open(path, "r", encoding="utf-8-sig") as f:
        return loads(f.read())


def read_bytes(path):
    with open(path, "rb") as f:
        return f.read()


def typestruct(o, depth=0, maxd=3, maxitems=60):
    if isinstance(o, dict):
        if depth >= maxd:
            return "dict(%d keys)" % len(o)
        items = list(o.items())[:maxitems]
        s = "{" + ", ".join("%s:%s" % (k, typestruct(v, depth + 1, maxd, maxitems)) for k, v in items) + "}"
        if len(o) > maxitems:
            s += " ...+%d" % (len(o) - maxitems)
        return s
    if isinstance(o, list):
        if not o:
            return "list[0]"
        return "list[%d] of %s" % (len(o), typestruct(o[0], depth + 1, maxd, maxitems))
    if isinstance(o, str):
        return "str(%r)" % (o[:40],)
    if isinstance(o, bool):
        return "bool"
    if isinstance(o, int):
        return "int"
    if isinstance(o, float):
        return "float"
    if o is None:
        return "null"
    return type(o).__name__


def keyspec(obj, path="root"):
    """Recursively dump the key skeleton (names + value kinds), deduped by key path."""
    seen = {}

    def walk(o, p):
        if isinstance(o, dict):
            for k, v in o.items():
                kp = p + "." + str(k)
                kind = typestruct(v, 0, 0)
                if kp not in seen:
                    seen[kp] = (set(), 0)
                seen[kp][0].add(kind)
                seen[kp] = (seen[kp][0], seen[kp][1] + 1)
                walk(v, kp)
        elif isinstance(o, list):
            for v in o[:5]:
                walk(v, p + "[]")

    walk(obj, path)
    return seen
