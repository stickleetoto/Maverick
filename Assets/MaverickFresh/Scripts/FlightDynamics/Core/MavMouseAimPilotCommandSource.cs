using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>What the source does while the mouse cannot steer (focus lost, cursor released, camera missing, no mouse).</summary>
    public enum MavMouseAimInputLossPolicy
    {
        /// <summary>
        /// Default. Hands off the stick: neutral pitch / roll / yaw, throttle held, and the aim re-anchored on the flight path
        /// every step so input resumes from zero error (no jump). The source still produces a command - a deliberate neutral
        /// one, as the keyboard source does with no key held - so the aircraft keeps flying its physics.
        /// </summary>
        NeutralStickHoldThrottle = 0,

        /// <summary>Keep steering toward the frozen aim direction (War-Thunder-like). Opt-in.</summary>
        HoldAimTarget = 1,

        /// <summary>
        /// Stop producing commands: HasCommandSignal goes false, the control law applies this source's declared
        /// NeutralCommand signal-loss policy, and live-readiness then withholds loads (the existing fail-closed rule).
        /// </summary>
        DropCommandSignal = 2
    }

    public enum MavMouseAimInputState
    {
        Active = 0,
        FreeLook = 1,
        HandsOff = 2,
        FocusLost = 3,
        CursorReleased = 4,
        CameraUnavailable = 5,
        NoMouseDevice = 6,
        External = 7
    }

    /// <summary>
    /// REALISTIC MOUSE INSTRUCTOR V2 - the opt-in mouse-aim pilot-command source. Mouse deltas move a persistent WORLD aim
    /// direction; once per physics step the aim, the aircraft's pose and its flight path go through the pure
    /// <see cref="MavMouseAimGuidance"/>; the result is published as normalized <see cref="MavPilotCommand"/> intent for the
    /// bound control law (for the F-15: the existing, unchanged MavF15PilotControlLawV2).
    ///
    /// It is intent only. It never touches a force, a moment, a surface, the aircraft's Transform or its Rigidbody state;
    /// it only READS the Rigidbody pose and flight path. Throttle is passed through and owned downstream (the F-15
    /// research rig flies fixed research thrust: THROTTLE INACTIVE).
    ///
    /// TIMING. <see cref="ProcessFrameInput"/> runs per rendered frame (Update) and only accumulates mouse deltas, which
    /// carry no time unit - render delta time is never used. <see cref="StepGuidance"/> runs per physics step at execution
    /// order <see cref="ExecutionOrder"/> (after the ownership authority at -400, before the control law at -300), so the
    /// law reads this step's command; it reads the Rigidbody pose of the last completed physics step (one step fresher
    /// than the law's own published state, an outer-loop sample, not a change to the law -> actuator -> body latency).
    /// <see cref="TryGetCommand"/> returns the cached step result and has no side effects, so the rig's law-switch gate
    /// can read it any number of times.
    ///
    /// READINESS. Declares itself an operational path (a human input device). <see cref="HasCommandSignal"/> is true
    /// exactly when this step's guidance produced a finite command. Invalid guidance (no aircraft bound, a non-finite
    /// pose or flight path) is a genuine signal loss. Device-level loss follows <see cref="inputLossPolicy"/>. No branch
    /// ever forwards an inspector test command.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class MavMouseAimPilotCommandSource : MavPilotCommandSourceBase
    {
        public const int ExecutionOrder = -350;

        public const string SourceName = "Maverick realistic mouse instructor v2 (mouse aim -> guidance -> normalized pilot intent)";

        [Header("Aircraft (read only - pose and flight path are read, never written)")]
        public Rigidbody aircraftBody;
        public MavSixDoFBody sixDoFBody;

        [Header("Guidance (" + MavMouseAimProvenance.MaverickTunedNonAuthoritative + ")")]
        public MavMouseAimGuidanceConfig guidance = MavMouseAimGuidanceConfig.Default();

        [Header("Mouse")]
        [Tooltip("Read the mouse / keys through MavFreshInput in Update. Off: input arrives only through ProcessFrameInput / SetScriptedTargetDirection (tests, external adapters).")]
        public bool readDeviceInput = true;

        [Tooltip("Aim degrees per MavFreshInput mouse unit (legacy 'Mouse X/Y' axis units). Input choice, not aircraft data.")]
        public float degreesPerMouseUnit = 1.2f;
        public bool invertY;
        [Range(10f, 89.9f)] public float elevationLimitDeg = MavMouseAimInput.DefaultElevationLimitDeg;

        [Tooltip("Hold: mouse moves the camera only; the aim target does not move.")]
        public KeyCode freeLookKey = KeyCode.C;

        [Tooltip("Toggle hands-off: neutral stick, aim re-anchored on the flight path (release-to-neutral).")]
        public KeyCode handsOffKey = KeyCode.X;

        public KeyCode releaseCursorKey = KeyCode.Escape;
        public KeyCode captureCursorKey = KeyCode.Mouse0;
        public bool manageCursorLock = true;

        [Tooltip("The aim is only steerable with a live aim camera bound (the camera rig registers itself). Without one the input-loss policy applies.")]
        public bool requireAimCamera = true;

        public MavMouseAimInputLossPolicy inputLossPolicy = MavMouseAimInputLossPolicy.NeutralStickHoldThrottle;

        [Tooltip("Passed through only. The F-15 research rig ignores it (fixed research thrust).")]
        [Range(0f, 1f)] public float throttle01 = 1f;

        [Header("Bound control law (set by the aircraft rig)")]
        [Tooltip("The guidance output is scaled for a RATE-command law. With any other law the source commands neutral stick (flagged).")]
        public bool boundLawIsRateCommand = true;
        public string boundLawName = "(not declared by a rig: guidance stick scale = config defaults)";

        [Header("Camera (registered by MavMouseAimCameraRig)")]
        public MavMouseAimCameraRig boundCamera;

        [Header("Debug")]
        public MavMouseAimAngles debugAim;
        public Vector2 debugFreeLookYawPitchDeg;
        public bool debugAimInitialized;
        public bool debugHandsOff;
        public bool debugFreeLookHeld;
        public MavMouseAimInputState debugInputState = MavMouseAimInputState.Active;
        public MavMouseAimInputState debugFrameInputState = MavMouseAimInputState.Active;
        public bool debugUsingScriptedTarget;
        public Vector3 debugTargetDirectionWorld;
        public MavMouseAimGuidanceOutput debugGuidance;
        public MavPilotCommand debugCommand = MavPilotCommand.Neutral;
        public bool debugHasCommand;
        public string debugStatus = "not stepped";
        public int debugGuidanceSteps;
        public int debugInvalidGuidanceSteps;
        public int debugInputLossSteps;
        public int debugRejectedInputSamples;
        public float debugSpecificEnergyHeightM;
        public float debugSpecificEnergyRateMps;

        private MavMouseAimGuidanceState guidanceState = MavMouseAimGuidanceState.Reset;
        private MavMouseAimAngles aim;
        private Vector2 freeLook;
        private bool aimInitialized;
        private bool handsOff;
        private bool freeLookHeld;
        private bool useScriptedTarget;
        private Vector3 scriptedTarget;
        private MavMouseAimInputState frameInputState = MavMouseAimInputState.Active;
        private MavPilotCommand cached = MavPilotCommand.Neutral;
        private bool hasCommand;
        private bool energyInitialized;
        private float lastEnergyHeight;

        public override string CommandSourceName
        {
            get { return SourceName; }
        }

        /// <summary>A human mouse is an operational input path by declaration (like the keyboard source).</summary>
        public override bool IsOperationalCommandSource
        {
            get { return true; }
        }

        public override bool HasCommandSignal
        {
            get { return isActiveAndEnabled && hasCommand; }
        }

        public override MavCommandSignalLossPolicy SignalLossPolicy
        {
            get { return MavCommandSignalLossPolicy.NeutralCommand; }
        }

        public override bool TryGetCommand(out MavPilotCommand command)
        {
            bool signal = HasCommandSignal;
            command = signal ? cached : MavPilotCommand.Neutral;
            return signal;
        }

        /// <summary>The persistent aim direction (world). Valid once initialized; before that the nose of the bound aircraft.</summary>
        public Vector3 AimDirectionWorld
        {
            get
            {
                if (useScriptedTarget)
                    return scriptedTarget;
                return aimInitialized ? aim.Direction : (aircraftBody != null ? aircraftBody.rotation * Vector3.forward : Vector3.forward);
            }
        }

        /// <summary>The camera's view direction: the aim plus the free-look offset. Never used by the guidance.</summary>
        public Vector3 ViewDirectionWorld
        {
            get { return useScriptedTarget ? scriptedTarget : MavMouseAimInput.ViewDirection(aim, freeLook); }
        }

        public bool HandsOff
        {
            get { return handsOff; }
        }

        private void OnEnable()
        {
            ResetGuidanceAndAim();
        }

        /// <summary>
        /// Forget everything: guidance filter and side memory, aim (re-anchored on the flight path at the next step), free-look,
        /// hands-off, the cached command. Called on enable and by the rig when it (re)starts the aircraft.
        /// </summary>
        public void ResetGuidanceAndAim()
        {
            guidanceState = MavMouseAimGuidanceState.Reset;
            aimInitialized = false;
            freeLook = Vector2.zero;
            handsOff = false;
            freeLookHeld = false;
            useScriptedTarget = false;
            cached = MavPilotCommand.Neutral;
            cached.throttle01 = throttle01;
            hasCommand = false;
            energyInitialized = false;
            debugStatus = "reset";
            PublishDebug();
        }

        /// <summary>Declared by the aircraft rig when it binds a rate-command law: the guidance's stick scale is the law's.</summary>
        public void ConfigureForRateCommandLaw(float pitchRateAtFullStickDegSec, float rollRateAtFullStickDegSec, string lawName)
        {
            boundLawIsRateCommand = true;
            boundLawName = lawName;
            guidance.pitchRateAtFullStickDegSec = pitchRateAtFullStickDegSec;
            guidance.rollRateAtFullStickDegSec = rollRateAtFullStickDegSec;
            guidanceState = MavMouseAimGuidanceState.Reset;
        }

        /// <summary>Declared by the aircraft rig when the bound law is not a rate-command law: neutral stick only.</summary>
        public void ConfigureForNonRateCommandLaw(string lawName)
        {
            boundLawIsRateCommand = false;
            boundLawName = lawName;
            guidanceState = MavMouseAimGuidanceState.Reset;
        }

        /// <summary>
        /// External / scripted aim (tests, a future target adapter). Replaces the mouse aim until <see cref="ClearScriptedTarget"/>.
        /// A non-finite or negligible vector is rejected and counted; the previous aim stays.
        /// </summary>
        public bool SetScriptedTargetDirection(Vector3 targetWorld)
        {
            if (!MavMouseAimGuidance.IsFinite(targetWorld) || targetWorld.sqrMagnitude < 1e-12f)
            {
                debugRejectedInputSamples++;
                return false;
            }

            scriptedTarget = targetWorld.normalized;
            useScriptedTarget = true;
            return true;
        }

        public void ClearScriptedTarget()
        {
            if (useScriptedTarget && MavMouseAimInput.TryAnglesFromDirection(scriptedTarget, elevationLimitDeg, out aim))
                aimInitialized = true;
            useScriptedTarget = false;
        }

        public void SetHandsOff(bool value)
        {
            handsOff = value;
        }

        // ================================================================== frame input (Update)

        private void Update()
        {
            if (!readDeviceInput)
            {
                frameInputState = MavMouseAimInputState.External;
                return;
            }

            if (MavFreshInput.GetKeyDown(handsOffKey))
                handsOff = !handsOff;

            if (manageCursorLock)
            {
                if (MavFreshInput.GetKeyDown(releaseCursorKey))
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked
                         && (MavFreshInput.GetKeyDown(captureCursorKey) || !cursorEverCaptured))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    cursorEverCaptured = true;
                }
            }

            MavMouseAimInputState state = MavMouseAimInputState.Active;
            if (!MousePresent())
                state = MavMouseAimInputState.NoMouseDevice;
            else if (!Application.isFocused)
                state = MavMouseAimInputState.FocusLost;
            else if (manageCursorLock && Cursor.lockState != CursorLockMode.Locked)
                state = MavMouseAimInputState.CursorReleased;

            bool available = state == MavMouseAimInputState.Active;
            ProcessFrameInput(MavFreshInput.MouseX(), MavFreshInput.MouseY(), available && MavFreshInput.GetKey(freeLookKey), available);
            if (!available)
                frameInputState = state;
        }

        private bool cursorEverCaptured;

        private static bool MousePresent()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePresent;
