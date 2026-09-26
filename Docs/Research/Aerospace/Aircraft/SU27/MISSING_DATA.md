# Su-27 Missing Data (SU27-R0 feasibility)

Everything needed for a 6-DOF model of the baseline production Su-27 / Su-27S is missing from the located public record. This list is the reason for the feasibility verdict, not a shopping list.

| Missing | Configuration | Status | Notes |
|---|---|---|---|
| Reference area, MAC, reference span, moment reference | baseline, T-10S, tunnel models | NOT_PUBLICLY_LOCATED | `SU27-SUKHOI-TSAGI-AERO-MASS-DATA`. Physical wing area and span repeated in secondary sources are identity data. Do not use them as reference quantities |
| Mass with defined loading, CG, inertias | baseline | NOT_PUBLICLY_LOCATED | Secondary weights have no loading definition |
| Static, dynamic and control aerodynamic coefficients | baseline | NOT_PUBLICLY_LOCATED | The model is described in `ICAS-2002-POGOSYAN-SU27` (printed data unknown). The 1987 T-105 data (`SU27-NIZH-2008-ZHELNIN`) are not published |
| Unsteady / hysteresis model parameters | any Su-27 configuration | NOT_PUBLICLY_LOCATED | Structure described (ICAS 2002); values unknown |
| FCS control laws, gains, schedules, limiter settings | baseline | NOT_PUBLICLY_LOCATED | `SU27-SDU10-FCS-DESIGN-DATA`. Architecture partly public (`SU27-TSAGI-CENTENARY-ARTICLE`) |
| FCS channel architecture and backup | baseline | Secondary sources disagree | Pitch FBW is consistently described. Backup and roll/yaw implementation differ between secondary sources ([`SOURCE_GRAPH.md` §4](SOURCE_GRAPH.md#4-secondary-source-disagreement-not-registered-as-a-value-conflict)) |
| Surface position limits, actuator rates and dynamics | baseline | NOT_PUBLICLY_LOCATED | Values in game and enthusiast models are `PROHIBITED` |
| AL-31F thrust deck, fuel flow, installation effects, spool dynamics | `ENG-AL-31F` | NOT_PUBLICLY_LOCATED | `SU27-AL31F-PERFORMANCE-DECK`. Only an official 12,500 kgf class figure with no stated condition. Do not scale a curve to it |
| Trim data and flight time histories | baseline | NOT_PUBLICLY_LOCATED | ICAS 2002 says flight tests at Sukhoi and LII underpin the model; no data located |

## Numbers seen but deliberately not recorded

These appear in encyclopedic, journalistic, game or enthusiast sources. They are listed so that nobody adopts them later by accident. None has a located primary source.

| Claim | Where it circulates | Why not recorded |
|---|---|---|
| Wing area about 62 m^2, span about 14.7 m, length and height | Encyclopedias, news-agency fact sheets, specification sites (some quote the export Su-27SK) | Secondary; physical, not reference, quantities; variant often unstated |
| Empty, normal and maximum take-off weights | Same | No loading definition; figures vary by source and variant |
| AoA limiter setting (a figure near 26 deg is often quoted) | Encyclopedic Cobra descriptions | No primary source; limiter logic not public |
| Cobra peak AoA of roughly 90 to 120 deg | Encyclopedic and popular accounts | Demonstration narrative; not a measured, configuration-identified value |
| AL-31F dry ("maximum non-afterburning") rating | Encyclopedias | No official statement located |
| Surface travel, actuator rates, FCS gains | Game manuals (DCS), enthusiast JSBSim models (FlightGear Su-27SK) | Game and enthusiast data: `PROHIBITED` |

## Excluded on purpose

- **Unofficial flight-manual copies** (`SU27-RLE-FLIGHT-MANUAL`): release status not established; not sought or used.
- **Unlicensed book scans** (for example of `SU27-BYUSHGENS-1998-TSAGI-MONOGRAPH`): only a lawfully obtained copy may be read.
- **Derivative data** (Su-30, Su-33, Su-27M/Su-35, Su-37, Su-27LL-PS, export and trainer variants): `DERIVATIVE_ONLY`, never baseline.
- **Restricted, classified or export-controlled material**: not sought.

## Leads worth one more look (feasibility only)

1. `ICAS-2002-POGOSYAN-SU27`: open the PDF (icas.org is blocked in this session). Record which figures print data, the configuration and the reference quantities. Confirm the full title and author list.
2. `SU27-BYUSHGENS-1998-TSAGI-MONOGRAPH`: check a lawful copy for Su-27 or "integral layout" tunnel data with model identity.
3. `SU27-NIZH-2008-ZHELNIN` and the sibling article "Прорыв в сверхманевренность" (Nauka i Zhizn, nkj.ru/archive/articles/4079): confirm the T-105 statement and whether any figure is reproduced.
4. `SU27-TSAGI-CENTENARY-ARTICLE`: identify the original author and publication, and confirm which statements refer to the baseline aircraft.

A network policy that allows icas.org, nkj.ru and tsagi.ru would let the first and third leads be checked in a later round.
