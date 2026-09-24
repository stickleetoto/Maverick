#!/usr/bin/env python3
"""Validate the Maverick aerospace public-source library metadata.

Checks Docs/Research/Aerospace/SOURCE_INDEX.json, every
Docs/Research/Aerospace/Aircraft/*/KNOWN_DATA.json and the generated views
against the rules in Docs/Research/Aerospace/SCHEMA.md. Nothing here is
specific to one aircraft; packs are discovered from the Aircraft/ folders.

Source records
* unique source IDs; no two sources share a report number or NTRS ID
* every enum field uses a documented value (access, retrieval, verification
  level, provenance grade, configuration scope, aircraft pack)
* aircraft sources carry configuration identity (configuration_ids + aircraft)
* existing_maverick_analysis entries are well formed and never raise the
  library's own verification level
* related_sources resolve; PAGE_VERIFIED needs a retrieved, hashed file

Known data
* unique configuration / value IDs across ALL packs
* every configuration states airframe, engine, FCS, dates, scope and lineage
* every value has a source (or UNRESOLVED + candidates that resolve), a known
  configuration, units, precision, convention and a field from the vocabulary
* a value's verification level never exceeds its source's level
* REPOSITORY_READING values stay at SEARCH_LEAD_ONLY and cite the repository
* ALLOWED only with a PAGE_VERIFIED source AND value and a captured location

Views and documents
* AIRCRAFT_NUMERIC_FIELD_INDEX.json covers every value, and
  implementation_allowed is never true without page verification
* generated views are in sync with the canonical JSON (--check of the builder)
* every source ID appears in SOURCE_INDEX.md
* IDs named in source-graph / lineage documents resolve
* every aircraft pack with KNOWN_DATA.json has its core documents

Standard library only. Exit code 0 = pass, 1 = failures (all listed).
"""
from __future__ import annotations

import importlib.util
import json
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LIB = ROOT / "Docs/Research/Aerospace"
INDEX_JSON = LIB / "SOURCE_INDEX.json"
INDEX_MD = LIB / "SOURCE_INDEX.md"
NUMERIC_INDEX = LIB / "AIRCRAFT_NUMERIC_FIELD_INDEX.json"
BUILDER = ROOT / "Tools/build_aerospace_source_views.py"

SOURCE_SCHEMA = "maverick.aerospace.source-index.v2"
KNOWN_SCHEMA = "maverick.aerospace.known-data.v2"

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
# Ordered weakest -> strongest. Only what the library itself inspected counts.
LEVELS = ("SEARCH_LEAD_ONLY", "CATALOGUE_VERIFIED", "ABSTRACT_VERIFIED", "CONTENT_EXTRACT_VERIFIED", "PAGE_VERIFIED")
PROVENANCE = {
    "ORIGINAL_PRIMARY", "PUBLIC_REPRODUCTION", "DERIVED_SIMULATOR", "CURVE_FIT",
    "DIGITIZED_PRIMARY_FIGURE", "CROSS_VALIDATION_ONLY", "BACKGROUND_ONLY",
}
SCOPE = {
    "EXACT_AIRFRAME", "PRODUCTION_FAMILY", "PREPRODUCTION", "RESEARCH_MODIFIED",
    "WIND_TUNNEL_MODEL", "SIMULATION_ONLY", "UNKNOWN",
}
PACKS = {"FA18", "F16", "F15", "F22", "F14", "FUNDAMENTALS", "OTHER"}
NON_AIRCRAFT_PACKS = {"FUNDAMENTALS", "OTHER"}
REPO_LEVELS = {"REPO_PAGE_READ", "REPO_PAGE_TRANSCRIBED_CROSSCHECKED", "REPO_CITATION_ONLY"}
EMA_FIELDS = ("path", "branch", "relationship", "locator", "level", "note")
REQUIRED_SOURCE_FIELDS = (
    "source_id", "title", "authors", "organization", "year", "report_number", "ntrs_id",
    "public_url", "pdf_url", "mirror_url", "local_filename", "sha256",
    "public_access_status", "retrieval_status", "verification_level", "aircraft_packs", "index_group",
    "aircraft", "aircraft_variant", "serial_or_tail_number", "research_configuration",
    "engine_configuration", "FCS_configuration", "date_or_phase", "configuration_ids",
    "topics", "contains", "important_pages", "important_figures", "important_tables",
    "page_index_status", "provenance_grade", "configuration_scope",
    "implementation_usefulness", "validation_usefulness", "limitations",
    "known_conflicts", "related_sources", "existing_maverick_analysis", "notes", "verification_notes",
)
CONFIG_FIELDS = ("configuration_id", "label", "airframe", "serial", "engine", "fcs", "research_hardware", "dates", "status",
                 "configuration_scope", "lineage_family", "notes")
