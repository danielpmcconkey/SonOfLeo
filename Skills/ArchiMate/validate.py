#!/usr/bin/env python3
"""
Deterministic ArchiMate model validator.

Parses Archi's relationships.xml to build the validity matrix, then checks
every relationship in a .archimate file against it. Results should match
Archi's built-in validation (Ctrl+Shift+V).

Usage:
    python validate.py [path/to/model.archimate]

Defaults to Architecture/SonOfLeo.archimate relative to the repo root.
"""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
REPO_ROOT = SCRIPT_DIR.parent.parent
DEFAULT_MODEL = REPO_ROOT / "Architecture" / "SonOfLeo.archimate"
RELATIONSHIPS_XML = SCRIPT_DIR / "references" / "relationships.xml"

NS = {"archimate": "http://www.archimatetool.com/archimate"}
XSI = "http://www.w3.org/2001/XMLSchema-instance"

RELATIONSHIP_CODES = {
    "a": "AccessRelationship",
    "c": "CompositionRelationship",
    "f": "FlowRelationship",
    "g": "AggregationRelationship",
    "i": "AssignmentRelationship",
    "n": "InfluenceRelationship",
    "o": "AssociationRelationship",
    "r": "RealizationRelationship",
    "s": "SpecializationRelationship",
    "t": "TriggeringRelationship",
    "v": "ServingRelationship",
}

CODE_BY_TYPE = {v: k for k, v in RELATIONSHIP_CODES.items()}

RELATIONSHIP_TYPES = set(RELATIONSHIP_CODES.values())

JUNCTION_TYPES = {"AndJunction", "OrJunction"}

VIEW_TYPES = {
    "ArchimateDiagramModel",
    "DiagramObject",
    "DiagramModelNote",
    "DiagramModelReference",
    "Connection",
    "Group",
    "SketchModel",
    "CanvasModel",
}


def load_validity_matrix(path: Path) -> dict[str, dict[str, str]]:
    tree = ET.parse(path)
    root = tree.getroot()
    matrix: dict[str, dict[str, str]] = {}
    for source in root.findall("source"):
        src_concept = source.attrib["concept"]
        targets: dict[str, str] = {}
        for target in source.findall("target"):
            targets[target.attrib["concept"]] = target.attrib["relations"]
        matrix[src_concept] = targets
    return matrix


def strip_archimate_prefix(xsi_type: str) -> str:
    return xsi_type.replace("archimate:", "")


def normalize_concept(concept: str) -> str:
    if concept in RELATIONSHIP_TYPES:
        return "Relationship"
    if concept in JUNCTION_TYPES:
        return "Junction"
    return concept


def parse_model(path: Path) -> tuple[dict[str, str], list[dict]]:
    """Returns (elements_by_id, relationships)."""
    tree = ET.parse(path)
    root = tree.getroot()

    elements: dict[str, str] = {}
    relationships: list[dict] = []

    for elem in root.iter("element"):
        xsi_type = elem.attrib.get(f"{{{XSI}}}type", "")
        if not xsi_type:
            continue
        concept = strip_archimate_prefix(xsi_type)
        elem_id = elem.attrib.get("id", "")
        name = elem.attrib.get("name", "")

        if concept in VIEW_TYPES:
            continue
        elif concept in RELATIONSHIP_TYPES:
            relationships.append({
                "id": elem_id,
                "type": concept,
                "source": elem.attrib.get("source", ""),
                "target": elem.attrib.get("target", ""),
                "name": name,
            })
        else:
            elements[elem_id] = concept

    return elements, relationships


def validate(model_path: Path, matrix_path: Path) -> list[dict]:
    matrix = load_validity_matrix(matrix_path)
    elements, relationships = parse_model(model_path)

    findings: list[dict] = []

    for rel in relationships:
        src_id = rel["source"]
        tgt_id = rel["target"]

        if src_id not in elements:
            findings.append({
                "severity": "ERROR",
                "type": "broken_reference",
                "message": f"Relationship '{rel['name'] or rel['id']}' "
                           f"({rel['type']}): source ID '{src_id}' not found",
                "relationship_id": rel["id"],
            })
            continue

        if tgt_id not in elements:
            findings.append({
                "severity": "ERROR",
                "type": "broken_reference",
                "message": f"Relationship '{rel['name'] or rel['id']}' "
                           f"({rel['type']}): target ID '{tgt_id}' not found",
                "relationship_id": rel["id"],
            })
            continue

        src_concept = normalize_concept(elements[src_id])
        tgt_concept = normalize_concept(elements[tgt_id])
        rel_code = CODE_BY_TYPE.get(rel["type"])

        if rel_code is None:
            findings.append({
                "severity": "ERROR",
                "type": "unknown_relationship",
                "message": f"Unknown relationship type: {rel['type']}",
                "relationship_id": rel["id"],
            })
            continue

        allowed = matrix.get(src_concept, {}).get(tgt_concept, "")
        if rel_code not in allowed:
            src_name = next(
                (r["name"] for r in relationships if r["id"] == src_id),
                elements.get(src_id, src_id),
            )
            findings.append({
                "severity": "ERROR",
                "type": "illegal_relationship",
                "message": (
                    f"Illegal {rel['type']}: "
                    f"{src_concept} → {tgt_concept} "
                    f"('{rel['name'] or rel['id']}'). "
                    f"Allowed: {_expand_codes(allowed) if allowed else 'none'}"
                ),
                "relationship_id": rel["id"],
                "source_concept": src_concept,
                "target_concept": tgt_concept,
                "relationship_type": rel["type"],
            })

    return findings


def _expand_codes(codes: str) -> str:
    return ", ".join(
        RELATIONSHIP_CODES.get(c, c) for c in sorted(codes)
    )


def print_summary(elements: dict, relationships: list, findings: list) -> None:
    print(f"Elements:      {len(elements)}")
    print(f"Relationships: {len(relationships)}")
    print(f"Findings:      {len(findings)}")
    print()

    errors = [f for f in findings if f["severity"] == "ERROR"]
    warnings = [f for f in findings if f["severity"] == "WARNING"]

    if errors:
        print(f"ERRORS ({len(errors)}):")
        for f in errors:
            print(f"  [{f['type']}] {f['message']}")
        print()

    if warnings:
        print(f"WARNINGS ({len(warnings)}):")
        for f in warnings:
            print(f"  [{f['type']}] {f['message']}")
        print()

    if not findings:
        print("VALID — no issues found.")


def main() -> int:
    model_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_MODEL

    if not model_path.exists():
        print(f"Model not found: {model_path}", file=sys.stderr)
        return 1

    if not RELATIONSHIPS_XML.exists():
        print(f"Relationship matrix not found: {RELATIONSHIPS_XML}", file=sys.stderr)
        return 1

    elements, relationships = parse_model(model_path)
    findings = validate(model_path, RELATIONSHIPS_XML)
    print_summary(elements, relationships, findings)

    return 1 if any(f["severity"] == "ERROR" for f in findings) else 0


if __name__ == "__main__":
    sys.exit(main())
