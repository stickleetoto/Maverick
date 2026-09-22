using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// How an engine power state maps onto power lever angle in degrees.
    ///
    /// Undeclared by default and on purpose. NASA TP-1034 plots thrust against PLA; Maverick's
    /// shared engine runtime carries an actual power state in percent. Those are different
    /// quantities, and no document in the R5 source pack relates them - the F100's power lever is
    /// a physical lever with detents, and what the control does between them is exactly the fuel
    /// and nozzle scheduling the sources withhold.
    ///
    /// A linear map would be an interface convention, which is legitimate, but it must be
    /// DECLARED as one rather than buried in a lookup, or the invented part becomes invisible the
    /// moment a thrust number comes out the other side.
    /// </summary>
    public struct MavF100PowerLeverConvention
    {
        public bool declared;

        /// <summary>Power lever angle at 0 percent power state, degrees.</summary>
        public float idlePowerLeverAngleDeg;

        /// <summary>Power lever angle at 100 percent power state, degrees.</summary>
        public float maximumPowerLeverAngleDeg;

        /// <summary>What this convention is, and who is answerable for it.</summary>
        public string declarationNote;

        public static MavF100PowerLeverConvention Undeclared
        {
            get { return new MavF100PowerLeverConvention(); }
        }

        /// <summary>
        /// A linear power-state-to-PLA convention between the two power lever angles that
        /// TP-1034 printed p. 11 names: idle at 20 deg, maximum thrust at 130 deg.
        ///
        /// The ENDPOINTS are sourced. The straight line between them is not - it is a Maverick
        /// interface choice, and <paramref name="note"/> is where the caller says so. Nothing in
        /// the repository declares this by default.
        /// </summary>
        public static MavF100PowerLeverConvention LinearBetweenSourcedEndpoints(string note)
        {
            MavF100PowerLeverConvention c = new MavF100PowerLeverConvention();
            c.declared = true;
            c.idlePowerLeverAngleDeg = MavF100SourceData.IdlePowerLeverAngleDegLowAltitude;
            c.maximumPowerLeverAngleDeg = MavF100SourceData.MaximumAugmentationPowerLeverAngleDeg;
            c.declarationNote = note;
            return c;
        }

        public float PowerLeverAngleDeg(float actualPowerPercent)
        {
            float t = Mathf.Clamp01(actualPowerPercent * 0.01f);
            return idlePowerLeverAngleDeg
                + (maximumPowerLeverAngleDeg - idlePowerLeverAngleDeg) * t;
        }
    }

    /// <summary>
    /// The F100-PW-100 dimensional thrust deck: complete in structure, and unable to produce a
    /// force.
    ///
    /// This component exists to hold a specific gap open in a way that cannot be overlooked. The
    /// R5 source pack supports the full SHAPE of F100 net thrust over the F-15 envelope - see
    /// <see cref="MavF100NormalizedNetThrustModel"/>, which is real, tested, and traceable to a
    /// figure. What it does not supply is the single scalar that turns a fraction into newtons.
    ///
    /// So <see cref="Evaluate"/> returns <see cref="MavThrustDataAuthority.Unavailable"/> and zero
    /// thrust, every time, and names the blockers. It does not return a plausible number. It does
    /// not fall back to an approximate one. An aircraft with this deck attached does not move,
    /// which is the correct behaviour for an aircraft whose engine thrust nobody has published.
    ///
    /// TWO INDEPENDENT BLOCKERS
    /// ------------------------
    ///   1. <see cref="MavF100SourceData.DesignMaximumNetThrust"/> is undeclared. Without it the
    ///      normalized characteristic has no scale.
    ///   2. <see cref="powerLeverConvention"/> is undeclared. Without it the shared runtime's
    ///      power percent cannot be placed on the source's power lever axis.
    ///
    /// Closing either one alone changes nothing. Both are reported together rather than one at a
    /// time, so that closing the first does not produce the impression that thrust is now a single
    /// step away when it is two.
    ///
    /// WHY IT IS STILL WORTH HAVING
    /// ----------------------------
    /// Because when the design maximum is recovered, the deck becomes live by declaring two
    /// fields, and every operating point, the envelope enforcement and the regression fixtures are
    /// already in place and already checked. The work that is blocked is genuinely blocked; the
    /// work that is not has been done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF100ThrustDeck : MavThrustDeckBase
    {
        [Header("The missing scale")]
        [Tooltip("Design maximum net thrust in newtons - the normalizer of NASA TP-1034 figure 17. Leave OFF. No document in the R5 source pack prints it, and a general-specification F100 figure is a different engine build at a different rating and would silently rescale every operating point.")]
        public bool designMaximumNetThrustDeclared;

        [Tooltip("Only read when the flag above is set. Uninstalled net thrust at sea level, Mach 0, PLA 130 deg.")]
        public float designMaximumNetThrustN;

        [Tooltip("Where the value above came from. Required to be non-empty before the deck will use it.")]
        public string designMaximumNetThrustCitation = string.Empty;

        [Header("Power lever convention")]
        [Tooltip("How engine power percent maps to power lever angle in degrees. Undeclared by default: no R5 source relates the two.")]
        public MavF100PowerLeverConvention powerLeverConvention =
            MavF100PowerLeverConvention.Undeclared;

        [Header("Debug")]
        [TextArea(4, 12)]
        public string debugBlockers = "not evaluated";

        public override string DeckName
        {
            get
            {
                return "F100-PW-100 normalized net thrust (NASA TP-1034 fig. 17, "
                    + MavF100SourceData.NormalizedThrustEngineBuild
                    + "; dimensional scale UNAVAILABLE)";
            }
        }

        /// <summary>
        /// Deliberately never <see cref="MavThrustDataAuthority.Authoritative"/> today.
        ///
        /// Even with both blockers closed this deck would carry F100-PW-100(3) data on a
        /// F100-PW-100 target, which is compatible support and not exact-target. Promoting it
        /// would need demonstrated equivalence between the two engine builds, which is a source
        /// question, not a code question.
        /// </summary>
        public override MavThrustDataAuthority Authority
        {
            get { return MavThrustDataAuthority.Unavailable; }
        }

        /// <summary>
        /// Refuse rather than clamp. The seven documented operating points are scattered, not a
        /// grid, so "the nearest edge of the envelope" is not a meaningful place to clamp to - it
        /// would return another flight condition's thrust under this one's name.
        /// </summary>
        public override MavEnvelopeExcursionPolicy ExcursionPolicy
        {
            get { return MavEnvelopeExcursionPolicy.RejectUnsupportedState; }
        }

        /// <summary>
        /// The sourced part, reachable directly: normalized net thrust at a documented operating
        /// point, as a fraction of design maximum. No scale needed, so this one works.
        /// </summary>
        public MavF100NetThrustFractionResult EvaluateNormalized(
            float altitudeM, float mach, float powerLeverAngleDeg)
        {
            return MavF100NormalizedNetThrustModel.Evaluate(altitudeM, mach, powerLeverAngleDeg);
        }

        public override MavThrustDeckResult Evaluate(MavThrustDeckQuery query)
        {
            string blockers = DescribeBlockers();
            debugBlockers = blockers;

            if (blockers.Length > 0)
                return MavThrustDeckResult.Unavailable(blockers);

            // Unreachable while either blocker stands. Written out anyway, because the whole
            // point of the structure is that closing the blockers is a declaration and not a
            // rewrite - and unwritten code is not something anyone can review.
            float pla = powerLeverConvention.PowerLeverAngleDeg(query.actualPowerPercent);

            MavF100NetThrustFractionResult fraction =
                MavF100NormalizedNetThrustModel.Evaluate(query.altitudeM, query.mach, pla);

            if (!fraction.HasNumber)
                return MavThrustDeckResult.Unavailable(fraction.reason);

            MavThrustDeckResult result = new MavThrustDeckResult();
            result.valid = true;
            result.thrustN = fraction.netThrustFraction * designMaximumNetThrustN;
            result.insideEnvelope = fraction.support == MavF100ThrustSupport.Supported;

            // Compatible support is not Authoritative, and this enum has no third rung, so a
            // PW-100(3) number must not present itself as accepted reference data.
            result.authority = MavThrustDataAuthority.Unavailable;

            result.statusReason = "uninstalled NET thrust; " + fraction.reason
                + "; scale: " + designMaximumNetThrustCitation;
            return result;
        }

        /// <summary>
        /// Every reason this deck cannot produce a force, in one string. Empty when there are
        /// none.
        /// </summary>
        public string DescribeBlockers()
        {
            return DescribeBlockers(
                designMaximumNetThrustDeclared,
                designMaximumNetThrustN,
                designMaximumNetThrustCitation,
                powerLeverConvention);
        }

        /// <summary>
        /// Pure form of the blocker rule, so validation can enumerate it without a GameObject.
        /// The component method is a thin forward to this.
        /// </summary>
        public static string DescribeBlockers(
            bool designMaximumNetThrustDeclared,
            float designMaximumNetThrustN,
            string designMaximumNetThrustCitation,
            MavF100PowerLeverConvention powerLeverConvention)
        {
            StringBuilder sb = new StringBuilder(512);

            if (!designMaximumNetThrustDeclared)
            {
                sb.Append("SCALE: ")
                  .Append(MavF100SourceData.DesignMaximumNetThrust.citation)
                  .Append(' ');
            }
            else if (designMaximumNetThrustN <= 0f)
            {
                sb.Append("SCALE: design maximum net thrust is declared but non-positive. ");
            }
            else if (string.IsNullOrEmpty(designMaximumNetThrustCitation))
            {
                sb.Append("SCALE: design maximum net thrust is declared with no citation, "
                    + "which is how an invented number enters a sourced deck. ");
            }

            if (!powerLeverConvention.declared)
            {
                sb.Append("POWER LEVER: no declared map from engine power percent to power lever "
                    + "angle. TP-1034 plots thrust against PLA and names idle at 20 deg and "
                    + "maximum at 130 deg, but no R5 source relates PLA to a power state, so the "
                    + "curve between them is an interface convention somebody must own. ");
            }
            else if (string.IsNullOrEmpty(powerLeverConvention.declarationNote))
            {
                sb.Append("POWER LEVER: convention declared without a note saying whose choice "
                    + "it is. ");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
