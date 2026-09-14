# F/A-18 Configuration Compatibility V0.2

Status: **STRICT CONFIGURATION BOUNDARY UPDATE**

## Non-transfer rules

The following are separate authority domains:

1. `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`
2. `NASA_F18_HARV_160780_PHASE1_BASIC`
3. `NASA_F18_HARV_160780_PHASE2_TVC_NASA0`
4. `NASA_F18_HARV_160780_PHASE2_TVC_NASA1A`
5. `NASA_F18_HARV_160780_PHASE3_TVC_ANSER`
6. operational F/A-18A/B/C/D families
7. AAW test aircraft
8. F/A-18E/F Super Hornet

No numeric field is transferable merely because all configurations are called F/A-18/F-18.

## Newly verified boundaries

### `f18bas` versus HARV Phase I

TM-107601 is exact authority for its simulation configuration. It is not an exact serial-number model of BuNo 160780. Its Fighter Escort 60%-fuel state (31,665 lb, FS 457.3, WL 101.6, Ixz -2430) differs from the exact Phase-I HARV table state in TP-97-206539 (31,980 lb, FS 454.33, WL 105.24, Ixz -2039). These states must remain separate.

### Phase I versus Phase II/III

TP-97-206539 Table 3 directly shows the Phase-I to modified Phase-II/III mass-property shift. Phase-II/III added TVCS, spin chute/emergency systems/ballast and associated equipment. Phase-II derivative data are not direct Phase-I coefficient authority.

### Actuator-version boundary

TM-107601 and TM-110216 document different actuator implementations. In particular:

- rudder rate: `f18bas` 61 deg/s; HARV/Dryden 82 deg/s,
- LEF: `f18bas` 18 deg/s and -3..+34 deg; HARV/Dryden 15 deg/s and -3..+33 deg,
- `f18harv` primary controls use higher-fidelity second-order/hinge-moment-aware models.

The later HARV model must not silently overwrite the `f18bas` profile.

### Engine-model boundary

TM-4240 describes a 1990 simplified F404-GE-400/HARV modified-nozzle engine model. TM-110216 documents a later `f18harv` implementation with the same table topology but different increasing-PLA rate limits. Both are primary sources for their own model version.

The Figure-4 Max-AB digitization from TM-4240 is **not** a generic production F404-GE-400 thrust deck and is **not** authority for a production F/A-18C/D installation.

### NASA-CR-194838 derivative boundary

The 1992 OBES campaign is Phase-II HARV. Its aerodynamic-control/stability estimates are valuable flight evidence, but the report states that thrust-vectoring doublet information was excluded from the analysis. Vane-derivative panels therefore require a special provenance audit and are not automatically flight-derived authority.

## Transfer policy

- Exact tables from Phase-I HARV -> exact Phase-I profile only.
- `f18bas` simulation tables -> `f18bas` profile only unless independent compatibility evidence exists.
- Phase-II/III flight derivatives -> validation/cross-check for Phase I unless the relevant hardware/configuration field is explicitly demonstrated unchanged.
- Generic F404 ratings -> search lead only.
- Digitized figures -> approximation/cross-validation layer, never silently upgraded to exact arrays.
