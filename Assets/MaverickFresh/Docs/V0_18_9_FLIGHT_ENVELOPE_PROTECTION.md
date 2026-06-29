# v0.18.9 Flight Envelope Protection / High AoA Slip Guard

## User Feedback Summary

- Initial left/right shake is mostly improved.
- Full W pitch-up at high speed can still destabilize the aircraft.
- Hard turns can create large AoA/AoS/slip and strange physics fighting.
- Debug telemetry showed Mach 1+ speed, A/B 110, significant slip, and very large applied torque.

## High Speed Pitch Limiter

The Instructor now soft-limits final pitch command at high speed before the Jet applies torque.

- `useHighSpeedPitchLimiter = true`
- `pitchLimiterStartSpeed = 260`
- `pitchLimiterFullSpeed = 430`
- `highSpeedPitchAuthorityMin = 0.42`
- `highSpeedManualPitchAuthorityMin = 0.55`
- `highSpeedPitchLimiterSmooth = 6`

Behavior:
- Below 260 m/s, W/S authority stays strong.
- From 260 to 430 m/s, pitch command fades smoothly.
- At 430 m/s and above, pitch still works but feels heavy.
- Manual W/S keeps more authority than mouse autopitch so recovery remains visible.

## AoA / AoS / Slip Guard

The Instructor now reduces final pitch-up, yaw, and roll commands when the aircraft is already outside a safer envelope.

- `useAoAAoSSoftGuard = true`
- `aoaSoftGuardStart = 18`
- `aoaHardGuardStart = 30`
- `aosSoftGuardStart = 10`
- `aosHardGuardStart = 24`
- `highSlipGuardStart = 25`
- `highSlipGuardHard = 55`
- `guardPitchReduction = 0.55`
- `guardYawReduction = 0.45`
- `guardRollReduction = 0.35`

Behavior:
- High AoA reduces nose-up pitch command.
- High AoS or side slip reduces yaw/roll overcorrection.
- The guard does not snap attitude or fully lock controls.
- `instructorState` shows `envelope_guard` when the soft guard is active.

## Torque Clamp

The Jet now clamps and smooths final control torque before `Rigidbody.AddRelativeTorque`.

- `useFinalTorqueClamp = true`
- `maxAppliedTorque = (4200, 2600, 5200)`
- `finalTorqueSmoothing = 10`

Behavior:
- F5/F6/F7 can still tune stronger or softer feel.
- F7 can still request aggressive torque, but the applied torque is capped.
- `debugAppliedTorque` and `debugControlTorqueAfterClamp` show post-clamp torque.

## Velocity Turn Assist Fade

Velocity turn assist now fades when AoS, forward velocity angle, or side slip is extreme.

- `velocityTurnAssistAoSLimit = 45`
- `velocityTurnAssistSlipFadeStart = 20`
- `velocityTurnAssistSlipFadeEnd = 55`

Behavior:
- Normal turns still get velocity alignment help.
- Extreme slip reduces velocity rotation assist and adds damping drag instead.
- F2 shows `VTA` as the current assist factor.

## High Speed / High Angle Drag

The Semi-Aero Stabilizer now adds extra envelope drag when the aircraft is fast and turning hard, or when AoA/AoS/slip is high.

- `highSpeedTurnDragStrength = 0.018`
- `highAoADragStrength = 0.030`
- `highAoSDragStrength = 0.040`
- `maxEnvelopeDragAccel = 28`

Behavior:
- Full W at Mach 1+ bleeds energy instead of holding unrealistic speed.
- Hard turns feel heavier and safer.
- Extreme slip receives more side damping.

## F2 Debug Additions

F2 control debug now reports:

- `HSP`: high speed pitch limiter factor.
- `GUARD`: AoA/AoS/slip command guard factor.
- `CLAMP`: whether final torque was clamped this frame.
- `VTA`: velocity turn assist current factor.
- `DRAG`: envelope drag acceleration amount.
- `CTORQ`: final control torque after clamp/smoothing.

## Manual Play Mode Checklist

1. Start in F5 Balanced.
2. Set throttle to 110 A/B and accelerate past 430 m/s.
3. Hold W for a full pull-up.
4. Confirm nose-up still works but does not spike into unstable motion.
5. Repeat with hard mouse turn plus A/D roll.
6. Open F2 and confirm `HSP`, `GUARD`, `VTA`, `DRAG`, and `CLAMP` change during hard maneuvers.
7. Press S during high AoA and confirm it still helps recovery.
8. Test F6 Smooth and F7 Aggressive.
9. Toggle Physical AI with F11 and confirm it still routes through Instructor -> Jet.
10. Quick smoke CAS/TGP/Radar controls: T/V/Escape/R/G, Y/N/M, Mouse0, Space.

## Tuning Guide

- If high-speed W still feels too violent, lower `highSpeedManualPitchAuthorityMin` toward `0.48`.
- If high-speed W feels too weak, raise `highSpeedManualPitchAuthorityMin` toward `0.65`.
- If mouse aim still overpulls at Mach 1+, lower `highSpeedPitchAuthorityMin`.
- If hard turns feel over-muted, raise `guardRollReduction` or `guardYawReduction`.
- If slip still creates physics fighting, lower `velocityTurnAssistSlipFadeStart` or raise `highAoSDragStrength`.
- If the aircraft bleeds too much speed, reduce `highSpeedTurnDragStrength` or `maxEnvelopeDragAccel`.
- If F7 feels too capped, raise `maxAppliedTorque` slightly while watching F2 `CTORQ`.
