# Source Library Metadata Schema v1

Schema IDs:
- `maverick.aerospace.source-index.v1`: `SOURCE_INDEX.json`
- `maverick.aerospace.known-data.v1`: `Aircraft/<type>/KNOWN_DATA.json`

`Tools/validate_aerospace_source_library.py` enforces everything marked **(validated)**.

---

## 1. Source record (`SOURCE_INDEX.json` → `sources[]`)

Every field is required **(validated)**. Unknown values are `null`, `"unknown"`, or a string that starts with `UNCONFIRMED`. Never omit a field.

| Field | Meaning |
|---|---|
| `source_id` | Stable ID, upper-case with hyphens. Prefer the report number (`NASA-TM-110216`), else `NTRS-<id>`, else `<VENUE>-<year>-<key>` **(validated: unique, pattern)** |
| `title`, `authors[]`, `organization`, `year` | Bibliographic identity. Unconfirmed authors are written as `UNCONFIRMED (...)` |
| `report_number` | As printed (NASA-TM-, TP-, CR-, RP-, SP-, AIAA paper no., DTIC AD no.) |
| `ntrs_id` | NTRS document ID (e.g. `19960027892`), or `null` |
| `public_url` | Canonical citation page (NTRS citation page preferred) |
| `pdf_url` | Official NTRS PDF, only when `public_access_status = PUBLIC_NTRS_PDF_LISTED` **(validated)** |
| `mirror_url` | Secondary copy (LTRS mirror, archive.org, CORE, author page). A mirror never replaces the canonical citation |
| `local_filename`, `sha256` | Set only when a file was actually retrieved. `sha256` is 64 lower-case hex **(validated)** |
| `public_access_status` | See §3 **(validated)** |
| `retrieval_status` | `NOT_RETRIEVED_EGRESS_BLOCKED`, `NOT_RETRIEVED`, `RETRIEVED_EXTERNAL_STORE`, `RETRIEVED_IN_REPO` **(validated)** |
| `verification_level` | See §2 **(validated)** |
| `aircraft`, `aircraft_variant`, `serial_or_tail_number`, `research_configuration`, `engine_configuration`, `FCS_configuration`, `date_or_phase` | Configuration identity (see §5) |
| `configuration_ids[]` | Links into a `KNOWN_DATA.json` configuration registry **(validated: must resolve)** |
| `topics[]` | Free-text topic tags |
| `contains{}` | Exactly the 22 keys below. Each value is `yes` (confirmed by abstract or extract), `likely` (implied, unconfirmed), `no`, or `unknown` **(validated)** |
| `important_pages[]`, `important_figures[]`, `important_tables[]` | `{id, content, verification}` objects. Empty until indexed |
| `page_index_status` | `NOT_INDEXED_R0`, `PARTIAL_UNVERIFIED`, `INDEXED` |
| `provenance_grade` | See §4 **(validated)** |
| `configuration_scope` | See §4 **(validated)** |
| `implementation_usefulness`, `validation_usefulness` | `{rating, note}` **(validated)**. Non-public sources must be `PROHIBITED` or `UNAVAILABLE` **(validated)** |
| `limitations[]`, `known_conflicts[]`, `notes`, `verification_notes` | Free text |
| `related_sources[]` | `{source_id, relation}` **(validated: must resolve)**. Relation vocabulary is in `SOURCE_GRAPH.md` |

`contains` keys: `geometry, mass, cg, inertia, aerodynamic_coefficients, static_derivatives, rate_derivatives, control_derivatives, reference_geometry, FCS_architecture, FCS_numeric_gains, actuator_limits, actuator_rates, propulsion, thrust_data, source_code, equations, tables, plots, flight_data, trim_data, validation_data`.

## 2. Verification levels

This axis records how far **we** have checked the source. It is separate from how good the source is.

| Level | Meaning | Allowed use |
|---|---|---|
| `L0_UNVERIFIED_LEAD` | Mentioned somewhere; existence not confirmed | Search target only |
| `L1_CITATION_CONFIRMED` | Catalogue record found: title/ID/report number | Planning |
| `L2_ABSTRACT_CONFIRMED` | Abstract or scope confirmed from the catalogue record or an index extract | Planning, lineage |
| `L3_CONTENT_EXTRACT` | A specific content item (value, range, statement) seen in a search or index extract; page not confirmed | Lead for verification; never code |
| `L4_PAGE_VERIFIED` | Document retrieved and hashed; page/table/figure checked by a reader | Implementation, for the matching configuration |
| `L5_TRANSCRIBED_CROSSCHECKED` | Values transcribed, then independently cross-checked (e.g. the two-pass visual check plus SI/US cross-check used for TP-1538 Table VI in `Docs/Reference/Data/F16/TP1538/`) | Frozen reference data |

L4 and above requires `retrieval_status = RETRIEVED_*` and a `sha256` **(validated)**.

## 3. Public access status