#elif ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Mouse.current != null;
#else
            return false;
#endif
        }

        /// <summary>
        /// One rendered frame of mouse input, in MavFreshInput mouse units. Production calls it from Update with the device
        /// values; tests call it with a scripted trace. Accumulates only - no time step involved. While free-look is held
        /// the delta moves the camera offset and never the aim; when it is released the offset returns to zero.
        /// A non-finite sample is rejected and counted.
        /// </summary>
        public void ProcessFrameInput(float mouseXUnits, float mouseYUnits, bool freeLookHeldThisFrame, bool deviceAvailable)
        {
            freeLookHeld = freeLookHeldThisFrame && deviceAvailable;
            if (!deviceAvailable)
            {
                // Generic loss; Update refines it (focus / cursor / device) right after this call.
                frameInputState = MavMouseAimInputState.FocusLost;
                PublishDebug();
                return;
            }

            frameInputState = freeLookHeld ? MavMouseAimInputState.FreeLook : MavMouseAimInputState.Active;
            Vector2 deg = MavMouseAimInput.UnitsToDegrees(mouseXUnits, mouseYUnits, degreesPerMouseUnit, invertY);
            bool accepted;
            if (freeLookHeld)
            {
                accepted = MavMouseAimInput.ApplyFreeLookDelta(ref freeLook, deg.x, deg.y);
            }
            else
            {
                freeLook = Vector2.zero;
                accepted = !aimInitialized || handsOff || useScriptedTarget
                    ? MavMouseAimGuidance.IsFinite(deg.x) && MavMouseAimGuidance.IsFinite(deg.y)
                    : MavMouseAimInput.ApplyDelta(ref aim, deg.x, deg.y, elevationLimitDeg);
            }

            if (!accepted)
                debugRejectedInputSamples++;
            PublishDebug();
        }

        // ================================================================== physics step

        private void FixedUpdate()
        {
            StepGuidance(Time.fixedDeltaTime);
        }

        /// <summary>
        /// One guidance step: snapshot aim + pose + flight path, evaluate the pure guidance, cache the command. Public so the
        /// edit-mode validation drives exactly this code, in the real order (source -> law -> actuator -> body).
        /// </summary>
        public void StepGuidance(float deltaTime)
        {
            ResolveBinding();
            debugGuidanceSteps++;

            if (aircraftBody == null)
            {
                Invalidate("no aircraft Rigidbody bound");
                return;
            }

            Quaternion rotation = aircraftBody.rotation;
            Vector3 vWorld = aircraftBody.linearVelocity;
            Vector3 positionWorld = aircraftBody.position;
            if (!MavMouseAimGuidance.IsFinite(vWorld) || !MavMouseAimGuidance.IsFinite(positionWorld)
                || float.IsNaN(rotation.x) || float.IsNaN(rotation.y) || float.IsNaN(rotation.z) || float.IsNaN(rotation.w))
            {
                Invalidate("aircraft pose / flight path is not finite");
                return;
            }

            float g = ResolveGravity();
            UpdateEnergy(positionWorld.y, vWorld.magnitude, g, deltaTime);

            // Effective input state for this step.
            MavMouseAimInputState stepState = frameInputState;
            if (requireAimCamera && (boundCamera == null || !boundCamera.isActiveAndEnabled || !boundCamera.HasLiveCamera))
                stepState = MavMouseAimInputState.CameraUnavailable;
            bool inputLost = stepState == MavMouseAimInputState.FocusLost || stepState == MavMouseAimInputState.CursorReleased
                             || stepState == MavMouseAimInputState.CameraUnavailable || stepState == MavMouseAimInputState.NoMouseDevice;
            if (useScriptedTarget && !inputLost)
                stepState = MavMouseAimInputState.External;

            if (inputLost)
            {
                debugInputLossSteps++;
                if (inputLossPolicy == MavMouseAimInputLossPolicy.DropCommandSignal)
                {
                    debugInputState = stepState;
                    Invalidate("mouse input unavailable (" + stepState + "): policy DropCommandSignal");
                    guidanceState = MavMouseAimGuidanceState.Reset;
                    return;
                }
            }

            bool neutralOnLoss = inputLost && inputLossPolicy == MavMouseAimInputLossPolicy.NeutralStickHoldThrottle;
            bool neutralStick = handsOff || neutralOnLoss || !boundLawIsRateCommand;
            if (handsOff && !inputLost)
                stepState = MavMouseAimInputState.HandsOff;
            debugInputState = stepState;

            // Re-anchor the aim on the reference direction when not steering, and at the first step: zero error, no jump.
            if (!aimInitialized || neutralStick)
                AnchorAimOnReference(rotation, vWorld);

            Vector3 targetWorld = useScriptedTarget && !neutralStick ? scriptedTarget : aim.Direction;
            MavMouseAimGuidanceInput input = MavMouseAimGuidanceInput.FromPose(rotation, vWorld, targetWorld, g);
            input.handsOff = neutralStick;

            MavMouseAimGuidanceOutput output = MavMouseAimGuidance.Step(input, guidance, ref guidanceState, deltaTime);
            debugGuidance = output;
            debugTargetDirectionWorld = targetWorld;

            if (!output.valid)
            {
                Invalidate("guidance invalid: " + output.invalidReason);
                return;
            }

            MavPilotCommand c = output.command;
            c.throttle01 = Mathf.Clamp01(MavMouseAimGuidance.IsFinite(throttle01) ? throttle01 : 0f);
            cached = c.Clamped();
            hasCommand = true;
            debugStatus = !boundLawIsRateCommand
                ? "NEUTRAL STICK: bound law is not a rate-command law (" + boundLawName + ")"
                : (neutralOnLoss ? "NEUTRAL STICK: mouse input unavailable (" + stepState + ")" : output.mode.ToString());
            PublishDebug();
        }

        private void AnchorAimOnReference(Quaternion rotation, Vector3 vWorld)
        {
            float speed = vWorld.magnitude;
            Vector3 reference = guidance.aimReference == MavAimReference.FlightPath && speed >= Mathf.Max(1e-6f, guidance.minimumFlightPathSpeedMps)
                ? vWorld / speed
                : rotation * Vector3.forward;
            MavMouseAimAngles anchored;
            if (MavMouseAimInput.TryAnglesFromDirection(reference, elevationLimitDeg, out anchored))
            {
                aim = anchored;
                aimInitialized = true;
            }
        }

        private void Invalidate(string reason)
        {
            hasCommand = false;
            cached = MavPilotCommand.Neutral;
            cached.throttle01 = throttle01;
            debugInvalidGuidanceSteps++;
            debugStatus = "NO COMMAND SIGNAL: " + reason;
            PublishDebug();
        }

        private void ResolveBinding()
        {
            if (aircraftBody == null)
                aircraftBody = GetComponent<Rigidbody>();
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
        }

        private float ResolveGravity()
        {
            if (sixDoFBody != null && sixDoFBody.debugEnvironment.valid && sixDoFBody.debugEnvironment.OwnsGravityThroughLoadSet
                && sixDoFBody.debugEnvironment.loadSetGravityMps2 > 0f)
                return sixDoFBody.debugEnvironment.loadSetGravityMps2;
            float unity = Physics.gravity.magnitude;
            return unity > 1e-3f ? unity : 9.80665f;
        }

        private void UpdateEnergy(float heightM, float speedMps, float g, float dt)
        {
            float he = heightM + speedMps * speedMps / (2f * g);
            if (!energyInitialized || !(dt > 0f))
            {
                debugSpecificEnergyRateMps = 0f;
                energyInitialized = true;
            }
            else
            {
                float rate = (he - lastEnergyHeight) / dt;
                float a = 1f - Mathf.Exp(-dt / 1f);
                debugSpecificEnergyRateMps += a * (rate - debugSpecificEnergyRateMps);
            }

            lastEnergyHeight = he;
            debugSpecificEnergyHeightM = he;
        }

        private void PublishDebug()
        {
            debugAim = aim;
            debugFreeLookYawPitchDeg = freeLook;
            debugAimInitialized = aimInitialized;
            debugHandsOff = handsOff;
            debugFreeLookHeld = freeLookHeld;
            debugFrameInputState = frameInputState;
            debugUsingScriptedTarget = useScriptedTarget;
            debugCommand = cached;
            debugHasCommand = hasCommand;
        }

        // ================================================================== telemetry

        /// <summary>Multi-line telemetry for the overlay or a log. Reads only.</summary>
        public string FormatTelemetry()
        {
            StringBuilder sb = new StringBuilder(1024);
            MavMouseAimGuidanceOutput o = debugGuidance;
            sb.AppendLine("MOUSE INSTRUCTOR V2 - " + MavMouseAimProvenance.MaverickTunedNonAuthoritative);
            sb.AppendLine("input " + debugInputState + (debugHandsOff ? " | HANDS OFF" : "") + (debugFreeLookHeld ? " | FREE LOOK" : "")
                          + " | loss policy " + inputLossPolicy);
            sb.Append("aim az ").Append(aim.azimuthDeg.ToString("F1")).Append(" el ").Append(aim.elevationDeg.ToString("F1"))
              .Append(" | ref ").Append(guidance.aimReference).AppendLine((o.limiters & MavAimGuidanceLimiter.NoseReferenceFallback) != 0 ? " (NOSE FALLBACK)" : "");
            sb.Append("error ").Append(o.targetErrorDeg.ToString("F1")).Append(" deg (nose ").Append(o.targetErrorFromNoseDeg.ToString("F1"))
              .Append(") | pitch-plane ").Append(o.pitchPlaneErrorDeg.ToString("F1")).Append(" lat ").Append(o.lateralErrorDeg.ToString("F1")).AppendLine();
            sb.Append("mode ").Append(o.mode).Append(" | bank ").Append(o.currentBankDeg.ToString("F0")).Append(" -> ")
              .Append(o.desiredBankDeg.ToString("F0")).Append(" (err ").Append(o.bankErrorDeg.ToString("F0")).Append(") | path rate ")
              .Append(o.desiredFlightPathRateDegSec.ToString("F1")).Append(" deg/s | lift req ").Append(o.requestedLiftG.ToString("F2")).AppendLine(" g");
            sb.Append("cmd pitch ").Append(cached.pitch.ToString("F2")).Append(" roll ").Append(cached.roll.ToString("F2"))
              .Append(" yaw ").Append(cached.yaw.ToString("F2")).Append(" | limiters ").AppendLine(o.limiters.ToString());
            sb.AppendLine("law " + boundLawName + (boundLawIsRateCommand ? "" : " | NOT RATE-COMMAND: neutral stick"));

            if (sixDoFBody != null)
            {
                MavFlightState s = sixDoFBody.debugState;
                sb.Append("TAS ").Append(s.trueAirspeedMps.ToString("F1")).Append(" m/s  M ").Append(s.mach.ToString("F2"))
                  .Append("  AoA ").Append(s.AlphaDeg.ToString("F1")).Append("  beta ").Append(s.BetaDeg.ToString("F2"))
                  .Append("  Nz ").Append(s.specificForceValid ? s.LoadFactorNz.ToString("F2") : "n/a").AppendLine();
                Vector3 pqr = s.aeroBodyRatesRadSec * Mathf.Rad2Deg;
                sb.Append("p ").Append(pqr.x.ToString("F1")).Append(" q ").Append(pqr.y.ToString("F1")).Append(" r ").Append(pqr.z.ToString("F1"))
                  .Append(" deg/s | energy ").Append(debugSpecificEnergyHeightM.ToString("F0")).Append(" m, ")
                  .Append(debugSpecificEnergyRateMps >= 0f ? "+" : "").Append(debugSpecificEnergyRateMps.ToString("F1")).AppendLine(" m/s");
                if (sixDoFBody.controlLaw != null)
                {
                    MavControlInput req = sixDoFBody.controlLaw.debugLastOutput;
                    sb.Append("surf req elev ").Append(req.elevatorDeg.ToString("F1")).Append(" ail ").Append(req.aileronDeg.ToString("F1"))
                      .Append(" rud ").Append(req.rudderDeg.ToString("F1"));
                }

                if (sixDoFBody.controlSurfaceActuator != null)
                {
                    MavControlInput act = sixDoFBody.controlSurfaceActuator.ActualSurfaceState;
                    sb.Append(" | actual ").Append(act.elevatorDeg.ToString("F1")).Append(" / ").Append(act.aileronDeg.ToString("F1"))
                      .Append(" / ").Append(act.rudderDeg.ToString("F1"));
                }

                sb.AppendLine();
                sb.Append("live-ready ").Append(sixDoFBody.debugReadyForLiveFdm ? "YES" : "NO").Append(" | armed ")
                  .Append(sixDoFBody.ArmedForLiveFlight ? "yes" : "NO").Append(" | loads ").Append(sixDoFBody.debugLoadApplications)
                  .Append(" | withheld ").Append(sixDoFBody.debugRejectedNotLiveReadyApplications).AppendLine();
                if (!sixDoFBody.debugReadyForLiveFdm)
                    sb.AppendLine("WARNING readiness: " + sixDoFBody.debugReadinessReason);
                if (sixDoFBody.propulsionModel != null && !sixDoFBody.propulsionModel.HasAuthoritativeData)
                    sb.AppendLine("WARNING thrust is NOT authoritative data (F-15 rig: THROTTLE INACTIVE - FIXED RESEARCH THRUST)");
            }

            if ((o.limiters & (MavAimGuidanceLimiter.LowSpeedPullCap | MavAimGuidanceLimiter.HighAlphaPullCap | MavAimGuidanceLimiter.LowAlphaPushCap)) != 0)
                sb.AppendLine("WARNING envelope soft cap active - the aircraft is near the edge of the model's admitted domain");
            sb.AppendLine("status " + debugStatus);
            return sb.ToString();
        }
    }
}
