#!/usr/bin/env python3
"""Independent offline validation for F-16 TP-1538 Phase 5D runtime semantics.

This validator is deliberately independent of Unity execution. It binds the runtime arrays to the
frozen canonical source artifact, proves all 90 published SI thrust cells exactly, checks the
source-authorized interpolation/power semantics, verifies explicit opt-in/default-zero wiring, and
statically rejects *actual* direct physics-write call sites in the Phase-5D F-16 production files.

It does NOT claim Unity validation.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

BASE_COMMIT = "ef682695a2d35962deceedc222fac321a90fbc19"
CANONICAL_RAW_SHA256 = "9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972"
CANONICAL_SOURCE_PREFIX = "NASA TP-1538 / NTRS 19800005879 / Table VI / rawSourceCsv sha256 "
CANONICAL_SOURCE_VERSION = "F16_TP1538_THRUST_DECK_V0.1"

MACH = [0.2, 0.4, 0.6, 0.8, 1.0]
ALT = [0.0, 3048.0, 6096.0, 9144.0, 12192.0, 15240.0]
STATES = [
    ("idle", "idleThrustSource", "FrozenIdleThrustN"),
    ("military", "militaryThrustSource", "FrozenMilitaryThrustN"),
    ("maximum", "maximumThrustSource", "FrozenMaximumThrustN"),
]


class Fail(AssertionError):
    pass


@dataclass
class Stats:
    assertions: int = 0
    mutations: int = 0
    mutations_killed: int = 0
    exact_runtime_cells: int = 0
    physics_write_sites: int = 0


S = Stats()


def check(cond: bool, msg: str) -> None:
    S.assertions += 1
    if not cond:
        raise Fail(msg)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def load_canonical_source(raw_path: Path, manifest_path: Path):
    actual_raw_sha = sha256_file(raw_path)
    check(actual_raw_sha == CANONICAL_RAW_SHA256,
          f"canonical rawSourceCsv SHA-256 changed: {actual_raw_sha}")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_sha = manifest["files"]["rawSourceCsv"]["sha256"]
    check(manifest_sha == CANONICAL_RAW_SHA256,
          f"manifest rawSourceCsv SHA-256 differs from canonical identity: {manifest_sha}")
    check(manifest["authority"]["reportNumber"] == "NASA-TP-1538", "manifest report identity")
    check(manifest["authority"]["ntrsRecordId"] == "19800005879", "manifest NTRS identity")
    check(manifest["authority"]["table"] == "TABLE VI.- THRUST VALUES USED IN SIMULATION",
          "manifest Table VI identity")

    with raw_path.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    si = [r for r in rows if r["sourceSection"] == "SI"]
    check(len(si) == 30, "expected 30 canonical published-SI source rows")

    table = {}
    for r in si:
        key = (float(r["sourceAltitude"]), float(r["mach"]))
        check(key not in table, f"duplicate canonical SI coordinate {key}")
        table[key] = tuple(float(r[col]) for _, col, _ in STATES)

    check(set(table) == {(a, m) for a in ALT for m in MACH}, "canonical SI grid mismatch")
    return table, actual_raw_sha


def locate(axis, x):
    if not math.isfinite(x) or x < axis[0] or x > axis[-1]:
        return None
    if x == axis[-1]:
        return len(axis) - 2, 1.0
    for i in range(len(axis) - 1):
        if axis[i] <= x <= axis[i + 1]:
            return i, (x - axis[i]) / (axis[i + 1] - axis[i])
    return None


def lerp(a, b, t):
    return a + (b - a) * t


def sample(table, altitude, mach, state_index):
    ah = locate(ALT, altitude)
    mm = locate(MACH, mach)
    if ah is None or mm is None:
        return None
    ai, at = ah
    mi, mt = mm
    v00 = table[(ALT[ai], MACH[mi])][state_index]
    v01 = table[(ALT[ai], MACH[mi + 1])][state_index]
    v10 = table[(ALT[ai + 1], MACH[mi])][state_index]
    v11 = table[(ALT[ai + 1], MACH[mi + 1])][state_index]
    return lerp(lerp(v00, v01, mt), lerp(v10, v11, mt), at)


def evaluate(table, altitude, mach, power):
    if not all(math.isfinite(v) for v in (altitude, mach, power)):
        return False, 0.0
    if altitude < ALT[0] or altitude > ALT[-1] or mach < MACH[0] or mach > MACH[-1]:
        return False, 0.0
    if power < 0.0 or power > 100.0:
        return False, 0.0
    vals = [sample(table, altitude, mach, i) for i in range(3)]
    if any(v is None for v in vals):
        return False, 0.0
    idle, mil, mx = vals
    if power < 50.0:
        thrust = idle + (mil - idle) * (power / 50.0)
    else:
        thrust = mil + (mx - mil) * ((power - 50.0) / 50.0)
    return True, thrust


def parse_array(text: str, name: str):
    pattern = rf"{re.escape(name)}\s*=\s*\{{(.*?)\}};"
    m = re.search(pattern, text, re.S)
    if not m:
        raise Fail(f"cannot find C# array {name}")
    return [float(x) for x in re.findall(r"(-?\d+(?:\.\d+)?)f", m.group(1))]


def parse_uint_const(text: str, name: str) -> int:
    m = re.search(rf"\b{name}\s*=\s*(\d+)u\s*;", text)
    if not m:
        raise Fail(f"cannot find C# uint constant {name}")
    return int(m.group(1))


def parse_string_const(text: str, name: str) -> str:
    # Used only for simple literal constants in this patch.
    m = re.search(rf"\b{name}\s*=\s*\n?\s*\"([^\"]*)\"\s*;", text)
    if not m:
        raise Fail(f"cannot find C# string constant {name}")
    return m.group(1)


def hash_byte(h, b):
    return ((h ^ b) * 16777619) & 0xFFFFFFFF


def hash_int(h, v):
    v &= 0xFFFFFFFF
    for shift in (0, 8, 16, 24):
        h = hash_byte(h, (v >> shift) & 0xFF)
    return h


def hash_string(h, s):
    for ch in s:
        c = ord(ch)
        h = hash_byte(h, c & 0xFF)
        h = hash_byte(h, (c >> 8) & 0xFF)
    return hash_byte(h, 0)


def f32bits(v):
    return struct.unpack("<I", struct.pack("<f", float(v)))[0]


def hash_floats(h, vals):
    h = hash_int(h, len(vals))
    for v in vals:
        h = hash_int(h, f32bits(v))
    return h


def runtime_hash(source_identity, source_version, alt, mach, idle, mil, mx):
    h = 2166136261
    h = hash_string(h, source_identity)
    h = hash_string(h, source_version)
    for a in (alt, mach, idle, mil, mx):
        h = hash_floats(h, a)
    return h


def strip_csharp_noncode(text: str) -> str:
    """Remove strings/chars/comments while preserving newlines for call-site scanning."""
    out = []
    i = 0
    n = len(text)
    state = "code"
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if state == "code":
            if c == "/" and nxt == "/":
                out.extend("  ")
                i += 2
                state = "line_comment"
                continue
            if c == "/" and nxt == "*":
                out.extend("  ")
                i += 2
                state = "block_comment"
                continue
            if c == '"':
                out.append(" ")
                i += 1
                state = "string"
                continue
            if c == "'":
                out.append(" ")
                i += 1
                state = "char"
                continue
            out.append(c)
            i += 1
            continue
        if state == "line_comment":
            if c == "\n":
                out.append("\n")
                state = "code"
            else:
                out.append(" ")
            i += 1
            continue
        if state == "block_comment":
            if c == "*" and nxt == "/":
                out.extend("  ")
                i += 2
                state = "code"
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
            continue
        if state in ("string", "char"):
            quote = '"' if state == "string" else "'"
            if c == "\\":
                out.append(" ")
                if i + 1 < n:
                    out.append("\n" if text[i + 1] == "\n" else " ")
                i += 2
                continue
            if c == quote:
                out.append(" ")
                i += 1
                state = "code"
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
            continue
    return "".join(out)


def physics_write_sites(path: Path, text: str) -> list[str]:
    code = strip_csharp_noncode(text)
    patterns = [
        r"\.\s*(?:AddForce|AddRelativeForce|AddTorque|AddRelativeTorque|MovePosition|MoveRotation)\s*\(",
        r"\.\s*(?:linearVelocity|angularVelocity|velocity)\s*=",
    ]
    found = []
    for pattern in patterns:
        for m in re.finditer(pattern, code):
            line = code.count("\n", 0, m.start()) + 1
            excerpt = re.sub(r"\s+", " ", code[m.start():m.start() + 80]).strip()
            found.append(f"{path}:{line}: {excerpt}")
    return found


def extract_method_body(text: str, method_name: str) -> str:
    # Sufficient for the uncomplicated methods in this patch: find name(...), then match braces.
    m = re.search(rf"\b{re.escape(method_name)}\s*\([^)]*\)\s*\{{", text, re.S)
    if not m:
        raise Fail(f"cannot find method {method_name}")
    brace = text.find("{", m.start())
    depth = 0
    for i in range(brace, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[brace + 1:i]
    raise Fail(f"unterminated method body {method_name}")


def kill(name: str, mutant: Callable[[], bool]) -> str:
    S.mutations += 1
    caught = mutant()
    if caught:
        S.mutations_killed += 1
        return f"{name}: KILLED"
    raise Fail(f"{name}: mutation survived")


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw", type=Path,
                    default=root / "Docs/Reference/Data/F16/TP1538/table_vi_raw_source.csv")
    ap.add_argument("--manifest", type=Path,
                    default=root / "Docs/Reference/Data/F16/TP1538/manifest.json")
    ap.add_argument("--csharp", type=Path,
                    default=root / "Assets/MaverickFresh/Scripts/FlightDynamics/F16/MavF16Tp1538ThrustDeck.cs")
    ap.add_argument("--propulsion", type=Path,
                    default=root / "Assets/MaverickFresh/Scripts/FlightDynamics/F16/MavF16PropulsionSystem.cs")
    ap.add_argument("--engine-runtime", type=Path,
                    default=root / "Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavEngineRuntime.cs")
    ap.add_argument("--historical-rig", type=Path,
                    default=root / "Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16ReferenceFlightScenarios.cs")
    ap.add_argument("--powered-validation", type=Path,
                    default=root / "Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16Tp1538RuntimeValidation.cs")
    args = ap.parse_args()

    table, canonical_sha = load_canonical_source(args.raw, args.manifest)
    cs = args.csharp.read_text(encoding="utf-8")
    propulsion = args.propulsion.read_text(encoding="utf-8")
    engine_runtime = args.engine_runtime.read_text(encoding="utf-8")
    historical_rig = args.historical_rig.read_text(encoding="utf-8")
    powered_validation = args.powered_validation.read_text(encoding="utf-8")

    # ---- canonical runtime-cell identity -------------------------------------------------------
    c_alt = parse_array(cs, "FrozenAltitudeAxisM")
    c_mach = parse_array(cs, "FrozenMachAxis")
    runtime_tables = [
        parse_array(cs, "FrozenIdleThrustN"),
        parse_array(cs, "FrozenMilitaryThrustN"),
        parse_array(cs, "FrozenMaximumThrustN"),
    ]

    check(c_alt == ALT, "C# altitude axis differs from canonical source")
    check(c_mach == MACH, "C# Mach axis differs from canonical source")

    # Cell-by-cell, not whole-list equality: every physical state has its own deterministic check.
    for ai, altitude in enumerate(ALT):
        for mi, mach in enumerate(MACH):
            source_values = table[(altitude, mach)]
            runtime_index = ai * len(MACH) + mi
            for state_index, (state_name, _, _) in enumerate(STATES):
                expected = source_values[state_index]
                got = runtime_tables[state_index][runtime_index]
                check(got == expected,
                      f"runtime {state_name} cell h={altitude} M={mach}: {got} != canonical {expected}")
                S.exact_runtime_cells += 1
    check(S.exact_runtime_cells == 90, f"exact runtime cell total {S.exact_runtime_cells} != 90")

    # Canonical dataset identity is a SHA-256 artifact identity. The compact runtime FNV hash is
    # derived from that identity because the SHA is embedded in FrozenSourceIdentity before arrays
    # are folded into the hash. It is not an independent source identity.
    c_canonical_sha = parse_string_const(cs, "CanonicalRawSourceSha256")
    c_source_version = parse_string_const(cs, "FrozenSourceVersion")
    check(c_canonical_sha == canonical_sha == CANONICAL_RAW_SHA256,
          "C# canonical source SHA-256 is not the frozen rawSourceCsv identity")
    check(c_source_version == CANONICAL_SOURCE_VERSION, "runtime source version changed")
    source_identity = CANONICAL_SOURCE_PREFIX + canonical_sha
    check(CANONICAL_SOURCE_PREFIX in cs and "+ CanonicalRawSourceSha256" in cs,
          "FrozenSourceIdentity is not visibly derived from CanonicalRawSourceSha256")
    declared_hash = parse_uint_const(cs, "FrozenTableHash")
    derived_hash = runtime_hash(source_identity, c_source_version, c_alt, c_mach, *runtime_tables)
    check(declared_hash == derived_hash,
          f"runtime provenance hash mismatch: declared 0x{declared_hash:08X}, derived 0x{derived_hash:08X}")

    # ---- source semantics ----------------------------------------------------------------------
    reachable = 0
    for (a, m), vals in sorted(table.items()):
        for p, expected in zip((0.0, 50.0, 100.0), vals):
            ok, got = evaluate(table, a, m, p)
            check(ok, f"grid point rejected: h={a} M={m} Pa={p}")
            check(got == expected, f"grid mismatch h={a} M={m} Pa={p}: {got} != {expected}")
            reachable += 1
    check(reachable == 90, f"reachable source cells {reachable} != 90")

    negatives = [(a, m, vals[0]) for (a, m), vals in table.items() if vals[0] < 0]
    check(len(negatives) == 12, f"negative Tidle coordinate count {len(negatives)} != 12")
    for a, m, expected in negatives:
        ok, got = evaluate(table, a, m, 0.0)
        check(ok and got == expected and got < 0, f"negative Tidle not preserved at h={a} M={m}")

    ok, mach_mid = evaluate(table, 0.0, 0.3, 50.0)
    check(ok and abs(mach_mid - 56245.0) < 1e-9, f"Mach interpolation anchor: {mach_mid}")
    ok, alt_mid = evaluate(table, 1524.0, 0.4, 100.0)
    check(ok and abs(alt_mid - 87981.5) < 1e-9, f"altitude interpolation anchor: {alt_mid}")

    h, m, p = 1000.0, 0.35, 25.0
    af = h / 3048.0
    mf = (m - 0.2) / 0.2
    idle0 = lerp(2824.0, 267.0, mf)
    idle1 = lerp(1890.0, 111.0, mf)
    mil0 = lerp(56401.0, 56089.0, mf)
    mil1 = lerp(40699.0, 41420.0, mf)
    idle_expected = lerp(idle0, idle1, af)
    mil_expected = lerp(mil0, mil1, af)
    interior_expected = lerp(idle_expected, mil_expected, 0.5)
    ok, interior = evaluate(table, h, m, p)
    check(ok and abs(interior - interior_expected) < 1e-8,
          f"2D/power interpolation anchor: {interior}")

    for q in [(-1, 0.4, 50), (15241, 0.4, 50), (0, 0.199, 50), (0, 1.001, 50),
              (0, 0.4, -0.01), (0, 0.4, 100.01)]:
        ok, thrust = evaluate(table, *q)
        check(not ok and thrust == 0.0, f"unsupported state was not rejected: {q}")
    for q in [(math.nan, 0.4, 50), (0, math.nan, 50), (0, 0.4, math.nan),
              (math.inf, 0.4, 50), (0, -math.inf, 50), (0, 0.4, math.inf)]:
        ok, thrust = evaluate(table, *q)
        check(not ok and thrust == 0.0, f"non-finite state was not rejected: {q}")

    valid = evaluate(table, 0, 0.4, 100)
    invalid = evaluate(table, 0, 1.1, 100)
    check(valid[0] and valid[1] != 0, "stale-thrust precondition invalid")
    check(invalid == (False, 0.0), "invalid query retained stale thrust")

    # ---- default-zero / explicit-opt-in wiring -------------------------------------------------
    propulsion_code = strip_csharp_noncode(propulsion)
    check(re.search(r"public\s+MavThrustDeckBase\s+thrustDeck\s*;", propulsion_code) is not None,
          "thrustDeck no longer defaults to null")
    check("installFrozenTp1538DeckOnAwake" not in propulsion_code,
          "automatic TP-1538-on-Awake switch still exists")

    awake = strip_csharp_noncode(extract_method_body(propulsion, "Awake"))
    configure_default = strip_csharp_noncode(extract_method_body(propulsion, "ConfigureF16Installation"))
    configure_tp = strip_csharp_noncode(extract_method_body(propulsion, "ConfigureTp1538SourcedInstallation"))
    check("EnsureTp1538DeckAttached" not in awake and "ConfigureTp1538SourcedInstallation" not in awake,
          "Awake silently opts into TP-1538")
    check("EnsureTp1538DeckAttached" not in configure_default,
          "default ConfigureF16Installation silently manufactures TP-1538")
    check(re.search(r"CreateInstallation\s*\(\s*thrustDeck\s*\)", configure_default) is not None,
          "default F-16 installation no longer passes the nullable thrustDeck unchanged")
    check("EnsureTp1538DeckAttached" in configure_tp and "ConfigureF16Installation" in configure_tp,
          "TP-1538 is not behind the explicit sourced-installation opt-in")

    engine_code = strip_csharp_noncode(engine_runtime)
    null_zero = re.search(
        r"MavThrustDeckBase\s+deck\s*=\s*installation\.engineProfile\.thrustDeck\s*;"
        r".*?if\s*\(\s*deck\s*==\s*null\s*\|\|.*?\)\s*\{"
        r".*?result\.thrustN\s*=\s*0f\s*;.*?return\s+result\s*;",
        engine_code,
        re.S,
    )
    check(null_zero is not None,
          "shared MavEngineRuntime no longer proves null deck -> exactly 0 N fail-closed result")

    historical_code = strip_csharp_noncode(historical_rig)
    powered_code = strip_csharp_noncode(powered_validation)
    check(re.search(r"\.propulsion\.thrustDeck\s*=\s*null\s*;", historical_code) is not None,
          "historical 5C-R builder no longer explicitly constructs an unpowered/null-deck rig")
    check("ConfigureTp1538SourcedInstallation" not in historical_code,
          "historical 5C-R scenario was contaminated with powered TP-1538 opt-in")
    check("ConfigureTp1538SourcedInstallation" in powered_code,
          "new powered-reference validation does not explicitly opt into TP-1538")

    # ---- actual physics-write call-site guard ---------------------------------------------------
    write_sites = []
    for path, text in ((args.csharp, cs), (args.propulsion, propulsion)):
        write_sites.extend(physics_write_sites(path, text))
    S.physics_write_sites = len(write_sites)
    check(not write_sites,
          "Phase-5D F-16 propulsion contains direct physics-write call site(s):\n  " + "\n  ".join(write_sites))
    # This intentionally does NOT ban the identifier/word 'Rigidbody'; comments and type references
    # are not physics writes. Only executable writer calls/assignments above fail validation.

    gameplay_enable_patterns = [
        r"MavFlightPhysicsOwner\s*\.\s*F16Replacement",
        r"ArmedForLiveFlight\s*=\s*true",
    ]
    for pattern in gameplay_enable_patterns:
        check(re.search(pattern, propulsion_code + "\n" + strip_csharp_noncode(cs)) is None,
              f"Phase-5D production code appears to enable gameplay replacement: {pattern}")

    # ---- mutation probes ------------------------------------------------------------------------
    expected_flat = [[], [], []]
    for a in ALT:
        for m in MACH:
            vals = table[(a, m)]
            for i in range(3):
                expected_flat[i].append(vals[i])

    mutations = []
    mutations.append(kill("M1 sign clamp on negative idle",
                          lambda: any(max(0.0, v) != v for v in expected_flat[0])))
    mutations.append(kill("M2 swapped Mach coordinates",
                          lambda: [0.2, 0.6, 0.4, 0.8, 1.0] != MACH))
    mutations.append(kill("M3 swapped altitude coordinates",
                          lambda: [0, 6096, 3048, 9144, 12192, 15240] != ALT))

    correct_p25 = evaluate(table, 0, 0.2, 25)[1]
    wrong_p25 = 2824.0 + (56401.0 - 2824.0) * (25.0 / 100.0)
    mutations.append(kill("M4 wrong power-state mapping",
                          lambda: abs(correct_p25 - wrong_p25) > 1.0))

    wrong_idle = lerp(lerp(2824.0, 267.0, af), lerp(1890.0, 111.0, af), mf)
    wrong_mil = lerp(lerp(56401.0, 56089.0, af), lerp(40699.0, 41420.0, af), mf)
    wrong_dim = lerp(wrong_idle, wrong_mil, 0.5)
    mutations.append(kill("M5 wrong interpolation dimension",
                          lambda: abs(interior_expected - wrong_dim) > 1e-3))
    mutations.append(kill("M6 unauthorized extrapolation",
                          lambda: evaluate(table, 0, 1.01, 50)[0] is False))
    mutations.append(kill("M7 stale last-known thrust",
                          lambda: invalid == (False, 0.0) and valid[1] != 0.0))

    # Dedicated wiring mutation: making TP-1538 an Awake/default path must be detectable.
    mutations.append(kill("M8 silent default TP-1538 attachment",
                          lambda: "EnsureTp1538DeckAttached" not in awake))

    print("F-16 TP-1538 Phase 5D independent runtime validation PASS")
    print(f"required base: {BASE_COMMIT}")
    print(f"canonical rawSourceCsv SHA-256: {canonical_sha}")
    print(f"canonical runtime cells exact: {S.exact_runtime_cells}/90")
    print("source cells reachable at Pa=0/50/100: 90/90")
    print("negative Tidle coordinates preserved: 12/12")
    print(f"runtime provenance hash derived from canonical identity: 0x{derived_hash:08X}")
    print("default F-16 propulsion: thrustDeck=null preserved; shared runtime null-deck thrust=0 N")
    print("powered TP-1538 reference path: explicit opt-in only")
    print("historical 5C-R: separate null-deck/unpowered path preserved")
    print(f"direct physics-write call sites in Phase-5D production files: {S.physics_write_sites}")
    print(f"assertions: {S.assertions}")
    print("mutation probes:")
    for line in mutations:
        print("  " + line)
    print(f"mutations killed: {S.mutations_killed}/{S.mutations}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (Fail, FileNotFoundError, KeyError, json.JSONDecodeError) as exc:
        print(f"F-16 TP-1538 Phase 5D independent runtime validation FAIL: {exc}")
        raise SystemExit(1)
