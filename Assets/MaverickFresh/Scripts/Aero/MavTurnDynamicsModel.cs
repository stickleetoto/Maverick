using UnityEngine;

namespace MaverickFresh
{
    /// <summary>Everything the turn-entry model needs about one aircraft configuration.</summary>
    [System.Serializable]
    public struct MavTurnModelConfig
    {
        public float massKg;
        public float wingAreaSqm;
        public float altitudeM;

        // Aero curve (MavAeroBody).
        public float clSlopePerDeg;
        public float zeroLiftAoADeg;
        public float stallAoADeg;
        public float fullStallAoADeg;
        public float postStallLiftFactor;
        public float maxAbsCl;
        public float cd0;
        public float aspectRatio;
        public float oswaldEfficiency;
        public float postStallDrag;
        public float maxLiftG;
        public float maxDragG;

        // Ownership.
        public float aeroBlend;
        public float liftBlend;
        public float dragBlend;
        public bool phase4B;

        // Legacy velocity-turn assist (MavMouseFlightJet).
        public bool useVelocityTurnAssist;
        public float velocityTurnAssistStrength;
        public float velocityTurnAssistMaxAccel;
        public float velocityTurnAssistMinSpeed;
        public float velocityTurnAssistFullSpeed;
        public float velocityTurnAssistInputFactor;

        // Pitch rate controller.
        public float targetPitchRateDeg;
        public float rateControlP;
        public float rateControlD;
        public float maxRateControlTorque;
        public float rigidAngularDamping;
        public float angularRateDampingPitch;
        public float releaseRateNullingScale;
        public float releaseCommandThreshold;

        // Aerodynamic static stability, and the legacy assist it replaces.
        public bool useAeroStaticStability;
        public float pitchStabilityStrength;
        public float stabilityReferenceDynamicPressure;
        public float maxStabilityAngularAccel;
        public float forwardAlignmentAssist;
        public float alignmentAssistFloorAtFullAero;

        // Instructor limiters. Without these a held full-pitch command drives AoA past stall, and
        // the transient numbers stop meaning anything.
        public float aoaSoftLimitDeg;
        public float aoaHardLimitDeg;
        public float aoaPitchReduction;
        public float sustainedGLimit;
        public float hardGLimit;
        public float gPitchReduction;

        // Propulsion, for the sustained-turn energy question.
        public float thrustN;
        public float throttle01;

        /// <summary>The F-16 as the catalog and scene configure it. Phase flag selects the tuning.</summary>
        public static MavTurnModelConfig F16(bool phase4B)
        {
            MavTurnModelConfig c = new MavTurnModelConfig();
            c.massKg = 9800f;
            c.wingAreaSqm = 27.9f;
            c.altitudeM = 3000f;

            c.clSlopePerDeg = 0.078f;
            c.zeroLiftAoADeg = 0f;
            c.stallAoADeg = 17f;
            c.fullStallAoADeg = 29f;
            c.postStallLiftFactor = 0.28f;
            c.maxAbsCl = 1.65f;
            c.cd0 = 0.026f;
            c.aspectRatio = 3.2f;
            c.oswaldEfficiency = 0.78f;
            c.postStallDrag = 0.22f;
            c.maxLiftG = 9.4f;
            c.maxDragG = 3.0f;

            c.phase4B = phase4B;
            if (phase4B)
            {
                c.aeroBlend = 0.95f;
                c.liftBlend = 0.92f;
                c.dragBlend = 0.95f;
                c.releaseRateNullingScale = 0.35f;
            }
            else
            {
                c.aeroBlend = 0.47f;
                c.liftBlend = 0.60f;
                c.dragBlend = 0.74f;
                c.releaseRateNullingScale = 1f;
            }

            c.useVelocityTurnAssist = true;
            c.velocityTurnAssistStrength = 0.018f;
            c.velocityTurnAssistMaxAccel = 14f;
            c.velocityTurnAssistMinSpeed = 80f;
            c.velocityTurnAssistFullSpeed = 230f;
            c.velocityTurnAssistInputFactor = 0.58f;

            c.targetPitchRateDeg = 56f;
            c.rateControlP = 0.62f;
            c.rateControlD = 0.08f;
            c.maxRateControlTorque = 48f;
            c.rigidAngularDamping = 1.05f;
            c.angularRateDampingPitch = 0.09f;
            c.releaseCommandThreshold = 0.06f;

            // The alignment assist and its aerodynamic replacement are the Phase 4B pair: the assist
            // is migrated down, and static stability takes over the job of restoring AoA.
            c.forwardAlignmentAssist = 0.008f;
            c.alignmentAssistFloorAtFullAero = 0.15f;
            c.stabilityReferenceDynamicPressure = 30000f;
            c.maxStabilityAngularAccel = 3.5f;
            if (phase4B)
            {
                c.useAeroStaticStability = true;
                c.pitchStabilityStrength = 0.055f;
            }
            else
            {
                c.useAeroStaticStability = false;
                c.pitchStabilityStrength = 0f;
            }

            c.aoaSoftLimitDeg = 22f;
            c.aoaHardLimitDeg = 30f;
            c.aoaPitchReduction = phase4B ? 0.05f : 0.45f;
            c.sustainedGLimit = 8.8f;
            c.hardGLimit = 11.2f;
            c.gPitchReduction = 0.30f;

            c.thrustN = 123000f;
            c.throttle01 = 1f;
            return c;
        }
    }

