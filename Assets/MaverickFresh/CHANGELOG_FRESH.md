# MAVERICK Fresh Changelog

## v0.18.14 - Rate-Based Heavy Control + Anti-Wobble Patch
- Added optional rate-based heavy control in `MavMouseFlightJet` so pitch/yaw/roll commands become target local angular rates and the Rigidbody follows with clamped PD torque.
- Retuned F5/F6/F7 rate targets, P/D gains, torque clamps, angular damping, and direct manual assist behavior.
- Made manual W/S, A/D, and Q/E axes register immediately while the aircraft response remains heavy through angular-rate physics.
- Added stronger center deadzones, roll input hysteresis, roll-rate quieting, and AoS yaw-assist deadzone to reduce center left/right wobble.
- Expanded F2 debug with rate mode, target/current angular rate, rate error, rate torque, roll hysteresis, center quiet, manual axis state, and direct assist state.
- Documented the patch and Play Mode checklist in `Docs/V0_18_14_RATE_BASED_HEAVY_CONTROL.md`.

## v0.18.13 — Heavy Response Tuning
- Retuned F5 Balanced so input/received commands stay immediate while actuator command and angular velocity build more slowly.
- Lowered Balanced acceleration torque to `(20, 9, 26)` with clamp `(42, 18, 52)`.
- Lowered Smooth acceleration torque to `(16, 7, 21)` with clamp `(34, 14, 42)`.
- Lowered Aggressive acceleration torque to `(28, 12, 36)` with clamp `(58, 24, 72)`.
- Reduced actuator response and command rate limits across F5/F6/F7 while keeping `ForceMode.Acceleration`.
- Increased angular damping and lowered `maxAngularVelocity` per preset so the aircraft body feels heavier instead of twitchy.
- Kept direct manual torque assist off for F5/F6 and on only for F7.
- Documented the tuning targets and Play Mode checks in `Docs/V0_18_13_HEAVY_RESPONSE_TUNING.md`.

## v0.18.11 - Flight Control Pipeline Audit & Actuator Fix
- Audited the manual W/S and A/D path from `MavFreshInput` through `MavInstructorController`, `MavMouseFlightJet`, and `Rigidbody`.
- Made the Instructor-to-Jet command handoff explicit with `SetControlCommand(...)` and added received/actuated/final torque debug fields.
- Switched control torque defaults and F5/F6/F7 profile paths to `ForceMode.Acceleration` with mode-specific torque scales and clamps.
- Raised the aircraft rotation ceiling to `maxAngularVelocity >= 8` and removed freeze-rotation constraints from the aircraft Rigidbody while preserving position constraints.
- Reduced angular/aero damping during active manual input and enforced manual pitch/roll/yaw minimum authority through envelope protection.
- Added a tunable direct manual torque assist fallback for W/S, A/D, and Q/E diagnostics without replacing the main actuator.
- Updated F2 debug overlays to show manual input, raw/final Instructor output, Jet received command, actuated command, final torque, torque mode, and assist state.
- Documented the audit and Play Mode checklist in `Docs/V0_18_11_FLIGHT_CONTROL_PIPELINE_AUDIT.md`.

## v0.18.9 - Flight Envelope Protection / High AoA Slip Guard
- Added high-speed pitch authority limiting so full W at Mach 1+ remains effective but no longer overdrives pitch torque.
- Added AoA/AoS/side-slip soft guards that reduce pitch-up, yaw, and roll overcorrection without snapping attitude or locking controls.
- Added final control torque clamp and smoothing in `MavMouseFlightJet` with post-clamp debug torque.
- Faded velocity turn assist during extreme AoS/forward-velocity-angle/side-slip states and added damping drag instead.
- Added high-speed turn drag plus high-AoA/high-AoS envelope drag, clamped by `maxEnvelopeDragAccel`.
- Expanded F2 debug with high-speed pitch limiter, guard factor, final torque clamp state, velocity turn assist factor, envelope drag, and clamped control torque.
- Documented manual Play Mode checks and tuning guidance in `Docs/V0_18_9_FLIGHT_ENVELOPE_PROTECTION.md`.

## v0.18.8 - War-Thunder-like Throttle Axis System
- Added retained throttle axis state with `throttlePercent` from -5% cutoff/brake through 110% afterburner/WEP.
- Kept W/S as pitch control while moving throttle to Shift/Ctrl, mouse wheel steps, X idle/restore, and I engine toggle.
- Added engine-on state, idle thrust, negative cutoff/brake drag, afterburner thrust mapping, and throttle spool response.
- Preserved legacy `throttleIntent`/`throttle` compatibility so Physical AI can still drive throttle through Instructor or Jet.
- Updated normal HUD and F2 debug overlays to show throttle percent, engine state, effective throttle, afterburner, and cutoff state.
- Documented controls, thrust mapping, spool behavior, manual tests, and tuning guide.

## v0.18.6 - Flight Feel / Turn Response / Camera UX Hotfix
- Added velocity turn assist to the Semi-Aero Stabilizer so hard turns rotate the velocity vector toward aircraft forward without instantly snapping.
- Reduced centered roll/yaw oscillation with roll command deadzone/slew, bank-hold deadzone, tiny-AoS yaw assist filtering, and center roll damping.
- Retuned Balanced/F5, Smooth/F6, and Aggressive/F7 mouse authority presets with stronger pitch/yaw response and calmer bank hold.
- Strengthened W/S elevator override with `pitchUpCommand = -2.55`, `pitchDownCommand = 2.35`, `manualPitchBoost = 2.35`, and faster keyboard elevator response.
- Added camera lag, hard-maneuver lag, speed-based FOV, and steep-dive camera pullback/protection to `MavMouseFlightRig`.
- Split normal HUD from developer telemetry with `showDeveloperDebugOnHud = false`; detailed control diagnostics remain on F2.
- Documented the hotfix behavior, tuning guide, and manual Play Mode checklist.

