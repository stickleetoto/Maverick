#!/usr/bin/env python3
"""Generate the derived views of the Maverick aerospace source library.

Canonical data lives in two places only:

* Docs/Research/Aerospace/SOURCE_INDEX.json          (every source record)
* Docs/Research/Aerospace/Aircraft/*/KNOWN_DATA.json (configurations and values)

Everything below is generated from them and must never be edited by hand:

* Aircraft/<PACK>/SOURCES.json            per-pack slice of SOURCE_INDEX.json
* AIRCRAFT_NUMERIC_FIELD_INDEX.json       flat index of every candidate numeric value
* the table blocks between GENERATED markers in SOURCE_INDEX.md and in each
  Aircraft/<PACK>/KNOWN_DATA.md

Usage:
    python3 Tools/build_aerospace_source_views.py          # write the views
    python3 Tools/build_aerospace_source_views.py --check  # exit 1 if any view is stale

Standard library only.
"""
from __future__ import annotations

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
GENERATOR = "Tools/build_aerospace_source_views.py"

PACK_FOLDERS = {"FA18": "FA18", "F16": "F16", "F15": "F15", "F22": "F22"}
PACK_ORDER = ["FA18", "F16", "F15", "F22", "F14", "FUNDAMENTALS", "OTHER"]
LEVELS = ["SEARCH_LEAD_ONLY", "CATALOGUE_VERIFIED", "ABSTRACT_VERIFIED", "CONTENT_EXTRACT_VERIFIED", "PAGE_VERIFIED"]
LEVEL_SHORT = {"SEARCH_LEAD_ONLY": "LEAD", "CATALOGUE_VERIFIED": "CAT", "ABSTRACT_VERIFIED": "ABS",
               "CONTENT_EXTRACT_VERIFIED": "EXTRACT", "PAGE_VERIFIED": "**PAGE**"}
ACCESS_SHORT = {
    "PUBLIC_NTRS_PDF_LISTED": "NTRS PDF", "PUBLIC_NTRS_CITATION_ONLY": "NTRS cit.", "PUBLIC_NTRS_RECORD_ID_UNCONFIRMED": "NASA, ID?",
    "PUBLIC_WEB_PAGE": "web", "PUBLIC_DISTRIBUTION_A": "Dist A", "PUBLIC_UNIVERSITY_REPOSITORY": "univ.", "PUBLIC_AUTHOR_HOSTED": "author",
    "PUBLIC_RELEASE_PAYWALLED": "paywall", "NOT_PUBLIC_DISTRIBUTION_LIMITED": "**NOT PUBLIC**", "NOT_PUBLIC_PROPRIETARY": "**PROPRIETARY**",
    "NOT_PUBLICLY_LOCATED": "**NOT LOCATED**",
}
PROV_SHORT = {"ORIGINAL_PRIMARY": "PRIMARY", "PUBLIC_REPRODUCTION": "REPRO", "DERIVED_SIMULATOR": "DERIVED_SIM", "CURVE_FIT": "CURVE_FIT",
              "DIGITIZED_PRIMARY_FIGURE": "DIGITIZED", "CROSS_VALIDATION_ONLY": "XVAL_ONLY", "BACKGROUND_ONLY": "BACKGROUND"}
SCOPE_SHORT = {"EXACT_AIRFRAME": "EXACT", "PRODUCTION_FAMILY": "PROD_FAMILY", "PREPRODUCTION": "PREPROD", "RESEARCH_MODIFIED": "RESEARCH_MOD",
               "WIND_TUNNEL_MODEL": "TUNNEL", "SIMULATION_ONLY": "SIM_ONLY", "UNKNOWN": "—"}
