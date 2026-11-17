using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>Provenance label of every mouse-aim guidance number. None of them is aircraft data.</summary>
    public static class MavMouseAimProvenance
    {
        public const string MaverickTunedNonAuthoritative = "MAVERICK_TUNED_NON_AUTHORITATIVE";

        public const string Identity =
            "Maverick realistic mouse instructor v2 (aim guidance -> normalized pilot intent; " + MaverickTunedNonAuthoritative
            + ") - NOT War Thunder's instructor, NOT an F-15 / F-16 production FCS";
    }

    /// <summary>
    /// What the aim direction is compared against. The two are different physical directions and are never conflated:
    /// at the F-15 research trim the nose sits about 17.5 deg above the velocity vector.
    /// </summary>
    public enum MavAimReference
    {
        /// <summary>The aircraft's velocity vector (flight path). First-slice default: the mouse says where to FLY.</summary>
        FlightPath = 0,

        /// <summary>The body +X axis (nose). Pointing semantics; with this, a level aim asks for a nose-down flight path at high alpha.</summary>
        Nose = 1
    }

    public enum MavAimGuidanceMode
    {
        Invalid = 0,
        OnTarget = 1,
        Tracking = 2,
        Reversal = 3,
        HandsOff = 4
    }

    [Flags]
    public enum MavAimGuidanceLimiter
    {
        None = 0,
        FlightPathRateCapped = 1 << 0,
        PitchStickSaturated = 1 << 1,
        RollStickSaturated = 1 << 2,
        YawStickSaturated = 1 << 3,
        PushLimited = 1 << 4,
        LowSpeedPullCap = 1 << 5,
        HighAlphaPullCap = 1 << 6,
        LowAlphaPushCap = 1 << 7,
        CommandRateLimited = 1 << 8,
        NoseReferenceFallback = 1 << 9,
        InvertedRollDeferred = 1 << 10
    }

    /// <summary>
    /// Every number of the mouse-aim guidance. ALL of it is <see cref="MavMouseAimProvenance.MaverickTunedNonAuthoritative"/>:
    /// controller / gameplay tuning, no aircraft source. The envelope soft caps are COMMAND caps that keep the
    /// requested pilot intent away from the edges of the aerodynamic model's admitted domain; they are not
    /// aircraft limits, they create no force, and the aircraft may still leave the domain through its own dynamics.
    /// </summary>
    [Serializable]
    public struct MavMouseAimGuidanceConfig
    {
        [Header("Target semantics")]
        public MavAimReference aimReference;

        [Tooltip("Below this true airspeed the flight path is not a usable direction and the nose is used instead (flagged). Numerical guard, m/s.")]
        public float minimumFlightPathSpeedMps;

        [Header("Outer loop (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Desired flight-path turn rate per radian of aim error, 1/s. 0.6 means the error would close with a ~1.7-s time constant if the aircraft could follow.")]
        public float flightPathRateGainPerSec;

        [Tooltip("Desired bank-closure time constant, s: bank error / this = requested roll rate.")]
        public float bankTimeConstantSeconds;

        [Tooltip("Gravity compensation of the requested lift direction (1 = full). 0 reduces the instructor to the naive 'point lift at the target' rule.")]
        public float gravityCompensation;

        [Tooltip("OFF (0) by default. Integral of the pitch-plane aim error, rad/s of path-rate command per rad of error per s, with anti-windup (frozen while saturated / soft-capped in the error's direction, leaked away far from the target). Evaluated and NOT adopted: at fixed research thrust it holds the aim path by pulling, drains the speed into the low-speed cap and leaves larger errors than the proportional loop. The proportional loop's standing offset is the aircraft's energy deficit relative to its trim path, which the model must show.")]
        public float pitchIntegralGainPerSec2;

        [Tooltip("The integral acts only within this aim error, deg (fine tracking); beyond it it leaks away.")]
        public float pitchIntegralActiveBelowDeg;

        [Tooltip("Largest integral contribution, in normalized pitch stick.")]
        [Range(0f, 1f)] public float pitchIntegralLimitStick;

        [Tooltip("Leak time constant of the integral outside its active band, s.")]
        public float pitchIntegralLeakSeconds;

        [Tooltip("Below this aim error a lift request pointing below the upright direction is never flown inverted: the aircraft unloads and pushes instead, deg.")]
        public float invertedRollMinErrorDeg;

        [Tooltip("From this aim error the gravity-compensated lift request is flown as computed (roll inverted and pull for large nose-down errors), deg.")]
        public float invertedRollFullErrorDeg;

        [Header("Stick scale of the bound rate-command law (synced from the law; not guidance tuning)")]
        [Tooltip("Pitch rate the bound control law commands at full stick, deg/s. The F-15 rig copies it from MavF15PilotControlGainsV2.")]
        public float pitchRateAtFullStickDegSec;

        [Tooltip("Roll rate the bound control law commands at full stick, deg/s. The F-15 rig copies it from MavF15PilotControlGainsV2.")]
        public float rollRateAtFullStickDegSec;

        [Header("Stick limits (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Range(0f, 1f)] public float pitchStickLimit;
        [Range(0f, 1f)] public float rollStickLimit;
        [Range(0f, 1f)] public float yawStickLimit;

        [Tooltip("Largest nose-down stick the guidance asks for: large nose-down errors are flown roll-and-pull instead of sustained push.")]
        [Range(0f, 1f)] public float maxPushStick;

        [Header("Fine aim yaw (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Pedal per degree of lateral aim error, active only near the target.")]
        public float fineAimYawPerDeg;
        public float fineAimFadeStartDeg;
        public float fineAimFadeEndDeg;

        [Header("Mode thresholds")]
        public float onTargetDeg;

        [Tooltip("Hysteresis band near 180 deg (target behind, or lift exactly opposite the request), deg: the previous side is kept so the command does not thrash.")]
        public float reversalHysteresisDeg;

        [Tooltip("Required-lift magnitude (in g) below which the roll request fades out: the request direction is undefined at zero.")]
        public float liftFadeLowG;
        public float liftFadeHighG;

        [Header("Command smoothing (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        public float commandTimeConstantSeconds;

        [Tooltip("Largest change of any normalized stick channel, per second.")]
        public float commandRatePerSec;

        [Header("Envelope soft caps - command caps protecting the model's admitted domain (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        public bool enableEnvelopeSoftCaps;

        [Tooltip("At and above this TAS the pull is not capped, m/s.")]
        public float lowSpeedFullPullMps;

        [Tooltip("At and below this TAS the pitch-stick ceiling is -lowSpeedNoseDownStick, m/s. Protects the research model's lowest source-exercised TAS (218.5 ft/s = 66.60 m/s), below which the F-15 research aero and thrust refuse.")]
        public float lowSpeedZeroPullMps;

        [Range(0f, 1f)] public float lowSpeedNoseDownStick;

        [Tooltip("Above this alpha the pull starts to fade, deg. Protects against flying the research model far outside the alpha it was exercised at (V2 Play Mode: 15.5 - 20.2 deg). Not an F-15 AoA limit.")]
        public float alphaPullFadeStartDeg;
        public float alphaPullZeroDeg;

        [Tooltip("Below this alpha the push starts to fade, deg. Protects the transcribed alpha span floor (-4 deg), below which the research aero refuses.")]
        public float alphaPushFullDeg;
        public float alphaPushZeroDeg;

        public static MavMouseAimGuidanceConfig Default()
        {
            return new MavMouseAimGuidanceConfig
            {
                aimReference = MavAimReference.FlightPath,
                minimumFlightPathSpeedMps = 5f,

                flightPathRateGainPerSec = 0.6f,
                bankTimeConstantSeconds = 1.0f,
                gravityCompensation = 1f,
                pitchIntegralGainPerSec2 = 0f,
                pitchIntegralActiveBelowDeg = 12f,
                pitchIntegralLimitStick = 0.4f,
                pitchIntegralLeakSeconds = 1f,
                invertedRollMinErrorDeg = 30f,
                invertedRollFullErrorDeg = 60f,

                pitchRateAtFullStickDegSec = 7f,
                rollRateAtFullStickDegSec = 15f,

                pitchStickLimit = 1f,
                rollStickLimit = 1f,
                yawStickLimit = 0.15f,
                maxPushStick = 0.6f,

                fineAimYawPerDeg = 0.05f,
                fineAimFadeStartDeg = 2f,
                fineAimFadeEndDeg = 5f,

                onTargetDeg = 0.5f,
                reversalHysteresisDeg = 10f,
                liftFadeLowG = 0.05f,
                liftFadeHighG = 0.25f,

                commandTimeConstantSeconds = 0.08f,
                commandRatePerSec = 4f,

                enableEnvelopeSoftCaps = true,
                lowSpeedFullPullMps = 82f,
                lowSpeedZeroPullMps = 72f,
                lowSpeedNoseDownStick = 0.1f,
                alphaPullFadeStartDeg = 24f,
                alphaPullZeroDeg = 28f,
                alphaPushFullDeg = 4f,
                alphaPushZeroDeg = 0f
            };
        }

        public struct Entry
        {
            public string name;
            public float value;
            public string unit;
            public string why;
            public string protects;
        }

        /// <summary>Every tuned number with its reason; validation checks the list covers every float field.</summary>
        public Entry[] Entries()
        {
            const string none = "-";
            return new[]
            {
                E("minimumFlightPathSpeedMps", minimumFlightPathSpeedMps, "m/s", "numerical guard: below it the velocity direction is noise", none),
                E("flightPathRateGainPerSec", flightPathRateGainPerSec, "1/s", "~1.7-s outer-loop time constant: with the ~1-s bank and V2 roll lags in series, a bank-to-turn heading loop at 0.6/s keeps damping near 0.45 (1/s estimated ~0.35); also slower than the V2 pitch loop, which settles in ~2.7 s", none),
                E("bankTimeConstantSeconds", bankTimeConstantSeconds, "s", "15 deg of bank error asks the full V2 roll rate (15 deg/s): the bank loop is no faster than the law can roll", none),
                E("gravityCompensation", gravityCompensation, "-", "full: the lift is asked to hold the aircraft up as well as turn it, so small errors bank little and level turns emerge", none),
                E("pitchIntegralGainPerSec2", pitchIntegralGainPerSec2, "1/s^2", "OFF. Play Mode evidence (0.15): 15-deg turns ended 11.6 deg off instead of 3.2, the moving-target lag grew from 5.8-7.8 to 15-20 deg, the speed fell into the low-speed cap - at fixed thrust the integral holds the path by bleeding energy", "-"),
                E("pitchIntegralActiveBelowDeg", pitchIntegralActiveBelowDeg, "deg", "fine tracking only: big manoeuvres never wind it up", "no integrator wind-up"),
                E("pitchIntegralLimitStick", pitchIntegralLimitStick, "-", "at most 0.4 stick of sustained trim offset", "no integrator wind-up"),
                E("pitchIntegralLeakSeconds", pitchIntegralLeakSeconds, "s", "outside the band the integral decays within ~1 s", "no integrator wind-up"),
                E("invertedRollMinErrorDeg", invertedRollMinErrorDeg, "deg", "Play Mode evidence: at 112 m/s an 8-12 deg nose-down correction made V w exceed g and rolled the aircraft to 37 deg bank; below 30 deg it now unloads and pushes", "no inverted roll for a modest correction"),
                E("invertedRollFullErrorDeg", invertedRollFullErrorDeg, "deg", "from 60 deg of error a nose-down target is rolled to and pulled (split-S-like), blended smoothly from 30", "no negative-g for large corrections"),
                E("pitchRateAtFullStickDegSec", pitchRateAtFullStickDegSec, "deg/s", "NOT tuning: the bound law's own full-stick rate (V2: 7), copied from the law by the rig", "the bound law's command scale"),
                E("rollRateAtFullStickDegSec", rollRateAtFullStickDegSec, "deg/s", "NOT tuning: the bound law's own full-stick rate (V2: 15), copied from the law by the rig", "the bound law's command scale"),
                E("pitchStickLimit", pitchStickLimit, "-", "full law authority", none),
                E("rollStickLimit", rollStickLimit, "-", "full law authority", none),
                E("yawStickLimit", yawStickLimit, "-", "pedal is a fine-aim aid only; the V2 law coordinates the roll itself", "no flat-yaw turning"),
                E("maxPushStick", maxPushStick, "-", "Play Mode evidence: 0.35 could not capture an 8 deg descent (rate-command law, nose-down authority shrinking with speed); large nose-down errors are still rolled and pulled by the gravity-compensated bank rule", "no sustained negative-g request"),
                E("fineAimYawPerDeg", fineAimYawPerDeg, "1/deg", "3 deg of lateral error asks the full fine-aim pedal", none),
                E("fineAimFadeStartDeg", fineAimFadeStartDeg, "deg", "fine aim only close to the target", none),
                E("fineAimFadeEndDeg", fineAimFadeEndDeg, "deg", "no pedal at all beyond 5 deg of error", "no flat-yaw turning"),
                E("onTargetDeg", onTargetDeg, "deg", "telemetry mode threshold only", none),
                E("reversalHysteresisDeg", reversalHysteresisDeg, "deg", "keeps the chosen side through a reversal / roll-over", none),
                E("liftFadeLowG", liftFadeLowG, "g", "request direction undefined at zero lift request", none),
                E("liftFadeHighG", liftFadeHighG, "g", "full roll authority once the request is a clear direction", none),
                E("commandTimeConstantSeconds", commandTimeConstantSeconds, "s", "4 physics steps: removes step-to-step jitter of the mouse path", none),
                E("commandRatePerSec", commandRatePerSec, "1/s", "full stick to neutral no faster than 0.25 s", "no command discontinuity"),
                E("lowSpeedFullPullMps", lowSpeedFullPullMps, "m/s", "~269 ft/s: near the slowest research trim (~260 ft/s)", "research TAS floor 66.60 m/s"),
                E("lowSpeedZeroPullMps", lowSpeedZeroPullMps, "m/s", "~236 ft/s: 5.4 m/s above the floor", "research TAS floor 66.60 m/s"),
                E("lowSpeedNoseDownStick", lowSpeedNoseDownStick, "-", "at the cap the guidance may only lower the nose a little", "research TAS floor 66.60 m/s"),
                E("alphaPullFadeStartDeg", alphaPullFadeStartDeg, "deg", "well above the exercised 15.5-20.2 deg", "extrapolation far outside the exercised alpha"),
                E("alphaPullZeroDeg", alphaPullZeroDeg, "deg", "pull fully faded", "extrapolation far outside the exercised alpha"),
                E("alphaPushFullDeg", alphaPushFullDeg, "deg", "push fades from here", "transcribed alpha floor -4 deg"),
                E("alphaPushZeroDeg", alphaPushZeroDeg, "deg", "no push at or below 0 deg alpha", "transcribed alpha floor -4 deg")
            };
        }

        private static Entry E(string name, float value, string unit, string why, string protects)
        {
            return new Entry { name = name, value = value, unit = unit, why = why, protects = protects };
        }

        public bool IsSelfConsistent(out string reason)
        {
            Entry[] entries = Entries();
            for (int i = 0; i < entries.Length; i++)
            {
                float v = entries[i].value;
                if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f)
                {
                    reason = entries[i].name + " must be finite and non-negative";
                    return false;
                }
            }

            if (!(pitchRateAtFullStickDegSec > 0f) || !(rollRateAtFullStickDegSec > 0f) || !(bankTimeConstantSeconds > 0f))
            {
                reason = "the law stick scales and the bank time constant must be positive";
                return false;
            }

            if (fineAimFadeEndDeg < fineAimFadeStartDeg || liftFadeHighG < liftFadeLowG
                || lowSpeedFullPullMps < lowSpeedZeroPullMps || alphaPullZeroDeg < alphaPullFadeStartDeg || alphaPushFullDeg < alphaPushZeroDeg)
            {
                reason = "every fade band must be ordered (end >= start)";
                return false;
            }

            reason = "self-consistent";
            return true;
        }
    }

    /// <summary>The guidance memory: command filter and the sides kept through ambiguous geometry. Reset explicitly.</summary>
    [Serializable]
    public struct MavMouseAimGuidanceState
    {
        public bool initialized;
        public float pitch;
        public float roll;
        public float yaw;

        [Tooltip("+1 / -1: the side of the last unambiguous bank error, kept inside the 180 deg hysteresis band. 0: none yet.")]
        public float lastBankErrorSign;

        public Vector3 lastTurnDirectionWorld;
        public bool hasLastTurnDirection;

        [Tooltip("Pitch-plane integral, rad/s of flight-path-rate command.")]
        public float pitchIntegralRadSec;

        public static MavMouseAimGuidanceState Reset
        {
            get { return new MavMouseAimGuidanceState(); }
        }
    }

    /// <summary>One guidance evaluation's inputs: a coherent snapshot of the aircraft pose and the aim direction, all world axes (Unity).</summary>
    public struct MavMouseAimGuidanceInput
    {
        public Vector3 targetDirectionWorld;
        public Vector3 noseWorld;
        public Vector3 rightWingWorld;

        [Tooltip("Aircraft up (Unity local +Y = aerodynamic body -Z).")]
        public Vector3 aircraftUpWorld;

        public Vector3 flightPathWorldMps;
        public Vector3 worldUp;
        public float gravityMps2;
        public bool handsOff;

        public static MavMouseAimGuidanceInput FromPose(Quaternion rotationWorld, Vector3 flightPathWorldMps, Vector3 targetDirectionWorld, float gravityMps2)
        {
            return new MavMouseAimGuidanceInput
            {
                targetDirectionWorld = targetDirectionWorld,
                noseWorld = rotationWorld * Vector3.forward,
                rightWingWorld = rotationWorld * Vector3.right,
                aircraftUpWorld = rotationWorld * Vector3.up,
                flightPathWorldMps = flightPathWorldMps,
                worldUp = Vector3.up,
                gravityMps2 = gravityMps2
            };
        }
    }

    /// <summary>One guidance evaluation: the bounded normalized intent and everything a person needs to see why.</summary>
    [Serializable]
    public struct MavMouseAimGuidanceOutput
    {
        public bool valid;
        public string invalidReason;
        public MavAimGuidanceMode mode;

        [Tooltip("Smoothed, rate-limited, bounded pitch / roll / yaw (throttle is not the guidance's).")]
        public MavPilotCommand command;

        [Tooltip("Before smoothing and limits.")]
        public MavPilotCommand rawCommand;

        public float targetErrorDeg;
        public float targetErrorFromNoseDeg;
        public float pitchPlaneErrorDeg;
        public float lateralErrorDeg;
        public float bankErrorDeg;
        public float currentBankDeg;
        public float desiredBankDeg;
        public float desiredFlightPathRateDegSec;
        public float requestedLiftG;
        public float pitchIntegralStick;
        public float trueAirspeedMps;
        public float alphaDeg;
        public bool reversalSideFromHistory;
        public MavAimGuidanceLimiter limiters;
    }

    /// <summary>
    /// MAVERICK MOUSE-AIM GUIDANCE - target direction in, bounded normalized pilot intent out. Pure and deterministic:
    /// (aim direction, aircraft pose and flight path, previous state, configuration, dt) -> pitch / roll / yaw intent.
    ///
    /// It never produces a force, a moment, a surface deflection, a pose or a velocity. The intent goes to the bound
    /// control law (for the F-15: the unchanged MavF15PilotControlLawV2, a pitch-RATE / stability-axis roll-RATE law),
    /// whose surfaces go through the actuator into the aerodynamic model. Whatever the aircraft does is the model's.
    ///
    /// ALGORITHM (bank-to-turn with gravity compensation; aircraft-independent geometry):
    ///   r  = reference direction: the flight path (velocity) by default, the nose on request or below a minimum speed
    ///   th = angle(r, target); d = unit component of the target perpendicular to r (the turn direction)
    ///   w_des = min(K th, w_max)                        desired flight-path turn rate; w_max = law full-stick pitch rate
    ///   f_req = V w_des d - g_perp                      specific force needed perpendicular to r (lift request)
    ///   l, s  = aircraft up and right wing, made perpendicular to r (lift direction, wing direction)
    ///   bank error  = atan2(f_req . s, f_req . l)      roll so that the lift points at f_req
    ///   pitch-rate  = w_des (d . l)                     the part of the desired path rotation the lift can make now
    ///   pedal       = small fine-aim term near the target only
    /// Consequences that emerge rather than being scripted: aim straight ahead -> f_req = -g_perp -> wings level;
    /// small errors bank little; a horizontal turn banks to atan(V w / g) and pulls sin(bank) of the turn rate
    /// (exactly the level-turn pitch rate); a large nose-down error rolls inverted and pulls; inverted with the target
    /// ahead rolls upright. Pull is preferred to push (push capped). The outer loop never asks more turn rate than the
    /// bound law commands at full stick, so the bank request matches the pull the law can actually ask for.
    ///
    /// AXES. Everything is in Unity WORLD vectors; aircraft axes are taken from the pose: nose = rotation * +Z (aero +X),
    /// right wing = rotation * +X (aero +Y), up = rotation * +Y (aero -Z). Output signs are MavPilotCommand's
    /// (pitch +1 nose up, roll +1 roll right, yaw +1 nose right). Angles are radians internally, degrees in debug.
    /// </summary>
    public static class MavMouseAimGuidance
    {
        private const float Tiny = 1e-6f;

        public static MavMouseAimGuidanceOutput Step(
            MavMouseAimGuidanceInput input,
            MavMouseAimGuidanceConfig config,
            ref MavMouseAimGuidanceState state,
            float deltaTime)
        {
            MavMouseAimGuidanceOutput o = new MavMouseAimGuidanceOutput();
            o.mode = MavAimGuidanceMode.Invalid;
            o.command = MavPilotCommand.Neutral;
            o.rawCommand = MavPilotCommand.Neutral;

            string reason;
            if (!InputsUsable(input, deltaTime, out reason))
            {
                o.invalidReason = reason;
                return o;
            }

            Vector3 up = input.worldUp.normalized;
            Vector3 nose = input.noseWorld.normalized;
            Vector3 right = input.rightWingWorld.normalized;
            Vector3 acUp = input.aircraftUpWorld.normalized;
            Vector3 target = input.targetDirectionWorld.normalized;
            float speed = input.flightPathWorldMps.magnitude;
            float g = Mathf.Max(0f, input.gravityMps2);

            o.trueAirspeedMps = speed;
            o.alphaDeg = speed > Tiny
                ? Mathf.Atan2(-Vector3.Dot(input.flightPathWorldMps, acUp), Vector3.Dot(input.flightPathWorldMps, nose)) * Mathf.Rad2Deg
                : 0f;
            o.targetErrorFromNoseDeg = AngleRad(nose, target) * Mathf.Rad2Deg;

            // ---------------------------------------------------------------- reference direction
            Vector3 reference = nose;
            if (config.aimReference == MavAimReference.FlightPath)
            {
                if (speed >= Mathf.Max(Tiny, config.minimumFlightPathSpeedMps))
                    reference = input.flightPathWorldMps / speed;
                else
                    o.limiters |= MavAimGuidanceLimiter.NoseReferenceFallback;
            }

            float theta = AngleRad(reference, target);
            o.targetErrorDeg = theta * Mathf.Rad2Deg;

            // ---------------------------------------------------------------- lift and wing directions perpendicular to r
            Vector3 lift = Perp(acUp, reference);
            if (lift.sqrMagnitude < 1e-8f)
                lift = Perp(-nose, reference);      // r along the aircraft up axis (alpha ~ 90 deg): degenerate, keep finite
            lift = lift.sqrMagnitude > 1e-12f ? lift.normalized : acUp;

            Vector3 wing = right - Vector3.Dot(right, reference) * reference - Vector3.Dot(right, lift) * lift;
            wing = wing.sqrMagnitude > 1e-8f ? wing.normalized : Vector3.Cross(reference, lift).normalized;

            o.currentBankDeg = BankAboutReferenceDeg(reference, lift, wing, up);

            // ---------------------------------------------------------------- turn direction d
            float hysteresis = Mathf.Clamp(config.reversalHysteresisDeg, 0f, 89f) * Mathf.Deg2Rad;
            Vector3 turn = Vector3.zero;
            bool reversal = theta > Mathf.PI - hysteresis;
            if (reversal)
            {
                Vector3 kept = state.hasLastTurnDirection ? Perp(state.lastTurnDirectionWorld, reference) : Vector3.zero;
                if (kept.sqrMagnitude > 0.04f)
                {
                    turn = kept.normalized;
                    o.reversalSideFromHistory = true;
                }
                else
                {
                    // Behind with no history: keep turning the way the aircraft is banked; wings level -> right.
                    float s = Vector3.Dot(right, up);
                    float side = s < -0.02f ? 1f : (s > 0.02f ? -1f : 1f);
                    turn = side * wing;
                }
            }
            else
            {
                Vector3 raw = target - Vector3.Dot(target, reference) * reference;
                if (raw.sqrMagnitude > 1e-12f)
                    turn = raw.normalized;
            }

            if (turn.sqrMagnitude > 0.5f)
            {
                state.lastTurnDirectionWorld = turn;
                state.hasLastTurnDirection = true;
            }

            // ---------------------------------------------------------------- envelope soft caps (command ceilings)
            float pullCeiling = Mathf.Clamp01(config.pitchStickLimit);
            float pushFloor = -Mathf.Clamp01(Mathf.Min(config.maxPushStick, config.pitchStickLimit));
            if (config.enableEnvelopeSoftCaps)
            {
                float speedFactor = Smooth(config.lowSpeedZeroPullMps, config.lowSpeedFullPullMps, speed);
                if (speedFactor < 1f)
                {
                    pullCeiling = Mathf.Lerp(-Mathf.Clamp01(config.lowSpeedNoseDownStick), pullCeiling, speedFactor);
                    o.limiters |= MavAimGuidanceLimiter.LowSpeedPullCap;
                }

                float alphaPull = 1f - Smooth(config.alphaPullFadeStartDeg, config.alphaPullZeroDeg, o.alphaDeg);
                if (alphaPull < 1f)
                {
                    pullCeiling = Mathf.Min(pullCeiling, pullCeiling * alphaPull);
                    o.limiters |= MavAimGuidanceLimiter.HighAlphaPullCap;
                }

                float alphaPush = Smooth(config.alphaPushZeroDeg, config.alphaPushFullDeg, o.alphaDeg);
                if (alphaPush < 1f)
                {
                    pushFloor *= alphaPush;
                    o.limiters |= MavAimGuidanceLimiter.LowAlphaPushCap;
                }

                pushFloor = Mathf.Min(pushFloor, pullCeiling);
            }

            // ---------------------------------------------------------------- outer loop: desired flight-path rate
            float pitchFullRad = Mathf.Max(Tiny, config.pitchRateAtFullStickDegSec) * Mathf.Deg2Rad;
            float rateCap = pitchFullRad * Mathf.Max(0f, pullCeiling);
            float rateRequest = Mathf.Max(0f, config.flightPathRateGainPerSec) * theta;
            float rateDes = Mathf.Min(rateRequest, rateCap);
            if (rateRequest > rateCap)
                o.limiters |= MavAimGuidanceLimiter.FlightPathRateCapped;
            o.desiredFlightPathRateDegSec = rateDes * Mathf.Rad2Deg;

            Vector3 gravityVec = -up * g;
            Vector3 gravityPerp = gravityVec - Vector3.Dot(gravityVec, reference) * reference;
            float effectiveSpeed = Mathf.Max(speed, Mathf.Max(Tiny, config.minimumFlightPathSpeedMps));
            Vector3 liftRequest = effectiveSpeed * rateDes * turn - Mathf.Max(0f, config.gravityCompensation) * gravityPerp;
            // Moderate nose-down corrections are unloaded and pushed, not rolled inverted: the part of the lift request that
            // points below the upright direction is phased in only between invertedRollMinErrorDeg and invertedRollFullErrorDeg.
            Vector3 uprightDir = Perp(up, reference);
            if (uprightDir.sqrMagnitude > 1e-6f)
            {
                uprightDir.Normalize();
                float down = Vector3.Dot(liftRequest, uprightDir);
                if (down < 0f)
                {
                    float allow = Smooth(config.invertedRollMinErrorDeg, config.invertedRollFullErrorDeg, theta * Mathf.Rad2Deg);
                    if (allow < 1f)
                    {
                        liftRequest -= (1f - allow) * down * uprightDir;
                        o.limiters |= MavAimGuidanceLimiter.InvertedRollDeferred;
                    }
                }
            }

            float gRef = g > Tiny ? g : 9.80665f;
            o.requestedLiftG = liftRequest.magnitude / gRef;

            // ---------------------------------------------------------------- bank
            float bankError = Mathf.Atan2(Vector3.Dot(liftRequest, wing), Vector3.Dot(liftRequest, lift));
            if (Mathf.Abs(bankError) > Mathf.PI - hysteresis && state.lastBankErrorSign != 0f)
                bankError = state.lastBankErrorSign * Mathf.Abs(bankError);
            else if (Mathf.Abs(bankError) > 1e-4f)
                state.lastBankErrorSign = Mathf.Sign(bankError);
            o.bankErrorDeg = bankError * Mathf.Rad2Deg;
            o.desiredBankDeg = WrapDeg(o.currentBankDeg + o.bankErrorDeg);

            float liftWeight = Smooth(config.liftFadeLowG, config.liftFadeHighG, o.requestedLiftG);
            float rollRaw = o.bankErrorDeg / Mathf.Max(Tiny, config.bankTimeConstantSeconds)
                            / Mathf.Max(Tiny, config.rollRateAtFullStickDegSec) * liftWeight;

            // ---------------------------------------------------------------- pitch
            float dotTurnLift = Vector3.Dot(turn, lift);
            float integralLimit = Mathf.Clamp01(config.pitchIntegralLimitStick) * pitchFullRad;
            float integral = Mathf.Clamp(state.pitchIntegralRadSec, -integralLimit, integralLimit);
            float pitchRaw = (rateDes * dotTurnLift + integral) / pitchFullRad;
            o.pitchPlaneErrorDeg = o.targetErrorDeg * dotTurnLift;
            o.pitchIntegralStick = integral / pitchFullRad;

            // Integral for the NEXT step (this step used the previous value, so a single evaluation is exactly proportional).
            float pitchPlaneErrorRad = theta * dotTurnLift;
            bool saturatedUp = pitchRaw >= pullCeiling && pitchPlaneErrorRad > 0f;
            bool saturatedDown = pitchRaw <= pushFloor && pitchPlaneErrorRad < 0f;
            if (input.handsOff)
            {
                integral = 0f;
            }
            else if (o.targetErrorDeg <= config.pitchIntegralActiveBelowDeg)
            {
                if (!saturatedUp && !saturatedDown)
                    integral += Mathf.Max(0f, config.pitchIntegralGainPerSec2) * pitchPlaneErrorRad * deltaTime;
            }
            else
            {
                integral *= config.pitchIntegralLeakSeconds > Tiny ? Mathf.Exp(-deltaTime / config.pitchIntegralLeakSeconds) : 0f;
            }

            state.pitchIntegralRadSec = Mathf.Clamp(integral, -integralLimit, integralLimit);
            o.lateralErrorDeg = o.targetErrorDeg * Vector3.Dot(turn, wing);

            // ---------------------------------------------------------------- fine-aim pedal
            float yawLimit = Mathf.Clamp01(config.yawStickLimit);
            float yawRaw = Mathf.Clamp(config.fineAimYawPerDeg * o.lateralErrorDeg, -yawLimit, yawLimit)
                           * (1f - Smooth(config.fineAimFadeStartDeg, config.fineAimFadeEndDeg, o.targetErrorDeg));

            if (input.handsOff)
            {
                rollRaw = pitchRaw = yawRaw = 0f;
                o.mode = MavAimGuidanceMode.HandsOff;
            }
            else if (reversal)
            {
                o.mode = MavAimGuidanceMode.Reversal;
            }
            else
            {
                o.mode = o.targetErrorDeg <= config.onTargetDeg ? MavAimGuidanceMode.OnTarget : MavAimGuidanceMode.Tracking;
            }

            o.rawCommand.pitch = pitchRaw;
            o.rawCommand.roll = rollRaw;
            o.rawCommand.yaw = yawRaw;

            // ---------------------------------------------------------------- limits
            float rollLimit = Mathf.Clamp01(config.rollStickLimit);
            float pitchLimited = Mathf.Clamp(pitchRaw, pushFloor, pullCeiling);
            if (pitchRaw > pullCeiling && pullCeiling >= Mathf.Clamp01(config.pitchStickLimit) - Tiny)
                o.limiters |= MavAimGuidanceLimiter.PitchStickSaturated;
            if (pitchRaw < pushFloor)
                o.limiters |= MavAimGuidanceLimiter.PushLimited;
            float rollLimited = Mathf.Clamp(rollRaw, -rollLimit, rollLimit);
            if (Mathf.Abs(rollRaw) > rollLimit)
                o.limiters |= MavAimGuidanceLimiter.RollStickSaturated;
            if (Mathf.Abs(yawRaw) >= yawLimit - Tiny && yawLimit > 0f)
                o.limiters |= MavAimGuidanceLimiter.YawStickSaturated;

            // ---------------------------------------------------------------- smoothing + rate limit (fixed physics dt)
            if (!state.initialized)
            {
                state.pitch = state.roll = state.yaw = 0f;
                state.initialized = true;
            }

            float a = config.commandTimeConstantSeconds > Tiny ? 1f - Mathf.Exp(-deltaTime / config.commandTimeConstantSeconds) : 1f;
            float maxStep = config.commandRatePerSec > 0f ? config.commandRatePerSec * deltaTime : float.PositiveInfinity;
            bool rateLimited = false;
            state.pitch = Filter(state.pitch, pitchLimited, a, maxStep, ref rateLimited);
            state.roll = Filter(state.roll, rollLimited, a, maxStep, ref rateLimited);
            state.yaw = Filter(state.yaw, yawRaw, a, maxStep, ref rateLimited);
            if (rateLimited)
                o.limiters |= MavAimGuidanceLimiter.CommandRateLimited;

            // The fixed stick limits bound the filtered command at every step. A soft cap that shrinks is reached through the
            // rate limit (within 1 / commandRatePerSec), never by a step: continuity is kept, the cap still wins quickly.
            float pitchLimit = Mathf.Clamp01(config.pitchStickLimit);
            state.pitch = Mathf.Clamp(state.pitch, -Mathf.Clamp01(Mathf.Min(config.maxPushStick, pitchLimit)), pitchLimit);
            state.roll = Mathf.Clamp(state.roll, -rollLimit, rollLimit);
            state.yaw = Mathf.Clamp(state.yaw, -yawLimit, yawLimit);

            o.command.pitch = state.pitch;
            o.command.roll = state.roll;
            o.command.yaw = state.yaw;
            o.valid = IsFinite(o.command.pitch) && IsFinite(o.command.roll) && IsFinite(o.command.yaw);
            if (!o.valid)
            {
                state = MavMouseAimGuidanceState.Reset;
                o.command = MavPilotCommand.Neutral;
                o.mode = MavAimGuidanceMode.Invalid;
                o.invalidReason = "guidance produced a non-finite command (refused, state reset)";
            }

            return o;
        }

        // ================================================================== helpers (public where validation needs them)

        public static bool InputsUsable(MavMouseAimGuidanceInput input, float deltaTime, out string reason)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
            {
                reason = "dt is not a positive finite step";
                return false;
            }

            if (!IsFinite(input.targetDirectionWorld))
            {
                reason = "aim target direction is not finite";
                return false;
            }

            if (input.targetDirectionWorld.sqrMagnitude < 1e-12f)
            {
                reason = "aim target direction is negligible (zero vector)";
                return false;
            }

            if (!IsFinite(input.noseWorld) || !IsFinite(input.rightWingWorld) || !IsFinite(input.aircraftUpWorld)
                || input.noseWorld.sqrMagnitude < 1e-8f || input.rightWingWorld.sqrMagnitude < 1e-8f || input.aircraftUpWorld.sqrMagnitude < 1e-8f)
            {
                reason = "aircraft attitude is not a finite basis";
                return false;
            }

            if (!IsFinite(input.flightPathWorldMps))
            {
                reason = "aircraft flight-path vector is not finite";
                return false;
            }

            if (!IsFinite(input.worldUp) || input.worldUp.sqrMagnitude < 1e-8f || float.IsNaN(input.gravityMps2) || float.IsInfinity(input.gravityMps2))
            {
                reason = "world up / gravity is not finite";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Angle between two directions, accurate near 0 and near 180 deg (atan2 of |cross| and dot).</summary>
        public static float AngleRad(Vector3 a, Vector3 b)
        {
            Vector3 an = a.normalized, bn = b.normalized;
            return Mathf.Atan2(Vector3.Cross(an, bn).magnitude, Vector3.Dot(an, bn));
        }

        public static float Smooth(float edge0, float edge1, float x)
        {
            if (float.IsNaN(x))
                return 0f;
            if (!(edge1 > edge0))
                return x >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Bank about the reference direction, deg: 0 upright, + right wing down, +-180 inverted. 0 when the reference is vertical.</summary>
        public static float BankAboutReferenceDeg(Vector3 reference, Vector3 lift, Vector3 wing, Vector3 worldUp)
        {
            Vector3 upright = Perp(worldUp, reference);
            if (upright.sqrMagnitude < 1e-6f)
                return 0f;
            upright.Normalize();
            return Mathf.Atan2(-Vector3.Dot(wing, upright), Vector3.Dot(lift, upright)) * Mathf.Rad2Deg;
        }

        private static Vector3 Perp(Vector3 v, Vector3 unitAxis)
        {
            return v - Vector3.Dot(v, unitAxis) * unitAxis;
        }

        private static float Filter(float current, float target, float alpha, float maxStep, ref bool rateLimited)
        {
            float desired = alpha * (target - current);
            if (Mathf.Abs(desired) > maxStep)
            {
                desired = Mathf.Sign(desired) * maxStep;
                rateLimited = true;
            }

            return current + desired;
        }

        public static float WrapDeg(float deg)
        {
            if (float.IsNaN(deg) || float.IsInfinity(deg))
                return 0f;
            deg = Mathf.Repeat(deg + 180f, 360f) - 180f;
            return deg;
        }

        public static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        public static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }
    }
}
