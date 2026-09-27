# Gameplay Integration R1 — Audit of the Current Game Flow

Written **before** any R1 code change. Base: `main` @ `f0f9519`.

This note records what the game does today, from Main Lobby to flying, and which parts may be taken out of the **player-facing** flow. Nothing is deleted because it looks old.

---

## 1. Current flow

```
Mav_MainLobby  (MavMainLobbyBootstrap, OnGUI)
   ENTER HANGAR ─────────────► Mav_Hangar
   QUICK F-22 FREE FLIGHT ───► MavGameSession.Launch(F22A)  ─► Mav_InGame
   QUICK F-16 DOGFIGHT ──────► MavGameSession.Launch(F16C)  ─► Mav_InGame

Mav_Hangar  (MavHangarBootstrap, OnGUI)
   A/D, arrows, < PREV / NEXT >  → MavGameSession.SelectAircraft (all five aircraft)
   TEST FLIGHT / Enter            → MavGameSession.Launch(selected, FreeFlight)
   MODE SELECT                    → Free Flight / Test Range / Dogfight / Ground Attack / Carrier Test

Mav_InGame
   MaverickFresh_Manager: MavFreshBootstrap (setupOnAwake)
     → EnsureAuthoritativeAircraftApplied: MavGameSession.SelectedAircraft → MavAircraftProfileApplier on Mav_Player
     → builds the legacy stack on the scene's Mav_Player
```

## 2. Authorities today

| Concern | Owner today |
|---|---|
| **Selection** | `MavGameSession` (static; `Game/MavSceneNames.cs`), persisted in PlayerPrefs. `DefaultAircraft = F22A`. Written by the Hangar on every Next/Prev and by the Lobby quick buttons. |
| **Spawn** | Nothing spawns. `Mav_InGame` carries a hand-made `Mav_Player`, and `MavFreshBootstrap` configures it for whatever `MavAircraftProfileApplier` is told. `MavInGameBootstrap` exists but is **not in any scene**. |
| **Player object** | `Mav_Player` (scene object). It carries the legacy flight stack: `MavMouseFlightJet`, `MavInstructorController`, `MavAeroBody`, `MavAtmosphericEngine`, `MavCombatFlapSystem`, `MavThrustVectorControl`, sensors, WT polish, gun VFX, lead sight. |
| **Physics ownership** | `MavFreshBootstrap.EnsureFlightPhysicsOwnership` installs `MavFlightPhysicsOwnership` → owner **Legacy**. |
| **F-16 FDM** | `MavF16SelectionAutoSetup` (DontDestroyOnLoad, installed `AfterSceneLoad`) scans every `MavAircraftProfileApplier` **4× a second** and adds `MavF16SelectionBinding`. When the F-16 is applied, it builds the Morelli reference stack on `Mav_Player` and **unconditionally keeps `simulationEnabled = false`** (safety hold). |
| **Camera** | `MaverickFresh_MouseFlightRig` (`MavMouseFlightRig`) drives the scene's Main Camera. |
| **HUD** | `MavFreshHud` on the Main Camera and `MavWTQuickHelpOverlay`, plus debug overlays. All developer-dense. |
| **Input** | `MavFreshInput` wrapper (legacy and new input systems both active, `activeInputHandler: 2`). Legacy aircraft use mouse-aim instructor control. The F-15 pilot rig uses `MavKeyboardPilotCommandSource`. |
| **Pause / restart / return** | None in `Mav_InGame`. `MavInGameMenuOverlay` is only added by the unused `MavInGameBootstrap`. |

## 3. Exact runtime path per aircraft

### F-15 (validated pilot-controlled research aircraft)

**Not reachable from the game.** It exists only as:
- `Prefabs/F15/F15_PilotControlledResearch_V1.prefab` and `_V2.prefab`;
- editor menus **Maverick/F-15/Place Pilot-Controlled Research F-15 In Scene** (V1 or V2).

The prefab path, once placed:
```
MavF15PilotControlledRig (Awake: EnsureStack)
  → MavKeyboardPilotCommandSource                      (operational by declaration)
  → MavF15PilotControlLaw (V1) | MavF15PilotControlLawV2 (V2), exactly one enabled
  → MavF15ControlActuator
  → MavF15PilotControlledAeroModel (frozen AFIT/Baumann/Davison routines, SourceReproduction gates)
    + MavF15PilotControlledFixedThrust (8,300 lbf, throttle inactive)
  → MavSixDoFBody
first FixedUpdate: StartFromTrim (Table VII point 36) → MavFlightPhysicsOwnership owner F15PilotControlledResearch
```