CONFIG_IDENTITY = ("airframe", "engine", "fcs", "dates", "status", "configuration_scope", "lineage_family")
VALUE_FIELDS = (
    "value_id", "field", "quantity", "symbol", "value", "units", "precision", "configuration_ids", "loading_state",
    "source_id", "candidate_source_ids", "location", "origin", "exactness", "verification_level",
    "reference_convention", "implementation_use", "validation_use", "conflicts_with",
    "existing_maverick_analysis", "notes",
)
IMPLEMENTATION_USE = {
    "ALLOWED", "BLOCKED_PENDING_PAGE_VERIFICATION", "PROHIBITED",
    "DOMAIN_METADATA_ONLY", "NOT_A_MODEL_PARAMETER",
}
ORIGIN = {"ORIGINAL", "REPRODUCED", "DIGITIZED", "DERIVED"}
EXACTNESS = {
    "EXACT", "APPROXIMATE", "DIGITIZED", "SEARCH_EXTRACT", "SEARCH_EXTRACT_APPROXIMATE",
    "ABSTRACT_STATEMENT", "PUBLIC_FACT_SHEET", "REPOSITORY_READING",
}
PAGE_EXACTNESS = {"EXACT", "APPROXIMATE", "DIGITIZED"}
NUMERIC_RECORD_FIELDS = (
    "value_id", "aircraft", "configuration", "configuration_scope", "field", "value", "units", "precision", "source_id",
    "page", "provenance", "verification_level", "implementation_allowed",
)
PACK_CORE_DOCS = ("SOURCE_GRAPH.md", "KNOWN_DATA.md", "MISSING_DATA.md", "SOURCES.json")
PACK_FEASIBILITY_DOCS = ("IMPLEMENTATION_FEASIBILITY.md", "PUBLIC_SOURCE_FEASIBILITY.md")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
ID = re.compile(r"^[A-Z0-9][A-Z0-9\-]+$")
# Tokens in graph/lineage documents that look like library IDs.
# The look-behind stops matches inside longer IDs (e.g. "NASA-REF-TP1538" inside "F16-CFG-NASA-REF-TP1538").
GRAPH_TOKEN = re.compile(r"(?<![A-Za-z0-9-])(?:NASA|NTRS|AIAA|DTIC|AFIT|AFFTC|MDC|RTO|JAIRCRAFT|JGCD|ICAS|SFTE|ASME|ARXIV|NESC|AGARD|NADC|AFWAL|AFRL|MIL|USAF)-[A-Z0-9][A-Z0-9-]*[A-Z0-9](?![A-Za-z0-9-])")
# Tokens that match the ID pattern but are not source IDs (control-law and programme names).
GRAPH_ALLOW = {"NASA-0", "NASA-1A", "NASA-2", "MIL-STD-1797", "MIL-F-8785C"}


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


def lvl(name) -> int:
    return LEVELS.index(name) if name in LEVELS else -1


def primary_report_number(rn) -> str | None:
    if not rn or not isinstance(rn, str) or rn.upper().startswith("UNKNOWN"):
        return None
    if rn.upper().startswith("UNCONFIRMED"):
        return None
    head = re.split(r"[;(]", rn)[0].strip().upper().replace("/", "-").replace(" ", "-")
    # Only report designators are compared (journal citations and free text are skipped).
    return head if re.fullmatch(r"[A-Z][A-Z0-9-]*\d[A-Z0-9-]*", head) else None


def validate_ema(owner: str, entries, rep: Report) -> None:
    rep.check(isinstance(entries, list), f"{owner}: existing_maverick_analysis must be a list")
    for e in entries or []:
        for f in EMA_FIELDS:
            rep.check(f in e, f"{owner}: existing_maverick_analysis entry missing '{f}'")
        rep.check(bool(e.get("path")) and bool(e.get("branch")) and bool(e.get("relationship")),
                  f"{owner}: existing_maverick_analysis needs path, branch and relationship")
        rep.check(e.get("level") in REPO_LEVELS, f"{owner}: existing_maverick_analysis level '{e.get('level')}'")


