using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// One engine's contribution for one physics step, with its own provenance attached.
    ///
    /// Per-engine rather than aggregated because a twin-engine aircraft that reports a single power
    /// state and a single authority flag cannot answer "which engine is out of envelope?" - ENG-004.
    /// A struct so an array of these can be reused every step without allocating.
    /// </summary>
    [Serializable]
    public struct MavEngineLoadResult
    {
        public int slotId;
        public string engineProfileId;

        [Tooltip("Throttle this engine actually received, after channel resolution.")]
        public float throttle01;

        public float commandedPowerPercent;
        public float actualPowerPercent;

        [Tooltip("Rate of change of actual power, percent per second, from the engine's power law.")]
        public float powerRatePercentPerSec;

        [Tooltip("Scalar thrust along the installed thrust direction, N.")]
        public float thrustN;

        [Tooltip("Force contributed in aerodynamic body axes, N.")]
        public Vector3 forceAeroBodyN;

        [Tooltip("Moment contributed in aerodynamic body axes, N*m: r x F plus any intrinsic engine moment.")]
        public Vector3 momentAeroBodyNm;

        [Tooltip("False when this engine's dimensional thrust is not backed by authoritative data.")]
        public bool thrustAuthoritative;

        [Tooltip("Provenance of this engine's thrust number, as an enum. Stored rather than formatted into statusReason because an enum-to-string conversion per engine per step is a real allocation - the Unity run measured it. Use DescribeThrustStatus() for human-readable text.")]
        public MavThrustDataAuthority thrustAuthority;

        [Tooltip("False when the query fell outside the thrust deck's validated envelope.")]
        public bool insideThrustEnvelope;

        [Tooltip("Whether this engine contributed loads: installed, enabled, not failed, not cut off. The propulsion system sums ONLY running engines.")]
        public bool running;

        [Tooltip("Commanded fuel cutoff this step. An OPERATIONAL state - it never makes the aggregate non-authoritative.")]
        public bool cutoff;

        public string statusReason;

        public static MavEngineLoadResult Zero
        {
            get { return new MavEngineLoadResult(); }
        }

        /// <summary>
        /// Human-readable thrust provenance, composed ON DEMAND.
        ///
        /// This used to be baked into <see cref="statusReason"/> every step. An enum-to-string
        /// conversion is reflective and allocating, and doing it per engine per physics step was
        /// measurable GC pressure in Unity - so the enum is stored and the text is built only when
        /// something actually reads it.
        /// </summary>
        public string DescribeThrustStatus()
        {
            return thrustAuthority + " / " + (statusReason != null ? statusReason : "no status");
        }
    }

    /// <summary>
    /// INDEPENDENT MUTABLE STATE for one physical engine.
    ///
    /// This class is the reason the profile/installation split exists. Two F-15 engines share one
    /// <see cref="MavEngineProfile"/> object and one <see cref="MavEngineInstallation"/> pattern, but
    /// each gets its own runtime, so at any instant the left engine can sit at 90% power while the
    /// right sits at 40%. Nothing here is static and nothing is shared: put a field on the profile
    /// instead and that guarantee is gone.
    ///
    /// A plain class, not a MonoBehaviour. An engine is not a scene object - it is state owned by the
    /// aircraft's propulsion system - and keeping it plain means the whole engine model can be
    /// validated deterministically without a GameObject, a scene, or a physics step.
    ///
    /// Ownership boundary: this class computes loads and returns them. It holds no Rigidbody
    /// reference and cannot apply anything. <see cref="MavSixDoFBody"/> remains the single place
    /// where a load reaches the physics engine.
    /// </summary>
    public sealed class MavEngineRuntime
    {
        private readonly MavEngineInstallation installation;
        private readonly IMavEnginePowerDynamics dynamics;

        // ---- mutable engine state: the entire point of this class -------------------------------
        private float actualPowerPercent;
        private float commandedPowerPercent;
        private bool failed;

        /// <summary>Last computed contribution, kept for telemetry between steps.</summary>
        public MavEngineLoadResult LastResult;

        public MavEngineRuntime(MavEngineInstallation installation, IMavEnginePowerDynamics dynamics)
        {
            this.installation = installation;
            this.dynamics = dynamics;
            LastResult = MavEngineLoadResult.Zero;

            if (installation != null)
            {
                LastResult.slotId = installation.slotId;
                LastResult.engineProfileId = installation.engineProfile != null
                    ? installation.engineProfile.engineProfileId
                    : "none";
            }
        }

        public MavEngineInstallation Installation
        {
            get { return installation; }
        }

        public IMavEnginePowerDynamics Dynamics
        {
            get { return dynamics; }
        }

        public float ActualPowerPercent
        {
            get { return actualPowerPercent; }
        }

        public float CommandedPowerPercent
        {
            get { return commandedPowerPercent; }
        }

        /// <summary>
        /// Whether this engine has been failed. Exists so the asymmetric-thrust path can be exercised
        /// and validated; there is deliberately no failure GAMEPLAY here - no probability, no damage
        /// model, no automatic shutdown - because none of that is sourced yet (ENG-010).
        /// </summary>
        public bool Failed
        {
            get { return failed; }
        }

        /// <summary>
        /// Whether this engine can contribute at all: installed, enabled and not failed.
        ///
        /// Does NOT consider cutoff, because cutoff is commanded per step and arrives as an argument
        /// rather than as runtime state. The authoritative per-step answer is
        /// <see cref="MavEngineLoadResult.running"/>, which the propulsion system uses.
        /// </summary>
        public bool IsContributing
        {
            get { return installation != null && installation.enabled && !failed; }
        }

        /// <summary>Sets the failed flag. A failed engine produces zero thrust and stops spooling.</summary>
        public void SetFailed(bool value)
        {
            failed = value;
        }

        /// <summary>
        /// Resets to a defined state, matching the requested throttle.
        ///
        /// Power is set to the commanded value rather than to zero: an engine that has just been
        /// initialised in flight is at its commanded power, and spooling it up from zero every time
        /// ownership initialises would be an invented transient.
        /// </summary>
        public void Reset(float throttle01)
        {
            failed = false;
            commandedPowerPercent = dynamics != null
                ? dynamics.CommandedPowerPercent(Mathf.Clamp01(throttle01))
                : 0f;
            actualPowerPercent = commandedPowerPercent;

            LastResult = MavEngineLoadResult.Zero;
            if (installation != null)
            {
                LastResult.slotId = installation.slotId;
                LastResult.engineProfileId = installation.engineProfile != null
                    ? installation.engineProfile.engineProfileId
                    : "none";
            }
            LastResult.throttle01 = Mathf.Clamp01(throttle01);
            LastResult.commandedPowerPercent = commandedPowerPercent;
            LastResult.actualPowerPercent = actualPowerPercent;
        }

        /// <summary>
        /// Advances this engine one step and returns its contribution.
        ///
        /// Sequence, deliberately: throttle to commanded power, commanded power to actual power
        /// through the engine's own law, actual power to scalar thrust through the deck, scalar thrust
        /// to a force along the installed direction, and finally r x F for the moment. Each stage has
        /// one owner, and the aircraft-specific part is confined to the power law.
        ///
        /// A non-finite throttle or dt is rejected rather than propagated: a NaN reaching the
        /// Rigidbody destroys the simulation, and the failure is much easier to diagnose here.
        /// </summary>
        public MavEngineLoadResult Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            float deltaTime)
        {
            return Evaluate(state, atmosphere, throttle01, false, deltaTime);
        }

        /// <summary>
        /// Advances this engine one step, with a commanded fuel-cutoff flag.
        ///
        /// Cutoff is modelled as commanded power zero plus exactly zero thrust. A real cutoff is not
        /// the same as closing the throttle - there is a shutdown transient, a windmilling drag term,
        /// and a relight procedure - and none of that is sourced for either aircraft (ENG-010). So the
        /// spool decays through the engine's own sourced law toward zero commanded power, and thrust is
        /// held at zero rather than modelled through an invented winddown curve.
        /// </summary>
        public MavEngineLoadResult Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            bool commandedCutoff,
            float deltaTime)
        {
            MavEngineLoadResult result = MavEngineLoadResult.Zero;
            result.slotId = installation != null ? installation.slotId : -1;
            result.engineProfileId = installation != null && installation.engineProfile != null
                ? installation.engineProfile.engineProfileId
                : "none";

            if (installation == null || installation.engineProfile == null)
            {
                result.statusReason = "no engine installed in this slot";
                LastResult = result;
                return result;
            }

            if (dynamics == null)
            {
                // Resolution failed and we deliberately did not substitute another law. Fail closed.
                result.statusReason = "power-dynamics law "
                                      + installation.engineProfile.powerDynamicsLaw
                                      + " could not be resolved; engine produces nothing";
                result.actualPowerPercent = actualPowerPercent;
                LastResult = result;
                return result;
            }

            if (!installation.enabled)
            {
                result.statusReason = "installation disabled";
                result.actualPowerPercent = actualPowerPercent;
                LastResult = result;
                return result;
            }

            if (!IsFinite(throttle01) || !IsFinite(deltaTime))
            {
                result.statusReason = "non-finite throttle or deltaTime rejected";
                result.actualPowerPercent = actualPowerPercent;
                LastResult = result;
                return result;
            }

            float throttle = Mathf.Clamp01(throttle01);
            result.throttle01 = throttle;
            result.cutoff = commandedCutoff;

            // ---- power state: the aircraft-specific law, called but not implemented here ---------
            //
            // A failed or cut-off engine commands zero power. The spool still decays through the
            // engine's own sourced law rather than snapping, because the decay from a commanded zero
            // IS part of that law - it is the windmilling/relight behaviour that is unsourced, not
            // this.
            commandedPowerPercent = (failed || commandedCutoff)
                ? 0f
                : dynamics.CommandedPowerPercent(throttle);

            result.powerRatePercentPerSec =
                dynamics.PowerRatePercentPerSec(actualPowerPercent, commandedPowerPercent);

            actualPowerPercent = dynamics.StepActualPowerPercent(
                actualPowerPercent, commandedPowerPercent, deltaTime);

            if (!IsFinite(actualPowerPercent))
            {
                // A law that returned garbage must not be allowed to carry it into the load set.
                actualPowerPercent = 0f;
                result.statusReason = "power law returned a non-finite state; engine held at zero";
                LastResult = result;
                return result;
            }

            result.commandedPowerPercent = commandedPowerPercent;
            result.actualPowerPercent = actualPowerPercent;

            if (failed || commandedCutoff)
            {
                // OPERATIONAL, not a provenance failure. thrustAuthoritative is deliberately left at
                // its default here and the propulsion system does not read it for a non-running
                // engine: whether this engine's DATA is sourced is a question about its profile, which
                // MavPropulsionSystem asks separately. Marking a shut-down engine's data unavailable
                // would make "the pilot shut it down" indistinguishable from "we never had numbers".
                result.statusReason = commandedCutoff
                    ? "commanded fuel cutoff: zero thrust, power decaying to zero"
                    : "engine failed: zero thrust, power decaying to zero";
                result.running = false;
                LastResult = result;
                return result;
            }

            result.running = true;

            // ---- dimensional thrust: whatever the deck can honestly supply, or nothing -----------
            MavThrustDeckBase deck = installation.engineProfile.thrustDeck;
            if (deck == null || deck.Authority == MavThrustDataAuthority.Unavailable)
            {
                result.thrustN = 0f;
                result.thrustAuthoritative = false;
                result.thrustAuthority = MavThrustDataAuthority.Unavailable;
                result.insideThrustEnvelope = false;
                result.statusReason = "no dimensional thrust deck: thrust remains unavailable";

                // Still publish the intrinsic moment: a rotor angular-momentum term does not depend
                // on thrust existing. It is zero unless a source declared it.
                result.momentAeroBodyNm = IntrinsicMomentAeroBodyNm(state);
                LastResult = result;
                return result;
            }

            MavThrustDeckResult deckResult = deck.Evaluate(
                MavThrustDeckQuery.Create(state.worldPositionM.y, state.mach, actualPowerPercent));

            result.insideThrustEnvelope = deckResult.insideEnvelope;

            // Reference copies, not concatenation. Building "Authority / reason" here cost an enum
            // ToString plus two joins for every engine on every physics step; the enum and the deck's
            // own string are carried instead, and DescribeThrustStatus() composes on demand.
            result.thrustAuthority = deck.Authority;
            result.statusReason = deckResult.statusReason;

            if (!deckResult.valid || !IsFinite(deckResult.thrustN))
            {
                result.thrustN = 0f;
                result.thrustAuthoritative = false;
                result.momentAeroBodyNm = IntrinsicMomentAeroBodyNm(state);
                LastResult = result;
                return result;
            }

            result.thrustN = deckResult.thrustN;
            result.thrustAuthoritative =
                deckResult.authority == MavThrustDataAuthority.Authoritative;

            // ---- force and moment from installation geometry -------------------------------------
            Vector3 force = installation.UnitThrustDirection * deckResult.thrustN;
            result.forceAeroBodyN = force;

            // r x F is the whole mechanism behind asymmetric-thrust yaw. There is no separate yaw
            // term anywhere in this file, and there must never be one: a hand-added torque would
            // produce a moment that does not correspond to the forces actually applied.
            result.momentAeroBodyNm =
                Vector3.Cross(installation.positionAeroBodyM, force)
                + IntrinsicMomentAeroBodyNm(state);

            LastResult = result;
            return result;
        }

        /// <summary>
        /// Moment the engine produces on its own, independent of where it is mounted.
        ///
        /// Today that is only the gyroscopic term from rotor angular momentum (ENG-008), and it is
        /// zero unless a source declared the rotor momentum - which for both the F-16 and the F-15 it
        /// currently has not, in the form this path needs. Kept as its own method so the r x F term
        /// above stays a clean statement of the installation geometry.
        /// </summary>
        private Vector3 IntrinsicMomentAeroBodyNm(MavFlightState state)
        {
            MavEngineRotorAngularMomentum rotor =
                installation.engineProfile.rotorAngularMomentum;

            if (!rotor.available)
                return Vector3.zero;

            // M_gyro = -omega x h, with h along the spool axis. The sign follows the reaction on the
            // airframe rather than on the rotor.
            Vector3 h = rotor.spinAxisAeroBody.normalized * rotor.magnitudeKgM2PerSec;
            return -Vector3.Cross(state.aeroBodyRatesRadSec, h);
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
