using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// The F-16's propulsion system: the shared <see cref="MavPropulsionSystem"/> configured with the
    /// F-16's single-engine installation.
    ///
    /// Default behaviour is intentionally unchanged from the pre-Phase-5D path: when
    /// <see cref="thrustDeck"/> is null, the installation carries no dimensional thrust deck and the
    /// shared runtime therefore produces exactly zero physical thrust. TP-1538 is NOT a production
    /// default and is never attached merely because this component exists.
    ///
    /// The frozen TP-1538 runtime is an explicit opt-in used by the isolated powered-reference path:
    /// callers must invoke <see cref="ConfigureTp1538SourcedInstallation"/> (or explicitly serialize
    /// a deck reference). Garza/Morelli remains the F-16-specific power-state law; this component
    /// only selects the installation and never applies physics loads itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16PropulsionSystem : MavPropulsionSystem
    {
        [Header("F-16 Engine Data")]
        [Tooltip("Optional dimensional thrust deck. NULL preserves the historical/default F-16 behaviour: dimensional physical thrust is exactly 0 N. TP-1538 is attached only by an explicit powered-reference opt-in.")]
        public MavThrustDeckBase thrustDeck;

        [Tooltip("Rebuilds the installation from MavF16PropulsionInstallation on Awake. Off only if a caller wants to supply an installation by hand.")]
        public bool buildInstallationOnAwake = true;

        public override string PropulsionModelName
        {
            get
            {
                return thrustDeck != null
                    ? "F-16 propulsion: Garza/Morelli power dynamics + " + thrustDeck.DeckName
                    : "F-16 propulsion: Garza/Morelli power dynamics (dimensional thrust unavailable)";
            }
        }

        private void Awake()
        {
            // IMPORTANT: do not auto-attach TP-1538 here. The null-deck path is the production/default
            // safety state and must remain exactly the same as before Phase 5D.
            if (buildInstallationOnAwake)
                ConfigureF16Installation();
        }

        /// <summary>
        /// Explicit Phase-5D powered-reference entry point.
        ///
        /// This is the only convenience path in this component that manufactures the frozen TP-1538
        /// deck. It is intended for isolated validation/reference rigs; calling ordinary
        /// <see cref="ConfigureF16Installation"/> never manufactures a deck.
        /// </summary>
        public bool ConfigureTp1538SourcedInstallation()
        {
            EnsureTp1538DeckAttached();
            return ConfigureF16Installation();
        }

        private MavF16Tp1538ThrustDeck EnsureTp1538DeckAttached()
        {
            MavF16Tp1538ThrustDeck deck = GetComponent<MavF16Tp1538ThrustDeck>();
            if (deck == null)
                deck = gameObject.AddComponent<MavF16Tp1538ThrustDeck>();

            deck.EnsureFrozenDataInstalled();
            thrustDeck = deck;
            return deck;
        }

        /// <summary>
        /// Installs the F-16 reference engine in slot 0 and builds its runtime.
        ///
        /// Public and idempotent so editor tooling and validation can configure the component without
        /// relying on Unity having called Awake. Deliberately preserves a null deck: this method does
        /// not attach TP-1538 and therefore retains the historical/default exactly-zero-thrust path.
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