def validate_sources(index, rep: Report) -> dict:
    rep.check(index.get("schema") == SOURCE_SCHEMA, f"SOURCE_INDEX.json: schema id must be {SOURCE_SCHEMA}")
    sources = index.get("sources", [])
    rep.check(isinstance(sources, list) and sources, "SOURCE_INDEX.json: sources must be a non-empty list")
    by_id: dict = {}
    report_numbers: dict = {}
    ntrs_ids: dict = {}
    for s in sources:
        sid = s.get("source_id", "<missing>")
        for f in REQUIRED_SOURCE_FIELDS:
            rep.check(f in s, f"{sid}: missing field '{f}'")
        rep.check(bool(ID.match(sid)), f"{sid}: source_id must be upper-case alphanumeric/hyphen")
        rep.check(sid not in by_id, f"{sid}: duplicate source_id")
        by_id[sid] = s
        rn = primary_report_number(s.get("report_number"))
        if rn:
            rep.check(rn not in report_numbers, f"{sid}: report number '{rn}' duplicates {report_numbers.get(rn)}")
            report_numbers.setdefault(rn, sid)
        if s.get("ntrs_id"):
            rep.check(s["ntrs_id"] not in ntrs_ids, f"{sid}: NTRS ID {s['ntrs_id']} duplicates {ntrs_ids.get(s['ntrs_id'])}")
            ntrs_ids.setdefault(s["ntrs_id"], sid)
        rep.check(s.get("public_access_status") in ACCESS, f"{sid}: public_access_status '{s.get('public_access_status')}'")
        rep.check(s.get("retrieval_status") in RETRIEVAL, f"{sid}: retrieval_status '{s.get('retrieval_status')}'")
        rep.check(s.get("verification_level") in LEVELS, f"{sid}: verification_level '{s.get('verification_level')}'")
        rep.check(s.get("provenance_grade") in PROVENANCE, f"{sid}: provenance_grade '{s.get('provenance_grade')}'")
        rep.check(s.get("configuration_scope") in SCOPE, f"{sid}: configuration_scope '{s.get('configuration_scope')}'")
        packs = s.get("aircraft_packs", [])
        rep.check(bool(packs), f"{sid}: aircraft_packs empty")
        for p in packs:
            rep.check(p in PACKS, f"{sid}: aircraft_packs value '{p}'")
        rep.check(bool(s.get("index_group")), f"{sid}: index_group empty")
        if packs and not set(packs) & NON_AIRCRAFT_PACKS:
            rep.check(bool(s.get("configuration_ids")), f"{sid}: aircraft source without configuration identity (configuration_ids empty)")
            rep.check(bool(s.get("aircraft")), f"{sid}: aircraft source without 'aircraft'")
            for key in ("aircraft_variant", "research_configuration", "engine_configuration", "FCS_configuration", "date_or_phase"):
                rep.check(bool(s.get(key)), f"{sid}: aircraft source without '{key}' (write NOT_STATED explicitly)")
        contains = s.get("contains", {})
        rep.check(set(contains) == set(CONTAINS_KEYS), f"{sid}: contains keys must be exactly the schema list")
        for k, v in contains.items():
            rep.check(v in CONTAINS_VALUES, f"{sid}: contains.{k} = '{v}'")
        for key in ("implementation_usefulness", "validation_usefulness"):
            u = s.get(key, {})
            rep.check(isinstance(u, dict) and "rating" in u and "note" in u, f"{sid}: {key} needs rating+note")
        sha = s.get("sha256")
        rep.check(sha is None or bool(SHA256.match(sha)), f"{sid}: sha256 must be 64 lower-case hex or null")
        if s.get("verification_level") == "PAGE_VERIFIED":
            rep.check(str(s.get("retrieval_status", "")).startswith("RETRIEVED"), f"{sid}: PAGE_VERIFIED requires a retrieved document")
            rep.check(sha is not None, f"{sid}: PAGE_VERIFIED requires sha256 of the retrieved file")
        if str(s.get("public_access_status", "")).startswith("NOT_PUBLIC"):
            rep.check(s.get("implementation_usefulness", {}).get("rating") in {"PROHIBITED", "UNAVAILABLE"},
                      f"{sid}: non-public source must be PROHIBITED/UNAVAILABLE for implementation")
        if s.get("pdf_url"):
            rep.check(s.get("public_access_status") == "PUBLIC_NTRS_PDF_LISTED", f"{sid}: pdf_url only for PUBLIC_NTRS_PDF_LISTED")
        validate_ema(sid, s.get("existing_maverick_analysis"), rep)
    for sid, s in by_id.items():
        for rel in s.get("related_sources", []):
            rep.check(rel.get("source_id") in by_id, f"{sid}: related source '{rel.get('source_id')}' not in index")
            rep.check(bool(rel.get("relation")), f"{sid}: related source '{rel.get('source_id')}' missing relation")
    return by_id


