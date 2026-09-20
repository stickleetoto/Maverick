#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MaverickFresh.Combat.Targeting;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    /// <summary>
    /// Deterministic validation for Engagement Target Contract R0: the vocabulary that keeps a track lock,
    /// a track designation and a bare world-point designation three different things.
    ///
    /// THESE ARE CONTRACT TESTS, NOT BEHAVIOR TESTS. Nothing here locks, designates or authorizes
    /// anything, because nothing in this phase does. What is asserted is the SHAPE of the vocabulary and,
    /// just as importantly, what it refuses to carry: no object reference, no implied lock, no implied
    /// authorization, and no ability to mint a track for a point.
    ///
    /// Much of it is reflection over the type, which is unusual and deliberate. "A track reference carries
    /// only a track id" is a claim about the absence of members and fields; exercising the type cannot
    /// prove an absence, so the absence is checked where it is actually decided. The same applies to the
    /// tripwire on <see cref="IMavTrackLockAuthority"/>: the rule that it stays track-only is a rule about
    /// what may never appear on it, and the cheapest honest way to hold that line is to pin its members.
    /// </summary>
    public static class MavEngagementTargetContractValidation
    {
        private const string ContractPath = "MaverickFresh/Scripts/Combat/Core/MavEngagementTargetContract.cs";
        private const string LockContractPath = "MaverickFresh/Scripts/Combat/Core/MavTrackLockContract.cs";

        /// <summary>
        /// Words that would turn a reference into a verdict. A target reference says WHAT is referenced;
        /// whether it is locked, designated or cleared to fire belongs to three separate authorities, and
        /// a member here with one of these names would be read as answering for them.
        /// </summary>
        private static readonly string[] ForbiddenVerdictTokens =
        {
            "Lock",
            "Authoriz",
            "Fire",
            "Launch",
            "Weapon",
            "Designat",
            "Commit",
            "Engage",
            "Valid",
            "Quality",
        };

        /// <summary>
        /// Words that would mean the lock authority had stopped being track-only. A lock is a maintained
        /// commitment to something being tracked; a bare coordinate cannot be locked, so any of these
        /// appearing on that interface means the two concepts have been merged after all.
        /// </summary>
        private static readonly string[] ForbiddenLockAuthorityTokens =
        {
            "Point",
            "World",
            "Designat",
            "Position",
            "Cas",
            "CAS",
            "Pod",
        };

        [MenuItem("Maverick/Combat/Run Engagement Target Contract Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Engagement Target Contract Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick Engagement Target Contract Validation R0");
            report.AppendLine("===============================================");

            ValidateTrackRef(report, ref passed, ref failed);
            ValidatePointRef(report, ref passed, ref failed);
            ValidateNoneRef(report, ref passed, ref failed);
            ValidateTrackIsNotPoint(report, ref passed, ref failed);
            ValidateNoObjectOwnership(report, ref passed, ref failed);
            ValidateNoVerdictsImplied(report, ref passed, ref failed);
            ValidateNoFakeTrackCreation(report, ref passed, ref failed);
            ValidateLockAuthorityStaysTrackOnly(report, ref passed, ref failed);

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---- a track reference carries a track id and nothing else ----------------------------------
        private static void ValidateTrackRef(StringBuilder report, ref int passed, ref int failed)
        {
            MavEngagementTargetRef r = MavEngagementTargetRef.FromTrack(7);

            Record(r.Kind == MavEngagementTargetKind.Track,
                   "E-001", "a track reference reports itself as a track",
                   report, ref passed, ref failed);
            Record(r.IsTrack && !r.IsWorldPoint && !r.IsNone,
                   "E-001b", "and as neither a world point nor nothing",
                   report, ref passed, ref failed);
            Record(r.TrackId == 7,
                   "E-002", "it carries the track id it was made from",
                   report, ref passed, ref failed);

            int id;
            Record(r.TryGetTrackId(out id) && id == 7,
                   "E-002b", "and hands it over through the checked accessor",
                   report, ref passed, ref failed);

            // The half that matters: it carries NO position.
            Record(r.WorldPoint == Vector3.zero,
                   "E-003", "a track reference exposes no world position",
                   report, ref passed, ref failed);
            Vector3 point;
            Record(!r.TryGetWorldPoint(out point) && point == Vector3.zero,
                   "E-003b", "and the checked point accessor refuses rather than inventing one",
                   report, ref passed, ref failed);

            // Track identity stays a track id: no other identity is representable.
            Record(MavEngagementTargetRef.FromTrack(7) == MavEngagementTargetRef.FromTrack(7),
                   "E-004", "two references to the same track are equal",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.FromTrack(7) != MavEngagementTargetRef.FromTrack(8),
                   "E-004b", "and references to different tracks are not",
                   report, ref passed, ref failed);

            // Id 0 is how the whole combat path says "no track", so a Track case cannot carry it.
            Record(MavEngagementTargetRef.FromTrack(0).IsNone,
                   "E-005", "track id 0 is not a track, it is nothing",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.FromTrack(-3).IsNone,
                   "E-005b", "and neither is a negative id",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.FromTrack(0).Kind == MavEngagementTargetKind.None,
                   "E-005c", "so there is only one way to express nothing",
                   report, ref passed, ref failed);
        }

        // ---- a point reference carries a position and nothing else ----------------------------------
        private static void ValidatePointRef(StringBuilder report, ref int passed, ref int failed)
        {
            Vector3 designated = new Vector3(1200.5f, 45.25f, -880f);
            MavEngagementTargetRef r = MavEngagementTargetRef.FromWorldPoint(designated);

            Record(r.Kind == MavEngagementTargetKind.WorldPoint,
                   "E-010", "a world-point reference reports itself as a world point",
                   report, ref passed, ref failed);
            Record(r.IsWorldPoint && !r.IsTrack && !r.IsNone,
                   "E-010b", "and as neither a track nor nothing",
                   report, ref passed, ref failed);
            Record(r.WorldPoint == designated,
                   "E-011", "it carries the exact point it was made from",
                   report, ref passed, ref failed);

            Vector3 point;
            Record(r.TryGetWorldPoint(out point) && point == designated,
                   "E-011b", "and hands it over through the checked accessor",
                   report, ref passed, ref failed);

            // The half that matters: a designated point is NOT a track and has no id.
            Record(r.TrackId == 0,
                   "E-012", "a world-point reference exposes no track id",
                   report, ref passed, ref failed);
            int id;
            Record(!r.TryGetTrackId(out id) && id == 0,
                   "E-012b", "and the checked id accessor refuses rather than inventing one",
                   report, ref passed, ref failed);

            // The origin is a real place. There is no in-band "no point" value.
            MavEngagementTargetRef atOrigin = MavEngagementTargetRef.FromWorldPoint(Vector3.zero);
            Record(atOrigin.IsWorldPoint && !atOrigin.IsNone,
                   "E-013", "a designation at the origin is a designation, not an absence",
                   report, ref passed, ref failed);
            Record(atOrigin != MavEngagementTargetRef.None,
                   "E-013b", "and is distinguishable from no reference at all",
                   report, ref passed, ref failed);

            Record(MavEngagementTargetRef.FromWorldPoint(designated)
                   == MavEngagementTargetRef.FromWorldPoint(designated),
                   "E-014", "two references to the same point are equal",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.FromWorldPoint(designated)
                   != MavEngagementTargetRef.FromWorldPoint(designated + new Vector3(0.5f, 0f, 0f)),
                   "E-014b", "and references to different points are not, without a tolerance",
                   report, ref passed, ref failed);
        }

        // ---- nothing carries neither ---------------------------------------------------------------
        private static void ValidateNoneRef(StringBuilder report, ref int passed, ref int failed)
        {
            MavEngagementTargetRef none = MavEngagementTargetRef.None;

            Record(none.Kind == MavEngagementTargetKind.None && none.IsNone,
                   "E-020", "None reports itself as nothing",
                   report, ref passed, ref failed);
            Record(none.TrackId == 0 && none.WorldPoint == Vector3.zero,
                   "E-021", "None carries neither a track id nor a position",
                   report, ref passed, ref failed);

            int id;
            Vector3 point;
            Record(!none.TryGetTrackId(out id) && !none.TryGetWorldPoint(out point),
                   "E-021b", "and both checked accessors refuse",
                   report, ref passed, ref failed);

            // An unset field must be the safe case, not an invalid one.
            MavEngagementTargetRef unset = default(MavEngagementTargetRef);
            Record(unset.IsNone && unset == MavEngagementTargetRef.None,
                   "E-022", "an unset reference is None, so a default field cannot look valid",
                   report, ref passed, ref failed);
            Record((int)MavEngagementTargetKind.None == 0,
                   "E-022b", "which holds because None is the zero of the enum",
                   report, ref passed, ref failed);
            Record(none != MavEngagementTargetRef.FromTrack(1)
                   && none != MavEngagementTargetRef.FromWorldPoint(Vector3.one),
                   "E-023", "None is equal to neither of the other two cases",
                   report, ref passed, ref failed);
        }

        // ---- a track is not a point ----------------------------------------------------------------
        /// <summary>
        /// The confusion this vocabulary exists to prevent, asserted directly: a track id and a coordinate
        /// are different kinds of thing, and no arrangement of their payloads may make them compare equal.
        /// </summary>
        private static void ValidateTrackIsNotPoint(StringBuilder report, ref int passed, ref int failed)
        {
            MavEngagementTargetRef track = MavEngagementTargetRef.FromTrack(5);
            MavEngagementTargetRef point = MavEngagementTargetRef.FromWorldPoint(new Vector3(5f, 0f, 0f));

            Record(track.Kind != point.Kind,
                   "E-030", "a track and a world point are different kinds",
                   report, ref passed, ref failed);
            Record(track != point && !track.Equals(point),
                   "E-031", "and a track reference never equals a point reference",
                   report, ref passed, ref failed);
            Record(!(track.IsTrack && track.IsWorldPoint),
                   "E-031b", "no reference is both at once",
                   report, ref passed, ref failed);

            // The specific trap: "track 5" and "the point at (5,0,0)" must not collide, including in a
            // dictionary, where a hash collision plus a sloppy Equals would silently merge them.
            Dictionary<MavEngagementTargetRef, string> byRef = new Dictionary<MavEngagementTargetRef, string>();
            byRef[track] = "track";
            byRef[point] = "point";
            Record(byRef.Count == 2 && byRef[track] == "track" && byRef[point] == "point",
                   "E-032", "the two are distinct keys, so a lookup cannot confuse them",
                   report, ref passed, ref failed);

            // Kind participates in the hash, which is what keeps that true in general.
            Record(MavEngagementTargetRef.FromTrack(5).GetHashCode()
                   == MavEngagementTargetRef.FromTrack(5).GetHashCode(),
                   "E-033", "equal references hash equally",
                   report, ref passed, ref failed);
            Record(MavEngagementTargetRef.None.GetHashCode()
                   != MavEngagementTargetRef.FromWorldPoint(Vector3.zero).GetHashCode(),
                   "E-033b", "and None does not hash like a designation at the origin",
                   report, ref passed, ref failed);

            // Converting between them is not offered. There is no FromTrack(point) or ToTrack().
            MethodInfo[] methods = typeof(MavEngagementTargetRef).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            bool conversionOffered = false;
            for (int i = 0; i < methods.Length; i++)
            {
                string n = methods[i].Name;
                if (n == "ToTrack" || n == "AsTrack" || n == "ToWorldPoint" || n == "AsWorldPoint"
                    || n == "op_Implicit" || n == "op_Explicit")
                    conversionOffered = true;
            }
            Record(!conversionOffered,
                   "E-034", "no conversion between the two kinds is offered, implicitly or explicitly",
                   report, ref passed, ref failed);
        }

        // ---- no object ownership, and no way to mutate a reference ----------------------------------
        /// <summary>
        /// A reference that held the GameObject would be reading truth no authority claimed - the exact
        /// shortcut the scene-searching legacy code takes, and the reason <c>MavTargetTrackData</c> holds
        /// an id instead of an object. Checked by reflection because it is a claim about what the type
        /// does not contain.
        /// </summary>
        private static void ValidateNoObjectOwnership(StringBuilder report, ref int passed, ref int failed)
        {
            Type t = typeof(MavEngagementTargetRef);

            Record(t.IsValueType,
                   "E-040", "the reference is a value type, so there is no shared instance to mutate",
                   report, ref passed, ref failed);

            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool allPrivate = true;
            bool allReadonly = true;
            bool anyObjectTyped = false;
            List<string> offenders = new List<string>();

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (!f.IsPrivate) { allPrivate = false; offenders.Add("public field " + f.Name); }
                if (!f.IsInitOnly) { allReadonly = false; offenders.Add("writable field " + f.Name); }
                if (HoldsAnObject(f.FieldType)) { anyObjectTyped = true; offenders.Add("object field " + f.Name + " : " + f.FieldType.Name); }
            }

            Record(fields.Length == 3,
                   "E-041", "the reference has exactly three fields: kind, track id, point",
                   report, ref passed, ref failed);
            Record(allPrivate,
                   "E-041b", "all of them private, so the payload is reached only through the accessors",
                   report, ref passed, ref failed);
            Record(allReadonly,
                   "E-042", "all of them readonly, so a handed-over reference cannot be edited",
                   report, ref passed, ref failed);
            Record(!anyObjectTyped,
                   "E-043", "none of them is a GameObject, Transform, Component or any UnityEngine.Object",
                   report, ref passed, ref failed);

            PropertyInfo[] props = t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            bool anySetter = false;
            bool anyObjectProperty = false;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].CanWrite) { anySetter = true; offenders.Add("settable property " + props[i].Name); }
                if (HoldsAnObject(props[i].PropertyType)) { anyObjectProperty = true; offenders.Add("object property " + props[i].Name); }
            }
            Record(!anySetter,
                   "E-044", "no property can be written",
                   report, ref passed, ref failed);
            Record(!anyObjectProperty,
                   "E-044b", "and no property hands out a scene object",
                   report, ref passed, ref failed);

            // The readonly-struct modifier itself, when the compiler recorded it.
            object[] attrs = t.GetCustomAttributes(false);
            bool readonlyStruct = false;
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i].GetType().Name == "IsReadOnlyAttribute")
                    readonlyStruct = true;
            }
            Record(readonlyStruct,
                   "E-045", "and the type is declared a readonly struct",
                   report, ref passed, ref failed);

            for (int i = 0; i < offenders.Count; i++)
                report.AppendLine("        offender: " + offenders[i]);

            // The source says so too, so a future field cannot be added without the word appearing.
            string source = ReadAsset(ContractPath);
            Record(source.Length > 0,
                   "E-046", "the contract source can be read",
                   report, ref passed, ref failed);
            if (source.Length > 0)
            {
                string code = StripComments(source);
                Record(!code.Contains("GameObject") && !code.Contains("Transform")
                       && !code.Contains("Component") && !code.Contains("MonoBehaviour"),
                       "E-046b", "and mentions no scene-object type in its code",
                       report, ref passed, ref failed);
            }
        }

        // ---- a reference is not a verdict ----------------------------------------------------------
        /// <summary>
        /// A TrackRef does not imply Locked. A WorldPoint does not imply a Track. A designation does not
        /// imply weapon authorization. All three are the same rule: this type says what is referenced and
        /// never whether anything is permitted.
        /// </summary>
        private static void ValidateNoVerdictsImplied(StringBuilder report, ref int passed, ref int failed)
        {
            Type t = typeof(MavEngagementTargetRef);
            MemberInfo[] members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            List<string> verdicts = new List<string>();
            for (int i = 0; i < members.Length; i++)
            {
                string name = members[i].Name;
                for (int k = 0; k < ForbiddenVerdictTokens.Length; k++)
                {
                    if (name.IndexOf(ForbiddenVerdictTokens[k], StringComparison.Ordinal) >= 0)
                        verdicts.Add(name + " (" + ForbiddenVerdictTokens[k] + ")");
                }
            }

            Record(verdicts.Count == 0,
                   "E-050", "no member of the reference answers a lock, designation or firing question",
                   report, ref passed, ref failed);
            for (int i = 0; i < verdicts.Count; i++)
                report.AppendLine("        verdict-shaped member: " + verdicts[i]);

            // Nor does it carry a lifecycle state, which would amount to the same thing.
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            bool carriesLifecycle = false;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == typeof(MavLockState)
                    || fields[i].FieldType == typeof(MavLockLossReason)
                    || fields[i].FieldType == typeof(MavTrackQuality))
                    carriesLifecycle = true;
            }
            Record(!carriesLifecycle,
                   "E-051", "and it carries no lock state, loss reason or quality",
                   report, ref passed, ref failed);

            // A track reference for a track that is locked, and one for a track that is not, are the same
            // reference. That is the point: the reference is not where lock lives.
            Record(MavEngagementTargetRef.FromTrack(9) == MavEngagementTargetRef.FromTrack(9),
                   "E-052", "a reference to a track is identical whether or not that track is locked",
                   report, ref passed, ref failed);
            Record(!typeof(IMavTrackLockAuthority).IsAssignableFrom(t),
                   "E-053", "the reference is not itself a lock authority",
                   report, ref passed, ref failed);

            string code = StripComments(ReadAsset(ContractPath));
            Record(code.Length > 0 && !code.Contains("interface "),
                   "E-054", "and this phase added a vocabulary, not an authority to go with it",
                   report, ref passed, ref failed);
        }

        // ---- a point designation must not become a track --------------------------------------------
        /// <summary>
        /// The shortcut this whole phase exists to refuse: minting a track for a designated point so that
        /// everything can go through one authority. Asserted against the real track owner - building
        /// references must leave it holding exactly what it held.
        /// </summary>
        private static void ValidateNoFakeTrackCreation(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavEngagementTargetContractHost");
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();
                owner.trackDropSeconds = 600f;
                int before = owner.TrackCount;

                Record(before == 0,
                       "E-060", "a fresh track owner holds no tracks",
                       report, ref passed, ref failed);

                MavEngagementTargetRef point = MavEngagementTargetRef.FromWorldPoint(new Vector3(300f, 0f, 900f));
                MavEngagementTargetRef track = MavEngagementTargetRef.FromTrack(4242);
                MavEngagementTargetRef none = MavEngagementTargetRef.None;

                Record(owner.TrackCount == before,
                       "E-061", "designating a bare point creates no track",
                       report, ref passed, ref failed);
                Record(point.IsWorldPoint && point.TrackId == 0,
                       "E-061b", "the point stays a point, with no id invented for it",
                       report, ref passed, ref failed);

                // A reference to a track id the owner has never heard of is still just an id. It does not
                // conjure the track, and resolving it is the owner's business, not the reference's.
                MavTargetTrackData resolved;
                Record(!owner.TryGetTrackById(track.TrackId, out resolved),
                       "E-062", "referencing an unknown track id does not make the owner know it",
                       report, ref passed, ref failed);
                Record(owner.TrackCount == before,
                       "E-062b", "and does not add anything to the owner",
                       report, ref passed, ref failed);
                Record(none.IsNone && owner.TrackCount == before,
                       "E-063", "nor does building a None reference",
                       report, ref passed, ref failed);

                // The contract cannot do it even in principle: it names nothing that could.
                string code = StripComments(ReadAsset(ContractPath));
                Record(code.Length > 0
                       && !code.Contains("MavTargetTrackOwner")
                       && !code.Contains("MavTrackObservation")
                       && !code.Contains("MavTargetTrackData"),
                       "E-064", "and the contract references no track-producing type at all",
                       report, ref passed, ref failed);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        // ---- the lock authority stays track-only ---------------------------------------------------
        /// <summary>
        /// The architectural rule of this phase, held where it can actually be broken. The pressure to add
        /// a <c>worldPoint</c> to <see cref="IMavTrackLockAuthority"/> is exactly what produced this
        /// vocabulary instead, and the pressure does not go away - so the interface's surface is pinned.
        ///
        /// Both halves are needed: the forbidden-token scan catches a member named for a point, and the
        /// exact member list catches one that is not.
        /// </summary>
        private static void ValidateLockAuthorityStaysTrackOnly(StringBuilder report, ref int passed, ref int failed)
        {
            Type t = typeof(IMavTrackLockAuthority);

            List<string> propertyNames = new List<string>();
            PropertyInfo[] props = t.GetProperties();
            for (int i = 0; i < props.Length; i++)
                propertyNames.Add(props[i].Name);
            propertyNames.Sort();

            List<string> methodNames = new List<string>();
            MethodInfo[] methods = t.GetMethods();
            for (int i = 0; i < methods.Length; i++)
            {
                if (!methods[i].IsSpecialName)   // skip property getters
                    methodNames.Add(methods[i].Name);
            }
            methodNames.Sort();

            string properties = string.Join(",", propertyNames.ToArray());
            string calls = string.Join(",", methodNames.ToArray());

            Record(properties == "AcquisitionProgress01,IsLockAuthorityActive,LastLossReason,LockState,TimeInLockSeconds",
                   "E-070", "the lock authority still exposes exactly its five track-lock properties",
                   report, ref passed, ref failed);
            Record(calls == "TryGetLockedTrack",
                   "E-071", "and exactly one call, which answers with a track",
                   report, ref passed, ref failed);

            if (properties != "AcquisitionProgress01,IsLockAuthorityActive,LastLossReason,LockState,TimeInLockSeconds")
                report.AppendLine("        actual properties: " + properties);
            if (calls != "TryGetLockedTrack")
                report.AppendLine("        actual calls: " + calls);

            // No member named for a place, a designation or a legacy owner.
            List<string> leaks = new List<string>();
            MemberInfo[] members = t.GetMembers();
            for (int i = 0; i < members.Length; i++)
            {
                for (int k = 0; k < ForbiddenLockAuthorityTokens.Length; k++)
                {
                    if (members[i].Name.IndexOf(ForbiddenLockAuthorityTokens[k], StringComparison.Ordinal) >= 0)
                        leaks.Add(members[i].Name + " (" + ForbiddenLockAuthorityTokens[k] + ")");
                }
            }
            Record(leaks.Count == 0,
                   "E-072", "no member of the lock authority names a point, a designation or a legacy owner",
                   report, ref passed, ref failed);
            for (int i = 0; i < leaks.Count; i++)
                report.AppendLine("        leaked member: " + leaks[i]);

            // The one call still answers with a track, not a target reference - migrating it to this
            // vocabulary would be a behavior change, and this phase migrates nothing.
            MethodInfo tryGet = t.GetMethod("TryGetLockedTrack");
            Record(tryGet != null && tryGet.ReturnType == typeof(bool),
                   "E-073", "TryGetLockedTrack still returns bool",
                   report, ref passed, ref failed);
            if (tryGet != null)
            {
                ParameterInfo[] ps = tryGet.GetParameters();
                Record(ps.Length == 1 && ps[0].ParameterType == typeof(MavTargetTrackData).MakeByRefType(),
                       "E-073b", "and still answers with a track, unchanged by this phase",
                       report, ref passed, ref failed);
            }
            else
            {
                Record(false, "E-073b", "and still answers with a track, unchanged by this phase",
                       report, ref passed, ref failed);
            }

            // And the interface source has not gained the new vocabulary either.
            string lockCode = StripComments(ReadAsset(LockContractPath));
            Record(lockCode.Length > 0 && !lockCode.Contains("MavEngagementTargetRef"),
                   "E-074", "the lock contract does not reference the new vocabulary at all",
                   report, ref passed, ref failed);
        }

        /// <summary>Whether a type would let a reference hold a live scene object.</summary>
        private static bool HoldsAnObject(Type t)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(t)
                   || t == typeof(GameObject)
                   || t == typeof(Transform)
                   || t == typeof(Component);
        }

        private static string ReadAsset(string relativePath)
        {
            string path = Path.Combine(Application.dataPath, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        /// <summary>
        /// Drops whole-line comments and XML docs, so a rule about code is not defeated by prose about
        /// code. These contracts explain themselves at length and name the very types they must not use.
        /// </summary>
        private static string StripComments(string source)
        {
            StringBuilder kept = new StringBuilder();
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//"))
                    continue;
                kept.AppendLine(lines[i]);
            }
            return kept.ToString();
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
