#!/usr/bin/env python3
"""Atlas documentation validation.

Phase-aware checker for docs/documentation-manifest.json, Markdown link
integrity, language mirror sets, and normative structure tuples (SRS
requirement IDs, HLD DES/OI IDs, ADR IDs).

Usage:
    python scripts/validate-docs.py [--phase P0|P1|P2|P3|P4|P5a|P5b|P5c|P5d|P6] [--repo ROOT]

The checker is phase-aware:
- authority paths must always exist;
- a legacy route's source must exist once its activeFromPhase has been reached;
- a target path must exist once the document's migrationPhase has been reached
  AND its recorded state says the target should exist (English In Review /
  Ready for Cutover / Canonical, or Chinese Synchronized / Outdated);
- a mirrorRequired target that is not yet due may be absent if it is planned;
  at P6 every mirrorRequired target must exist.

Exits 0 on success, 1 on validation failure.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

# Phase ordering used for phase-aware checks.
PHASE_ORDER = ["P0", "P1", "P2", "P3", "P4", "P5a", "P5b", "P5c", "P5d", "P6"]
ALLOWED_STATES = {
    "Not Started",
    "Legacy Canonical",
    "English In Review",
    "English Ready for Cutover",
    "English Canonical",
    "Chinese Synchronized",
    "Chinese Outdated",
    "N/A",
}
MIRRORED_ROLE_EXCEPTIONS = {"migration-control"}

LINK_RE = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
FENCE_RE = re.compile(r"^```|^~~~", re.MULTILINE)
ANCHOR_RE = re.compile(r"^(#{1,6})\s+(.+?)\s*$", re.MULTILINE)


def phase_index(phase: str) -> int:
    if phase not in PHASE_ORDER:
        raise ValueError(f"Unknown phase {phase!r}")
    return PHASE_ORDER.index(phase)


def is_fenced(line_index: int, fence_lines: set[int]) -> bool:
    return line_index in fence_lines


def collect_fenced_lines(text: str) -> set[int]:
    """Return 0-based line numbers that are inside fenced code blocks."""
    inside = False
    result: set[int] = set()
    for i, line in enumerate(text.splitlines()):
        stripped = line.strip()
        if stripped.startswith("```") or stripped.startswith("~~~"):
            inside = not inside
            continue
        if inside:
            result.add(i)
    return result


def github_anchor(heading: str) -> str:
    """Approximate GitHub's heading anchor slug for a heading line's text."""
    slug = heading.strip().lower()
    slug = re.sub(r"[^\w\s\u4e00-\u9fff\-_]", "", slug)
    slug = re.sub(r"\s+", "-", slug)
    slug = re.sub(r"-+", "-", slug)
    return slug.strip("-")


def collect_anchors(text: str) -> dict[str, str]:
    """Map slug -> original heading for a markdown file."""
    anchors: dict[str, str] = {}
    for match in ANCHOR_RE.finditer(text):
        slug = github_anchor(match.group(2))
        if slug:
            anchors.setdefault(slug, match.group(2))
    return anchors


def load_manifest(repo: Path) -> dict:
    manifest_path = repo / "docs" / "documentation-manifest.json"
    if not manifest_path.exists():
        raise SystemExit(f"ERROR: manifest not found at {manifest_path}")
    with manifest_path.open(encoding="utf-8") as fh:
        return json.load(fh)


def load_status(repo: Path) -> str:
    status_path = repo / "docs" / "translation-status.md"
    if not status_path.exists():
        raise SystemExit(f"ERROR: status table not found at {status_path}")
    return status_path.read_text(encoding="utf-8")


def validate_manifest_schema(manifest: dict, errors: list[str]) -> None:
    if manifest.get("schemaVersion") != 1:
        errors.append("manifest.schemaVersion must be 1")

    docs = manifest.get("documents")
    if not isinstance(docs, dict) or not docs:
        errors.append("manifest.documents must be a non-empty object")
        return

    seen_ids: set[str] = set()
    for doc_id, doc in docs.items():
        if doc.get("documentId") != doc_id:
            errors.append(f"document {doc_id}: documentId must equal its key")
        if doc_id in seen_ids:
            errors.append(f"document {doc_id}: duplicate documentId")
        seen_ids.add(doc_id)

        role = doc.get("role")
        if not role:
            errors.append(f"document {doc_id}: role required")

        phase = doc.get("migrationPhase")
        if phase not in PHASE_ORDER:
            errors.append(f"document {doc_id}: invalid migrationPhase {phase!r}")

        targets = doc.get("targets") or {}
        for lang in ("en", "zh"):
            if lang not in targets or not targets.get(lang):
                errors.append(f"document {doc_id}: targets.{lang} required")

        authority = doc.get("authority") or {}
        if not authority.get("path"):
            errors.append(f"document {doc_id}: authority.path required")
        if authority.get("language") not in ("en", "zh"):
            errors.append(f"document {doc_id}: authority.language must be en or zh")
        state = authority.get("state")
        if state not in ALLOWED_STATES:
            errors.append(f"document {doc_id}: invalid authority.state {state!r}")

        integrity = doc.get("integrity") or {}
        for field in ("translatedFromBlob", "reviewedCandidateBlob", "canonicalBlob"):
            value = integrity.get(field)
            if value is not None and not isinstance(value, str):
                errors.append(f"document {doc_id}: integrity.{field} must be null or string")

        if "mirrorRequired" not in doc:
            errors.append(f"document {doc_id}: mirrorRequired required")

    validation_inputs = manifest.get("validationInputs") or {}
    for key, doc_id in validation_inputs.items():
        if doc_id not in docs:
            errors.append(f"validationInputs.{key} references unknown document {doc_id!r}")

    routes = manifest.get("legacyRoutes")
    if not isinstance(routes, list):
        errors.append("manifest.legacyRoutes must be an array")
    else:
        for i, route in enumerate(routes):
            for field in ("sourcePath", "targetDocumentId", "kind", "activeFromPhase", "removeAfterPhase", "removalCondition", "reason"):
                if field not in route:
                    errors.append(f"legacyRoutes[{i}]: missing {field}")
            if route.get("targetDocumentId") not in docs:
                errors.append(f"legacyRoutes[{i}]: unknown targetDocumentId")
            if route.get("activeFromPhase") not in PHASE_ORDER:
                errors.append(f"legacyRoutes[{i}]: invalid activeFromPhase")

    exceptions = manifest.get("mirrorExceptions")
    if not isinstance(exceptions, list):
        errors.append("manifest.mirrorExceptions must be an array")
    else:
        for i, exception in enumerate(exceptions):
            for field in ("path", "reason", "lifetime", "removalCondition"):
                if field not in exception:
                    errors.append(f"mirrorExceptions[{i}]: missing {field}")


def phase_reached(current: str, required: str) -> bool:
    return phase_index(current) >= phase_index(required)


def validate_path_existence(manifest: dict, repo: Path, current_phase: str, errors: list[str]) -> None:
    docs = manifest["documents"]

    for doc_id, doc in docs.items():
        authority_path = doc["authority"]["path"]
        if not (repo / authority_path).exists():
            errors.append(f"document {doc_id}: authority path missing: {authority_path}")

        state = doc["authority"]["state"]
        phase = doc["migrationPhase"]
        targets = doc["targets"]
        mirror_required = doc.get("mirrorRequired", False)

        # English target existence depends on state, not just phase.
        if state in ("English In Review", "English Ready for Cutover", "English Canonical"):
            if not (repo / targets["en"]).exists():
                errors.append(f"document {doc_id}: English target missing for state {state}: {targets['en']}")

        # Chinese target existence.
        chinese_state = doc.get("chineseState") or "Legacy Canonical"
        if chinese_state in ("Chinese Synchronized", "Chinese Outdated"):
            if not (repo / targets["zh"]).exists():
                errors.append(f"document {doc_id}: Chinese target missing for state {chinese_state}: {targets['zh']}")

        # Phase-gated planned target: once the migration phase has started and
        # the document has moved, both targets should exist unless excepted.
        if phase_reached(current_phase, phase) and mirror_required and state == "English Canonical":
            for lang in ("en", "zh"):
                if not (repo / targets[lang]).exists():
                    errors.append(f"document {doc_id}: target missing at phase {phase}: {targets[lang]}")

    for route in manifest.get("legacyRoutes", []):
        if phase_reached(current_phase, route["activeFromPhase"]):
            source = repo / route["sourcePath"]
            if not source.exists():
                errors.append(
                    f"legacy route source missing at phase {current_phase}: {route['sourcePath']}"
                )
            if route["removalCondition"] != "permanent" and route.get("removeAfterPhase"):
                if phase_reached(current_phase, route["removeAfterPhase"]):
                    if source.exists():
                        errors.append(
                            f"legacy route source should have been removed: {route['sourcePath']}"
                        )

    # P6: all mirrorRequired targets must exist.
    if current_phase == "P6":
        for doc_id, doc in docs.items():
            if not doc.get("mirrorRequired", False):
                continue
            for lang in ("en", "zh"):
                target = doc["targets"][lang]
                if not (repo / target).exists():
                    errors.append(f"document {doc_id}: mirrorRequired target missing at P6: {target}")


def validate_status_consistency(manifest: dict, status_text: str, errors: list[str]) -> None:
    docs = manifest["documents"]
    status_ids = set(re.findall(r"`([a-z0-9-]+)`", status_text))
    # Only document IDs appear as backticked lowercase keys in the table rows.
    manifest_ids = set(docs.keys())
    for doc_id in manifest_ids:
        if doc_id not in status_text:
            errors.append(f"status table missing row for document {doc_id}")


def extract_links(md_path: Path, repo: Path) -> list[tuple[int, str, str]]:
    """Return (line_number, link_target, fragment) for local markdown links."""
    text = md_path.read_text(encoding="utf-8")
    fenced = collect_fenced_lines(text)
    links: list[tuple[int, str, str]] = []
    for i, line in enumerate(text.splitlines()):
        if i in fenced:
            continue
        for match in LINK_RE.finditer(line):
            target = match.group(1).strip()
            if not target or target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            # Strip trailing spaces and titles like "path "title"".
            target = target.split(" ")[0]
            fragment = ""
            if "#" in target:
                target, fragment = target.split("#", 1)
            links.append((i + 1, target, fragment))
    return links


def validate_links(repo: Path, errors: list[str]) -> None:
    md_files = sorted(repo.rglob("*.md"))
    md_files = [
        p
        for p in md_files
        if ".git" not in p.parts
        and ".packages" not in p.parts
        and "artifacts" not in p.parts
        and "obj" not in p.parts
        and "bin" not in p.parts
    ]
    anchors_cache: dict[Path, dict[str, str]] = {}
    for md in md_files:
        anchors_cache[md] = collect_anchors(md.read_text(encoding="utf-8"))

    for md in md_files:
        for line_no, target, fragment in extract_links(md, repo):
            resolved = (md.parent / target).resolve()
            if not resolved.exists():
                errors.append(f"{md.relative_to(repo)}:{line_no}: broken link target: {target}")
                continue
            if fragment:
                target_anchors = anchors_cache.get(resolved)
                if target_anchors is None:
                    target_anchors = collect_anchors(resolved.read_text(encoding="utf-8"))
                    anchors_cache[resolved] = target_anchors
                slug = github_anchor(fragment)
                if slug not in target_anchors:
                    errors.append(
                        f"{md.relative_to(repo)}:{line_no}: broken anchor '#{fragment}' in {target}"
                    )


def validate_internal_links_within_doc(repo: Path, errors: list[str]) -> None:
    """Check same-file anchors used as [text](#fragment)."""
    md_files = sorted(repo.rglob("*.md"))
    md_files = [
        p
        for p in md_files
        if ".git" not in p.parts
        and ".packages" not in p.parts
        and "artifacts" not in p.parts
        and "obj" not in p.parts
        and "bin" not in p.parts
    ]
    for md in md_files:
        text = md.read_text(encoding="utf-8")
        anchors = collect_anchors(text)
        fenced = collect_fenced_lines(text)
        for i, line in enumerate(text.splitlines()):
            if i in fenced:
                continue
            for match in LINK_RE.finditer(line):
                target = match.group(1).strip()
                if target.startswith("#"):
                    slug = github_anchor(target[1:])
                    if slug not in anchors:
                        errors.append(
                            f"{md.relative_to(repo)}:{i + 1}: broken same-file anchor '#{target[1:]}'"
                        )


def extract_srs_ids(repo: Path, manifest: dict | None = None) -> set[str]:
    srs_path = authority_path(repo, manifest, "srs")
    if srs_path is None or not srs_path.exists():
        return set()
    text = srs_path.read_text(encoding="utf-8")
    return set(re.findall(r"^REQ-(?:F|NF)-\d{3} .*\[Actor:", text, re.MULTILINE))


def extract_hld_ids(repo: Path, manifest: dict | None = None) -> tuple[set[str], set[str]]:
    hld_path = authority_path(repo, manifest, "hld")
    if hld_path is None or not hld_path.exists():
        return set(), set()
    text = hld_path.read_text(encoding="utf-8")
    des = set(re.findall(r"\bDES-[A-Z]+-\d{3}\b", text))
    oi = set(re.findall(r"\bOI-\d{3}\b", text))
    return des, oi


def authority_path(repo: Path, manifest: dict | None, doc_id: str) -> Path | None:
    """Resolve a document's current authority path from the manifest."""
    if manifest is None:
        return None
    doc = manifest.get("documents", {}).get(doc_id)
    if doc is None:
        return None
    path = doc.get("authority", {}).get("path")
    return repo / path if path else None


def validate_srs_tuple(repo: Path, manifest: dict, errors: list[str]) -> None:
    srs_path = authority_path(repo, manifest, "srs")
    if srs_path is None or not srs_path.exists():
        return
    text = srs_path.read_text(encoding="utf-8")
    formal = re.findall(r"^REQ-(?:F|NF)-\d{3} .*\[Actor:", text, re.MULTILINE)
    formal = [item.split(" ")[0] for item in formal]
    if len(formal) != 150:
        errors.append(f"SRS formal requirement count is {len(formal)}, expected 150")
    f_count = sum(1 for item in formal if item.startswith("REQ-F-"))
    nf_count = sum(1 for item in formal if item.startswith("REQ-NF-"))
    if f_count != 142:
        errors.append(f"SRS REQ-F count is {f_count}, expected 142")
    if nf_count != 8:
        errors.append(f"SRS REQ-NF count is {nf_count}, expected 8")
    if len(set(formal)) != len(formal):
        errors.append("SRS contains duplicate formal requirement IDs")

    start_marker = "<!-- atlas:requirements-index:start -->"
    end_marker = "<!-- atlas:requirements-index:end -->"
    if start_marker not in text:
        errors.append("SRS missing atlas:requirements-index:start marker")
    if end_marker not in text:
        errors.append("SRS missing atlas:requirements-index:end marker")
    if start_marker in text and end_marker in text and text.index(start_marker) > text.index(end_marker):
        errors.append("SRS index markers out of order")


def validate_hld_tuple(repo: Path, manifest: dict, errors: list[str]) -> None:
    des, oi = extract_hld_ids(repo, manifest)
    if not des:
        errors.append("HLD contains no DES identifiers")
    if not oi:
        errors.append("HLD contains no OI identifiers")
    duplicates = [item for item in des if list(des).count(item) > 1]  # set already unique
    if duplicates:
        errors.append(f"HLD duplicate DES identifiers: {sorted(set(duplicates))}")


def validate_adr_tuple(repo: Path, manifest: dict, errors: list[str]) -> None:
    adr_dir = authority_path(repo, manifest, "adr-index")
    if adr_dir is None or not adr_dir.exists():
        return
    adr_files = sorted(adr_dir.parent.glob("*.md"))
    for adr in adr_files:
        if adr.name == "README.md":
            continue
        text = adr.read_text(encoding="utf-8")
        if not re.search(r"^#\s+ADR-\d{4}", text, re.MULTILINE):
            errors.append(f"{adr.relative_to(repo)}: missing '# ADR-0000' title")
        if not re.search(r"^#{1,3}\s*(状态|Status)\s*$", text, re.MULTILINE) \
                and not re.search(r"^- \**状态\**：", text, re.MULTILINE) \
                and not re.search(r"^- \**Status\**:", text, re.MULTILINE) \
                and "Status:" not in text:
            errors.append(f"{adr.relative_to(repo)}: missing status section")
        if not re.search(r"^#{1,3}\s*(Proposed )?(决策|Decision)\s*$", text, re.MULTILINE):
            errors.append(f"{adr.relative_to(repo)}: missing decision section")


def validate_mirror_set(repo: Path, manifest: dict, current_phase: str, errors: list[str]) -> None:
    """en/zh mirror file sets must match for documents that have cut over.

    Only documents whose manifest authority state is English Canonical are
    required to have both targets. Documents that have not reached their
    migration phase (for example SRS before P5d) may have an English
    translation without a Chinese mirror yet.
    """
    if not phase_reached(current_phase, "P1"):
        return
    docs = manifest.get("documents", {})
    exceptions = {e["path"] for e in manifest.get("mirrorExceptions", [])}
    for doc_id, doc in docs.items():
        if doc.get("authority", {}).get("state") != "English Canonical":
            continue
        if not doc.get("mirrorRequired", False):
            continue
        targets = doc.get("targets", {})
        en_path = targets.get("en")
        zh_path = targets.get("zh")
        if not en_path or not zh_path:
            continue
        en_exists = (repo / en_path).exists()
        zh_exists = (repo / zh_path).exists()
        if not en_exists:
            errors.append(f"mirror set: canonical document {doc_id} missing English target: {en_path}")
        if not zh_exists:
            errors.append(f"mirror set: canonical document {doc_id} missing Chinese target: {zh_path}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Atlas documentation contract")
    parser.add_argument("--phase", default="P0", choices=PHASE_ORDER, help="current migration phase")
    parser.add_argument("--repo", default=".", help="repository root")
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    errors: list[str] = []
    warnings: list[str] = []

    manifest = load_manifest(repo)
    validate_manifest_schema(manifest, errors)
    validate_status_consistency(manifest, load_status(repo), errors)
    validate_path_existence(manifest, repo, args.phase, errors)
    validate_links(repo, errors)
    validate_internal_links_within_doc(repo, errors)
    validate_srs_tuple(repo, manifest, errors)
    validate_hld_tuple(repo, manifest, errors)
    validate_adr_tuple(repo, manifest, errors)
    validate_mirror_set(repo, manifest, args.phase, errors)

    for warning in warnings:
        print(f"WARN: {warning}")
    for error in errors:
        print(f"ERROR: {error}")

    if errors:
        print(f"\nValidation FAILED with {len(errors)} error(s) at phase {args.phase}.")
        return 1
    print(f"Validation PASSED at phase {args.phase}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
