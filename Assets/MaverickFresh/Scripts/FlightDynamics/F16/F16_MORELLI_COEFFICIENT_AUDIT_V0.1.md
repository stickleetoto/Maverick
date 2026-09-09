# F-16 Morelli Coefficient Audit v0.1

Status: **PASS for the current polynomial constants and equation structure**

Branch: `feature/f16-flight-dynamics-core`

## Authority and validation policy

Authoritative aerodynamic source:

- Eugene A. Morelli, **Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics**, American Control Conference, 1998.
- NASA NTRS record: `20040110310` / earlier record `19990008037`.

External validator only:

- `stanleybak/AeroBenchVVPython`
- AeroBench source code and tables are **not** authoritative inputs to Maverick and are not copied into this implementation.

## What was audited

`MavF16MorelliPolynomial.cs` was manually checked against the published Morelli polynomial model and the paper's Table 3 parameter set.

Coefficient groups checked:

- `a0..a6` — `Cx0`
- `b0..b4` — `Cxq`
- `c0..c2` — `Cy0`
- `d0..d3` — `Cyp`
- `e0..e3` — `Cyr`
- `f0..f5` — `Cz0`
- `g0..g4` — `Czq`
- `h0..h7` — `Cl0`
- `i0..i3` — `Clp`
- `j0..j4` — `Clr`
- `k0..k6` — `Cl-da`
- `l0..l6` — `Cl-dr`
- `m0..m7` — `Cm0`
- `n0..n5` — `Cmq`
- `o0..o6` — `Cn0`
- `p0..p4` — `Cnp`
- `q0..q2` — `Cnr`
- `r0..r9` — `Cn-da`
- `s0..s5` — `Cn-dr`

The current Maverick constants match the original Morelli Table 3 values used for this audit.

## Equation-structure audit

The implementation uses the published compact model structure:

- `Cx = Cx0 + Cxq * qHat`
- `Cy = Cy0 + Cyp * pHat + Cyr * rHat`
- `Cz = Cz0 + Czq * qHat`
- `Cl = Cl0 + Clp * pHat + Clr * rHat + Cl_da * da + Cl_dr * dr`
- `Cm = Cm0 + Cmq * qHat + Cz * (xcgRef - xcg)`
- `Cn = Cn0 + Cnp * pHat + Cnr * rHat + Cn_da * da + Cn_dr * dr - Cy * (xcgRef - xcg) * (cbar / b)`

Rate normalization is performed as:

- `pHat = p*b/(2V)`
- `qHat = q*cbar/(2V)`
- `rHat = r*b/(2V)`

This matches the intended nondimensional-rate form.

## Important cross-check findings

AeroBench is useful as a behavioral oracle, but its implementation is not byte-for-byte identical to the 1998 Morelli table. Known examples found during audit include:

- Original Morelli `h0 = -1.058583e-1`; AeroBench uses a rounded/truncated form.
- Original Morelli `k1 = -4.073901e-2`; AeroBench uses a slightly shorter decimal form.
- Original Morelli `n0 = -5.159153`; AeroBench uses `-5.19153`.

Maverick keeps the original Morelli values rather than changing them to match AeroBench.

A later NASA presentation also shows a different printed `Cm0` constant than the 1998 table. For this branch, the 1998 aerodynamic paper remains authoritative, so `m0 = -2.029370e-2` is retained.

## AeroBench full-trajectory caution

AeroBench's `subf16_model` can run the Morelli polynomial path, but the surrounding model also adds an additional legacy damping-derivative layer after evaluating the Morelli polynomial. Because the Morelli polynomial already contains nondimensional rate terms, full-trajectory comparisons must be treated as cross-validation rather than a requirement for exact identity.

Recommended validation hierarchy:

1. **Coefficient-level:** Maverick polynomial vs. values computed directly from the published Morelli equations.
2. **6DoF state-derivative level:** Maverick equations using the same state and control convention.
3. **Trajectory-level:** AeroBench as external behavioral cross-check with documented configuration differences.

## Freeze decision

For `F16 Reference Dynamics v0.1`, the aerodynamic polynomial constants and compact equation structure are **frozen** until a reproducible NASA-source discrepancy is found.

Future changes to the polynomial must include:

- source identifier,
- exact affected coefficient/equation,
- before/after value,
- reason for change,
- coefficient-level regression result.
