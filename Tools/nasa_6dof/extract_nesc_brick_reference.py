#!/usr/bin/env python3
"""Extract the NASA NESC tumbling-brick reference data (atmospheric check-cases 2 and 3)
into a small, provenance-stamped C# data file for Maverick's validation suites.

Standard library only. Reads the two official NESC zips directly (no extraction), verifies
their SHA-256 against the values pinned below (from
Docs/Research/InstructorV2/Phase2/NESC_DOWNLOAD_MANIFEST.md on the research branch), and
refuses to run on any other file. Never substitutes a different dataset.

Usage:
    python Tools/nasa_6dof/extract_nesc_brick_reference.py --source-dir <dir with the zips> \
        --out Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavNasaBrickReferenceData.cs

What is extracted:
  * brick mass, inertia, reference geometry, damping derivatives and the true-airspeed floor,
    read from brick_inertia.dml / brick_aero.dml (initialValue / minValue attributes);
  * for every tool (sim) that published case 2 and case 3: samples every SAMPLE_STEP_S of
      - inertial body rates p, q, r (bodyAngularRateWrtEi, deg/s),
      - the body attitude relative to the INITIAL local NED frame, as a direction-cosine matrix,
        derived from the tool's Euler angles (3-2-1, relative to the rotating NED), its geodetic
        latitude/longitude, and Earth rotation (see derive_c_b_n0 below),
      - altitude (ft), true airspeed (ft/s), air density (slug/ft^3), local gravity (ft/s^2)
        where the tool published them (NaN otherwise).

The frame derivation is ENGINEERING INFERENCE implemented here, not a NASA output; it is
documented in Docs/Validation/NASA/CASE2_TORQUE_FREE_REPORT.md.
"""
import argparse
import csv
import hashlib
import io
import math
import re
import sys
import zipfile

CHECKCASES_ZIP = "Atmospheric_checkcases.zip"
MODELS_ZIP = "Atmospheric_models.zip"
PINNED_SHA256 = {
    CHECKCASES_ZIP: "1a83df6d00fa186973e8fb7bb8aefa5f7ed580d2ce8aa1327f50ac0c7bd5c869",
    MODELS_ZIP: "10e2dc267856ef37eaedbcdd16eb49a8c44b39301054de0fa78292c1c4aba3bc",
}
CASE_DIRS = {
    2: "Atmospheric_checkcases/Atmos_02_TumblingBrickNoDamping/Atmos_02_sim_{sim}.csv",
    3: "Atmospheric_checkcases/Atmos_03_TumblingBrickDamping/Atmos_03_sim_{sim}.csv",
}
SIMS = ["01", "02", "04", "05", "06"]
SAMPLE_STEP_S = 2.0
DURATION_S = 30.0
OMEGA_EARTH = 7.292115e-5  # rad/s, NESC Vol II Table 73 (atmospheric check-cases)
KNOT_FT_S = 6076.115485564304 / 3600.0  # 1 international nautical mile per hour in ft/s


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def transpose(a):
    return [[a[j][i] for j in range(3)] for i in range(3)]


def r1(x):
    c, s = math.cos(x), math.sin(x)
    return [[1, 0, 0], [0, c, s], [0, -s, c]]


def r2(x):
    c, s = math.cos(x), math.sin(x)
    return [[c, 0, -s], [0, 1, 0], [s, 0, c]]


def r3(x):
    c, s = math.cos(x), math.sin(x)
    return [[c, s, 0], [-s, c, 0], [0, 0, 1]]


def c_n_from_e(lat_rad, lon_rad):
    """Direction cosines of local NED with respect to ECEF (standard geodetic form)."""
    sl, cl = math.sin(lat_rad), math.cos(lat_rad)
    so, co = math.sin(lon_rad), math.cos(lon_rad)
    return [
        [-sl * co, -sl * so, cl],
        [-so, co, 0.0],
        [-cl * co, -cl * so, -sl],
    ]


def derive_c_b_n0(t, yaw_deg, pitch_deg, roll_deg, lat_deg, lon_deg, lat0_deg, lon0_deg):
    """Body attitude relative to the local NED frame frozen at t = 0 (an inertial frame).

    C_b<-n  = R1(roll) R2(pitch) R3(yaw)            (3-2-1 Euler, body relative to rotating NED)
    C_n<-e  = geodetic NED/ECEF at (lat, lon)
    C_e<-i  = R3(OMEGA_EARTH * t)                   (ECEF aligned with ECI at t = 0)
    C_b<-i  = C_b<-n C_n<-e C_e<-i
    C_n0<-i = C_n<-e(lat0, lon0)                    (since C_e<-i(0) = I)
    C_b<-n0 = C_b<-i C_n0<-i^T
    """
    c_bn = mat_mul(mat_mul(r1(math.radians(roll_deg)), r2(math.radians(pitch_deg))), r3(math.radians(yaw_deg)))
    c_ne = c_n_from_e(math.radians(lat_deg), math.radians(lon_deg))
    c_ei = r3(OMEGA_EARTH * t)
    c_bi = mat_mul(mat_mul(c_bn, c_ne), c_ei)
    c_n0i = c_n_from_e(math.radians(lat0_deg), math.radians(lon0_deg))
    return mat_mul(c_bi, transpose(c_n0i))


