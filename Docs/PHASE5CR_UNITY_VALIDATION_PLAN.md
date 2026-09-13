# Phase 5C-R Unity Validation Plan

**Status: PREPARED, NOT RUN.** Nothing in this document has been executed. Everything validated so far
is offline against production code; these ten cases require Unity's own physics integrator and a play
session, and no offline harness can stand in for them.

Branch `sol/phase5-wip`. Written 2026-09-12 (Phase 5B.7).

---

## Why these ten and not others

Every case below tests something the offline suites structurally cannot:

- **Unity's rotational integrator.** The principal-axis inertia representation is validated to
  ~4·10⁻⁷ rad/s² against both the full-matrix Euler solution and TP-1538's own scalar equations. What
  is *not* validated is whether PhysX reproduces that when it integrates. Cases 3–8 exist for this.
- **Accumulated behaviour over time.** Offline tests evaluate single frames or a pure integrator.
  Drift, energy leakage and slow divergence only appear over seconds of real stepping.
- **The interaction of real component execution order.** Offline the harness calls `FixedUpdate` by
  reflection in an order it chooses. Unity chooses its own.

## Rig

`MavF16ReferencePhysicsRoot` on a bare GameObject, **not** the gameplay prefab:

```
F16_ReferenceRoot              <- Rigidbody, MavFlightPhysicsOwnership, MavF16ReferencePhysicsRoot
    VisualModel (optional)     <- child, any visual offset lives here
```

Confirm before every run, from the component's own readout:

| Check | Expected |
|---|---|
| `configurationStatus` | `reference configuration applied at spawn: 9298.7 kg, datum OriginIsReferenceCg, centre of mass (0, 0, 0)` |
| `Rigidbody.mass` | **9298.65 kg** |
| `Rigidbody.centerOfMass` | **(0, 0, 0)** — by construction, not by measurement |
| `Rigidbody.inertiaTensor` | **(75673.6, 85576.5, 12850.5)** kg·m² |
| `Rigidbody.inertiaTensorRotation` | **1.0492°** about Unity X |
| `massPolicy` | `Reference` |
| `propulsionIntentionallyDisabled` | **true** |
| `MavFlightPhysicsOwnership.owner` | `Legacy` at spawn; **nothing auto-switches** |
| `allowReplacementActivation` | **false** unless the operator deliberately lifts it for the run |

**Fixed timestep:** run each case at `Time.fixedDeltaTime = 0.02` and repeat cases 3, 6 and 8 at
`0.005`. Offline `TIMESTEP-001` shows the *model* is timestep-insensitive to 0.006%; this checks Unity
is too.

## Channels to record, every case, every physics step

Record all of these. A case that records only what the tester expected to be interesting cannot later
answer a question nobody thought to ask.

| Group | Channels | Source |
|---|---|---|
| State | position, rotation, linear velocity, angular velocity, mass | Rigidbody |
| Flow | α, β | `MavAeroBody.debugAoADeg/debugAoSDeg`, `MavFlightDynamicsMath.ComputeAlphaRad/BetaRad` |
| Air | reference Mach, ρ, T, p, a, qbar | `MavAtmosphereModel.ReferenceMach` + `Sample` |
| Rates | p, q, r (deg/s, aero body axes) | `MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody` |
| Surfaces | requested vs actual elevator / aileron / rudder | `MavF16ControlActuator.command` / `.actual` |
| Coefficients | CX, CY, CZ, Cl, Cm, Cn | `MavSixDoFBody.debugCoefficients` |
| Loads | dimensional force and moment, Unity local and aero body | `MavSixDoFBody.debugUnityLocalForceN/TorqueNm` |
| Gravity | provider, `Rigidbody.useGravity`, effective gravity vector, flag corrections | `MavFlightPhysicsOwnership` |
| Acceleration | world acceleration, specific force, body-normal specific force, **Nz**, legacy HUD G | `MavForceAccountingDiagnostics` |
| Energy | ½V² + gh per unit mass, and its rate | `MavForceAccountingDiagnostics.mechanicalEnergyRateWPerKg` |
| Envelope | `lastEnvelopeStatus`, steps inside / outside, termination reason | `MavF16ReferencePhysicsRoot` |

Record **both** Nz and the legacy HUD G in every case. They differ by ~1 g upright by definition, and
seeing both is how a future reader knows which one a number is.

---

## The ten cases

### 1. Zero-input ballistic / free fall

Spawn at altitude, zero velocity, no control input, aerodynamics disabled.

