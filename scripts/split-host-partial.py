#!/usr/bin/env python3
"""Split AtlasWorkspaceHost.cs into partial-class files by responsibility.

Phase 1 of the Host decomposition: pure file organization. Members are
moved verbatim into partial files; no logic or public API changes.

Usage: python scripts/split-host-partial.py <path-to-AtlasWorkspaceHost.cs>
"""
import re
import sys
from pathlib import Path

# Member name -> target partial file (without extension).
RENDERING = {
    "BuildRenderTree", "RenderNode", "RenderToolBar", "RenderToolBarRegion",
    "IsUpperOrLowerRegion", "IsBottomRegion", "RenderFixedSlot",
    "RenderHostError", "OrderedItems", "RenderCollapseButton",
    "AddOptionalAttribute",
}
DECLARATIVE = {
    "ProcessDeclarationAsync", "ProcessContentSourcesAsync",
    "BuildContentGeneration", "TryMergeRuntimeItems", "RememberDeclaration",
    "Fingerprint", "ResetDeclarationLedger", "PresentationFingerprint",
    "NodeFingerprint", "ItemFingerprint", "DefinitionError",
    "ShouldOwnWorkspace", "ValidateOwnership", "ThrowIfDisposed",
}
INTERACTION = {
    "ToggleToolAsync", "ToggleSplitChildAsync", "OnWorkspaceChanged",
    "OnItemActivated", "OnInteractionAsync", "NotifySplitPreviewClearedAsync",
    "ResolveCommittedBasis", "CurrentAnchor", "ExecuteDockItemAsync",
    "ExecuteKeyboardCommandAsync", "FocusToolGroupAsync", "CycleTab",
    "ReportErrorAsync", "SetErrorAsync", "InteractionConfigurationFingerprint",
}
HELPERS = {
    "ResolveSplitterPresentation", "SplitClass", "SplitStyle",
    "IsSubtreeEffectivelyHidden", "CollapseAdjustedStyle", "SplitRatio",
    "Invariant", "CssClasses",
}
DOMAIN_FOR = {}
for name in RENDERING:
    DOMAIN_FOR[name] = "Rendering"
for name in DECLARATIVE:
    DOMAIN_FOR[name] = "Declarative"
for name in INTERACTION:
    DOMAIN_FOR[name] = "Interaction"
for name in HELPERS:
    DOMAIN_FOR[name] = "Helpers"


# AtlasXmlLayoutSerializer domains.
SERIALIZER_READ = {
    "LoadSecure", "ValidateSchema", "ValidateLimits",
    "ReadSnapshot", "ReadItem", "ReadNode", "ReadStrip",
}
SERIALIZER_WRITE = {
    "WriteItems", "WriteLayout", "WriteNode", "WriteToolBars",
    "WriteAutoHide", "CreateSchemas",
}
SERIALIZER_LEGACY = {
    "ReadLegacy", "ChainLegacyRoots", "ChooseActive",
    "ParseLegacyRegion", "EdgeFor",
}
SERIALIZER_RESTORE = {
    "ApplyRestoreOptions", "BuildRemaps", "RemapSnapshot",
    "ReplaceWithPlaceholders", "DropItems", "CopySnapshot",
}
SERIALIZER_HELPERS = {
    "EnsureSnapshotValid", "TryCompatibleVersion", "Required", "Optional",
    "OptionalAttribute", "Format", "ParseEnum", "ParseBool", "ParseInt",
    "ParseFinite", "ParseNullableFinite", "Invalid", "InvalidVersion",
}


def serializer_domains():
    mapping = {}
    for name in SERIALIZER_READ:
        mapping[name] = "Read"
    for name in SERIALIZER_WRITE:
        mapping[name] = "Write"
    for name in SERIALIZER_LEGACY:
        mapping[name] = "Legacy"
    for name in SERIALIZER_RESTORE:
        mapping[name] = "Restore"
    for name in SERIALIZER_HELPERS:
        mapping[name] = "Helpers"
    return mapping

ATTR_ONLY = re.compile(r"^    \[[^\]]+\]\s*$")
COMMENT_LINE = re.compile(r"^    //")

# Method: Name( or Name<T>( — the name must not follow `.`, `=`, `{`, or a
# word character, and must not be `new` (so initializers like
# `Guid.NewGuid()` / `= new()` don't match).
NAME_METHOD = re.compile(r"(?<![.=\w{])(?!new\b)([A-Za-z_]\w*)\s*(?:<[^()]*>)?\s*\(")
# Property/field: Name { / Name = / Name ;
NAME_PROP = re.compile(r"([A-Za-z_]\w*)\s*(?:\{|=|;)")


