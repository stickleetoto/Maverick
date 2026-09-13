using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// One engine's throttle-to-power law and power-state transient, as a pure strategy.
    ///
    /// This interface is the seam that keeps aircraft-specific equations OUT of the shared runtime.
    /// <see cref="MavEngineRuntime"/> owns the mutable power value and calls into an implementation
    /// of this to find out how it should move; it never contains a breakpoint, a gearing constant, or
    /// a time constant of its own.
    ///
    /// Implementations must be STATELESS. The whole point of the profile/runtime split is that a
    /// twin-engine aircraft shares one engine definition while holding two independent states, and a
    /// strategy that cached the last power value would quietly reintroduce the sharing this
    /// architecture exists to prevent. State belongs to the runtime, always.
    ///
    /// Power is expressed in PERCENT (0..100) because the sourced F-16 model is written that way and
    /// converting it at this boundary would invite rounding differences into a regression suite that
    /// is meant to be bit-stable.
    /// </summary>
    public interface IMavEnginePowerDynamics
    {
        /// <summary>Identity of the law, for telemetry. Names the source, not the aircraft.</summary>
        string DynamicsName { get; }

        /// <summary>Throttle 0..1 to commanded power percent 0..100.</summary>
        float CommandedPowerPercent(float throttle01);

        /// <summary>Rate of change of actual power, percent per second. Pure: no state, no clock.</summary>
        float PowerRatePercentPerSec(float actualPowerPercent, float commandedPowerPercent);

        /// <summary>
        /// Advances actual power by one step. Separate from the rate so an implementation may choose
        /// its own integration - the sourced part of a model is the derivative, while the numerical
        /// method is Maverick infrastructure and is not claimed to match any original integrator.
        /// </summary>
        float StepActualPowerPercent(
            float actualPowerPercent,
            float commandedPowerPercent,
            float deltaTime);
    }

    /// <summary>
    /// No sourced transient: actual power equals commanded power immediately.
    ///
    /// This is the honest default for an engine whose spool data is unavailable. It is NOT a claim
    /// that the engine responds instantly - it is a refusal to invent a time constant. An aircraft
    /// using this gets a visibly unrealistic throttle response rather than a plausible-looking
    /// fabricated one, which is the intended trade.
    ///
    /// Throttle maps linearly to 0..100 percent. That is plumbing, not sourced gearing, and any
    /// engine with real gearing data must select a law that carries it.
    /// </summary>
    public sealed class MavInstantEnginePowerDynamics : IMavEnginePowerDynamics
    {
        public static readonly MavInstantEnginePowerDynamics Instance =
            new MavInstantEnginePowerDynamics();

        public string DynamicsName
        {
            get { return "instant power response (no sourced transient)"; }
        }

        public float CommandedPowerPercent(float throttle01)
        {
            return Mathf.Clamp01(throttle01) * 100f;
        }

        public float PowerRatePercentPerSec(float actualPowerPercent, float commandedPowerPercent)
        {
            // An instant response has no finite rate. Reporting 0 would read as "not moving", which
            // is the opposite of the truth, so report the gap that is closed within the step.
            return Mathf.Clamp(commandedPowerPercent, 0f, 100f)
                   - Mathf.Clamp(actualPowerPercent, 0f, 100f);
        }

        public float StepActualPowerPercent(
            float actualPowerPercent,
            float commandedPowerPercent,
            float deltaTime)
        {
            return Mathf.Clamp(commandedPowerPercent, 0f, 100f);
        }
    }

    /// <summary>
    /// Resolves a profile's declared law to an implementation.
    ///
    /// A switch, not reflection: the set of laws is small, closed, and worth reading in one place,
    /// and a profile must not be able to name a law that does not exist. Implementations are
    /// stateless singletons, so resolving costs no allocation and can happen during a physics step
    /// without generating garbage.
    ///
    /// Registration rather than a hard reference keeps the aircraft-specific laws out of this file:
    /// F-16 equations live in the F16 namespace and register themselves, so the shared layer has no
    /// compile-time knowledge of any particular aircraft.
    /// </summary>
    public static class MavEnginePowerDynamicsFactory
    {
        private static IMavEnginePowerDynamics f16GarzaMorelli;

        /// <summary>
        /// How many times an F-16 registration has been applied.
        ///
        /// Exposed so validation can prove registration is IDEMPOTENT rather than merely working: a
        /// registrar called from three lifecycle hooks must not accumulate registrations, and a
        /// counter is the only way to tell "registered once" from "registered four times with the same
        /// value". Registering the identical instance again does not increment it.
        /// </summary>
        public static int F16RegistrationCount { get; private set; }

        /// <summary>
        /// Registers the F-16 Garza/Morelli implementation. Called by the F16 assembly so this
        /// shared file does not have to reference an aircraft-specific type.
        ///
        /// Passing NULL clears the registration. That is deliberate and is the only supported way to
        /// reach the unregistered state after startup: it lets validation prove the fail-closed path
        /// without a test-only back door, and it means an unregistered law is expressible rather than
        /// unreachable.
        /// </summary>
        public static void RegisterF16GarzaMorelli(IMavEnginePowerDynamics implementation)
        {
            if (ReferenceEquals(f16GarzaMorelli, implementation))
                return;

            f16GarzaMorelli = implementation;
            F16RegistrationCount++;
        }

        /// <summary>
        /// Whether a law can currently be resolved. False for a law whose implementation has not
        /// registered yet, which the caller must treat as a configuration error rather than
        /// substituting a different law.
        /// </summary>
        public static bool CanResolve(MavEnginePowerDynamicsLaw law)
        {
            switch (law)
            {
                case MavEnginePowerDynamicsLaw.InstantNoSourcedTransient:
                    return true;

                case MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState:
                    return f16GarzaMorelli != null;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Resolves the law, or returns null.
        ///
        /// Deliberately returns null instead of falling back to the instant law. A silent
        /// substitution would let an aircraft fly a different engine model than its profile declares,
        /// and the caller failing closed on null is a better outcome than that.
        /// </summary>
        public static IMavEnginePowerDynamics Resolve(MavEnginePowerDynamicsLaw law)
        {
            switch (law)
            {
                case MavEnginePowerDynamicsLaw.InstantNoSourcedTransient:
                    return MavInstantEnginePowerDynamics.Instance;

                case MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState:
                    return f16GarzaMorelli;

                default:
                    return null;
            }
        }
    }
}
