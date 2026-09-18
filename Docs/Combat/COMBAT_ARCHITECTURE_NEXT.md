# Combat Architecture — What Comes Next

The intended shape of Maverick's combat stack, and the order it gets built in. Written at the end of
Weapon Separation R0; **none of the phases below is implemented**.

## 1. Target structure

```
Assets/MaverickFresh/Scripts/Combat/
├── Core/          contracts shared by everything else        [R0: exists, 3 files]
├── Targeting/     track ownership, selection, lock lifecycle [TargetTrack Core]
├── Sensors/       radar, IRST, targeting pod as producers    [Radar Core R0]
├── Weapons/       launchers, munitions, guidance             [Missile Core]
├── Damage/        receivers, kill records                    [later]
├── Telemetry/     engagement recording                       [later]
└── Legacy/        adapters onto the old stack, then deleted  [as needed]
```

Directories are created when something goes in them. `Core/` exists because R0 put three contracts
there; the rest do not exist yet and should not be created empty.

## 2. Dependency direction

```
Player input            AI / FAM
     |                      |
     +----------+-----------+
                |
      IMavCombatCommandSource
                |
                v
        Combat command handling
                |
     +----------+-----------+
     |                      |
 Targeting               Weapon launcher
     |                      |
 IMavTargetTrackSource   Munition (seeker, guidance, autopilot, dynamics)
     |                      |
 MavTargetTrackData    IMavDamageReceiver
     ^                      |
     |                      v
  Sensors               Damage receivers
     |
     +-------> HUD / telemetry (read-only)
```

Invariants, enforced by `MavCombatBoundaryScan` where they can be:

| | Rule | Enforced |
|---|---|---|
| C-1 | `Combat/Core` must not reference flight-dynamics implementation types | scan |
| C-2 | `Combat/Core` must not write a Rigidbody | scan |
| C-3 | `Combat/Core` must not search the scene | scan |
| C-4 | `Combat/Core` must not read input | scan |
| C-5 | no radar/seeker/guidance/AIM-120/AIM-9 type may be declared under `Combat/` while the phase is separation-only | scan |
| — | HUD reads state, never owns it | review |
| — | aircraft physics has exactly one writer, and combat is not it | FDM writer scan |

C-5 is phase-scoped: it is a tripwire for *this* phase, and Radar Core / Missile Core will relax it
deliberately as each lands. Relaxing it is a decision, not an accident.

## 3. Phase sequence

### Weapon Separation R0 — done

Inventory, ownership audit, three contracts, damage-contract adoption, boundary scan. No behavior
change.

### TargetTrack Core — next

Make `MavTargetTrackData` real: something owns tracks, assigns stable ids, ages them, and drops them.
A legacy adapter presents today's `MavCASTarget` and `MavRadarSignature` scans as tracks, so HUD and
AI can move onto the contract before any real sensor exists.

Done when: no consumer calls `FindObjectsOfType` for targets; `MavFreshHud` reads tracks; there is
one answer to "what is the aircraft engaging" (closes O-4).

### Radar Core R0

A real sensor producing tracks: scan volume, range, aspect, detection, track initiation and drop.
Ownership only — no lock semantics yet. `MavF22SensorSuite` is prior art to read, **not** a base to
extend; it conflates scanning, locking, input and HUD.

Done when: tracks come from a sensor model rather than from scene truth, and a target outside the
scan volume is genuinely not known.

### Radar track / lock

STT, TWS, lock acquisition and loss, and what a lock means to a weapon. Absorbs the pod and sensor
lock authorities (closes O-4 fully).

### Missile Core

Launcher, munition lifecycle, seeker, guidance, autopilot, dynamics, telemetry — as separate pieces,
not one component. Launch takes a `MavTargetTrackData`, never a GameObject.

Done when: a munition can fly a guidance law against a track, and damage arrives through
`IMavDamageReceiver`.

### AMRAAM-style profile

A parameterised profile over Missile Core, sourced like the FDM reference data: numbers that are
public and documented, or explicitly marked provisional. The same provenance discipline applies —
unknown values stay disabled or declared, never invented.

### BVR vertical slice

Detect → track → lock → launch → guide → intercept, end to end, against an AI opponent, with
telemetry good enough to argue about.

### Legacy weapon deletion

Only now. Delete the dormant `Assets/Maverick/` combat line and whatever of the live CAS stack the
new architecture has replaced. Recoverable in git throughout; nothing is deleted while anything still
depends on it.

### IR seeker / AIM-9-style profile

After the radar path works end to end.

## 4. Things to carry forward, and things not to

Carry forward:

- The provenance discipline from the FDM work: sourced values, or explicitly provisional ones.
- Fail-closed defaults. The powered-shadow work is the model — a feature that refuses to arm when its
  preconditions are not met, loudly, rather than half-working.
- Checked architecture rules. A scan that fails is worth more than a document that asserts.

Do not carry forward:

- Arcade combat feedback — hit markers, kill feed, damage-direction indicators, arcade kill
  confirmation — as a **dependency** of the combat core. They may remain as legacy presentation while
  removing them would risk regressions, but nothing in `Combat/` may require them. Future feedback
  comes from sensor state, track state, lock state, seeker state, weapon status, aircraft damage
  state and telemetry.
- `FindObjectsOfType` as an interface.
- Any component that reads input and simulates in the same method.