def read_dml_values(text, var_ids, attribute="initialValue"):
    out = {}
    for vid in var_ids:
        m = re.search(r'<variableDef[^>]*varID="%s"[^>]*>' % re.escape(vid), text)
        if not m:
            raise SystemExit("DAVE-ML variable %s not found" % vid)
        a = re.search(r'%s="([^"]+)"' % attribute, m.group(0))
        if not a:
            raise SystemExit("DAVE-ML variable %s has no %s" % (vid, attribute))
        out[vid] = float(a.group(1))
    return out


def col(row, header, name):
    if name not in header:
        return float("nan")
    v = row[header.index(name)].strip()
    return float(v) if v else float("nan")


def load_case(zf, case, sim):
    name = CASE_DIRS[case].format(sim=sim)
    raw = zf.read(name)
    file_sha = hashlib.sha256(raw).hexdigest()
    rows = list(csv.reader(io.StringIO(raw.decode("utf-8-sig"))))
    header = [h.strip() for h in rows[0]]
    data = [r for r in rows[1:] if r and r[0].strip()]
    first = data[0]
    lat0 = col(first, header, "latitude_deg")
    lon0 = col(first, header, "longitude_deg")
    samples = []
    k = 0
    while k * SAMPLE_STEP_S <= DURATION_S + 1e-9:
        target = k * SAMPLE_STEP_S
        row = min(data, key=lambda r: abs(float(r[0]) - target))
        t = float(row[0])
        if abs(t - target) > 1e-6:
            raise SystemExit("case %d sim %s has no sample at t = %.3f" % (case, sim, target))
        g = lambda n: col(row, header, n)
        tas = g("trueAirspeed_nmi_h") * KNOT_FT_S
        if math.isnan(tas):
            vx, vy, vz = g("feVelocity_ft_s_X"), g("feVelocity_ft_s_Y"), g("feVelocity_ft_s_Z")
            tas = math.sqrt(vx * vx + vy * vy + vz * vz)
        c = derive_c_b_n0(t, g("eulerAngle_deg_Yaw"), g("eulerAngle_deg_Pitch"), g("eulerAngle_deg_Roll"),
                          g("latitude_deg"), g("longitude_deg"), lat0, lon0)
        samples.append({
            "t": t,
            "pqr": [g("bodyAngularRateWrtEi_deg_s_Roll"), g("bodyAngularRateWrtEi_deg_s_Pitch"),
                    g("bodyAngularRateWrtEi_deg_s_Yaw")],
            "c": [c[i][j] for i in range(3) for j in range(3)],
            "alt": g("altitudeMsl_ft"),
            "tas": tas,
            "rho": g("airDensity_slug_ft3"),
            "grav": g("localGravity_ft_s2"),
        })
        k += 1
    return name, file_sha, samples


