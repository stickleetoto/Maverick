# Project MAVERICK — Camera Fix v0.1

## Why the camera goes under the aircraft

Most likely one of these is happening:

1. `Targeting_Pod_Camera` was auto-created under the aircraft and is rendering directly to the Game view.
   - This camera is physically under the aircraft, so the whole Game view looks like the belly/landing gear.
2. The chase camera offset is using the aircraft local transform before the aircraft forward/up axes are fully corrected.
3. Multiple active cameras exist, and the pod camera or wrong camera has equal/higher depth than Main Camera.

## Fast fix

1. Add `MaverickStableChaseCamera` to `Main Camera`.
2. Assign:
   - `target = F15E_Player`
3. Recommended values:
   - distance = 22
   - height = 6
   - lookAheadDistance = 45
   - disableOtherSceneCamerasOnStart = true
4. Press the component context menu:
   - `Snap To Target`
5. Disable or remove other camera scripts on Main Camera:
   - `WarThunderChaseCamera`
   - `ThirdPersonFlightCamera`
6. If there is a child camera named `Targeting_Pod_Camera`, either:
   - disable its Camera component, or
   - assign a RenderTexture to its `targetTexture`.

## Optional cleaner

Attach `MaverickCameraSceneCleaner` to `Maverick_Manager` and assign `mainCamera`.
It disables pod cameras that render directly to screen.
