using System;
using Unity.Profiling;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Phase P0.2b: measures what the propulsion hot path allocates inside a REAL player loop.
    ///
    /// ============================ WHY THE P0.2 MEASUREMENT NEEDED REPLACING
    ///
    /// P0.2 measured GC.GetTotalMemory deltas over 2000 steps and took the minimum across seven
    /// trials, reasoning that editor background allocation can only ADD to a reading so the minimum
    /// is a sound upper bound. That reasoning is correct and still holds. What P0.2 never did was
    /// ask whether the instrument could see the thing it was being used to rule out.
    ///
    /// P0.2b asked. GC.GetTotalMemory is a LEVEL - the current size of the managed heap - not a
    /// counter of bytes allocated. Run the identical procedure over a loop that deliberately
    /// allocates a float[4] per step and it reports ZERO, because the arrays are collected inside
    /// the window and the level comes back to where it started. So P0.2's "0 bytes/step" was an
    /// upper bound at a resolution P0.2 had not established, not a measurement of zero. It did catch
    /// the original 907 bytes/step string defect, because that garbage outgrew what the window
    /// reclaimed - which is exactly why the blind spot went unnoticed.
    ///
    /// GC.CollectionCount was tried as a replacement and rejected on evidence: under Unity's Boehm
    /// collector the trigger threshold scales with heap size, and 40 MB of deliberate garbage moved
    /// the count by zero in this editor. GC.GetTotalAllocatedBytes would be ideal but needs
    /// .NET Standard 2.1, and this project is set to 2.0 - a project setting a validation pass has
    /// no business changing to suit its own measurement.
    ///
    /// ============================ THE INSTRUMENT USED HERE
    ///
    /// Unity's own "GC Allocated In Frame" counter, read through ProfilerRecorder. It is a COUNTER,
    /// so short-lived garbage is included, which is the specific defect in every level-based
    /// instrument above. It needs real frames, which is why this is a component.
    ///
    /// Measured as a DIFFERENTIAL across three phases of equal length, each judged by its minimum
    /// frame reading:
    ///
    ///   EVALUATING   - FixedUpdate runs N propulsion steps per frame
    ///   IDLE         - FixedUpdate runs none                       (the player loop's own cost)
    ///   SENSITIVITY  - FixedUpdate allocates a float[4] N times per frame, no propulsion
    ///
    /// (EVALUATING - IDLE) / N is the per-step cost of the propulsion path. Whatever the rest of the
    /// player loop allocates appears in both terms and cancels. (SENSITIVITY - IDLE) / N must come
    /// back at roughly 40 bytes, and that is what makes a zero in the first figure meaningful: it
    /// demonstrates, in the same run and on the same instrument, that a per-step allocation of this
    /// size would have been seen.
    ///
    /// This is not a formal zero-allocation proof - only an allocation-free-by-construction analysis
    /// of the IL would be that. It is a direct measurement of the right quantity with a stated and
    /// demonstrated sensitivity, which is what P0.2 lacked.
    ///
    /// ============================ WHY THIS IS NOT IN THE Editor FOLDER
    ///
    /// It was, at first, and Unity refused it: "Can't add script behaviour because it is an editor
    /// script." An editor-assembly MonoBehaviour can be attached in edit mode but not in Play Mode,
    /// and Play Mode is the entire point of this probe. So it lives beside the other runtime
    /// validation components, which is also where MavScriptedPropulsionCommandSource and
    /// MavFixedSyntheticDeck already are. It references no UnityEditor type.
    ///
    /// Everything created here is synthetic and in memory, labelled SYNTHETIC_VALIDATION_ONLY.
    /// </summary>
    public sealed class MavPropulsionPlayModeProbe : MonoBehaviour
    {
        // ---- results, read by the editor-side lifecycle machine before it exits Play Mode --------

        public static bool Finished;
        public static bool RecorderValid;
        public static bool MarkerRecorderValid;
        public static bool ProfilerWasEnabled;
        public static bool ProfilerEnabledForRun;

        /// <summary>Which instrument the numbers below came from, for the report.</summary>
        public static string InstrumentUsed = "none";

        public static long MinFrameBytesEvaluating;
        public static long MinFrameBytesIdle;
        public static long MinFrameBytesSensitivity;

        public static long MarkerBytesEvaluating;
        public static long MarkerBytesIdle;
        public static long MarkerBytesSensitivity;
        public static int StepsPerFrameUsed;
        public static int FramesEvaluating;
        public static int FramesIdle;
        public static int FramesSensitivity;
        public static int FixedUpdates;

        // Corroborating observations from a long loop in one real FixedUpdate. Reported, not
        // asserted: both are level/threshold instruments whose limits are established above.
        public static int LongLoopSteps;
        public static int Gen0CollectionsEvaluating;
        public static int Gen0CollectionsAllocControl;
        public static double LongLoopHeapBytesPerStep;
        public static double LongLoopMilliseconds;

        public static void Clear()
        {
            Finished = false;
            RecorderValid = false;
            MarkerRecorderValid = false;
            ProfilerWasEnabled = false;
            ProfilerEnabledForRun = false;
            InstrumentUsed = "none";
            MinFrameBytesEvaluating = 0;
            MinFrameBytesIdle = 0;
            MinFrameBytesSensitivity = 0;
            MarkerBytesEvaluating = 0;
            MarkerBytesIdle = 0;
            MarkerBytesSensitivity = 0;
            StepsPerFrameUsed = 0;
            FramesEvaluating = 0;
            FramesIdle = 0;
            FramesSensitivity = 0;
            FixedUpdates = 0;
            LongLoopSteps = 0;
            Gen0CollectionsEvaluating = 0;
            Gen0CollectionsAllocControl = 0;
            LongLoopHeapBytesPerStep = 0d;
            LongLoopMilliseconds = 0d;
        }

        /// <summary>
        /// Unblocks a driver whose own setup threw before or during this probe's creation.
        ///
        /// Without it, a crash in the editor-side callback would leave the machine waiting on
        /// Finished forever and the run would end at the watchdog with no diagnosis. A suite that
        /// hangs instead of reporting is worse than one that fails, which is the same lesson a
        /// crashing probe taught in Phase 5B.7.
        /// </summary>
        public static void ForceFinish()
        {
            Finished = true;
        }

        /// <summary>Bytes a float[4] costs: four floats plus the array header.</summary>
        public const int SensitivityBytesPerStep = 4 * sizeof(float) + 24;

        private const int WarmupFrames = 30;
        private const int PhaseFrames = 80;
        private const int GapFrames = 4;
        private const int StepsPerFrame = 40;
        private const int LongLoopStepCount = 1000000;

        private const int PhaseWarmup = 0;
        private const int PhaseLongLoop = 1;
        private const int PhaseEvaluating = 2;
        private const int PhaseIdle = 3;
        private const int PhaseSensitivity = 4;
        private const int PhaseDone = 5;

        private string syntheticTag = "SYNTHETIC_VALIDATION_ONLY";
        private float thrustN = 50000f;
        private float lateralOffsetM = 3f;

        private MavPropulsionSystem system;
        private MavEngineProfile profile;
        private MavFixedSyntheticDeck deck;
        private GameObject deckHost;

        private MavFlightState state;
        private MavAtmosphereSample atmosphere;

        private ProfilerRecorder allocRecorder;
        private ProfilerRecorder markerRecorder;
        private int phase = PhaseWarmup;
        private int phaseFrame;
        private long minEvaluating = long.MaxValue;
        private long minIdle = long.MaxValue;
        private long minSensitivity = long.MaxValue;
        private long markerMinEvaluating = long.MaxValue;
        private long markerMinIdle = long.MaxValue;
        private long markerMinSensitivity = long.MaxValue;
        private float[] sensitivitySink;
        private float controlSink;

        public void Configure(string tag, float synthThrustN, float synthLateralOffsetM)
        {
            syntheticTag = tag;
            thrustN = synthThrustN;
            lateralOffsetM = synthLateralOffsetM;
        }

        private void Start()
        {
            Clear();
            StepsPerFrameUsed = StepsPerFrame;

            MavF16EngineLawRegistrar.EnsureRegistered();

            deckHost = new GameObject("p02b-probe-deck-" + syntheticTag);
            deck = deckHost.AddComponent<MavFixedSyntheticDeck>();
            deck.fixedThrustN = thrustN;
            deck.declaredAuthority = MavThrustDataAuthority.SyntheticBench;

            profile = MavEngineProfile.CreateInMemory("p02b-probe-" + syntheticTag);
            profile.displayName = "p02b-probe-" + syntheticTag;
            profile.engineVariantIdentity = syntheticTag + " - not aircraft data";
            profile.sourceIdentity = syntheticTag;
            profile.provenance = MavEngineDataProvenance.CrossValidationOnly;
            profile.powerDynamicsLaw = MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState;
            profile.powerDynamicsProvenance = MavEngineDataProvenance.PublicReference;
            profile.thrustDeck = deck;

            MavPropulsionInstallationProfile installation = new MavPropulsionInstallationProfile();
            installation.installationId = "p02b-probe-twin-" + syntheticTag;
            installation.aircraftConfiguration = syntheticTag;
            installation.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "left", new Vector3(0f, -lateralOffsetM, 0f), 0),
                MakeSlot(1, "right", new Vector3(0f, lateralOffsetM, 0f), 1)
            };

            system = gameObject.AddComponent<MavPropulsionSystem>();
            system.installation = installation;
            system.autoResolveCommandSource = false;
            string error;
            system.Build(true, out error);

            MavScriptedPropulsionCommandSource source =
                gameObject.AddComponent<MavScriptedPropulsionCommandSource>();
            source.Resize(2);
            source.SetEngineThrottle(0, 0.9f);
            source.SetEngineThrottle(1, 0.4f);
            system.commandSource = source;
            system.composeStatusString = false;
            system.ResetEngineState(0.5f);

            // Cached OUTSIDE the measured loop on purpose. These are inputs a real caller already
            // holds; allocating them per step would measure the harness, not the system.
            state = new MavFlightState();
            state.worldPositionM = new Vector3(0f, 3000f, 0f);
            state.mach = 0.5f;
            state.trueAirspeedMps = 160f;

            atmosphere = new MavAtmosphereSample();
            atmosphere.altitudeM = 3000f;
            atmosphere.densityKgM3 = 0.9093f;
            atmosphere.speedOfSoundMps = 328.6f;

            // The "GC Allocated In Frame" counter is emitted by the profiler, and the profiler is
            // OFF in batchmode. A ProfilerRecorder on it still reports Valid and still returns a
            // value - the first P0.2b run read a flat 40 bytes/frame for all three phases, including
            // the one deliberately allocating 1600 bytes per frame. So Valid is not enough: the
            // profiler has to be switched on, and the sensitivity phase is what proves it took.
            ProfilerWasEnabled = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;
            ProfilerEnabledForRun = UnityEngine.Profiling.Profiler.enabled;

            allocRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC Allocated In Frame");
            RecorderValid = allocRecorder.Valid;

            // A second instrument on the same quantity, in case the counter above is not fed in
            // this context. Summing the GC.Alloc marker's samples over a frame is the other way
            // Unity exposes the same number, and whichever one passes the sensitivity control is
            // the one the report is allowed to use.
            markerRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame);
            MarkerRecorderValid = markerRecorder.Valid;
        }

        private MavEngineInstallation MakeSlot(int id, string name, Vector3 pos, int channel)
        {
            MavEngineInstallation s = new MavEngineInstallation();
            s.slotId = id;
            s.slotName = name;
            s.engineProfile = profile;
            s.positionAeroBodyM = pos;
            s.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            s.geometryDeclared = true;
            s.geometryProvenance = syntheticTag + " - not aircraft geometry";
            s.throttleChannel = channel;
            s.enabled = true;
            return s;
        }

        /// <summary>
        /// Drives the system from FixedUpdate and takes the long-loop observation there, so both
        /// happen inside a genuine physics step.
        ///
        /// It does NOT carry the per-frame measurement. In batchmode the editor's player loop runs
        /// Update far more often than FixedUpdate - the first attempt logged 20 FixedUpdate calls
        /// against roughly 240 Update frames - so a phase measured per Update frame while its work
        /// happened per FixedUpdate was mostly measuring frames in which nothing had been done. The
        /// measured work is in <see cref="Update"/> for that reason, and this method exists to show
        /// the same code path runs correctly under a real fixed timestep.
        /// </summary>
        private void FixedUpdate()
        {
            if (system == null || phase == PhaseDone)
                return;

            FixedUpdates++;

            if (phase == PhaseLongLoop)
            {
                if (LongLoopSteps == 0)
                    MeasureLongLoop();

                return;
            }

            system.Evaluate(state, atmosphere, 0.5f, Time.fixedDeltaTime);
        }

        /// <summary>
        /// Corroborating observation, taken inside one real FixedUpdate. REPORTED, not asserted:
        /// both figures come from level/threshold instruments whose limits this file documents, and
        /// the load-bearing measurement is the frame-counter differential.
        /// </summary>
        private void MeasureLongLoop()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            int gen0Before = GC.CollectionCount(0);
            long heapBefore = GC.GetTotalMemory(false);

            for (int i = 0; i < LongLoopStepCount; i++)
                system.Evaluate(state, atmosphere, 0.5f, 0.02f);

            long heapAfter = GC.GetTotalMemory(false);
            Gen0CollectionsEvaluating = GC.CollectionCount(0) - gen0Before;
            watch.Stop();

            LongLoopSteps = LongLoopStepCount;
            LongLoopHeapBytesPerStep = (double)(heapAfter - heapBefore) / LongLoopStepCount;
            LongLoopMilliseconds = watch.Elapsed.TotalMilliseconds;

            // Control for the collection counter: deliberate garbage of the same step count. A zero
            // here is what disqualified GC.CollectionCount as the primary instrument.
            int allocBefore = GC.CollectionCount(0);
            float sum = 0f;
            for (int i = 0; i < LongLoopStepCount; i++)
            {
                float[] junk = new float[4];
                junk[0] = i;
                sum += junk[0];
            }

            Gen0CollectionsAllocControl = GC.CollectionCount(0) - allocBefore;
            controlSink = sum;
        }

        /// <summary>
        /// The phase clock, the frame-counter read, and the measured work.
        ///
        /// All three are here on purpose. Each counter's LastValue reports the frame that just
        /// completed, so the read has to come BEFORE this frame's work; and the work has to be in
        /// the same callback as the read, because in batchmode Update runs an order of magnitude
        /// more often than FixedUpdate and a phase whose work lived elsewhere would mostly be
        /// measuring frames in which nothing happened.
        ///
        /// The first two frames of every phase are skipped, so a reading always describes a frame
        /// that was entirely inside the phase it is attributed to.
        /// </summary>
        private void Update()
        {
            if (phase == PhaseDone || Finished)
                return;

            phaseFrame++;

            long value = RecorderValid ? allocRecorder.LastValue : 0L;
            long marker = MarkerRecorderValid ? markerRecorder.LastValue : 0L;

            switch (phase)
            {
                case PhaseWarmup:
                    DoPropulsionWork();
                    if (phaseFrame >= WarmupFrames)
                        Advance(PhaseLongLoop);

                    break;

                case PhaseLongLoop:
                    // The loop itself runs inside a single FixedUpdate; this just waits for it.
                    if (phaseFrame >= GapFrames && LongLoopSteps > 0)
                        Advance(PhaseEvaluating);

                    break;

                case PhaseEvaluating:
                    if (phaseFrame > 2)
                    {
                        FramesEvaluating++;
                        if (value < minEvaluating) minEvaluating = value;
                        if (marker < markerMinEvaluating) markerMinEvaluating = marker;
                    }

                    DoPropulsionWork();

                    if (phaseFrame >= PhaseFrames)
                        Advance(PhaseIdle);

                    break;

                case PhaseIdle:
                    if (phaseFrame > 2)
                    {
                        FramesIdle++;
                        if (value < minIdle) minIdle = value;
                        if (marker < markerMinIdle) markerMinIdle = marker;
                    }

                    // Deliberately no work. This phase is what the propulsion cost is measured
                    // against, so it must do none of it.

                    if (phaseFrame >= PhaseFrames)
                        Advance(PhaseSensitivity);

                    break;

                case PhaseSensitivity:
                    if (phaseFrame > 2)
                    {
                        FramesSensitivity++;
                        if (value < minSensitivity) minSensitivity = value;
                        if (marker < markerMinSensitivity) markerMinSensitivity = marker;
                    }

                    DoSensitivityWork();

                    if (phaseFrame >= PhaseFrames)
                        Complete();

                    break;
            }
        }

        private void DoPropulsionWork()
        {
            for (int i = 0; i < StepsPerFrame; i++)
                system.Evaluate(state, atmosphere, 0.5f, 0.02f);
        }

        /// <summary>
        /// The same number of steps, each allocating a known amount and doing no propulsion work.
        /// This is what decides whether the instrument's zero for the propulsion phase means
        /// anything: an instrument that cannot see this cannot see a per-step leak either.
        /// </summary>
        private void DoSensitivityWork()
        {
            for (int i = 0; i < StepsPerFrame; i++)
            {
                float[] junk = new float[4];
                junk[0] = i;
                sensitivitySink = junk;
            }
        }

        private void Advance(int next)
        {
            phase = next;
            phaseFrame = 0;
        }

        private void Complete()
        {
            MinFrameBytesEvaluating = minEvaluating == long.MaxValue ? -1 : minEvaluating;
            MinFrameBytesIdle = minIdle == long.MaxValue ? -1 : minIdle;
            MinFrameBytesSensitivity = minSensitivity == long.MaxValue ? -1 : minSensitivity;

            MarkerBytesEvaluating = markerMinEvaluating == long.MaxValue ? -1 : markerMinEvaluating;
            MarkerBytesIdle = markerMinIdle == long.MaxValue ? -1 : markerMinIdle;
            MarkerBytesSensitivity =
                markerMinSensitivity == long.MaxValue ? -1 : markerMinSensitivity;

            // Pick the instrument that DEMONSTRATED it can see a known per-step allocation. An
            // instrument that reads flat is not chosen just because it is first, and if neither
            // bites the caller is told so rather than handed a zero.
            long counterDelta = MinFrameBytesSensitivity - MinFrameBytesIdle;
            long markerDelta = MarkerBytesSensitivity - MarkerBytesIdle;
            long needed = (long)StepsPerFrame * SensitivityBytesPerStep / 2L;

            if (RecorderValid && counterDelta >= needed)
                InstrumentUsed = "GC Allocated In Frame counter";
            else if (MarkerRecorderValid && markerDelta >= needed)
                InstrumentUsed = "GC.Alloc marker, summed per frame";
            else
                InstrumentUsed = "none passed its sensitivity control";

            if (allocRecorder.Valid)
                allocRecorder.Dispose();

            if (markerRecorder.Valid)
                markerRecorder.Dispose();

            phase = PhaseDone;
            Finished = true;
        }

        private void OnDestroy()
        {
            if (allocRecorder.Valid)
                allocRecorder.Dispose();

            if (markerRecorder.Valid)
                markerRecorder.Dispose();

            // Left exactly as found: this probe turned the profiler on for its own measurement and
            // has no business changing the editor's state past its own lifetime.
            UnityEngine.Profiling.Profiler.enabled = ProfilerWasEnabled;

            if (profile != null)
                DestroyImmediate(profile);

            if (deckHost != null)
                DestroyImmediate(deckHost);

            // Read once so the compiler cannot treat the sensitivity and control work as dead.
            if (sensitivitySink != null && sensitivitySink.Length == 0)
                Debug.Log("unreachable " + controlSink);
        }
    }
}
