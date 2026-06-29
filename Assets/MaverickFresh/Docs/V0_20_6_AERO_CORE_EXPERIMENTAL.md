# MaverickFresh v0.20.6 — Aero Core Experimental

기준: v0.20.2 Manual Mav_Player / Hangar Selection 구조를 유지하고, 물리만 v0.20.3~v0.20.6까지 한 번에 전진시킨 패치입니다.

## 포함된 단계

- v0.20.3: `MavAeroBody` 신규 공력 코어 추가
- v0.20.4: legacy velocity-turn assist를 aero 활성도에 따라 점진적으로 약화
- v0.20.5: AoA 기반 lift curve / stall / post-stall drag 추가
- v0.20.6: dynamic pressure 기반 조종 권한을 `MavMouseFlightJet` rate controller에 연결

## 새 구조

`Mav_Player`에 다음 컴포넌트를 둡니다.

- Rigidbody
- MavMouseFlightJet
- MavInstructorController
- MavWTFeelPolishController
- MavAircraftProfileApplier
- MavAircraftVisualSwitcher
- MavAeroBody

비행기 FBX는 `Mav_Player/AircraftVisuals/F15EX`, `F16`, `F18`, `F22`, `F35` 같은 시각 모델로만 둡니다.

## 중요한 점

`MavAeroBody`는 아직 Experimental입니다. 기존 arcade physics를 완전히 삭제하지 않고, `aeroBlend`, `liftBlend`, `dragBlend`, `gravityBlend`로 섞는 구조입니다.

초기값은 안전하게 낮게 잡혀 있습니다.

- aeroBlend: 0.44~0.50
- gravityBlend: 0.45
- velocityAssistFade: 0.42~0.55

즉 바로 완전 리얼 물리로 바뀌는 게 아니라, 기존 조작감 위에 양력/항력/중력/실속을 점진적으로 얹습니다.

## HUD

F2 디버그 HUD에서 다음을 확인하세요.

- AERO ON/OFF
- CL/CD
- STALL
- QAUTH
- Lift G / Drag G
- Air density / dynamic pressure

## 튜닝 순서

1. F-15EX 기준으로 250~320 m/s 수평비행 확인
2. `QAUTH`가 0.8~1.0 근처인지 확인
3. 강하게 피치 업해서 `STALL`이 올라가는지 확인
4. 속도 150 m/s 이하에서 `QAUTH`가 낮아지고 조종이 둔해지는지 확인
5. 안정적이면 `aeroBlend`와 `gravityBlend`를 0.55~0.70까지 천천히 올립니다.

## 롤백 방법

`MavAeroBody.useAeroBody = false`로 끄면 기존 legacy 비행에 가깝게 돌아갑니다.
