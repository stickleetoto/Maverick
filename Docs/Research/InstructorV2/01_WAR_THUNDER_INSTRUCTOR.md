# 01 — War Thunder Mouse Aim and the "Instructor": What Is Publicly Known

**Purpose:** separate what Gaijin has publicly confirmed about Mouse Aim and the Instructor from community observation and from inference, and decide what Maverick can learn and reproduce **independently**.

**Not claimed:** access to War Thunder's proprietary flight model or Instructor code. Neither is in the public DagorEngine release (§4). Maverick does not and must not reproduce War Thunder's implementation; the goal is comparable *accessibility* on top of Maverick's own physics.

Evidence classes: `PRIMARY_VERIFIED` (official Gaijin source loaded and read), `SECONDARY_VERIFIED` (third-party source loaded; community statements are verified *as statements*, not as facts), `ENGINEERING_INFERENCE`, `UNVERIFIED`. Sources were loaded by a research sub-agent of this session on 2026-11-17; keys resolve in `07_RESEARCH_SOURCES.md`.

---

## 1. Publicly confirmed behaviour (PRIMARY_VERIFIED)

| # | Statement | Source |
|---|---|---|
| W1 | The Instructor sits between **all** player inputs (mouse, keyboard, gamepad, joystick) and the aircraft, converting commands into aircraft control in real time; whether it is optional "depends on specific mode" (administrator reply) | `[WT-NEWS-INSTRUCTOR-2016]` |
| W2 | In mouse aim the player sets the **direction**; the Instructor works the controls (stated for helicopters: cyclic and pedals; collective stays manual). Simplified/realistic helicopter modes turn stick input into an intended rotation rate that the Instructor executes; only "full controls" is direct | `[WT-DEVBLOG-HELI-2018]` |
| W3 | No virtual instructor in Simulator air battles | `[WT-WIKI-SIM]` |
| W4 | Game strings name an Instructor that **cancels actions "that could lead to the loss of the airplane"**, a toggle between control modes, "Aiming" and "Mouse-joystick" headers | `[WT-WIKI-CDK]` |
| W5 | Instructor **G limits are per aircraft and per control mode**: F-16A Instructor max overload **11 G** with mouse/simplified controls (2023); F-104 Instructor "forced turn" limit raised to 7.5 G **with full fuel** (2024), implying a mass/fuel dependence | `[WT-CL-SONS-OF-ATTILA]`, `[WT-CL-DANCE-OF-DRAGONS]` |
| W6 | The Instructor has a **critical-AoA limiter** that the player can switch off in mouse aim / simplified ("Switch maneuvering and limitations mode", allows a "cobra") | `[WT-CL-HORNETS-STING]` |
| W7 | Separately, some aircraft in full controls have a modelled SAS "damper" mode with its own AoA/overload limiters (F-16A, MiG-29, J35/JA37/AJ37) — a different system from the Instructor | `[WT-CL-SONS-OF-ATTILA]` |
| W8 | Instructor and mouse-aim behaviour are **tuned per airframe** repeatedly (e.g. 2018 Fw 190/Ta 152, Beaufort/Blenheim/Mosquito; 2024 MiG-23 roll sway; 2026 MiG-35/MiG-29KR at high altitude and speed) | `[WT-CL-1.79]`, `[WT-CL-DANCE-OF-DRAGONS]`, `[WT-CL-2.59.0.11]` |
| W9 | **What the cursor controls:** since update 2.35.1.40 (10 Apr 2024) mouse aim matches the **aiming reticle** to the cursor; previously the aircraft's **longitudinal axis** was matched. Reason: on aircraft whose sight is offset vertically, rolling made the sight drift sideways | `[WT-CL-2.35.1.40]`, `[WT-ITS-FIXED-90]` |
| W10 | In VTOL hover the Instructor tries to hold altitude | `[WT-WIKI-VTOL]` |
| W11 | A 2015 Mouse Aim mechanic change was reverted after player feedback (no technical detail) | `[WT-CL-2015-REVERT]` |
| W12 | Staff comments (low weight): the Instructor limits pitch in some cases because of stall/spin risk at high AoA (helper); artificially weakening mouse aim would cause strong backlash (administrator) | `[WT-NEWS-INSTRUCTOR-RU-2016]` |

