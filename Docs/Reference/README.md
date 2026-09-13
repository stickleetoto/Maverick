# Maverick Flight-Dynamics Reference Library

Status: **REFERENCE INDEX — source links and provenance rules**

This directory is a research index for aircraft data used by Maverick. It does **not** make every linked source an implementation authority, and it does not mean that data from different aircraft, blocks, test vehicles, or research configurations may be mixed.

## Source hierarchy

### Tier 1 — implementation authority when configuration matches

- NASA Technical Reports Server (NTRS) primary technical reports/papers.
- NASA TP/TM/TN/CR documents containing the actual geometry, mass properties, aerodynamic data, equations, or flight-test results for the configuration being modeled.
- Original Morelli / Garza & Morelli F-16 publications used by the current reference F-16 stack.

### Tier 2 — configuration-specific NASA research

Examples:

- AFTI/F-16
- F-16XL
- VISTA/MATV
- NASA modified F-15B
- three-surface F-15 research configurations
- F-15 ACTIVE
- F-15 with conformal fuel tanks

These sources are authoritative for **their own test vehicle/configuration**, but must not silently overwrite a baseline F-16 or F-15 model.

### Tier 3 — external cross-validation / implementation comparison

Examples:

- AeroBench / AeroBenchVVPython
- Texas A&M F16-Model-Matlab
- JSBSim

Tier 3 is useful for regression, trim, trajectory, and architecture comparisons. It is not allowed to silently replace a Tier 1 value.

## Repository rules

1. **Configuration identity is mandatory.** Record whether a datum belongs to F-16 reference, AFTI/F-16, F-16XL, VISTA/MATV, baseline F-15, F-15 CFT, three-surface F-15, modified NASA F-15B, F-15 ACTIVE, F-15E, etc.
2. **Do not merge research-aircraft control laws into production-aircraft claims.** AFTI/VISTA/MATV and ACTIVE are especially easy to misuse.
3. **Do not infer operational F-16C/F-15C/F-15E values from a different research configuration without labeling the result approximate.**
4. **Keep aerodynamic valid ranges explicit.** Extrapolation must be declared; it must never be presented as source-validated behavior.
5. **No guessed numbers.** Missing public data is recorded as `UNAVAILABLE`, `APPROXIMATE`, or `MAVERICK_TUNING`.
6. **Prefer official NTRS citation pages over mirrors.** When a PDF is public, the citation page remains the canonical link because it preserves document ID, report number, publication date, and distribution status.
7. **Do not vendor restricted NASA software/data.** A public report may be used while an associated software package is release-gated.
8. **Reference PDFs are linked, not copied into the repo by default.** This keeps the repository small and preserves provenance at the official source.

## Packs

- [`F16_SOURCE_PACK_V0.1.md`](./F16_SOURCE_PACK_V0.1.md) — current F-16 reference chain, supporting F-16/AFTI/F-16XL/MATV research, and validation implementations.
- [`F15_SOURCE_PACK_V0.1.md`](./F15_SOURCE_PACK_V0.1.md) — baseline/high-alpha/spin/transonic/propulsion F-15 research sources and configuration-separation rules.

## Architecture and roadmaps

- [`../FlightDynamics/SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.1.md`](../FlightDynamics/SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.1.md) — Profile + Engine decision, common propulsion runtime target, current engine gaps, and F-16/F-15 migration sequence.
- [`../FlightDynamics/F15_DEVELOPMENT_ROADMAP_V0.1.md`](../FlightDynamics/F15_DEVELOPMENT_ROADMAP_V0.1.md) — staged F-15 reference-aircraft roadmap from configuration freeze through subsonic aero, twin-engine propulsion, high-alpha/spin, transonic extension, and gameplay variants.

## Current development interpretation

### F-16

Best current candidate for the first deeply validated reference aircraft because the public chain is unusually coherent:

`NASA TP-1538 -> Morelli nonlinear aerodynamics -> Garza & Morelli nonlinear simulation -> Maverick validation / external cross-validation`

### F-15

The public record is exceptionally rich, especially at high angle of attack and spin, but is distributed across multiple models and flight/research configurations. The F-15 reference model should therefore be assembled from a **configuration-tagged source graph**, not by treating all NASA F-15 reports as one interchangeable database.

## Propulsion development rule

Maverick uses a **Profile + Engine** architecture. Flight-dynamics profiles select aircraft geometry/mass/aero and a propulsion installation. Installed engine slots reference exact, provenance-tagged engine profiles and create independent runtime states. F-16 and F-15 share the engine/runtime/aggregation infrastructure, but they do not share numeric engine data unless exact engine/configuration compatibility is established.

## Provenance labels used in these packs

- `AUTHORITATIVE` — primary source accepted for the modeled configuration.
- `PUBLIC_REFERENCE` — strong public source useful for implementation/validation, but configuration matching must still be checked.
- `CROSS_VALIDATION_ONLY` — external implementation or mismatched configuration useful as an oracle, not a direct value source.
- `APPROXIMATE` — intentionally approximate extension with provenance.
- `MAVERICK_TUNING` — project tuning, not claimed as aircraft data.
- `UNAVAILABLE` — no accepted public source yet.