    /// <summary>One step of the turn-entry model.</summary>
    [System.Serializable]
    public struct MavTurnModelState
    {
        public float timeSeconds;
        public float speed;
        public float aoaDeg;
        public float pitchRateDegPerSec;
        public float aeroCurvatureAccel;
        public float legacyCurvatureAccel;
        public float totalCurvatureAccel;
        public float turnRateDegPerSec;
        public float loadFactorG;
        public float aeroCurvatureShare;
        public float limiterReduction;
    }

    /// <summary>
    /// A pure pitch-plane turn-entry model of the live legacy stack.
    ///
    /// WHY THIS EXISTS. "Turn rate must not be reached instantly" is a claim about a transient, and a
    /// transient cannot be checked by inspecting configuration values. This integrates the actual
    /// mechanism using the production lift and drag curves from MavAeroBody, so the sequence the
    /// requirement asks for - rate builds, AoA builds, lift builds, trajectory bends - is either
    /// present in the numbers or it is not.
    ///
    /// THE ONE EQUATION THAT MATTERS:
    ///
    ///     dAoA/dt = q - (aeroCurvature + legacyCurvature) / V
    ///
    /// Angle of attack is the gap between where the nose points and where the aircraft is going. The
    /// nose rotates at pitch rate q; the velocity vector rotates at (curvature accel / V). AoA is the
    /// integral of the difference, and lift is a function of AoA.
    ///
    /// That is precisely why the legacy velocity-turn assist felt rail-like. It curves the velocity
    /// vector DIRECTLY, which appears in that equation as a term that cancels q - so AoA never opens,
    /// lift never builds, and the turn arrives immediately without ever having been aerodynamic. The
    /// assist does not merely add curvature; it suppresses the mechanism that was supposed to produce
    /// it. Reducing its ownership is therefore not a cosmetic gain change.
    ///
    /// SCOPE. Pitch-plane only: it assumes bank is established and asks what the pitch axis does. It
    /// does not model roll-in, coupling, or the instructor's command shaping, so it under-states total
    /// turn-entry time rather than over-stating it. It is a mechanism check, not a flight model, and
    /// it is not a substitute for flying the aircraft.
    /// </summary>
    public static class MavTurnDynamicsModel
    {
        public const float G = 9.80665f;

        /// <summary>Turn authority aerodynamics exercises under this configuration.</summary>
        public static float AeroAuthority(MavTurnModelConfig c)
        {
            return MavAeroBody.ComputeAeroTurnAuthority(true, c.aeroBlend, c.liftBlend, c.phase4B);
        }

        /// <summary>Ownership left to the legacy velocity-turn assist.</summary>
        public static float LegacyAuthority(MavTurnModelConfig c)
        {
            return 1f - AeroAuthority(c);
        }

