# Maverick F-16 Trim, Control Law and Readiness — Phase 2C/2D v0.1

Status: IMPLEMENTED, NOT LIVE
Branch: `claude/f16-trim-control-phase2`
PR base: `feature/f16-propulsion-phase2`
Target Unity: 6000.3.16f1

`MavSixDoFBody.simulationEnabled` remains `false` by default, and the F-16 auto-setup explicitly
holds it off. Nothing in this phase gives the new flight-dynamics path ownership of an aircraft.

## 1. What this phase adds

| Concern | Type | Location |
| --- | --- | --- |
| Trim problem definition | `MavTrimCondition`, `MavTrimPlant`, `MavTrimResult`, `MavTrimResidual`, `MavTrimSolverSettings` | `Core/MavTrimTypes.cs` |
| Deterministic solver | `MavDampedNewtonSolver`, `MavSteadyFlightTrimSolver` | `Core/MavTrimSolver.cs` |
| F-16 trim | `MavF16TrimReference` | `F16/MavF16TrimReference.cs` |
| Control law v0.1 | `MavF16ControlLawV01` | `F16/MavF16ControlLawV01.cs` |
| Protection maths | `MavControlLawProtections` and the three limiter settings structs | `Core/MavControlLawProtections.cs` |
| Command source contract | `MavPilotCommandSourceBase`, `MavManualPilotCommandSource` | `Core/MavPilotCommandSource.cs` |
| Readiness separation | `MavFlightDynamicsReadiness*` | `Core/MavFlightDynamicsReadiness.cs` |
| Validation | `MavFlightDynamicsPhase2Validation`, `MavFlightDynamicsOwnershipScan` | `Validation/` |

## 2. Trim infrastructure

### 2.1 Physical formulation

Steady symmetric flight in conventional aircraft body axes (X forward, Y right, Z down), wings
level, zero sideslip, zero body rates, constant true airspeed and constant flight-path angle.
Pitch attitude is `theta = alpha + gamma`, so gravity resolves into body axes as
`(-m*g*sin(theta), 0, +m*g*cos(theta))`:

```text
axial  : qbar*S*CX + T - m*g*sin(theta) = 0
normal : qbar*S*CZ     + m*g*cos(theta) = 0
pitch  : qbar*S*cbar*Cm                 = 0
```

Dynamic pressure is applied exactly once, inside `MavFlightDynamicsMath.Dimensionalize`. Nothing in
the solver multiplies by `qbar` a second time, and validation `[T0]` pins that by comparing the
pitch residual against an independently computed `qbar*S*cbar*Cm`.

### 2.2 Two modes, because thrust is not free

| Mode | Imposed | Unknowns | Residuals driven |
| --- | --- | --- | --- |
| `PoweredSteadyFlight` | flight-path angle | alpha, elevator | normal, pitch |
| `UnpoweredGlide` | thrust = 0 | alpha, elevator, gamma | axial, normal, pitch |

In powered mode the axial equation *defines* the required thrust rather than constraining the
solution, so alpha and elevator come from a 2x2 solve and the thrust follows. Whether the propulsion
model can supply that thrust is then a separate question, answered separately.

### 2.3 The solver

`MavDampedNewtonSolver` is deliberately boring: central-difference Jacobian with fixed steps
(one-sided at a bound), Gaussian elimination with partial pivoting, step-halving line search that
accepts only a strict decrease in the max-abs residual norm, hard box bounds on every unknown. No
randomness, no wall-clock input, no adaptive heuristic. Identical inputs produce identical iterates,
which validation asserts directly.

Residuals are normalized before being driven — forces by `m*g`, the moment by `qbar*S*cbar` — because
a Newton iteration on mixed N and N·m quantities is otherwise dominated by whichever channel happens
to have the larger magnitude. The default tolerance is `1e-5` normalized, which is well under a
Newton on a 9-tonne airframe and deliberately not tighter: the residuals are differences of
~1e5-magnitude quantities in `float`, whose cancellation noise floor sits near `1e-7` normalized.

**The solver never touches a Rigidbody.** It cannot: `MavTrimPlant` holds frozen numbers and two pure
functions, and there is no `Rigidbody`, `Transform` or component reference anywhere in the call
graph. The F-16 propulsion delegate is a pure steady-state function, so a trim solve cannot advance
a live engine's spool state either.