### 1.1 The Gaijin patent — the only first-party algorithm description

`[GAIJIN-PATENT-US8770979]` *System and method for simulated aircraft control through desired direction of flight*, Gaijin Entertainment; inventors K. Yudintsev, A. Polyakov; priority 2012-03-21, granted 2014-07-08. **PRIMARY_VERIFIED** (Google Patents page read). Google Patents displays the status as "Expired – Fee Related" (lapse dated 2026-08-10); recorded as displayed, **not a legal opinion**.

As described (paraphrased):
- **Main process (303A): look-ahead prediction, not PID.** Copy the current angular rate and orientation into predicted values; for a fixed number of steps, transform the desired direction into the *predicted* local frame, set aileron/rudder from its lateral component and elevator from its vertical component (each times a coefficient, outputs in [−1, 1]), integrate predicted angular rate and orientation forward. The controls chosen account for where the aircraft *will* be pointing.
- **Alternative (303B):** simulate candidate controls forward, score alignment by a dot product, double or halve per-axis multipliers depending on overshoot.
- **Behaviour:** banks toward the target; elevator follows once roll brings the target into the vertical plane; returns level when aligned. Low-speed/ground cases simply give the closest achievable controls; an unreachable target can stall the aircraft. **No AoA or G limiting is described** in the patent. One desired-direction marker; camera centred on it; world-absolute direction by default.
- The server simulates each user's aircraft (anti-cheat context).
- The word "instructor" does not appear.

---

## 2. Community-observed behaviour (SECONDARY_VERIFIED as statements only)

| # | Observation | Source |
|---|---|---|
| C1 | A 2018 paraphrase of the official (now unreachable) wiki article lists Instructor functions: keep the wing below critical AoA; trim to hold the path when controls are released; compensate torque/asymmetry; slow G build-up near the airframe limit; automatic gear/flaps; restrict control near the ground; optional engine management; in mouse aim, move surfaces to keep the nose on the cursor with partial override | `[STEAM-TANKFRIEND-2018]` (primary wiki page UNVERIFIED — HTTP 401/404) |
| C2 | The Instructor cannot be fully disabled in mouse aim; only sub-options (engine, flaps, gear, near-ground) | 2016 news comments |
| C3 | Instructor limits vary strongly by aircraft (e.g. a reported ~36 % instantaneous turn-rate difference with/without Instructor on one aircraft; 14 g realistic vs 9 g Instructor on another) | `[WT-FORUM-INCONSISTENT-LIMITS-2025]` |
| C4 | Mouse-aim turns can slip or skid (poor coordination), notably on some jets | `[WT-FORUM-COORDINATION-2024]` |
| C5 | With "maneuver mode" in mouse aim, jets are forced wings-level below ~790–800 km/h | `[WT-FORUM-WINGS-LEVEL-2025]` |
| C6 | Arcade jets can "hang" on the rudder at 400–500 km/h, then snap toward the cursor | `[WT-FORUM-ARCADE-RUDDER-2024]` |
| C7 | Players describe the circle as the mouse/desired direction and a separate marker as where the nose points | Steam threads; MouseFlight README |

None of C1–C7 was acknowledged by staff in the threads read.

---

## 3. Engineering inference