FIELD_GROUPS = [
    ("Geometry and reference quantities", {"reference_area", "reference_span", "mean_aerodynamic_chord", "physical_span", "length", "height",
                                           "cg_reference", "model_scale"}),
    ("Mass properties", {"mass", "cg", "Ixx", "Iyy", "Izz", "Ixz", "fuel_mass", "mass_increment"}),
    ("Controls, actuators and FCS", {"surface_position_limit", "surface_rate_limit", "actuator_dynamics", "fcs_gearing", "fcs_schedule_boundary",
                                     "fcs_design_target", "fcs_frame_rate"}),
    ("Aerodynamic-data domains and envelopes", {"aero_domain_alpha", "aero_domain_beta", "aero_domain_mach", "aero_configuration_state", "flight_envelope"}),
    ("Propulsion", {"thrust_table", "thrust_class", "throttle_map", "engine_dynamics", "engine_angular_momentum", "engine_airflow", "nozzle_vectoring_limit"}),
    ("Accuracy statements", {"model_accuracy"}),
]
ALL_FIELDS = set().union(*[g[1] for g in FIELD_GROUPS])


def begin(name):
    return f"<!-- BEGIN GENERATED: {name} ({GENERATOR}; do not edit by hand) -->"


def end(name):
    return f"<!-- END GENERATED: {name} -->"


def cell(x) -> str:
    s = "—" if x is None or x == "" or x == [] else (", ".join(str(i) for i in x) if isinstance(x, list) else str(x))
    return s.replace("|", "/").replace("\n", " ")


def clip(s: str, n: int) -> str:
    return s if len(s) <= n else s[: n - 1].rstrip() + "…"


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def dump_json(obj) -> str:
    return json.dumps(obj, indent=1, ensure_ascii=False) + "\n"


def known_files():
    return sorted((LIB / "Aircraft").glob("*/KNOWN_DATA.json"))


# ------------------------------------------------------------------ per-pack SOURCES.json
def pack_sources(index) -> dict[Path, str]:
    out = {}
    for pack, folder in PACK_FOLDERS.items():
        recs = [s for s in index["sources"] if pack in s["aircraft_packs"]]
        doc = {
            "schema": "maverick.aerospace.pack-sources.v1",
            "generated_by": GENERATOR,
            "generated_from": "Docs/Research/Aerospace/SOURCE_INDEX.json",
            "library_revision": index["library_revision"],
            "pack": pack,
            "note": "Generated slice of SOURCE_INDEX.json (records whose aircraft_packs contains this pack). Edit SOURCE_INDEX.json, then regenerate.",
            "source_count": len(recs),
            "verification_levels": dict(Counter(s["verification_level"] for s in recs)),
            "sources": recs,
        }
        out[LIB / "Aircraft" / folder / "SOURCES.json"] = dump_json(doc)
    return out


