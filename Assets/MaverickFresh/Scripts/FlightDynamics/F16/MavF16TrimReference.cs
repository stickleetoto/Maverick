using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// F-16 trim entry point: builds a <see cref="MavTrimPlant"/> from the data already frozen in
    /// this branch and hands it to the aircraft-independent steady-flight solver.
    ///
    /// Sources used, and nothing else:
    ///   - geometry and validity envelope : MavF16MorelliReference (NASA Morelli compact model)
    ///   - mass                           : MavF16MassReference (NASA nominal mass properties)
    ///   - aerodynamic coefficients       : MavF16MorelliPolynomial, evaluated at zero body rates
    ///   - propulsion                     : MavF16EnginePowerModel steady power state, which
    ///                                      currently produces exactly zero thrust
    ///
    /// Nothing is invented here. In particular no thrust value is supplied, because the
    /// altitude/Mach thrust deck is not frozen in this repository. The consequence is stated
    /// plainly rather than worked around: a powered straight-and-level trim is NOT achievable for
    /// the F-16 on this branch, and the solver reports
    /// <see cref="MavTrimStatus.ConvergedButThrustUnavailable"/> together with the thrust the
    /// airframe would have needed.
    ///
    /// The unpowered-glide trim IS achievable with zero thrust, and is provided so the trim
    /// infrastructure is demonstrably exercised against real frozen aerodynamic data instead of
    /// only against a synthetic test plant.
    ///
    /// Everything in this class is pure: no component is read or written, and no Rigidbody exists
    /// anywhere in the call graph.
    /// </summary>
    public static class MavF16TrimReference
    {
        /// <summary>
        /// Builds the F-16 trim plant from frozen reference data.
        ///
        /// The aerodynamic delegate evaluates the compact Morelli polynomial at ZERO body rates,
        /// which is correct by definition for a steady trim, and applies the same published-envelope
        /// clamps the flying model uses so the trim solution is a point the aerodynamic model would
        /// actually reproduce in flight.
        /// </summary>
        public static MavTrimPlant CreatePlant()
        {
            return CreatePlant(null);
        }

        /// <summary>
        /// Builds the F-16 trim plant with an explicit steady propulsion function.
        ///
        /// The override exists so the powered-trim path can be exercised against an explicitly
        /// SYNTHETIC deck without that deck ever being wired into the F-16 runtime. Passing null
        /// keeps the shipped behaviour: sourced power dynamics, zero dimensional thrust.
        /// </summary>
        public static MavTrimPlant CreatePlant(MavTrimSteadyPropulsionFunction propulsionOverride)
        {
            MavAeroReferenceGeometry geometry = MavF16MorelliReference.CreateReferenceGeometry();
            float chordOverSpan = geometry.meanAerodynamicChordM / geometry.wingSpanM;

            MavTrimPlant plant = new MavTrimPlant();
            plant.plantId = "f16-morelli-clean-subsonic-v0.1";
            plant.referenceGeometry = geometry;
            plant.massKg = MavF16MassReference.MassKg;
            plant.alphaMinDeg = MavF16MorelliReference.AlphaMinDeg;
            plant.alphaMaxDeg = MavF16MorelliReference.AlphaMaxDeg;

            plant.controlSurfaceLimits = new MavControlSurfaceLimits
            {
                elevatorMinDeg = MavF16MorelliReference.ElevatorMinDeg,
                elevatorMaxDeg = MavF16MorelliReference.ElevatorMaxDeg,
                aileronMinDeg = MavF16MorelliReference.AileronMinDeg,
                aileronMaxDeg = MavF16MorelliReference.AileronMaxDeg,
                rudderMinDeg = MavF16MorelliReference.RudderMinDeg,
                rudderMaxDeg = MavF16MorelliReference.RudderMaxDeg,
                leadingEdgeFlapMinDeg = 0f,
                leadingEdgeFlapMaxDeg = 0f
            };

            plant.aeroFunction = delegate (float alphaRad, float betaRad, MavControlInput surfaces)
            {
                return MavF16MorelliPolynomial.Evaluate(
                    MavF16MorelliReference.ClampAlphaRad(alphaRad),
                    MavF16MorelliReference.ClampBetaRad(betaRad),
                    MavF16MorelliReference.ClampElevatorRad(surfaces.elevatorDeg * Mathf.Deg2Rad),
                    MavF16MorelliReference.ClampAileronRad(surfaces.aileronDeg * Mathf.Deg2Rad),
                    MavF16MorelliReference.ClampRudderRad(surfaces.rudderDeg * Mathf.Deg2Rad),
                    0f,
                    0f,
                    0f,
                    MavF16MassReference.XcgCbar,
                    MavF16MassReference.XcgReferenceCbar,
                    chordOverSpan
                );
            };

            plant.steadyPropulsionFunction = propulsionOverride ?? SteadyPropulsion;

            // Only an override could ever make this true, and only if it is backed by an
            // authoritative deck. The shipped path has no dimensional thrust data at all.
            plant.propulsionDataAuthoritative = false;

            return plant;
        }

        /// <summary>
        /// Builds a steady propulsion function from any thrust deck evaluation, applying the sourced
        /// Garza/Morelli throttle gearing to reach the settled power state first.
        ///
        /// Kept separate from <see cref="CreatePlant()"/> so that attaching a deck is always a
        /// deliberate act at the call site, never something the F-16 plant does on its own.
        /// </summary>
        public static MavTrimSteadyPropulsionFunction CreateSteadyPropulsionFromDeck(
            MavThrustDeckBase deck)
        {
            if (deck == null)
                return SteadyPropulsion;

            return delegate (MavFlightState state, MavAtmosphereSample atmosphere, float throttle01)
            {
                float steadyPowerPercent =
                    MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(throttle01);

                MavThrustDeckResult deckResult = deck.Evaluate(
                    MavThrustDeckQuery.Create(state.worldPositionM.y, state.mach, steadyPowerPercent));

                if (!deckResult.valid)
                    return MavF16EnginePowerModel.BuildZeroThrustLoads(steadyPowerPercent);

                return MavF16EnginePowerModel.BuildAxialThrustLoads(
                    deckResult.thrustN,
                    steadyPowerPercent,
                    deckResult.authority == MavThrustDataAuthority.Authoritative);
            };
        }

        /// <summary>
        /// Steady-state F-16 propulsion for trim.
        ///
        /// The sourced Garza/Morelli throttle gearing and power-state dynamics are honoured: at a
        /// settled condition actual engine power equals commanded power, so the steady power state
        /// is simply the throttle gearing with no spool transient. Dimensional thrust is then taken
        /// from <see cref="MavF16EnginePowerModel.BuildZeroThrustLoads"/>, which is exactly zero
        /// while the thrust deck is unavailable.
        ///
        /// This routine reads no component state and advances nothing, so a trim solve can never
        /// disturb a live engine model.
        /// </summary>
        public static MavPropulsiveLoads SteadyPropulsion(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01)
        {
            float steadyPowerPercent = MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(throttle01);
            return MavF16EnginePowerModel.BuildZeroThrustLoads(steadyPowerPercent);
        }

        /// <summary>Powered straight-and-level trim at a given altitude and true airspeed.</summary>
        public static MavTrimResult SolveStraightAndLevel(float altitudeM, float trueAirspeedMps)
        {
            return MavSteadyFlightTrimSolver.Solve(
                CreatePlant(),
                MavTrimCondition.StraightAndLevel(altitudeM, trueAirspeedMps)
            );
        }

        /// <summary>Powered steady flight along an imposed flight-path angle.</summary>
        public static MavTrimResult SolveSteadyFlightPath(
            float altitudeM,
            float trueAirspeedMps,
            float flightPathAngleDeg)
        {
            return MavSteadyFlightTrimSolver.Solve(
                CreatePlant(),
                MavTrimCondition.SteadyFlightPath(altitudeM, trueAirspeedMps, flightPathAngleDeg)
            );
        }

        /// <summary>
        /// Unpowered steady glide: thrust pinned at zero, flight-path angle solved for. This is the
        /// physically well-posed trim for the current F-16 configuration, and the one that actually
        /// converges.
        /// </summary>
        public static MavTrimResult SolveUnpoweredGlide(float altitudeM, float trueAirspeedMps)
        {
            return MavSteadyFlightTrimSolver.Solve(
                CreatePlant(),
                MavTrimCondition.UnpoweredGlide(altitudeM, trueAirspeedMps)
            );
        }

        /// <summary>
        /// Human-readable trim survey across a few reference points. Used by the editor menu; it
        /// computes and formats, and changes nothing.
        /// </summary>
        public static string BuildTrimSurvey()
        {
            float[] altitudes = { 0f, 3000f, 6000f };
            float[] airspeeds = { 130f, 160f, 200f };

            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("Maverick F-16 Trim Survey (v0.1)");
            report.AppendLine("================================");
            report.AppendLine();
            report.AppendLine(
                "Powered straight-and-level trim is expected to report ConvergedButThrustUnavailable: "
                + "the alpha/elevator solution is real, but no frozen thrust deck exists to supply the "
                + "required axial force. That is the honest result, not a solver failure.");

            for (int i = 0; i < altitudes.Length; i++)
            {
                for (int j = 0; j < airspeeds.Length; j++)
                {
                    report.AppendLine();
                    report.Append(SolveStraightAndLevel(altitudes[i], airspeeds[j]).report);
                    report.Append(SolveUnpoweredGlide(altitudes[i], airspeeds[j]).report);
                }
            }

            return report.ToString();
        }
    }
}
