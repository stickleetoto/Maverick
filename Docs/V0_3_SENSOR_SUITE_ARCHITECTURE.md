# Eagle Physical AI Lab v0.3 — F-15E-Inspired Sensor Suite

## Purpose

v0.3 adds an abstract F-15E-style sensor layer for gameplay and AI training:

- Radar-style search, track, selection, and lock state
- Targeting pod-style stabilized view, zoom, target track, and CAS designation
- Sensor fusion for AI/validator use
- Sensor telemetry logs for later imitation learning and policy training

This is not a real radar or targeting system model. Values are intentionally gameplay-tuned.

## New Folders

```text
Scripts/Sensors/Radar/
Scripts/Sensors/TargetingPod/
Scripts/Sensors/Fusion/
Scripts/Sensors/UI/
```

## Main Components

### F15ERadarSystem

Attach to the aircraft root.

Responsibilities:

- Modes: Standby, AirSearch, TrackWhileScan, SingleTargetTrack, GroundMap, GroundMovingTarget, CasTargetCue
- Scans objects with RadarSignature
- Maintains tracks with quality/ID confidence
- Selects/locks tracks
- Draws gizmo scan cone and track lines

Default debug keys:

```text
R = cycle radar mode
T = next track
O = lock selected track
U = unlock
```

### RadarSignature

Attach to ground units, aircraft, or sensor-detectable objects.

Fields:

- team
- kind
- radarVisibility
- visualContrast
- heatContrast
- identificationDifficulty
- canBeDesignatedForCas

### RadarSignatureAutoBinder

Prototype helper. It finds GroundUnit objects and adds RadarSignature automatically.

### TargetingPodSystem

Attach to aircraft root.

Responsibilities:

- Targeting pod modes
- Manual slew
- Zoom
- Radar slave
- Area/point tracking
- CAS target designation
- Optional Camera connection for real pod view

Default debug keys:

```text
P = cycle pod mode
Y = slave pod to radar selected/locked track
Enter = designate current target
Backspace = clear pod track
I/J/K/L = slew pod
+/- = zoom
```

### TargetingPodCameraAutoSetup

Optional helper. Creates a simple child Camera for the targeting pod if none is assigned. Replace this with your own cockpit/MFD camera setup later.

### SensorFusionManager

Combines radar tracks, pod contact, active CAS requests, and target team into one fused target.

Responsibilities:

- Select best CAS candidate
- Estimate fused confidence
- Auto-feed AbstractStrikeSystem.selectedTarget
- Provide sensor state for AI/validator

### SensorAidedCasValidator

Optional wrapper around existing CasValidator.

It can reject abstract strikes when:

- fused target does not match selected target
- sensor confidence is too low
- friendly risk near fused target is too high

### SensorLogRecorder

Writes sensor telemetry as JSONL.

Default path:

```text
Application.persistentDataPath/EaglePhysicalAILab/sensor_sessions/
```

### RadarHud / TargetingPodHud / SensorCrosshairOverlay

Simple OnGUI debug overlays.

## Minimal Setup

1. Put scripts under `Assets/EaglePhysicalAI/Scripts/`.
2. Add `EaglePhysicalAIBootstrap` to an empty GameObject.
3. Assign your F-15E root object to `aircraftObject`.
4. Make sure ground objects have `GroundUnit`.
5. Press Play.
6. The bootstrap adds radar, pod, fusion, sensor logs, and HUDs.

## Recommended Scene Objects

Aircraft root:

```text
Rigidbody
AircraftPhysicsController
ManualAircraftInput
F15ERadarSystem
TargetingPodSystem
TargetingPodCameraAutoSetup
SensorFusionManager
AbstractStrikeSystem
SensorLogRecorder
```

Ground targets:

```text
GroundUnit
Collider
RadarSignature
```

If `RadarSignature` is missing, `RadarSignatureAutoBinder` can add it.

## Physical AI Use

v0.3 does not yet retrain the existing 32-value PhysicalAIObservation vector. Instead, sensor telemetry is logged separately so the next patch can add:

- sensor-aware observation vector
- target confidence as AI input
- pod/radar mode actions
- attack/abort decision learning
- CAS target confirmation behavior cloning

Recommended next version:

```text
v0.4 Sensor-Aware Physical AI
- Extend observation vector or add SensorObservation
- Add AI actions for radar mode, pod slew, pod slave, designate, lock/unlock
- Train from tester sensor usage logs
```
