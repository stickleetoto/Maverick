# Maverick Flight Dynamics — Phase 3

Status: IN PROGRESS
Branch: `claude/f16-trim-control-phase2`
Target Unity: 6000.3.16f1

`MavSixDoFBody.simulationEnabled` remains `false` by default throughout Phase 3.

---

## Phase 3A — Propulsion

### The problem being solved

Phase 2 left the F-16 with sourced engine power-state dynamics and **no dimensional thrust at all**.
That was honest but structural: there was no place to put a thrust deck if one ever arrived, and no
way to test the powered path without inventing numbers.

Phase 3A adds the architecture. It does not add F-16 data.

### What was NOT done, deliberately

No F-16 thrust data was invented. No altitude/Mach tables were fabricated, nothing was interpolated
from unsourced figures, and no generic F110/F100 headline thrust was converted into a flight
envelope. **The shipped F-16 still produces exactly 0 N and still reports
`ConvergedButThrustUnavailable` for a powered trim.** `[A3]` pins that as a regression.

### Separation of concerns

```text
throttle -> [ sourced Garza/Morelli gearing + spool dynamics ] -> actual power %
actual power % + altitude + Mach -> [ thrust deck ] -> thrust N
thrust N -> [ propulsion model ] -> body-axis force / moment / reported thrust / authority
```

The split is the point. Maverick genuinely has sourced *power dynamics*; it does not have sourced
*thrust*. Keeping them in separate types means one can be true without implying the other.
`MavF16EnginePowerModel` keeps the sourced half and gains an optional `MavThrustDeckBase`; with no
deck attached, behaviour is byte-for-byte what it was.

### Provenance travels with the number

```csharp
enum MavThrustDataAuthority { Unavailable, SyntheticBench, Authoritative }
```

Every `MavThrustDeckResult` carries its own authority, so a value cannot be laundered by passing it
through a layer. `MavPropulsiveLoads.hasAuthoritativeData` and `MavTrimResult.propulsionDataAuthoritative`
carry it onward to telemetry, readiness and trim reports.

### Out-of-envelope policy

```csharp
enum MavEnvelopeExcursionPolicy {
    ClampToValidatedEnvelope,           // edge value, excursion reported, stays authoritative
    RejectUnsupportedState,             // no result at all
    MarkNonAuthoritativeExtrapolation   // genuinely extrapolates, authority DOWNGRADED
}
```

Two details worth stating:

- **Extrapolation really extrapolates.** The first implementation clamped and merely relabelled the
  result, which would have made the policy name a lie. It now uses an unclamped interpolation
  weight — and is bounded to one grid cell beyond the edge, because "mark it non-authoritative" is
  not a licence to return an arbitrary number.
- **Clamping stays authoritative.** The edge value *is* supported data. The excursion is reported
  separately via `insideEnvelope`.

Only the first two policies are acceptable for live flight, and
`MavPropulsionModelBase.IsAcceptableForLiveFlight` — which is what readiness now consults, rather
than `HasAuthoritativeData` — enforces it. An authoritative deck configured to extrapolate is
rejected for flight.

### Powered trim

`ConvergedButThrustUnavailable` can now become a real powered result, with a third outcome between
them:

| Status | Meaning |
| --- | --- |
| `Converged` | closed using **accepted** data, or needed no thrust at all |
| `ConvergedWithNonAuthoritativeThrust` | closed, thrust available, but the data is synthetic/extrapolated. Never a reference result |
| `ConvergedButThrustUnavailable` | the engine cannot supply the required thrust |
| `ThrustNotMonotonic` | thrust cannot be inverted for a throttle at this condition |

A condition needing essentially no thrust is never downgraded — it required no thrust data to begin
with, which is why the F-16 glide trim still reports plain `Converged`.

### Throttle inversion is no longer assumed to be well posed

The old code took idle and full thrust, checked the required value fell between them, and bisected.
Both halves of that were unsound for a general deck:

1. **Monotonicity was assumed.** A deck that rises, dips and rises again has different endpoints
   while a bisection on it converges happily on a throttle that does not produce the requested
   thrust.
2. **The endpoints were treated as the range.** For the same non-monotonic curve the achievable
   thrust range is *wider* than its endpoints, so an endpoint-derived availability test rejects
   conditions the engine can actually hold.

Now a single fixed 17-point sweep answers both, in the right order: monotonicity first, and only
once it is established are the endpoints treated as the range. A non-monotonic deck yields
`ThrustNotMonotonic` and **no throttle is invented**. `[A5]` pins this with a deliberately folded
thrust curve.

### The synthetic bench deck

`MavSyntheticBenchThrustDeck` exists so the architecture can be tested end to end. Its safety
properties are structural, not procedural:

