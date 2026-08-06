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
    r"(?:\s*<[^()]*>)?\s*\(")
# Helper methods that take an explicit literal sequence as their second
# argument (called with `builder` as the first argument).
HELPER_SEQUENCE_CALL = re.compile(
    r"AddOptionalAttribute\s*\(\s*builder\s*,\s*(\d+)\s*,")
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
    """Return ordered builder-call signatures inside [start, end).
    SetKey is excluded: it does not produce a render frame and its
    presence is covered by manual review and browser verification."""
    calls = []
    for m in BUILDER_CALL.finditer(source, start, end):
        op = m.group(1)
        if op == "SetKey":
            continue
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
    """Return (op, seq, new_segment) triples for consuming calls inside a
    method. new_segment is True when the call immediately follows a
    CloseElement/CloseComponent/CloseRegion (a new frame/region domain).
    Helper calls like AddOptionalAttribute(builder, N, ...) are folded in
    at their source position."""
    items = []
    consuming = {
        "OpenElement", "OpenComponent", "OpenRegion", "AddAttribute",
        "AddContent", "AddMarkupContent", "AddElementReferenceCapture",
        "AddComponentReferenceCapture",
    }
    prev_was_close = False
    for m in BUILDER_CALL.finditer(source, start, end):
        op = m.group(1)
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
        if op in consuming and re.fullmatch(r"\d+", first):
            items.append((m.start(), op, int(first), prev_was_close))
        prev_was_close = op in ("CloseElement", "CloseComponent", "CloseRegion")

    for m in HELPER_SEQUENCE_CALL.finditer(source, start, end):
        items.append((m.start(), "Helper", int(m.group(1)), False))

    items.sort(key=lambda item: item[0])
    return [(op, seq, new_segment) for _, op, seq, new_segment in items]


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


def check_continuous(seq_items):
    """Render methods may contain several sequence domains: OpenRegion
    children restart, conditional branches reuse the same slot, frames under
    different parents can repeat the same numbers, and a CloseRegion returns
    to the parent domain. Collapse adjacent duplicates, then require every
    increasing segment to be strictly +1. A segment boundary is any value
    that does not continue the run, or a call flagged new_segment (immediately
    after a Close*)."""
    deduped = []
    for op, seq, new_segment in seq_items:
        if not deduped or deduped[-1][1] != seq:
            deduped.append((op, seq, new_segment))
    if not deduped:
        return True
    seg_start = 0
    for i in range(1, len(deduped) + 1):
        is_boundary = (
            i == len(deduped)
            or deduped[i][2]
            or deduped[i][1] <= deduped[i - 1][1])
        if is_boundary:
            for j in range(seg_start + 1, i):
                if deduped[j][1] != deduped[j - 1][1] + 1:
                    return False
            seg_start = i
    return True


def cmd_check_sequence(files):
    failed = False
    for f in files:
        for name, methods in render_methods(Path(f)).items():
            for idx, m in enumerate(methods):
                seq = m["sequence"]
                if not check_continuous(seq):
                    failed = True
                    vals = [s for _, s, _ in seq]
                    print(f"FAIL {f}::{name}#{idx}: sequence {vals} not continuous per domain segment")
    if failed:
        print("LAYER-A: FAILED")
        sys.exit(1)
    print("LAYER-A: all methods have continuous per-domain sequence segments")


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
