#!/usr/bin/env python3
"""Independent validator for the frozen NASA TP-1538 Table VI F-16 thrust data.

This validator deliberately does not import or call validate_f16_tp1538_thrust_deck.py.
It uses independent hard-coded grids, unit conversions, topology checks, and in-memory
mutation probes so it can serve as a second validation path.
"""

from __future__ import annotations

import copy
import csv
import hashlib
import json
import sys
from dataclasses import dataclass
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Dict, List, Mapping, Sequence, Tuple

ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "Docs" / "Reference" / "Data" / "F16" / "TP1538"
RAW_PATH = DATA_DIR / "table_vi_raw_source.csv"
DERIVED_PATH = DATA_DIR / "table_vi_si_converted.csv"
MANIFEST_PATH = DATA_DIR / "manifest.json"

EXPECTED_MACH = (Decimal("0.2"), Decimal("0.4"), Decimal("0.6"), Decimal("0.8"), Decimal("1.0"))
EXPECTED_ALT_M = (0, 3048, 6096, 9144, 12192, 15240)
EXPECTED_ALT_FT = (0, 10000, 20000, 30000, 40000, 50000)
EXPECTED_STATES = ("Tidle", "Tmil", "Tmax")
LBF_TO_N = Decimal("4.448")
FT_TO_M = Decimal("0.3048")

RAW_FIELDS = (
    "sourceSection",
    "mach",
    "sourceAltitude",
    "sourceAltitudeUnit",
    "idleThrustSource",
    "militaryThrustSource",
    "maximumThrustSource",
    "sourceForceUnit",
    "verificationPassA",
    "verificationPassB",
)
DERIVED_FIELDS = (
    "altitudeMeters",
    "mach",
    "idleThrustN",
    "militaryThrustN",
    "maximumThrustN",
)
STATE_COLUMNS = (
    ("Tidle", "idleThrustSource", "idleThrustN"),
    ("Tmil", "militaryThrustSource", "militaryThrustN"),
    ("Tmax", "maximumThrustSource", "maximumThrustN"),
)


class ValidationError(AssertionError):
    pass


@dataclass
class ValidationStats:
    raw_si_rows: int = 0
    raw_us_rows: int = 0
    physical_coordinates: int = 0
    thrust_cells_per_unit: int = 0
    cross_unit_total: int = 0
    cross_unit_matches: int = 0
    cross_unit_mismatches: int = 0
    max_pre_rounding_residual_n: Decimal = Decimal("0")
    negative_si_cells: int = 0
    negative_us_cells: int = 0
    negative_physical_coordinates: int = 0
    derived_rows: int = 0
    derived_cells: int = 0


def req(condition: bool, message: str) -> None:
    if not condition:
        raise ValidationError(message)


