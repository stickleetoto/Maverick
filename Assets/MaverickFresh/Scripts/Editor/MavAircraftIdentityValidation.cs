#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Validation for CANONICAL AIRCRAFT IDENTITY.
    ///
    /// The bug this suite exists to keep dead: selecting the F-16 and getting the F-22. Several
    /// independent paths used to answer "I cannot resolve that aircraft" with the F-22A profile -
    /// the catalog lookup, both visual-factory entry points, both visual-switcher entry points, and
    /// the hangar's slot search. Each of them returned a fully populated, perfectly valid profile,
    /// so no caller could tell it had been handed a different aircraft than the one it asked for.
    /// The F-16 selection ended up flying 16 500 kg, 78 m^2 of wing and thrust vectoring.
    ///
    /// So the checks below come in four groups:
    ///
    ///   [A] RESOLUTION  the catalog resolves each kind to EXACTLY that kind
    ///   [B] SUBSTITUTION  a missing, ambiguous or undefined kind resolves to NOTHING
    ///   [C] IDENTITY    a mis-mapped profile is rejected rather than repaired
    ///   [D] COMPONENT   real GameObjects: a change that cannot complete changes NOTHING
    ///
    /// The component group is the one that matters most, because atomicity is not a property of a
    /// function - it is a property of the order the components are driven in. A pure check cannot
    /// see that physics was reconfigured before the visual was known to exist.
    /// </summary>
    public static class MavAircraftIdentityValidation
    {
        [MenuItem("Maverick/Aircraft/Run Aircraft Identity Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("Maverick CANONICAL AIRCRAFT IDENTITY Validation");
            report.AppendLine("===============================================");

            ValidateCanonicalResolution(report, ref passed, ref failed);
            ValidateNoSubstitution(report, ref passed, ref failed);
            ValidateIdentityConsistency(report, ref passed, ref failed);
            ValidateComponentApplication(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed).Append(" failed=").Append(failed);

            if (failed == 0)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        // ==================================================================== [A] resolution

        private static void ValidateCanonicalResolution(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A] Each kind resolves to exactly itself");

            // F-16 -> F-16, asserted on data that is unmistakably the F-16's and not the F-22's.
            MavAircraftRuntimeProfile f16;
            string error;
            bool okF16 = MavAircraftCatalog.TryGetBuiltIn(MavAircraftKind.F16C, out f16, out error);

            Record(okF16, "F-16C resolves (" + error + ")", report, ref passed, ref failed);

            if (okF16)
            {
                Record(f16.aircraft == MavAircraftKind.F16C,
                    "F-16C profile declares F16C, not " + f16.aircraft,
                    report, ref passed, ref failed);
                Record(f16.aircraftId == "f16c",
                    "F-16C profile carries aircraftId 'f16c' (got '" + f16.aircraftId + "')",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(f16.mass - 9800f) < 0.5f,
                    "F-16C mass is the F-16's 9800 kg, not the F-22's 16500 (got "
                    + f16.mass.ToString("F0") + ")",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(f16.wingArea - 27.9f) < 0.05f,
                    "F-16C wing area is 27.9 m^2, not the F-22's 78.0 (got "
                    + f16.wingArea.ToString("F1") + ")",
                    report, ref passed, ref failed);
                Record(!f16.useThrustVectorControl,
                    "F-16C does NOT get thrust vectoring (an F-22 tell)",
                    report, ref passed, ref failed);
            }

            // F-22 -> F-22. The same lookup must still work for the aircraft that used to be the
            // substitute, or the fix would have traded one wrong answer for another.
            MavAircraftRuntimeProfile f22;
            bool okF22 = MavAircraftCatalog.TryGetBuiltIn(MavAircraftKind.F22A, out f22, out error);

            Record(okF22, "F-22A resolves (" + error + ")", report, ref passed, ref failed);

            if (okF22)
            {
                Record(f22.aircraft == MavAircraftKind.F22A,
                    "F-22A profile declares F22A, not " + f22.aircraft,
                    report, ref passed, ref failed);
                Record(f22.aircraftId == "f22a",
                    "F-22A profile carries aircraftId 'f22a' (got '" + f22.aircraftId + "')",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(f22.mass - 16500f) < 0.5f,
                    "F-22A mass is 16500 kg (got " + f22.mass.ToString("F0") + ")",
                    report, ref passed, ref failed);
                Record(f22.useThrustVectorControl,
                    "F-22A keeps thrust vectoring",
                    report, ref passed, ref failed);
            }

            // And the F-16 and F-22 must be genuinely different objects, so that "it resolved" can
            // never be satisfied by handing back the same profile twice.
            if (okF16 && okF22)
            {
                Record(!ReferenceEquals(f16, f22) && f16.aircraftId != f22.aircraftId,
                    "the F-16 and F-22 profiles are distinct",
                    report, ref passed, ref failed);
            }
        }

        // ==================================================================== [B] no substitution

        private static void ValidateNoSubstitution(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[B] Nothing is ever substituted for a failed lookup");

            // Every declared kind: resolve to itself or fail. Never to something else.
            MavAircraftKind[] kinds = (MavAircraftKind[])System.Enum.GetValues(typeof(MavAircraftKind));
            bool allExact = true;
            string firstWrong = "none";

            for (int i = 0; i < kinds.Length; i++)
            {
                MavAircraftRuntimeProfile profile;
                string error;
                if (!MavAircraftCatalog.TryGetBuiltIn(kinds[i], out profile, out error))
                    continue;

                if (profile == null || profile.aircraft != kinds[i])
                {
                    allExact = false;
                    firstWrong = kinds[i] + " -> "
                                 + (profile == null ? "null" : profile.aircraft.ToString());
                    break;
                }
            }

            Record(allExact,
                "every declared aircraft kind resolves to itself or fails, never to another "
                + "aircraft (first mismatch: " + firstWrong + ")",
                report, ref passed, ref failed);

            // An undefined enum value is the case the old fallback handled by returning the F-22.
            MavAircraftRuntimeProfile bogus;
            string bogusError;
            bool bogusResolved = MavAircraftCatalog.TryGetBuiltIn(
                (MavAircraftKind)9999, out bogus, out bogusError);

            Record(!bogusResolved && bogus == null,
                "an undefined MavAircraftKind is refused rather than resolved to the F-22A",
                report, ref passed, ref failed);
            Record(!string.IsNullOrEmpty(bogusError),
                "and the refusal states why: " + bogusError,
                report, ref passed, ref failed);

            Record(MavAircraftCatalog.CanonicalAircraftId((MavAircraftKind)9999) == null,
                "an undefined kind has no canonical aircraft id",
                report, ref passed, ref failed);

            // The convenience wrapper must fail the same way. It is the one most callers use.
            Record(MavAircraftCatalog.GetBuiltIn((MavAircraftKind)9999) == null,
                "GetBuiltIn returns null for an unresolvable kind instead of a different aircraft",
                report, ref passed, ref failed);

            // Every kind's canonical id is unique, or "the id matches the kind" would be a weaker
            // statement than it looks.
            bool idsUnique = true;
            for (int i = 0; i < kinds.Length && idsUnique; i++)
            {
                for (int j = i + 1; j < kinds.Length; j++)
                {
                    if (MavAircraftCatalog.CanonicalAircraftId(kinds[i])
                        == MavAircraftCatalog.CanonicalAircraftId(kinds[j]))
                    {
                        idsUnique = false;
                        break;
                    }
                }
            }

            Record(idsUnique, "canonical aircraft ids are unique across all kinds",
                report, ref passed, ref failed);

            // ---- the branches that used to fall back -----------------------------------------
            // These need an explicit candidate list: with all five built-ins present, "no profile
            // for this kind" and "two profiles for this kind" are unreachable, so a test against the
            // built-in list cannot distinguish a fail-closed resolver from one that answers F-22A.

            List<MavAircraftRuntimeProfile> withoutF16 = MavAircraftCatalog.CreateBuiltInProfiles();
            for (int i = withoutF16.Count - 1; i >= 0; i--)
            {
                if (withoutF16[i] != null && withoutF16[i].aircraft == MavAircraftKind.F16C)
                    withoutF16.RemoveAt(i);
            }

            MavAircraftRuntimeProfile missing;
            string missingError;
            bool missingResolved = MavAircraftCatalog.TryResolveFrom(
                withoutF16, MavAircraftKind.F16C, out missing, out missingError);

            Record(!missingResolved,
                "a MISSING F-16 profile is refused rather than resolved to another aircraft",
                report, ref passed, ref failed);
            Record(missing == null,
                "and nothing at all is handed back - not the F-22A, not the first profile in the list",
                report, ref passed, ref failed);
            Record(!string.IsNullOrEmpty(missingError),
                "and the refusal says which aircraft could not be resolved: " + missingError,
                report, ref passed, ref failed);

            // The other aircraft must still resolve from that same list, so the check above is
            // testing "the F-16 is gone", not "the list is broken".
            MavAircraftRuntimeProfile stillF22;
            string stillError;
            Record(MavAircraftCatalog.TryResolveFrom(
                       withoutF16, MavAircraftKind.F22A, out stillF22, out stillError)
                   && stillF22 != null && stillF22.aircraft == MavAircraftKind.F22A,
                "the F-22 still resolves from the same list, so the F-16 refusal is specific",
                report, ref passed, ref failed);

            // Two profiles claiming one kind: ambiguous, and ambiguity is not a licence to pick.
            List<MavAircraftRuntimeProfile> duplicated = MavAircraftCatalog.CreateBuiltInProfiles();
            MavAircraftRuntimeProfile impostor = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);
            if (impostor != null)
            {
                impostor.aircraft = MavAircraftKind.F16C;
                duplicated.Add(impostor);

                MavAircraftRuntimeProfile ambiguous;
                string ambiguousError;
                bool ambiguousResolved = MavAircraftCatalog.TryResolveFrom(
                    duplicated, MavAircraftKind.F16C, out ambiguous, out ambiguousError);

                Record(!ambiguousResolved && ambiguous == null,
                    "two profiles claiming F16C is refused as ambiguous, not resolved by picking one",
                    report, ref passed, ref failed);
                Record(!string.IsNullOrEmpty(ambiguousError),
                    "and the ambiguity is reported: " + ambiguousError,
                    report, ref passed, ref failed);
            }

            // A null candidate list must not become "the default aircraft".
            MavAircraftRuntimeProfile fromNull;
            string nullListError;
            Record(!MavAircraftCatalog.TryResolveFrom(
                       null, MavAircraftKind.F16C, out fromNull, out nullListError)
                   && fromNull == null,
                "a null candidate list resolves to nothing rather than to a default aircraft",
                report, ref passed, ref failed);

            // The visual factory refused a null profile rather than building an F-22 placeholder.
            GameObject parent = new GameObject("MavIdentityTest_FactoryParent");
            parent.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                GameObject nullVisual = MavAircraftVisualFactory.CreateDisplayVisual(
                    null, parent.transform, null, false);

                Record(nullVisual == null,
                    "CreateDisplayVisual(null) builds nothing rather than an F-22 placeholder",
                    report, ref passed, ref failed);

                GameObject nullRoot = MavAircraftVisualFactory.CreateFlightAircraftRoot(null, null);
                Record(nullRoot == null,
                    "CreateFlightAircraftRoot(null) builds nothing rather than an F-22 aircraft",
                    report, ref passed, ref failed);

                if (nullRoot != null)
                    Object.DestroyImmediate(nullRoot);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // ==================================================================== [C] identity checks

        private static void ValidateIdentityConsistency(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C] A mis-mapped identity is rejected, not repaired");

            string error;

            Record(!MavAircraftCatalog.IsIdentityConsistent(null, MavAircraftKind.F16C, out error),
                "a null profile is not a valid F-16 identity",
                report, ref passed, ref failed);

            // A profile whose enum and id disagree. This is the "enum/mapping is wrong" case: the
            // data is complete and plausible, and only the cross-check catches it.
            MavAircraftRuntimeProfile mismapped = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(mismapped != null, "test setup: the F-16 profile is available to corrupt",
                report, ref passed, ref failed);

            if (mismapped != null)
            {
                mismapped.aircraftId = "f22a";

                Record(!MavAircraftCatalog.IsIdentityConsistent(
                           mismapped, MavAircraftKind.F16C, out error),
                    "an F16C profile carrying the F-22's aircraftId is rejected: " + error,
                    report, ref passed, ref failed);

                // And it must not be accepted as the F-22 either, on the strength of its id alone.
                Record(!MavAircraftCatalog.IsIdentityConsistent(
                           mismapped, MavAircraftKind.F22A, out error),
                    "and it is not accepted as the F-22 either, because its kind still says F16C",
                    report, ref passed, ref failed);
            }

            // The reverse: right id, wrong declared kind.
            MavAircraftRuntimeProfile wrongKind = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            if (wrongKind != null)
            {
                wrongKind.aircraft = MavAircraftKind.F22A;

                Record(!MavAircraftCatalog.IsIdentityConsistent(
                           wrongKind, MavAircraftKind.F22A, out error),
                    "a profile claiming F22A while carrying aircraftId 'f16c' is rejected: " + error,
                    report, ref passed, ref failed);
            }

            // A correctly mapped profile still passes, or the check would be useless.
            MavAircraftRuntimeProfile good = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(good != null
                   && MavAircraftCatalog.IsIdentityConsistent(good, MavAircraftKind.F16C, out error),
                "a correctly mapped F-16 profile is accepted",
                report, ref passed, ref failed);
        }

        // ==================================================================== [D] component level

        private static void ValidateComponentApplication(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D] Real components: applied exactly, or not at all");

            // ---------------------------------------------------------------- D1: F-16 -> F-16
            MavIdentityRig rig = MavIdentityRig.Build(allowPlaceholderVisuals: true);
            try
            {
                string error;
                bool applied = rig.applier.TryApplyAircraft(MavAircraftKind.F16C, out error);

                Record(applied, "F-16 selection applies (" + error + ")",
                    report, ref passed, ref failed);
                Record(rig.applier.aircraft == MavAircraftKind.F16C,
                    "the applier reports F16C (got " + rig.applier.aircraft + ")",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(rig.rigidbody.mass - 9800f) < 0.5f,
                    "the Rigidbody carries F-16 mass 9800 kg, not F-22 16500 (got "
                    + rig.rigidbody.mass.ToString("F0") + ")",
                    report, ref passed, ref failed);
                Record(rig.visualSwitcher.activeAircraft == MavAircraftKind.F16C,
                    "the visual switcher shows F16C (got " + rig.visualSwitcher.activeAircraft + ")",
                    report, ref passed, ref failed);
                Record(rig.visualSwitcher.activeVisual != null
                       && rig.visualSwitcher.activeVisual.activeSelf,
                    "and an F-16 visual is actually active",
                    report, ref passed, ref failed);
                Record(rig.applier.aircraft == rig.visualSwitcher.activeAircraft,
                    "physics identity and visual identity agree",
                    report, ref passed, ref failed);

                // ------------------------------------------------------------ D2: F-22 -> F-22
                applied = rig.applier.TryApplyAircraft(MavAircraftKind.F22A, out error);

                Record(applied, "F-22 selection applies on the same rig (" + error + ")",
                    report, ref passed, ref failed);
                Record(rig.applier.aircraft == MavAircraftKind.F22A
                       && Mathf.Abs(rig.rigidbody.mass - 16500f) < 0.5f,
                    "switching to the F-22 gives F-22 mass 16500 kg (got "
                    + rig.rigidbody.mass.ToString("F0") + ")",
                    report, ref passed, ref failed);
                Record(rig.visualSwitcher.activeAircraft == MavAircraftKind.F22A,
                    "and the visual switcher follows to F22A",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            // ---------------------------------------------------------------- D3: missing visual
            // The F-16 physics profile exists, but no F-16 visual can be produced. The whole change
            // must be refused: this is the atomicity claim, and it is the one a pure test cannot make.
            MavIdentityRig strict = MavIdentityRig.Build(allowPlaceholderVisuals: false);
            try
            {
                // Establish a known prior aircraft to be preserved.
                string error;
                strict.visualSwitcher.createPlaceholderIfMissing = true;
                bool prior = strict.applier.TryApplyAircraft(MavAircraftKind.F22A, out error);
                Record(prior, "test setup: the rig starts as a working F-22 (" + error + ")",
                    report, ref passed, ref failed);

                float massBefore = strict.rigidbody.mass;
                MavAircraftKind applierBefore = strict.applier.aircraft;
                MavAircraftKind visualBefore = strict.visualSwitcher.activeAircraft;
                GameObject visualObjectBefore = strict.visualSwitcher.activeVisual;

                // Now make an F-16 visual genuinely unobtainable.
                strict.visualSwitcher.createPlaceholderIfMissing = false;
                strict.visualSwitcher.autoFindSceneVisuals = false;
                strict.visualSwitcher.preclaimAllResolvedVisuals = false;
                strict.visualSwitcher.RemoveRuntimeVisual(MavAircraftKind.F16C);
                strict.visualSwitcher.SetSource(MavAircraftKind.F16C, null);

                bool refused = !strict.applier.TryApplyAircraft(MavAircraftKind.F16C, out error);

                Record(refused,
                    "an F-16 with no obtainable visual is REFUSED rather than half-applied",
                    report, ref passed, ref failed);
                Record(!string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(strict.applier.lastApplyError),
                    "and the refusal is reported: " + error,
                    report, ref passed, ref failed);

                // The four ways the old code could have leaked a half-change.
                Record(Mathf.Abs(strict.rigidbody.mass - massBefore) < 0.001f,
                    "the Rigidbody mass is untouched (" + strict.rigidbody.mass.ToString("F0")
                    + " kg, was " + massBefore.ToString("F0") + ")",
                    report, ref passed, ref failed);
                Record(strict.applier.aircraft == applierBefore,
                    "the applier still reports the previous aircraft " + applierBefore,
                    report, ref passed, ref failed);
                Record(strict.visualSwitcher.activeAircraft == visualBefore,
                    "the visual switcher still reports " + visualBefore
                    + " - it does NOT claim the aircraft it failed to show",
                    report, ref passed, ref failed);
                Record(strict.visualSwitcher.activeVisual == visualObjectBefore
                       && visualObjectBefore != null && visualObjectBefore.activeSelf,
                    "and the previous aircraft's model is still the one on screen",
                    report, ref passed, ref failed);

                // A refused change must also not have produced an F-22 anywhere.
                Record(strict.applier.aircraft != MavAircraftKind.F16C,
                    "the refused F-16 was not recorded as applied",
                    report, ref passed, ref failed);
            }
            finally
            {
                strict.Destroy();
            }

            // ---------------------------------------------------------------- D4: preclaim + refusal
            // Preparation deactivates every visual before it can discover that the requested one is
            // unobtainable. A refusal therefore has to put the previous model back on screen - and
            // this is the case the D3 rig cannot see, because it runs with pre-claiming off.
            MavIdentityRig preclaim = MavIdentityRig.Build(allowPlaceholderVisuals: true);
            try
            {
                string error;
                preclaim.visualSwitcher.preclaimAllResolvedVisuals = true;

                bool prior = preclaim.applier.TryApplyAircraft(MavAircraftKind.F22A, out error);
                Record(prior, "test setup: pre-claiming rig starts as a working F-22 (" + error + ")",
                    report, ref passed, ref failed);

                GameObject shownBefore = preclaim.visualSwitcher.activeVisual;
                Record(shownBefore != null && shownBefore.activeSelf,
                    "test setup: an F-22 visual is on screen",
                    report, ref passed, ref failed);

                // Make the F-16 unobtainable while leaving pre-claiming on.
                preclaim.visualSwitcher.createPlaceholderIfMissing = false;
                preclaim.visualSwitcher.RemoveRuntimeVisual(MavAircraftKind.F16C);
                preclaim.visualSwitcher.SetSource(MavAircraftKind.F16C, null);

                bool refused = !preclaim.applier.TryApplyAircraft(MavAircraftKind.F16C, out error);

                Record(refused, "the F-16 is still refused with pre-claiming enabled",
                    report, ref passed, ref failed);
                Record(preclaim.visualSwitcher.activeVisual == shownBefore
                       && shownBefore != null && shownBefore.activeSelf,
                    "and the previous F-22 model is put back on screen rather than left hidden by "
                    + "the pre-claim pass",
                    report, ref passed, ref failed);
                Record(preclaim.visualSwitcher.activeAircraft == MavAircraftKind.F22A
                       && preclaim.applier.aircraft == MavAircraftKind.F22A,
                    "with both halves still reporting the F-22",
                    report, ref passed, ref failed);
            }
            finally
            {
                preclaim.Destroy();
            }

            // ---------------------------------------------------------------- D5: bad identity
            MavIdentityRig bad = MavIdentityRig.Build(allowPlaceholderVisuals: true);
            try
            {
                string error;
                bool prior = bad.applier.TryApplyAircraft(MavAircraftKind.F22A, out error);
                Record(prior, "test setup: rig starts as a working F-22",
                    report, ref passed, ref failed);

                float massBefore = bad.rigidbody.mass;

                // An undefined kind, and then a mis-mapped profile: both must change nothing.
                bool refusedUndefined = !bad.applier.TryApplyAircraft((MavAircraftKind)9999, out error);
                Record(refusedUndefined && Mathf.Abs(bad.rigidbody.mass - massBefore) < 0.001f
                       && bad.applier.aircraft == MavAircraftKind.F22A,
                    "an undefined aircraft kind changes nothing and does not fall back to the F-22 "
                    + "as if it had succeeded",
                    report, ref passed, ref failed);

                MavAircraftRuntimeProfile mismapped =
                    MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
                if (mismapped != null)
                {
                    mismapped.aircraftId = "f22a";
                    bool refusedMismap = !bad.applier.TryApplyProfile(mismapped, out error);

                    Record(refusedMismap,
                        "a mis-mapped F-16 profile is refused: " + error,
                        report, ref passed, ref failed);
                    Record(Mathf.Abs(bad.rigidbody.mass - massBefore) < 0.001f
                           && bad.applier.aircraft == MavAircraftKind.F22A,
                        "and it changed neither physics nor the recorded identity",
                        report, ref passed, ref failed);
                }

                bool refusedNull = !bad.applier.TryApplyProfile(null, out error);
                Record(refusedNull && bad.applier.aircraft == MavAircraftKind.F22A,
                    "a null profile is refused loudly instead of returning silently",
                    report, ref passed, ref failed);
            }
            finally
            {
                bad.Destroy();
            }
        }

        // ==================================================================== rig

        /// <summary>
        /// A real player-shaped GameObject: Rigidbody, profile applier, visual switcher.
        ///
        /// Deliberately minimal, and deliberately isolated - allowGlobalRigLookup and scene visual
        /// discovery are off, so a validation run cannot reach into whatever scene happens to be
        /// open, and HideAndDontSave keeps it out of the scene file.
        /// </summary>
        private sealed class MavIdentityRig
        {
            public GameObject gameObject;
            public Rigidbody rigidbody;
            public MavAircraftProfileApplier applier;
            public MavAircraftVisualSwitcher visualSwitcher;

            public static MavIdentityRig Build(bool allowPlaceholderVisuals)
            {
                MavIdentityRig rig = new MavIdentityRig();

                rig.gameObject = new GameObject("MavIdentityTestRig");
                rig.gameObject.hideFlags = HideFlags.HideAndDontSave;

                rig.rigidbody = rig.gameObject.AddComponent<Rigidbody>();
                rig.rigidbody.useGravity = false;

                rig.applier = rig.gameObject.AddComponent<MavAircraftProfileApplier>();
                rig.applier.applyOnStart = false;
                rig.applier.renameObject = false;
                rig.applier.allowGlobalRigLookup = false;

                rig.visualSwitcher = rig.gameObject.AddComponent<MavAircraftVisualSwitcher>();
                rig.visualSwitcher.autoFindSceneVisuals = false;
                rig.visualSwitcher.claimLooseSceneVisuals = false;
                rig.visualSwitcher.preclaimAllResolvedVisuals = false;
                rig.visualSwitcher.createPlaceholderIfMissing = allowPlaceholderVisuals;

                return rig;
            }

            public void Destroy()
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

                gameObject = null;
            }
        }

        private static void Record(bool ok, string name, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok)
            {
                passed++;
                report.Append("  PASS  ").AppendLine(name);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").AppendLine(name);
            }
        }
    }
}
#endif
