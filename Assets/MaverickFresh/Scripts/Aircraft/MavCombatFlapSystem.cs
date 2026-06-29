using UnityEngine;

namespace MaverickFresh
{
    public enum MavFlapState
    {
        Retracted = 0,
        Combat = 1,
        Landing = 2
    }

    /// <summary>
    /// v0.20.7 simple War-Thunder-like flap controller.
    /// It temporarily modifies the current MavAeroBody/jet low-speed authority after the aircraft profile has been applied.
    /// F cycles Retracted -> Combat -> Landing -> Retracted. High speed auto-retracts to protect the airframe.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCombatFlapSystem : MonoBehaviour
    {
        [Header("v0.20.7 Combat Flaps")]
        public bool useFlaps = true;
        public KeyCode cycleKey = KeyCode.F;
        public MavFlapState state = MavFlapState.Retracted;
        public bool autoRetractAtHighSpeed = true;
        public float combatMaxSpeed = 260f;
        public float landingMaxSpeed = 170f;

        [Header("Combat Flap Multipliers")]
        public float combatLiftSlopeMultiplier = 1.08f;
        public float combatCd0Add = 0.010f;
        public float combatStallAoAAdd = 2.0f;
        public float combatLowSpeedPitchAuthorityMultiplier = 1.10f;
        public float combatMaxLiftGAdd = 0.35f;

        [Header("Landing Flap Multipliers")]
        public float landingLiftSlopeMultiplier = 1.18f;
        public float landingCd0Add = 0.028f;
        public float landingStallAoAAdd = 4.0f;
        public float landingLowSpeedPitchAuthorityMultiplier = 1.18f;
        public float landingMaxLiftGAdd = 0.55f;

        [Header("Debug")]
        public string debugState = "UP";
        public float debugSpeed;
        public bool debugAutoRetracted;
        public bool debugHasBaseline;

        private MavAeroBody aero;
        private MavMouseFlightJet jet;
        private MavAircraftProfileApplier profile;
        private string lastProfileStamp;
        private bool hasBaseline;
        private float baseClSlope;
        private float baseCd0;
        private float baseStallAoA;
        private float baseMaxLiftG;
        private float baseLowSpeedPitchAuthority;

        private void Awake()
        {
            Resolve();
        }

        private void Start()
        {
            Resolve();
            CaptureBaseline();
            ApplyState();
        }

        private void Update()
        {
            Resolve();
            WatchProfileReapply();

            if (!useFlaps)
            {
                state = MavFlapState.Retracted;
                ApplyState();
                return;
            }

            if (MavFreshInput.GetKeyDown(cycleKey))
            {
                CycleFlaps();
                ApplyState();
            }
        }

        private void FixedUpdate()
        {
            Resolve();
            debugSpeed = jet != null ? jet.speed : (GetComponent<Rigidbody>() != null ? GetComponent<Rigidbody>().linearVelocity.magnitude : 0f);
            debugAutoRetracted = false;

            if (!autoRetractAtHighSpeed || state == MavFlapState.Retracted)
                return;

            float limit = state == MavFlapState.Landing ? landingMaxSpeed : combatMaxSpeed;
            if (debugSpeed > limit)
            {
                state = MavFlapState.Retracted;
                debugAutoRetracted = true;
                ApplyState();
            }
        }

        public void SetState(MavFlapState newState)
        {
            state = newState;
            ApplyState();
        }

        public void CycleFlaps()
        {
            if (state == MavFlapState.Retracted) state = MavFlapState.Combat;
            else if (state == MavFlapState.Combat) state = MavFlapState.Landing;
            else state = MavFlapState.Retracted;
        }

        private void Resolve()
        {
            if (aero == null) aero = GetComponent<MavAeroBody>();
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (profile == null) profile = GetComponent<MavAircraftProfileApplier>();
        }

        private void WatchProfileReapply()
        {
            if (profile == null)
                return;

            if (lastProfileStamp != profile.lastApplied)
            {
                state = MavFlapState.Retracted;
                CaptureBaseline();
                ApplyState();
                lastProfileStamp = profile.lastApplied;
            }
        }

        private void CaptureBaseline()
        {
            Resolve();
            if (aero != null)
            {
                baseClSlope = aero.clSlopePerDeg;
                baseCd0 = aero.cd0;
                baseStallAoA = aero.stallAoADeg;
                baseMaxLiftG = aero.maxLiftG;
            }
            if (jet != null)
            {
                baseLowSpeedPitchAuthority = jet.lowSpeedPitchAuthority;
            }
            hasBaseline = aero != null || jet != null;
            debugHasBaseline = hasBaseline;
        }

        private void ApplyState()
        {
            if (!hasBaseline)
                CaptureBaseline();

            float liftMul = 1f;
            float cdAdd = 0f;
            float stallAdd = 0f;
            float lowPitchMul = 1f;
            float maxGAdd = 0f;
            debugState = "UP";

            if (state == MavFlapState.Combat)
            {
                liftMul = combatLiftSlopeMultiplier;
                cdAdd = combatCd0Add;
                stallAdd = combatStallAoAAdd;
                lowPitchMul = combatLowSpeedPitchAuthorityMultiplier;
                maxGAdd = combatMaxLiftGAdd;
                debugState = "COMBAT";
            }
            else if (state == MavFlapState.Landing)
            {
                liftMul = landingLiftSlopeMultiplier;
                cdAdd = landingCd0Add;
                stallAdd = landingStallAoAAdd;
                lowPitchMul = landingLowSpeedPitchAuthorityMultiplier;
                maxGAdd = landingMaxLiftGAdd;
                debugState = "LANDING";
            }

            if (aero != null)
            {
                aero.clSlopePerDeg = baseClSlope * liftMul;
                aero.cd0 = baseCd0 + cdAdd;
                aero.stallAoADeg = baseStallAoA + stallAdd;
                aero.maxLiftG = baseMaxLiftG + maxGAdd;
            }
            if (jet != null)
            {
                jet.lowSpeedPitchAuthority = baseLowSpeedPitchAuthority * lowPitchMul;
            }
        }
    }
}