| Value | Meaning |
|---|---|
| `PUBLIC_NTRS_PDF_LISTED` | NTRS record with a downloadable PDF seen in the index |
| `PUBLIC_NTRS_CITATION_ONLY` | NTRS record confirmed; PDF availability not confirmed |
| `PUBLIC_NTRS_RECORD_ID_UNCONFIRMED` | Public NASA report, NTRS ID not pinned down |
| `PUBLIC_WEB_PAGE` | Official public web page (nasa.gov etc.) |
| `PUBLIC_DISTRIBUTION_A` | DoD document marked Distribution A (approved for public release) |
| `PUBLIC_UNIVERSITY_REPOSITORY` | Thesis or report in an open institutional repository |
| `PUBLIC_AUTHOR_HOSTED` | Publicly released paper, copy hosted by author or institution |
| `PUBLIC_RELEASE_PAYWALLED` | Publicly released but commercially published (AIAA, journals) |
| `NOT_PUBLIC_DISTRIBUTION_LIMITED` | Distribution B/C/D/E/F or similar. **Stop.** |
| `NOT_PUBLIC_PROPRIETARY` | Manufacturer-proprietary. **Stop.** |
| `NOT_PUBLICLY_LOCATED` | Searched and time-boxed without finding a public copy |

## 4. Provenance grade vs configuration scope

These are two independent axes. Never collapse them into one label.

**`provenance_grade`: how the data came to exist**

| Value | Meaning |
|---|---|
| `ORIGINAL_PRIMARY` | Originating report of the measurement, analysis or design |
| `PUBLIC_REPRODUCTION` | Faithful reprint or re-issue of a primary source |
| `DERIVED_SIMULATOR` | Simulation assembled from upstream data (may add fits, fixes, increments) |
| `CURVE_FIT` | Analytic fit to an upstream table or database |
| `DIGITIZED_PRIMARY_FIGURE` | Values read off a primary figure (needs a digitization record, see §7) |
| `CROSS_VALIDATION_ONLY` | External implementation used as an oracle, never as a value authority |
| `BACKGROUND_ONLY` | Fact sheets, textbooks, overviews |

**`configuration_scope`: what the data describes**

| Value | Meaning |
|---|---|
| `EXACT_AIRFRAME` | A specific tail number in a specific phase |
| `PRODUCTION_FAMILY` | Production model family (block/lot may still matter) |
| `PREPRODUCTION` | FSD / prototype / pre-production airframe |
| `RESEARCH_MODIFIED` | Airframe with research hardware or research control laws |
| `WIND_TUNNEL_MODEL` | Sub-scale or full-scale tunnel article |
| `SIMULATION_ONLY` | A simulation's own definition |
| `UNKNOWN` | Not established, or not aircraft-specific (fundamentals) |

## 5. Configuration registry (`KNOWN_DATA.json` → `configurations[]`)

`configuration_id`, `label`, `airframe`, `serial`, `engine`, `fcs`, `research_hardware`, `dates`, `status`, `notes`.

The IDs are the join key between sources and values. Track model/variant, production vs pre-production, block, tail/serial, research modifications, engine model/build, nozzle, FCS revision, external research hardware and test phase/date wherever known.

## 6. Value record (`KNOWN_DATA.json` → `values[]`)

Each field answers one of the ten numeric-data questions:

| # | Question | Field |
|---|---|---|
| 1 | What quantity? | `quantity`, `symbol` |
| 2 | Which aircraft/configuration? | `configuration_ids[]` **(validated: non-empty, resolves)**, `loading_state` |
| 3 | What source? | `source_id` **(validated: resolves)**, or `UNRESOLVED` + `candidate_source_ids[]` **(validated)** |
| 4 | What page/table/figure? | `location` (`NOT_CAPTURED` if unknown) **(validated: present)** |
| 5 | Original or reproduced? | `origin`: `ORIGINAL`, `REPRODUCED`, `DIGITIZED`, `DERIVED` **(validated)** |
| 6 | Exact / approximate / digitized? | `exactness`: `EXACT`, `APPROXIMATE`, `DIGITIZED`, `SEARCH_EXTRACT`, `SEARCH_EXTRACT_APPROXIMATE`, `ABSTRACT_STATEMENT`, `PUBLIC_FACT_SHEET` **(validated)** |
| 7 | Units? | `units` **(validated: present)** |
| 8 | Reference convention? | `reference_convention` **(validated: present)** |
| 9 | Usable for implementation? | `implementation_use`: `ALLOWED` only if the source is L4+, the location is captured and exactness is page-level **(validated)** |
| 10 | Validation only? | `validation_use` **(validated: present)** |

Plus `value`, `conflicts_with[]` (**validated**: must resolve) and `notes`.

## 7. Digitized datasets (future)

Every digitized dataset gets its own folder beside the aircraft pack, containing:
- `manifest.json`: source, figure number, axis labels and units, configuration, series identity, extraction method and tool, pixel/plot uncertainty, interpolation assumptions, excluded or unreadable points, sha256 of the source PDF and of every output file.
- raw digitized CSV in figure units, and a converted CSV if needed.
- the script that produced the output.

Illegible points are `NaN` with a note, and are never repaired. Follow the existing `Docs/Reference/Data/F16/TP1538/manifest.json` as the pattern.

## 8. Search procedure for each useful source

1. Inspect the bibliography.
2. Identify upstream primary reports.
3. Identify downstream reproductions and users.
4. Search exact report numbers.
5. Search author + title.
6. Search NTRS/DTIC accession numbers.
7. Record public availability.
8. Time-box. Mark `NOT_PUBLICLY_LOCATED` and move on.