def validate_known(files, sources: dict, rep: Report):
    all_cfg: dict = {}
    all_vals: dict = {}
    docs = {}
    for path in files:
        doc = load(path, rep)
        if doc is None:
            continue
        docs[path] = doc
        rel = path.relative_to(ROOT)
        rep.check(doc.get("schema") == KNOWN_SCHEMA, f"{rel}: schema id must be {KNOWN_SCHEMA}")
        rep.check(bool(doc.get("aircraft")), f"{rel}: aircraft missing")
        for c in doc.get("configurations", []):
            cid = c.get("configuration_id", "<missing>")
            for f in CONFIG_FIELDS:
                rep.check(f in c, f"{rel}:{cid}: configuration missing field '{f}'")
            for f in CONFIG_IDENTITY:
                rep.check(bool(c.get(f)), f"{rel}:{cid}: configuration identity field '{f}' empty")
            rep.check(c.get("configuration_scope") in SCOPE, f"{rel}:{cid}: configuration_scope '{c.get('configuration_scope')}'")
            rep.check(cid not in all_cfg, f"{rel}: configuration_id {cid} already defined in {all_cfg.get(cid)}")
            all_cfg[cid] = rel
    try:
        builder = load_builder()
        fields = builder.ALL_FIELDS
    except Exception as exc:  # noqa: BLE001
        rep.check(False, f"cannot import {BUILDER.name} ({exc})")
        fields = set()
    for path, doc in docs.items():
        rel = path.relative_to(ROOT)
        for v in doc.get("values", []):
            vid = v.get("value_id", "<missing>")
            for f in VALUE_FIELDS:
                rep.check(f in v, f"{rel}:{vid}: missing field '{f}'")
            rep.check(vid not in all_vals, f"{rel}: value_id {vid} already defined in {all_vals.get(vid)}")
            all_vals[vid] = rel
            rep.check(v.get("field") in fields, f"{rel}:{vid}: field '{v.get('field')}' not in the field vocabulary")
            rep.check(bool(v.get("configuration_ids")), f"{rel}:{vid}: configuration_ids empty (naked number)")
            for cid in v.get("configuration_ids", []):
                rep.check(cid in all_cfg, f"{rel}:{vid}: unknown configuration '{cid}'")
            rep.check(bool(v.get("units")), f"{rel}:{vid}: units missing")
            rep.check(bool(v.get("precision")), f"{rel}:{vid}: precision missing (use UNKNOWN explicitly)")
            rep.check(bool(v.get("location")), f"{rel}:{vid}: location missing (use NOT_CAPTURED explicitly)")
            rep.check(v.get("origin") in ORIGIN, f"{rel}:{vid}: origin '{v.get('origin')}'")
            rep.check(v.get("exactness") in EXACTNESS, f"{rel}:{vid}: exactness '{v.get('exactness')}'")
            rep.check(v.get("verification_level") in LEVELS, f"{rel}:{vid}: verification_level '{v.get('verification_level')}'")
            rep.check(v.get("implementation_use") in IMPLEMENTATION_USE, f"{rel}:{vid}: implementation_use '{v.get('implementation_use')}'")
            rep.check(bool(v.get("validation_use")), f"{rel}:{vid}: validation_use missing")
            rep.check(bool(v.get("reference_convention")), f"{rel}:{vid}: reference_convention missing")
            validate_ema(f"{rel}:{vid}", v.get("existing_maverick_analysis"), rep)
            if v.get("exactness") == "REPOSITORY_READING":
                rep.check(v.get("verification_level") == "SEARCH_LEAD_ONLY",
                          f"{rel}:{vid}: REPOSITORY_READING values stay SEARCH_LEAD_ONLY (a Maverick reading is not a primary source)")
                rep.check(bool(v.get("existing_maverick_analysis")), f"{rel}:{vid}: REPOSITORY_READING needs an existing_maverick_analysis entry")
            src = v.get("source_id")
            rep.check(bool(src), f"{rel}:{vid}: numeric field without source_id")
            if src == "UNRESOLVED":
                cands = v.get("candidate_source_ids", [])
                rep.check(bool(cands), f"{rel}:{vid}: UNRESOLVED source needs candidate_source_ids")
                for c in cands:
                    rep.check(c in sources, f"{rel}:{vid}: candidate source '{c}' not in index")
                rep.check(v.get("implementation_use") != "ALLOWED", f"{rel}:{vid}: UNRESOLVED source cannot be ALLOWED")
            elif src:
                rep.check(src in sources, f"{rel}:{vid}: source '{src}' not in index")
                if src in sources:
                    rep.check(lvl(v.get("verification_level")) <= lvl(sources[src].get("verification_level")),
                              f"{rel}:{vid}: value level {v.get('verification_level')} exceeds source level {sources[src].get('verification_level')}")
            if v.get("implementation_use") == "ALLOWED":
                s = sources.get(src, {})
                rep.check(s.get("verification_level") == "PAGE_VERIFIED", f"{rel}:{vid}: ALLOWED requires a PAGE_VERIFIED source")
                rep.check(v.get("verification_level") == "PAGE_VERIFIED", f"{rel}:{vid}: ALLOWED requires the value itself PAGE_VERIFIED")
                rep.check(v.get("location") not in (None, "", "NOT_CAPTURED"), f"{rel}:{vid}: ALLOWED requires a captured page/table/figure")
                rep.check(v.get("exactness") in PAGE_EXACTNESS, f"{rel}:{vid}: ALLOWED requires page-level exactness")
                rep.check(v.get("reference_convention") not in (None, "", "n/a"), f"{rel}:{vid}: ALLOWED requires a stated convention")
                rep.check(v.get("units") not in (None, "", "-"), f"{rel}:{vid}: ALLOWED requires units")
    for path, doc in docs.items():
        rel = path.relative_to(ROOT)
        for v in doc.get("values", []):
            for other in v.get("conflicts_with", []):
                rep.check(other in all_vals, f"{rel}:{v.get('value_id')}: conflicts_with '{other}' not found")
    return all_cfg, all_vals, docs