**Problems in a player setting:**
- The prefab carries its own *Chase Camera + AudioListener*, which becomes a second camera in any scene that already has one.
- It carries `MavF15PilotControlledHud`, whose **F2 key switches V1 ↔ V2**.
- The V2 prefab starts in V2, but V1 is still one key away.

**Identity drift:**
| Concept | Value |
|---|---|
| Serialized id | `MavAircraftKind.F15E`, catalog id `f15e` |
| Legacy display name | "F-15EX EAGLE II" (legacy tuning blob) |
| Physics configuration id | `F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`, over research configuration `MavF15AfitResearchIdentity.ConfigurationId` |

The model is not an F-15EX.

### F-16

`Mav_Player` with the **legacy** stack and the legacy F-16 profile (Phase 4B turn dynamics, legacy aero) flies the aircraft.

The Morelli reference FDM is attached next to it by `MavF16SelectionBinding` and held off. The hold is unconditional in code:
- its command source is set `treatAsOperationalSource = false`;
- no engine profile declares a source envelope (`MAINLINE_CONSOLIDATION_2026-09.md` §4.2).

**The reference F-16 FDM is not operationally live-ready on `main`, and nothing in the game can make it so.**

### F/A-18, F-22, F-35

Legacy stack with legacy tuning blobs only. There is no validated flight model.

## 4. Obsolete or legacy paths

**Player-visible, can leave the player flow:**
- **Lobby:** "F-22 Primary Jet Combat Sandbox Prototype", "F-22A is the primary aircraft", QUICK F-22 FREE FLIGHT, QUICK F-16 DOGFIGHT.
- **Hangar:**
  - the F/A-18, F-22 and F-35 cards presented as playable;
  - the invented stat bars (SPEED/TURN/… from the legacy tuning blob);
  - MODE SELECT with Test Range / Dogfight / Ground Attack / Carrier Test as if they were equivalent working modes.
- **F-15:** the V1/V2 choice (F2) and the editor-menu placement step.

**Legacy code kept** (development path, validated by the official suites, or both):
- `MavFreshBootstrap`, `Mav_Player` and the legacy stack;
- `MavInGameBootstrap` and `MavInGameMenuOverlay`;
- `MavF16SelectionAutoSetup`;
- `MavAircraftCatalog` and its tuning blobs;
- every V1 file and test.

**Constraints on any change here** (the official Baseline v1 gate):
- `aircraft_startup_order` `[S6]` needs every scene carrying `MavAircraftProfileApplier` to also carry `MavFreshBootstrap` or `MavInGameBootstrap`.
- `[S7]` reads `MavFreshBootstrap.cs` source text.
- `phase5_writer_scan` forbids a new ungated Rigidbody writer anywhere under `Scripts/`.
- The frozen F-15 source scans cover `Scripts/FlightDynamics/` only.

New gameplay code therefore lives outside `FlightDynamics/` and outside the authority roots, and writes no Rigidbody state.

## 5. What R1 changes in the player-facing flow

- **One selection authority:** `MavGameSession`, extended with an explicit launch request. It never holds simulation state.
- **One launch contract:** `MavPlayableAircraftDefinition` (identity, display name, status, spawn strategy, readiness), shared by F-15 and F-16.
- **One spawn authority** in `Mav_InGame`: `MavFlightSessionDirector`, a flight-session state machine that spawns through `MavPlayableAircraftSpawner`.
  - The legacy `Mav_Player` objects are kept, **inactive by default**.
  - They are activated only for an explicit development launch of a legacy aircraft.
- **F-15:** always `AssistedV2`, built from code with no prefab camera and no F2 law key. V1 stays in the repository for validation and development only.
- **F-16:** shown, **not launchable**. Its status is taken from the real hold and blockers, not from what we would like.
  - It gets no fallback aircraft and no legacy F-16 under an FDM label.
  - The legacy F-16 stays reachable only as an explicitly labelled development launch.
