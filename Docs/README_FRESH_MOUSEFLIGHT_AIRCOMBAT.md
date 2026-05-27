# MAVERICK Fresh MouseFlight AirCombat v0.16 — Physical AI Core

## Direction

Physical AI is postponed.

Fresh branch priority:

```text
1. War-Thunder-like mouse flight feel
2. Camera / free-look / HUD
3. Basic air-combat targets
4. Later: radar / weapons
5. Much later: Physical AI
```

## v0.16 changes

- Added `MavPhysicalAIController`
- Added `MavPhysicalAIRewardLogger`
- Added `MavAITargetDrone`
- Added `MavPhysicalAIScenarioBootstrap`
- Physical AI drives the existing mouse-flight/instructor stack
- Added F11 AI toggle and F3 mode cycle
- Added F4 reward logging
- Added automatic AI target drone scenario
- Physical AI starter is now installed by default, but AI starts disabled

## v0.15 changes

- Added `MavWTFeelPolishController`
- Added F5/F6/F7/F8 War-Thunder-like feel presets
- Added `MavWTQuickHelpOverlay`
- Added H quick help overlay
- Refined TGP default FOV/slew/snap values
- Balanced default WT-like F-15 feel around comfort and CAS usability
- v0.15 focuses on polish, not a large new subsystem

## v0.14 changes

- Added `MavTargetingPodSystem`
- Added TGP camera and RenderTexture view
- Added PIP/fullscreen targeting pod display
- Added I/J/K/L pod slew controls
- Added zoom controls
- Added target/point lock
- Added TGP-to-CAS designation handoff
- CAS bootstrap now installs TGP automatically

## v0.13 changes

- Added asset/prefab-based ordnance support
- Added `MavCASOrdnanceAssets`
- Gun shells, rockets, bombs, and missiles can now use assigned prefabs
- Added optional spawn points for muzzle/rails/pylons
- Added guided Missile weapon
- Projectile script now supports prefab visuals and missile guidance

## v0.12 changes

- Improved W/S as War-Thunder-like elevator override
- W/S now suppresses mouse pitch while held
- Added keyboard elevator response/release tuning
- Added CAS ballistic projectile system
- Added CCIP-lite predictor
- Rockets/bombs/gun now use physical projectiles in sim-lite mode
- CAS is now more realistic than instant damage

## v0.11 changes

- Reduced unwanted climbing tendency
- Added mouse pitch comfort/deadzone curve
- Added nose-high pitch-down assist
- Made W/S behave more like manual elevator override
- Reduced flat yaw further
- Stabilized bank-hold roll behavior
- Tuned F-15 feel toward War Thunder's 800–900 km/h turn band

## v0.10 changes

- Added CAS target component
- Added CAS targeting/designation system
- Added CAS weapon system
- Added CAS test range spawner
- Added CAS starter bootstrap
- Fresh bootstrap now installs CAS starter by default
- Physical AI starter is now off by default until CAS loop is stable

## v0.9 changes

- Added Physical AI starter bootstrap
- Added CSV flight data recorder
- Added simple AI pilot
- Added ghost trajectory recorder
- F9 toggles recording
- F10 toggles Player/SimpleAI mode
- Physical AI begins as data collection + simple autopilot, not full RL yet

## v0.8.6 changes

- Restored left/right horizontal roll zones
- Kept bank-hold roll behavior
- Disabled top/bottom pitch zones
- Disabled continuous no-zone bank mapping
- HUD now shows only RZONE text, with no boundary lines

## v0.8.5 changes

- Removed active screen zone splitting
- Disabled X/Y roll/pitch zones by default
- Removed HUD zone state display
- Kept bank-hold behavior
- Added continuous mouse-X-to-bank-angle mapping

## v0.8.4 changes

- Side roll zones now command target bank angle
- Aircraft no longer rolls endlessly while mouse stays on the side
- Center no-roll zone commands wings-level bank target
- Added bank-hold proportional/damping controller
- HUD now shows current bank, target bank, and bank-hold command

## v0.8.3 changes

