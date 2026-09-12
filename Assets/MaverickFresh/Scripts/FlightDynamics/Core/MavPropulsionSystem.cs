using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-level, engine-count-agnostic propulsion aggregator.
    ///
    /// One installed engine for the F-16, two for the F-15, N in general. The engine count is DATA -
    /// the length of the installation's slot array - not a code path, which is the property that lets
    /// both aircraft run the same file.
    ///
    ///   for each enabled slot i:
    ///       runtime_i advances its own power state
    ///       F_i = thrust_i * direction_i
    ///       M_i = r_i x F_i + intrinsic_i
    ///
    ///   F_total = sum(F_i)
    ///   M_total = sum(M_i)
    ///
    /// This is a <see cref="MavPropulsionModelBase"/> on purpose. <see cref="MavSixDoFBody"/> already
    /// holds exactly one propulsion model and adds exactly one propulsive contribution per step, so
    /// presenting N engines as one model means the multi-engine capability arrives without touching
    /// the load-application boundary or the duplicate-contribution guard at all. The alternative -
    /// teaching SixDoF about engine arrays - would have put aggregation on both sides of the
    /// boundary.
    ///
    /// Nothing here touches Rigidbody. There is no Rigidbody field in this file.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavPropulsionSystem : MavPropulsionModelBase
    {
        [Header("Installation")]
        [Tooltip("Which engines this aircraft is equipped with. Empty means unpowered, which is a valid configuration with defined behaviour.")]
        public MavPropulsionInstallationProfile installation = new MavPropulsionInstallationProfile();

        [Header("Command")]
        [Tooltip("Optional per-engine command source. NONE means every engine follows the single throttle from the control pipeline, which is the normal case and all the F-16 needs. Attach one to get differential throttle, asymmetric shutdown, or a propulsion-based control law.")]
        public MavPropulsionCommandSourceBase commandSource;

        [Tooltip("Resolve a command source from this GameObject when the field is empty. Off means an explicitly wired source only.")]
        public bool autoResolveCommandSource = true;

        [Header("Aggregate Telemetry (read-only)")]
        [Tooltip("Engines INSTALLED, whatever their operational state.")]
        public int debugEngineCount;

        [Tooltip("Engines that actually contributed loads this step: installed, enabled, not failed, not cut off.")]
        public int debugContributingEngineCount;

        [Tooltip("Contributing engines whose thrust number came from authoritative data.")]
        public int debugAuthoritativeEngineCount;

        [Tooltip("Installed engines whose PROFILE declares authoritative thrust data, regardless of whether they are running. This is the data question; the counter above is the this-step question.")]
        public int debugAuthoritativeProfileCount;

        [Tooltip("Contributing engines whose thrust query fell outside the deck's validated envelope.")]
        public int debugOutsideEnvelopeEngineCount;

        [Header("Aggregate Operational State (read-only)")]
        [Tooltip("Engines commanded to fuel cutoff this step. An OPERATIONAL state, never a provenance failure.")]
        public int debugCutoffEngineCount;

        [Tooltip("Engines marked failed. Operational, not a provenance failure.")]
        public int debugFailedEngineCount;

        [Tooltip("Installations disabled in configuration. Operational/configuration, not a provenance failure.")]
        public int debugDisabledEngineCount;

        [Header("Aggregate Loads (read-only)")]
        [Tooltip("SUM of every contributing engine's scalar thrust along its own thrust direction, N. Not a per-engine value and not a maximum.")]
        public float debugTotalThrustN;

        public Vector3 debugTotalForceAeroBodyN;
        public Vector3 debugTotalMomentAeroBodyNm;

        [Tooltip("MEAN actual power over contributing engines, percent. A mean cannot express 90/40, so read debugEngineResults for anything asymmetric - see debugPowerStateSpread01.")]
        public float debugMeanPowerPercent;

        [Tooltip("max minus min actual power across contributing engines, as 0..1. NON-ZERO means the scalar mean above is hiding an asymmetry.")]
        public float debugPowerStateSpread01;

        [Tooltip("max minus min COMMANDED throttle across engines. Non-zero means the asymmetry was commanded rather than a symptom.")]
        public float debugCommandedThrottleSpread01;

        // Interned literals. Assigning one of these to a field allocates nothing, whereas
        // enum.ToString() allocates a fresh string every call AND resolves the name reflectively -
        // the Unity run measured 907 bytes per step with those in the loop.
        public const string AuthorityLinkedScalar = "LINKED_SCALAR";
        public const string AuthorityPerEngineSource = "PER_ENGINE_SOURCE";
        private const string ModeLinkedThrottle = "LinkedThrottle";
        private const string ModePerEngineThrottle = "PerEngineThrottle";

        [Header("Command Authority (read-only diagnostic)")]
        [Tooltip("Which layer owns per-engine throttle and cutoff this step: LINKED_SCALAR or PER_ENGINE_SOURCE. Exactly one, never a blend. Telemetry only - nothing reads this to make a decision.")]
        public string debugCommandAuthority = "LINKED_SCALAR";

        [Tooltip("How many engines a command source explicitly addressed this step. Must equal the engine count for per-engine authority to be granted.")]
        public int debugAddressedEngineCount;

        [Tooltip("True when a source published a command covering only SOME engines and it was therefore refused in full. A configuration or control-law defect, surfaced rather than silently half-applied.")]
        public bool debugPartialCommandRefused;

        public string debugCommandMode = ModeLinkedThrottle;

        [Header("Verbose Telemetry")]
        [Tooltip("OFF by default. ON composes the debugStatus summary string every physics step, which allocates a few hundred bytes per step. The numeric counters above are always live and allocate nothing; this is only the human-readable roll-up. Turn it on while diagnosing, off for flight.")]
        public bool composeStatusString = false;

        [Tooltip("Human-readable roll-up of the counters above. Only refreshed while composeStatusString is ON - the numeric fields are the always-live telemetry.")]
        public string debugStatus = "not initialised";

        [Header("Per-Engine Telemetry (read-only)")]
        [Tooltip("One entry per installed slot, refreshed every step. Reused in place; no per-step allocation.")]
        public MavEngineLoadResult[] debugEngineResults = new MavEngineLoadResult[0];

        // Runtimes are built once. Rebuilding them per step would reset the spool states, which are
        // the one thing in this system that must persist across steps.
        private MavEngineRuntime[] runtimes;
        private bool built;
        private string buildError = string.Empty;

        // One command object for the life of the component. Resized at Build, overwritten per step.
        private readonly MavPropulsionCommand command = new MavPropulsionCommand();

        public override string PropulsionModelName
        {
            get
            {
                int count = installation != null ? installation.EngineCount : 0;
                string id = installation != null ? installation.installationId : "none";
                return "Propulsion system: " + count + " engine(s), installation '" + id + "'";
            }
        }

        /// <summary>
        /// True only when EVERY installed engine has authoritative dimensional thrust data.
        ///
        /// Deliberately unanimous, not any-of. A twin with one sourced engine and one guessed engine
        /// produces a net force that is part guess, and reporting that aggregate as authoritative
        /// would launder the unsourced half. Zero engines is false: no thrust data exists to be
        /// authoritative about.
        ///
        /// A question about DATA, not about operation. It reads every installed engine's profile and
        /// ignores whether that engine is running, cut off, failed or idling - because an intentionally
        /// shut-down engine with a sourced model is still described by sourced data, and its zero
        /// thrust is a number we KNOW rather than one we lack.
        /// </summary>
        public override bool HasAuthoritativeData
        {
            get
            {
                if (installation == null || installation.EngineCount == 0)
                    return false;

                for (int i = 0; i < installation.engines.Length; i++)
                {
                    MavEngineInstallation slot = installation.engines[i];
                    if (slot == null
                        || slot.engineProfile == null
                        || !slot.engineProfile.HasAuthoritativeThrustData)
                        return false;
                }

                return true;
            }
        }

        public override bool IsAcceptableForLiveFlight
        {
            get
            {
                if (installation == null)
                    return false;

                string reason;
                return installation.IsAcceptableForLiveFlight(out reason);
            }
        }

        public override string ThrustDataStatus
        {
            get
            {
                if (installation == null || installation.EngineCount == 0)
                    return "no engines installed: propulsion contributes nothing";

                string reason;
                installation.IsAcceptableForLiveFlight(out reason);
                return reason;
            }
        }

        public bool IsBuilt
        {
            get { return built; }
        }

        public string BuildError
        {
            get { return buildError; }
        }

        public int RuntimeCount
        {
            get { return runtimes != null ? runtimes.Length : 0; }
        }

        /// <summary>
        /// The runtime for one slot index, so validation and telemetry can inspect an individual
        /// engine's independent state. Returns null for an out-of-range index rather than throwing.
        /// </summary>
        public MavEngineRuntime GetRuntime(int index)
        {
            if (runtimes == null || index < 0 || index >= runtimes.Length)
                return null;

            return runtimes[index];
        }

        /// <summary>Finds a runtime by its declared slot id. Slot ids are unique per installation.</summary>
        public MavEngineRuntime GetRuntimeBySlotId(int slotId)
        {
            if (runtimes == null)
                return null;

            for (int i = 0; i < runtimes.Length; i++)
            {
                if (runtimes[i] != null
                    && runtimes[i].Installation != null
                    && runtimes[i].Installation.slotId == slotId)
                    return runtimes[i];
            }

            return null;
        }

        /// <summary>
        /// Creates one runtime per installed slot, resolving each engine's declared power law.
        ///
        /// Idempotent unless forced: calling it again on an already-built system does nothing, because
        /// a rebuild would discard every engine's spool state. Pass force only when the installation
        /// itself has changed.
        /// </summary>
        public bool Build(bool force, out string error)
        {
            if (built && !force)
            {
                error = "already built";
                return true;
            }

            built = false;
            buildError = string.Empty;
            runtimes = null;

            if (installation == null)
            {
                buildError = "no installation profile";
                error = buildError;
                debugStatus = buildError;
                return false;
            }

            string reason;
            if (!installation.IsValid(out reason))
            {
                buildError = "installation invalid: " + reason;
                error = buildError;
                debugStatus = buildError;
                return false;
            }

            int count = installation.EngineCount;
            runtimes = new MavEngineRuntime[count];
            debugEngineResults = new MavEngineLoadResult[count];

            for (int i = 0; i < count; i++)
            {
                MavEngineInstallation slot = installation.engines[i];

                IMavEnginePowerDynamics dynamics =
                    MavEnginePowerDynamicsFactory.Resolve(slot.engineProfile.powerDynamicsLaw);

                if (dynamics == null)
                {
                    // Fail closed rather than substituting a different law: an aircraft flying a law
                    // its profile does not declare is worse than an aircraft that refuses to build.
                    buildError = "slot " + slot.slotId + " declares power law "
                                 + slot.engineProfile.powerDynamicsLaw
                                 + " which is not registered; refusing to substitute another law";
                    error = buildError;
                    debugStatus = buildError;
                    runtimes = null;
                    return false;
                }

                // One runtime per slot. Two slots sharing an engine profile still land here twice and
                // get two distinct runtimes - the independence requirement, enforced structurally.
                runtimes[i] = new MavEngineRuntime(slot, dynamics);
            }

            // Size the command once, here, so no physics step ever allocates one.
            command.Resize(count);

            if (commandSource == null && autoResolveCommandSource)
                commandSource = GetComponent<MavPropulsionCommandSourceBase>();

            built = true;
            debugEngineCount = count;
            debugStatus = "built: " + count + " engine runtime(s)";
            error = "OK";
            return true;
        }

        /// <summary>
        /// This step's propulsion command.
        ///
        /// Reset to linked at the pipeline throttle at the start of every Evaluate, then offered to
        /// the command source. Exposed so telemetry and validation can read what was commanded, which
        /// is what separates "the engines are asymmetric because they were told to be" from "the
        /// engines are asymmetric and nobody asked for that".
        /// </summary>
        public MavPropulsionCommand Command
        {
            get { return command; }
        }

        public override MavPropulsiveLoads Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            float deltaTime)
        {
            if (!built)
            {
                string error;
                Build(false, out error);
            }

            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;

            debugTotalForceAeroBodyN = Vector3.zero;
            debugTotalMomentAeroBodyNm = Vector3.zero;
            debugTotalThrustN = 0f;
            debugContributingEngineCount = 0;
            debugAuthoritativeEngineCount = 0;
            debugAuthoritativeProfileCount = 0;
            debugOutsideEnvelopeEngineCount = 0;
            debugCutoffEngineCount = 0;
            debugFailedEngineCount = 0;
            debugDisabledEngineCount = 0;
            debugMeanPowerPercent = 0f;
            debugPowerStateSpread01 = 0f;
            debugCommandedThrottleSpread01 = 0f;

            if (runtimes == null || runtimes.Length == 0)
            {
                // Zero engines: defined, safe, and not an error. An unpowered aircraft is a
                // configuration Phase 5C flies deliberately.
                debugEngineCount = 0;
                debugCommandMode = ModeLinkedThrottle;
                debugCommandAuthority = AuthorityLinkedScalar;
                debugAddressedEngineCount = 0;
                debugPartialCommandRefused = false;
                debugStatus = built
                    ? "no engines installed: zero propulsive load"
                    : buildError;
                loads.hasAuthoritativeData = false;
                return loads;
            }

            debugEngineCount = runtimes.Length;

            // ---- resolve this step's command -----------------------------------------------------
            //
            // Reset to LINKED at the pipeline throttle first, then offer the command to the source.
            // Order matters: a source that is disabled, removed, or declines to publish leaves linked
            // flight in place rather than a stale asymmetric command. A stuck differential throttle is
            // a control failure; reverting to linked is not.
            command.BeginStep(throttle01);

            bool published = false;
            if (commandSource != null && commandSource.enabled)
                published = commandSource.TryPublishPropulsionCommand(command, throttle01);

            // ---- COMMAND AUTHORITY: exactly one owner, never a blend ----------------------------
            //
            // A source owns per-engine commands only if it published a command covering EVERY
            // installed engine. Anything else - no source, a source that declined, or a source that
            // addressed only some engines - reverts fully to the scalar linked throttle.
            //
            // Partial coverage is REFUSED rather than partially honoured. Leaving the unaddressed
            // engines on the scalar would produce numbers indistinguishable from a fully commanded
            // asymmetry, and a reader could not tell which engines the source had actually commanded.
            // Refusing is also the safe direction: linked flight is a defined state, a half-applied
            // differential command is not.
            int addressed = command.AddressedEngineCount;
            debugAddressedEngineCount = addressed;

            if (published && command.EngineCount > 0 && addressed == command.EngineCount)
            {
                command.CommitPerEngineAuthority();
                debugPartialCommandRefused = false;
            }
            else
            {
                debugPartialCommandRefused = published && addressed > 0
                                             && addressed != command.EngineCount;
                command.RevertToLinked(throttle01);
            }

            debugCommandAuthority =
                command.Authority == MavPropulsionCommandAuthority.PerEngineSource
                    ? AuthorityPerEngineSource
                    : AuthorityLinkedScalar;

            // Not Mode.ToString(): an enum ToString allocates and resolves the name reflectively.
            debugCommandMode = command.Mode == MavPropulsionCommandMode.PerEngineThrottle
                ? ModePerEngineThrottle
                : ModeLinkedThrottle;

            debugCommandedThrottleSpread01 = command.CommandedThrottleSpread();

            // Reused in place. A fresh array here would allocate every physics step for every
            // aircraft, which is exactly the per-frame garbage this system is required to avoid.
            if (debugEngineResults == null || debugEngineResults.Length != runtimes.Length)
                debugEngineResults = new MavEngineLoadResult[runtimes.Length];

            float powerSum = 0f;
            int powerSamples = 0;
            float powerMin = float.MaxValue;
            float powerMax = float.MinValue;

            // Authority of the numbers actually summed this step. The configuration-level question
            // is HasAuthoritativeData's job, not this loop's.
            bool everyContributingResultAuthoritative = true;

            // Whether every installed slot still HAS an engine definition. Distinct from every
            // operational flag: a slot whose profile is missing or has been destroyed has lost its
            // data, and no amount of "it is not running anyway" makes that sourced.
            bool everySlotHasProfile = true;

            for (int i = 0; i < runtimes.Length; i++)
            {
                MavEngineRuntime runtime = runtimes[i];
                if (runtime == null)
                {
                    everySlotHasProfile = false;
                    continue;
                }

                MavEngineInstallation slot = runtime.Installation;

                // ---- the DATA question, asked of every installed engine regardless of state -------
                //
                // slot.engineProfile != null also catches Unity "fake-null": a ScriptableObject that
                // has been destroyed compares equal to null while the C# reference survives. That case
                // exists only in a real Unity session and is why this check is not merely defensive.
                bool hasProfile = slot != null && slot.engineProfile != null;
                if (!hasProfile)
                    everySlotHasProfile = false;

                bool profileAuthoritative =
                    hasProfile && slot.engineProfile.HasAuthoritativeThrustData;

                if (profileAuthoritative)
                    debugAuthoritativeProfileCount++;

                float engineThrottle = command.ThrottleForEngine(i);
                bool cutoff = command.IsCutoff(i);

                MavEngineLoadResult result =
                    runtime.Evaluate(state, atmosphere, engineThrottle, cutoff, deltaTime);

                debugEngineResults[i] = result;

                // ---- operational bookkeeping, kept apart from provenance --------------------------
                if (cutoff)
                    debugCutoffEngineCount++;
                if (runtime.Failed)
                    debugFailedEngineCount++;
                if (slot != null && !slot.enabled)
                    debugDisabledEngineCount++;

                // A disabled, failed or cut-off engine adds nothing at all - not a zero force that
                // happens to sum to nothing, but no participation. P-009 and P-017 check this.
                if (!result.running)
                    continue;

                debugContributingEngineCount++;

                debugTotalForceAeroBodyN += result.forceAeroBodyN;
                debugTotalMomentAeroBodyNm += result.momentAeroBodyNm;
                debugTotalThrustN += result.thrustN;

                if (result.thrustAuthoritative)
                    debugAuthoritativeEngineCount++;
                else
                    everyContributingResultAuthoritative = false;

                if (result.thrustN != 0f && !result.insideThrustEnvelope)
                    debugOutsideEnvelopeEngineCount++;

                powerSum += result.actualPowerPercent;
                powerSamples++;
                if (result.actualPowerPercent < powerMin) powerMin = result.actualPowerPercent;
                if (result.actualPowerPercent > powerMax) powerMax = result.actualPowerPercent;
            }

            loads.forceAeroBodyN = debugTotalForceAeroBodyN;
            loads.momentAeroBodyNm = debugTotalMomentAeroBodyNm;

            // SUM of each engine's scalar thrust along its own thrust direction. Defined here rather
            // than left to the reader: it is not a per-engine value and not a maximum.
            loads.reportedThrustN = debugTotalThrustN;

            // MEAN actual power over contributing engines. Retained because the whole pre-existing
            // pipeline reads it, and honest about what it cannot do: a mean cannot express 90% on one
            // side and 40% on the other. powerStateSpread01 below is non-zero exactly when this
            // scalar is hiding an asymmetry, so a consumer can tell instead of guessing.
            debugMeanPowerPercent = powerSamples > 0 ? powerSum / powerSamples : 0f;
            loads.powerState01 = powerSamples > 0
                ? Mathf.Clamp01(debugMeanPowerPercent * 0.01f)
                : 0f;

            debugPowerStateSpread01 = powerSamples > 0
                ? Mathf.Clamp01((powerMax - powerMin) * 0.01f)
                : 0f;

            loads.contributingEngineCount = debugContributingEngineCount;
            loads.powerStateSpread01 = debugPowerStateSpread01;

            // ---- DATA AUTHORITY of THESE loads ---------------------------------------------------
            //
            // Exactly one question: is every number summed into these loads backed by authoritative
            // data? Nothing else. In particular it does NOT ask whether the aircraft's whole
            // propulsion configuration is sourced - that is a different question with a different
            // answer and a different owner, see below.
            //
            //   - every engine that CONTRIBUTED returned an authoritative number. Unanimous, because
            //     the summed force contains each contribution and one guess makes the total part
            //     guess. A deck can hold authoritative tables and still answer an out-of-envelope
            //     query by extrapolating; that answer is not supported by the data however good the
            //     table is, and result.thrustAuthoritative already carries the downgrade.
            //
            //   - the condition is vacuously true when nothing is running, which is the point of
            //     section 6: an authoritative twin with both engines deliberately shut down reports
            //     authoritative data and zero thrust. Its thrust is a number we KNOW, not one we lack.
            //     Folding operational state into provenance would conflate "the engines are off" with
            //     "we have no engine data", and those call for opposite responses.
            //
            //   - everySlotHasProfile guards the OTHER direction, and exists because the Unity run
            //     found it. A slot whose profile is missing - never assigned, or a ScriptableObject
            //     asset destroyed mid-session, which is Unity "fake-null" and cannot happen to a plain
            //     class - contributes nothing, so the vacuous-truth above would have reported
            //     authoritative data for an aircraft whose engine definition had been LOST. That is
            //     genuinely "we have no engine data" and must not read as sourced.
            //
            // WHY THE INSTALLED-PROFILE TERM IS NOT HERE. An earlier version also required every
            // INSTALLED profile to be authoritative, which sounds stricter and is simply a different
            // question wearing this one's name. It made a per-step load flag flip based on what is
            // bolted to the aircraft rather than on what these loads contain - and it would have made
            // a live-flight gate that reads it flip the moment a pilot started a second, unsourced
            // engine. The configuration question is answered, operation-independently, by
            // HasAuthoritativeData and IsAcceptableForLiveFlight on this component. Readiness reads
            // those; this flag describes this step's numbers. debugAuthoritativeProfileCount below
            // reports the configuration side as telemetry so both are visible without either
            // impersonating the other.
            loads.hasAuthoritativeData =
                debugEngineCount > 0
                && everySlotHasProfile
                && everyContributingResultAuthoritative;

            // Composed only on request. This concatenation and its nine int-to-string conversions
            // were the whole of the measured per-step allocation; the engine loop above allocates
            // nothing. Every number in it is already available as a live numeric field, so gating the
            // string costs no information - only convenience.
            if (composeStatusString)
                debugStatus = DescribeStatus();

            return loads;
        }

        public override void ResetEngineState(float throttle01)
        {
            if (!built)
            {
                string error;
                Build(false, out error);
            }

            if (runtimes == null)
                return;

            // Reset establishes an initial condition, so it uses the LINKED throttle deliberately:
            // an aircraft being initialised has not yet had a differential command published, and
            // inventing one here would mean the engines started asymmetric for no commanded reason.
            // BeginStep also leaves authority at LinkedScalar, which is the correct initial owner.
            command.BeginStep(throttle01);

            for (int i = 0; i < runtimes.Length; i++)
            {
                if (runtimes[i] == null)
                    continue;

                runtimes[i].Reset(command.ThrottleForEngine(i));
            }
        }

        /// <summary>
        /// Human-readable roll-up of this step's counters.
        ///
        /// Allocates, so it is called only when <see cref="composeStatusString"/> is on, or on demand
        /// by an inspector, a log line, or validation. Every value in it is also a live numeric field.
        /// </summary>
        public string DescribeStatus()
        {
            return "engines=" + debugEngineCount
                + " contributing=" + debugContributingEngineCount
                + " authoritativeProfiles=" + debugAuthoritativeProfileCount
                + " authoritativeResults=" + debugAuthoritativeEngineCount
                + " cutoff=" + debugCutoffEngineCount
                + " failed=" + debugFailedEngineCount
                + " disabled=" + debugDisabledEngineCount
                + " outsideEnvelope=" + debugOutsideEnvelopeEngineCount
                + " authority=" + debugCommandAuthority
                + " addressed=" + debugAddressedEngineCount
                + (debugPartialCommandRefused ? " PARTIAL_COMMAND_REFUSED" : "");
        }

        /// <summary>
        /// Aggregate loads composed from an explicit set of per-engine results.
        ///
        /// Pure and static so validation can check the aggregation rule on hand-built inputs, without
        /// a GameObject and without going through the engine model that produced them. That
        /// separation matters: a test that built its expectation by calling the same summation it is
        /// checking would pass whatever that summation did.
        /// </summary>
        public static MavPropulsiveLoads ComposeAggregate(MavEngineLoadResult[] results)
        {
            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
            if (results == null || results.Length == 0)
                return loads;

            int contributing = 0;
            int authoritative = 0;
            float powerSum = 0f;

            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i].running)
                    continue;

                contributing++;
                loads.forceAeroBodyN += results[i].forceAeroBodyN;
                loads.momentAeroBodyNm += results[i].momentAeroBodyNm;
                loads.reportedThrustN += results[i].thrustN;
                powerSum += results[i].actualPowerPercent;

                if (results[i].thrustAuthoritative)
                    authoritative++;
            }

            loads.powerState01 = contributing > 0
                ? Mathf.Clamp01(powerSum / contributing * 0.01f)
                : 0f;
            loads.contributingEngineCount = contributing;

            // This overload sees only results, never profiles, so it can answer the this-step question
            // and not the installed-data one. It is a helper for checking the summation rule on
            // hand-built inputs, NOT the authority rule - MavPropulsionSystem.Evaluate owns that,
            // because only it can see every installed engine's profile.
            loads.hasAuthoritativeData = contributing > 0 && authoritative == contributing;
            return loads;
        }
    }
}
