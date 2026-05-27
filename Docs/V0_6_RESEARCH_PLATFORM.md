# Eagle Physical AI Lab v0.6 — Research Platform Patch

v0.6 turns the v0.5 integrated flight/sensor AI foundation into a more usable research loop.
It adds mission curriculum, safety supervision, run metrics, data quality monitoring, no-strike zones,
abstract threat zones, and simple policy evaluation tools.

## New Runtime Systems

### Safety

`Scripts/Safety/FlightEnvelopeSafetyGuard.cs`

- watches altitude, stall risk, overspeed, unusual attitude, and crash state
- exposes `FlightSafetyState`: `Nominal`, `Caution`, `Warning`, `Recovery`
- can optionally override autonomous modes with a basic recovery command
- does not model a real aircraft flight control system

### Mission Curriculum

`Scripts/Mission/CurriculumMissionManager.cs`

Stages:

```text
FreeFlight
WaypointApproach
CasOrbit
SensorConfirm
StrikeOrAbort
ReturnToBase
Complete
```

Support marker:

```text
MissionWaypoint.cs
```

Use this to make simple training missions without building a full campaign system.

### Scenario Volumes

`Scripts/Scenario/NoStrikeZone.cs`

- abstract no-fire area
- `CasValidator` now denies target points inside active no-strike zones

`Scripts/Scenario/GroundThreatZone.cs`

- abstract danger/risk area for route planning and reward shaping
- `PhysicalAIRewardTracker` applies a small penalty when the aircraft flies through active threat zones

### Run Metrics

`Scripts/Data/RunMetricsRecorder.cs`

Writes low-frequency JSONL snapshots to:

```text
Application.persistentDataPath/EaglePhysicalAILab/run_metrics/
```

Tracks:

- mission stage
- safety state
- flight/sensor mode
- altitude/speed/stall risk
- fused sensor confidence
- requests and strike counts
- reward return
- curriculum score
- crash/complete/fail flags

### Dataset Quality Monitor

`Scripts/Data/DatasetQualityMonitor.cs`

Live estimate of whether collected demonstrations are useful.
It checks:

- manual ratio
- physical action diversity
- sensor action diversity
- strike signal presence
- sensor interaction ratio

### Policy Evaluation

`Scripts/Training/PolicyEvaluationRunner.cs`

- runs timed episodes in-scene
- can switch flight/sensor modes for evaluation
- resets aircraft to a spawn point
- writes JSONL summaries to:

```text
Application.persistentDataPath/EaglePhysicalAILab/evaluations/
```

### v0.6 Bootstrap

`Scripts/Setup/EagleV06ResearchBootstrap.cs`

Add this to the aircraft or a manager object after v0.5 integration.
It preserves v0.5 and adds:

- `FlightEnvelopeSafetyGuard`
- `CurriculumMissionManager`
- `RunMetricsRecorder`
- `DatasetQualityMonitor`
- `PolicyEvaluationRunner`
- `MissionAndSafetyHUD`

### HUD

`Scripts/UI/MissionAndSafetyHUD.cs`

Shows:

- curriculum stage/progress
- safety state/reason
- data quality estimate
- metrics output path
- policy evaluation status

## New Tools

### Analyze integrated demonstrations

```bash
python Tools/analyze_integrated_demonstrations.py <jsonl_file_or_dir>
```

### Split demonstrations

```bash
python Tools/split_demonstrations.py input.jsonl output_dir --valid 0.1 --test 0.1
```

## Suggested Scene Setup

1. Put v0.6 `Scripts` into `Assets/EaglePhysicalAI/Scripts`.
2. Add `EagleV06ResearchBootstrap` to the aircraft or a scene manager.
3. Assign `aircraftObject` to the F-15E root object.
4. Keep or allow auto-added `EagleV05IntegratedBootstrap`.
5. Add optional `MissionWaypoint` objects for ingress/orbit/return.
6. Add optional `NoStrikeZone` and `GroundThreatZone` volumes.
7. Assign `scriptedTarget` and `scriptedRequester` if using curriculum auto CAS requests.
8. Press Play.

## Important Direction

v0.6 still avoids real-world weapons or avionics replication. It is a game/AI-training abstraction.
The purpose is to collect better tester data and evaluate policy behavior safely inside Unity.

## Default Toggle

`MissionAndSafetyHUD` toggles with `M` by default to avoid F-key conflicts.