| Expect | Value |
|---|---|
| world acceleration | `Physics.gravity`, (0, −9.81, 0) |
| specific force | ~0 |
| **Nz** | **~0** |
| legacy HUD G | ~−1 |
| gravity provider | exactly one |

**Fails if** acceleration is not 1 g down, or Nz is not ~0. This is the Unity-side confirmation of
offline `GRAVITY-001` and `GSEM-001`.

### 2. Unpowered glide

Spawn at 3000 m, 180 m/s level, aerodynamics enabled, propulsion disabled, no input.

| Expect | Value |
|---|---|
| Nz | settles near +1 while the flight path is shallow |
| energy rate | **negative throughout** — no thrust, so drag must remove energy |
| α | small and positive, stable |
| reference Mach | ~0.55 at spawn, rising as it descends and the speed of sound falls |

**Fails if** total mechanical energy ever rises, or α diverges.

### 3. Elevator step

Trimmed glide, then a step elevator input held 3 s.

Record the **sequence**: surface moves → q builds → α builds → Cz/Cm respond → Nz builds → flight path
bends. That ordering is the thing to look at; a model where Nz leads α is wrong regardless of the
numbers.

**Fails if** Nz precedes α, or the response is instantaneous (no transient), or the sign of the pitch
response contradicts `AERO-CONTROL-001` (positive elevator → nose down).

### 4. Aileron step

Trimmed glide, step aileron held 2 s.

| Expect | Value |
|---|---|
| p | builds then approaches a steady rate |
| Cl | opposes p as it builds (Clp damping, offline-verified negative at every α) |
| r, β | **non-zero** — roll-yaw coupling is real and the reference model has it |

**Fails if** β stays identically zero (the model is not coupling), or p grows without bound.

### 5. Rudder step

Trimmed glide, step rudder held 2 s. Expect β to build, Cn to oppose it, and a rolling moment to
appear through Cl_β. Sign convention: positive β should produce negative Cy, per offline
`AERO-SYM-003`.

### 6. Pitch doublet

Elevator +step 1 s, −step 1 s, release. Watch for overshoot, oscillation and settling. Repeat at
dt = 0.005 and compare peak α and peak Nz.

**Fails if** the two timesteps disagree by more than a few percent, or the response is divergent.

### 7. Lateral doublet

Aileron +step 1 s, −step 1 s, release. This is closest to Morelli's own published validation case — a
lateral/directional doublet at α = 10°, Mach 0.26, which his Figure 1 shows agreeing with the wind
tunnel to within 10%. Set up at that condition if practical; it is the only quantitative external
comparison available.

### 8. Free angular-rate decay

Establish p, q, r simultaneously, then release all inputs.

This is **the dynamic Ixz case in Unity**. Offline the principal-axis representation reproduces the
coupled Euler solution exactly; here we find out whether PhysX does. Record p, q, r and compare the
decay against `MavInertiaTensorMath.TryAngularAcceleration` evaluated on the same states.

**Fails if** the measured angular acceleration diverges from the analytical prediction beyond
integration error — that would mean Unity is not honouring `inertiaTensorRotation` the way the
representation assumes.

### 9. Energy-loss sanity

Long unpowered glide, several minutes. Total mechanical energy per unit mass must decrease
monotonically. Any sustained increase means a force is adding energy — and with propulsion disabled,
there is nothing entitled to.

### 10. Reference-envelope exit

Deliberately accelerate past Mach 0.6 (dive), or command α past 45°.

| Expect | Value |
|---|---|
| `lastEnvelopeStatus` | `OutOfReferenceRange` or `OutOfFlowAngleDomain` |
| `testTerminated` | **true** |
| `terminationReason` | names the bound that was exceeded |
| further steps | **no further violations counted; nothing oscillates** |

**Fails if** the test continues, or ownership flips back and forth. The isolated rig is deliberately
not a gameplay system: crossing the boundary ends the experiment rather than handing the aircraft back
and forth.

---

## What a passing run does and does not establish

**Would establish:** Unity's integrator honours the reference mass/inertia representation; the
replacement aerodynamics produce a physically ordered response; energy behaves; the envelope gate fails
closed in a live session.

**Would not establish:** anything about powered flight (propulsion is disabled and has no
authoritative data), anything above Mach 0.6, anything about the leading-edge-flap-retracted
configuration, or that the aircraft is *pleasant* to fly. Those are later phases or different questions.

**Honest expectation about coverage.** Reference Mach is higher than the legacy HUD suggested — 200 m/s
is already Mach 0.68 at the tropopause, and Mach 0.6 is only 177 m/s there. Cases 2, 9 and 10 should be
planned around that: a glide from high altitude will exit the envelope on speed before it runs out of
altitude.
