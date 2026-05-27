# MAVERICK Fresh v0.16 Stabilization Notes

## Compile Fixes Made

- Repaired malformed HUD, quick help, and targeting pod GUI strings that could break C# parsing.
- Replaced Unity 6-style Rigidbody API calls with older-compatible properties:
  - `Rigidbody.linearVelocity` -> `Rigidbody.velocity`
  - `Rigidbody.linearDamping` -> `Rigidbody.drag`
  - `Rigidbody.angularDamping` -> `Rigidbody.angularDrag`
- Removed the `FindObjectsOfType<Camera>(true)` overload from the bootstrap camera pass for broader Unity compatibility.
- Kept existing CAS, TGP, and Physical AI class/enums in place without architectural rewrites.

## Runtime Fixes Made

- Physical AI now applies its `mouseAim` command in `LateUpdate`, so it reliably drives the existing `MavMouseFlightRig` after the rig has processed player input for the frame.
- Physical AI is forced disabled during scenario bootstrap startup, matching the expected F11 opt-in flow.
- Added `mouseAim` null guards before AI scripts write to the rig.
- Added safe fallback target-name matching if AI target search strings are empty.
- Added `MavAITargetDrone.currentVelocity` so AirIntercept lead calculation does not depend on kinematic Rigidbody velocity behavior.
- Preserved asset-based ordnance prefab assignment through `MavCASOrdnanceAssets`.

## Controls

- `F11` toggles Physical AI on/off.
- `F3` cycles Physical AI mode.
- `F4` toggles Physical AI reward logging.
- `F9` toggles full flight CSV recording.
- `F12` toggles the main Fresh HUD.
- `H` toggles the quick help overlay.
- CAS/TGP controls remain:
  - `F` designate, `Tab` cycle target, `Backspace` clear.
  - `1/2` previous/next weapon, `Mouse0` gun, `Space` secondary weapon.
  - `T` TGP display, `O` TGP mode, `I/J/K/L` slew, `5/6` zoom, `R` lock, `G` push designation.

## Known Limitations

- This is still a heuristic physical AI controller, not ML-Agents training.
- AirIntercept uses simple lead pursuit and energy safety logic; it does not yet model advanced BFM.
- CAS auto-fire remains disabled by default.
- The local sandbox did not include a Unity Editor install, so final Unity assembly compilation still needs to be confirmed by opening the project in Unity.
- This repository snapshot is an importable `MaverickFresh` folder, not a full `Assets` project root. After import, this note maps to `Assets/MaverickFresh/Docs/V0_16_STABILIZATION_NOTES.md`.

## Next Recommended Work

- Open the Unity project and confirm the Console has no script compile errors.
- Enter Play Mode with `MavFreshBootstrap` active and verify `F15E_Player` receives CAS, TGP, recorder, reward logger, ghost trail, and Physical AI components once.
- Press `F11` and verify AirIntercept steers by moving `MavMouseFlightRig.mouseAim` instead of rotating or teleporting the aircraft.
- Run one short `F4` reward log and one short `F9` flight CSV recording, then confirm files appear under `Application.persistentDataPath/MaverickFresh`.
