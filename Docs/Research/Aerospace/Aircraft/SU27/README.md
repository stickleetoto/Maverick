# Su-27 pack (SU27-R0 public-source feasibility)

**Feasibility only. No Su-27 model is built.** In short:

- A defensible public-source 6-DOF model of the baseline Su-27 / Su-27S is not possible. Only engine identity is strong; every model-defining number is `NOT_PUBLICLY_LOCATED`.
- A partial research model is possible only as a Su-27-*structured* model with tuned numbers.
- A clearly labelled fictional, Su-27-inspired adversary is the defensible option.

Details: [`PUBLIC_SOURCE_FEASIBILITY.md`](PUBLIC_SOURCE_FEASIBILITY.md).

Scope: baseline Su-27 / Su-27S, early T-10 / T-10S material, and derivatives only where needed to keep them out. Su-30, Su-33, Su-35 and other derivative data are never baseline authority. Game and enthusiast data are prohibited.

**How far this study got.** It used web-search index extracts only. The document hosts (icas.org, tsagi.ru, nkj.ru, sukhoi.org, uecrus.com, NTRS, DTIC) are blocked in this session, so no page was verified.

| Document | Content |
|---|---|
| [`PUBLIC_SOURCE_FEASIBILITY.md`](PUBLIC_SOURCE_FEASIBILITY.md) | 17-field classification and answers A, B, C |
| [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md) | T-10 / T-10S / production / tunnel / derivative / generic / game lineages, kept apart |
| [`KNOWN_DATA.md`](KNOWN_DATA.md) + [`KNOWN_DATA.json`](KNOWN_DATA.json) | 10 configurations, 3 values (none usable as model inputs) |
| [`MISSING_DATA.md`](MISSING_DATA.md) | What is missing, numbers deliberately not recorded, and the leads worth one more look |
| [`SOURCES.json`](SOURCES.json) | Generated slice of `SOURCE_INDEX.json` (20 records) |

Strongest source per area:

| Area | Source | Level | Reading |
|---|---|---|---|
| Aerodynamics | `ICAS-2002-POGOSYAN-SU27` (Sukhoi/TsAGI, 2002) | extract | Describes the high-alpha model (asymmetric vortex breakdown, hysteresis) built from tunnel and LII flight tests. Printed data unknown |
| Geometry / mass | none | — | `NOT_PUBLICLY_LOCATED` |
| FCS | `SU27-TSAGI-CENTENARY-ARTICLE` | extract | Architecture only: FBW with stability augmentation, subsonic static instability, AoA-scheduled leading-edge flaps |
| Propulsion | `SU27-UEC-2017-AL31F-NEWS` (UEC, official) | extract | AL-31F identity and 12,500 kgf class figure (condition unstated) |
| Validation | `SU27-NIZH-2008-ZHELNIN`; `AIAA-93-0183` | extract / catalogue | Where full-envelope tunnel data were made (T-105, 1987); Western Cobra analysis. No quantitative flight data |
