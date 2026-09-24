# Maverick Aerospace Public-Source Library

Status: **R1: multi-aircraft expansion.** F-16 deep pack, F-15 consolidation, F-22 feasibility audit, fundamentals expansion, a cross-aircraft coverage matrix and a numeric field index, on top of the r0 F/A-18 pack. Research and documentation only. No runtime code.

This library is where Maverick keeps public aerospace engineering sources: what exists, where it comes from, which aircraft configuration it describes, and whether a number from it is safe to use in code. The aim is that the next aircraft starts from this index instead of repeating the source hunt.

It sits next to the existing per-aircraft packs in [`Docs/Reference/`](../../Reference/README.md). Those packs, and the F-15 and F/A-18 work on other branches, remain the authority for the implementation work already done. From R1 the library cross-references them through `existing_maverick_analysis` (path, branch, commit, locator). **A Maverick document's reading of a page is recorded as such. It is never treated as the library having read the primary source.**

---

## 1. How to answer "can I use this number in code?"

1. Find the value in [`AIRCRAFT_NUMERIC_FIELD_INDEX.json`](AIRCRAFT_NUMERIC_FIELD_INDEX.json), or in `Aircraft/<type>/KNOWN_DATA.json` (the `.md` view renders the same data).
2. Read `implementation_allowed` (index) or `implementation_use` (KNOWN_DATA):
   - `ALLOWED` / `true`: the library inspected the source page itself (`PAGE_VERIFIED`), the page/table/figure is recorded, and configuration, units and convention are explicit. Use it for that configuration only.
   - `BLOCKED_PENDING_PAGE_VERIFICATION`: a lead. Do not put it in code.
   - `PROHIBITED`: not public, or only a fact-sheet or identity figure. Never use it in code.
   - `DOMAIN_METADATA_ONLY` / `NOT_A_MODEL_PARAMETER`: use only as a domain gate, tolerance, gap record or scenario reference.
3. Check the configuration. A value for NASA 840 in Phase 2 is **not** a value for a production F/A-18A. A value for the TP-1538 simulation is **not** a value for an F-16C.
4. Check `conflicts_with`. If it is not empty, read both values before choosing.
5. If `exactness` is `REPOSITORY_READING`, the number came from another Maverick document. Follow `existing_maverick_analysis` to see who read it, where and on which branch.

**After R1 no value is `ALLOWED`.** See §4.

## 2. Directory map

```
Docs/Research/Aerospace/
├─ README.md                           this file
├─ SCHEMA.md                           metadata schema v2, enums, verification levels
├─ SOURCE_INDEX.json                   every source (canonical)
├─ SOURCE_INDEX.md                     human-readable index (tables generated)
├─ SOURCE_GRAPH.md                     lineage rules, repeated-number register, cross-aircraft graph
├─ AIRCRAFT_DATA_COVERAGE_MATRIX.md    F-15 / F-16 / F/A-18 / F-22 coverage by field (no score)
├─ AIRCRAFT_NUMERIC_FIELD_INDEX.json   every candidate numeric value, flat (generated)
├─ Aircraft/
│  ├─ README.md                        per-aircraft status
│  ├─ FA18/   r0 pack + R1 corrections        (SOURCE_GRAPH, KNOWN_DATA, MISSING_DATA, IMPLEMENTATION_FEASIBILITY, SOURCES.json)
│  ├─ F16/    R1 deep pack                    (+ F16_MODEL_LINEAGE, F16_FCS_/F16_PROPULSION_/F16_VALIDATION_SOURCE_GRAPH)
│  ├─ F15/    R1 consolidation                (+ F15_PUBLIC_DATA_MATRIX, F15_PROPULSION_SOURCE_GRAPH)
│  ├─ F22/    R1 feasibility audit            (PUBLIC_SOURCE_FEASIBILITY instead of IMPLEMENTATION_FEASIBILITY)
│  └─ F14/  Other/                     status stubs
├─ Aerodynamics/README.md              fundamentals: axes, derivatives, high alpha, identification, mass properties
├─ FlightControls/README.md            fundamentals: SAS/CAS, scheduling, actuators, PIO, trim, linearization
├─ Propulsion/README.md                fundamentals: installed/uninstalled, in-flight thrust, spool dynamics, inlets
└─ Validation/README.md                fundamentals: check-cases, parameter estimation, correlation
```

Tools (Python 3 standard library only):

```
python3 Tools/build_aerospace_source_views.py    # regenerate SOURCES.json slices, the numeric index and the generated tables
python3 Tools/validate_aerospace_source_library.py
```

The validator runs the generator in check mode, so a stale view fails validation.

## 3. Core rules

These add to the rules in [`Docs/Reference/README.md`](../../Reference/README.md). They do not replace them.