- `Authority` is hard-coded to `SyntheticBench`. There is no inspector field that can raise it.
- It therefore can never satisfy propulsion acceptance for live flight.
- The F-16 auto-setup never attaches it; it must be added by hand.
- Its numbers are round and obviously artificial.

Its shape (thrust falling with density ratio, a mild Mach term, sourced power blend on top) exists
only so the magnitudes are realistic enough to exercise the solver. None of it is a claim about any
aircraft.

### Phase 3A validation

| Section | Covers |
| --- | --- |
| `[A0]` | authority and live-flight acceptance rules; unconfigured and malformed decks; the sourced power blend |
| `[A1]` | deterministic bounded interpolation, clamping, genuine bounded extrapolation, repeatability |
| `[A2]` | the three excursion policies and their effect on authority |
| `[A3]` | **regression: F-16 dimensional thrust is still unavailable** and the shipped trim results are unchanged |
| `[A4]` | powered trim through a deck, including that the solved throttle really produces the required thrust, and that accepted data yields a true `Converged` |
| `[A5]` | monotonicity guard and refusal to invent a throttle |

---

## Phase 3B — Attitude and flight state

### The problem being solved

The Phase 2 control law converted a commanded load factor into a pitch rate with

```text
q = g * (n - 1) / V
```

which is the wings-level special case. In a bank it under-commands pitch rate, so a banked turn
sags. `MavFlightState` carried no attitude at all, so the law had nothing better to use.

### What was added

`MavAttitude` on `MavFlightState`, carrying pitch attitude, bank angle, heading, flight-path angle,
and three honesty flags: `valid`, `nearVerticalSingularity` and `flightPathValid`.

### Coordinate discipline

This is the part most likely to be broken by a later "simplification", so it is stated plainly.

Maverick already carries a handedness hazard between Unity local axes (X right, Y up, Z forward) and
aerodynamic body axes (X forward, Y right, Z down). The change of basis has determinant -1, so true
vectors and axial vectors convert differently, and `MavFlightDynamicsMath` keeps those cases apart
on purpose.

**`MavAttitudeMath` does not participate in that conversion at all.** Every angle is defined
geometrically, from world-space direction vectors and world "up":

```text
theta = asin(forward.y)                nose above the horizon
psi   = atan2(forward.x, forward.z)    compass direction of the nose
phi   = atan2(-right.y, up.y)          how far the right wing has dropped
gamma = asin(velocity.y / |velocity|)  climb angle of the velocity vector
```

None of those depend on rotation handedness, Euler ordering, or a basis change. They are statements
about where vectors point, which makes them immune to the class of sign error this project has
already hit. They must not be replaced with a quaternion-to-Euler extraction later.

`[B0]` asserts **physical directions**, not round trips: nose above the horizon gives positive
theta, right wing down gives positive phi, nose right increases psi, climbing velocity gives
positive gamma. Round-trip tests are deliberately not relied on, because a matching pair of sign
errors cancels in a round trip and survives it untouched.

### Corrected load-factor relation

```text
q = g * (n - cos(phi)) / V
```

At `phi = 0` this reduces exactly to the Phase 2 expression, so wings-level behaviour is unchanged -
`[B1]` and `[B2]` both pin that as a regression. In a steady coordinated level turn `n = 1/cos(phi)`,
and substituting gives `q = (g/V)·sin²(phi)/cos(phi)`, the standard result.

### Turn compensation

Neutral stick now commands the load factor that holds altitude at the current bank, `n = 1/cos(phi)`,
instead of a flat 1 g. A 45° bank commands 1.41 g, a 60° bank commands 2 g, and the aircraft holds
its turn without the pilot pulling manually - the War-Thunder-like usability the design goal asks
for, produced entirely through physical surface commands.

It is bounded and it withdraws rather than escalating:

- clamped to `maxTurnCompensationG` (default 4 g, Maverick tuning)
- withdrawn past `minimumCosBankForTurnCompensation` (default cos 75°), because approaching
  knife-edge, holding altitude is no longer a matter of pulling harder
- withdrawn entirely when the attitude reference is unavailable, degrading to the Phase 2
  wings-level relation rather than acting on a meaningless bank angle

Every one of those is reported in `MavF16ControlLawDebug`, so a withdrawal is visible rather than
inferred from handling.

This remains the **Maverick F-16 Control Law**. Nothing here is derived from the real F-16 FLCS.

### Phase 3B validation

| Section | Covers |
| --- | --- |
| `[B0]` | attitude physical directions, degenerate basis, vertical singularity, low-speed flight path, safe degradation |
| `[B1]` | the corrected q relation, its exact reduction to Phase 2 at wings level, and bounded turn-compensation load factor |
| `[B2]` | wings level, moderate bank, steep bank, positive-g turn, unloaded, command direction, bank-aware g limiting, and loss of attitude |
