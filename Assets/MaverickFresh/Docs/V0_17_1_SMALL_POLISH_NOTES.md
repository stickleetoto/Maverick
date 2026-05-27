# MAVERICK Fresh v0.17.1 Small Polish Notes

## GunMuzzle firing fix

- `MavCASWeaponSystem` now uses `MavCASOrdnanceAssets.gunMuzzle` as the primary gun shell spawn origin.
- Gun aim can still come from the camera/mouse ray, but gun shells no longer spawn from the camera ray origin.
- If no `gunMuzzle` is assigned, the gun falls back to an aircraft-relative nose/gun position:
  `transform.position + transform.forward * 11.5f + transform.right * 0.85f - transform.up * 1.1f`.

## TGP display default

- `MavTargetingPodSystem.displayMode` remains `PictureInPicture` by default.
- Added `allowFullscreenMode`, default `false`.
- `T` toggles only `Off <-> PictureInPicture`.
- `O` cycles `Off <-> PictureInPicture` while `allowFullscreenMode` is false.
- When `allowFullscreenMode` is true, `O` cycles `Off -> PictureInPicture -> Fullscreen -> Off`.
- The TGP camera continues to render into its `RenderTexture`; the main gameplay camera is not replaced.

## Mouse/instructor softness tuning

- Reduced mouse aim pitch/yaw authority in the instructor defaults and active presets.
- Lowered automatic pitch clamp.
- Increased input smoothing and center pitch comfort.
- Kept keyboard elevator override values intact so `W/S` remains a strong manual elevator path.

## Transform.down validation

- Searched for `.down` usage.
- No `Transform.down` usage remained in the patched scripts.
- Existing `Vector3.down` usages were preserved.

## Manual Unity setup

- Create or assign `F15E_Player/GunMuzzle`.
- Assign that transform to `MavCASOrdnanceAssets.gunMuzzle`.
- Position `GunMuzzle` slightly outside the aircraft collider so shells do not immediately hit the aircraft.
- TGP fullscreen is available only when `MavTargetingPodSystem.allowFullscreenMode` is enabled.
