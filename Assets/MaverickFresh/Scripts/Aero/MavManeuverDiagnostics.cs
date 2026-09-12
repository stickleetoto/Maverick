using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh
{
    /// <summary>
    /// Phase 5B high-maneuver diagnostics.
    ///
    /// WHY THIS EXISTS. The aircraft is reported as acceptable in cruise and non-physical during
    /// abrupt or high-G maneuvers. That is a statement about transients, and a transient cannot be
    /// diagnosed from configuration values or from flying around by feel - it needs the same maneuver
    /// flown the same way twice, with every contribution recorded at fixed timestep.
    ///
    /// WHAT IT DOES NOT DO. It applies no force, no torque, and no velocity change. It never writes
    /// to the Rigidbody, so it cannot be the reason the aircraft behaves any particular way. Its only
    /// effect on the aircraft is to substitute the pilot COMMAND while a scripted case is running,
    /// through MavMouseFlightJet.scriptedCommandActive - and a command is what a pilot would have
    /// supplied anyway. Limiters, gains and assists all still run exactly as they normally do.
    ///
    /// THE SEPARATION THAT MATTERS. Requirement 7 of Phase 4B asked for curvature produced by
    /// aerodynamics to be distinguishable from curvature produced directly by legacy assistance.
    /// That distinction is carried through here as two independently measured channels -
    /// aeroCurvatureAccel from MavAeroBody, legacyCurvatureAccel from the jet's velocity-turn assist -
    /// rather than inferred from one total.
    ///
    /// MORELLI SHADOW. Each step the Morelli polynomial is evaluated at the LIVE aircraft state and
    /// its coefficients and dimensional loads are recorded alongside the legacy ones. This is shadow
    /// in the Phase 5A sense: computed, recorded, never applied. It answers "what would the
    /// replacement model have done here" without the replacement model touching anything.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public class MavManeuverDiagnostics : MonoBehaviour
    {
        public enum MavManeuverCase
        {
            None = 0,
            AbruptFullPitch = 1,
            AbruptFullRoll = 2,
            CombinedRollPitch = 3,
            HighSpeedPull = 4,
            MediumSpeedPull = 5,
            LowSpeedHighAoAPull = 6,
            ReleaseAfterHighLoad = 7,
            SustainedHighGTurn = 8,
            HighAoARollYawCoupling = 9,
            NearStallRecovery = 10
        }

        [Header("Wiring (resolved at runtime)")]
        public MavMouseFlightJet jet;
        public MavAeroBody aeroBody;
        public MavInstructorController instructor;
        public Rigidbody body;

        [Header("Run Control")]
        [Tooltip("Set a case and tick runRequested to fly it. The diagnostic drives the command; do "
                 + "not fly manually while a case is running.")]
        public MavManeuverCase selectedCase = MavManeuverCase.None;
        public bool runRequested;
        [Tooltip("Seconds of scripted input before the command returns to neutral.")]
        public float holdSeconds = 3f;
        [Tooltip("Total recorded duration, including the post-release settle.")]
        public float recordSeconds = 8f;
        [Tooltip("Written under Application.persistentDataPath when a case finishes.")]
        public bool writeCsvOnFinish = true;

        [Tooltip("Whether the scripted command counts as MANUAL pilot input, i.e. the KEYBOARD pitch "
                 + "path. Default false, because MavInstructorController sets pitchOverride only from "
                 + "keyboard pitch input - mouse aim never sets it - so false is what a normal "
                 + "mouse-flight pull actually does. Set true to reproduce keyboard pitch, which "
                 + "engages ApplyManualLimiterBypass and lerps the AoA and G limiters 65% of the way "
                 + "towards no limiting at all.")]
        public bool treatScriptedInputAsManual;

        [Header("Morelli Shadow")]
        [Tooltip("Evaluate the replacement F-16 aero model at the live state each step. Compute only: "
                 + "nothing here is ever applied to the Rigidbody.")]
        public bool evaluateMorelliShadow = true;
        [Tooltip("Elevator/aileron/rudder the shadow assumes for the current pilot command, in deg at "
                 + "full deflection. These are the Morelli input envelope limits, not a control law.")]
        public Vector3 shadowFullSurfaceDeg = new Vector3(25f, 21.5f, 30f);

        [Header("Protection Envelope In Force (read from the authority)")]
        [Tooltip("Read from MavInstructorController when a case starts, i.e. the values the limiters "
                 + "will really use - not the catalog constants and not the jet mirrors.")]
        public float envelopeAoASoftLimitDeg;
        public float envelopeAoAHardLimitDeg;
        public float envelopeSoftGLimit;
        public float envelopeHardGLimit;
        public float envelopeAoAPitchReduction;

        [Header("Live Readout")]
        public string status = "idle";
        public float elapsed;
        public int samples;

        [Header("Derived Metrics (filled when a case finishes)")]
        public float metricTimeToRollRateResponse = -1f;
        public float metricTimeToAoAGrowth = -1f;
        public float metricTimeFromAoAToNz = -1f;
        public float metricPeakAoADeg;
        public float metricPeakNzG;
        public float metricPeakRollRateDegSec;
        public float metricPeakPitchRateDegSec;
        public float metricPeakYawRateDegSec;
        public float metricTasLossMps;
        [Tooltip("Seconds spent beyond each protection breakpoint. These are the numbers that say "
                 + "whether the envelope was actually enforced, and they are measured against the "
                 + "authority's values rather than hardcoded thresholds.")]
        public float metricTimeAboveSoftAoA;
        public float metricTimeAboveHardAoA;
        public float metricTimeAboveSoftG;
        public float metricTimeAboveHardG;
        public int metricSignReversalsAfterRelease;
        [Tooltip("Mean over the run of aeroCurvature / (aeroCurvature + legacyCurvature).")]
        public float metricMeanAeroOwnership;
        [Tooltip("First time the legacy pitch angular acceleration exceeds what the Morelli moment "
                 + "could have produced about Iyy. This is the divergence timestamp.")]
        public float metricFirstOwnershipDivergenceT = -1f;
        public string metricDominantLegacyContribution = "none";

        private readonly List<Sample> recorded = new List<Sample>(1200);
        private bool running;
        private bool released;
        private float releaseTime;

        private struct Sample
        {
            public float t;
            public float tas, mach, qbar, alphaDeg, betaDeg;
            public float p, q, r;
            public float bankDeg, pitchAttitudeDeg, nzG;
            public Vector3 velocityDir;

            public Vector3 legacyAeroForce;
            public Vector3 legacyStabilityAccel;
            public Vector3 legacyControlTorque;
            public float legacyVelocityAssistAccel;
            public float legacyAlignmentAssistTorque;
            public float legacyAlignmentAssistScale;
            public float legacyAeroCurvatureAccel;
            public string limiterState;
            public float thrustBoostAllowance;

            public float cx, cy, cz, cl, cm, cn;
            public Vector3 shadowForceN;
            public Vector3 shadowMomentNm;
            public float shadowPitchAccelRadSec2;
            public float legacyPitchAccelRadSec2;
        }

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (aeroBody == null) aeroBody = GetComponent<MavAeroBody>();
            if (instructor == null) instructor = GetComponent<MavInstructorController>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Resolve();
            if (jet == null || body == null)
            {
                status = "not wired: needs MavMouseFlightJet and a Rigidbody";
                return;
            }

            if (runRequested && !running)
                BeginRun();

            if (!running)
                return;

            elapsed += Time.fixedDeltaTime;
            DriveScriptedCommand();
            recorded.Add(Capture());
            samples = recorded.Count;

            if (elapsed >= recordSeconds)
                FinishRun();
        }

        private void BeginRun()
        {
            if (selectedCase == MavManeuverCase.None)
            {
                status = "select a case first";
                runRequested = false;
                return;
            }

            CaptureEnvelopeInForce();

            recorded.Clear();
            elapsed = 0f;
            released = false;
            releaseTime = -1f;
            running = true;
            ResetMetrics();
            status = "running " + selectedCase;
        }

        /// <summary>
        /// Read the protection envelope from the component that enforces it.
        ///
        /// Deliberately NOT from the catalog and NOT from the jet mirrors. The whole class of defect
        /// being chased here is a configured value that never reaches its consumer, so a diagnostic
        /// that reported the configured value would hide exactly what it exists to reveal.
        /// </summary>
        private void CaptureEnvelopeInForce()
        {
            if (instructor == null)
                return;

            envelopeAoASoftLimitDeg = instructor.aoaSoftLimitDeg;
            envelopeAoAHardLimitDeg = instructor.aoaHardLimitDeg;
            envelopeSoftGLimit = instructor.softGLimit;
            envelopeHardGLimit = instructor.hardGLimit;
            envelopeAoAPitchReduction = instructor.aoaPitchReduction;
        }

        private void ResetMetrics()
        {
            metricTimeToRollRateResponse = -1f;
            metricTimeToAoAGrowth = -1f;
            metricTimeFromAoAToNz = -1f;
            metricPeakAoADeg = 0f;
            metricPeakNzG = 0f;
            metricPeakRollRateDegSec = 0f;
            metricPeakPitchRateDegSec = 0f;
            metricPeakYawRateDegSec = 0f;
            metricTasLossMps = 0f;
            metricTimeAboveSoftAoA = 0f;
            metricTimeAboveHardAoA = 0f;
            metricTimeAboveSoftG = 0f;
            metricTimeAboveHardG = 0f;
            metricSignReversalsAfterRelease = 0;
            metricMeanAeroOwnership = 0f;
            metricFirstOwnershipDivergenceT = -1f;
            metricDominantLegacyContribution = "none";
        }

        /// <summary>
        /// The scripted command for the selected case. Pitch is negative for nose-up, matching the
        /// sign the instructor and the AoA limiter already use.
        /// </summary>
        private void DriveScriptedCommand()
        {
            bool hold = elapsed < holdSeconds;
            if (!hold && !released)
            {
                released = true;
                releaseTime = elapsed;
            }

            Vector3 cmd = Vector3.zero;
            float throttle = 0.85f;

            switch (selectedCase)
            {
                case MavManeuverCase.AbruptFullPitch:
                case MavManeuverCase.HighSpeedPull:
                case MavManeuverCase.MediumSpeedPull:
                case MavManeuverCase.LowSpeedHighAoAPull:
                case MavManeuverCase.NearStallRecovery:
                    cmd = new Vector3(hold ? -1f : 0f, 0f, 0f);
                    break;

                case MavManeuverCase.AbruptFullRoll:
                    cmd = new Vector3(0f, 0f, hold ? 1f : 0f);
                    break;

                case MavManeuverCase.CombinedRollPitch:
                    cmd = new Vector3(hold ? -1f : 0f, 0f, hold ? 1f : 0f);
                    break;

                case MavManeuverCase.ReleaseAfterHighLoad:
                    // Deliberately short hold: the interesting part is what happens after release.
                    cmd = new Vector3(elapsed < Mathf.Min(holdSeconds, 2f) ? -1f : 0f, 0f, 0f);
                    if (elapsed >= Mathf.Min(holdSeconds, 2f) && !released)
                    {
                        released = true;
                        releaseTime = elapsed;
                    }
                    break;

                case MavManeuverCase.SustainedHighGTurn:
                    // Roll in, then hold the pull for the whole run: this case is about energy bleed,
                    // so the command must never be released.
                    cmd = elapsed < 1.2f
                        ? new Vector3(-0.35f, 0f, 1f)
                        : new Vector3(-0.85f, 0f, 0f);
                    released = false;
                    break;

                case MavManeuverCase.HighAoARollYawCoupling:
                    // Establish high AoA first, then roll while holding it. This is the case the
                    // pitch-plane offline model cannot answer at all.
                    cmd = elapsed < 2f
                        ? new Vector3(-1f, 0f, 0f)
                        : new Vector3(-0.8f, 0f, hold ? 1f : 0f);
                    break;
            }

            if (selectedCase == MavManeuverCase.LowSpeedHighAoAPull
                || selectedCase == MavManeuverCase.NearStallRecovery)
            {
                throttle = 0.15f;
            }

            jet.scriptedCommandActive = true;
            jet.scriptedCommandPitchYawRoll = cmd;
            jet.scriptedCommandThrottle = throttle;
            jet.scriptedCommandCountsAsManual = treatScriptedInputAsManual;
        }

        private Sample Capture()
        {
            Sample s = new Sample();
            s.t = elapsed;

            Vector3 worldVel = body.linearVelocity;
            s.tas = worldVel.magnitude;
            s.velocityDir = s.tas > 0.01f ? worldVel / s.tas : transform.forward;

            float soundSpeed = 340.3f;
            s.mach = s.tas / soundSpeed;

            Vector3 localRates = transform.InverseTransformDirection(body.angularVelocity) * Mathf.Rad2Deg;
            s.p = localRates.z;
            s.q = localRates.x;
            s.r = localRates.y;

            s.alphaDeg = aeroBody != null ? aeroBody.debugAoADeg : jet.aoaEstimateDeg;
            s.betaDeg = aeroBody != null ? aeroBody.debugAoSDeg : jet.aosEstimateDeg;
            s.qbar = aeroBody != null ? aeroBody.debugDynamicPressure : 0f;

            s.bankDeg = MavTurnDynamicsRules.ComputeBankAngleDeg(transform.right, transform.up);
            s.pitchAttitudeDeg = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            // NOTE this is the AERO LIFT magnitude, not true Nz. Kept as the Phase 4B channel it has
            // always been so the 4B/5B comparisons stay comparable; MavForceAccountingDiagnostics
            // publishes true Nz separately.
            s.nzG = jet.CurrentAeroLiftG();

            if (aeroBody != null)
            {
                s.legacyAeroForce = aeroBody.debugLiftForce + aeroBody.debugDragForce;
                s.legacyStabilityAccel = aeroBody.debugStaticStabilityAccel;
                s.legacyAeroCurvatureAccel = aeroBody.debugAeroCurvatureAccel;
            }

            s.legacyControlTorque = jet.debugFinalTorque + jet.debugRateControlTorque;
            s.legacyVelocityAssistAccel = jet.debugLegacyCurvatureAccel;
            s.legacyAlignmentAssistTorque = jet.debugAlignmentAssistTorque;
            s.legacyAlignmentAssistScale = jet.debugAlignmentAssistScale;
            s.thrustBoostAllowance = jet.debugThrustBoostAllowance;
            s.limiterState = instructor != null ? instructor.instructorState : "no_instructor";

            // Legacy commanded pitch angular acceleration. Acceleration torque mode means the value
            // IS an angular acceleration in rad/s^2, which is precisely why it can be compared with
            // a real aerodynamic moment divided by a real inertia.
            s.legacyPitchAccelRadSec2 = s.legacyControlTorque.x;

            if (evaluateMorelliShadow)
                CaptureMorelliShadow(ref s);

            return s;
        }

        /// <summary>
        /// Evaluate the replacement aero model at the live state. Compute only.
        /// </summary>
        private void CaptureMorelliShadow(ref Sample s)
        {
            float chordOverSpan =
                MavF16MorelliReference.MeanAerodynamicChordM / MavF16MorelliReference.WingSpanM;

            float vForRates = Mathf.Max(1f, s.tas);
            float pHat = s.p * Mathf.Deg2Rad * MavF16MorelliReference.WingSpanM / (2f * vForRates);
            float qHat = s.q * Mathf.Deg2Rad * MavF16MorelliReference.MeanAerodynamicChordM / (2f * vForRates);
            float rHat = s.r * Mathf.Deg2Rad * MavF16MorelliReference.WingSpanM / (2f * vForRates);

            // Surface deflections implied by the current pilot command. This is a mapping for the
            // shadow comparison, NOT a control law: the replacement FLCS is not running here, and
            // pretending otherwise would make the shadow numbers mean something they do not.
            float elevator = -jet.scriptedCommandPitchYawRoll.x * shadowFullSurfaceDeg.x * Mathf.Deg2Rad;
            float aileron = jet.scriptedCommandPitchYawRoll.z * shadowFullSurfaceDeg.y * Mathf.Deg2Rad;
            float rudder = jet.scriptedCommandPitchYawRoll.y * shadowFullSurfaceDeg.z * Mathf.Deg2Rad;

            MavAeroCoefficients c = MavF16MorelliPolynomial.Evaluate(
                s.alphaDeg * Mathf.Deg2Rad,
                s.betaDeg * Mathf.Deg2Rad,
                elevator, aileron, rudder,
                pHat, qHat, rHat,
                MavF16MassReference.XcgCbar,
                MavF16MassReference.XcgReferenceCbar,
                chordOverSpan);

            s.cx = c.cx; s.cy = c.cy; s.cz = c.cz;
            s.cl = c.cl; s.cm = c.cm; s.cn = c.cn;

            float S = MavF16MorelliReference.WingAreaM2;
            float b = MavF16MorelliReference.WingSpanM;
            float cbar = MavF16MorelliReference.MeanAerodynamicChordM;
            float qS = s.qbar * S;

            s.shadowForceN = new Vector3(c.cx * qS, c.cy * qS, c.cz * qS);
            s.shadowMomentNm = new Vector3(c.cl * qS * b, c.cm * qS * cbar, c.cn * qS * b);
            s.shadowPitchAccelRadSec2 = s.shadowMomentNm.y / MavF16MassReference.IyKgM2;
        }

        private void FinishRun()
        {
            running = false;
            runRequested = false;
            jet.scriptedCommandActive = false;
            ComputeMetrics();
            status = "finished " + selectedCase + ": " + samples + " samples";

            if (writeCsvOnFinish)
                status += "; " + WriteCsv();
        }

        private void ComputeMetrics()
        {
            if (recorded.Count == 0)
                return;

            float aoaBaseline = Mathf.Abs(recorded[0].alphaDeg);
            float nzBaseline = Mathf.Abs(recorded[0].nzG);
            float ownershipSum = 0f;

            float aoaGrowthT = -1f;
            float nzGrowthT = -1f;

            for (int i = 0; i < recorded.Count; i++)
            {
                Sample s = recorded[i];

                if (Mathf.Abs(s.alphaDeg) > Mathf.Abs(metricPeakAoADeg)) metricPeakAoADeg = s.alphaDeg;
                if (Mathf.Abs(s.nzG) > Mathf.Abs(metricPeakNzG)) metricPeakNzG = s.nzG;
                if (Mathf.Abs(s.p) > Mathf.Abs(metricPeakRollRateDegSec)) metricPeakRollRateDegSec = s.p;
                if (Mathf.Abs(s.q) > Mathf.Abs(metricPeakPitchRateDegSec)) metricPeakPitchRateDegSec = s.q;
                if (Mathf.Abs(s.r) > Mathf.Abs(metricPeakYawRateDegSec)) metricPeakYawRateDegSec = s.r;

                if (metricTimeToRollRateResponse < 0f && Mathf.Abs(s.p) > 5f)
                    metricTimeToRollRateResponse = s.t;
                if (aoaGrowthT < 0f && Mathf.Abs(s.alphaDeg) > aoaBaseline + 1f)
                    aoaGrowthT = s.t;
                if (nzGrowthT < 0f && Mathf.Abs(s.nzG) > nzBaseline + 0.5f)
                    nzGrowthT = s.t;

                // Time beyond each breakpoint. Guarded so a missing instructor reports zero rather
                // than accumulating against a 0 threshold, which would read as "always exceeded".
                if (envelopeAoASoftLimitDeg > 0f && Mathf.Abs(s.alphaDeg) > envelopeAoASoftLimitDeg)
                    metricTimeAboveSoftAoA += Time.fixedDeltaTime;
                if (envelopeAoAHardLimitDeg > 0f && Mathf.Abs(s.alphaDeg) > envelopeAoAHardLimitDeg)
                    metricTimeAboveHardAoA += Time.fixedDeltaTime;
                if (envelopeSoftGLimit > 0f && Mathf.Abs(s.nzG) > envelopeSoftGLimit)
                    metricTimeAboveSoftG += Time.fixedDeltaTime;
                if (envelopeHardGLimit > 0f && Mathf.Abs(s.nzG) > envelopeHardGLimit)
                    metricTimeAboveHardG += Time.fixedDeltaTime;

                float aero = Mathf.Abs(s.legacyAeroCurvatureAccel);
                float legacy = Mathf.Abs(s.legacyVelocityAssistAccel);
                ownershipSum += MavTurnDynamicsRules.ComputeAeroCurvatureShare(aero, legacy);

                // Divergence: the step where the legacy stack commands more pitch angular
                // acceleration than the real aerodynamic moment could have produced.
                if (metricFirstOwnershipDivergenceT < 0f
                    && Mathf.Abs(s.shadowPitchAccelRadSec2) > 1e-4f
                    && Mathf.Abs(s.legacyPitchAccelRadSec2) > 2f * Mathf.Abs(s.shadowPitchAccelRadSec2))
                {
                    metricFirstOwnershipDivergenceT = s.t;
                }
            }

            metricTimeToAoAGrowth = aoaGrowthT;
            metricTimeFromAoAToNz = (aoaGrowthT >= 0f && nzGrowthT >= 0f) ? nzGrowthT - aoaGrowthT : -1f;
            metricTasLossMps = recorded[recorded.Count - 1].tas - recorded[0].tas;
            metricMeanAeroOwnership = ownershipSum / recorded.Count;
            metricSignReversalsAfterRelease = CountPitchRateReversalsAfterRelease();
            metricDominantLegacyContribution = DescribeDominantContribution();
        }

        private int CountPitchRateReversalsAfterRelease()
        {
            if (releaseTime < 0f)
                return 0;

            int reversals = 0;
            float previous = 0f;
            bool have = false;

            for (int i = 0; i < recorded.Count; i++)
            {
                if (recorded[i].t < releaseTime)
                    continue;

                float rate = recorded[i].q;
                if (have && Mathf.Abs(rate) > 1f && Mathf.Sign(rate) != Mathf.Sign(previous))
                    reversals++;
                if (Mathf.Abs(rate) > 1f)
                {
                    previous = rate;
                    have = true;
                }
            }
            return reversals;
        }

        /// <summary>
        /// Which legacy mechanism carried the most authority over the run. Reported by magnitude so
        /// the answer is measured rather than assumed.
        /// </summary>
        private string DescribeDominantContribution()
        {
            float control = 0f, velocityAssist = 0f, alignment = 0f, stability = 0f, aero = 0f;
            for (int i = 0; i < recorded.Count; i++)
            {
                control += Mathf.Abs(recorded[i].legacyControlTorque.x);
                velocityAssist += Mathf.Abs(recorded[i].legacyVelocityAssistAccel);
                alignment += Mathf.Abs(recorded[i].legacyAlignmentAssistTorque);
                stability += Mathf.Abs(recorded[i].legacyStabilityAccel.x);
                aero += Mathf.Abs(recorded[i].legacyAeroCurvatureAccel);
            }

            int n = Mathf.Max(1, recorded.Count);
            control /= n; velocityAssist /= n; alignment /= n; stability /= n; aero /= n;

            return string.Format(CultureInfo.InvariantCulture,
                "controlTorque={0:F3} velAssist={1:F3} alignAssist={2:F3} stability={3:F3} aeroCurv={4:F3}",
                control, velocityAssist, alignment, stability, aero);
        }

        [ContextMenu("Log Maneuver Report")]
        public void LogReport()
        {
            Debug.Log(BuildReport(), this);
        }

        public string BuildReport()
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("Phase 5B maneuver report: " + selectedCase);
            sb.AppendLine("  samples                 " + samples);
            sb.AppendLine("  time to roll response   " + F(metricTimeToRollRateResponse) + " s");
            sb.AppendLine("  time to AoA growth      " + F(metricTimeToAoAGrowth) + " s");
            sb.AppendLine("  AoA growth -> Nz build  " + F(metricTimeFromAoAToNz) + " s");
            sb.AppendLine("  peak AoA                " + F(metricPeakAoADeg) + " deg");
            sb.AppendLine("  peak Nz                 " + F(metricPeakNzG) + " g");
            sb.AppendLine("  peak p / q / r          " + F(metricPeakRollRateDegSec) + " / "
                          + F(metricPeakPitchRateDegSec) + " / " + F(metricPeakYawRateDegSec) + " deg/s");
            sb.AppendLine("  TAS change              " + F(metricTasLossMps) + " m/s");
            sb.AppendLine("  envelope in force       AoA " + F(envelopeAoASoftLimitDeg) + "/"
                          + F(envelopeAoAHardLimitDeg) + " deg, G " + F(envelopeSoftGLimit) + "/"
                          + F(envelopeHardGLimit) + ", reduction " + F(envelopeAoAPitchReduction));
            sb.AppendLine("  time above soft/hard AoA " + F(metricTimeAboveSoftAoA) + " / "
                          + F(metricTimeAboveHardAoA) + " s");
            sb.AppendLine("  time above soft/hard G   " + F(metricTimeAboveSoftG) + " / "
                          + F(metricTimeAboveHardG) + " s");
            sb.AppendLine("  reversals after release " + metricSignReversalsAfterRelease);
            sb.AppendLine("  mean aero ownership     " + F(metricMeanAeroOwnership));
            sb.AppendLine("  first divergence        " + F(metricFirstOwnershipDivergenceT) + " s");
            sb.AppendLine("  mean |contribution|     " + metricDominantLegacyContribution);
            return sb.ToString();
        }

        private static string F(float v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }

        public string WriteCsv()
        {
            StringBuilder sb = new StringBuilder(64 * 1024);
            sb.AppendLine("t_s,tas_mps,mach,qbar_pa,alpha_deg,beta_deg,p_degps,q_degps,r_degps,"
                          + "bank_deg,pitch_att_deg,nz_g,vel_x,vel_y,vel_z,"
                          + "legacy_aero_force_x,legacy_aero_force_y,legacy_aero_force_z,"
                          + "legacy_stability_accel_x,legacy_control_torque_x,legacy_control_torque_y,"
                          + "legacy_control_torque_z,legacy_vel_assist_accel,legacy_align_torque,"
                          + "legacy_align_scale,legacy_aero_curv_accel,limiter_state,thrust_boost,"
                          + "cx,cy,cz,cl,cm,cn,"
                          + "shadow_fx,shadow_fy,shadow_fz,shadow_mx,shadow_my,shadow_mz,"
                          + "shadow_pitch_accel,legacy_pitch_accel");

            CultureInfo inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < recorded.Count; i++)
            {
                Sample s = recorded[i];
                sb.AppendLine(string.Format(inv,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},"
                    + "{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},"
                    + "{28},{29},{30},{31},{32},{33},"
                    + "{34},{35},{36},{37},{38},{39},{40},{41}",
                    s.t, s.tas, s.mach, s.qbar, s.alphaDeg, s.betaDeg, s.p, s.q, s.r,
                    s.bankDeg, s.pitchAttitudeDeg, s.nzG,
                    s.velocityDir.x, s.velocityDir.y, s.velocityDir.z,
                    s.legacyAeroForce.x, s.legacyAeroForce.y, s.legacyAeroForce.z,
                    s.legacyStabilityAccel.x,
                    s.legacyControlTorque.x, s.legacyControlTorque.y, s.legacyControlTorque.z,
                    s.legacyVelocityAssistAccel, s.legacyAlignmentAssistTorque,
                    s.legacyAlignmentAssistScale, s.legacyAeroCurvatureAccel,
                    s.limiterState, s.thrustBoostAllowance,
                    s.cx, s.cy, s.cz, s.cl, s.cm, s.cn,
                    s.shadowForceN.x, s.shadowForceN.y, s.shadowForceN.z,
                    s.shadowMomentNm.x, s.shadowMomentNm.y, s.shadowMomentNm.z,
                    s.shadowPitchAccelRadSec2, s.legacyPitchAccelRadSec2));
            }

            string path = System.IO.Path.Combine(
                Application.persistentDataPath,
                "phase5b_" + selectedCase
                + (treatScriptedInputAsManual ? "_manual" : "_nonmanual") + ".csv");
            System.IO.File.WriteAllText(path, sb.ToString());
            return "csv=" + path;
        }
    }
}
