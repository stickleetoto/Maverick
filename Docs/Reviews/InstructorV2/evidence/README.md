# Evidence

Everything here was produced by Unity 6000.3.16f1 in batch mode, in throwaway detached worktrees of `ad348086e09f7761e5e3e3ecef7cb45f97e8cff0`.

## `reproduction/`

| File | Content |
|---|---|
| `mi2_headless.json` | `MavFdmValidationBatchAdapter` result for `MavMouseAimInstructorV2Validation` (61/0) |
| `mi2_playmode_reproduced.txt` | The PR's Play Mode runner output (29/0); byte-identical to `Docs/FlightDynamics/Data/mouse_instructor_v2/mi2_playmode_report.txt` after newline normalization |

## `probe_source/`

Audit-only code, kept as `.txt` so it never compiles in this repository.

| File | Role |
|---|---|
| `MavAuditProbeRunner.cs.txt` | Play Mode runner. Builds the PR's own `MavF15PilotControlledRig.Create(name, MouseAim, false, AssistedV2)`, one physics step before `StartFromTrim`, then sets the scripted aims and events. Reads state only. Logs CSV every 5 steps (0.1 s). The file holds the **batch-2** scenario list |
| `MavAuditProbeDriver.cs.txt` | Editor driver (`-executeMethod ...MavAuditProbeDriver.RunBatch -auditOut <dir>`). Runs the pure-function probe, then enters Play Mode on the empty batch scene |
| `analysis_scripts.py.txt` | The three Python scripts used to extract extrema, damping and saturation |

To reproduce:
1. Copy the two `.cs.txt` files into `Validation/` and `Editor/` of a throwaway checkout of `ad34808`, renamed to `.cs`.
2. Run Unity batch mode without `-quit`.

## Scenarios

Each scenario starts from the validated trim: Table VII point 36, 91.7 m/s, α 17.5°, path −2.9°. Offsets are relative to the initial flight-path azimuth and elevation: +az is right, +el is up. dt is 0.02 s and `captureFramerate` is 50.

### Batch 1 (`probe_batch1/`)

| id | Duration | Aim offset az / el | Events |
|---|---|---|---|
| DROP10_RESTORE | 30 s | +25 / 0 | 3 s: `DropCommandSignal` + camera required (loss); 13 s: camera restored |
| DROP3_RESTORE | 20 s | +25 / 0 | loss 3 s; restore 6 s |
| HANDSOFF_IN_TURN_120S | 120 s | +30 / 0 | 8 s: hands-off |
| INPUTLOSS_NEUTRAL_120S | 120 s | +30 / 0 | 8 s: camera lost, default policy |
| MOVING_2DPS_120S | 120 s | target az moves +2 °/s from t = 2 s | — |
| TURN15R_40S | 40 s | +15 / 0 | — |
| TURN90L_40S | 40 s | −90 / 0 | — |
| DESC8_LAT0 | 20 s | 0 / −8 | — |
| DESC8_LAT2L | 20 s | −2 / −8 | — |
| DESC12_LAT3R | 20 s | +3 / −12 | — |
| DIVE30_HOLD_60S | 60 s | 0 / −30 | — |
| DIVE60_HOLD_60S | 60 s | 0 / −60 | — |
| CLIMB80_HOLD_60S | 60 s | 0 / +80 | — |
| REVERSAL180_120S | 120 s | +180 / 0 | — |

### Batch 2 (`probe_batch2/`)

| id | Duration | Aim offset az / el | Events |
|---|---|---|---|
| DROP15_RESTORE | 40 s | +25 / 0 | loss 3 s; restore 18 s |
| DIVE85_HOLD_90S | 90 s | 0 / −85 | the mouse's own elevation limit |
| DESC8_LAT0p5L | 20 s | −0.5 / −8 | — |
| DESC8_LAT2L_40S | 40 s | −2 / −8 | — |
| TURN15R_40S_BASE | 40 s | +15 / 0 | — |
| TURN15R_40S_NOYAW | 40 s | +15 / 0 | `fineAimYawPerDeg` = 0 (probe-only config change) |
| TURN15R_40S_K0.3 | 40 s | +15 / 0 | `flightPathRateGainPerSec` = 0.3 (probe-only) |
| TURN15R_40S_TAU2 | 40 s | +15 / 0 | `bankTimeConstantSeconds` = 2 (probe-only) |
| TURN5R_30S | 30 s | +5 / 0 | — |
| TURN5UP_30S | 30 s | 0 / +5 | — |

The probe-only config changes are applied to the probe's rig instance in memory and never to any committed default.

## CSV columns

| Column | Meaning |
|---|---|
| `t` | Time, s |
| `tas` | True airspeed, m/s |
| `alpha`, `beta` | Angle of attack and sideslip, deg |
| `bank` | Bank, deg, + right wing down |
| `pathEl` | Flight-path elevation, deg |
| `dAz` | Path azimuth − aim azimuth, deg. Negative means the path is left of the aim |
| `dEl` | Path elevation − aim elevation, deg. Negative means the path is below the aim |
| `err` | Total angle between path and aim, deg |
| `nz` | Load factor |
| `p`, `q`, `r` | Aero-body rates, deg/s |
| `cmdP`, `cmdR`, `cmdY` | Source command |
| `signal` | Source `HasCommandSignal` |
| `loads` | Loads applied since the run start |
| `withheld` | Loads withheld by readiness since the run start |
| `aeroRefused`, `thrustRefused` | 1 when the research model refused that sample |
| `owner` | Physics owner |
| `mode` | Guidance mode |
| `desBank` | Guidance desired bank, deg |
| `limiters` | Guidance limiter bitmask (`MavAimGuidanceLimiter`) |
| `altM` | Altitude, m |

In the summary files, the `aero refused ... (first at t ...: <status>)` text prints the aero model's **final** status string, not the status at the first refusal. Use the CSV `aeroRefused` column and `tas` for the refusal time and cause.
