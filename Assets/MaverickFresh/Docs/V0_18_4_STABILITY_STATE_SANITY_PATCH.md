# v0.18.4 Stability & State Sanity Patch

## Current Bug Summary

- TGP display could appear at game start and cover too much of the screen.
- Control diagnosis was too thin for W/S, mouse aim, Instructor output, and Rigidbody response.
- Large heading changes could leave velocity, aircraft forward, and weapon forward vectors feeling disconnected.
- Gun shells inherited full aircraft speed implicitly and could still fall back toward camera-based aiming paths.
- Model swaps could silently break GunMuzzle, TGP, radar, camera, and weapon mount assumptions.

## TGP State Manager Behavior

- `MavTargetingPodSystem` now starts in `Off` by default.
- `forceOffOnStart`, `startHidden`, and `showPipByDefault` make startup state explicit.
- `T` toggles Picture-in-Picture.
- `V` intentionally enters or exits fullscreen/focus mode.
- `Escape` exits fullscreen/focus mode.
- `O` still cycles display modes, but fullscreen is only available while `allowFullscreenMode` is true.
- The pod camera is protected from accidentally taking over `Camera.main`; if Main Camera is assigned as the pod camera, a dedicated `MavTGP_Camera` is created instead.
- Optional `CanvasGroup` and `RawImage` references are hidden when TGP is Off.

## Control Debug Overlay Usage

- Press `F2` to toggle the embedded `MavFreshHud` control debug panel.
- `MavControlDebugOverlay` is also available as a standalone component for scenes without `MavFreshHud`.
- The overlay shows:
  - W/S, A/D, Q/E pressed state
  - mouse cursor offset
  - TGP focus state
  - keyboard pitch input
  - mouse pitch/yaw input
  - Instructor P/Y/R, throttle intent, bank target, bank hold, coordinated yaw, and state
  - Jet speed, vertical speed, AoA, AoS, G, local velocity, angular velocity
  - forward velocity angle/alignment
  - applied torque debug and Semi-Aero active state

## Semi-Aero Stabilizer Behavior And Tuning Fields

`MavMouseFlightJet` keeps the existing physical actuator path and adds a weak optional layer:

```text
useSemiAeroStabilizer
sideSlipDamping
sideSlipDampingHighAoS
aoaDragStrength
aosDragStrength
forwardAlignmentAssist
forwardAlignmentMaxTorque
angularRateDampingPitch
angularRateDampingYaw
angularRateDampingRoll
highAoADragStart
highAoDampingStart
```

The stabilizer calculates local velocity, AoA, AoS, vertical speed, forward velocity alignment, and forward velocity angle. It applies:

- side-slip damping opposite local X velocity
- mild drag when AoA or AoS is high
- weak forward/velocity alignment torque at flight speed
- angular rate damping for pitch, yaw, and roll

It does not replace the Instructor, mouse flight rig, CAS, TGP, Physical AI, or Rigidbody actuator.

## Projectile Velocity Inheritance Behavior

`MavCASWeaponSystem` now exposes:

```text
projectileInheritsAircraftVelocity = true
projectileVelocityInheritanceFactor = 0.75
muzzleSpawnForwardOffset = 1.5
ignoreAircraftCollisionsForProjectiles = true
projectileSelfCollisionIgnoreTime = 0.35
```

Gun shells spawn from `MavCASOrdnanceAssets.gunMuzzle` when assigned, use `GunMuzzle.forward`, and fall back to the aircraft forward vector. Camera rays remain useful for HUD/aiming and selected ordnance paths, but physical gun shell direction no longer defaults to the camera.

Projectile launch velocity is:

```text
muzzleForward * muzzleSpeed + aircraftVelocity * projectileVelocityInheritanceFactor
```

`MavCASBallisticProjectile` can temporarily ignore the owning aircraft root during early raycast travel.

## Mount Validator Behavior

`MavAircraftMountValidator` checks:

- missing GunMuzzle
- missing CameraFollowPoint
- missing TGP mount while TGP exists
- missing RadarOrigin while a radar-like component exists
- GunMuzzle and RadarOrigin forward direction
- missile rail direction
- GunMuzzle inside or too near aircraft colliders

It logs clear warnings and exposes `warningCount` and `lastWarning`. It does not move mounts by default. If `createMissingFallbackMounts` is enabled, it can create simple child fallback mounts for GunMuzzle, CameraFollowPoint, and RadarOrigin.

## Bootstrap Integration

`MavFreshBootstrap` now has `installStateSanityPatch`, `installTGPStateManager`, and `installMountValidator`. It reuses existing components when present and does not add duplicates. `MavCASStarterBootstrap` also wires `MavTGPStateManager` after TGP setup.

The standalone `MavControlDebugOverlay` is not auto-installed by default because `MavFreshHud` already owns the normal F2 panel.

## Manual Unity Test Checklist

1. Start Play Mode.
2. Confirm TGP is hidden on start.
3. Press T and confirm PIP appears.
4. Press T again and confirm PIP hides.
5. Press V if focus mode exists and confirm focus mode works.
6. Press F2 and confirm control debug overlay appears.
7. Hold W and verify debug input and aircraft pitch response.
8. Hold S and verify debug input and aircraft pitch response.
9. Move mouse left/right and verify instructor and jet response.
10. Perform a hard 180-degree turn and check AoA/AoS/velocity stability.
11. Fire gun at high speed and confirm bullets inherit aircraft velocity.
12. Check Console for Mount Validator warnings.
13. Confirm Radar Y/N/M still works.
14. Confirm CAS/TGP designation still works.
15. Confirm Physical AI F11 still works.

## Known Limitations

- The Semi-Aero Stabilizer is intentionally lightweight, not a full wing/aero-surface simulation.
- Mount validation uses practical name matching and collider bounds checks, not imported-model-specific metadata.
- `Physics.IgnoreCollision` is applied to projectile colliders, while projectile raycast travel uses an owner ignore window.
- Final feel still needs Play Mode testing because prefab mass, collider shape, and scene startup order affect the result.

## Recommended Next Work

- Tune Semi-Aero values from flight recorder samples after hard turns.
- Add optional one-click mount gizmo visualization.
- Add a TGP UI prefab path if the project moves away from OnGUI PIP rendering.
