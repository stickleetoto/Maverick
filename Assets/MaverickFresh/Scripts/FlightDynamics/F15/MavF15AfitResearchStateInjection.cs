using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// WP-4A. A previously solved research equilibrium - the source's eight states plus its fixed
    /// stabilator - to initialize the research runtime body at.
    ///
    /// The eight states are exactly the WP-3B/WP-3C/WP-3D ones, in physical units: angles in
    /// radians, rates in rad/s, true airspeed in ft/s. Heading and altitude are NOT source states
    /// (the source has neither); they only place the body in the Unity world.
    /// </summary>
    [Serializable]
    public struct MavF15ResearchRuntimeInitialState
    {
        public double alphaRad;
        public double betaRad;
        public double pRadSec;
        public double qRadSec;
        public double rRadSec;
        public double thetaRad;
        public double phiRad;
        public double trueAirspeedFtPerSec;

        /// <summary>The equilibrium's fixed symmetric stabilator, degrees (source sign: + nose-down).</summary>
        public double symmetricStabilatorDeg;

        [Tooltip("NOT a source state: world heading, rad. 0 = world +Z.")]
        public double headingRad;

        [Tooltip("NOT a source state: world altitude, m. The source's 20,000-ft density label (6,096 m) keeps the standard-atmosphere altitude gate satisfied when the source density is not overridden.")]
        public float altitudeM;

        [Tooltip("The equilibrium this is, e.g. 'Baumann Table VII point 150, WP-3C solve'. Required.")]
        public string sourceNote;

        public const float DefaultAltitudeM = MavF15CoefficientFitCondition.PressureAltitudeM;
    }

    /// <summary>What an initialization did, and what the body reads back from it.</summary>
    [Serializable]
    public struct MavF15ResearchInitializationReport
    {
        public bool initialized;
        public string reason;

        public Vector3 worldPositionM;
        public Quaternion worldRotation;
        public Vector3 worldVelocityMps;
        public Vector3 worldAngularVelocityRadSec;

        [Tooltip("The body's own state builder, evaluated at the pose and motion just applied.")]
        public MavFlightState readback;

        public double alphaErrorRad;
        public double betaErrorRad;
        public double thetaErrorRad;
        public double phiErrorRad;
        public double rateErrorRadSec;
        public double trueAirspeedRelativeError;
        public double heldStabilatorErrorDeg;

        [Tooltip("Every readback error inside the float round-trip tolerance below.")]
        public bool readbackWithinTolerance;

        public MavF15ResearchRuntimePreparationReport preparation;
    }

    /// <summary>
    /// WP-4A. Initializes the research runtime body at a known research equilibrium.
    ///
    /// INITIALIZATION ONLY. It never arms the body, never steps it, and never writes the Rigidbody
    /// itself: the pose and motion go through <see cref="MavSixDoFBody.TryApplyInitialKinematicState"/>,
    /// the body's own sanctioned initializer, and the stabilator goes through
    /// <see cref="MavF15AfitResearchStaticSurfaceHold"/> into the F-15 actuator, which stays the only
    /// owner of actual surface state. Both refuse once the body is armed.
    ///
    /// Everything is validated before anything is applied: the research runtime authority, an
    /// unarmed body, a hold and an F-15 actuator on it, finite states, a true airspeed inside the
    /// source-exercised span, alpha/beta inside the transcribed span, pitch and bank away from the
    /// Euler singularity, and a stabilator inside the research demonstrated range.
    ///
    /// FRAMES. The source's body axes are x forward, y right, z down, with Euler angles psi, theta,
    /// phi in the usual 3-2-1 order. Unity's Quaternion.Euler(x, y, z) applies z, then x, then y about
    /// the fixed axes - the same as yaw, then pitch, then roll about the moving ones - and a
    /// positive Unity x rotation pitches the nose DOWN while a positive Unity z rotation rolls LEFT.
    /// Hence Euler(-theta, psi, -phi). Velocity is a true vector (<see cref="MavFlightDynamicsMath.AeroBodyVectorToUnityLocal"/>);
    /// body rates are an axial vector, so p, q, r map to Unity local (-q, r, -p) - the inverse of
    /// <see cref="MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody"/>.
    /// </summary>
    public static class MavF15AfitResearchStateInjection
    {
        public const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Float round trip through a Unity Quaternion and Transform, not a physical tolerance.
        /// </summary>
        public const double AngleToleranceRad = 2e-6;
        public const double RateToleranceRadSec = 2e-6;
        public const double SpeedRelativeTolerance = 2e-6;

        /// <summary>Pitch or bank this close to 90 deg would make the Euler set singular.</summary>
        public const double MaxAbsEulerDeg = 85.0;

        /// <summary>Axial-vector map from aero body rates (p, q, r) to Unity local angular velocity.</summary>
        public static Vector3 AeroBodyRatesToUnityLocal(Vector3 pqrRadSec)
        {
            return new Vector3(-pqrRadSec.y, pqrRadSec.z, -pqrRadSec.x);
        }

        /// <summary>
        /// The Unity world pose and motion for a research state. Pure: touches no component.
        /// </summary>
        public static bool TryComputeKinematics(
            MavF15ResearchRuntimeInitialState s,
            out Vector3 worldPositionM,
            out Quaternion worldRotation,
            out Vector3 worldVelocityMps,
            out Vector3 worldAngularVelocityRadSec,
            out string reason)
        {
            worldPositionM = Vector3.zero;
            worldRotation = Quaternion.identity;
            worldVelocityMps = Vector3.zero;
            worldAngularVelocityRadSec = Vector3.zero;

            if (!Finite(s.alphaRad) || !Finite(s.betaRad) || !Finite(s.pRadSec) || !Finite(s.qRadSec)
                || !Finite(s.rRadSec) || !Finite(s.thetaRad) || !Finite(s.phiRad)
                || !Finite(s.trueAirspeedFtPerSec) || !Finite(s.headingRad) || !Finite(s.altitudeM))
            {
                reason = "a non-finite state";
                return false;
            }

            if (Math.Abs(s.thetaRad * RadToDeg) > MaxAbsEulerDeg || Math.Abs(s.phiRad * RadToDeg) > MaxAbsEulerDeg)
            {
                reason = "pitch or bank within " + (90.0 - MaxAbsEulerDeg).ToString("F0")
                    + " deg of the Euler singularity";
                return false;
            }

            if (!(s.trueAirspeedFtPerSec > 0.0))
            {
                reason = "true airspeed must be positive";
                return false;
            }

            worldRotation = Quaternion.Euler(
                (float)(-s.thetaRad * RadToDeg),
                (float)(s.headingRad * RadToDeg),
                (float)(-s.phiRad * RadToDeg));

            double v = s.trueAirspeedFtPerSec * MavF15AfitResearchRuntimeEnvironment.FootToMeters;
            double ca = Math.Cos(s.alphaRad), sa = Math.Sin(s.alphaRad);
            double cb = Math.Cos(s.betaRad), sb = Math.Sin(s.betaRad);
            Vector3 aeroBodyVelocity = new Vector3((float)(v * ca * cb), (float)(v * sb), (float)(v * sa * cb));

            worldVelocityMps = worldRotation * MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(aeroBodyVelocity);
            worldAngularVelocityRadSec = worldRotation * AeroBodyRatesToUnityLocal(
                new Vector3((float)s.pRadSec, (float)s.qRadSec, (float)s.rRadSec));
            worldPositionM = new Vector3(0f, s.altitudeM, 0f);

            reason = "OK";
            return true;
        }

        public static MavF15ResearchInitializationReport TryInitialize(
            MavSixDoFBody body, MavF15ResearchRuntimeInitialState s)
        {
            MavF15ResearchInitializationReport report = new MavF15ResearchInitializationReport();
            string reason;

            // ---- validate everything first; nothing is applied until all of it passes ----------

            if (!MavF15ResearchRuntimeAuthority.TryGrant(body, out reason))
                return Refuse(report, reason);

            if (body.simulationEnabled)
                return Refuse(report, "the body is armed; research initialization happens before arming, and this phase never arms");

            MavF15AfitResearchStaticSurfaceHold hold = body.GetComponent<MavF15AfitResearchStaticSurfaceHold>();
            if (hold == null)
                return Refuse(report, "no MavF15AfitResearchStaticSurfaceHold on the body: every research equilibrium needs its stabilator");

            MavF15ControlActuator actuator = body.controlSurfaceActuator as MavF15ControlActuator;
            if (actuator == null)
                return Refuse(report, "the body's surface actuator is not the F-15 actuator that owns actual surface state");

            if (!(s.trueAirspeedFtPerSec >= MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedFtPerSec
                  && s.trueAirspeedFtPerSec <= MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedFtPerSec))
            {
                return Refuse(report, "true airspeed " + s.trueAirspeedFtPerSec.ToString("F1")
                    + " ft/s is outside the source-exercised "
                    + MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedFtPerSec.ToString("F1") + "-"
                    + MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedFtPerSec.ToString("F1") + " ft/s");
            }

            string spanReason = "non-finite alpha or beta";
            if (!Finite(s.alphaRad) || !Finite(s.betaRad)
                || !MavF15BaumannMach06Domain.IsInsideTranscribedSpan((float)s.alphaRad, (float)s.betaRad, out spanReason))
            {
                return Refuse(report, "alpha/beta outside the transcribed span: " + spanReason);
            }

            MavF15ResearchStaticSurfaceSetting setting;
            if (!MavF15ResearchStaticSurfaceSetting.TryCreate(
                    (float)s.symmetricStabilatorDeg, s.sourceNote, out setting, out reason))
                return Refuse(report, reason);

            Vector3 position, velocity, angularVelocity;
            Quaternion rotation;
            if (!TryComputeKinematics(s, out position, out rotation, out velocity, out angularVelocity, out reason))
                return Refuse(report, reason);

            // ---- apply: the hold, then the body's own initializer, then the actuator ----------

            if (!hold.TryEngage(setting, out reason))
                return Refuse(report, reason);

            if (!body.TryApplyInitialKinematicState(position, rotation, velocity, angularVelocity, out reason))
                return Refuse(report, reason);

            actuator.StepActuator(0f);

            report.worldPositionM = position;
            report.worldRotation = rotation;
            report.worldVelocityMps = velocity;
            report.worldAngularVelocityRadSec = angularVelocity;

            // ---- read back through the body's own state builder --------------------------------

            report.readback = ReadBack(body);
            MavFlightState r = report.readback;
            report.alphaErrorRad = Math.Abs(r.alphaRad - s.alphaRad);
            report.betaErrorRad = Math.Abs(r.betaRad - s.betaRad);
            report.thetaErrorRad = Math.Abs(r.attitude.pitchAttitudeRad - s.thetaRad);
            report.phiErrorRad = Math.Abs(r.attitude.bankAngleRad - s.phiRad);
            report.rateErrorRadSec = Math.Max(Math.Abs(r.aeroBodyRatesRadSec.x - s.pRadSec),
                Math.Max(Math.Abs(r.aeroBodyRatesRadSec.y - s.qRadSec), Math.Abs(r.aeroBodyRatesRadSec.z - s.rRadSec)));
            report.trueAirspeedRelativeError = Math.Abs(
                r.trueAirspeedMps / MavF15AfitResearchRuntimeEnvironment.FootToMeters - s.trueAirspeedFtPerSec)
                / s.trueAirspeedFtPerSec;
            report.heldStabilatorErrorDeg = Math.Abs(
                actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg - s.symmetricStabilatorDeg);

            report.readbackWithinTolerance =
                report.alphaErrorRad <= AngleToleranceRad && report.betaErrorRad <= AngleToleranceRad
                && report.thetaErrorRad <= AngleToleranceRad && report.phiErrorRad <= AngleToleranceRad
                && report.rateErrorRadSec <= RateToleranceRadSec
                && report.trueAirspeedRelativeError <= SpeedRelativeTolerance
                && actuator.debugResearchStaticHold
                && report.heldStabilatorErrorDeg <= 1e-6 * Math.Max(1.0, Math.Abs(s.symmetricStabilatorDeg));

            report.preparation = MavF15AfitResearchRuntimePreparation.Evaluate(body);
            report.initialized = true;
            report.reason = "initialized at " + s.sourceNote + " (not armed, not stepped; "
                + (report.readbackWithinTolerance ? "readback within float tolerance" : "READBACK OUT OF TOLERANCE") + ")";
            return report;
        }

        /// <summary>
        /// The flight state the body would build at its current pose and motion, in the environment
        /// its profile provider resolves - the same builder and the same environment the physics step
        /// uses, without stepping.
        /// </summary>
        public static MavFlightState ReadBack(MavSixDoFBody body)
        {
            Transform t = body.transform;
            Rigidbody rb = body.GetComponent<Rigidbody>();
            MavFlightEnvironment environment = body.ResolveEnvironment(t.position.y);

            Vector3 worldVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 worldRate = rb != null ? rb.angularVelocity : Vector3.zero;

            return MavSixDoFBody.BuildFlightState(
                t.position,
                worldVelocity,
                t.InverseTransformDirection(worldVelocity),
                t.InverseTransformDirection(worldRate),
                t.forward,
                t.up,
                t.right,
                environment.atmosphere);
        }

        private static MavF15ResearchInitializationReport Refuse(MavF15ResearchInitializationReport report, string reason)
        {
            report.initialized = false;
            report.reason = "research initialization refused: " + reason;
            return report;
        }

        private static bool Finite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