        /// <summary>
        /// The legacy forward-alignment assist's restoring angular acceleration, rad/s^2, following
        /// the production shape: it fades in over 8-75 degrees of nose-off angle and is clamped.
        /// </summary>
        public static float AlignmentAssistAccel(MavTurnModelConfig c, float aoaDeg, float scale)
        {
            float angle = Mathf.Abs(aoaDeg);
            if (angle <= 3f)
                return 0f;

            float angleT = Mathf.Clamp01(Mathf.InverseLerp(8f, 75f, angle));
            return Mathf.Min(c.forwardAlignmentAssist * scale * angleT, 0.35f);
        }

        /// <summary>Aerodynamic curvature acceleration at a given AoA and speed, m/s^2.</summary>
        public static float AeroCurvatureAccel(MavTurnModelConfig c, float aoaDeg, float speed)
        {
            float density = MavAeroBody.ComputeAirDensity(c.altitudeM, 1.225f, 8500f, 0.18f);
            float q = 0.5f * density * speed * speed;
            float qS = q * Mathf.Max(0.01f, c.wingAreaSqm);

            float cl = MavAeroBody.ComputeLiftCoefficient(
                aoaDeg, c.zeroLiftAoADeg, c.clSlopePerDeg, c.stallAoADeg, c.fullStallAoADeg,
                c.postStallLiftFactor, c.maxAbsCl);

            float liftN = Mathf.Abs(cl) * qS * c.liftBlend * c.aeroBlend;
            float maxLiftN = c.massKg * G * Mathf.Max(0.1f, c.maxLiftG);
            liftN = Mathf.Min(liftN, maxLiftN);

            return liftN / Mathf.Max(1f, c.massKg);
        }

        /// <summary>Aerodynamic drag deceleration at a given AoA and speed, m/s^2.</summary>
        public static float AeroDragDecel(MavTurnModelConfig c, float aoaDeg, float speed)
        {
            float density = MavAeroBody.ComputeAirDensity(c.altitudeM, 1.225f, 8500f, 0.18f);
            float q = 0.5f * density * speed * speed;
            float qS = q * Mathf.Max(0.01f, c.wingAreaSqm);

            float cl = MavAeroBody.ComputeLiftCoefficient(
                aoaDeg, c.zeroLiftAoADeg, c.clSlopePerDeg, c.stallAoADeg, c.fullStallAoADeg,
                c.postStallLiftFactor, c.maxAbsCl);

            float stallFactor = Mathf.InverseLerp(
                c.stallAoADeg, Mathf.Max(c.stallAoADeg + 0.1f, c.fullStallAoADeg), Mathf.Abs(aoaDeg));

            float cd = MavAeroBody.ComputeDragCoefficient(
                cl, c.cd0, c.aspectRatio, c.oswaldEfficiency, c.postStallDrag, stallFactor, 0f, 0f);

            float dragN = cd * qS * c.dragBlend * c.aeroBlend;
            float maxDragN = c.massKg * G * Mathf.Max(0.1f, c.maxDragG);
            dragN = Mathf.Min(dragN, maxDragN);

            return dragN / Mathf.Max(1f, c.massKg);
        }

        /// <summary>
        /// The legacy velocity-turn assist's curvature acceleration, following the production
        /// formula: it scales with V*sin(noseOffAngle), fades in with speed and with the nose-off
        /// angle, and is clamped. The nose-off angle in the pitch plane IS the angle of attack.
        /// </summary>
        public static float LegacyCurvatureAccel(
            MavTurnModelConfig c, float aoaDeg, float speed, float commandMagnitude)
        {
            if (!c.useVelocityTurnAssist || speed < c.velocityTurnAssistMinSpeed)
                return 0f;

            float angleDeg = Mathf.Abs(aoaDeg);
            if (angleDeg < 1f)
                return 0f;

            float centripetalNeeded = speed * Mathf.Sin(angleDeg * Mathf.Deg2Rad);

            float speedT = Mathf.InverseLerp(
                c.velocityTurnAssistMinSpeed,
                Mathf.Max(c.velocityTurnAssistMinSpeed + 1f, c.velocityTurnAssistFullSpeed),
                speed);

            float inputT = Mathf.Clamp01(
                Mathf.Clamp01(commandMagnitude) / Mathf.Max(0.01f, c.velocityTurnAssistInputFactor));
            float inputFactor = Mathf.Lerp(0.30f, 1f, inputT);
            float angleT = Mathf.InverseLerp(4f, 70f, angleDeg);

            float accel = centripetalNeeded
                * c.velocityTurnAssistStrength
                * LegacyAuthority(c)
                * Mathf.Clamp01(speedT)
                * Mathf.Clamp01(angleT)
                * inputFactor;

            return Mathf.Min(accel, Mathf.Max(0.1f, c.velocityTurnAssistMaxAccel));
        }

