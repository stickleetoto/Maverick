using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// The F-16's propulsion system: the shared <see cref="MavPropulsionSystem"/> configured with the
    /// F-16's single-engine installation.
    ///
    /// This is the entire aircraft-specific part of F-16 propulsion under the new architecture. There
    /// is no F-16 engine core here, no F-16 aggregation code, and no F-16 load composition - only the
    /// selection of an installation and an engine profile. That is the shape the architecture is
    /// supposed to produce, and it is why the F-15 will not need a second propulsion stack.
    ///
    /// Because <see cref="MavPropulsionSystem"/> is a <see cref="MavPropulsionModelBase"/>, this drops
    /// into <see cref="MavSixDoFBody.propulsionModel"/> exactly where
    /// <see cref="MavF16EnginePowerModel"/> did, and the load-application boundary is untouched.
    ///
    /// Thrust is exactly zero unless a deck is attached, which is the current and correct F-16 state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16PropulsionSystem : MavPropulsionSystem
    {
        [Header("F-16 Engine Data")]
        [Tooltip("Optional dimensional thrust deck. NULL means thrust stays exactly zero, which is the current F-16 condition: the TP-1538 Table VI transcription is not frozen. Attaching a deck does not make it authoritative - the deck declares that itself.")]
        public MavThrustDeckBase thrustDeck;

        [Tooltip("Rebuilds the installation from MavF16PropulsionInstallation on Awake. Off only if a caller wants to supply an installation by hand.")]
        public bool buildInstallationOnAwake = true;

        public override string PropulsionModelName
        {
            get
            {
                return thrustDeck != null
                    ? "F-16 propulsion: Garza/Morelli power dynamics + " + thrustDeck.DeckName
                    : "F-16 propulsion: Garza/Morelli power dynamics (thrust deck pending)";
            }
        }

        private void Awake()
        {
            if (buildInstallationOnAwake)
                ConfigureF16Installation();
        }

        /// <summary>
        /// Installs the F-16 reference engine in slot 0 and builds its runtime.
        ///
        /// Public and idempotent so editor tooling and validation can configure the component without
        /// relying on Unity having called Awake.
        /// </summary>
        public bool ConfigureF16Installation()
        {
            installation = MavF16PropulsionInstallation.CreateInstallation(thrustDeck);

            string error;
            bool ok = Build(true, out error);
            if (!ok)
                Debug.LogWarning("[MavF16PropulsionSystem] " + error, this);

            return ok;
        }
    }
}