# ------------------------------------------------------------------ numeric field index
def numeric_index(index) -> str:
    sources = {s["source_id"]: s for s in index["sources"]}
    records = []
    for path in known_files():
        doc = load_json(path)
        cfg_scope = {c["configuration_id"]: c.get("configuration_scope", "UNKNOWN") for c in doc["configurations"]}
        for v in doc["values"]:
            src = sources.get(v["source_id"])
            page_ok = (src is not None and src["verification_level"] == "PAGE_VERIFIED" and v.get("verification_level") == "PAGE_VERIFIED")
            records.append({
                "value_id": v["value_id"],
                "aircraft": doc["aircraft"],
                "configuration": v["configuration_ids"],
                "configuration_scope": sorted({cfg_scope.get(c, "UNKNOWN") for c in v["configuration_ids"]}),
                "field": v["field"],
                "quantity": v["quantity"],
                "value": v["value"],
                "units": v["units"],
                "precision": v["precision"],
                "source_id": v["source_id"],
                "candidate_source_ids": v["candidate_source_ids"],
                "page": v["location"] if page_ok else None,
                "location_as_recorded": v["location"],
                "provenance": src["provenance_grade"] if src else "UNRESOLVED",
                "value_origin": v["origin"],
                "exactness": v["exactness"],
                "verification_level": v["verification_level"],
                "source_verification_level": src["verification_level"] if src else None,
                "implementation_use": v["implementation_use"],
                "implementation_allowed": v["implementation_use"] == "ALLOWED",
                "has_existing_maverick_analysis": bool(v.get("existing_maverick_analysis")),
                "known_data_file": str(path.relative_to(ROOT)),
            })
    doc = {
        "schema": "maverick.aerospace.numeric-field-index.v1",
        "generated_by": GENERATOR,
        "library_revision": index["library_revision"],
        "rule": ("implementation_allowed is true only when implementation_use is ALLOWED, which the validator permits only for a value whose "
                 "source content was inspected by the library at PAGE_VERIFIED, with configuration, units, convention and page/table/figure recorded. "
                 "'page' is null unless both the source and the value are PAGE_VERIFIED; 'location_as_recorded' may carry a repository or extract locator "
                 "that the library has not checked."),
        "summary": {
            "records": len(records),
            "implementation_allowed": sum(r["implementation_allowed"] for r in records),
            "by_aircraft": dict(Counter(r["aircraft"] for r in records)),
            "by_verification_level": dict(Counter(r["verification_level"] for r in records)),
            "by_implementation_use": dict(Counter(r["implementation_use"] for r in records)),
            "with_existing_maverick_analysis": sum(r["has_existing_maverick_analysis"] for r in records),
        },
        "records": records,
    }
    return dump_json(doc)


# ------------------------------------------------------------------ markdown blocks
def replace_block(text: str, name: str, body: str) -> str:
    pat = re.compile(re.escape(begin(name)) + r".*?" + re.escape(end(name)), re.S)
    if not pat.search(text):
        raise SystemExit(f"missing GENERATED markers '{name}'")
    return pat.sub(lambda _m: begin(name) + "\n" + body.rstrip() + "\n" + end(name), text)


def source_link(s) -> str:
    u = s["public_url"] or s["mirror_url"]
    label = s["report_number"] or s["source_id"]
    label = clip(label, 60)
    return f"[{cell(label)}]({u})" if u else cell(label)


def short_cfg(ids) -> str:
    if not ids:
        return "—"
    return ", ".join(re.sub(r"^(FA18|F16|F15|F22)-CFG-", "", c) for c in ids)


def index_md_blocks(index) -> dict[str, str]:
    srcs = index["sources"]
    lv = Counter(s["verification_level"] for s in srcs)
    rows = ["| Metric | Value |", "|---|---|", f"| Sources indexed | {len(srcs)} |"]
    for p in PACK_ORDER:
        n = sum(p in s["aircraft_packs"] for s in srcs)
        if n:
            rows.append(f"| Pack `{p}` (a source can belong to several packs) | {n} |")
    rows.append("| " + " / ".join(LEVEL_SHORT[l].strip("*") for l in reversed(LEVELS)) + " | "
                + " / ".join(str(lv.get(l, 0)) for l in reversed(LEVELS)) + " |")
    rows.append(f"| Not public or not located | {sum(s['public_access_status'].startswith('NOT_') for s in srcs)} |")
    rows.append(f"| Sources with an `existing_maverick_analysis` cross-reference | {sum(bool(s['existing_maverick_analysis']) for s in srcs)} |")
    summary = "\n".join(rows)

    groups: dict[str, list] = {}
    for s in srcs:
        groups.setdefault(s["index_group"], []).append(s)

    def gkey(g):
        first = groups[g][0]["aircraft_packs"][0]
        return (PACK_ORDER.index(first) if first in PACK_ORDER else 99, g)

    out = []
    for g in sorted(groups, key=gkey):
        out.append(f"### {g}\n")
        out.append("| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Level | Repo x-ref | Impl. | Valid. |")
        out.append("|---|---|---|---|---|---|---|---|---|---|---|---|")
        for s in groups[g]:
            out.append("| " + " | ".join([
                f"`{s['source_id']}`", cell(clip(s["title"], 90)), cell(s["year"]), source_link(s), cell(short_cfg(s["configuration_ids"])),
                PROV_SHORT[s["provenance_grade"]], SCOPE_SHORT[s["configuration_scope"]], ACCESS_SHORT[s["public_access_status"]],
                LEVEL_SHORT[s["verification_level"]], str(len(s["existing_maverick_analysis"])) if s["existing_maverick_analysis"] else "—",
                cell(s["implementation_usefulness"]["rating"]), cell(s["validation_usefulness"]["rating"])]) + " |")
        out.append("")
    return {"source-summary": summary, "source-tables": "\n".join(out)}


