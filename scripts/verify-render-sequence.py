#!/usr/bin/env python3
"""Atlas RenderTreeBuilder static verification.

Layers:
  A — sequence continuity: every render method's literal sequence args
      must be a strict 0..N-1 increasing run (no repeats, no jumps).
  B — structure diff: the method's builder-call sequence (tag/attr/type,
      ignoring sequence args) must match a baseline snapshot.

Usage:
  python scripts/verify-render-sequence.py baseline <files...> --out <json>
  python scripts/verify-render-sequence.py check-sequence <files...>
  python scripts/verify-render-sequence.py diff <baseline.json> <files...>
"""
import json
import re
import sys
from pathlib import Path

BUILDER_CALL = re.compile(
    r"builder\.(OpenElement|OpenComponent|OpenRegion|AddAttribute|"
    r"AddContent|AddMarkupContent|AddElementReferenceCapture|"
    r"CloseElement|CloseComponent|CloseRegion|SetKey|AddComponentReferenceCapture)"
    r"(?:\s*<[^>]*>)?\s*\(")
METHOD_HEAD = re.compile(
    r"(?:protected\s+override\s+void|private\s+void|internal\s+void|public\s+void)\s+"
    r"(BuildRenderTree|Render\w+)\s*\(")


def find_methods(source: str):
    """Yield (name, body_start, body_end) for each render method."""
    for m in METHOD_HEAD.finditer(source):
        name = m.group(1)
        brace = source.find("{", m.end())
        if brace < 0:
            continue
        depth = 0
        i = brace
        while i < len(source):
            ch = source[i]
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    yield name, brace, i
                    break
            i += 1


def extract_calls(source: str, start: int, end: int):
    """Return ordered builder-call signatures inside [start, end)."""
    calls = []
    for m in BUILDER_CALL.finditer(source, start, end):
        op = m.group(1)
        call_start = m.end()
        # capture up to the closing paren (naive, no nested parens in args used)
        paren = source.find(")", call_start)
        args = source[call_start:paren] if paren >= 0 else ""
        if op in ("OpenElement", "AddAttribute"):
            # first string literal arg
            lit = re.search(r'"((?:[^"\\]|\\.)*)"', args)
            calls.append(f"{op}:{lit.group(1) if lit else '?'}")
        elif op == "OpenComponent":
            typ = re.search(r"<([^>]+)>", args)
            calls.append(f"OpenComponent:{(typ.group(1) if typ else '?').strip()}")
        else:
            calls.append(op)
    return calls


def sequence_args(source: str, start: int, end: int):
    """Return the literal sequence args for consuming calls inside a method."""
    seq = []
    consuming = {
        "OpenElement", "OpenComponent", "OpenRegion", "AddAttribute",
        "AddContent", "AddMarkupContent", "AddElementReferenceCapture",
        "AddComponentReferenceCapture",
    }
    for m in BUILDER_CALL.finditer(source, start, end):
        op = m.group(1)
        if op not in consuming:
            continue
        call_start = m.end()
        paren = source.find(")", call_start)
        args = source[call_start:paren] if paren >= 0 else ""
        if op == "OpenComponent":
            # The regex already consumed the optional generic and the
            # opening paren, so call_start sits right after it: args is
            # the bare first parameter (the sequence literal).
            first = args.strip() if args else ""
        else:
            first = args.split(",")[0].strip() if args else ""
        # literal int only; skip `sequence++` / `var sequence` style
        if re.fullmatch(r"\d+", first):
            seq.append((op, int(first)))
    return seq


def render_methods(path: Path):
    src = path.read_text(encoding="utf-8")
    out = {}
    for name, start, end in find_methods(src):
        out.setdefault(name, []).append({
            "calls": extract_calls(src, start, end),
            "sequence": sequence_args(src, start, end),
        })
    return out


def cmd_baseline(files, out_path):
    snapshot = {}
    for f in files:
        snapshot[Path(f).name] = render_methods(Path(f))
    Path(out_path).write_text(json.dumps(snapshot, indent=1), encoding="utf-8")
    total = sum(len(v) for v in snapshot.values())
    print(f"baseline: {len(snapshot)} files, {total} render methods -> {out_path}")


def check_continuous(seq):
    """Each 0-start segment must be 0..N-1 continuous; segments may restart
    at 0 for conditional branches and collection iterations (local domain)."""
    expected = 0
    for v in seq:
        if v == 0:
            expected = 1
        elif v == expected:
            expected += 1
        else:
            return False
    return True


def cmd_check_sequence(files):
    failed = False
    for f in files:
        for name, methods in render_methods(Path(f)).items():
            for idx, m in enumerate(methods):
                seq = [s for _, s in m["sequence"]]
                if not check_continuous(seq):
                    failed = True
                    print(f"FAIL {f}::{name}#{idx}: sequence {seq} not continuous per 0-start segment")
    if failed:
        print("LAYER-A: FAILED")
        sys.exit(1)
    print("LAYER-A: all methods have continuous 0-start sequence segments")


def cmd_diff(baseline_path, files):
    baseline = json.loads(Path(baseline_path).read_text(encoding="utf-8"))
    failed = False
    for f in files:
        current = render_methods(Path(f))
        base = baseline.get(Path(f).name, {})
        for name in set(base) | set(current):
            b = base.get(name, [])
            c = current.get(name, [])
            if len(b) != len(c):
                failed = True
                print(f"FAIL {f}::{name}: method count {len(b)} -> {len(c)}")
                continue
            for i, (bc, cc) in enumerate(zip(b, c)):
                if bc["calls"] != cc["calls"]:
                    failed = True
                    print(f"FAIL {f}::{name}#{i}: call sequence changed")
                    print(f"  before: {bc['calls']}")
                    print(f"  after : {cc['calls']}")
    if failed:
        print("LAYER-B: FAILED")
        sys.exit(1)
    print("LAYER-B: call sequences match baseline")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    mode = sys.argv[1]
    if mode == "baseline":
        out_idx = sys.argv.index("--out")
        files = [Path(a) for a in sys.argv[2:out_idx]]
        out = sys.argv[out_idx + 1]
        cmd_baseline(files, out)
    elif mode == "check-sequence":
        cmd_check_sequence([Path(a) for a in sys.argv[2:]])
    elif mode == "diff":
        baseline_path, files = sys.argv[2], [Path(a) for a in sys.argv[3:]]
        cmd_diff(baseline_path, files)
    else:
        print(__doc__)
        sys.exit(2)
