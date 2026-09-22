using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The F-15's propulsion system: the shared <see cref="MavPropulsionSystem"/> configured with
    /// the NASA 836 twin F100-PW-100 installation.
    ///
    /// WHAT THIS DOES AND DOES NOT PROVIDE
    /// -----------------------------------
    /// It provides the twin-engine INSTALLATION: two slots, two independent runtime states, two
    /// throttle channels, one shared engine-profile object, and the r x F installation-moment
    /// path. All of that is aircraft-independent machinery the F-16 already exercises; nothing is
    /// duplicated for the F-15.
    ///
    /// It does NOT provide engine PERFORMANCE. No exact-target thrust deck, spool law, fuel-flow
    /// map or inlet-recovery schedule has been recovered for the F100-PW-100 on NASA 836, so the
    /// installation carries no deck and dimensional thrust is exactly zero. That is the honest
    /// state, and the F-16's Garza/Morelli power law is deliberately NOT borrowed to fill it: that
    /// law describes a different engine, and the shared runtime supporting it is not a licence to
    /// apply it here.
    ///
    /// THE ENGINE-OUT TRAP
    /// -------------------
    /// Mount coordinates are undeclared, so both slots sit at the origin. With zero thrust that is
    /// harmless - r x F is zero because F is zero. The moment it stops being harmless is when
    /// somebody attaches a thrust deck: force becomes real, r is still zero, and the aircraft
    /// quietly models both engines on the centreline. An engine failure would then produce no yaw
    /// at all, and nothing about the number 0 N*m would look wrong.
    ///
    /// The guard is <see cref="MavPropulsionSystem.IsAcceptableForLiveFlight"/>, which already
    /// refuses undeclared installation geometry. <see cref="DescribeInstallationGaps"/> spells the
    /// same thing out in words so it is visible before someone goes looking for the yaw that is
    /// missing.
    ///
    /// Measuring those coordinates off the F-15E visual mesh in this repository would NOT close
    /// the gap. The physics target is NASA F-15B 836; the mesh is a different aircraft and is a
    /// visual proxy only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PropulsionSystem : MavPropulsionSystem
    {
        [Header("F-15 Engine Data")]
        [Tooltip("Optional dimensional thrust deck, SHARED by both engines. NULL is the current and correct state: no F100-PW-100 deck has been accepted for NASA 836, so dimensional thrust is exactly 0 N. Attaching one without declaring mount geometry gives a twin-engine aircraft with no engine-out yaw - see DescribeInstallationGaps.")]
        public MavThrustDeckBase thrustDeck;

        [Tooltip("Rebuilds the twin installation from MavF15PropulsionSkeleton on Awake. Off only if a caller supplies an installation by hand.")]
        public bool buildInstallationOnAwake = true;

        [Header("Debug")]
        [TextArea(3, 10)]
        public string debugInstallationGaps = "not built";

        public override string PropulsionModelName
        {
            get
            {
                return thrustDeck != null
                    ? "F-15 twin F100-PW-100: " + thrustDeck.DeckName
                      + " (NOT exact-target validated)"
                    : "F-15 twin F100-PW-100 (engine performance data unavailable; zero thrust)";
            }
        }

        private void Awake()
        {
            if (buildInstallationOnAwake)
                ConfigureF15Installation();
        }

        /// <summary>
        /// Installs the twin F100 skeleton and builds both runtimes.
        ///
        /// Public and idempotent so editor tooling and validation can configure the component
        /// without relying on Unity having called Awake, matching the F-16 entry point. Preserves
        /// a null deck unless one was explicitly assigned: this method never manufactures engine
        /// performance data.
        /// </summary>
        public bool ConfigureF15Installation()
        {
            installation = MavF15PropulsionSkeleton.CreateTwinSkeleton(thrustDeck);

            string error;
            bool ok = Build(true, out error);
            if (!ok)
                Debug.LogWarning("[MavF15PropulsionSystem] " + error, this);

            debugInstallationGaps = DescribeInstallationGaps();
            return ok;
        }

        /// <summary>
        /// What is still missing before this propulsion system may be believed, in words.
        ///
        /// The numeric flags on the base class answer "is it acceptable"; this answers "what
        /// exactly is absent", which is the question someone actually has when the aircraft will
        /// not move.
        /// </summary>
        public string DescribeInstallationGaps()
        {
            if (installation == null || installation.EngineCount == 0)
                return "no installation built";

            System.Text.StringBuilder gaps = new System.Text.StringBuilder(512);

            if (thrustDeck == null)
            {
                gaps.AppendLine(
                    "THRUST: no dimensional deck. Dimensional thrust is exactly 0 N. "
                    + "Closing this needs an F100-PW-100 thrust deck matched to the NASA 836 "
                    + "installation - NASA TP-1373, TM-X-3261 and TP-1034 are the candidate "
                    + "source set, with engine-build differences kept explicit.");
            }

            int undeclaredGeometry = 0;
            for (int i = 0; i < installation.engines.Length; i++)
            {
                MavEngineInstallation slot = installation.engines[i];
                if (slot != null && !slot.geometryDeclared)
                    undeclaredGeometry++;
            }

            if (undeclaredGeometry > 0)
            {
                gaps.AppendLine(
                    "GEOMETRY: " + undeclaredGeometry + " of " + installation.EngineCount
                    + " mount positions and thrust lines are undeclared, so every r x F "
                    + "installation moment is zero by absence rather than by measurement. "
                    + "A thrust deck attached in this state would give a twin-engine aircraft "
                    + "with NO engine-out yaw. Do not measure these off the F-15E visual mesh: "
                    + "the physics target is NASA F-15B 836.");
            }

            gaps.AppendLine(
                "TRANSIENTS: no sourced spool/transient law. The profile selects "
                + "InstantNoSourcedTransient, which is declared rather than modelled. The F-16 "
                + "Garza/Morelli law describes a different engine and is not applicable.");

            gaps.AppendLine(
                "INLET / FUEL: no inlet-recovery schedule and no fuel-flow map. "
                + "NASA CR-144866 covers inlet/engine integration context for this airframe.");

            return gaps.ToString();
        }
    }
}