| # | Inference | Basis |
|---|---|---|
| I1 | The Instructor is a **direction-tracking controller wrapped by per-aircraft envelope limiters** (critical AoA, G with fuel/mass dependence, near-ground restriction, loss-of-aircraft veto), with a player toggle for the AoA limit. It is not one universal controller | W4–W8, W12, patent |
| I2 | Its limits are **gameplay values that can exceed real-aircraft limits** — the F-16A Instructor limit of 11 G is above the commonly published F-16 design limit of 9 g (itself not verified as a production FCS number in doc 02 §1.1) | W5 + doc 02 |
| I3 | The controlled quantity is a **body-fixed axis** (formerly the longitudinal axis, now the gun line). The flight path therefore lags the cursor by roughly α (and β); at high α the velocity vector sits well below the cursor | W9, patent marker description |
| I4 | The patent's look-ahead is a lightweight **predictive** method: it needs a model of the aircraft's angular response (rate/acceleration), i.e. it is closer to a short-horizon shooting/MPC idea than to a PID. Its documented form commands surfaces from the target's body-frame components — the same mapping family as MouseFlight — but evaluated in the predicted future frame, which suppresses overshoot | patent 303A/303B |
| I5 | Because War Thunder tunes the Instructor per airframe and per mode, its "feel" is partly the Instructor's tuning, not only the flight model. A physics-honest Maverick will feel different wherever War Thunder's Instructor limits differ from Maverick's modelled aircraft | W5, W8, C3 |
| I6 | War Thunder's "loss-of-aircraft veto" and "near-ground restriction" are **command-side** protections (they change what is requested), not added forces — compatible in principle with Maverick's ownership rules | W4, C1 |

What is **not** publicly established: any numeric general limit formula; whether the Instructor modifies forces or only commands (I6 is inference); low-speed cut-off behaviour ("turns off at low speed" — UNVERIFIED); ground-avoidance algorithm; per-axis gains; the 2015 test mechanic.

---

## 4. DagorEngine public source: what is and is not there

`[DAGOR-ENGINE-REPO]`, BSD-3-Clause, read via GitHub pages/API (latest commit seen 2026-11-07). **PRIMARY_VERIFIED** for contents read.

- **Included:** engine and tool sources under `prog/`; samples; generic game physics libraries, including `prog/gameLibs/gamePhys/common/polares.{h,cpp}` (a generic wing-polar CL/CD-vs-AoA library with post-stall and Mach-factor modes), `mass.{h,cpp}` (part masses, inertia, fuel), `kinematics.h`, ground-vehicle physics.
- **Interface stub only:** `daGame/netAutopilot.h` declares an abstract `NetAutopilot`; it includes `gamePhys/phys/autopilotState.h`, which is **not** in the repository (404); `physActor.h` has a `getAutopilot()` returning null by default.
- **Not found:** any aircraft flight model, mouse aim or Instructor (code search: no `flightModel`, `mouseAim`, `wingArea`; only a third-party "instructor" hit). Code search on very large repositories is not guaranteed exhaustive.
- Whether `Polares` is the code War Thunder ships is ENGINEERING_INFERENCE (naming and a propeller Mach mode suggest an aircraft origin), not confirmed.

**Conclusion:** War Thunder's Instructor and aircraft FM are **not public**. Nothing from DagorEngine is needed or proposed for Maverick.

---

## 5. Independent open implementations

### 5.1 MouseFlight (Brian Hernandez, Unity, MIT, v1.3, 2019) `[MOUSEFLIGHT]` — SECONDARY_VERIFIED (code read)

Maverick's legacy rig and instructor descend from it (`Docs/MOUSEFLIGHT_MIT_ATTRIBUTION.md`).
- Unparented rig; mouse delta rotates an aim transform about the camera's right/up axes; camera slerps toward the aim with `1 − exp(−λ dt)`; camera up switches from world up to the rig's own up when `|aim.y| > 0.9` (a near-zenith guard); free-look freezes the aim.
- Autopilot: body-frame target `local = normalize(InverseTransformPoint(target)) · sensitivity`; `yaw = clamp(local.x)`, `pitch = −clamp(local.y)`; roll blends `wingsLevelRoll = transform.right.y` and `aggressiveRoll = clamp(local.x)` by `InverseLerp(0, 10°, angleOff)`; output applied as **direct torque** on the Rigidbody.
- Author's own caveats: purely proportional, overshoot-prone, target directly behind not handled.
- No AoA, G, speed, stall or ground logic.

