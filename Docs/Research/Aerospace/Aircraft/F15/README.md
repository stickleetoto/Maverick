# F-15 pack (R1 consolidation)

Centralises F-15 source lineage that already exists in the repository. The F-15 implementation work (`claude/f15-full-implementation`) is **not** redone here. Start with [`F15_PUBLIC_DATA_MATRIX.md`](F15_PUBLIC_DATA_MATRIX.md).

| Document | Content |
|---|---|
| [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md) | Configuration spine, NASA 836 / McDonnell / NASA preproduction / subscale lineages, conflict register F15-X1 to X8 |
| [`F15_PUBLIC_DATA_MATRIX.md`](F15_PUBLIC_DATA_MATRIX.md) | Field x configuration matrix |
| [`F15_PROPULSION_SOURCE_GRAPH.md`](F15_PROPULSION_SOURCE_GRAPH.md) | F100 build registry and lineage |
| [`KNOWN_DATA.md`](KNOWN_DATA.md) + [`KNOWN_DATA.json`](KNOWN_DATA.json) | Configurations and candidate values (all repository readings) |
| [`MISSING_DATA.md`](MISSING_DATA.md) | Ranked gaps (from the repository gap audit) |
| [`IMPLEMENTATION_FEASIBILITY.md`](IMPLEMENTATION_FEASIBILITY.md) | Verdicts |
| [`SOURCES.json`](SOURCES.json) | Generated slice of `SOURCE_INDEX.json` |

Repository authority documents (unchanged): `Docs/Reference/F15_*` on main, plus the branch documents cited in each source's `existing_maverick_analysis`.
