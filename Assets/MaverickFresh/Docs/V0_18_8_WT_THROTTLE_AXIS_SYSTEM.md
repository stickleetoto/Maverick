# v0.18.8 War-Thunder-like Throttle Axis System

## Concept
- Throttle is now a retained relative axis, not a springing key state.
- Releasing throttle keys leaves the current `throttlePercent` where it was.
- W/S remains pitch control.
- Existing Physical AI can still set throttle through `SetThrottleIntent()` with normalized values.

## Throttle Range
- `-5%`: cutoff / idle-brake / small deceleration region.
- `0%`: idle, with small engine thrust if `engineOn` is true.
- `1%` to `100%`: dry thrust / military power.
- `101%` to `110%`: afterburner / WEP.

## Controls
- `LeftShift`: increase throttle while held.
- `LeftControl`: decrease throttle while held.
- Mouse wheel up/down: step throttle by `5%`.
- `X`: toggle idle/restore. Positive throttle stores the previous positive value and drops to idle; idle/cutoff restores the stored value or 95%.
- `I`: engine on/off while TGP is off. If TGP PIP/focus is active, existing TGP IJKL slew input keeps priority.

## Engine State
- `engineOn = true`: thrust output follows `throttlePercent`.
- `engineOn = false`: thrust output is zero, while the stored throttle percent remains unchanged.
- HUD shows `ENGINE OFF` while the engine is disabled.

## Thrust Mapping
- `0%` throttle maps to `idleThrust01 = 0.04`, not absolute zero.
- `100%` maps to dry thrust around `1.0`.
- `110%` maps to afterburner/WEP using `afterburnerThrustMultiplier = 1.35` by default.
- Negative throttle uses `negativeThrottleBrakeDrag = 0.018` for a small slowdown assist.

## Spool Behavior
- `effectiveThrottle01` moves toward the commanded throttle instead of jumping instantly.
- Balanced default uses `throttleSpoolUpRate = 1.8` and `throttleSpoolDownRate = 2.4`.
- Smooth uses slower spool; Aggressive uses faster spool and stronger afterburner response.

## HUD / Debug
- Normal HUD shows compact states: `THR 95`, `THR 100`, `A/B 105`, `IDLE 0`, `CUT -5`, or `ENGINE OFF`.
- F2 debug shows:
  - `throttlePercent`
  - `effectiveThrottle01`
  - `engineOn`
  - `afterburnerActive`
  - `negativeThrottleActive`

## Preset Defaults
- F5 Balanced:
  - `throttlePercent = 95`
  - `throttleChangeRatePercentPerSecond = 45`
  - `afterburnerThrustMultiplier = 1.35`
- F6 Smooth:
  - `throttlePercent = 90`
  - `throttleChangeRatePercentPerSecond = 35`
  - smoother spool
- F7 Aggressive:
  - `throttlePercent = 100`
  - `throttleChangeRatePercentPerSecond = 60`
  - stronger afterburner response

## Manual Test Checklist
- Hold Shift and confirm throttle percent rises and stays after release.
- Hold Ctrl and confirm throttle percent falls and stays after release.
- Use mouse wheel to step throttle up/down in 5% increments.
- Press X from positive throttle and confirm idle; press X again and confirm restore.
- Hold Ctrl below idle and confirm cutoff/brake can reach -5%.
- With TGP off, press I and confirm HUD shows `ENGINE OFF`, thrust output goes to zero, and stored throttle remains.
- With TGP active, confirm IJKL slew still works and does not toggle the engine.
- Press I again and confirm engine resumes using the stored throttle percent.
- Confirm W/S still pitches the aircraft and never changes throttle.
- Confirm Physical AI F11 still drives throttle and mouse aim through the existing chain.

## Tuning Guide
- If throttle changes too slowly, raise `throttleChangeRatePercentPerSecond`.
- If mouse wheel steps feel too coarse, lower `throttleWheelStepPercent`.
- If afterburner is too strong, lower `afterburnerThrustMultiplier`.
- If cutoff slows the aircraft too much, lower `negativeThrottleBrakeDrag`.
- If engine response feels too jumpy, lower `throttleSpoolUpRate` and `throttleSpoolDownRate`.
- If idle accelerates too much, lower `idleThrust01`.