### 5.2 MouseAimFlight (ferram4/tetryds, KSP mod, 2016) `[KSP-MOUSEAIMFLIGHT]` — SECONDARY_VERIFIED (code read)

- Errors as angles; roll target is the aim point raised along world "up" by a weight that is large near the nose (wings-level) and shrinks for large errors (bank-to-turn); sideslip and pitch corrections; three PIDs with derivative on measured rate; gains scaled down with dynamic pressure; extra up-weighting near the ground.
- **Licence is inconsistent** (README says MIT; LICENSE says "All rights reserved" for most files). **Ideas only; do not copy.**

### 5.3 Others (low weight)

MouseAimFlightRedux (KSP, All Rights Reserved; claims torque/inertia-aware G/AoA limiting, README only); a 2026 game PR describing a "War Thunder-style aim" with G hold, AoA cap, rolling-G roll reduction, `tan(α)` rudder coordination and look-ahead ground avoidance (imitation design, licence unknown); a 2026 indie dev log (its War Thunder claims unsourced); World of Warplanes' documented mouse assist (level-off zone, roll acceleration, auto split-S height, low-altitude bank limit). No GDC talk or engineering paper on mouse-aim flight assist was found.

---

## 6. What Maverick can learn and reproduce independently

| Lesson | Evidence | Maverick translation (doc 06) | Physics-honest? |
|---|---|---|---|
| L1 The player sets a **direction**; an assistant turns it into control inputs | W1, W2, patent | `MavMouseAimPilotCommandSource` emits `MavPilotCommand` only | yes — command-side |
| L2 The aimed axis must be explicit; War Thunder moved from **nose axis to gun line** | W9 | nose-pointing default, flight-path option, both in telemetry (doc 03 §6) | yes |
| L3 Bank toward the target, pull once the target is in the lift plane, level when aligned | patent behaviour; MouseFlight | roll-then-pull weighting `w = cos² Δμ` (doc 03 §4.5) | yes |
| L4 Anticipate to avoid overshoot | patent 303A look-ahead | Maverick uses square-root bank capture and pursuit gain set from measured roll response instead (doc 03 §4.4, §5.1); a look-ahead is a P3 alternative | yes |
| L5 Envelope limits are **per aircraft and per mode**, and the player may disable the AoA limit | W5, W6 | limits come from the control law / rig domain caps through the semantics descriptor, never from a guidance constant; a "limits off" mode would only lift *guidance* caps, never the law's protections or the model's domain gate | yes, if limits stay in the law |
| L6 Loss-of-aircraft veto and near-ground restriction are command-side | W4, C1 (I6) | possible P2 guidance features (e.g. ground-proximity pull-up *intent*), labelled gameplay assist | yes, if command-side |
| L7 Unreachable targets simply give the closest achievable controls; the aircraft can stall | patent | proportional guidance with no integrator; shortfall shown in telemetry (doc 03 §5.4) | yes |

**What Maverick must not copy:**
- War Thunder's **limit values** (e.g. 11 G) — they are gameplay tuning for a different flight model, and some exceed real-aircraft limits (I2).
- Any **direct-torque** mouse flight (MouseFlight's `AddRelativeTorque`; Maverick's legacy jet) — forbidden by the ownership rules.
- The patent's exact process as a specification. The patent is recorded as prior art; Maverick's proposed method (lift-vector pursuit, doc 03 §4) is a different, classical formulation. Before adopting a look-ahead method modelled on 303A/303B, check the patent's legal status properly (the "expired" status above is only as displayed).
- KSP MouseAimFlight code (licence).

**Expected difference in feel (inference):** where War Thunder's Instructor is tuned to make an aircraft turn harder or more stably than its own FM would on direct controls (C3), a Maverick aircraft will feel weaker, because Maverick's limits are the modelled aircraft's. That is the intended consequence of the core vision, and the human test (doc 06 §8.4) should be briefed on it.