def validate_numeric_index(all_vals: dict, rep: Report) -> None:
    rep.check(NUMERIC_INDEX.exists(), "AIRCRAFT_NUMERIC_FIELD_INDEX.json missing")
    if not NUMERIC_INDEX.exists():
        return
    doc = load(NUMERIC_INDEX, rep)
    if doc is None:
        return
    recs = doc.get("records", [])
    ids = Counter(r.get("value_id") for r in recs)
    rep.check(set(ids) == set(all_vals), "AIRCRAFT_NUMERIC_FIELD_INDEX.json: records do not match the KNOWN_DATA values")
    rep.check(all(n == 1 for n in ids.values()), "AIRCRAFT_NUMERIC_FIELD_INDEX.json: duplicate value_id")
    for r in recs:
        vid = r.get("value_id")
        for f in NUMERIC_RECORD_FIELDS:
            rep.check(f in r, f"numeric index {vid}: missing '{f}'")
        rep.check(bool(r.get("source_id")), f"numeric index {vid}: numeric field without source_id")
        if r.get("implementation_allowed"):
            rep.check(r.get("verification_level") == "PAGE_VERIFIED" and r.get("source_verification_level") == "PAGE_VERIFIED" and bool(r.get("page")),
                      f"numeric index {vid}: implementation_allowed=true without page verification")
        if r.get("page") is not None:
            rep.check(r.get("verification_level") == "PAGE_VERIFIED", f"numeric index {vid}: page set without PAGE_VERIFIED")