def read_csv(path: Path) -> Tuple[Tuple[str, ...], List[Dict[str, str]]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        fields = tuple(reader.fieldnames or ())
        rows = [dict(row) for row in reader]
    return fields, rows


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dec(text: str) -> Decimal:
    return Decimal(text)


def nearest_integer_n(value: Decimal) -> int:
    return int(value.quantize(Decimal("1"), rounding=ROUND_HALF_UP))


def by_coordinate(
    rows: Sequence[Mapping[str, str]], section: str
) -> Dict[Tuple[Decimal, int], Mapping[str, str]]:
    selected = [row for row in rows if row["sourceSection"] == section]
    result: Dict[Tuple[Decimal, int], Mapping[str, str]] = {}
    for row in selected:
        key = (dec(row["mach"]), int(row["sourceAltitude"]))
        req(key not in result, f"{section}: duplicate coordinate {key}")
        result[key] = row
    return result


def validate_manifest_structure(manifest: Mapping[str, object]) -> None:
    req(
        manifest.get("schema") == "maverick.f16.tp1538.thrust-deck-manifest.v0.1",
        "manifest schema",
    )
    req(manifest.get("status") == "FROZEN_DATA_ONLY_NOT_INTEGRATED", "manifest status")

    authority = manifest.get("authority")
    req(isinstance(authority, dict), "manifest authority missing")
    assert isinstance(authority, dict)
    req(authority.get("organization") == "NASA", "source organization")
    req(authority.get("reportNumber") == "NASA-TP-1538", "document id/report number")
    req(authority.get("ntrsRecordId") == "19800005879", "NTRS record id")
    req(authority.get("table") == "TABLE VI.- THRUST VALUES USED IN SIMULATION", "table identity")
    req(authority.get("pdfPage") == 99, "PDF page identity")
    req(authority.get("reportPage") == 93, "report page identity")
    req(authority.get("appendixContext") == "Appendix B, Engine Simulation", "appendix provenance")

    schema = manifest.get("sourceTableSchema")
    req(isinstance(schema, dict), "sourceTableSchema missing")
    assert isinstance(schema, dict)
    req(tuple(Decimal(v) for v in schema.get("machGrid", [])) == EXPECTED_MACH, "manifest Mach grid")

    si = schema.get("si")
    us = schema.get("usCustomary")
    req(isinstance(si, dict) and isinstance(us, dict), "manifest unit sections missing")
    assert isinstance(si, dict) and isinstance(us, dict)
    req(tuple(si.get("altitudeGrid", [])) == EXPECTED_ALT_M, "manifest SI altitude grid")
    req(tuple(us.get("altitudeGrid", [])) == EXPECTED_ALT_FT, "manifest US altitude grid")
    req(tuple(si.get("powerStates", [])) == EXPECTED_STATES, "manifest SI power states")
    req(tuple(us.get("powerStates", [])) == EXPECTED_STATES, "manifest US power states")
    req(si.get("altitudeUnit") == "m" and si.get("forceUnit") == "N", "manifest SI units")
    req(us.get("altitudeUnit") == "ft" and us.get("forceUnitPrinted") == "lb", "manifest US printed units")
    req(
        us.get("forceUnitInterpretation") == "pound-force (lbf), because the quantity is thrust",
        "manifest US force interpretation",
    )

    conversion = manifest.get("conversion")
    req(isinstance(conversion, dict), "manifest conversion policy missing")
    assert isinstance(conversion, dict)
    req(conversion.get("feetToMeters") == "0.3048", "manifest ft->m policy")
    req(
        conversion.get("poundsForceToNewtonsForSourceTableCrossCheck") == "4.448",
        "manifest historical lbf->N policy",
    )
    req(
        conversion.get("roundingForPrintedSiCrossCheck") == "nearest integer N, ROUND_HALF_UP",
        "manifest rounding policy",
    )

    transcription = manifest.get("transcription")
    req(isinstance(transcription, dict), "manifest transcription provenance missing")
    assert isinstance(transcription, dict)
    req(
        transcription.get("method") == "Manual visual transcription from original-page renders",
        "manifest transcription method",
    )
    req(transcription.get("passA") == "400 dpi PDFium render, PDF page 99/233", "manifest pass A provenance")
    req(
        transcription.get("passB") == "500 dpi Poppler/pdftoppm render, PDF page 99/233",
        "manifest pass B provenance",
    )

    visual = manifest.get("visualSourceAccess")
    req(isinstance(visual, dict), "visual source provenance missing")
    assert isinstance(visual, dict)
    req(
        "manually transcribed" in str(visual.get("numericSourceRule", "")).lower(),
        "numeric source rule does not preserve visual transcription authority",
    )

    integration = manifest.get("integration")
    req(isinstance(integration, dict), "integration declaration missing")
    assert isinstance(integration, dict)
    req(integration.get("connectedToProductionEngine") is False, "manifest claims production integration")
    req(
        integration.get("productionF16PhysicalThrustExpectedN") == 0,
        "manifest physical F-16 thrust expectation is not zero",
    )


def validate_semantics(
    raw_fields: Sequence[str],
    raw_rows: Sequence[Mapping[str, str]],
    derived_fields: Sequence[str],
    derived_rows: Sequence[Mapping[str, str]],
    *,
    lbf_to_n: Decimal = LBF_TO_N,
) -> ValidationStats:
    req(tuple(raw_fields) == RAW_FIELDS, "raw schema/order changed")
    req(tuple(derived_fields) == DERIVED_FIELDS, "derived schema/order changed")

    sections = {row["sourceSection"] for row in raw_rows}
    req(sections == {"SI", "US_CUSTOMARY"}, f"unexpected source sections: {sorted(sections)}")

    si_rows = [row for row in raw_rows if row["sourceSection"] == "SI"]
    us_rows = [row for row in raw_rows if row["sourceSection"] == "US_CUSTOMARY"]
    req(len(si_rows) == 30, f"SI row count {len(si_rows)} != 30")
    req(len(us_rows) == 30, f"US row count {len(us_rows)} != 30")

    req({dec(row["mach"]) for row in si_rows} == set(EXPECTED_MACH), "SI Mach grid")
    req({dec(row["mach"]) for row in us_rows} == set(EXPECTED_MACH), "US Mach grid")
    req({int(row["sourceAltitude"]) for row in si_rows} == set(EXPECTED_ALT_M), "SI altitude grid")
    req({int(row["sourceAltitude"]) for row in us_rows} == set(EXPECTED_ALT_FT), "US altitude grid")
    req(
        all(row["sourceAltitudeUnit"] == "m" and row["sourceForceUnit"] == "N" for row in si_rows),
        "SI row units",
    )
    req(
        all(row["sourceAltitudeUnit"] == "ft" and row["sourceForceUnit"] == "lb" for row in us_rows),
        "US row units",
    )

    expected_si_coords = {(mach, alt) for mach in EXPECTED_MACH for alt in EXPECTED_ALT_M}
    expected_us_coords = {(mach, alt) for mach in EXPECTED_MACH for alt in EXPECTED_ALT_FT}
    si = by_coordinate(raw_rows, "SI")
    us = by_coordinate(raw_rows, "US_CUSTOMARY")
    req(set(si) == expected_si_coords, "SI topology")
    req(set(us) == expected_us_coords, "US topology")

    matches = 0
    mismatches = 0
    max_residual = Decimal("0")
    negative_si = 0
    negative_us = 0
    negative_coords = set()

    for (mach, alt_ft), us_row in sorted(us.items()):
        alt_m_exact = Decimal(alt_ft) * FT_TO_M
        req(alt_m_exact == alt_m_exact.to_integral_value(), f"non-integral expected SI altitude for {alt_ft} ft")
        alt_m = int(alt_m_exact)
        req(alt_m in EXPECTED_ALT_M, f"altitude conversion {alt_ft} ft -> {alt_m} m outside grid")
        si_row = si[(mach, alt_m)]

        for state, raw_col, _ in STATE_COLUMNS:
            us_value = int(us_row[raw_col])
            si_value = int(si_row[raw_col])
            converted = Decimal(us_value) * lbf_to_n
            rounded = nearest_integer_n(converted)
            residual = abs(converted - Decimal(si_value))
            max_residual = max(max_residual, residual)
            if rounded == si_value:
                matches += 1
            else:
                mismatches += 1

            if si_value < 0:
                negative_si += 1
                req(state == "Tidle", f"negative SI non-idle cell at Mach {mach}, {alt_m} m, {state}")
                negative_coords.add((mach, alt_m))
            if us_value < 0:
                negative_us += 1
                req(state == "Tidle", f"negative US non-idle cell at Mach {mach}, {alt_ft} ft, {state}")

            req(
                (si_value < 0) == (us_value < 0),
                f"sign disagreement at Mach {mach}, {alt_ft} ft/{alt_m} m, {state}",
            )

    req(mismatches == 0, f"historical 4.448 source-table conversion mismatches: {mismatches}")
    req(matches == 90, f"historical source-table conversion matches: {matches} != 90")
    req(max_residual == Decimal("0.480"), f"max pre-rounding residual {max_residual} != 0.480 N")
    req(negative_si == 12, f"negative SI source cells {negative_si} != 12")
    req(negative_us == 12, f"negative US source cells {negative_us} != 12")
    req(len(negative_coords) == 12, f"negative physical coordinates {len(negative_coords)} != 12")

    req(len(derived_rows) == 30, f"derived row count {len(derived_rows)} != 30")
    derived: Dict[Tuple[Decimal, int], Mapping[str, str]] = {}
    for row in derived_rows:
        mach = dec(row["mach"])
        alt_dec = dec(row["altitudeMeters"])
        req(alt_dec == alt_dec.to_integral_value(), f"derived non-integral altitude {alt_dec}")
        key = (mach, int(alt_dec))
        req(key not in derived, f"derived duplicate coordinate {key}")
        derived[key] = row
    req(set(derived) == expected_si_coords, "derived topology")

    for (mach, alt_ft), us_row in us.items():
        alt_m = int(Decimal(alt_ft) * FT_TO_M)
        row = derived[(mach, alt_m)]
        for _, raw_col, derived_col in STATE_COLUMNS:
            expected = (Decimal(us_row[raw_col]) * LBF_TO_N).quantize(Decimal("0.001"))
            actual = Decimal(row[derived_col])
            req(
                actual == expected,
                f"derived value mismatch Mach {mach}, {alt_m} m, {derived_col}: {actual} != {expected}",
            )

    return ValidationStats(
        raw_si_rows=len(si_rows),
        raw_us_rows=len(us_rows),
        physical_coordinates=len(si),
        thrust_cells_per_unit=len(si_rows) * len(STATE_COLUMNS),
        cross_unit_total=90,
        cross_unit_matches=matches,
        cross_unit_mismatches=mismatches,
        max_pre_rounding_residual_n=max_residual,
        negative_si_cells=negative_si,
        negative_us_cells=negative_us,
        negative_physical_coordinates=len(negative_coords),
        derived_rows=len(derived_rows),
        derived_cells=len(derived_rows) * 3,
    )


def validate_hashes(manifest: Mapping[str, object]) -> Tuple[str, str]:
    files = manifest.get("files")
    req(isinstance(files, dict), "manifest files block missing")
    assert isinstance(files, dict)
    raw_meta = files.get("rawSourceCsv")
    derived_meta = files.get("siConvertedCsv")
    req(isinstance(raw_meta, dict) and isinstance(derived_meta, dict), "manifest file metadata missing")
    assert isinstance(raw_meta, dict) and isinstance(derived_meta, dict)

    raw_sha = sha256_file(RAW_PATH)
    derived_sha = sha256_file(DERIVED_PATH)
    req(raw_sha == raw_meta.get("sha256"), f"raw SHA-256 mismatch: {raw_sha} != {raw_meta.get('sha256')}")
    req(
        derived_sha == derived_meta.get("sha256"),
        f"derived SHA-256 mismatch: {derived_sha} != {derived_meta.get('sha256')}",
    )
    return raw_sha, derived_sha


def expect_mutation_failure(name: str, action) -> str:
    try:
        action()
    except ValidationError as exc:
        return f"{name}: CAUGHT ({exc})"
    raise ValidationError(f"{name}: mutation survived validation")


def run_mutations(
    raw_fields: Sequence[str],
    raw_rows: Sequence[Mapping[str, str]],
    derived_fields: Sequence[str],
    derived_rows: Sequence[Mapping[str, str]],
) -> List[str]:
    reports: List[str] = []

    def m1() -> None:
        mutated = copy.deepcopy(list(raw_rows))
        row = next(
            r
            for r in mutated
            if r["sourceSection"] == "US_CUSTOMARY" and r["mach"] == "0.2" and r["sourceAltitude"] == "0"
        )
        row["militaryThrustSource"] = str(int(row["militaryThrustSource"]) + 1)
        validate_semantics(raw_fields, mutated, derived_fields, derived_rows)

    reports.append(expect_mutation_failure("M1 +1 thrust cell", m1))

    def m2() -> None:
        mutated = copy.deepcopy(list(raw_rows))
        row = next(r for r in mutated if r["sourceSection"] == "US_CUSTOMARY" and int(r["idleThrustSource"]) < 0)
        row["idleThrustSource"] = str(abs(int(row["idleThrustSource"])))
        validate_semantics(raw_fields, mutated, derived_fields, derived_rows)

    reports.append(expect_mutation_failure("M2 negative idle -> positive", m2))

    def m3() -> None:
        mutated = copy.deepcopy(list(raw_rows))
        a = next(
            r
            for r in mutated
            if r["sourceSection"] == "US_CUSTOMARY" and r["mach"] == "0.2" and r["sourceAltitude"] == "0"
        )
        b = next(
            r
            for r in mutated
            if r["sourceSection"] == "US_CUSTOMARY" and r["mach"] == "0.4" and r["sourceAltitude"] == "0"
        )
        a["mach"], b["mach"] = b["mach"], a["mach"]
        validate_semantics(raw_fields, mutated, derived_fields, derived_rows)

    reports.append(expect_mutation_failure("M3 swap Mach coordinates", m3))

    reports.append(
        expect_mutation_failure(
            "M4 conversion factor 4.449",
            lambda: validate_semantics(
                raw_fields,
                raw_rows,
                derived_fields,
                derived_rows,
                lbf_to_n=Decimal("4.449"),
            ),
        )
    )

    def m5() -> None:
        mutated = copy.deepcopy(list(raw_rows))
        del mutated[-1]
        validate_semantics(raw_fields, mutated, derived_fields, derived_rows)

    reports.append(expect_mutation_failure("M5 remove row", m5))
    return reports


def main() -> int:
    raw_fields, raw_rows = read_csv(RAW_PATH)
    derived_fields, derived_rows = read_csv(DERIVED_PATH)
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))

    validate_manifest_structure(manifest)
    stats = validate_semantics(raw_fields, raw_rows, derived_fields, derived_rows)
    raw_sha, derived_sha = validate_hashes(manifest)
    mutation_reports = run_mutations(raw_fields, raw_rows, derived_fields, derived_rows)

    print("F-16 TP-1538 independent data validation: PASS")
    print(f"grid: Mach={','.join(str(v) for v in EXPECTED_MACH)}")
    print(f"altitude SI={','.join(str(v) for v in EXPECTED_ALT_M)} m")
    print(f"altitude US={','.join(str(v) for v in EXPECTED_ALT_FT)} ft")
    print(f"states={','.join(EXPECTED_STATES)}")
    print(
        f"rows: SI={stats.raw_si_rows} US={stats.raw_us_rows} "
        f"physical_coordinates={stats.physical_coordinates} "
        f"thrust_cells_per_unit={stats.thrust_cells_per_unit}"
    )
    print(
        f"cross-unit: total={stats.cross_unit_total} matches={stats.cross_unit_matches} "
        f"mismatches={stats.cross_unit_mismatches} "
        f"max_pre_rounding_residual_N={stats.max_pre_rounding_residual_n}"
    )
    print(
        f"negative source cells: SI={stats.negative_si_cells} US={stats.negative_us_cells} "
        f"physical_coordinates={stats.negative_physical_coordinates}; states=Tidle only"
    )
    print(f"raw_sha256={raw_sha}")
    print(f"derived_sha256={derived_sha}")
    for report in mutation_reports:
        print(report)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValidationError as exc:
        print(f"F-16 TP-1538 independent data validation: FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
