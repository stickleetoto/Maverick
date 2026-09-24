# Source Library Metadata Schema v2 (R1, R2 additions)

Schema IDs:
- `maverick.aerospace.source-index.v2`: `SOURCE_INDEX.json`
- `maverick.aerospace.known-data.v2`: `Aircraft/<type>/KNOWN_DATA.json`
- `maverick.aerospace.pack-sources.v1`: `Aircraft/<type>/SOURCES.json` (generated)
- `maverick.aerospace.numeric-field-index.v2`: `AIRCRAFT_NUMERIC_FIELD_INDEX.json` (generated)
- `maverick.aerospace.conflict-register.v1`: `CONFLICT_REGISTER.json` (canonical; R2)
- `maverick.aerospace.source-files.v1`: `SOURCE_FILES_MANIFEST.json` (canonical; R2)

`Tools/validate_aerospace_source_library.py` enforces everything marked **(validated)**. `Tools/build_aerospace_source_views.py` generates the views; the validator fails if any view is stale **(validated)**.

**Changes from v1 (r0):** verification levels renamed to the R1 vocabulary (§2); new source fields `aircraft_packs`, `index_group`, `existing_maverick_analysis` (§1, §1a); configuration records gain `configuration_scope` and `lineage_family` (§5); value records gain `field`, `precision`, `verification_level` and `existing_maverick_analysis`, and exactness gains `REPOSITORY_READING` (§6); the numeric field index (§6a).

---

## 1. Source record (`SOURCE_INDEX.json` → `sources[]`)

Every field is required **(validated)**. Unknown values are `null`, `"unknown"`, or a string that starts with `UNCONFIRMED`. Never omit a field.

| Field | Meaning |
|---|---|
| `source_id` | Stable ID, upper-case with hyphens. Prefer the report number (`NASA-TM-110216`), else `NTRS-<id>`, else `<VENUE>-<year>-<key>` **(validated: unique, pattern; no two sources may share a primary report number or an NTRS ID)** |
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
| `aircraft_packs[]` | `FA18`, `F16`, `F15`, `F22`, `F14`, `FUNDAMENTALS`, `OTHER` **(validated: non-empty, enum)**. A source may belong to several packs (e.g. the F100 lineage is in `F15` and `F16`) |
| `index_group` | Heading under which `SOURCE_INDEX.md` lists the source **(validated: non-empty)** |
| `existing_maverick_analysis[]` | Cross-references to Maverick documents that analysed this source (§1a) **(validated: structure)** |
| `aircraft`, `aircraft_variant`, `serial_or_tail_number`, `research_configuration`, `engine_configuration`, `FCS_configuration`, `date_or_phase` | Configuration identity: airframe/block, modifications, engine, FCS, date (see §5). For a source in any aircraft pack, `aircraft`, `aircraft_variant`, `research_configuration`, `engine_configuration`, `FCS_configuration` and `date_or_phase` must be non-empty; write `NOT_STATED` rather than leaving them null **(validated)** |
| `configuration_ids[]` | Links into a `KNOWN_DATA.json` configuration registry **(validated: must resolve; non-empty for aircraft-pack sources)** |
| `topics[]` | Free-text topic tags |
| `contains{}` | Exactly the 22 keys below. Each value is `yes` (confirmed by abstract or extract), `likely` (implied, unconfirmed), `no`, or `unknown` **(validated)** |
| `important_pages[]`, `important_figures[]`, `important_tables[]` | `{id, content, verification}` objects. Empty until indexed |
| `page_index_status` | `NOT_INDEXED_R0`, `PARTIAL_UNVERIFIED`, `INDEXED` |
| `provenance_grade` | See §4 **(validated)** |
| `configuration_scope` | See §4 **(validated)** |
| `implementation_usefulness`, `validation_usefulness` | `{rating, note}` **(validated)**. Non-public sources must be `PROHIBITED` or `UNAVAILABLE` **(validated)** |
| `limitations[]`, `known_conflicts[]`, `notes`, `verification_notes` | Free text |
| `related_sources[]` | `{source_id, relation}` **(validated: must resolve)**. Relation vocabulary is in `SOURCE_GRAPH.md` |

### 1a. `existing_maverick_analysis[]`

Records that another Maverick document (on any branch) analysed the source. It is evidence that someone in the project read something. **It never raises `verification_level`.**

