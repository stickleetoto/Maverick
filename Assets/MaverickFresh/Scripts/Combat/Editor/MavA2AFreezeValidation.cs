#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MaverickFresh.Combat.Legacy;
using MaverickFresh.Combat.Sensors;
using MaverickFresh.Combat.Targeting;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    /// <summary>
    /// Deterministic validation for A2A-Only Combat Freeze R0: air-to-ground is dormant, preserved, and
    /// barred from the active runtime, while the A2A stack keeps running.
    ///
    /// TWO CLAIMS, AND THEY PULL AGAINST EACH OTHER. "A2G cannot activate" and "A2A still works" are easy
    /// to satisfy one at a time and easy to break together - the composition root installs both stacks from
    /// one method, and an early return in the wrong place freezes everything. So the central case here runs
    /// the REAL bootstrap and asserts both halves of what it produced.
    ///
    /// EDIT MODE DOES NOT RUN LIFECYCLE CALLBACKS. Unity raises no Awake, OnEnable or Update outside play
    /// mode, so where a barrier lives in one of those it is invoked by reflection. That is deliberate and
    /// the same approach the consumer-migration suite settled on: widening the method to observe it would
    /// change the production surface, and Unity's guarantee to CALL it is not what is in doubt.
    ///
    /// SOME BARRIERS ARE STRUCTURAL AND SAID SO. The AI writes CAS designation by assigning public fields
    /// directly, and no guard inside the designator can intercept a field assignment. That barrier is
    /// checked at its source, which is where it is actually decided - marked clearly rather than dressed up
    /// as a behavioral proof.
    /// </summary>
    public static class MavA2AFreezeValidation
    {
        private const string DocPath = "../Docs/Combat/A2A_ONLY_COMBAT_FREEZE_R0.md";
        private const string HudPath = "MaverickFresh/Scripts/MavFreshHud.cs";
        private const string AiPath = "MaverickFresh/Scripts/PhysicalAI/MavPhysicalAIController.cs";
        private const string HelpPath = "MaverickFresh/Scripts/MavWTQuickHelpOverlay.cs";

        /// <summary>
        /// Every A2G source file. Freezing is quarantine, not deletion: each of these must still be here,
        /// so that restoring air-to-ground is a policy change rather than an archaeology exercise.
        /// </summary>
        private static readonly string[] AirToGroundSources =
        {
            "MaverickFresh/Scripts/CAS/MavCASBallisticProjectile.cs",
            "MaverickFresh/Scripts/CAS/MavCASCCIPPredictor.cs",
            "MaverickFresh/Scripts/CAS/MavCASOrdnanceAssets.cs",
            "MaverickFresh/Scripts/CAS/MavCASStarterBootstrap.cs",
            "MaverickFresh/Scripts/CAS/MavCASTarget.cs",
            "MaverickFresh/Scripts/CAS/MavCASTargetingSystem.cs",
            "MaverickFresh/Scripts/CAS/MavCASTestRangeSpawner.cs",
            "MaverickFresh/Scripts/CAS/MavCASWeaponSystem.cs",
            "MaverickFresh/Scripts/CAS/MavNeonTracer.cs",
            "MaverickFresh/Scripts/CAS/MavTGPStateManager.cs",
            "MaverickFresh/Scripts/CAS/MavTargetingPodSystem.cs",
        };

        /// <summary>Declarations that would mean a later phase had started under cover of this one.</summary>
        private static readonly string[] PrematureDeclarations =
        {
            "class MavSeeker",
            "class MavMissileSeeker",
            "class MavMissileGuidance",
            "class MavGuidanceLaw",
            "class MavProportionalNavigation",
            "class MavMissileAutopilot",
            "class MavMissileCore",
            "class MavLaunchAuthorization",
            "class MavTrackPrediction",
        };

        [MenuItem("Maverick/Combat/Run A2A Freeze Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("A2A Freeze Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick A2A-Only Combat Freeze Validation R0");
            report.AppendLine("============================================");

            ValidatePolicy(report, ref passed, ref failed);
            ValidateBootstrapInstallsA2AOnly(report, ref passed, ref failed);
            ValidateCasDesignatorFrozen(report, ref passed, ref failed);
            ValidatePodFrozen(report, ref passed, ref failed);
            ValidateWeaponReleaseFrozen(report, ref passed, ref failed);
            ValidateAiDesignationWriterDisabled(report, ref passed, ref failed);
            ValidateHudPresentation(report, ref passed, ref failed);
            ValidateA2AStackIntact(report, ref passed, ref failed);
            ValidateSourcePreserved(report, ref passed, ref failed);
            ValidateScopeHeld(report, ref passed, ref failed);
            ValidateRolesDocumented(report, ref passed, ref failed);

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---- the policy itself ---------------------------------------------------------------------
        private static void ValidatePolicy(StringBuilder report, ref int passed, ref int failed)
        {
            Record(MavCombatScopePolicy.AirToGroundFrozen,
                   "F-001", "air-to-ground is frozen",
                   report, ref passed, ref failed);
            Record(!MavCombatScopePolicy.AirToGroundAllowed,
                   "F-001b", "so no A2G system is allowed to install, enable or act",
                   report, ref passed, ref failed);

            // No runtime setter: nothing in a scene, a debug menu or a stray script can re-open A2G.
            Type t = typeof(MavCombatScopePolicy);
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.Static);
            bool anySetter = false;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].CanWrite)
                    anySetter = true;
            }
            Record(!anySetter,
                   "F-002", "the policy exposes no setter, so the freeze cannot be lifted at runtime",
                   report, ref passed, ref failed);

            FieldInfo frozen = t.GetField("AirToGroundFrozen", BindingFlags.Public | BindingFlags.Static);
            Record(frozen != null && frozen.IsInitOnly,
                   "F-002b", "and the decision itself is readonly",
                   report, ref passed, ref failed);
            Record(t.IsAbstract && t.IsSealed,
                   "F-002c", "the policy is static: there is no instance to configure differently",
                   report, ref passed, ref failed);
        }

        // ---- the composition root: A2A installed, A2G not ------------------------------------------
        /// <summary>
        /// The central case. <c>MavCASStarterBootstrap.Setup</c> installs BOTH stacks, so this drives the
        /// real bootstrap and asserts what it actually produced - the A2A components present, the A2G
        /// components absent.
        ///
        /// Written this way because the obvious implementation of this phase - return early from Setup -
        /// silently freezes the A2A stack too, since InstallTargetTrackCore is called from the end of the
        /// same method. A test that only checked "no CAS components" would have passed that mistake.
        /// </summary>
        private static void ValidateBootstrapInstallsA2AOnly(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject manager = new GameObject("MavFreezeManagerHost");
            GameObject aircraft = new GameObject("MavFreezeAircraft");
            try
            {
                MavCASStarterBootstrap boot = manager.AddComponent<MavCASStarterBootstrap>();
                boot.aircraftObject = aircraft;
                boot.addTargeting = true;
                boot.addWeapons = true;
                boot.addTargetingPod = true;
                boot.addTGPStateManager = true;

                boot.Setup();

                // A2A: every one of these must be here. This is the path the phase exists to keep.
                Record(aircraft.GetComponent<MavTargetTrackOwner>() != null,
                       "F-010", "the bootstrap still installs the track owner",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavEngagementView>() != null,
                       "F-010b", "and the engagement view",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavRadarSensor>() != null,
                       "F-011", "and the radar sensor: Radar Core stays active",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavTrackLockController>() != null,
                       "F-012", "and the lock controller: the authoritative lock stays active",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavLegacyTargetObservationFeed>() != null,
                       "F-013", "and the legacy observation feed, which is migration evidence",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavLegacyEngagementProbe>() != null,
                       "F-013b", "and the legacy engagement probe",
                       report, ref passed, ref failed);

                // A2G: none of these may be here.
                Record(aircraft.GetComponent<MavCASTargetingSystem>() == null,
                       "F-014", "but it installs no CAS designator",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavCASWeaponSystem>() == null,
                       "F-014b", "no CAS weapon system",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavTargetingPodSystem>() == null,
                       "F-014c", "no targeting pod",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavTGPStateManager>() == null,
                       "F-014d", "no TGP state manager",
                       report, ref passed, ref failed);
                Record(aircraft.GetComponent<MavCASCCIPPredictor>() == null
                       && aircraft.GetComponent<MavCASOrdnanceAssets>() == null,
                       "F-014e", "and no CCIP predictor or ordnance assets",
                       report, ref passed, ref failed);

                // The install flags were all left true, which is the point: the freeze does not depend on
                // scene data being edited.
                Record(boot.addTargeting && boot.addWeapons && boot.addTargetingPod,
                       "F-015", "and it refused with every A2G install flag still set to true",
                       report, ref passed, ref failed);

                // The lock controller was wired to the owner and view, not left dangling.
                MavTrackLockController ctl = aircraft.GetComponent<MavTrackLockController>();
                Record(ctl != null && ctl.owner != null && ctl.engagementView != null,
                       "F-016", "the installed lock authority is wired to its track owner and view",
                       report, ref passed, ref failed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aircraft);
                UnityEngine.Object.DestroyImmediate(manager);
            }
        }

        // ---- the CAS designator cannot act ---------------------------------------------------------
        private static void ValidateCasDesignatorFrozen(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavFreezeCasHost");
            try
            {
                MavCASTargetingSystem cas = host.AddComponent<MavCASTargetingSystem>();

                Record(!cas.IsDesignationAllowed,
                       "F-020", "a CAS designator that exists anyway reports designation as not allowed",
                       report, ref passed, ref failed);

                // Its Awake is the barrier that disables it. Edit mode does not raise it, so drive it.
                MethodInfo awake = typeof(MavCASTargetingSystem).GetMethod(
                    "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                Record(awake != null,
                       "F-021", "the designator's startup barrier can be driven directly",
                       report, ref passed, ref failed);
                if (awake != null)
                {
                    awake.Invoke(cas, null);
                    Record(!cas.enabled,
                           "F-021b", "and it switches the component off",
                           report, ref passed, ref failed);
                    Record(cas.status == "a2g_frozen",
                           "F-021c", "recording why, rather than looking merely idle",
                           report, ref passed, ref failed);
                }

                // The designation command the F key reaches. Fails closed and says so.
                bool designated = cas.DesignateCandidateOrPoint();
                Record(!designated,
                       "F-022", "the designate command refuses",
                       report, ref passed, ref failed);
                Record(cas.status.EndsWith(MavCombatScopePolicy.FrozenEventSuffix),
                       "F-022b", "and reports the freeze rather than an ordinary miss",
                       report, ref passed, ref failed);
                Record(cas.designatedTarget == null && !cas.hasDesignatedPoint,
                       "F-022c", "leaving nothing designated",
                       report, ref passed, ref failed);

                // The cycle command Tab reaches.
                cas.CycleTarget();
                Record(cas.designatedTarget == null && !cas.hasDesignatedPoint,
                       "F-023", "the cycle-target command designates nothing",
                       report, ref passed, ref failed);
                Record(cas.status.EndsWith(MavCombatScopePolicy.FrozenEventSuffix),
                       "F-023b", "and also reports the freeze",
                       report, ref passed, ref failed);

                // The bare-ground-point path: nothing is resolved, so there is nothing to designate even
                // if the candidate search were somehow reached.
                cas.hasAimGroundPoint = true;
                cas.UpdateGroundPoint();
                Record(!cas.hasAimGroundPoint,
                       "F-024", "no bare world point is resolved for designation",
                       report, ref passed, ref failed);
                Record(!cas.DesignateCandidateOrPoint() && !cas.hasDesignatedPoint,
                       "F-024b", "so the bare-point designation path has nothing to act on",
                       report, ref passed, ref failed);

                // The input loop itself returns before reading any key.
                MethodInfo update = typeof(MavCASTargetingSystem).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                if (update != null)
                {
                    cas.enabled = true;
                    update.Invoke(cas, null);
                    Record(!cas.enabled && cas.designatedTarget == null,
                           "F-025", "its input loop switches itself off instead of reading A2G keys",
                           report, ref passed, ref failed);
                }
                else
                {
                    Record(false, "F-025", "its input loop switches itself off instead of reading A2G keys",
                           report, ref passed, ref failed);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        // ---- the pod cannot lock, least of all a bare point ----------------------------------------
        private static void ValidatePodFrozen(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavFreezePodHost");
            try
            {
                MavTargetingPodSystem pod = host.AddComponent<MavTargetingPodSystem>();

                Record(!pod.IsPodAllowed,
                       "F-030", "a targeting pod that exists anyway reports itself as not allowed",
                       report, ref passed, ref failed);

                // A bare point lock: the case the track vocabulary deliberately cannot express, and the
                // reason it must not be establishable while A2G is frozen.
                pod.hasLookPoint = true;
                pod.lookPoint = new Vector3(120f, 0f, 3000f);
                pod.ToggleLock();

                Record(!pod.isLocked,
                       "F-031", "the pod cannot establish a bare world-point lock",
                       report, ref passed, ref failed);
                Record(!pod.lockedToTarget && pod.lockedTarget == null,
                       "F-031b", "nor a target lock",
                       report, ref passed, ref failed);
                Record(!pod.hasLookPoint,
                       "F-031c", "and its look point is cleared rather than left ready",
                       report, ref passed, ref failed);
                Record(pod.status == "a2g_frozen",
                       "F-031d", "recording why",
                       report, ref passed, ref failed);

                // Two flight systems read displayMode to decide whether the pod owns the view.
                pod.displayMode = MavTargetingPodDisplayMode.Fullscreen;
                pod.SetFocus(true);
                Record(pod.displayMode == MavTargetingPodDisplayMode.Off,
                       "F-032", "the pod cannot take the camera: focus mode is refused",
                       report, ref passed, ref failed);

                pod.displayMode = MavTargetingPodDisplayMode.Fullscreen;
                pod.SetPip(true);
                Record(pod.displayMode == MavTargetingPodDisplayMode.Off,
                       "F-032b", "and picture-in-picture is refused",
                       report, ref passed, ref failed);

                // Pushing a designation to CAS is the pod's other writer.
                pod.PushDesignationToCAS();
                Record(pod.status.EndsWith(MavCombatScopePolicy.FrozenEventSuffix),
                       "F-033", "and it cannot push a designation to CAS",
                       report, ref passed, ref failed);

                MethodInfo awake = typeof(MavTargetingPodSystem).GetMethod(
                    "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                if (awake != null)
                {
                    pod.enabled = true;
                    pod.displayMode = MavTargetingPodDisplayMode.PictureInPicture;
                    awake.Invoke(pod, null);
                    Record(!pod.enabled && pod.displayMode == MavTargetingPodDisplayMode.Off,
                           "F-034", "its startup barrier disables it and forces the display off",
                           report, ref passed, ref failed);
                }
                else
                {
                    Record(false, "F-034", "its startup barrier disables it and forces the display off",
                           report, ref passed, ref failed);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        // ---- no A2G weapon can be released, gun included -------------------------------------------
        /// <summary>
        /// The gun is asserted here with the bombs on purpose. It is declared, selected, fed and fired by
        /// MavCASWeaponSystem, so keeping it would mean keeping that component running - the half-frozen
        /// ownership this phase exists to prevent.
        /// </summary>
        private static void ValidateWeaponReleaseFrozen(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavFreezeWeaponHost");
            try
            {
                MavCASWeaponSystem weapons = host.AddComponent<MavCASWeaponSystem>();

                Record(!weapons.IsWeaponReleaseAllowed,
                       "F-040", "a CAS weapon system that exists anyway reports release as not allowed",
                       report, ref passed, ref failed);

                int gun = weapons.gunAmmo;
                int rockets = weapons.rocketAmmo;
                int bombs = weapons.bombAmmo;
                int pgm = weapons.precisionAmmo;
                int missiles = weapons.missileAmmo;

                weapons.lastEvent = "unset";
                bool gunFired = weapons.TryFirePrimary();
                Record(!gunFired,
                       "F-041", "the gun cannot fire - it is CAS-owned and frozen with the rest",
                       report, ref passed, ref failed);
                Record(weapons.lastEvent.EndsWith(MavCombatScopePolicy.FrozenEventSuffix),
                       "F-041b", "and says so rather than reporting an empty magazine",
                       report, ref passed, ref failed);

                weapons.lastEvent = "unset";
                bool secondaryFired = weapons.TryFireSecondary();
                Record(!secondaryFired
                       && weapons.lastEvent.EndsWith(MavCombatScopePolicy.FrozenEventSuffix),
                       "F-042", "the secondary store cannot be released",
                       report, ref passed, ref failed);

                // Each store checks the RECORDED REASON as well as the return value. Negative-testing
                // showed why: in edit mode these weapons cannot fire anyway - no rig, no camera, no
                // ordnance assets - so `returns false` alone passes even with the freeze lifted. The reason
                // string is what distinguishes "refused because frozen" from "failed for its own reasons".
                Record(RefusedAsFrozen(weapons, MavCASWeapon.Rockets),
                       "F-043", "rockets cannot be released, and the refusal names the freeze",
                       report, ref passed, ref failed);
                Record(RefusedAsFrozen(weapons, MavCASWeapon.TrainingBomb),
                       "F-043b", "bombs cannot be released",
                       report, ref passed, ref failed);
                Record(RefusedAsFrozen(weapons, MavCASWeapon.PrecisionStrike),
                       "F-043c", "the precision weapon cannot be released",
                       report, ref passed, ref failed);
                Record(RefusedAsFrozen(weapons, MavCASWeapon.Missile),
                       "F-043d", "and the A2G missile cannot be released",
                       report, ref passed, ref failed);

                // Nothing was consumed by any of those attempts. A refusal that still decremented ammo
                // would mean the store had been released and merely failed to appear.
                Record(weapons.gunAmmo == gun && weapons.rocketAmmo == rockets
                       && weapons.bombAmmo == bombs && weapons.precisionAmmo == pgm
                       && weapons.missileAmmo == missiles,
                       "F-044", "and no ammunition was consumed by the refused releases",
                       report, ref passed, ref failed);

                MethodInfo update = typeof(MavCASWeaponSystem).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                if (update != null)
                {
                    weapons.enabled = true;
                    update.Invoke(weapons, null);
                    Record(!weapons.enabled,
                           "F-045", "its input loop switches itself off instead of reading trigger keys",
                           report, ref passed, ref failed);
                }
                else
                {
                    Record(false, "F-045", "its input loop switches itself off instead of reading trigger keys",
                           report, ref passed, ref failed);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        /// <summary>
        /// Whether selecting this store and pulling the trigger was refused BECAUSE of the freeze.
        ///
        /// The return value alone is not enough: in edit mode these weapons fail for lack of a rig, camera
        /// and ordnance assets, so a false return proves nothing. The recorded event does.
        /// </summary>
        private static bool RefusedAsFrozen(MavCASWeaponSystem weapons, MavCASWeapon store)
        {
            weapons.lastEvent = "unset";
            weapons.selectedWeapon = store;
            bool fired = weapons.TryFireSelected();
            return !fired && weapons.lastEvent.EndsWith(MavCombatScopePolicy.FrozenEventSuffix);
        }

        // ---- the AI is no longer a CAS designation writer ------------------------------------------
        /// <summary>
        /// STRUCTURAL, and it has to be. The AI writes <c>casTargeting.designatedTarget</c> and three more
        /// public fields by direct assignment. No guard inside the designator can intercept a field write,
        /// so the barrier lives at the write site and is checked there. Asserted as source, not dressed up
        /// as behavior.
        /// </summary>
        private static void ValidateAiDesignationWriterDisabled(StringBuilder report, ref int passed, ref int failed)
        {
            string source = ReadAsset(AiPath);
            Record(source.Length > 0,
                   "F-050", "the AI source can be read",
                   report, ref passed, ref failed);
            if (source.Length == 0)
                return;

            string code = StripComments(source);

            // Every designation write must sit behind the scope check.
            Record(code.Contains("allowAutoDesignate && casTargeting != null && MavCombatScopePolicy.AirToGroundAllowed"),
                   "F-051", "the AI's CAS designation write is gated on the scope policy",
                   report, ref passed, ref failed);

            // And there must be no second, ungated write anywhere in the file.
            int writes = CountOccurrences(code, "casTargeting.designatedTarget =");
            Record(writes == 1,
                   "F-051b", "and there is exactly one such write site to gate",
                   report, ref passed, ref failed);
            Record(CountOccurrences(code, "casTargeting.hasDesignatedPoint = true") == 1,
                   "F-051c", "with one matching hasDesignatedPoint write",
                   report, ref passed, ref failed);

            // The AI's A2G release call is gated too.
            Record(code.Contains("MavCombatScopePolicy.AirToGroundAllowed")
                   && code.Contains("casWeapons.TryFireSelected()"),
                   "F-052", "and its A2G release call is gated as well",
                   report, ref passed, ref failed);

            int policyChecks = CountOccurrences(code, "MavCombatScopePolicy.AirToGroundAllowed");
            Record(policyChecks >= 2,
                   "F-052b", "so both AI A2G paths - designate and release - are behind the policy",
                   report, ref passed, ref failed);

            // AI targeting was not redesigned: its CAS logic is still present, just barred.
            Record(code.Contains("FindCASTarget()"),
                   "F-053", "the AI's CAS logic is preserved, not rewritten",
                   report, ref passed, ref failed);
        }

        // ---- the HUD ------------------------------------------------------------------------------
        private static void ValidateHudPresentation(StringBuilder report, ref int passed, ref int failed)
        {
            string hud = ReadAsset(HudPath);
            Record(hud.Length > 0,
                   "F-060", "the HUD source can be read",
                   report, ref passed, ref failed);
            if (hud.Length == 0)
                return;

            // The player panel's A2G lines are gone, not reworded.
            Record(!hud.Contains("SEC {casWeapons.selectedSecondaryWeapon}"),
                   "F-061", "the player HUD no longer shows A2G weapon status",
                   report, ref passed, ref failed);
            Record(!hud.Contains("TGP {targetingPod.displayMode} {(targetingPod.isLocked ? \"LOCK\" : \"SEARCH\")}"),
                   "F-061b", "and no longer shows a TGP lock/search line",
                   report, ref passed, ref failed);
            Record(!hud.Contains("Mouse0 neon gun"),
                   "F-061c", "and no longer advertises A2G keys to the player",
                   report, ref passed, ref failed);
            Record(!hud.Contains("Space missile/secondary"),
                   "F-061d", "including the secondary-release hint",
                   report, ref passed, ref failed);

            // The developer panel keeps the dormant readouts, labelled.
            Record(hud.Contains("LegacyAirToGroundDebugHudLines"),
                   "F-062", "dormant A2G state is still available to a developer",
                   report, ref passed, ref failed);
            Record(hud.Contains("MavCombatScopePolicy.DormantLabel"),
                   "F-062b", "and every such line carries the dormant label",
                   report, ref passed, ref failed);

            string helper = Between(hud, "private string LegacyAirToGroundDebugHudLines()", "private string TrackHudLine");
            if (helper.Length == 0)
                helper = Between(hud, "private string LegacyAirToGroundDebugHudLines()", "/// <summary>");
            int labelUses = CountOccurrences(helper, "label +");
            Record(labelUses >= 3,
                   "F-062c", "each of the three dormant readouts is labelled, not just the first",
                   report, ref passed, ref failed);
            Record(!helper.Contains("\"LOCK\""),
                   "F-062d", "and no dormant line prints a bare LOCK caption",
                   report, ref passed, ref failed);

            // The authoritative A2A lock line is untouched and still called.
            Record(hud.Contains("AuthoritativeLockHudLine()"),
                   "F-063", "the player HUD still presents the authoritative lock",
                   report, ref passed, ref failed);
            Record(hud.Contains("AuthoritativeLockDebugHudLine()"),
                   "F-063b", "and the developer panel still presents its detail",
                   report, ref passed, ref failed);

            // Player-facing quick help no longer lists A2G commands.
            string help = ReadAsset(HelpPath);
            Record(help.Length > 0 && !help.Contains("F designate")
                   && !help.Contains("R lock | G push designation"),
                   "F-064", "the player quick-help no longer lists CAS or TGP commands",
                   report, ref passed, ref failed);
            Record(help.Contains("MavCombatScopePolicy.DormantLabel"),
                   "F-064b", "and states that air-to-ground is disabled",
                   report, ref passed, ref failed);

            // And the authoritative lock presentation still works, driven for real.
            ValidateAuthoritativeLockStillWorks(report, ref passed, ref failed);
        }

        /// <summary>
        /// The A2A half of the HUD claim, exercised rather than grepped: a real controller, a real lock, and
        /// the presenter the migrated HUD uses.
        /// </summary>
        private static void ValidateAuthoritativeLockStillWorks(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavFreezeLockHudHost");
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();
                owner.trackDropSeconds = 600f;
                ScriptedFeed feed = new ScriptedFeed();
                owner.RegisterFeed(feed);

                MavEngagementView view = host.AddComponent<MavEngagementView>();

                MavTrackLockController ctl = host.AddComponent<MavTrackLockController>();
                ctl.owner = owner;
                ctl.engagementView = view;
                ctl.acquisitionSeconds = 1.0f;
                ctl.staleTrackSeconds = 0.75f;
                ctl.coastSeconds = 2.0f;
                ctl.minimumLockQuality = MavTrackQuality.Tracked;
                ctl.minimumSelectableQuality = MavTrackQuality.Coarse;
                ctl.autoSelectBestTrack = true;

                feed.Set(1, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                ctl.Step(1.0f);

                Record(ctl.LockedTrackId != 0,
                       "F-070", "the authoritative lock still establishes a lock",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.FormatPlayerLine(ctl, view).StartsWith("LOCK LOCKED"),
                       "F-070b", "and the player lock line still reads as a committed lock",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.FormatDebugLine(ctl, view).Contains("AUTH LOCKED"),
                       "F-070c", "with the developer line intact",
                       report, ref passed, ref failed);
                Record(view.authoritativeLockTrackId == ctl.LockedTrackId,
                       "F-070d", "and the engagement view still carries the authoritative track",
                       report, ref passed, ref failed);

                // Radar Core and TargetTrack are alive in the same host: a track existed to lock onto.
                Record(owner.TrackCount > 0,
                       "F-071", "TargetTrack Core still holds tracks",
                       report, ref passed, ref failed);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        // ---- the A2A stack is untouched ------------------------------------------------------------
        private static void ValidateA2AStackIntact(StringBuilder report, ref int passed, ref int failed)
        {
            // Radar Core: the sensor still produces observations through the feed contract.
            GameObject host = new GameObject("MavFreezeRadarHost");
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();
                MavRadarSensor radar = host.AddComponent<MavRadarSensor>();
                radar.owner = owner;

                Record(radar is IMavTargetObservationFeed,
                       "F-080", "Radar Core still registers as an observation feed",
                       report, ref passed, ref failed);
                Record(((IMavTargetObservationFeed)radar).FeedSource == MavTrackSource.Radar,
                       "F-080b", "and still reports itself as a radar source",
                       report, ref passed, ref failed);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }

            // The lock authority is still track-only. The pressure to add a world point to it is exactly
            // what the engagement-target vocabulary exists to absorb, and the freeze does not change that.
            Type auth = typeof(IMavTrackLockAuthority);
            List<string> names = new List<string>();
            PropertyInfo[] props = auth.GetProperties();
            for (int i = 0; i < props.Length; i++)
                names.Add(props[i].Name);
            names.Sort();

            Record(string.Join(",", names.ToArray())
                   == "AcquisitionProgress01,IsLockAuthorityActive,LastLossReason,LockState,TimeInLockSeconds",
                   "F-081", "IMavTrackLockAuthority still exposes exactly its five track-lock properties",
                   report, ref passed, ref failed);

            List<string> leaks = new List<string>();
            MemberInfo[] members = auth.GetMembers();
            string[] forbidden = { "Point", "World", "Designat", "Cas", "Pod" };
            for (int i = 0; i < members.Length; i++)
            {
                for (int k = 0; k < forbidden.Length; k++)
                {
                    if (members[i].Name.IndexOf(forbidden[k], StringComparison.Ordinal) >= 0)
                        leaks.Add(members[i].Name);
                }
            }
            Record(leaks.Count == 0,
                   "F-081b", "and names no point, designation or legacy owner",
                   report, ref passed, ref failed);
            Record(typeof(MavTrackLockController).GetInterface("IMavTrackLockAuthority") != null,
                   "F-082", "MavTrackLockController is still the implementation of it",
                   report, ref passed, ref failed);

            // The engagement target vocabulary is unchanged: all three kinds, WorldPoint dormant but intact.
            Record(MavEngagementTargetRef.FromTrack(4).IsTrack
                   && MavEngagementTargetRef.FromTrack(4).TrackId == 4,
                   "F-083", "the Track case of the target vocabulary is unchanged",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.FromWorldPoint(Vector3.one).IsWorldPoint
                   && MavEngagementTargetRef.FromWorldPoint(Vector3.one).TrackId == 0,
                   "F-083b", "the WorldPoint case survives as dormant infrastructure for A2G restoration",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.None.IsNone,
                   "F-083c", "and None is unchanged",
                   report, ref passed, ref failed);
            Record(typeof(MavEngagementTargetRef)
                   .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Length == 3,
                   "F-083d", "the vocabulary was not redesigned: still three fields",
                   report, ref passed, ref failed);
            Record(Enum.GetNames(typeof(MavEngagementTargetKind)).Length == 3,
                   "F-083e", "and still three kinds",
                   report, ref passed, ref failed);
        }

        // ---- nothing was deleted -------------------------------------------------------------------
        private static void ValidateSourcePreserved(StringBuilder report, ref int passed, ref int failed)
        {
            int missing = 0;
            for (int i = 0; i < AirToGroundSources.Length; i++)
            {
                string path = Path.Combine(Application.dataPath, AirToGroundSources[i]);
                if (!File.Exists(path))
                {
                    missing++;
                    report.AppendLine("        deleted A2G source: " + AirToGroundSources[i]);
                }
            }

            Record(missing == 0,
                   "F-090", "every A2G source file still exists: this is quarantine, not deletion",
                   report, ref passed, ref failed);
            Record(AirToGroundSources.Length == 11,
                   "F-090b", "and all eleven of them are accounted for",
                   report, ref passed, ref failed);

            // The logic inside them is preserved too - frozen, not gutted.
            string weapons = ReadAsset("MaverickFresh/Scripts/CAS/MavCASWeaponSystem.cs");
            Record(weapons.Contains("private bool FireBomb()")
                   && weapons.Contains("private bool FireRocket()")
                   && weapons.Contains("private bool FirePrecisionStrike()")
                   && weapons.Contains("private bool FireGun()"),
                   "F-091", "the A2G release implementations are preserved, only barred",
                   report, ref passed, ref failed);

            string pod = ReadAsset("MaverickFresh/Scripts/CAS/MavTargetingPodSystem.cs");
            Record(pod.Contains("status = \"lock_point\"") && pod.Contains("PushDesignationToCAS"),
                   "F-091b", "as is the pod's point-lock and designation logic",
                   report, ref passed, ref failed);

            string cas = ReadAsset("MaverickFresh/Scripts/CAS/MavCASTargetingSystem.cs");
            Record(cas.Contains("status = \"designated_point\""),
                   "F-091c", "and the CAS bare-point designation logic",
                   report, ref passed, ref failed);
        }

        // ---- scope held ---------------------------------------------------------------------------
        private static void ValidateScopeHeld(StringBuilder report, ref int passed, ref int failed)
        {
            // No later phase started under cover of this one.
            string combatRoot = Path.Combine(Application.dataPath, "MaverickFresh/Scripts");
            List<string> found = new List<string>();
            if (Directory.Exists(combatRoot))
            {
                string[] files = Directory.GetFiles(combatRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    // The two scanners that hold these tokens as string literals are not declarations of
                    // them. Excluded by name rather than by trying to parse strings out of the source: a
                    // token inside a quote is the thing this check is made of, and a cleverer matcher would
                    // be a C# parser written to avoid naming two files.
                    string name = Path.GetFileName(files[i]);
                    if (name == "MavA2AFreezeValidation.cs" || name == "MavCombatBoundaryScan.cs")
                        continue;

                    string text = StripComments(File.ReadAllText(files[i]));
                    for (int k = 0; k < PrematureDeclarations.Length; k++)
                    {
                        if (text.Contains(PrematureDeclarations[k]))
                            found.Add(PrematureDeclarations[k] + " in " + Path.GetFileName(files[i]));
                    }
                }
            }
            Record(found.Count == 0,
                   "F-100", "no missile core, seeker, guidance or track-prediction type was added",
                   report, ref passed, ref failed);
            for (int i = 0; i < found.Count; i++)
                report.AppendLine("        premature declaration: " + found[i]);

            // The migration default is untouched.
            GameObject host = new GameObject("MavFreezeViewHost");
            try
            {
                MavEngagementView view = host.AddComponent<MavEngagementView>();
                Record(!view.preferAuthoritativeLock,
                       "F-101", "preferAuthoritativeLock is still false",
                       report, ref passed, ref failed);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }

            // Issue #16's assumptions: three legacy lock authorities still exist and are still projected.
            // Retiring any of them is a later phase, and the freeze is not a retirement.
            Record(typeof(MonoBehaviour).IsAssignableFrom(typeof(MavF22SensorSuite))
                   && typeof(MonoBehaviour).IsAssignableFrom(typeof(MavTargetingPodSystem))
                   && typeof(MonoBehaviour).IsAssignableFrom(typeof(MavCASTargetingSystem)),
                   "F-102", "all three legacy lock authorities still exist as components",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectSensorSuite(true, 1f, true) == MavLockState.Locked
                   && MavLegacyLockProjection.ProjectSensorSuite(true, 0.5f, false) == MavLockState.Acquiring
                   && MavLegacyLockProjection.ProjectSensorSuite(false, 0f, false) == MavLockState.Idle,
                   "F-102b", "and the legacy projection still maps their states unchanged",
                   report, ref passed, ref failed);
            Record(typeof(MonoBehaviour).IsAssignableFrom(typeof(MavLegacyEngagementProbe)),
                   "F-102c", "with the probe that feeds the migration evidence still present",
                   report, ref passed, ref failed);

            // The F-22 suite is deliberately NOT frozen. It is A2A legacy, kept as the evidence the lock
            // migration is measured against, so it carries no scope guard - and that absence is the
            // classification, asserted rather than assumed.
            string f22 = ReadAsset("MaverickFresh/Scripts/Sensors/MavF22SensorSuite.cs");
            Record(f22.Length > 0 && !f22.Contains("MavCombatScopePolicy"),
                   "F-102d", "the F-22 STT suite is not frozen: it keeps running as legacy A2A evidence",
                   report, ref passed, ref failed);

            // The lead sight was not migrated.
            string lead = ReadAsset("MaverickFresh/Scripts/HUD/MavAirToAirLeadSight.cs");
            Record(lead.Length > 0 && lead.Contains("sensorSuite.selectedTarget"),
                   "F-103", "MavAirToAirLeadSight is untouched: its migration is still deferred",
                   report, ref passed, ref failed);
        }

        // ---- the two roles this phase must state out loud ------------------------------------------
        /// <summary>
        /// Section 6 and section 5 both demand an unambiguous answer rather than an implementation detail:
        /// what the F-22 STT lock IS now, and whether the gun is active or frozen. Both are checked against
        /// the doc, because "explicitly classified" means written down where a reader will find it.
        /// </summary>
        private static void ValidateRolesDocumented(StringBuilder report, ref int passed, ref int failed)
        {
            string doc = ReadAsset(DocPath);
            Record(doc.Length > 0,
                   "F-110", "the freeze document exists",
                   report, ref passed, ref failed);
            if (doc.Length == 0)
                return;

            // F-22 STT: shadow diagnostic, not authority, and not retired.
            Record(doc.Contains("SHADOW") && doc.Contains("MavF22SensorSuite"),
                   "F-111", "the F-22 STT runtime role is stated, and stated as a shadow",
                   report, ref passed, ref failed);
            Record(doc.Contains("NOT the active authority") || doc.Contains("not the active authority"),
                   "F-111b", "explicitly not the active authority",
                   report, ref passed, ref failed);
            Record(doc.Contains("not retired") || doc.Contains("NOT retired"),
                   "F-111c", "and explicitly not retired in this phase",
                   report, ref passed, ref failed);

            // And the runtime matches the claim: STT still runs, and its overlay says SHADOW.
            Record(MavCombatScopePolicy.ShadowLabel == "SHADOW",
                   "F-112", "the shadow label the overlay uses is the one the doc names",
                   report, ref passed, ref failed);
            string overlay = ReadAsset("MaverickFresh/Scripts/Sensors/MavSensorHudOverlay.cs");
            Record(overlay.Contains("MavCombatScopePolicy.ShadowLabel"),
                   "F-112b", "the legacy STT overlay labels itself a shadow rather than a lock",
                   report, ref passed, ref failed);
            Record(overlay.Length > 0 && !overlay.Contains("? \"LOCK\" : \"TGT\""),
                   "F-112c", "and no longer draws a bare LOCK marker",
                   report, ref passed, ref failed);

            // The gun: explicitly classified, and the classification is FROZEN.
            Record(doc.Contains("GUN") || doc.Contains("Gun"),
                   "F-113", "the gun decision is recorded",
                   report, ref passed, ref failed);
            Record(doc.Contains("gun is FROZEN") || doc.Contains("Gun: FROZEN")
                   || doc.Contains("GUN: FROZEN") || doc.Contains("gun is frozen"),
                   "F-113b", "and it is classified as frozen, not active",
                   report, ref passed, ref failed);
            Record(doc.Contains("MavCASWeaponSystem"),
                   "F-113c", "with the ownership that forced that decision named",
                   report, ref passed, ref failed);
        }

        // ---- helpers -------------------------------------------------------------------------------
        private static string ReadAsset(string relativePath)
        {
            string path = Path.Combine(Application.dataPath, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string Between(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            if (end < 0)
                return string.Empty;
            return source.Substring(start, end - start);
        }

        /// <summary>Drops whole-line comments, so a rule about code is not defeated by prose about code.</summary>
        private static string StripComments(string source)
        {
            StringBuilder kept = new StringBuilder();
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("//"))
                    continue;
                kept.AppendLine(lines[i]);
            }
            return kept.ToString();
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }
            return count;
        }

        /// <summary>A feed the test drives, so observation timing is the test's to choose.</summary>
        private sealed class ScriptedFeed : IMavTargetObservationFeed
        {
            public readonly List<MavTrackObservation> pending = new List<MavTrackObservation>();
            public bool IsFeedActive { get { return true; } }
            public MavTrackSource FeedSource { get { return MavTrackSource.Radar; } }

            public int CollectObservations(List<MavTrackObservation> into)
            {
                for (int i = 0; i < pending.Count; i++)
                    into.Add(pending[i]);
                return pending.Count;
            }

            public void Set(int key, Vector3 position, MavTrackQuality quality)
            {
                pending.Clear();
                MavTrackObservation o = new MavTrackObservation();
                o.sourceKey = key;
                o.source = MavTrackSource.Radar;
                o.quality = quality;
                o.position = position;
                o.hasVelocity = true;
                o.velocityMps = new Vector3(0f, 0f, 200f);
                o.displayName = "BANDIT" + key;
                o.team = 2;
                o.isAirTarget = true;
                pending.Add(o);
            }
        }

        private static void Record(bool condition, string id, string description,
                                   StringBuilder report, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                report.AppendLine("  PASS  " + id + "  " + description);
            }
            else
            {
                failed++;
                report.AppendLine("  FAIL  " + id + "  " + description);
            }
        }
    }
}
#endif
