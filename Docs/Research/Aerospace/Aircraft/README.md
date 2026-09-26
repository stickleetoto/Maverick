# Aircraft Packs: status

Every aircraft pack has `SOURCE_GRAPH.md`, `KNOWN_DATA.md` (+ `KNOWN_DATA.json`), `MISSING_DATA.md`, a feasibility document (`IMPLEMENTATION_FEASIBILITY.md`, or `PUBLIC_SOURCE_FEASIBILITY.md` for a feasibility-only audit) and a generated `SOURCES.json` slice. The validator requires them for every folder that has a `KNOWN_DATA.json`. Configuration IDs defined in a pack's `KNOWN_DATA.json` are the join key used by `SOURCE_INDEX.json`, and are unique across packs.

Cross-aircraft comparison (no score): [`../AIRCRAFT_DATA_COVERAGE_MATRIX.md`](../AIRCRAFT_DATA_COVERAGE_MATRIX.md).

| Pack | Status | Sources | Configurations | Values (ALLOWED) | Start here |
|---|---|---:|---:|---:|---|
| [`FA18/`](FA18/IMPLEMENTATION_FEASIBILITY.md) | r0 complete; R1 metadata fixes, repository acquisitions and cross-links | 76 | 13 | 102 (0) | `IMPLEMENTATION_FEASIBILITY.md` |
| [`F16/`](F16/README.md) | **R1 deep pack; R2 file verification** | 83 | 18 | 79 (**34**) | `IMPLEMENTATION_FEASIBILITY.md` (verdicts A-G) |
| [`F15/`](F15/README.md) | **R1 consolidation** of existing repository lineage | 61 | 19 | 56 (0) | `F15_PUBLIC_DATA_MATRIX.md` |
| [`F22/`](F22/README.md) | **R1 feasibility audit** (no model) | 21 | 7 | 14 (0) | `PUBLIC_SOURCE_FEASIBILITY.md` |
| [`SU27/`](SU27/README.md) | **SU27-R0 public-source feasibility** (no model; search extracts only) | 20 | 10 | 3 (0) | `PUBLIC_SOURCE_FEASIBILITY.md` |
| [`F14/`](F14/README.md) | Not started (3 sources tagged `F14` so far) | 3 | — | — | — |
| [`Other/`](Other/README.md) | Placeholder | — | — | — | — |

Counts are per pack; a source can belong to several packs (e.g. the F100 lineage is in both F-15 and F-16). Totals after R2: 234 sources, 57 configurations, 251 values, 34 ALLOWED (all for `F16-CFG-NESC-CHECKCASE`). After SU27-R0: 254 sources, 67 configurations, 254 values; ALLOWED unchanged at 34. Conflicts: [`../CONFLICT_REGISTER.md`](../CONFLICT_REGISTER.md).
