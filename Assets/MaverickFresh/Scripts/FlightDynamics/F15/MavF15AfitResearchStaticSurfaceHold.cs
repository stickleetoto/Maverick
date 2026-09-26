using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// WP-4A. A SOURCE-DEFINED STATIC surface setting for the research runtime body: the fixed
    /// stabilator of one known research equilibrium (a Table VII state recovered by WP-3B/WP-3C),
    /// with aileron, differential tail and rudder at the 0 deg every such equilibrium holds.
    ///
    /// WHAT IT IS NOT - each of these is false by construction and reported as false:
    ///   - not physical travel, and not a hard stop: it is one point, not an interval, and the
    ///     research actuator's travel authority stays unavailable (zero channels);
    ///   - not an actuator limit and not a rate: it has no rate field, and nothing moves - it is
    ///     placed before arming and cannot be changed while armed;
    ///   - not gearing and not a pilot-control mapping: no stick, pedal or law output reaches it.
    ///
    /// It is admitted only inside <see cref="MavF15ResearchDemonstratedControlRange"/> - the inputs
    /// the research source demonstrably tabulated - and that range is still not a physical limit.
    ///
    /// Distinct on purpose from <see cref="MavF15ResearchStaticControlState"/>, which stays
    /// STATIC_EQUILIBRIUM_VALIDATION_ONLY and can never become a surface state. This type exists
    /// because WP-4A needs the research BODY to sit at a known equilibrium's stabilator, and it can
    /// only become the actuator's held state through <see cref="MavF15AfitResearchStaticSurfaceHold"/>
    /// on a body the research runtime authority grants.
    /// </summary>
    [Serializable]
    public struct MavF15ResearchStaticSurfaceSetting
    {
        public const string Kind = "RESEARCH_SOURCE_DEFINED_STATIC_SURFACE_SETTING";

        public float symmetricStabilatorDeg;

        [Tooltip("The equilibrium this setting reproduces, e.g. 'Baumann Table VII point 150, WP-3C solve'. Required.")]
        public string sourceNote;

        /// <summary>Always false: a research source's tabulated input is not an aircraft limit.</summary>
        public bool IsPhysicalLimit
        {
            get { return false; }
        }

        /// <summary>Always false: one fixed point, no interval of travel.</summary>
        public bool ProvidesTravel
        {
            get { return false; }
        }

        /// <summary>Always false: nothing here moves, so there is no rate.</summary>
        public bool HasRateLimit
        {
            get { return false; }
        }

        /// <summary>Always false: no pilot or law command maps onto it.</summary>
        public bool HasGearing
        {
            get { return false; }
        }

        /// <summary>
        /// Admits a stabilator inside the research demonstrated range, with a named source. The
        /// other three channels are the demonstrated [0, 0] and are not parameters.
        /// </summary>
        public static bool TryCreate(
            float symmetricStabilatorDeg,
            string sourceNote,
            out MavF15ResearchStaticSurfaceSetting setting,
            out string reason)
        {
            setting = default(MavF15ResearchStaticSurfaceSetting);

            if (string.IsNullOrEmpty(sourceNote))
            {
                reason = "a source-defined static setting must name the equilibrium it reproduces";
                return false;
            }

            MavF15ResearchDemonstratedControlRange range =
                MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();

            if (!range.symmetricStabilator.Contains(symmetricStabilatorDeg))
            {
                reason = "symmetric stabilator " + symmetricStabilatorDeg.ToString("F4")
                    + " deg is outside the research demonstrated range "
                    + range.symmetricStabilator.minDeg.ToString("F1") + ".."
                    + range.symmetricStabilator.maxDeg.ToString("F1")
                    + " deg (not a physical limit; it is where the source's tabulated equilibria lie)";
                return false;
            }

            if (!range.differentialStabilator.Contains(0f) || !range.aileron.Contains(0f) || !range.rudder.Contains(0f))
            {
                reason = "the research demonstrated range no longer admits the neutral lateral surfaces";
                return false;
            }

            setting = new MavF15ResearchStaticSurfaceSetting
            {
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                sourceNote = sourceNote
            };
            reason = Kind;
            return true;
        }

        /// <summary>The held four-channel state: stabilator as set, the rest at the demonstrated 0.</summary>
        public MavF15SurfaceState ToHeldSurfaceState()
        {
            return new MavF15SurfaceState
            {
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                differentialStabilatorDeg = 0f,
                aileronDeg = 0f,
                rudderDeg = 0f
            };
        }
    }

    /// <summary>
    /// WP-4A. Holds the research body's surfaces at one <see cref="MavF15ResearchStaticSurfaceSetting"/>.
    ///
    /// The F-15 actuator (<see cref="MavF15ControlActuator"/>) stays the only owner of actual surface
    /// state: it finds this component on its GameObject and, while it is engaged and granted,
    /// publishes the held state as the actual one and ignores every command. Present but not
    /// granted, the actuator holds the surfaces NEUTRAL and says why - it never falls back to the
    /// command.
    ///
    /// Engaging and releasing are refused while the body is armed, so the setting is a static
    /// initial condition, never a control input. The research runtime authority must grant the
    /// body; anything but the research configuration fails closed. Writes no Rigidbody state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15AfitResearchStaticSurfaceHold : MonoBehaviour
    {
        public MavSixDoFBody sixDoFBody;

        [Tooltip("Set only through TryEngage, before arming.")]
        public bool engaged;

        public MavF15ResearchStaticSurfaceSetting setting;

        [Header("Debug")]
        public bool debugGranted;
        public string debugStatus = "not engaged";

        // Built on engage, so the per-step resolution the actuator performs allocates nothing.
        private string heldStatus;

        public bool TryEngage(MavF15ResearchStaticSurfaceSetting requested, out string reason)
        {
            Resolve();

            if (!MavF15ResearchRuntimeAuthority.TryGrant(sixDoFBody, out reason))
            {
                debugStatus = "REFUSED: " + reason;
                return false;
            }

            if (sixDoFBody.simulationEnabled)
            {
                reason = "static hold refused: the body is armed; a static setting is placed only before "
                    + "arming and is not a control input";
                debugStatus = "REFUSED: " + reason;
                return false;
            }

            MavF15ResearchStaticSurfaceSetting validated;
            if (!MavF15ResearchStaticSurfaceSetting.TryCreate(
                    requested.symmetricStabilatorDeg, requested.sourceNote, out validated, out reason))
            {
                reason = "static hold refused: " + reason;
                debugStatus = "REFUSED: " + reason;
                return false;
            }

            setting = validated;
            engaged = true;
            heldStatus = "held: " + Describe(setting);
            reason = "static hold engaged: " + Describe(setting);
            debugStatus = reason;
            return true;
        }

        public bool TryRelease(out string reason)
        {
            Resolve();
            if (sixDoFBody != null && sixDoFBody.simulationEnabled)
            {
                reason = "static hold release refused: the body is armed";
                return false;
            }

            engaged = false;
            setting = default(MavF15ResearchStaticSurfaceSetting);
            heldStatus = null;
            reason = "static hold released";
            debugStatus = reason;
            return true;
        }

        /// <summary>
        /// The state the actuator must hold. False - with the actuator then holding NEUTRAL - when
        /// nothing is engaged, the body is not granted, or the setting no longer validates.
        /// </summary>
        public bool TryResolveHeldState(out MavF15SurfaceState held, out string reason)
        {
            Resolve();
            held = MavF15SurfaceState.Neutral;
            debugGranted = false;

            if (!engaged)
            {
                reason = "no static setting engaged";
                debugStatus = reason;
                return false;
            }

            if (!MavF15ResearchRuntimeAuthority.TryGrant(sixDoFBody, out reason))
            {
                debugStatus = "REFUSED: " + reason;
                return false;
            }

            MavF15ResearchStaticSurfaceSetting validated;
            if (!MavF15ResearchStaticSurfaceSetting.TryCreate(
                    setting.symmetricStabilatorDeg, setting.sourceNote, out validated, out reason))
            {
                debugStatus = "REFUSED: " + reason;
                return false;
            }

            held = validated.ToHeldSurfaceState();
            debugGranted = true;
            if (heldStatus == null)
                heldStatus = "held: " + Describe(validated);
            reason = debugStatus = heldStatus;
            return true;
        }

        public static string Describe(MavF15ResearchStaticSurfaceSetting s)
        {
            return "symmetric stabilator " + s.symmetricStabilatorDeg.ToString("F4")
                + " deg, aileron / differential tail / rudder 0 (" + s.sourceNote
                + "); source-defined static setting - no travel, hard stop, rate or gearing";
        }

        private void Resolve()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
        }
    }
}
