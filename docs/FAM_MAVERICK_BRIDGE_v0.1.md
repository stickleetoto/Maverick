# FAM ↔ MAVERICK Flight Observer Bridge v0.1

This branch reuses MAVERICK as a human-flight data source for the FAM research project. It does **not** add a new simulator and it does not let FAM control weapons or engagement logic.

## Goal

Record player-flown aircraft state and control history at the FAM decision rate (4 Hz), attach a conservative flight-behavior label, and export lossless JSONL for offline FAM training/relabeling.

## Components

- `FamBehaviorToken.cs` — FAM flight-only token vocabulary.
- `FamFlightFrame.cs` — versioned JSONL schema.
- `FamBehaviorLabeler.cs` — conservative heuristic labeler. Raw controls remain in every frame, so labels can be replaced later.
- `FamFlightRecorder.cs` — passive 4 Hz recorder. It never writes controls.
- `Tools/fam_dataset_audit.py` — standalone dataset quality/transition audit.

## Unity setup

1. Open the existing MAVERICK project.
2. Select the player aircraft.
3. Ensure it has:
   - `AircraftPhysicsController`
   - `AircraftStateSensor`
   - `ManualAircraftInput` or a compatible subclass
4. Add `FamFlightRecorder` to the player aircraft.
5. Optionally assign another aircraft's `AircraftStateSensor` as `leaderSensor` for formation/following data.
6. Set `pilotId` and `scenarioId` if desired.
7. Play the scene and press **F8** to start/stop recording.

The recorder is passive. Player/manual control stays authoritative.

## Output

Files are written under:

```text
Application.persistentDataPath/
  EaglePhysicalAILab/
    fam_flight/
      <pilot>_<scenario>_fam_<timestamp>.jsonl
```

Each frame contains:

- versioned schema id
- session id / step / time / dt
- ego position, attitude, velocity, speed, altitude, AoA, stall state
- raw pitch/roll/yaw/throttle and throttle-rate
- optional leader-relative state
- heuristic FAM flight behavior token

Weapon/target/strike fields are intentionally absent.

## Dataset audit

```powershell
python Tools/fam_dataset_audit.py "<recording>.jsonl"
```

Optional JSON report:

```powershell
python Tools/fam_dataset_audit.py "<recording>.jsonl" --out fam_audit.json
```

The audit reports:

- token counts and entropy
- within-session token transitions
- approximate sample rate
- leader availability
- control variation
- basic quality gates

## Important interpretation rule

The heuristic behavior token is **not ground truth**. The raw player controls and aircraft state are the authoritative demonstration record. FAM can later relabel the same JSONL with a better sequence-aware labeler without replaying the flight.

## Recommended first dataset

Collect multiple 30–120 second manual sessions with deliberately varied flight behavior:

- straight/steady flight
- left/right turns
- climbs/descents
- speed changes
- formation hold
- falling behind and rejoining
- closing/opening distance
- recovery from unstable flight

The important property is **within-session behavior transitions**, not raw file count.

## Next bridge stage

After enough human sessions exist, add an offline converter from `FAM-MAVERICK-FLIGHT-v0.1` JSONL into the current FAM Python `StateSchema`/`ActorSchema` dataset. Keep that conversion outside Unity so FAM remains engine-independent.
