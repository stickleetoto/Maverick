# Aircraft Packs: status

Every aircraft pack has the same four documents: `SOURCE_GRAPH.md`, `KNOWN_DATA.md` (+ `KNOWN_DATA.json`), `MISSING_DATA.md` and `IMPLEMENTATION_FEASIBILITY.md`. The configuration IDs defined in a pack's `KNOWN_DATA.json` are the join key used by `SOURCE_INDEX.json`.

| Pack | Priority | Status | Notes |
|---|---|---|---|
| [`FA18/`](FA18/IMPLEMENTATION_FEASIBILITY.md) | A | **r0 complete (citation/abstract level)** | 66 sources, 12 configurations, 50 candidate values (none code-usable yet). Next: FA18-R1 page verification |
| [`F16/`](F16/README.md) | B | Queued (F16-R1) | Existing authority is `Docs/Reference/F16_*` |
| [`F15/`](F15/README.md) | C | Queued | Existing authority is `Docs/Reference/F15_*`; do not redo the implementation-branch work |
| [`F14/`](F14/README.md) | D | Not started | Feasibility question open |
| [`F22/`](F22/README.md) | E | Not started | **Feasibility audit only**; output will be `PUBLIC_SOURCE_FEASIBILITY.md` |
| [`Other/`](Other/README.md) | — | Placeholder | For research aircraft that inform methods (X-29, X-31, F-16XL, …) without being Maverick targets |
