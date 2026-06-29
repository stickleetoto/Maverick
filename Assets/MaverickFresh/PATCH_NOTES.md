# MaverickFresh v0.22.7r — Aero Phase-A Rollback

Scripts-only rollback patch.

## Purpose
Rollback the v0.22.7 Aero Stability Phase-A physics experiment.

## Reverted files
- `Scripts/Aero/MavAeroBody.cs`
- `Scripts/Aero/MavThrustVectorControl.cs`

## Source baseline
These files were restored from the pre-v0.22.7 script baseline where the Aero Phase-A pitch moment / aero damping / Mach authority / TVC assist changes were not applied.

## Not touched
- Scenes
- Prefabs
- Models
- Materials
- Gun scripts
- Missile scripts
- Sensor scripts
- AI scripts
- Editor safe tools

## Notes
This patch is meant to undo the latest physics experiment only. If gun/detail patches also need rollback, do that separately so we do not accidentally overwrite unrelated work.
