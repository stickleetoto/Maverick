#!/usr/bin/env python3
"""Validate the Maverick aerospace public-source library metadata.

Checks Docs/Research/Aerospace/SOURCE_INDEX.json and every
Docs/Research/Aerospace/Aircraft/*/KNOWN_DATA.json against the rules in
Docs/Research/Aerospace/SCHEMA.md:

* unique source / configuration / value IDs
* every enum field uses a documented value
* related_sources, configuration_ids and value source_ids resolve
* no record claims page-level verification without a retrieved, hashed file
* no numeric value is released for implementation unless its source is
  page-verified (L4+) and its location is captured
* every source_id appears in SOURCE_INDEX.md

Standard library only. Exit code 0 = pass, 1 = failures (all listed).
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LIB = ROOT / "Docs/Research/Aerospace"
INDEX_JSON = LIB / "SOURCE_INDEX.json"
INDEX_MD = LIB / "SOURCE_INDEX.md"

CONTAINS_KEYS = (
    "geometry", "mass", "cg", "inertia", "aerodynamic_coefficients",
    "static_derivatives", "rate_derivatives", "control_derivatives",
    "reference_geometry", "FCS_architecture", "FCS_numeric_gains",
    "actuator_limits", "actuator_rates", "propulsion", "thrust_data",
    "source_code", "equations", "tables", "plots", "flight_data",
    "trim_data", "validation_data",
)
CONTAINS_VALUES = {"yes", "likely", "no", "unknown"}

ACCESS = {
    "PUBLIC_NTRS_PDF_LISTED", "PUBLIC_NTRS_CITATION_ONLY", "PUBLIC_NTRS_RECORD_ID_UNCONFIRMED",
    "PUBLIC_WEB_PAGE", "PUBLIC_DISTRIBUTION_A", "PUBLIC_UNIVERSITY_REPOSITORY",
    "PUBLIC_AUTHOR_HOSTED", "PUBLIC_RELEASE_PAYWALLED",
    "NOT_PUBLIC_DISTRIBUTION_LIMITED", "NOT_PUBLIC_PROPRIETARY", "NOT_PUBLICLY_LOCATED",
}
RETRIEVAL = {"NOT_RETRIEVED_EGRESS_BLOCKED", "NOT_RETRIEVED", "RETRIEVED_EXTERNAL_STORE", "RETRIEVED_IN_REPO"}
LEVELS = (
    "L0_UNVERIFIED_LEAD", "L1_CITATION_CONFIRMED", "L2_ABSTRACT_CONFIRMED",
    "L3_CONTENT_EXTRACT", "L4_PAGE_VERIFIED", "L5_TRANSCRIBED_CROSSCHECKED",
)
PROVENANCE = {
    "ORIGINAL_PRIMARY", "PUBLIC_REPRODUCTION", "DERIVED_SIMULATOR", "CURVE_FIT",
    "DIGITIZED_PRIMARY_FIGURE", "CROSS_VALIDATION_ONLY", "BACKGROUND_ONLY",
}
SCOPE = {
    "EXACT_AIRFRAME", "PRODUCTION_FAMILY", "PREPRODUCTION", "RESEARCH_MODIFIED",
    "WIND_TUNNEL_MODEL", "SIMULATION_ONLY", "UNKNOWN",
}
REQUIRED_SOURCE_FIELDS = (
    "source_id", "title", "authors", "organization", "year", "report_number", "ntrs_id",
    "public_url", "pdf_url", "mirror_url", "local_filename", "sha256",
    "public_access_status", "retrieval_status", "verification_level",
    "aircraft", "aircraft_variant", "serial_or_tail_number", "research_configuration",
    "engine_configuration", "FCS_configuration", "date_or_phase", "configuration_ids",
    "topics", "contains", "important_pages", "important_figures", "important_tables",
    "page_index_status", "provenance_grade", "configuration_scope",
    "implementation_usefulness", "validation_usefulness", "limitations",
    "known_conflicts", "related_sources", "notes", "verification_notes",
)
VALUE_FIELDS = (
    "value_id", "quantity", "symbol", "value", "units", "configuration_ids", "loading_state",
    "source_id", "candidate_source_ids", "location", "origin", "exactness",
    "reference_convention", "implementation_use", "validation_use", "conflicts_with", "notes",
)
IMPLEMENTATION_USE = {
    "ALLOWED", "BLOCKED_PENDING_PAGE_VERIFICATION", "PROHIBITED",
    "DOMAIN_METADATA_ONLY", "NOT_A_MODEL_PARAMETER",
}
ORIGIN = {"ORIGINAL", "REPRODUCED", "DIGITIZED", "DERIVED"}
EXACTNESS = {
    "EXACT", "APPROXIMATE", "DIGITIZED", "SEARCH_EXTRACT", "SEARCH_EXTRACT_APPROXIMATE",
    "ABSTRACT_STATEMENT", "PUBLIC_FACT_SHEET",
}
SHA256 = re.compile(r"^[0-9a-f]{64}$")
ID = re.compile(r"^[A-Z0-9][A-Z0-9\-]+$")


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []

    def check(self, ok: bool, msg: str) -> None:
        if not ok:
            self.errors.append(msg)


def load(path: Path, rep: Report):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 - report any parse failure
        rep.check(False, f"{path.relative_to(ROOT)}: cannot parse JSON ({exc})")
        return None


def validate_sources(index, rep: Report) -> dict:
    rep.check(index.get("schema") == "maverick.aerospace.source-index.v1", "SOURCE_INDEX.json: schema id")
    sources = index.get("sources", [])
    rep.check(isinstance(sources, list) and sources, "SOURCE_INDEX.json: sources must be a non-empty list")
    by_id: dict = {}
    for s in sources:
        sid = s.get("source_id", "<missing>")
        for f in REQUIRED_SOURCE_FIELDS:
            rep.check(f in s, f"{sid}: missing field '{f}'")
        rep.check(bool(ID.match(sid)), f"{sid}: source_id must be upper-case alphanumeric/hyphen")
        rep.check(sid not in by_id, f"{sid}: duplicate source_id")
        by_id[sid] = s
        rep.check(s.get("public_access_status") in ACCESS, f"{sid}: public_access_status '{s.get('public_access_status')}'")
        rep.check(s.get("retrieval_status") in RETRIEVAL, f"{sid}: retrieval_status '{s.get('retrieval_status')}'")
        rep.check(s.get("verification_level") in LEVELS, f"{sid}: verification_level '{s.get('verification_level')}'")
        rep.check(s.get("provenance_grade") in PROVENANCE, f"{sid}: provenance_grade '{s.get('provenance_grade')}'")
        rep.check(s.get("configuration_scope") in SCOPE, f"{sid}: configuration_scope '{s.get('configuration_scope')}'")
        contains = s.get("contains", {})
        rep.check(set(contains) == set(CONTAINS_KEYS), f"{sid}: contains keys must be exactly the schema list")
        for k, v in contains.items():
            rep.check(v in CONTAINS_VALUES, f"{sid}: contains.{k} = '{v}'")
        for key in ("implementation_usefulness", "validation_usefulness"):
            u = s.get(key, {})
            rep.check(isinstance(u, dict) and "rating" in u and "note" in u, f"{sid}: {key} needs rating+note")
        sha = s.get("sha256")
        rep.check(sha is None or bool(SHA256.match(sha)), f"{sid}: sha256 must be 64 lower-case hex or null")
        level = LEVELS.index(s.get("verification_level")) if s.get("verification_level") in LEVELS else -1
        if level >= LEVELS.index("L4_PAGE_VERIFIED"):
            rep.check(str(s.get("retrieval_status", "")).startswith("RETRIEVED"), f"{sid}: L4+ requires a retrieved document")
            rep.check(sha is not None, f"{sid}: L4+ requires sha256 of the retrieved file")
        if s.get("public_access_status", "").startswith("NOT_PUBLIC"):
            rep.check(s.get("implementation_usefulness", {}).get("rating") in {"PROHIBITED", "UNAVAILABLE"},
                      f"{sid}: non-public source must be PROHIBITED/UNAVAILABLE for implementation")
        if s.get("pdf_url"):
            rep.check(s.get("public_access_status") == "PUBLIC_NTRS_PDF_LISTED", f"{sid}: pdf_url only for PUBLIC_NTRS_PDF_LISTED")
    for sid, s in by_id.items():
        for rel in s.get("related_sources", []):
            rep.check(rel.get("source_id") in by_id, f"{sid}: related source '{rel.get('source_id')}' not in index")
            rep.check(bool(rel.get("relation")), f"{sid}: related source '{rel.get('source_id')}' missing relation")
    return by_id


def validate_known(path: Path, doc, sources: dict, rep: Report) -> set:
    rel = path.relative_to(ROOT)
    rep.check(doc.get("schema") == "maverick.aerospace.known-data.v1", f"{rel}: schema id")
    cfg_ids: set = set()
    for c in doc.get("configurations", []):
        cid = c.get("configuration_id", "<missing>")
        rep.check(cid not in cfg_ids, f"{rel}: duplicate configuration_id {cid}")
        cfg_ids.add(cid)
    val_ids: set = set()
    values = doc.get("values", [])
    for v in values:
        vid = v.get("value_id", "<missing>")
        for f in VALUE_FIELDS:
            rep.check(f in v, f"{rel}:{vid}: missing field '{f}'")
        rep.check(vid not in val_ids, f"{rel}: duplicate value_id {vid}")
        val_ids.add(vid)
        for cid in v.get("configuration_ids", []):
            rep.check(cid in cfg_ids, f"{rel}:{vid}: unknown configuration '{cid}'")
        rep.check(bool(v.get("configuration_ids")), f"{rel}:{vid}: configuration_ids empty (naked number)")
        rep.check(bool(v.get("units")), f"{rel}:{vid}: units missing")
        rep.check(bool(v.get("location")), f"{rel}:{vid}: location missing (use NOT_CAPTURED explicitly)")
        rep.check(v.get("origin") in ORIGIN, f"{rel}:{vid}: origin '{v.get('origin')}'")
        rep.check(v.get("exactness") in EXACTNESS, f"{rel}:{vid}: exactness '{v.get('exactness')}'")
        rep.check(v.get("implementation_use") in IMPLEMENTATION_USE, f"{rel}:{vid}: implementation_use '{v.get('implementation_use')}'")
        rep.check(bool(v.get("validation_use")), f"{rel}:{vid}: validation_use missing")
        rep.check(bool(v.get("reference_convention")), f"{rel}:{vid}: reference_convention missing")
        src = v.get("source_id")
        if src == "UNRESOLVED":
            cands = v.get("candidate_source_ids", [])
            rep.check(bool(cands), f"{rel}:{vid}: UNRESOLVED source needs candidate_source_ids")
            for c in cands:
                rep.check(c in sources, f"{rel}:{vid}: candidate source '{c}' not in index")
            rep.check(v.get("implementation_use") != "ALLOWED", f"{rel}:{vid}: UNRESOLVED source cannot be ALLOWED")
        else:
            rep.check(src in sources, f"{rel}:{vid}: source '{src}' not in index")
            if v.get("implementation_use") == "ALLOWED" and src in sources:
                lvl = sources[src].get("verification_level")
                rep.check(lvl in LEVELS and LEVELS.index(lvl) >= LEVELS.index("L4_PAGE_VERIFIED"),
                          f"{rel}:{vid}: ALLOWED requires source at L4+ (is {lvl})")
                rep.check(v.get("location") != "NOT_CAPTURED", f"{rel}:{vid}: ALLOWED requires a captured page/table/figure")
                rep.check(v.get("exactness") in {"EXACT", "APPROXIMATE", "DIGITIZED"}, f"{rel}:{vid}: ALLOWED requires page-level exactness")
    for v in values:
        for other in v.get("conflicts_with", []):
            rep.check(other in val_ids, f"{rel}:{v.get('value_id')}: conflicts_with '{other}' not found")
    return cfg_ids


def main() -> int:
    rep = Report()
    index = load(INDEX_JSON, rep)
    if index is None:
        print("\n".join(rep.errors))
        return 1
    sources = validate_sources(index, rep)

    all_cfg: set = set()
    known_files = sorted((LIB / "Aircraft").glob("*/KNOWN_DATA.json"))
    for path in known_files:
        doc = load(path, rep)
        if doc is not None:
            all_cfg |= validate_known(path, doc, sources, rep)
    for sid, s in sources.items():
        for cid in s.get("configuration_ids", []):
            rep.check(cid in all_cfg, f"{sid}: configuration '{cid}' not defined in any Aircraft/*/KNOWN_DATA.json")

    md = INDEX_MD.read_text(encoding="utf-8") if INDEX_MD.exists() else ""
    rep.check(bool(md), "SOURCE_INDEX.md missing")
    for sid in sources:
        rep.check(f"`{sid}`" in md, f"{sid}: not listed in SOURCE_INDEX.md")

    levels: dict = {}
    for s in sources.values():
        levels[s["verification_level"]] = levels.get(s["verification_level"], 0) + 1
    if rep.errors:
        print(f"FAIL: {len(rep.errors)} problem(s)")
        for e in rep.errors:
            print(" -", e)
        return 1
    print(f"PASS: {len(sources)} sources, {len(all_cfg)} configurations, {len(known_files)} KNOWN_DATA file(s)")
    for lvl in LEVELS:
        if lvl in levels:
            print(f"  {lvl}: {levels[lvl]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
