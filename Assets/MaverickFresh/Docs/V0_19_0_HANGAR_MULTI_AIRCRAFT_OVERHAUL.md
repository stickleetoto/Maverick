# MaverickFresh v0.19.0 — Hangar & Multi-Aircraft Overhaul

## 방향

기존 MaverickFresh의 비행 가능한 코어는 유지하고, 위에 게임 루프를 올린다.

- 메인 로비: `Mav_MainLobby`
- 3D 격납고: `Mav_Hangar`
- 인게임 비행: `Mav_InGame`

세 씬은 반드시 분리된다.

## 추가된 것

- `MavGameSession` / `MavSceneNames`
- `MavAircraftKind`
- `MavAircraftRuntimeProfile`
- `MavAircraftCatalog`
- `MavAircraftProfileApplier`
- `MavAircraftVisualFactory`
- `MavMainLobbyBootstrap`
- `MavHangarBootstrap`
- `MavInGameBootstrap`
- `MavInGameMenuOverlay`
- `Maverick/Create Three-Scene Game Pack` Editor 메뉴

## 기체 5종

- F-15E: 무겁고 안정적, 고속/고무장
- F-16C: 가볍고 민첩, 롤 빠름
- F/A-18E: 저속 안정성, 함재기 느낌
- F-22A: 고AoA, 강한 피치, TVC 느낌
- F-35A: 안정적인 센서/정밀타격형 멀티롤

## 설치/테스트

1. `MaverickFresh` 폴더를 `Assets/` 아래에 넣는다.
2. Unity 상단 메뉴에서 `Maverick > Create Three-Scene Game Pack` 실행.
3. `Assets/MaverickFresh/Scenes/Mav_MainLobby.unity`를 연다.
4. Play.
5. 메인 로비 → 격납고 → 기체 선택 → 모드 선택 → 인게임 비행.

## 실제 모델 연결

`MavHangarBootstrap`과 `MavInGameBootstrap`에 각각 다음 슬롯이 있다.

- `f15Prefab`
- `f16Prefab`
- `fa18Prefab`
- `f22Prefab`
- `f35Prefab`

처음에는 placeholder 기체가 자동 생성된다. 실제 모델을 넣으면 해당 프리팹을 슬롯에 연결하면 된다.

## 주의

- 기존 비행 코어(`MavMouseFlightJet`, `MavInstructorController`, `MavWTFeelPolishController`)는 그대로 사용한다.
- 실제 항공역학 재현이 아니라 영상용/게임용 아케이드-시뮬레이션 튜닝이다.
- 기체별 수치는 `MavAircraftCatalog` 안의 built-in profile에서 먼저 조정한다.