def graph_documents():
    for p in sorted(LIB.rglob("*.md")):
        n = p.name.upper()
        if "SOURCE_GRAPH" in n or "LINEAGE" in n:
            yield p


def validate_graph_refs(sources: dict, cfgs: dict, vals: dict, rep: Report) -> None:
    known = set(sources) | set(cfgs) | set(vals) | GRAPH_ALLOW
    for p in graph_documents():
        text = p.read_text(encoding="utf-8")
        for tok in sorted(set(GRAPH_TOKEN.findall(text))):
            rep.check(tok in known, f"{p.relative_to(ROOT)}: graph references unknown ID '{tok}'")
        for tok in sorted(set(re.findall(r"`([A-Z0-9][A-Z0-9\-]{3,})`", text))):
            if "-" in tok and (tok.startswith(("FA18-", "F16-", "F15-", "F22-", "ENG-")) or GRAPH_TOKEN.fullmatch(tok)):
                rep.check(tok in known, f"{p.relative_to(ROOT)}: graph references unknown ID '{tok}'")


def validate_pack_docs(rep: Report) -> None:
    for kd in sorted((LIB / "Aircraft").glob("*/KNOWN_DATA.json")):
        folder = kd.parent
        for name in PACK_CORE_DOCS:
            rep.check((folder / name).exists(), f"{folder.relative_to(ROOT)}: missing {name}")
        rep.check(any((folder / n).exists() for n in PACK_FEASIBILITY_DOCS),
                  f"{folder.relative_to(ROOT)}: missing a feasibility document ({' or '.join(PACK_FEASIBILITY_DOCS)})")


def load_builder():
    sys.dont_write_bytecode = True  # importing the builder must not leave Tools/__pycache__ behind
    spec = importlib.util.spec_from_file_location("build_aerospace_source_views", BUILDER)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)  # type: ignore[union-attr]
    return mod


def validate_generated(rep: Report) -> None:
    try:
        builder = load_builder()
        for path, content in builder.render().items():
            current = path.read_text(encoding="utf-8") if path.exists() else None
            rep.check(current == content, f"{path.relative_to(ROOT)}: generated view is stale (run python3 {BUILDER.relative_to(ROOT)})")
    except SystemExit as exc:
        rep.check(False, f"generator failed: {exc}")
    except Exception as exc:  # noqa: BLE001
        rep.check(False, f"generator failed: {exc!r}")


def main() -> int:
    rep = Report()
    index = load(INDEX_JSON, rep)
    if index is None:
        print("\n".join(rep.errors))
        return 1
    sources = validate_sources(index, rep)
    files = sorted((LIB / "Aircraft").glob("*/KNOWN_DATA.json"))
    cfgs, vals, _docs = validate_known(files, sources, rep)
    for sid, s in sources.items():
        for cid in s.get("configuration_ids", []):
            rep.check(cid in cfgs, f"{sid}: configuration '{cid}' not defined in any Aircraft/*/KNOWN_DATA.json")
    validate_numeric_index(vals, rep)
    validate_generated(rep)
    validate_graph_refs(sources, cfgs, vals, rep)
    validate_pack_docs(rep)

    md = INDEX_MD.read_text(encoding="utf-8") if INDEX_MD.exists() else ""
    rep.check(bool(md), "SOURCE_INDEX.md missing")
    for sid in sources:
        rep.check(f"`{sid}`" in md, f"{sid}: not listed in SOURCE_INDEX.md")

    if rep.errors:
        print(f"FAIL: {len(rep.errors)} problem(s)")
        for e in rep.errors:
            print(" -", e)
        return 1
    levels = Counter(s["verification_level"] for s in sources.values())
    allowed = sum(1 for p in files for v in json.loads(p.read_text(encoding="utf-8"))["values"] if v["implementation_use"] == "ALLOWED")
    print(f"PASS: {len(sources)} sources, {len(cfgs)} configurations, {len(vals)} values in {len(files)} KNOWN_DATA file(s); "
          f"{allowed} value(s) ALLOWED for implementation")
    for lv in reversed(LEVELS):
        print(f"  {lv}: {levels.get(lv, 0)}")
    packs = Counter(p for s in sources.values() for p in s["aircraft_packs"])
    print("  packs: " + ", ".join(f"{p} {packs[p]}" for p in sorted(packs)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