        /// <summary>
        /// Integrates a pitch-plane turn entry.
        ///
        /// <paramref name="commandHold"/> is how long the pitch command is held at full deflection;
        /// after that it returns to neutral, which is what makes the same run usable for the
        /// release-to-neutral scenario.
        /// </summary>
        public static MavTurnModelState[] Simulate(
            MavTurnModelConfig c,
            float initialSpeed,
            float commandMagnitude,
            float commandHoldSeconds,
            float durationSeconds,
            float dt)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(durationSeconds / Mathf.Max(0.001f, dt)));
            MavTurnModelState[] output = new MavTurnModelState[steps];

            float speed = initialSpeed;
            float aoaDeg = 0f;
            float pitchRateDeg = 0f;
            float time = 0f;

            for (int i = 0; i < steps; i++)
            {
                float command = time < commandHoldSeconds ? commandMagnitude : 0f;

                // --- instructor limiters ---------------------------------------------------------
                // Applied to the command before the rate controller sees it, which is where
                // MavInstructorController applies them.
                float previousLoadG = i > 0 ? output[i - 1].loadFactorG : 0f;
                float limiterReduction = MavTurnDynamicsRules.ComputePitchCommandReduction(
                    aoaDeg, previousLoadG, c.aoaSoftLimitDeg, c.aoaHardLimitDeg, c.aoaPitchReduction,
                    c.sustainedGLimit, c.hardGLimit, c.gPitchReduction);
                command *= limiterReduction;

                // --- pitch rate controller, as MavMouseFlightJet runs it ------------------------
                float targetRateDeg = command * c.targetPitchRateDeg;
                float rateErrorDeg = targetRateDeg - pitchRateDeg;

                float nullingScale = MavTurnDynamicsRules.ComputeRateNullingScale(
                    Mathf.Abs(command), c.releaseCommandThreshold, c.releaseRateNullingScale);

                float torque = rateErrorDeg * c.rateControlP * nullingScale
                               - pitchRateDeg * c.rateControlD;
                torque = Mathf.Clamp(torque, -c.maxRateControlTorque, c.maxRateControlTorque);

                // Rigidbody angular damping and the semi-aero pitch rate damper both act on the rate.
                float dampingDeg = pitchRateDeg * (c.rigidAngularDamping + c.angularRateDampingPitch);

                // The two mechanisms that restore the nose toward the velocity vector. Phase 4B
                // migrates the first down and turns the second on; they are a replacement pair, and
                // the model runs both so the migration can be seen to conserve the job rather than
                // abandon it.
                float alignmentScale = MavTurnDynamicsRules.ComputeAlignmentAssistScale(
                    LegacyAuthority(c), c.alignmentAssistFloorAtFullAero);
                float alignmentAccelDeg =
                    -Mathf.Sign(aoaDeg) * AlignmentAssistAccel(c, aoaDeg, alignmentScale) * Mathf.Rad2Deg;

                float density = MavAeroBody.ComputeAirDensity(c.altitudeM, 1.225f, 8500f, 0.18f);
                float qNow = 0.5f * density * speed * speed;
                float stabilityAccelDeg = -MavAeroBody.ComputeStabilityAccel(
                    aoaDeg, c.pitchStabilityStrength,
                    qNow / Mathf.Max(1f, c.stabilityReferenceDynamicPressure),
                    c.useAeroStaticStability ? c.aeroBlend : 0f,
                    c.maxStabilityAngularAccel) * Mathf.Rad2Deg;

                float pitchAccelDeg = torque * Mathf.Rad2Deg - dampingDeg
                                      + alignmentAccelDeg + stabilityAccelDeg;

                pitchRateDeg += pitchAccelDeg * dt;

                // --- curvature ----------------------------------------------------------------
                float aeroAccel = AeroCurvatureAccel(c, aoaDeg, speed);
                float legacyAccel = LegacyCurvatureAccel(c, aoaDeg, speed, Mathf.Abs(command));
                float totalAccel = aeroAccel + legacyAccel;

                // THE equation: AoA is the integral of (nose rotation - velocity rotation).
                float velocityRotationDeg = (totalAccel / Mathf.Max(1f, speed)) * Mathf.Rad2Deg;
                aoaDeg += (pitchRateDeg - velocityRotationDeg) * dt;
                aoaDeg = Mathf.Clamp(aoaDeg, -40f, 40f);

                // --- energy -------------------------------------------------------------------
                float dragDecel = AeroDragDecel(c, aoaDeg, speed);
                float thrustAccel = (c.thrustN * Mathf.Clamp01(c.throttle01)) / Mathf.Max(1f, c.massKg);
                speed += (thrustAccel - dragDecel) * dt;
                speed = Mathf.Max(1f, speed);

                MavTurnModelState st = new MavTurnModelState();
                st.timeSeconds = time;
                st.speed = speed;
                st.aoaDeg = aoaDeg;
                st.pitchRateDegPerSec = pitchRateDeg;
                st.aeroCurvatureAccel = aeroAccel;
                st.legacyCurvatureAccel = legacyAccel;
                st.totalCurvatureAccel = totalAccel;
                st.turnRateDegPerSec =
                    MavTurnDynamicsRules.ComputeTurnRateDegPerSec(totalAccel, speed);
                st.loadFactorG = totalAccel / G;
                st.aeroCurvatureShare =
                    MavTurnDynamicsRules.ComputeAeroCurvatureShare(aeroAccel, legacyAccel);
                st.limiterReduction = limiterReduction;
                output[i] = st;

                time += dt;
            }