### 2.5 The propulsion contract is checked where the answer is built

v0.1 models thrust as acting along body X through the centre of gravity. A model reporting an off-axis
force or a thrust-line moment is refused with `MavTrimStatus.UnsupportedCondition` rather than silently
approximated.

That contract is verified at **every point the solver actually uses**: a cheap precheck at the initial
guess, a re-check at the solved attitude, and every throttle sampled while resolving the setting. A
single precheck is not enough, because a model whose thrust vector or moment depends on state can
satisfy a probe at one attitude and violate the assumption at the one the answer is built from — which
would produce a quietly wrong trim instead of a refusal. `[T4]` pins this with a plant that is clean at
the initial guess and violating at the solution.

An unpowered glide additionally requires that the engine can actually be commanded to produce nothing.
A model with non-zero idle thrust cannot reach the condition a `T = 0` solution describes, so that case
is refused too.

### 2.4 Reported convergence, error and residuals

`MavTrimResult` carries the status, the converged flag, dimensional and normalized residuals per
channel, the driven norm, the iteration count, whether alpha or elevator was pinned at a bound, the
required and available thrust, and a formatted report. Every non-converged outcome is a distinct
enum value — `MaxIterationsExceeded`, `Stalled`, `SingularJacobian`, `NonFiniteResidual`,
`UnsupportedCondition`, `InvalidPlant` — and none of them is mapped onto `Converged`.

## 3. F-16 trim: the honest result

`MavF16TrimReference` builds the plant from data already frozen in this branch: Morelli geometry and
validity envelope, the NASA nominal mass, the compact Morelli polynomial evaluated at zero body
rates, and the Garza/Morelli engine power state — which currently produces exactly **0 N**.

**A powered straight-and-level F-16 trim is therefore not achievable on this branch.** The solver
reports `ConvergedButThrustUnavailable`, with `converged == false`, `throttleDetermined == false`,
`throttle01 == NaN`, and the required thrust quantified against the 0 N available. No throttle is
invented and no thrust map is fabricated.

The unpowered glide trim *is* well-posed at zero thrust, and converges on the frozen aerodynamic
data. It is provided so the trim infrastructure is demonstrably exercised against real reference
aerodynamics rather than only against a synthetic test plant.

Survey produced by `Maverick > Flight Dynamics > Report F-16 Trim Survey`:

| alt (m) | TAS (m/s) | level alpha | level elev | required thrust | glide gamma | glide L/D |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | 130 | 3.065° | -4.648° | 6 864 N | -4.315° | 13.3 |
| 0 | 150 | 1.830° | -4.015° | 7 774 N | -4.886° | 11.7 |
| 0 | 180 | 0.732° | -3.426° | 10 718 N | -6.751° | 8.4 |
| 0 | 210 | 0.090° | -3.067° | 14 998 N | -9.483° | 6.0 |
| 3000 | 150 | 3.126° | -4.679° | 6 851 N | -4.307° | 13.3 |
| 6000 | 150 | 5.106° | -5.639° | 7 322 N | -4.615° | 12.4 |

Trends are physically coherent: trim alpha rises with altitude and falls with speed, required thrust
shows a minimum near the minimum-drag speed at each altitude, and the level and glide solutions agree
on alpha and elevator to three decimal places. Imposing the solved glide angle back as a *powered*
condition converges needing 0.00 N, which cross-checks the two modes against each other.

Caveat, stated rather than buried: an L/D near 13 is optimistic for a real F-16. The compact Morelli
`CX` fit is a clean-airframe, low-alpha, subsonic fit and carries no store or excrescence drag. These
numbers are a self-consistency result for *this model*, not a claim about the aircraft. Comparing
them against AeroBenchVVPython as an external oracle remains a later task.

## 4. Maverick F-16 Control Law v0.1

**This is not the F-16 FLCS.** It is not derived from, validated against, or intended to reproduce
the real flight control system. Every gain, limit, schedule and time constant is Maverick tuning
chosen for handling. The F-16 name identifies which airframe the tuning targets, nothing more.

```text
pitch stick -> commanded Nz (or pitch rate) -> protected pitch-rate demand
               -> pitch-rate error -> ELEVATOR degrees
roll stick  -> commanded roll rate -> protected roll-rate demand
               -> roll-rate error -> AILERON degrees
pedal       -> commanded sideslip + turn coordination + washed-out yaw damper -> RUDDER degrees
throttle    -> passed straight through
```