| Field | Meaning |
|---|---|
| `path` | Repository path of the analysing document |
| `branch` | Branch and commit, e.g. `claude/f15-full-implementation @89140b9` |
| `relationship` | What the document did with the source (page read, transcription, audit, citation) |
| `locator` | Page/table/figure the document cites, as it cites it (not re-checked by the library) |
| `level` | `REPO_PAGE_READ` (the document says it read the page), `REPO_PAGE_TRANSCRIBED_CROSSCHECKED` (transcribed and cross-checked, e.g. with a manifest and hash), `REPO_CITATION_ONLY` **(validated)** |
| `note` | Free text, e.g. the PDF sha256 a repository manifest records |

`contains` keys: `geometry, mass, cg, inertia, aerodynamic_coefficients, static_derivatives, rate_derivatives, control_derivatives, reference_geometry, FCS_architecture, FCS_numeric_gains, actuator_limits, actuator_rates, propulsion, thrust_data, source_code, equations, tables, plots, flight_data, trim_data, validation_data`.

## 2. Verification levels

This axis records how far **the library** has checked the source itself. It is separate from how good the source is, and from what other Maverick documents say they read (§1a). Never upgrade beyond what was actually inspected. Never infer metadata (year, author, report type) from an accession-number pattern.

| Level (R1) | r0 name | Meaning | Allowed use |
|---|---|---|---|
| `SEARCH_LEAD_ONLY` | L0 | Mentioned somewhere (including only in another Maverick document); the library has not confirmed it | Search target only |
| `CATALOGUE_VERIFIED` | L1 | Catalogue record seen: title / ID / report number | Planning |
| `ABSTRACT_VERIFIED` | L2 | Abstract or scope seen in the catalogue record or an index extract | Planning, lineage |
| `CONTENT_EXTRACT_VERIFIED` | L3 | A specific content item (value, range, statement) seen in a search or index extract; page not confirmed | Lead for verification; never code |
| `PAGE_VERIFIED` | L4 / L5 | Document retrieved and hashed by the library; the page, table or figure checked by a reader (L5-style transcription with cross-check is recorded in the value's notes) | Implementation, for the matching configuration |

`PAGE_VERIFIED` requires `retrieval_status = RETRIEVED_*`, a `sha256`, and the file(s) listed in `SOURCE_FILES_MANIFEST.json` with matching hashes **(validated)**. A value's own `verification_level` may not exceed its source's level **(validated)**.

**Machine-readable primary files (R2).** For a data file (DAVE-ML, CSV) the "page" is the file element. `PAGE_VERIFIED` then means the cited element (`variableDef`, `griddedTableDef`, calculation, check-shot, CSV row) was read in the hashed file, and the value's `location` gives the file name and line number. A multi-file dataset record carries as its `sha256` the SHA-256 of the sorted lines `"<file sha256>  <repository path>\n"` of its files **(validated)**.

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

`configuration_id`, `label`, `airframe`, `serial`, `engine`, `fcs`, `research_hardware`, `dates`, `status`, `configuration_scope`, `lineage_family`, `notes`. `airframe`, `engine`, `fcs`, `dates`, `status`, `configuration_scope` and `lineage_family` must be non-empty (`NOT_RECORDED` is allowed) **(validated)**. Configuration IDs are unique across all packs **(validated)**; engine configurations (`ENG-…`) are defined once, in the pack where they are primary, and referenced from other packs.

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
| 6 | Exact / approximate / digitized? | `exactness`: `EXACT`, `APPROXIMATE`, `DIGITIZED`, `SEARCH_EXTRACT`, `SEARCH_EXTRACT_APPROXIMATE`, `ABSTRACT_STATEMENT`, `PUBLIC_FACT_SHEET`, `REPOSITORY_READING` **(validated)**. `REPOSITORY_READING` = the value was read in another Maverick document; it must have `verification_level = SEARCH_LEAD_ONLY` and an `existing_maverick_analysis` entry **(validated)** |
| 7 | Units? | `units` **(validated: present)** |
| 8 | Reference convention? | `reference_convention` **(validated: present)** |
| 9 | Usable for implementation? | `implementation_use`: `ALLOWED` only if the source **and** the value are `PAGE_VERIFIED`, the location is captured, exactness is page-level, and units and convention are stated **(validated)** |
| 10 | Validation only? | `validation_use` **(validated: present)** |

Plus `value`, `conflicts_with[]` (**validated**: must resolve across all packs), `notes`, and the v2 fields:

| Field | Meaning |
|---|---|
| `field` | Canonical field name from the vocabulary in `Tools/build_aerospace_source_views.py` (`FIELD_GROUPS`), e.g. `reference_area`, `physical_span`, `Ixz`, `surface_rate_limit`, `thrust_table` **(validated)** |
| `precision` | Printed precision (e.g. `1 slug-ft^2`, `0.01 ft`), `UNKNOWN`, or a note such as "as extracted" **(validated: present)** |
| `verification_level` | What the library has seen of this value (§2) **(validated: ≤ source level)** |
| `existing_maverick_analysis[]` | As §1a, for this value |

Gap records are allowed: a value such as `NOT PUBLICLY LOCATED` with `implementation_use = NOT_A_MODEL_PARAMETER` documents a failed search so it is not repeated blindly.

### 6.1 When `implementation_use = ALLOWED` (R2 rule, validated)

All of these must hold:

1. the source is `PAGE_VERIFIED` and the value is `PAGE_VERIFIED`;
2. `location` names a page, table, figure, section, equation or file line;
3. `source_id` is a real source (not `UNRESOLVED`);
4. every configuration in `configuration_ids` has a known `configuration_scope` (not `UNKNOWN`);
5. `units` and `reference_convention` are stated;
6. the source has a non-empty `provenance_grade`;
7. the value is not listed in any `UNRESOLVED` conflict in `CONFLICT_REGISTER.json`.

## 6a. Numeric field index (`AIRCRAFT_NUMERIC_FIELD_INDEX.json`, generated)

One record per value across all packs: `value_id`, `aircraft`, `configuration`, `configuration_scope`, `field`, `quantity`, `value`, `units`, `precision`, `source_id`, `candidate_source_ids`, `page`, `location_as_recorded`, `provenance` (source provenance grade), `value_origin`, `exactness`, `verification_level`, `source_verification_level`, `implementation_use`, `implementation_allowed`, `has_existing_maverick_analysis`, `conflict_ids`, `unresolved_conflict`, `why_not_allowed` (plain-language reason when not allowed), `known_data_file`.

- `page` is `null` unless both the source and the value are `PAGE_VERIFIED` **(validated)**.
- `implementation_allowed` is `true` only when `implementation_use = ALLOWED` and the page is verified **(validated)**.
- Every KNOWN_DATA value appears exactly once **(validated)**.

## 6b. Conflict register (`CONFLICT_REGISTER.json`, R2)

One entry per conflict: `conflict_id`, `aircraft`, `quantity`, `claims[]` (`claim`, `source_id`, `configuration_id`), `classification[]`, `status`, `resolution`, `value_ids[]`, `source_ids[]`, `evidence_basis`, `next_check`.

- Classifications: `SAME_SOURCE_TYPO_OR_REVISION`, `DIFFERENT_CONFIGURATION`, `DIFFERENT_PHASE`, `INSTALLED_VS_UNINSTALLED`, `PHYSICAL_VS_REFERENCE_GEOMETRY`, `ROUNDING`, `LINEAGE_TRANSCRIPTION_DIFFERENCE`, `IMPLEMENTATION_DIFFERENCE`, `DIFFERENT_DEFINITION`, `METADATA_DISAGREEMENT`, `NOT_A_CONFLICT`, `STILL_UNRESOLVED` **(validated)**.
- Status: `RESOLVED`, `RESOLVED_PER_CONFIGURATION`, `UNRESOLVED` **(validated)**. `UNRESOLVED` requires `STILL_UNRESOLVED`; a resolved entry must not carry it and must have a resolution text **(validated)**.
- Every `conflicts_with` pair in any `KNOWN_DATA.json` must appear together in one entry **(validated)**.

## 6c. Source-file manifest (`SOURCE_FILES_MANIFEST.json`, R2)

One record per retrieved file: `source_id`, `filename`, `repository_path`, `source_url`, `retrieved_via`, `report_or_file_id`, `sha256`, `size_bytes`, `acquisition_date`, `stored_at` **(validated: present, hash format, source resolves)**. Files are stored outside Git. The manifest also records the hosts that were blocked in the session.

## 6d. Field vocabulary additions (R2)

`Ixy`, `Iyz`, `aero_coefficient_definition`, `aero_coefficient_table`, `rate_normalization`, `control_input_definition`, `aero_domain_control`, `trim_state`, `check_case_dataset` (see `FIELD_GROUPS` in the generator).

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