            return output;
        }

        /// <summary>Peak turn rate reached over a run, deg/s.</summary>
        public static float PeakTurnRate(MavTurnModelState[] run)
        {
            float peak = 0f;
            for (int i = 0; i < run.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(run[i].turnRateDegPerSec));

            return peak;
        }

        /// <summary>
        /// Time to first reach a fraction of the run's peak turn rate. This is the turn-entry
        /// transient: a rail reaches its peak almost immediately, an aircraft takes time.
        /// </summary>
        public static float TimeToFractionOfPeakTurnRate(MavTurnModelState[] run, float fraction)
        {
            float target = PeakTurnRate(run) * Mathf.Clamp01(fraction);
            if (target <= 0.0001f)
                return -1f;

            for (int i = 0; i < run.Length; i++)
            {
                if (Mathf.Abs(run[i].turnRateDegPerSec) >= target)
                    return run[i].timeSeconds;
            }

            return -1f;
        }

        /// <summary>Sample nearest a given time, for reading a run at a chosen instant.</summary>
        public static MavTurnModelState At(MavTurnModelState[] run, float timeSeconds)
        {
            MavTurnModelState best = run.Length > 0 ? run[0] : new MavTurnModelState();
            float bestGap = float.MaxValue;

            for (int i = 0; i < run.Length; i++)
            {
                float gap = Mathf.Abs(run[i].timeSeconds - timeSeconds);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = run[i];
                }
            }

            return best;
        }

        /// <summary>
        /// How many times the pitch rate changes sign after the command is released. Zero or one is a
        /// clean decay; repeated sign changes are the oscillation the requirement forbids.
        /// </summary>
        public static int CountRateSignReversalsAfter(MavTurnModelState[] run, float afterSeconds)
        {
            int reversals = 0;
            float previous = 0f;
            bool havePrevious = false;

            for (int i = 0; i < run.Length; i++)
            {
                if (run[i].timeSeconds < afterSeconds)
                    continue;

                float rate = run[i].pitchRateDegPerSec;
                if (Mathf.Abs(rate) < 0.05f)
                    continue;

                if (havePrevious && Mathf.Sign(rate) != Mathf.Sign(previous))
                    reversals++;

                previous = rate;
                havePrevious = true;
            }

            return reversals;
        }
    }
}