- Removed HUD roll-zone boundary guide lines
- Added vertical pitch zones
- Center rectangle now means no-roll and no-pitch
- Top/bottom screen zones now shape pitch input
- HUD text shows both X and Y zone states

## v0.8.2 changes

- Added screen-based roll zones
- Center corridor no longer triggers roll
- Outer left/right screen zones trigger bank turn
- Roll fades in smoothly between no-roll and full-roll zones
- HUD now shows RZONE state and vertical zone guide lines

## v0.8.1 changes

- Left/right flat yaw rotation reduced
- Auto yaw command capped
- Yaw torque reduced
- Yaw damper added
- Side-slip damping strengthened
- Mouse left/right now favors bank-turn over rudder-turn

## v0.8 changes

- Camera horizon stabilization added
- Camera no longer follows 100% aircraft roll
- Conditional instructor stabilizer added
- Auto-level is strong near center and weak during active turns
- Speed-dependent pitch/roll authority curve added
- G limiter-ish pitch suppression added
- Low-speed nose-down assist added
- HUD now shows instructor state and authority values

## v0.7 changes

- Roll speed reduced
- Camera moved closer
- Camera far clip increased to 24000
- Basic attitude stabilizer added
- Natural wings-level recovery added
- Angular velocity damping added
- Gentle pitch recovery added

## v0.6 changes

- W/S pitch authority greatly increased
- Turn rate increased
- Roll response increased
- Added best-turn-speed band around 236 m/s / about 850 km/h
- Added manual pitch/roll boost
- Less overspeed drag so the jet keeps energy better
- New HUD TURN factor

## v0.5 changes

- Added public F-15E-inspired spec profile
- Added `MavF15EFlightSpecProfile`
- Uses high-level public F-15E/F-15-family dimensions, thrust, speed, ceiling, and mass references
- Changes tuning toward heavy twin-engine fighter feel
- Softer pitch, stronger roll, better speed assist
- HUD now shows Mach estimate, G estimate, and speed regime

## v0.4 changes

- Added `MavFreshControlProfile`
- Added tuning presets:
  - `WarThunderMouseAim`
  - `HeavyJet`
  - `ArcadeStable`
  - `DebugDirect`
- Smoother input commands
- Softer pitch
- Less nose-up tendency
- More roll-driven turning
- Speed assist to prevent endless acceleration / low-speed falling
- RMB zoom
- W/S remain pitch controls:
  - W = nose up
  - S = nose down
- Shift/Ctrl are throttle controls
- C/LeftAlt only for held free-look

## Install

Replace:

```text
Assets/MaverickFresh
```

with this ZIP's:

```text
MaverickFresh
```

## Scene setup

Use:

```text
MaverickFresh_Manager
└── MavFreshBootstrap
```

Assign:

```text
Aircraft Object = F15E_Player
Main Camera = Main Camera
```

## Controls

```text
Mouse Move = cursor aim target
Mouse2 = recenter aim
Hold C / LeftAlt = free look
RMB = zoom
W = nose up
S = nose down
A / D = roll override
Q / E = yaw/rudder override
Shift / Ctrl = throttle up/down
X = idle
F12 = HUD toggle

Home = WarThunderMouseAim preset
PageUp = ArcadeStable preset
End = HeavyJet preset
PageDown = DebugDirect preset
```

## Recommended tuning

Start with:

```text
MavFreshControlProfile.preset = WarThunderMouseAim
MavF15EFlightSpecProfile = Apply F-15E Inspired Profile
```

If nose still rises too much:

```text
MavMouseFlightJet.noseDownTrim = 0.16
MavMouseFlightJet.pitchGain = 0.45
MavMouseFlightJet.maxAutoPitch = 0.35
MavMouseFlightJet.turnTorque.x = 50
```

If it feels too slow:

```text
MavMouseFlightJet.rollGain = 1.05
MavMouseFlightJet.turnTorque.z = 100
MavMouseFlightJet.sensitivity = 3.6
```

If camera feels too stiff:

```text
MavMouseFlightRig.camSmoothSpeed = 8
MavMouseFlightRig.cameraLocalPosition.z = -22
```
