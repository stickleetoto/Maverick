# Maverick

**Maverick** is a Unity-based modern fighter jet simulation prototype focused on building and testing an AI pilot / unmanned fighter aircraft system.

The project started as a combat flight prototype, but the current direction is not simply “make a shooter.”
The main goal is to create a controllable flight physics testbed where aircraft behavior, instructor control, air combat decisions, and AI pilot evaluation can be developed step by step.

Current primary aircraft: **F-22 Raptor**
Secondary aircraft targets: **F-15EX, F-16, F/A-18, F-35**

---

## Project Goal

Maverick aims to become a lightweight experimental simulator for:

* Modern fighter flight physics
* War Thunder-like arcade mouse flight feel
* AI pilot behavior testing
* Aircraft control and energy management experiments
* Combat decision logging and evaluation
* Future unmanned fighter AI research

This is currently a prototype, not a finished game.

---

## Current Focus

The current development focus is:

```txt
Flight physics first.
Weapons, gun polish, hit markers, kill feed, and arcade combat feedback are currently frozen.
```

The project is now focused on making the aircraft feel better to fly before expanding the combat systems.

---

## Core Systems

### Flight Physics

Implemented / in progress:

* Rigidbody-based jet flight
* Mouse flight control
* War Thunder-like instructor control direction
* Aircraft runtime profiles
* F-22 primary flight setup
* Atmospheric engine model
* Afterburner behavior
* Lift / drag / stall approximation
* Combat flap system
* Landing gear system
* Thrust vectoring support for F-22
* Golden trace flight data recorder for physics regression testing

---

### Aircraft System

The project uses a generic player aircraft root:

```txt
Mav_Player
├── Rigidbody
├── Flight / instructor scripts
├── Aero / engine scripts
├── Aircraft profile scripts
└── AircraftVisuals
    ├── F22
    ├── F15EX
    ├── F16
    ├── F18
    └── F35
```

`Mav_Player` is the actual controllable physics object.
Aircraft visuals are swapped or selected separately.

---

### Current Physics Direction

The project is moving toward a cleaner physics architecture inspired by:

* JSBSim-style separation of flight dynamics responsibilities
* Single-body Unity aero model
* War Thunder-like mouse instructor control
* Arcade feel through the instructor layer, not fake forces in the physics layer

Important rule:

```txt
Do not simply add more forces on top of the aircraft.
When replacing fake assists with real aerodynamic behavior,
migrate ownership gradually and preserve the original flight feel.
```

---

## Golden Trace Recorder

Maverick includes a Golden Trace Recorder for flight physics testing.

It records:

* Speed
* Mach
* Altitude
* G-force
* AoA / AoS
* Pitch / yaw / roll rate
* Turn rate
* Energy change
* AeroBody debug values
* Engine debug values
* TVC state

This is used to compare flight behavior before and after physics patches.

Example test cases:

```txt
1. Afterburner straight acceleration
2. 300-knot level turn
3. Dive acceleration
4. High AoA pull
5. Low-speed turn
```

The purpose is to avoid changing flight feel blindly.

---

## Controls

Basic controls may change during development.

```txt
Mouse       - Aim / flight direction
W / S       - Pitch input or throttle depending on current setup
A / D       - Roll / yaw depending on current setup
F           - Combat flaps
G           - Landing gear
T           - Radar / target lock system
X           - Target cycle
Space       - Missile fire
F10         - Start / stop golden trace recording
F11         - Add golden trace marker
```

---

## Development Rules

This project uses strict patching rules to avoid breaking scenes and prefabs.

Current rules:

```txt
- Prefer Scripts-only patches
- Do not overwrite scenes
- Do not overwrite prefabs
- Do not overwrite models
- Do not overwrite materials
- Do not modify weapons unless explicitly needed
- Flight physics changes must be isolated and reversible
```

---

## Current Roadmap

### Phase 1 — Physics Baseline

* Record golden traces
* Identify current flight feel baseline
* Measure speed, turn rate, AoA, G, and energy loss

### Phase 2 — Aero Ownership Migration

* Move fake stabilization into real aerodynamic behavior
* Reduce old fake assists when new aero effects are enabled
* Avoid double-applying damping or pitch recovery

### Phase 3 — Mouse Instructor v2

* Convert mouse input into target direction / target G / target AoA
* Add smoother War Thunder-like instructor control
* Keep arcade feel in the instructor layer

### Phase 4 — F-22 Speed & Energy Pass

* Improve F-22 speed feel
* Tune thrust, drag, afterburner, and energy loss
* Make high-speed and low-speed behavior feel distinct

### Phase 5 — AI Pilot Evaluation

* Add blackbox-style AI pilot logging
* Evaluate missile evasion, pursuit, energy state, and decision quality
* Use the simulator as an AI pilot testbed

---

## Status

Maverick is under active prototype development.

The current priority is not content expansion, but improving the foundation:

```txt
Better flight physics.
Better mouse flight feel.
Better test data.
Better AI pilot evaluation.
```

---

## Asset Credits

Some aircraft and visual assets may come from external sources.
Please check `MAVERICK_ASSET_CREDITS.md` for attribution and license information.

---

## Developer

Created by **Lee Jaeyoon** as part of an experimental AI / simulation development project.
