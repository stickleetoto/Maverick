# MaverickFresh v0.19.0 — Three Scene Hangar Game Overhaul

이번 패치는 기존 MaverickFresh의 “그나마 잘 나는” 비행 코어를 살리고, 위에 게임 구조를 올리는 패치입니다.

## 씬 구조

사용자 요구대로 세 흐름은 전부 별도 씬입니다.

1. `Mav_MainLobby` — 메인 로비
2. `Mav_Hangar` — 3D 격납고 / 기체 선택
3. `Mav_InGame` — 인게임 비행

## 빠른 세팅

1. 이 `MaverickFresh` 폴더를 Unity 프로젝트의 `Assets/` 안에 넣습니다.
2. Unity가 컴파일을 끝낼 때까지 기다립니다.
3. 상단 메뉴에서 `Maverick > Create Three-Scene Game Pack`을 누릅니다.
4. 자동 생성된 `Assets/MaverickFresh/Scenes/Mav_MainLobby.unity`를 엽니다.
5. Play를 누릅니다.

## 조작 흐름

메인 로비:
- `ENTER HANGAR` 클릭

격납고:
- A/D 또는 좌우 방향키: 기체 변경
- Enter: 테스트 비행
- Mode Select: Free Flight / Test Range / Dogfight / Ground Attack / Carrier Test 선택

인게임:
- H 또는 Esc: 격납고로 복귀
- F1: 게임 루프 오버레이 숨김/표시
- 기존 Maverick 조작 유지

## 기체

현재 built-in 프로필:

- F-22A primary / F-15EX
- F-16C
- F/A-18E
- F-22A
- F-35A

처음에는 placeholder 모델이 나옵니다. 실제 모델 프리팹은 `MavHangarBootstrap`과 `MavInGameBootstrap`의 슬롯에 연결하면 됩니다.

## 실제 모델 연결 위치

각 씬의 Bootstrap 오브젝트에서:

- `f15Prefab`
- `f16Prefab`
- `fa18Prefab`
- `f22Prefab`
- `f35Prefab`

에 프리팹을 연결하세요.

격납고와 인게임이 다른 씬이므로, 같은 프리팹 슬롯을 두 씬의 Bootstrap에 각각 연결해야 합니다.


## v0.20.8 note

The default and primary aircraft is now **F-22A Raptor**. Use `Mav_Player/AircraftVisuals/F22` as the main visual slot, with F15EX/F16/F18/F35 as optional secondary aircraft.
