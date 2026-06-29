# v0.18.5 Regression Audit & Safety Cleanup

## Checks Performed

- Re-scanned MAVERICK Fresh scripts for v0.18.4 references, duplicate component installation paths, TGP display state, CAS weapon spawning, Physical AI toggles, and Fresh input mappings.
- Reviewed the active control chain: `MavMouseFlightRig -> MavInstructorController -> MavMouseFlightJet -> Rigidbody`.
- Reviewed weapon inputs and launch path: Mouse0 gun, Space secondary, 1/2 secondary cycle, 3 missile select, 4 bomb select.
- Reviewed TGP inputs: T PIP, V focus, Escape exit focus, R lock, G designate.
- Reviewed Physical AI inputs and path: F11 toggles `MavPhysicalAIController.aiEnabled`, and AI writes mouse aim/throttle instead of rotating the aircraft transform directly.
- Reviewed existing legacy radar components and Fresh bootstrap behavior that can disable old Eagle/Maverick components.
- Ran Unity batchmode compile and `dotnet build Assembly-CSharp.csproj`.

## Bugs Fixed

- `MavWTFeelPolishController.ApplyPreset()` no longer forces `MavTargetingPodSystem.displayMode` to PictureInPicture. F5/F6/F7/F8 tuning presets now preserve the current TGP visibility state.
- `MavTargetingPodSystem.ClampDisplayMode()` now falls back to Off, not PIP, if fullscreen is found while fullscreen mode is not allowed.
- Non-ballistic gun fallback now uses GunMuzzle/fallback muzzle origin and forward direction instead of Camera viewport ray direction.
- Removed unused camera-direction gun helper methods from `MavCASWeaponSystem` to make the physical gun path unambiguous.
- `MavFreshInput` now maps `KeyCode.N` and `KeyCode.M` for Fresh-side sensor/debug hotkeys.
- `MavFreshBootstrap` now preserves radar-related components while disabling old systems, and can force legacy radar/HUD state off at startup through safe reflection fields.

## Systems Verified

- Compile: Unity script compilation succeeds with no errors; `dotnet build Assembly-CSharp.csproj` succeeds with no errors.
- Startup: TGP defaults to Off through `MavTargetingPodSystem` and `MavTGPStateManager`; F2 debug overlay fields default false; Physical AI controller defaults disabled.
- Control path: W/S debug fields are updated in `MavInstructorController`; mouse aim is read from `MavMouseFlightRig`; Instructor mirrors P/Y/R to `MavMouseFlightJet`; Jet applies thrust/torque to Rigidbody.
- Weapons: Mouse0 calls primary gun only; ballistic and non-ballistic gun direction now resolve through GunMuzzle/fallback muzzle forward; projectile inheritance remains configurable.
- Sensors: T/V/Escape/R/G remain handled by `MavTargetingPodSystem`; legacy radar components are no longer disabled by Fresh bootstrap when radar preservation is enabled.
- Physical AI: F11 toggles `aiEnabled`; `ApplyCommand()` writes to `rig.mouseAim` and throttle, not direct transform rotation.
- Mount safety: `MavAircraftMountValidator` uses null-safe reference resolution, warning counters, and no automatic mount creation unless explicitly enabled.
- HUD/debug: F2 remains opt-in; TGP PIP default rect is small; legacy radar HUD can be hidden by Fresh bootstrap startup safety.

## Remaining Known Issues

- This pass did not run interactive Play Mode input tests; Unity compile and static/runtime-path audit were performed.
- Existing Unity/package warnings remain, mostly `FindObjectOfType`/`FindObjectsOfType` obsolete warnings.
- Existing legacy radar keybind profile is still the older profile defaults; Fresh now preserves radar components and can keep them off by default, but exact Y/N/M semantics depend on the scene's assigned radar/keybind components.
- Existing working tree still contains unrelated scene/package/model/material changes that were not modified by this patch.

## Manual Play Mode Checklist

1. Start Play Mode and confirm no console compile errors.
2. Confirm Main Camera is active and follows the Fresh rig.
3. Confirm TGP is hidden on start.
4. Press T and confirm a small PIP appears.
5. Press T again and confirm PIP hides.
6. Press V and confirm TGP focus/fullscreen only appears intentionally.
7. Press Escape and confirm TGP focus exits.
8. Press F2 and confirm control debug overlay appears, then press F2 again to hide it.
9. Hold W and verify W debug state plus pitch response.
10. Hold S and verify S debug state plus pitch response.
11. Move mouse left/right and verify Instructor and Jet P/Y/R values respond.
12. Press F5/F6/F7/F8 and confirm presets do not force TGP PIP visible.
13. Fire Mouse0 gun at speed and confirm GunMuzzle/fallback muzzle direction is used.
14. Press Space and confirm selected secondary fires.
15. Press 1/2 to cycle secondary selection.
16. Press 3 and confirm missile selected.
17. Press 4 and confirm bomb selected.
18. Test TGP R lock and G designate.
19. Test radar controls for the active scene/keybind profile and confirm radar starts off or compact if Fresh bootstrap is active.
20. Press F11 and confirm Physical AI toggles on/off and still drives the mouseAim/Instructor path.
21. Check Console for clear Mount Validator warnings after model swaps.
