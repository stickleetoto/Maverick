using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Registers the F-16 power-dynamics implementation with the shared engine-law factory.
    ///
    /// This exists so the dependency points the right way. <see cref="MavEnginePowerDynamicsFactory"/>
    /// lives in the aircraft-independent Core layer and must not reference an aircraft-specific type,
    /// so the F-16 side announces itself instead. A shared layer that had to know about every aircraft
    /// would not be a shared layer.
    ///
    /// ================================ REGISTRATION LIFECYCLE ================================
    ///
    /// Registration happens through three independent routes, because the factory is reached from
    /// three contexts with different lifecycles. Any one of them is sufficient; they overlap on
    /// purpose, and overlapping is safe because <see cref="EnsureRegistered"/> is idempotent.
    ///
    ///   1. PLAYER BUILD and PLAY MODE - [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] below.
    ///      This is a UnityEngine attribute and ships in the player. It runs before any scene loads,
    ///      therefore before any Awake, therefore before any propulsion system can build.
    ///
    ///   2. EDITOR, without entering play mode - MavF16EngineLawRegistrarEditor, which lives under
    ///      Editor/ in the editor-only assembly. It is NOT in this file: a UnityEditor reference in a
    ///      runtime script, even behind #if UNITY_EDITOR, keeps that reference present in the runtime
    ///      assembly while the editor is running. Putting it in the editor assembly removes the
    ///      question entirely.
    ///
    ///   3. ANY CONTEXT, on demand - an explicit EnsureRegistered() call from
    ///      MavF16PropulsionInstallation.CreateEngineProfile. This covers offline harnesses, which have
    ///      neither Unity hook, and it covers any ordering surprise in the two above: anything that
    ///      builds an F-16 engine profile registers the law first, by construction.
    ///
    /// ORDERING. Route 1 runs before scene load and route 3 runs inside profile construction, so the
    /// law is registered before any <see cref="MavPropulsionSystem.Build"/> that needs it. There is no
    /// ordering in which a profile exists but its law does not.
    ///
    /// DOMAIN RELOAD. Unity clears statics on domain reload, so both the factory's registration and
    /// this class's cached flag reset together. They cannot disagree, because the flag is not cached:
    /// see below.
    ///
    /// FAIL CLOSED. If registration somehow has not happened, the law is NOT silently replaced.
    /// <c>Resolve</c> returns null and <see cref="MavPropulsionSystem.Build"/> refuses, naming the
    /// missing law. An aircraft flying a different engine model than its profile declares would be far
    /// worse than one that will not start.
    /// </summary>
    public static class MavF16EngineLawRegistrar
    {
        /// <summary>
        /// Whether the factory can currently resolve the F-16 law.
        ///
        /// Asks the FACTORY rather than a local bool. A cached flag can disagree with the thing it
        /// describes - a domain reload that cleared one and not the other, or a caller clearing the
        /// registration directly - and then this class would report success while
        /// <c>Resolve</c> returned null. Asking the authority cannot drift from it.
        /// </summary>
        public static bool IsRegistered
        {
            get
            {
                return MavEnginePowerDynamicsFactory.CanResolve(
                    MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);
            }
        }

        /// <summary>
        /// Registers the law if the factory cannot already resolve it.
        ///
        /// Safe to call from anywhere, any number of times, including from a static context in an
        /// offline test. Idempotent and self-healing: because it checks the factory rather than a
        /// local flag, it re-registers after anything that cleared the registration, and repeated
        /// calls do not accumulate registrations.
        /// </summary>
        public static void EnsureRegistered()
        {
            if (IsRegistered)
                return;

            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(
                MavF16GarzaMorelliEngineDynamics.Instance);
        }

        /// <summary>
        /// Route 1: player build and play mode. A UnityEngine attribute, so it ships in the player.
        ///
        /// Public rather than private so the editor-assembly registrar and validation can call the
        /// same entry point the engine calls, instead of duplicating its body.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterOnGameStart()
        {
            EnsureRegistered();
        }
    }
}