What it does not do: no `Rigidbody` access of any kind, no aerodynamic force or moment calculation
(`qbar` appears only as a gain schedule, never as a load), no velocity-vector alignment, no attitude
recovery assist, no fake damping, no leading-edge-flap command.

The distinction on damping matters and is enforced structurally: the yaw damper and both rate loops
command *physical surfaces*, and the resulting moment comes from the aerodynamic model like any other.
Nothing writes a damping torque. `[O0]` scans the sources to prove it.

### 4.1 Load-factor command

Neutral stick commands 1 g, full aft commands the configured maximum. The feed-forward conversion
uses the level-flight relation `n ~= 1 + V*q/g`, so `q = g*(n-1)/V`, and a closed-loop term corrects
toward the target from the *measured* load factor. Both terms scale with `g/V`, so the load-factor
response is the same at every airspeed rather than becoming violent at low speed.

The measured load factor is new in this phase. `MavSixDoFBody` publishes the summed load set divided
by mass as `MavFlightState.specificForceAeroBodyG` — a genuine accelerometer channel, gravity-free by
construction because gravity is applied by the Rigidbody and never enters the load set. That is what
lets the control law close a load-factor loop without computing an aerodynamic force itself, which
its own contract forbids.

`specificForceValid` distinguishes "no reading" from "a genuine 0 g reading". A protection that
mistook one for the other would silently disarm itself.

### 4.2 Yaw sign convention

Right pedal commands a nose-right yaw. Yawing the nose right moves it to the right of the velocity
vector, so the body-Y velocity component — and therefore `beta = asin(v/V)` — becomes **negative**.
The commanded sideslip therefore carries a leading minus sign. Validation `[C1]` pins this in both
directions and through the aerodynamic model.

The yaw damper washes out the low-frequency yaw rate before opposing it, so it does not fight the
steady yaw rate of a coordinated turn. Validation drives a sustained rate for 20 s and asserts the
damper input decays below 5 % of its initial value, then applies a fresh disturbance and asserts it
still gets through.

## 5. Protections

All values are Maverick tuning. The +9 / -3 g figures are the commonly published F-16 airframe design
load factors, which are *not* part of the frozen NASA reference set in this repository and are
therefore treated as tuning. The 25° alpha limit is a handling choice; the aerodynamic model's
published -10°..+45° validity range is a *modelling* range and is deliberately not reused as a
control limit.

| Protection | Mechanism |
| --- | --- |
| Load factor | open-loop rate ceiling `g*(nLimit-1)/V`, refined by a measured-margin ceiling `(nLimit-nz)*g/V` |
| Angle of attack | remaining alpha margin converted to remaining nose-up rate authority |
| Roll rate | soft-saturated rate limit, faded with alpha through a smoothstep |

Beyond a limit the ceiling goes negative, so the law commands a *recovery* rate rather than merely
removing authority. Nose-down commands are never blocked at high alpha — that would trap the aircraft
above its own limit — and validation asserts it.

Precedence is explicit: the negative-side floor is applied first and the nose-up ceiling last, so when
protections disagree the ceiling wins. That is the safe direction, because the alpha and positive-g
limits are the ones protecting the airframe.

### 5.1 Smooth limiting

`SoftSaturate` is exactly the identity below the knee, eases with a slope falling from 1 to 0 across
the band, and holds the limit beyond. `SmoothMin`/`SmoothMax` use a compact-support quadratic blend:
they return *exactly* `min`/`max` whenever the arguments differ by at least the band.

That exactness is not cosmetic. The common `sqrt`-form smooth minimum shifts its result slightly even
when neither argument is near binding, so chaining four protections leaves a small permanent offset —
a control law with a hidden trim bias at neutral stick. The first run of this suite caught exactly
that: a 0.02° elevator bias in trimmed 1 g flight with the stick centred. The compact-support form
removes it, and `[C0]`/`[L3]` now assert both the absence of bias and the exactness property.

Both smoothed operators are conservative by construction: a smoothed protection can only ever be
tighter than the hard one, never looser.

## 6. Operational readiness separation

Phase 1 conflated two different questions. They are now separate:

