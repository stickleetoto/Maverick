# F-15 R1 Implementation Status V0.1

Status: **IMPLEMENTED SKELETON — FAIL-CLOSED BY DESIGN**

Branch: `sol/f15-r1-profile`

Target remains exactly:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

## 1. Purpose

Start the full-scale F-15 implementation by reusing the aircraft-independent flight-dynamics
architecture already proven on the F-16, without importing F-16 aircraft data.

This patch intentionally implements only F15-R1 physical/profile plumbing. It does not create an
F-15 aerodynamic model, real F100 thrust model, or F-15 control law.

## 2. Reused from the F-16 path

Reused common architecture:

- `MavFlightDynamicsProfile`
- `MavMassProperties`
- `MavControlSurfaceActuatorBase`
- shared atmosphere / axes / load dimensionalization
- `MavSixDoFBody` as the only Rigidbody load boundary
- shared N-engine propulsion installation/runtime architecture

Not reused:

- Morelli F-16 aerodynamic equations or coefficients
- F-16 control-law gains
- F-16 control-surface limits
- F-16 engine power law
- F-16 thrust data
- F-16 geometry/mass values

## 3. Implemented files

### `MavF15ReferenceData.cs`

Carries exact target identity, engine count/variant, and exact-target full-scale dimensions already
frozen in the reference docs.

The exact NASA 836 coefficient-reference `S` and `cbar` are still not frozen. They are stored as
zero in the generated aerodynamic reference geometry so `MavFlightDynamicsProfile.IsValid` refuses
the profile.

The aerodynamic envelope is represented by an impossible/inverted interval so no flight state can
accidentally be reported as within a nonexistent source envelope.

Control travel is zero until exact-target hard-stop/sign authority is accepted.

### `MavF15MassReference.cs`

Implements the exact NASA/TM-2012-215978 Table 1 **"Baseline F-15B test airplane"** column at
8,000 lb fuel:

- weight: 37,426 lb
- mass equivalent: 16,976.14804 kg (DERIVED)
- CG: 26.34% MAC
- Ixx: 30,345 slug-ft^2
- Iyy: 198,687 slug-ft^2
- Izz: 223,214 slug-ft^2
- Ixz: -5,070 slug-ft^2

**Corrected.** Earlier revisions of this file listed the "Spike extended" column
(37,152 lb / 26.05% MAC / 27,953 / 190,777 / 213,957 / -460) under the baseline heading. See the
correction note in `F15_FULL_SCALE_TARGET_FREEZE_V0.1.md`. All three table-1 columns are now held
explicitly in `MavF15Table1MassStates`, and `MavF15MassReferenceValidation` (38 checks) pins the
mislabelled tuple to `QuietSpikeExtended`.

The source Ixz sign is preserved and mapped into the same Unity principal-inertia representation used
by the common FDM/F-16 path.

This is one frozen mass state only. No fuel-dependent interpolation is implied.

### `MavF15FlightDynamicsProfile.cs`

Creates the first F-15 `MavFlightDynamicsProfileProvider`.

It supplies:

- exact NASA 836 identity;
- exact frozen mass/CG/inertia state;
- ~~exact frozen span~~ — **corrected**: the physical 42.8-ft span is no longer placed in the coefficient reference geometry's span field, which now reads zero like `S` and `cbar` (`F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1.md` §5);
- declared two-engine installation identity.

It deliberately remains invalid because the exact-target coefficient reference set (`S`, `cbar`, reference `b`) is unavailable in the held primary sources.

### `MavF15ControlActuator.cs`

Copies the F-16 ownership pattern, not its numbers:

`control law -> requested surface state -> actuator -> aerodynamic model -> SixDoF`

No Rigidbody torque is applied.

Without a valid physical profile, the fallback preserves throttle plumbing but clamps every
aerodynamic control surface to zero rather than borrowing preproduction/F-15-family hard stops.

### `MavF15PropulsionSkeleton.cs`

Updated the old "engine variant unfrozen" state to the now-frozen target identity:

- two Pratt & Whitney F100-PW-100 engines;
- pre-Quiet-Spike NASA 836 target.

Numeric performance remains unavailable:

- no exact-target thrust deck;
- no exact-target spool/transient law;
- no fuel-flow map;
- no inlet-recovery map;
- no engine mount/thrust-line coordinates.

Accordingly the skeleton still produces zero dimensional thrust and cannot become live-ready.

## 4. Newly acquired sources and promotion boundary

### NASA TM-72861

`Precision Controllability of the F-15 Airplane`, Sisk & Matheny, May 1979.

Useful F-15 family/preproduction evidence includes:

- F-15 mechanical + CAS architecture;
- pitch CAS authority of +/-10 deg stabilator;
- yaw CAS authority of +/-5 deg rudder;
- differential stabilator gearing;
- ARI schedule structure;
- family geometry/control travel tables.

But the report explicitly concerns F-15 number 8, initially a preproduction control-system aircraft.
Those numeric limits/schedules are therefore not silently promoted to exact NASA 836 hard-stop or
control-law authority in R1.

### AFIT / McDonnell lineage

The acquired wing-rock studies expose useful family geometry/inertia/control material and directly
identify the McDonnell source lineage:

- MDC A4172
- DN-1180.01-238-458 Rev. D

Those sources improve the acquisition path but do not by themselves close the exact NASA 836
coefficient-reference or hard-stop equivalence questions.

## 5. Current readiness

| Layer | Status |
|---|---|
| exact target identity | READY |
| exact mass / CG / inertia reference state | READY |
| common SixDoF architecture | REUSED |
| two-engine architecture | REUSED |
| exact engine variant identity | READY |
| exact coefficient-reference S / cbar / b / moment reference | BLOCKED — audited UNAVAILABLE in held primary sources |
| exact aerodynamic coefficient model | BLOCKED |
| exact hard stops / sign convention | BLOCKED |
| exact actuator rates | BLOCKED |
| exact F100 thrust deck | BLOCKED |
| engine installation coordinates | BLOCKED |
| live F-15 flight | REFUSED |

## 6. Next implementation step

F15-R2 should start only after choosing one of two explicit paths:

1. recover exact NASA 836 baseline coefficient-reference geometry/database; or
2. create a separately tagged bounded research model using a non-target F-15 source, keeping it
   CROSS_VALIDATION / RESEARCH and never presenting it as the NASA 836 baseline.

Until then, R1 remaining invalid is a feature, not a bug.
