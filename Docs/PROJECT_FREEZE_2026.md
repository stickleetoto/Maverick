# Maverick Project Freeze — 2026-09-28

## Status

Maverick is **frozen for now**.

The repository is being preserved as a validated flight-dynamics research codebase and future simulation reference, not actively pushed toward a finished game.

Stable baseline:

`main @ f0f9519a785d7f327a0de7e452c93285c073d42f`

No later experimental PR is part of the accepted baseline.

---

## Why development is stopping here

Maverick reached the point where additional changes were producing diminishing returns.

Two late experiments made the boundary clear:

- **PR #32 — Gameplay Integration R1**
  - attempted to make the project feel more like a complete game;
  - changed presentation/runtime flow too broadly;
  - introduced replacement placeholder visuals instead of preserving the existing aircraft presentation;
  - closed without merge.

- **PR #33 — F-15 Flight Physics R2**
  - added a source-backed research-model actuator lag and later promoted it to the pilot default;
  - automated regression and shared/F-16 gates passed;
  - human flight testing reported noticeable vibration and an overly game-like feel;
  - the research remains useful, but the behavior is not accepted into the stable baseline;
  - closed without merge.

The goal is therefore to preserve the strongest validated state instead of continuing to disturb it.

---

## What Maverick successfully established

### Flight-dynamics architecture

The project established and exercised a structured flight-physics path around:

`PilotCommand -> ControlLaw -> ControlSurfaceActuator -> Aero / Propulsion -> LoadSet -> SixDoFBody -> Rigidbody`

The architecture separates command, control-law, actuator, aerodynamic/propulsion, load, and rigid-body ownership.

### F-15 research path

The repository contains a public-source research implementation based on the AFIT / Baumann / Davison lineage, including:

- mass and inertia handling;
- six-axis aerodynamic forces and moments;
- source-condition handling;
- trim and equilibrium work;
- longitudinal and lateral-directional coefficient handling;
- control-surface state and actuator architecture;
- pilot-controlled V1 and V2 control paths;
- ownership and fail-closed runtime authority;
- source provenance and reference documentation;
- deterministic validation and Play Mode validation.

The pilot control law is a **Maverick non-authoritative control approximation**, not a claim of reproducing the real production F-15 FCS/CAS/SAS.

### F-16 research path

The repository also contains substantial F-16 work, including:

- Morelli-based aerodynamic/reference work;
- F-16 control-law and actuator structure;
- engine power/propulsion reference scaffolding;
- readiness and validation infrastructure.

The F-16 reference path should not be described as a finished player-ready aircraft unless its own readiness gates are explicitly satisfied.

### Validation discipline

A major reusable result of Maverick is the validation process itself:

- frozen research baselines;
- deterministic headless suites;
- Unity Play Mode validation;
- explicit source/provenance tracking;
- fail-closed identity and ownership checks;
- regression gates protecting shared/F-16 behavior.

---

## Stable baseline policy

The accepted stable line is:

`main @ f0f9519a785d7f327a0de7e452c93285c073d42f`

Do not merge PR #32 or PR #33 into this baseline.

Their branches are preserved as research/experiment history:

- `claude/gameplay-f15-f16-r1`
- `claude/f15-physics-r2`

Future work should branch from the stable baseline unless a deliberate decision is made to resurrect one of those experiments.

---

## Known limits at freeze

The F-15 research model remains constrained by public data availability and source conditions.

Important limits include:

- limited aerodynamic source domain;
- fixed/source-specific atmosphere assumptions;
- fixed research thrust on the pilot-controlled F-15;
- incomplete public F100 thrust-deck coverage;
- incomplete authoritative actuator travel/rate data;
- source-model regions whose damping behavior is not ideal for human-feel tuning;
- tension between preserving source behavior and adding stronger gameplay-oriented stabilization.

These limits should be treated as research boundaries rather than hidden with arbitrary tuning.

---

## Recommended future use

Maverick is best retained as:

- a flight-dynamics reference implementation;
- a deterministic validation/testbed;
- a simulation backend candidate for FAM;
- a source/provenance archive;
- a future restart point if stronger public data becomes available.

If development resumes, prefer small isolated physics changes over broad scene/gameplay rewrites.

A resumed change should answer:

1. What physical or architectural deficiency is being fixed?
2. What public evidence supports the change?
3. Which component owns the behavior afterward?
4. What existing approximation is being replaced?
5. Which frozen regressions prove unrelated behavior did not move?
6. What does a human flight test report after the automated gates pass?

---

## Restart point

For a clean restart:

1. branch from `main`;
2. keep the frozen F-15 research baseline intact;
3. preserve the V1/V2 historical regression paths;
4. avoid editing models, scenes, UI, and physics in the same work package;
5. validate the smallest possible change;
6. require both automated validation and a human flight test before promotion.

---

## Final project disposition

**MAVERICK: FROZEN / RESEARCH COMPLETE FOR NOW**

The project is not deleted or discarded.

Its validated work is retained for future research and reuse, while unstable or overly broad late experiments remain archived outside the stable baseline.