```text
NOT_PREPARED
STRUCTURALLY_PREPARED     "are the parts present and wired?"
OPERATIONALLY_LIVE_READY  "would handing this the aircraft actually be correct?"
```

Structural preparation is the Phase 1 rule, unchanged: Rigidbody, valid profile, aerodynamic model,
actuator, control law, propulsion model. `MavSixDoFBody.EvaluateReadiness(...)` still answers exactly
this, so existing callers and Phase 1 validation are unaffected.

Operational live-readiness additionally requires:

1. aerodynamic reference geometry matching the physical profile
2. a control law that is **enabled** and actually driving **this** actuator
3. an actuator that is **enabled** and bound to **this** six-DoF body
4. propulsion output that is authoritative, **or** non-authoritative and explicitly accepted by an
   operator (`acceptNonAuthoritativePropulsion`, default `false`)
5. a pilot-command source that reports itself fit for operational use
6. no legacy physics owner enabled on the same Rigidbody

Criterion 6 is detected by matching component type names against a serialized deny-list
(`MavAeroBody`, `MavAtmosphericEngine`, `MavMouseFlightJet`, `MavInstructorController`,
`MavThrustVectorControl`). Matching by name is deliberate: the new core must not take a compile
dependency on the stack it is replacing, and no legacy file needs to be edited.

Criterion 5 is why `MavPilotCommandSourceBase` exists now. A public inspector field on a control law
is not an answer to "is there a valid command source?". `MavManualPilotCommandSource` reports itself
operational only when an operator sets `treatAsOperationalSource` — a test rig must not satisfy a
safety gate by being added to a GameObject. This is the socket a real input path plugs into later; it
is **not** the War-Thunder mouse instructor, which is not implemented in this phase.

### 6.1 The gate has teeth

`requireOperationalReadinessForLoadApplication` defaults to `true`. Setting `simulationEnabled` while
not operationally live-ready no longer flies the aircraft: loads are refused, a counter increments,
and an error is logged once. `allowStructuralOnlyLoadApplication` is the explicit, warned override for
isolated bench testing.

**The current F-16 configuration is honestly reported as STRUCTURALLY_PREPARED, not live-ready** — it
has no frozen thrust deck and no operational command source.

### 6.2 Legacy-ownership detection is never answered from cache while armed

While load application is armed, the legacy-owner scan runs on **every** physics step. Caching is a
diagnostics optimisation only.

The reason is specific: a cached "no conflict" verdict is exactly the failure this gate exists to
prevent. If a legacy owner were enabled mid-flight, a stale answer would let both systems apply forces
to the same Rigidbody until the cache expired, and there is no acceptable length for that window.
`ShouldRescanLegacyOwnership(armed, countdown)` is pure and returns `true` for every countdown value
when armed, so no schedule can produce a stale safety answer. `[R2]` asserts that across the whole
countdown range rather than at a sample point.

`NotifyOwnershipChanged()` drops the cached verdict and forces the readiness judgement itself to be
rebuilt, so a future atomic ownership controller can invalidate readiness at the instant it changes
anything.

### 6.3 Command-source loss is fail-closed, and the inspector is bench-only

`IsOperationalCommandSource` and `HasCommandSignal` are deliberately separate:

- `IsOperationalCommandSource` is a **declaration** about the kind of path. It does not change from
  step to step.
- `HasCommandSignal` is **live state**: is a command actually arriving?

Operational live-readiness requires both, corroborated by what the control law actually observed on
its last poll (the law runs at -300 and the body at -100, so within one physics step that observation
is already fresh). Collapsing the two would let a nominally operational source stop producing while
readiness still reported the stack fit to fly.

On signal loss the control law applies the source's **declared** `MavCommandSignalLossPolicy` —
`NeutralCommand` (centre the axes, hold the last throttle) or `HoldLastCommand`. Neither is "keep
flying on the inspector field". Inspector input is a bench affordance: it applies when no source is
wired, or when a source that does *not* claim to be an operational path has nothing to say. Dropouts
are logged once and counted in `debugCommandSignalLossEvents`, so a broken input path is countable
rather than merely survivable.

`NeutralCommand` holds the last throttle rather than chopping to idle, because idling is a far larger
disturbance than centring the stick and this layer does not own thrust management. An engine-side
response to signal loss belongs to the propulsion path.

