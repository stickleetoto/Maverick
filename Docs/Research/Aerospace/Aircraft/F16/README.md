# F-16 pack (R1, R2)

The deepest pack in the library. Start with [`IMPLEMENTATION_FEASIBILITY.md`](IMPLEMENTATION_FEASIBILITY.md).

**R2:** the first library-verified F-16 configuration is `F16-CFG-NESC-CHECKCASE` (NASA's NESC check-case model files, file-line verified; 34 implementation-allowed values). It is a validation configuration, not Maverick's Morelli-model reference F-16. See [`../../VERIFICATION_LOG_R2.md`](../../VERIFICATION_LOG_R2.md).

| Document | Content |
|---|---|
| [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md) | Configuration spine, top-level lineage, six-axis coefficient support, conflicts |
| [`F16_MODEL_LINEAGE.md`](F16_MODEL_LINEAGE.md) | Every F-16 model, where its numbers came from, build-up conventions |
| [`F16_FCS_SOURCE_GRAPH.md`](F16_FCS_SOURCE_GRAPH.md) | Four-layer FCS matrix (public architecture / public gains / research approximations / production not located) |
| [`F16_PROPULSION_SOURCE_GRAPH.md`](F16_PROPULSION_SOURCE_GRAPH.md) | F100 / F110 lineage, never merged |
| [`F16_VALIDATION_SOURCE_GRAPH.md`](F16_VALIDATION_SOURCE_GRAPH.md) | Check-cases, flight data, independence |
| [`KNOWN_DATA.md`](KNOWN_DATA.md) + [`KNOWN_DATA.json`](KNOWN_DATA.json) | Configuration registry and candidate values |
| [`MISSING_DATA.md`](MISSING_DATA.md) | Blockers and gaps |
| [`IMPLEMENTATION_FEASIBILITY.md`](IMPLEMENTATION_FEASIBILITY.md) | Verdicts A-G |
| [`SOURCES.json`](SOURCES.json) | Generated slice of `SOURCE_INDEX.json` |

The repository's existing F-16 authority documents stay where they are and are not changed by this library: `Docs/Reference/F16_SOURCE_PACK_V0.1.md`, `Docs/Reference/F16_TP1538_THRUST_DECK_V0.1.md`, `Docs/Reference/Data/F16/TP1538/`, and `Assets/MaverickFresh/Scripts/FlightDynamics/F16/F16_*.md`.