def known_md_blocks(doc) -> dict[str, str]:
    c_rows = ["| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Scope | Lineage | Status |",
              "|---|---|---|---|---|---|---|---|---|---|"]
    for c in doc["configurations"]:
        serial = c.get("serial") or "n/a"
        af = c["airframe"] if serial in ("n/a", None, "") else f"{c['airframe']}; **{serial}**"
        c_rows.append("| " + " | ".join([f"`{c['configuration_id']}`", cell(c["label"]), cell(af), cell(c["engine"]), cell(c["fcs"]),
                                          cell(c["research_hardware"]), cell(c["dates"]), SCOPE_SHORT.get(c.get("configuration_scope", "UNKNOWN"), "—"),
                                          cell(c.get("lineage_family")), cell(c["status"])]) + " |")
    v_out = []
    for title, fields in FIELD_GROUPS:
        vals = [v for v in doc["values"] if v["field"] in fields]
        if not vals:
            continue
        v_out.append(f"### {title}\n")
        v_out.append("| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |")
        v_out.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        for v in vals:
            src = v["source_id"] if v["source_id"] != "UNRESOLVED" else "UNRESOLVED: " + ", ".join(v["candidate_source_ids"])
            val = v["value"]
            if isinstance(val, (int, float)) and not isinstance(val, bool):
                val = f"{val:,}" if isinstance(val, int) else str(val)
            v_out.append("| " + " | ".join([
                f"`{v['value_id']}`", cell(clip(v["quantity"], 70)), f"**{cell(val)}**", cell(v["units"]), cell(clip(v["precision"], 40)),
                cell(short_cfg(v["configuration_ids"])), cell(src), cell(clip(v["location"], 70)), v["exactness"], LEVEL_SHORT[v["verification_level"]],
                v["implementation_use"], "yes" if v.get("existing_maverick_analysis") else "—", cell(v["conflicts_with"])]) + " |")
        v_out.append("")
    return {"configurations": "\n".join(c_rows), "values": "\n".join(v_out)}


# ------------------------------------------------------------------ driver
def render() -> dict[Path, str]:
    index = load_json(INDEX_JSON)
    out = pack_sources(index)
    out[NUMERIC_INDEX] = numeric_index(index)
    text = INDEX_MD.read_text(encoding="utf-8")
    for name, body in index_md_blocks(index).items():
        text = replace_block(text, name, body)
    out[INDEX_MD] = text
    for path in known_files():
        md = path.with_suffix(".md")
        if not md.exists():
            continue
        text = md.read_text(encoding="utf-8")
        for name, body in known_md_blocks(load_json(path)).items():
            text = replace_block(text, name, body)
        out[md] = text
    return out


def main(argv) -> int:
    check = "--check" in argv
    stale = []
    for path, content in render().items():
        current = path.read_text(encoding="utf-8") if path.exists() else None
        if current != content:
            stale.append(path)
            if not check:
                path.write_text(content, encoding="utf-8")
    if check:
        if stale:
            print("STALE generated views (run python3 Tools/build_aerospace_source_views.py):")
            for p in stale:
                print(" -", p.relative_to(ROOT))
            return 1
        print("generated views are up to date")
        return 0
    print(f"wrote {len(stale)} file(s)" if stale else "no changes")
    for p in stale:
        print(" -", p.relative_to(ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
