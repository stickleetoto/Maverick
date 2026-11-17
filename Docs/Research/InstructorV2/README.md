# Instructor v2 Research — Index

**Branch:** `claude/instructor-v2-research`, from `origin/main @ 71820a9` (documentation-only freeze commit; stable research baseline `f0f9519`).
**Development branch read, not modified:** `claude/realistic-mouse-instructor-v2 @ 5776617`. At the time of reading it held only the handoff document; this snapshot will go stale.
**Date:** 2026-11-17.
**Type:** research, technical evaluation and design proposal. No production code, scene, prefab, coefficient, propulsion data or tuning was changed.

## Documents

| # | Document | Content |
|---|---|---|
| 01 | [War Thunder Instructor](01_WAR_THUNDER_INSTRUCTOR.md) | What is confirmed, observed and inferred about Mouse Aim and the Instructor; Gaijin patent; DagorEngine contents; MouseFlight and others; what Maverick can reproduce |
| 02 | [Real fighter flight control](02_REAL_FIGHT_CONTROL.md) | TP-1538 simulated F-16 CAS; early F-15 CAS; handling-quality criteria; envelope protection; NDI/INDI; allocation; turn, coupling and energy physics; F-22 limits |
| 03 | [Guidance algorithms](03_GUIDANCE_ALGORITHMS.md) | PID, quaternion, rate, load-factor/C\*, scheduling, LQR, NDI, MPC, allocation compared; recommended lift-vector pursuit; scenarios A–G |
| 04 | [FDM engine comparison](04_FDM_ENGINE_COMPARISON.md) | Maverick vs JSBSim vs YASim vs others; Unity PhysX facts; licensing; hybrid options |
| 05 | [Maverick gap analysis](05_MAVERICK_GAP_ANALYSIS.md) | File-level audit: what to reuse, legacy instructor defects, control-law semantics, ownership risks, model envelopes |
| 06 | [Technical recommendation](06_TECHNICAL_RECOMMENDATION.md) | Architecture, modules, pseudocode, safety, validation (metrics M1–M13), priorities, ranked shortlist, notes for the development agent |
| 07 | [Research sources](07_RESEARCH_SOURCES.md) | Bibliography with evidence class, inspection status, claims supported, disagreements, unverified items |

## Most important findings

1. **The physics side needs no change.** Maverick's command → control law → actuator → aero/propulsion → load set → `MavSixDoFBody` pipeline is ordered, identity-checked and fail-closed. Instructor v2 only has to produce a `MavPilotCommand` (doc 05 §2).
2. **The legacy instructor cannot be bridged as is.** Its outputs were tuned as torque demands for the legacy direct-torque jet. It also carries a constant nose-down bias, aim-opposing recovery terms, a render-rate control loop, and no defined behaviour for a target behind the aircraft (doc 05 §3).
3. **The missing seam is stick semantics.** On F-15 V2, full stick means 7 °/s pitch rate, 15 °/s stability-axis roll and 1.5° sideslip. On the F-16 V0.1 law it means 9 g, 270 °/s body-axis roll and 8° sideslip. Guidance must emit *physical* intent and convert it through a per-law descriptor built from the laws' existing gains (doc 05 C1, doc 06 §2[C]).
4. **Public fighter control laws command physical variables.** Both the TP-1538 simulated F-16 and the early F-15 CAS use a load-factor/C\* pitch command, roll-rate command, stability-axis yaw damping and an ARI; protections act on commands. Enns et al. name velocity-vector roll and a C\*-type pitch variable as the controlled variables (doc 02). This is the interface the instructor should use.
5. **War Thunder's mouse aim controls a body-fixed axis, not the velocity vector.** It was the longitudinal axis until April 2024 and the gun reticle since; the Instructor's G/AoA limits are tuned per aircraft and per mode (F-16A 11 G), and the AoA limit can be toggled. Gaijin's patent describes a look-ahead controller with no limiter. Neither the FM nor the Instructor is in the public DagorEngine source (doc 01).
6. **The recommended guidance is lift-vector pursuit with roll-then-pull sequencing.** It needs no Euler angles, has no windup, stays continuous near zero error, holds a remembered turn direction near 180°, and has a principled gain rule (`K_θ τ_r ≈ 0.5`) tied to each law's measured roll response (doc 03 §4–5).
7. **The flyable F-15 research rig is a correctness testbed, not an agility demonstrator.** It has one trim at α 17.5° and 300.8 ft/s, fixed thrust and density, and no protections; 7 °/s of pitch rate is about 1.5 g at its trim speed. The 17.5° trim α makes the nose-vs-flight-path choice first-order (doc 05 C4–C5).
8. **No engine swap helps.** The instructor is engine-independent. Keep Maverick as runtime; use NASA DAVE-ML check cases (preferred) or JSBSim offline with Maverick's own model as cross-checks only. PhysX omits ω × Iω, and Maverick already compensates for it once; Unity 6000.3's `maxAngularVelocity` default is 50 rad/s (doc 04).

## Recommended architecture (summary)

```text
mouse delta (Update) → MavMouseAimInput (world aim orientation, free-look freeze, snapshot)
  → MavMouseAimPilotCommandSource.TryGetCommand  (inside the law's poll, cached once per physics step)
      → MavAimGuidance.Step        (pure: lift-vector pursuit → p_s,cmd, n_cmd, β_cmd)
      → IMavPilotCommandSemantics  (per law: physical intent → MavPilotCommand)
  → existing control law → actuator → aero/propulsion → LoadSet → MavSixDoFBody → Rigidbody
```

## Top five actionable improvements (all P0, no FDM change)

1. Pilot-command semantics descriptor per control law.
2. Lift-vector pursuit guidance as a pure, tested function.
3. Turn-direction memory and push/roll hysteresis for reversals and "below" targets.
4. Guidance computed in the law's poll and idempotent per physics step; input integrated from raw mouse deltas.
5. Instructor telemetry and a deterministic scenario suite with metrics M1–M13.

The full ranked table is in doc 06 §10.

## Open questions

| # | Question | Who decides / how |
|---|---|---|
| Q1 | Default target semantics: nose/gun line (proposed) or flight path? | user product decision; doc 06 §3 |
| Q2 | Turn side when the aim jumps directly behind with no history: pull in the current lift plane (proposed) or toward the lower-energy side? | user / tuning, visible in telemetry |
| Q3 | Which aircraft should carry the "feel" evaluation, given the F-15 rig's narrow envelope and the F-16's missing thrust deck? | user; needs new public data or an accepted limitation |
| Q4 | Should the F-16 law move to stability-axis roll and yaw damping (public TP-1538 and F-15 CAS evidence)? | separate control-law package with F-16 gates |
| Q5 | Does TP-1538 Appendix A / Figs. 62–65 close the source library's open F-16 FCS questions F16-Q-C1/C2? | source-library owner, after page transcription |
| Q6 | Is a "limits off" mode wanted, as War Thunder offers? If so, it may lift only guidance caps, never the law's protections or the model's domain gate | user |
| Q7 | Gaijin patent status before any look-ahead design inspired by it | legal check, if ever relevant |

## Evidence limitations

- Production FCS gains for the F-15, F-16 and F-22 are not public. Every Maverick law and every guidance value stays `MAVERICK_TUNED_NON_AUTHORITATIVE`.
- The official War Thunder Instructor wiki article was unreachable; its content is known only from a paraphrase.
- Several TP-1538 gains are readings from scanned figures (moderate confidence).
- External pages were read by research sub-agents. The lead relied on their structured reports plus two direct citation checks.
- No Unity run, compile or test was performed for this research; it changes no code.