def member_name(line: str):
    if not line.startswith("    ") or not line.lstrip().startswith(
            ("public ", "private ", "protected ", "internal ")):
        return None
    m = NAME_METHOD.search(line)
    if m and line[:m.start()].count("(") == 0:
        return m.group(1)
    m2 = NAME_PROP.search(line)
    if m2:
        return m2.group(1)
    return None


def split_file(path: Path):
    # Normalize line endings to LF so partial files match the repo's
    # .editorconfig (end_of_line = lf) and git diff stays clean.
    text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
    lines = text.splitlines(keepends=True)
    is_serializer = "AtlasXmlLayoutSerializer" in path.name
    domain_for = serializer_domains() if is_serializer else DOMAIN_FOR
    class_name = "AtlasXmlLayoutSerializer" if is_serializer else "AtlasWorkspaceHost"
    domains = (["Read", "Write", "Legacy", "Restore", "Helpers"]
               if is_serializer
               else ["Rendering", "Declarative", "Interaction", "Helpers"])
    ns = "Atlas.Core.Persistence" if is_serializer else "Atlas.Blazor.Components"

    # Find class opening brace.
    class_line = next(
        i for i, l in enumerate(lines)
        if "class " + class_name in l)
    open_brace = next(
        i for i in range(class_line, len(lines))
        if "{" in lines[i])
    # Find the final closing brace (last non-empty line).
    close_brace = len(lines) - 1
    while close_brace > open_brace and lines[close_brace].strip() == "":
        close_brace -= 1
    assert lines[close_brace].strip() == "}", "expected } at " + str(close_brace)

    header = "".join(lines[: open_brace + 1])
    trailer = lines[close_brace]  # the closing }

    # Walk members between open_brace+1 and close_brace.
    members = {}  # name -> [text lines]
    i = open_brace + 1
    pending_attrs = []
    while i < close_brace:
        # Skip blank lines.
        if lines[i].strip() == "":
            i += 1
            continue
        # Attribute/comment-only lines belong to the member that follows.
        if ATTR_ONLY.match(lines[i]) or COMMENT_LINE.match(lines[i]):
            pending_attrs.append(lines[i])
            i += 1
            continue
        name = member_name(lines[i])
        assert name is not None, "unparsed member at line %d: %s" % (i + 1, lines[i].strip()[:60])
        start = i
        # Find member end: next member start with same indent, or close_brace.
        i += 1
        depth = 0
        while i < close_brace:
            stripped = lines[i].strip()
            if stripped == "":
                i += 1
                continue
            if "{" in stripped or stripped.endswith("{"):
                depth += 1
            if "}" in stripped:
                depth -= 1
            if depth == 0 and lines[i].startswith("    "):
                nxt = member_name(lines[i])
                if (nxt and nxt != name) or ATTR_ONLY.match(lines[i]) or COMMENT_LINE.match(lines[i]):
                    break
            i += 1
        members[name] = "".join(pending_attrs) + "".join(lines[start:i])
        pending_attrs = []

    # Build partial files.
    # Reuse the main file's full using block so every partial compiles.
    using_block = "".join(
        l for l in lines if l.startswith("using "))
    for domain in domains:
        domain_members = {n: t for n, t in members.items() if domain_for.get(n) == domain}
        body = "".join(domain_members[n] for n in domain_members)  # insertion order = source order
        partial = (
            using_block
            + "\n"
            + "namespace " + ns + ";\n"
            + "\n"
            + f"/// <summary>{domain} members of <see cref=\"{class_name}\"/> (partial).</summary>\n"
            + "public sealed partial class " + class_name + "\n"
            + "{\n"
            + body
            + "}\n"
        )
        out = path.with_name(f"{class_name}.{domain}.cs")
        with open(out, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(partial)
        print(f"wrote {out.name}: {len(domain_members)} members")

    # Rewrite the main file: header + kept members + trailer.
    kept = {n: t for n, t in members.items() if domain_for.get(n) is None}
    # Keep field/property order as-is: iterate members in original order.
    kept_ordered = []
    for n in members:
        if domain_for.get(n) is None:
            kept_ordered.append(members[n])
    new_main = header + "".join(kept_ordered) + trailer
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(new_main)
    print(f"main file: kept {len(kept_ordered)} members")


if __name__ == "__main__":
    split_file(Path(sys.argv[1]))