## v0.18.5 - Regression Audit & Safety Cleanup
- Prevented F5/F6/F7/F8 feel presets from re-opening TGP PIP.
- Kept disallowed TGP fullscreen states clamped to Off instead of silently returning to PIP.
- Removed remaining camera-direction fallback from non-ballistic gun firing; gun now uses GunMuzzle/fallback muzzle forward.
- Added Fresh input mappings for N/M sensor hotkeys.
- Added radar preservation/startup-off safety in Fresh bootstrap so legacy radar is not disabled as an old component and HUD can stay hidden by default.
- Documented regression checks, fixed bugs, verified systems, and manual Play Mode checklist.

## v0.18.4 Stability & State Sanity Patch
- Forced TGP startup into Off with safe PIP/focus controls: T toggles PIP, V toggles intentional focus, and Escape exits focus.
- Added `MavTGPStateManager` for explicit TGP startup state and optional RawImage/CanvasGroup visibility sync.
- Expanded F2 control diagnostics and added standalone `MavControlDebugOverlay` for scenes without `MavFreshHud`.
- Added optional Semi-Aero Stabilizer fields to `MavMouseFlightJet` for side-slip damping, high AoA/AoS drag, forward-velocity alignment assist, and angular rate damping.
- Added projectile velocity inheritance controls and owner-collision safety for ballistic CAS projectiles.
- Added `MavAircraftMountValidator` to warn about missing, misaligned, or collider-overlapping aircraft mounts.
- Integrated TGP state sanity and mount validation into Fresh/CAS bootstraps without adding mission gameplay.

## v0.18.3 — Physics/Input/Mouse Feel Hotfix
- Strengthened W/S elevator override with `pitchUpCommand = -2.35`, `pitchDownCommand = 2.15`, `manualPitchBoost = 2.15`, and faster keyboard elevator response/release tuning.
- Added W/S pitch debug fields on `MavInstructorController` and an optional F2 control debug overlay in `MavFreshHud`.
- Increased balanced mouse authority to `pitchGain = 0.76`, `yawGain = 0.22`, `rollGain = 1.18`, and `maxAutoPitch = 0.62`.
- Tuned physical actuator response to `turnTorque = (125, 16, 150)`, `angularDamping = 1.35`, and `maxAngularVelocity = 4.8`, with optional acceleration torque mode left off by default.
- Updated F5/F6/F7 feel presets and camera follow strengths while preserving CAS, TGP, ordnance, radar, telemetry, and Physical AI routing.

## v0.18.1 — Mouse Camera Follow Hotfix
- Made mouse camera follow use viewport cursor offset directly.
- Removed double-smoothing inside `ApplyMouseAimCameraFollow`, so the camera visibly leans toward the mouse direction.
- Increased F5/F6/F7 camera follow preset values.
- Added debug fields `mouseAimCameraViewportOffset` and `cameraFollowState`.

# MAVERICK Fresh Changelog

## v0.18 — Telemetry-Based Instructor + Camera Tuning

- Added AoA/AoS/vertical-speed telemetry estimates.
- Added `coordinatedYawAssistOutput` and Coordinated Yaw Assist tuning fields.
- Tuned F5/F6/F7 presets using F/A-18 and F-14 War Thunder telemetry observations.
- Added G/AoA protection fields for short-term high-G/high-AoA behavior.
- Improved speed feel with moderate thrust/cruise/max-combat-speed tuning.
- Added War-Thunder-like mouse aim camera follow to `MavMouseFlightRig`.
- Expanded `MavFlightDataRecorder` CSV columns for Instructor and aero telemetry.
- Added `tools/telemetry/wt_telemetry_recorder.py`.
- Added `tools/telemetry/compare_wt_maverick.py`.
- Updated HUD with compact AoA/AoS/Vy/CYAW display.

## v0.17.2 — WT Mouse Authority + Fixed Gun + WT-like Weapon Controls

- Gun shells fire from fixed GunMuzzle direction.
- Mouse0 fires primary gun.
- Space fires selected secondary weapon.
- 1/2 cycle secondary weapons.
- 3 quick-selects missile, 4 quick-selects bomb.
# v0.18.9 - Manual Control Authority Balance Patch

- Restored stronger W/S pitch and A/D roll authority while preserving the heavy jet feel from recent patches.
- Added manual pitch/roll/yaw authority boost fields with partial limiter and damping bypass.
- Added explicit control-surface actuator smoothing and command-rate limits to `MavMouseFlightJet`.
- Retuned F5/F6/F7 pitch, roll, actuator, angular damping, velocity assist, and high-speed limiter values.
- Expanded F2 debug output with manual boost state, raw/smoothed instructor commands, and actuator command state.

## v0.20.8 — F-22 Primary Aircraft Update

- Changed the default/primary aircraft from F15EX to F-22A Raptor.
- Hangar now starts centered on F-22A when no saved session exists.
- In-game fallback aircraft is now F-22A.
- Visual factory/switcher fallbacks now prefer F-22A.
- Retuned F-22 profile for primary gameplay: stronger thrust, higher cruise speed, high-AoA envelope, better high-altitude/ram engine behavior, lower wave drag.
- Added `MavThrustVectorControl` as a profile-driven F-22 TVC assist component.
- Added TVC HUD/F2 debug lines.
- Non-F22 aircraft explicitly disable TVC.