Consequence worth stating plainly: a sustained dropout removes live-readiness, which under the load
gate stops the new path from applying loads at all. The architecture's designed destination for a
readiness failure is a handover back to legacy, and the ownership controller that performs that
handover is a later phase. Nothing on this branch flies live, so no aircraft is affected today.

## 7. Validation

`Maverick > Flight Dynamics > Run Phase 2 Trim and Control Validation`, or
`Run All Flight Dynamics Validation` for every suite.

| Section | Covers |
| --- | --- |
| `[T0]` | residual arithmetic, gravity resolution, single `qbar` application |
| `[T1]` | convergence on a synthetic plant with a hand-checkable answer, determinism, glide |
| `[T2]` | non-convergent honesty: no thrust, no pitch authority, alpha outside the model envelope, unsupported and invalid requests |
| `[T3]` | F-16 trim on frozen data, powered and unpowered, with a cross-check between the two modes |
| `[T4]` | propulsion contract verified at the **solved** state, not only at a precheck; unreachable unpowered glide |
| `[C0]` | pitch / roll / yaw command direction all the way to Unity-local Rigidbody torque |
| `[C1]` | coordinated yaw, manual yaw bias sign, yaw-damper washout, aileron-rudder interconnect |
| `[L0]` | load-factor limiter, including monotonicity and recovery beyond the limit |
| `[L1]` | angle-of-attack limiter, including smoothness and that nose-down is never blocked |
| `[L2]` | roll-rate limiter and alpha authority fade |
| `[L3]` | soft-saturation and smooth min/max properties: bounded, monotone, continuous, conservative, unbiased |
| `[L4]` | integrator anti-windup and 500-step bounded-state run |
| `[R0]` | structural versus operational readiness, each criterion blocking individually |
| `[R2]` | legacy-ownership detection: no stale window while armed, and the deny-list rule |
| `[R3]` | command-source dropout: availability gates readiness, and loss never reaches inspector input |
| `[R1]` | measured specific force and load-factor sign convention |
| `[O0]` | source scan: no Rigidbody motion writes outside `MavSixDoFBody` |

A synthetic plant is used alongside the F-16 on purpose: it separates "the solver is correct" from
"the F-16 data is correct", so a failure points at one or the other rather than at both.

`[O0]` is a source scan rather than a runtime assertion because the rule is "no other component may
*ever* apply a force", and no runtime test can prove a negative about code it happens not to execute.
The scan strips line comments and string literals before matching, so the extensive documentation
that mentions `AddForce` is not a false positive, and it verifies its own classifier before trusting
its verdict on the tree.

Result at time of writing: **285 checks pass, 0 fail**, across all four suites, with 0 ownership
violations over 37 files.

## 8. Known limitations

1. **No attitude reference.** `MavFlightState` carries no bank angle, so the load-factor command uses
   the level-flight relation and under-commands pitch rate in a steep bank. Closing that gap needs an
   attitude source and belongs to a later phase.
2. **Trim is symmetric and wings-level only.** A banked or turning trim request is refused as
   `UnsupportedCondition`, not approximated.
3. **Thrust is modelled along body X through the CG.** A propulsion model reporting off-axis thrust or
   a thrust-line moment is refused rather than approximated, at the solved state as well as up front.
   The Garza/Morelli engine angular-momentum term is still not integrated.
4. **No powered F-16 trim** until the altitude/Mach thrust deck is frozen from an approved source.
5. **Control-law gains are unflown.** They are dimensionally reasoned and validated for direction,
   bounds, smoothness and stability of the stored state, but no closed-loop flight test or frequency
   response has been run. They will need tuning against real trajectories.
6. **One physics step of sensor latency** remains by design: the control law runs at execution order
   -300 and reads the state the body sampled at -100 in the previous step. Deterministic and
   documented, not accidental.
7. **L/D from the compact Morelli fit is optimistic** for a real airframe, as noted in section 3.

## 9. Next steps

1. Compare the trim points and step/doublet responses against AeroBenchVVPython as an external oracle
   only, in explicitly matched initial conditions.
2. Freeze the F-16 thrust deck from an approved source, then re-run the powered trim and expect
   `Converged`.
3. Record a golden trace before any ownership migration.
4. Add an attitude reference and extend the trim solver to steady banked turns.
5. Only then, build the War-Thunder-style instructor **above** the control law, and begin one-for-one
   ownership migration from the legacy stack.