1. **Public sources only.** NASA NTRS, NACA, DTIC Distribution A, AFRL/USAF public reports, NATO/RTO public reports, FAA, public university repositories, AFIT theses, and public manufacturer releases. Internet Archive is used only as a mirror of a clearly public document.
2. **Restricted means stop.** Distribution-limited, export-controlled, classified or proprietary material is recorded (so nobody looks for it again) with `implementation_usefulness = PROHIBITED` or `UNAVAILABLE`. It is never reconstructed. Examples: F/A-18 NATOPS (Distribution C); F100 specification CP2903B (classified); AFFTC-TR-75-32 (AD-B accession).
3. **Provenance and configuration are separate axes.** `provenance_grade` says how the data was produced. `configuration_scope` says what it describes.
4. **Serial identity is not configuration identity.** NASA 840 (F/A-18 HARV), NASA 836 (F-15B), the NF-16D VISTA and F100 engine P680063 each flew in several configurations.
5. **No naked numbers.** Every value records field, quantity, configuration, source, location, origin, exactness, precision, units, convention, verification level, implementation use and validation use.
6. **Conflicts are recorded, not resolved by preference.** Both values are kept, each tagged with its configuration.
7. **A repeated number is not independent confirmation.** See the repeated-number register in [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md#2-repeated-number-register).
8. **No guessed coefficients, gains or limits.** Missing means `NOT_PUBLICLY_LOCATED` or a gap record, not a plausible fill. No estimate from performance claims.
9. **Verification levels record only what the library itself inspected.** Metadata is never inferred from accession-number patterns.
10. **PDFs stay out of Git by default.** Record `local_filename` and `sha256` when a file is retrieved. Git holds metadata, page indexes, lineage, small derived datasets and the scripts that produced them.

## 4. Retrieval limitation (read this before trusting anything)

r0 and R1 were built in temporary cloud sessions whose network egress policy **blocked** `ntrs.nasa.gov`, `apps.dtic.mil`, `archive.org`, `core.ac.uk`, `nasa.gov` page fetches and university repositories. The library's research channel was a web-search index (catalogue records, abstracts, short extracts), plus reading the Maverick repository itself.

Consequences:

- **The library opened no PDF.** `local_filename` and `sha256` are `null`, `retrieval_status` is `NOT_RETRIEVED_EGRESS_BLOCKED`, and nothing is `PAGE_VERIFIED`.
- Verification levels (R1 vocabulary): `SEARCH_LEAD_ONLY` < `CATALOGUE_VERIFIED` < `ABSTRACT_VERIFIED` < `CONTENT_EXTRACT_VERIFIED` < `PAGE_VERIFIED` (see [`SCHEMA.md` §2](SCHEMA.md#2-verification-levels)).
- Many F-15, F-16 and F/A-18 sources **were** retrieved and read by other Maverick work (with PDF hashes in repository manifests). Those readings are cross-referenced, not inherited. A source known to the library only through a repository document is `SEARCH_LEAD_ONLY` at library level, and its values are `REPOSITORY_READING`.
- Unconfirmed authors, titles, report numbers and years are marked `UNCONFIRMED` or left `null`. They are never filled from memory.

The next package that matters is a library page-verification pass from an environment that can reach NTRS. It can confirm repository readings quickly by comparing PDF hashes (see [`AIRCRAFT_DATA_COVERAGE_MATRIX.md`](AIRCRAFT_DATA_COVERAGE_MATRIX.md) for where that pays off most).

## 5. Adding or upgrading a source

1. Search from source chains, not at random. Read the bibliography, find the upstream primary report and the downstream reproductions, then search exact report numbers and NTRS/DTIC accession numbers (see `SCHEMA.md` §8).
2. Add or extend the record in `SOURCE_INDEX.json`. Every field in `SCHEMA.md` is required. Use `null`, `"unknown"`, `NOT_STATED` or `UNCONFIRMED` rather than omitting a field.
3. If you opened the PDF: record `local_filename`, `sha256`, the page, figure and table index, and raise the level to `PAGE_VERIFIED`. If a repository manifest already lists a hash for the same document, compare it and note the result.
4. Put any number in `KNOWN_DATA.json` with all provenance fields.
5. Update the aircraft `SOURCE_GRAPH.md` if lineage changed.
6. Run the generator, then the validator.
7. Time-box dead ends. If an original cannot be located, mark it `NOT_PUBLICLY_LOCATED` and move on.

## 6. Scope boundary

This branch touches only `Docs/Research/Aerospace/**`, `Tools/validate_aerospace_source_library.py` and `Tools/build_aerospace_source_views.py`. It does not modify `Assets/**`, FlightDynamics runtime code, scenes, prefabs, weapons, sensors, AI, gameplay or F15Replacement.