def fmt(x):
    if isinstance(x, float) and math.isnan(x):
        return "double.NaN"
    return repr(float(x))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source-dir", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    paths = {}
    for zname, pinned in PINNED_SHA256.items():
        p = "%s/%s" % (args.source_dir.rstrip("/\\"), zname)
        got = sha256_of(p)
        if got != pinned:
            raise SystemExit("SHA-256 mismatch for %s: %s (expected %s). Refusing." % (zname, got, pinned))
        paths[zname] = p

    models = zipfile.ZipFile(paths[MODELS_ZIP])
    inertia_raw = models.read("brick_inertia.dml")
    aero_raw = models.read("brick_aero.dml")
    inertia_sha = hashlib.sha256(inertia_raw).hexdigest()
    aero_sha = hashlib.sha256(aero_raw).hexdigest()
    inertia = read_dml_values(inertia_raw.decode("utf-8"), ["XIXX", "XIYY", "XIZZ", "XIZX", "XIXY", "XIYZ", "XMASS"])
    aero = read_dml_values(aero_raw.decode("utf-8"), ["SWING", "BSPAN", "CBAR", "CLP_DAMPING", "CLR_DAMPING",
                                                      "CMQ_DAMPING", "CNP_DAMPING", "CNR_DAMPING"])
    vfloor = read_dml_values(aero_raw.decode("utf-8"), ["VRW"], attribute="minValue")["VRW"]

    cases = zipfile.ZipFile(paths[CHECKCASES_ZIP])
    case_data = {}
    for case in (2, 3):
        case_data[case] = [(sim,) + load_case(cases, case, sim) for sim in SIMS]

    L = []
    w = L.append
    w("// <auto-generated>")
    w("// Generated by Tools/nasa_6dof/extract_nesc_brick_reference.py - do not edit by hand.")
    w("// Source: NASA NESC 6-DoF check cases (https://nescacademy.nasa.gov/flightsim/2015),")
    w("//   %s sha256 %s" % (CHECKCASES_ZIP, PINNED_SHA256[CHECKCASES_ZIP]))
    w("//   %s sha256 %s" % (MODELS_ZIP, PINNED_SHA256[MODELS_ZIP]))
    w("//   brick_inertia.dml sha256 %s" % inertia_sha)
    w("//   brick_aero.dml sha256 %s" % aero_sha)
    for case in (2, 3):
        for sim, name, fsha, _ in case_data[case]:
            w("//   %s sha256 %s" % (name, fsha))
    w("// NASA/TM-2015-218675: US Government work, public use permitted. Values are a sampled subset.")
    w("// The attitude matrices are derived (Euler + lat/lon + Earth rotation); see the extractor docstring.")
    w("// </auto-generated>")
    w("#if UNITY_EDITOR")
    w("namespace MaverickFresh.FlightDynamics.Validation")
    w("{")
    w("    /// <summary>NASA NESC atmospheric check cases 2 and 3 (tumbling brick): model constants and sampled reference outputs. English units as published.</summary>")
    w("    public static class MavNasaBrickReferenceData")
    w("    {")
    w("        public const string CheckcasesZipSha256 = \"%s\";" % PINNED_SHA256[CHECKCASES_ZIP])
    w("        public const string ModelsZipSha256 = \"%s\";" % PINNED_SHA256[MODELS_ZIP])
    w("        public const string BrickInertiaDmlSha256 = \"%s\";" % inertia_sha)
    w("        public const string BrickAeroDmlSha256 = \"%s\";" % aero_sha)
    w("        // brick_inertia.dml (slug, slug*ft^2)")
    for k, cs in (("XMASS", "MassSlug"), ("XIXX", "IxxSlugFt2"), ("XIYY", "IyySlugFt2"), ("XIZZ", "IzzSlugFt2"),
                  ("XIZX", "IzxSlugFt2"), ("XIXY", "IxySlugFt2"), ("XIYZ", "IyzSlugFt2")):
        w("        public const double %s = %s;" % (cs, fmt(inertia[k])))
    w("        // brick_aero.dml (ft^2, ft, per radian of normalised rate)")
    for k, cs in (("SWING", "ReferenceAreaFt2"), ("BSPAN", "ReferenceSpanFt"), ("CBAR", "ReferenceChordFt"),
                  ("CLP_DAMPING", "Clp"), ("CLR_DAMPING", "Clr"), ("CMQ_DAMPING", "Cmq"),
                  ("CNP_DAMPING", "Cnp"), ("CNR_DAMPING", "Cnr")):
        w("        public const double %s = %s;" % (cs, fmt(aero[k])))
    w("        /// <summary>brick_aero.dml: trueAirspeed (VRW) minValue - the input floor used in the rate normalisation.</summary>")
    w("        public const double TrueAirspeedFloorFtS = %s;" % fmt(vfloor))
    w("        public const double EarthRotationRadS = %s; // NESC Vol II Table 73" % fmt(OMEGA_EARTH))
    w("        public const double SampleStepS = %s;" % fmt(SAMPLE_STEP_S))
    w("        public static readonly string[] Sims = { %s };" % ", ".join('"%s"' % s for s in SIMS))
    for case in (2, 3):
        w("        // Case %d rows per sim: t, p, q, r (deg/s, inertial), C_b<-n0 (9, row-major), alt ft, TAS ft/s, rho slug/ft^3, g ft/s^2" % case)
        w("        public static readonly double[][][] Case%d = new double[][][]" % case)
        w("        {")
        for sim, name, fsha, samples in case_data[case]:
            w("            new double[][] // sim %s" % sim)
            w("            {")
            for s in samples:
                vals = [s["t"]] + s["pqr"] + s["c"] + [s["alt"], s["tas"], s["rho"], s["grav"]]
                w("                new double[] { %s }," % ", ".join(fmt(v) for v in vals))
            w("            },")
        w("        };")
    w("    }")
    w("}")
    w("#endif")
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L) + "\n")
    print("wrote", args.out, "rows per sim:", len(case_data[2][0][3]))


if __name__ == "__main__":
    main()
