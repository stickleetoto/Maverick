# F-16: queued package F16-R1 (Priority B)

Status: **not started in this library.** The current authority is the existing reference set in `Docs/Reference/`:

- [`F16_SOURCE_PACK_V0.1.md`](../../../../Reference/F16_SOURCE_PACK_V0.1.md): NASA TP-1538 → Morelli 1998 → Garza & Morelli chain, plus AFTI/F-16, F-16XL, VISTA/MATV and cross-validation implementations.
- [`F16_TP1538_THRUST_DECK_V0.1.md`](../../../../Reference/F16_TP1538_THRUST_DECK_V0.1.md) and `Docs/Reference/Data/F16/TP1538/`: Table VI thrust deck, transcribed and cross-checked (the L5 pattern for this library).
- `F16_TP1538_RUNTIME_SEMANTICS_V0.1.md`, `F16_TP1538_RUNTIME_VALIDATION_V0.1.md`, `Docs/F16_TP1538_TABLE_VI_TRANSCRIPTION.md`.

## F16-R1 scope (planned)

1. Migrate every source from `F16_SOURCE_PACK_V0.1.md` into `SOURCE_INDEX.json` with verification levels. Leave the existing pack unchanged.
2. Build `SOURCE_GRAPH.md` for the Morelli lineage, keeping three things separate:
   - the specific NASA test aircraft (AFTI/F-16, F-16XL, VISTA/MATV, etc.)
   - generic production-family F-16 data
   - simulator/research models (TP-1538 simulation, Garza & Morelli, Stevens & Lewis-derived codes)
3. Extend with NASA thrust data, control laws, mass/inertia, high-alpha/departure studies and flight-test validation.
4. Check the NESC 6-DOF check-cases (`NASA-TM-2015-218675`) for a case usable against the Maverick F-16 reference.
5. Produce `KNOWN_DATA`, `MISSING_DATA` and `IMPLEMENTATION_FEASIBILITY` for F-16 in the r0 format.
