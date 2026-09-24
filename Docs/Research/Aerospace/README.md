# Maverick Aerospace Public-Source Library

Status: **r0: skeleton + first F/A-18 source pack.** Research and documentation only. No runtime code.

This library is where Maverick keeps public aerospace engineering sources: what exists, where it comes from, which aircraft configuration it describes, and whether a number from it is safe to use in code. The aim is that the next aircraft starts from this index instead of repeating the source hunt.

It sits next to the existing per-aircraft packs in [`Docs/Reference/`](../../Reference/README.md). Those packs (F-16 TP-1538/Morelli chain, F-15 full-scale catalogue) are still the authority for the F-16 and F-15 work already done. They will be migrated into this index in later packages. Until then, see [Existing packs not yet migrated](SOURCE_INDEX.md#existing-packs-not-yet-migrated).

---

## 1. How to answer "can I use this number in code?"

1. Find the value in `Aircraft/<type>/KNOWN_DATA.json` (or the `.md` view).
2. Read `implementation_use`:
   - `ALLOWED`: the source is page-verified (L4 or above), the page/table/figure is recorded, and the configuration is explicit. You may use it for that configuration only.
   - `BLOCKED_PENDING_PAGE_VERIFICATION`: the value is a lead. Do not put it in code.
   - `PROHIBITED`: the value is not public, or it is only a fact-sheet or identity figure. Never use it in code.
   - `DOMAIN_METADATA_ONLY` / `NOT_A_MODEL_PARAMETER`: use it only as a domain gate, tolerance or scenario reference.
3. Check that `configuration_ids` matches the aircraft configuration you are building. A value for NASA 840 in Phase 2 is **not** a value for a production F/A-18A.
4. Check `conflicts_with`. If it is not empty, read both values before choosing.

**In r0 no value is `ALLOWED`.** See §4.

## 2. Directory map

```
Docs/Research/Aerospace/
├─ README.md                 this file
├─ SCHEMA.md                 metadata schema, enums, verification levels
├─ SOURCE_INDEX.md           human-readable index of every source
├─ SOURCE_INDEX.json         machine-readable index (canonical)
├─ SOURCE_GRAPH.md           library-wide lineage rules + cross-aircraft graph
├─ Aircraft/
│  ├─ README.md              per-aircraft status
│  ├─ FA18/                  r0 pack (priority A)
│  │  ├─ SOURCE_GRAPH.md
│  │  ├─ KNOWN_DATA.md / KNOWN_DATA.json
│  │  ├─ MISSING_DATA.md
│  │  └─ IMPLEMENTATION_FEASIBILITY.md
│  ├─ F16/  F15/  F14/  F22/  Other/   status stubs (queued packages)
├─ Aerodynamics/README.md    fundamentals: axes, derivatives, high alpha, identification
├─ FlightControls/README.md  fundamentals: SAS/CAS, scheduling, actuators, trim, linearization
├─ Propulsion/README.md      fundamentals: turbofan, installed/uninstalled, spool dynamics
└─ Validation/README.md      fundamentals: check-cases, parameter estimation, correlation
```

Topic subfolders (for example `Aerodynamics/HighAlpha/`) are listed in each topic README. A subfolder is created only when its first source note is written, so the tree has no empty placeholder files.

Validation script: [`Tools/validate_aerospace_source_library.py`](../../../Tools/validate_aerospace_source_library.py) (Python 3 standard library only). Run it after every edit:

```
python3 Tools/validate_aerospace_source_library.py
```

## 3. Core rules

These add to the rules in [`Docs/Reference/README.md`](../../Reference/README.md). They do not replace them.

1. **Public sources only.** NASA NTRS, NACA, DTIC Distribution A, AFRL/USAF public reports, FAA, public university repositories, AFIT theses, and public manufacturer releases. Internet Archive is used only as a mirror of a clearly public document.
2. **Restricted means stop.** Distribution-limited, export-controlled or proprietary material is recorded (so nobody goes looking for it again) with `implementation_usefulness = PROHIBITED`. Copies found online do not change a document's release status. Example: the F/A-18 NATOPS manual is Distribution C.
3. **Provenance and configuration are separate axes.** `provenance_grade` says how the data was produced. `configuration_scope` says what it describes. A strong primary source can still describe the wrong airframe.
4. **Serial identity is not configuration identity.** NASA 840 flew in three HARV phases with different mass, hardware and control laws, and its wings later flew on NASA 853 (AAW).
5. **No naked numbers.** Every value records quantity, configuration, source, location, origin, exactness, units, convention, implementation use and validation use. The validator enforces this.
6. **Conflicts are recorded, not resolved by preference.** Both values are kept, with the probable reason if a source supports it.
7. **A repeated number is not independent confirmation.** See the repeated-number register in [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md).
8. **No guessed coefficients, gains or limits.** Missing means `UNAVAILABLE`, not a plausible fill.
9. **PDFs stay out of Git by default.** Store downloads in a durable external folder, for example `E:\aerospace-sources\` or an equivalent. Record `local_filename` and `sha256` in the index. Git holds metadata, page indexes, lineage, small derived datasets, and the scripts that produced them.

## 4. r0 retrieval limitation (read this before trusting anything)

r0 was built in a temporary cloud session. Its network egress policy **blocked** `ntrs.nasa.gov`, `apps.dtic.mil`, `archive.org`, `core.ac.uk`, `nasa.gov` page fetches and university repositories. The only research channel was a web-search index, which returns catalogue records, abstracts and short content extracts.

Consequences:

- **No PDF was opened, downloaded or hashed.** `local_filename` and `sha256` are `null` everywhere, and `retrieval_status` is `NOT_RETRIEVED_EGRESS_BLOCKED`.
- The highest verification level reached is **L3 (content extract)**. Page, figure and table numbers are almost all `NOT_INDEXED_R0`.
- Numbers in `KNOWN_DATA` come from search extracts of named NASA documents. They are useful leads that tell the next pass what to verify and where. They are **not** implementation data.
- A few author lists, report numbers and NTRS IDs could not be confirmed. They are marked `UNCONFIRMED` in place, never filled in.

The first follow-up package (FA18-R1, see [`Aircraft/FA18/IMPLEMENTATION_FEASIBILITY.md`](Aircraft/FA18/IMPLEMENTATION_FEASIBILITY.md#5-next-research-packages)) is a page-verification pass run from an environment that can reach NTRS.

## 5. Adding or upgrading a source

1. Search from source chains, not at random. Read the bibliography, find the upstream primary report and the downstream reproductions, then search exact report numbers and NTRS/DTIC accession numbers (see `SCHEMA.md` §6).
2. Add or extend the record in `SOURCE_INDEX.json`. Every field in `SCHEMA.md` is required. Use `null`, `"unknown"` or `UNCONFIRMED` rather than omitting a field.
3. Add the `source_id` to `SOURCE_INDEX.md`.
4. If you opened the PDF: record `local_filename`, `sha256`, the page, figure and table index, and raise the level to L4.
5. Put any number in `KNOWN_DATA.json` with all provenance fields.
6. Update the aircraft `SOURCE_GRAPH.md` if lineage changed.
7. Run the validator.
8. Time-box dead ends. If an original cannot be located, mark it `NOT_PUBLICLY_LOCATED` and move on.

## 6. Scope boundary

This branch touches only `Docs/Research/Aerospace/**` and `Tools/validate_aerospace_source_library.py`. It does not modify `Assets/**`, FlightDynamics runtime code, scenes, prefabs, weapons, sensors, AI, gameplay or F15Replacement.
