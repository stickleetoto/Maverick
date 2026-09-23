using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// How an F100 quantity relates to the exact target, NASA F-15B 836.
    ///
    /// The R5 source pack deliberately spans three authority levels and they must not be
    /// collapsed. The engine IDENTITY of NASA 836 is exact-target; that does not make every
    /// F100-PW-100-family number exact-target performance data for it.
    ///
    /// These names are the R5 brief's vocabulary. They map onto the repository's single
    /// provenance enum, <see cref="MavEngineDataProvenance"/>, rather than introducing a second
    /// competing scale - one vocabulary is what makes "the worst field wins" checkable.
    /// </summary>
    public enum MavF100SourceClass
    {
        /// <summary>No accepted public source. Not usable as engine data.</summary>
        Unavailable = 0,

        /// <summary>
        /// An oracle, not a value source: a result that can CHECK an implementation but must
        /// never be read back into one. TP-1782's flight comparison is the case in point.
        /// </summary>
        CrossValidationOnly = 1,

        /// <summary>
        /// A real F100 source whose engine build is not proven equivalent to NASA 836's:
        /// the PW-100(3) model, or the prototype series 2 7/8 test engines.
        /// </summary>
        CompatibleSupport = 2,

        /// <summary>Accepted for the F100-PW-100 as installed on NASA F-15B 836 specifically.</summary>
        AuthoritativeExactTarget = 3
    }

    /// <summary>
    /// What a thrust number actually MEANS. Mixing these silently is the classic way to get a
    /// number that is wrong by tens of kilonewtons while looking entirely plausible.
    ///
    /// TP-1373 and TP-1782 are about GROSS thrust throughout. TM X-3261 and TP-1034 compute both,
    /// and print the transformation between them. Nothing in Maverick may convert between the two
    /// except through <see cref="MavF100ThrustSemantics"/>, which carries the sourced equation.
    /// </summary>
    public enum MavF100ThrustQuantity
    {
        Unspecified = 0,

        /// <summary>Nozzle gross thrust: the exhaust stream only, with no inlet momentum debit.</summary>
        GrossThrust = 1,

        /// <summary>Inlet momentum drag, w2 * V0. Positive magnitude; subtracted from gross.</summary>
        RamDrag = 2,

        /// <summary>Gross thrust minus ram drag, with no airframe installation effects applied.</summary>
        UninstalledNetThrust = 3,

        /// <summary>
        /// Net thrust after airframe installation effects (inlet spillage, bleed, nozzle/boattail
        /// interference). NOTHING in the R5 pack closes these for NASA 836.
        /// </summary>
        InstalledNetThrust = 4
    }

    /// <summary>
    /// One documented engine operating point from NASA TP-1034 figure 17: a flight condition and
    /// the net-thrust characteristic measured across power lever angle at that condition.
    ///
    /// The ordinate is a FRACTION of design maximum net thrust, not a force. The figure's own axis
    /// says so, and TP-1034 never prints the value of that normalizer anywhere - see
    /// <see cref="MavF100SourceData.DesignMaximumNetThrust"/>.
    /// </summary>
    public struct MavF100OperatingPointCurve
    {
        /// <summary>Panel letter within figure 17, so any number here can be traced to a page.</summary>
        public string panel;

        public float altitudeM;
        public float mach;

        /// <summary>Power lever angle in degrees, strictly ascending.</summary>
        public float[] powerLeverAngleDeg;

        /// <summary>Net thrust as a fraction of design maximum net thrust, one per PLA breakpoint.</summary>
        public float[] netThrustFraction;

        public int Count
        {
            get { return powerLeverAngleDeg == null ? 0 : powerLeverAngleDeg.Length; }
        }
    }

    /// <summary>
    /// Why a quantity is missing. The distinction matters because it changes what to do next.
    /// </summary>
    public enum MavF100BlockerKind
    {
        /// <summary>Nothing is missing.</summary>
        None = 0,

        /// <summary>
        /// Not found yet in the sources held. Searching further may close it.
        /// </summary>
        NotYetFound = 1,

        /// <summary>
        /// The primary source exists and is NOT public, so the public report chain cannot close
        /// it however far it is followed. Reported for the F100 thrust and fuel-consumption
        /// specification - see <see cref="MavF100SourceData.ClassifiedThrustSpecification"/>.
        ///
        /// Recording this separately is what stops the same search being re-run. "Keep looking"
        /// and "looking will not help" are different instructions to whoever picks this up next.
        /// </summary>
        PublicSourceBlocked = 2
    }

    /// <summary>
    /// A scalar that the sources define but never print. Carries its own declared flag because
    /// zero is a legal force and "nobody published it" is not zero.
    /// </summary>
    public struct MavF100DeclaredScalar
    {
        public bool declared;
        public float value;
        public MavF100SourceClass sourceClass;
        public MavF100BlockerKind blocker;
        public string citation;

        public static MavF100DeclaredScalar Undeclared(string whyNot)
        {
            return Undeclared(MavF100BlockerKind.NotYetFound, whyNot);
        }

        public static MavF100DeclaredScalar Undeclared(
            MavF100BlockerKind blocker, string whyNot)
        {
            MavF100DeclaredScalar s = new MavF100DeclaredScalar();
            s.declared = false;
            s.value = 0f;
            s.sourceClass = MavF100SourceClass.Unavailable;
            s.blocker = blocker;
            s.citation = whyNot;
            return s;
        }
    }

    /// <summary>
    /// EXTRACTED SOURCE DATA for the Pratt &amp; Whitney F100-PW-100 family, from the four NASA
    /// documents in the R5 source pack. Data only: no evaluation, no interpolation, no policy.
    /// Everything that DOES something with these numbers lives in
    /// <see cref="MavF100NormalizedNetThrustModel"/> and its siblings, which is the separation the
    /// R5 brief requires for digitized figure data.
    ///
    /// THE ONE FACT THAT SHAPES EVERYTHING ELSE
    /// ----------------------------------------
    /// Across all four documents there is no absolute thrust value FOR A FLIGHT CONDITION AND
    /// POWER SETTING. Every thrust RESULT in the pack is either a fraction of an unpublished
    /// normalizer or a percentage difference against a proprietary manufacturer deck:
    ///
    /// That wording is deliberately narrower than it first was. An earlier revision of this file
    /// said there was no absolute thrust value anywhere in the pack, and that was wrong: TP-1034
    /// appendix C prints a dimensional thrust SCALE, 30 000 lbf, as the full-scale factor of the
    /// simulation's net-thrust channel. See <see cref="SimulationNetThrustChannelFullScaleLbf"/>.
    /// It is not the figure 17 normalizer - it is larger, and provably so - but it is a real
    /// printed dimensional thrust fact and the audit now records it as one.
    ///
    ///   - TP-1034 figure 17 plots "fraction of design maximum net thrust" and never states the
    ///     design maximum.
    ///   - TP-1373 plots percent error against P&amp;W CCD 1088-2.0, a deck we do not have, and
    ///     normalizes its axes by 111.2 kN and 98.4 kg/sec.
    ///   - TM X-3261 prints complete thrust EQUATIONS, but they are driven by component maps that
    ///     the report supplies as graphs and that its FORTRAN reads from data cards.
    ///   - TP-1782 publishes only percentage agreement between two calculation methods, and
    ///     withholds the SGTM coefficients entirely.
    ///
    /// So the SHAPE of F100 net thrust over the F-15 envelope is recoverable and is recorded here.
    /// The SCALE is not, and is recorded as undeclared. See
    /// Docs/Reference/F15_R5_F100_SOURCE_AUDIT_V0.1.md.
    /// </summary>
    public static class MavF100SourceData
    {
        // ------------------------------------------------------------------ identity

        public const string EngineIdentity = "Pratt & Whitney F100-PW-100";

        public const string TargetAircraftConfiguration =
            "NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100";

        /// <summary>
        /// The engine build that figure 17 actually describes. NOT the target build.
        ///
        /// TP-1034 is explicitly the F100-PW-100(3): "It features improved fan performance over
        /// the earlier F100-PW-100(1) version" (printed p. 2). Its component maps, augmentor
        /// efficiency, duct pressure drop and nozzle coefficients were all regenerated for that
        /// build. Reading its thrust characteristic as NASA 836's would be exactly the silent
        /// substitution the R5 brief forbids.
        /// </summary>
        public const string NormalizedThrustEngineBuild = "F100-PW-100(3)";

        /// <summary>
        /// The engine build that TP-1373 and TP-1782 describe. Also NOT the target build.
        ///
        /// TP-1373 printed p. 4: prototype series 2 7/8 - series 2 core, series 3 improved
        /// stability fan with recessed splitter, control schedule differences from BOTH series 2
        /// and series 3, and series 2 actuated divergent nozzles where series 3 engines have
        /// free-floating ones. TP-1782 printed p. 4 adds the plain statement that "the results are
        /// not totally representative of production F100 engines".
        /// </summary>
        public const string CalibrationEngineBuild =
            "F100 prototype series 2 7/8 (engines P680059, P680063)";

        // ------------------------------------------------------- the missing normalizer

        /// <summary>
        /// Design maximum net thrust: the force that every number in <see cref="NetThrustCurves"/>
        /// is a fraction OF. It is the single scalar standing between this deck and dimensional
        /// thrust, and no document in the pack prints it.
        ///
        /// Its definition is nevertheless pinned precisely by the data: figure 17(a) reaches
        /// exactly 1.0 at sea level, Mach 0, PLA 130 deg (maximum augmentation), so the normalizer
        /// is uninstalled net thrust at the sea-level static maximum-augmentation design point.
        ///
        /// DO NOT fill this in from a general-specification F100 figure. Such a number would be
        /// for a different engine build, at a different rating, on a different installation, and
        /// it would silently rescale every operating point in this file.
        /// </summary>
        public static MavF100DeclaredScalar DesignMaximumNetThrust
        {
            get
            {
                return MavF100DeclaredScalar.Undeclared(
                    MavF100BlockerKind.PublicSourceBlocked,
                    "PUBLIC-SOURCE BLOCKED: design maximum net thrust is the normalizer of NASA TP-1034 "
                    + "figure 17 and is not printed in TP-1034, TM X-3261, TP-1373 or TP-1782. "
                    + "Definition is fixed: uninstalled net thrust at sea level, Mach 0, "
                    + "PLA 130 deg. Two candidate values have been investigated and both "
                    + "rejected: TP-1373's 111.2 kN axis scale (wrong thrust quantity - see "
                    + "AxisNormalizerIsNotTheDesignMaximum) and TP-1034's 30 000 lbf simulation "
                    + "channel scale (a SCALED FRACTION full scale, provably larger than the "
                    + "normalizer - see Y12IsNotTheFigure17Normalizer). The second does yield a "
                    + "derived upper bound of about 22 400 lbf. The F100 thrust and "
                    + "fuel-consumption requirements are reported to live in P&W specification "
                    + "CP2903B, which is CLASSIFIED, so the public NASA report chain is not "
                    + "expected to close this. Do not reconstruct CP2903B values. Only another "
                    + "UNCLASSIFIED primary source that explicitly publishes the figure can "
                    + "close it.");
            }
        }

        // ------------------------------------------------- NASA TP-1056 (reported)

        /// <summary>
        /// NASA TP-1056, the F100 multivariable control synthesis programme evaluation, is a
        /// follow-on to TP-1034 on the same real-time simulation. The report is NOT held in this
        /// repository, so nothing below was read here.
        ///
        /// Three findings were reported from it. Two of them do not need TP-1056 at all, because
        /// TP-1034 states the same things in text that IS held, and those are cited instead - a
        /// claim corroborated by a source in hand is worth more than the same claim resting on a
        /// report nobody here can open.
        ///
        ///   1. The real-time F100-PW-100(3) simulation is patterned after CCD 1103-1.0.
        ///      CORROBORATED IN HAND: TP-1034 printed p. 3 - "modifications were made to elements
        ///      of that model to match the performance of the F100-PW-100(3) engine as predicted
        ///      by the corresponding digital simulation (CCD 1103-1.0)" - and again on printed
        ///      p. 6 and in the summary of results.
        ///
        ///   2. Engine net thrust is computed in the DIGITAL portion of the hybrid simulation.
        ///      CORROBORATED IN HAND: TP-1034 printed p. 4 lists, among the digital-portion
        ///      modifications, "auxiliary calculations such as the calculation of engine thrust
        ///      and surge margins". The appendix C listing shows it directly - Y12 is computed in
        ///      FORTRAN on printed p. 26. See <see cref="Y12IsNotTheFigure17Normalizer"/>, which
        ///      this strengthens: the scaled-fraction ceiling is imposed where the value is
        ///      COMPUTED, not merely where it is output to the analog machine, so the derived
        ///      upper bound does not depend on how the figure was plotted.
        ///
        ///   3. F100 thrust and fuel-consumption requirements live in P&amp;W specification CP2903B,
        ///      which is classified; the public programme used CCD 1103-1.0 predicted performance
        ///      instead. NOT CORROBORATED: "CP2903B" and "2903" appear nowhere in the four
        ///      documents held. This one rests on the report of TP-1056 alone.
        /// </summary>
        public const string Tp1056ReportedProvenance =
            "NASA TP-1056 (F100 multivariable control synthesis programme) is NOT held in this "
            + "repository. Findings 1 and 2 as reported are corroborated independently by "
            + "TP-1034 printed pp. 3, 4 and 26, which are held, and are cited to TP-1034. "
            + "Finding 3 (CP2903B classified) is unverified here and rests on the report alone.";

        /// <summary>
        /// The reported reason the absolute normalizer is not expected to be publicly
        /// recoverable: the F100 thrust and fuel-consumption requirements are said to be in
        /// P&amp;W specification CP2903B, which is classified.
        ///
        /// Unverified here. It is nonetheless acted on, because acting on it costs nothing and
        /// only changes where NOT to look: it downgrades the public NASA report chain as a search
        /// target, which is why <see cref="DesignMaximumNetThrust"/> now carries
        /// <see cref="MavF100BlockerKind.PublicSourceBlocked"/> rather than NotYetFound.
        ///
        /// NOTHING in this repository may attempt to reconstruct, estimate or infer CP2903B
        /// values. A classified specification is not a gap to be filled by inference.
        /// </summary>
        public const string ClassifiedThrustSpecification =
            "REPORTED (unverified here): F100 thrust and fuel-consumption requirements are "
            + "contained in P&W specification CP2903B, which is CLASSIFIED. The public "
            + "multivariable-control programme used CCD 1103-1.0 predicted performance instead. "
            + "Consequence: the absolute figure 17 normalizer is PUBLIC-SOURCE BLOCKED, not "
            + "merely not-yet-found. Do not reconstruct CP2903B values.";

        // ------------------------------------ TP-1034 appendix C: the Y12 thrust channel

        /// <summary>
        /// Full-scale factor of the net-thrust channel in the TP-1034 hybrid simulation, in lbf.
        ///
        /// NASA TP-1034 appendix C, FORTRAN listing, printed p. 28, verified from the page image
        /// at 6x against neighbouring digits on the same page:
        ///
        ///     FN=Y12
        ///     FN=FN*30000.
        ///     FNSI=FN*4.4482E-3
        ///
        /// with the symbol list defining FN as uninstalled net thrust in lbf and FNSI the same in
        /// kN. NASA TM X-3261's appendix C carries the identical block, so the two reports agree.
        ///
        /// THIS IS A MACHINE SCALE FACTOR, NOT A DESIGN VALUE. See
        /// <see cref="Y12IsNotTheFigure17Normalizer"/> before using it for anything.
        /// </summary>
        public const float SimulationNetThrustChannelFullScaleLbf = 30000f;

        /// <summary>Exact pound-force to newton conversion, for the SI form of the channel scale.</summary>
        public const double PoundForceToNewton = 4.4482216152605;

        /// <summary>
        /// The same full scale in newtons, 133.45 kN. The source's own constant, 4.4482E-3,
        /// converts to kN and agrees to five figures.
        /// </summary>
        public const float SimulationNetThrustChannelFullScaleN =
            (float)(SimulationNetThrustChannelFullScaleLbf * PoundForceToNewton);

        public const string Y12ChannelCitation =
            "NASA TP-1034 appendix C, printed p. 28 (PDF p. 32): FN=Y12; FN=FN*30000.; "
            + "FNSI=FN*4.4482E-3. Y12 is declared a DAC SCALED FRACTION on printed p. 25 and is "
            + "computed on printed p. 26 as the net thrust expression, gross terms less FRD (ram "
            + "drag) less the pressure-area term, rescaled by /.349335.";

        /// <summary>
        /// Why the 30 000 lbf channel scale is NOT the design maximum net thrust that figure 17
        /// is normalized by - which is the question that matters, since the two would otherwise
        /// look interchangeable.
        ///
        /// 1. **Y12 is a SCALED FRACTION.** TP-1034 appendix C printed p. 25 declares it among
        ///    the DAC variables as such. That is the EAI hybrid fixed-point fractional type: the
        ///    value lives in [-1, 1) and cannot represent anything outside it. So the largest net
        ///    thrust this simulation can even express is 30 000 lbf, at Y12 = 1.
        ///
        /// 2. **Figure 17 goes to 1.338.** Panel (e), 6.096 km at Mach 1.8, maximum augmentation.
        ///    If figure 17's normalizer were the channel's full scale, that point would require
        ///    Y12 = 1.338, which the type cannot hold. The plotted markers are this simulation's
        ///    own output and the steady-state printout derives from the same channel, so every
        ///    hybrid thrust value in the report passed through Y12. Therefore the figure's
        ///    normalizer is a SMALLER number than the channel scale, and the two are not the same
        ///    quantity.
        ///
        /// 3. **Every scale factor in that block is a round headroom value.** The clearest case is
        ///    on the same page: PLA=PLA*150., where the documented maximum power lever angle is
        ///    130 degrees (printed p. 11). Also XNL and XNH at 15 000 rpm against corrected fan
        ///    speeds that TP-1373 shows peaking near 11 000, T4 at 4 000, T41 at 3 000, WF7 at 20,
        ///    WA2 at 450. These are analog scaling constants chosen round and safely above the
        ///    expected maximum - which is what makes 30 000 an unsurprising choice and a
        ///    misleading one to read as a rating.
        ///
        /// So this constant is recorded as a simulation-output fact and is deliberately NOT
        /// applied to <see cref="NetThrustCurves"/>.
        /// </summary>
        public const string Y12IsNotTheFigure17Normalizer =
            "Y12 is declared SCALED FRACTION (TP-1034 appendix C, printed p. 25), so it lives in "
            + "[-1, 1) and 30 000 lbf is the largest net thrust the simulation can express. "
            + "Figure 17 panel (e) plots 1.338 of its own normalizer, which the channel could not "
            + "carry if that normalizer were the channel scale. The figure's design maximum is "
            + "therefore strictly smaller than 30 000 lbf, and the two are different quantities.";

        /// <summary>
        /// An upper bound on the design maximum net thrust, DERIVED rather than printed.
        ///
        /// If the largest value figure 17 plots is 1.338 of the design maximum D, and that thrust
        /// had to pass through a channel whose full scale is 30 000 lbf and whose type cannot
        /// exceed 1.0, then:
        ///
        ///     1.338 * D &lt;= 30 000 lbf   =&gt;   D &lt;= 22 422 lbf  (99.7 kN)
        ///
        /// This is the first quantitative constraint on the missing scalar, and it is worth having:
        /// it rules out 25 000 lbf / 111.2 kN independently of every other argument against that
        /// number.
        ///
        /// It is a BOUND, not a value, and it rests on three things being true together: the
        /// digitized 1.338 (measured here, +/-0.01), the SCALED FRACTION range (printed), and the
        /// plotted hybrid markers having come through Y12 (strongly implied, since the report's
        /// own steady-state thrust printout is FN = Y12 * 30000). It is graded
        /// <see cref="MavF100SourceClass.CrossValidationOnly"/> because it is an inference from
        /// printed facts rather than a printed fact, and nothing computes with it.
        /// </summary>
        public static float DesignMaximumNetThrustUpperBoundLbf
        {
            get
            {
                float peak = 0f;
                MavF100OperatingPointCurve[] curves = NetThrustCurves;
                for (int i = 0; i < curves.Length; i++)
                {
                    for (int k = 0; k < curves[i].Count; k++)
                    {
                        if (curves[i].netThrustFraction[k] > peak)
                            peak = curves[i].netThrustFraction[k];
                    }
                }

                return peak > 0f
                    ? SimulationNetThrustChannelFullScaleLbf / peak
                    : float.NaN;
            }
        }

        // --------------------------------------------------- TP-1034 figure 17 curves

        public const string NetThrustCurveCitation =
            "NASA TP-1034, figure 17, printed pp. 64-65 (PDF pp. 68-69): open-loop hybrid "
            + "steady-state net thrust at standard-day conditions, F100-PW-100(3). "
            + "Ordinate: fraction of design maximum net thrust. Abscissa: corresponding power "
            + "lever angle, deg. DIGITIZED from the page image - not printed table values.";

        /// <summary>
        /// Digitization tolerance, in fraction-of-design-maximum units.
        ///
        /// Not a source number: the accuracy of reading the plotted markers. It is bounded by an
        /// independent check the figure supplies for free - panel (a) at PLA 130 must be exactly
        /// 1.0 by the definition of the normalizer, and this digitization reads 1.004 there.
        /// </summary>
        public const float NetThrustDigitizationTolerance = 0.01f;

        /// <summary>
        /// The seven documented operating points of TP-1034 figure 17.
        ///
        /// Values are the HYBRID simulation markers (open circles), which are discrete and
        /// therefore locatable exactly. The solid baseline-digital curve was NOT digitized;
        /// TP-1034 printed p. 13 records that the two differ by up to 9 percent of design maximum
        /// thrust at the supersonic augmented conditions, which is the honest error bar on
        /// treating these markers as the manufacturer's predicted performance.
        ///
        /// A fresh array each call: this is source data, and a shared mutable array would let one
        /// caller edit the source out from under every other.
        /// </summary>
        public static MavF100OperatingPointCurve[] NetThrustCurves
        {
            get
            {
                return new MavF100OperatingPointCurve[]
                {
                    Curve("17(a)", 0f, 0.00f,
                        new float[] { 20.0f, 24.4f, 30.2f, 39.7f, 50.1f, 59.8f, 70.2f, 83.4f, 100.0f, 110.1f, 120.0f, 129.8f },
                        new float[] { 0.050f, 0.095f, 0.133f, 0.233f, 0.314f, 0.395f, 0.470f, 0.628f, 0.705f, 0.840f, 0.943f, 1.004f }),

                    Curve("17(b)", 3048f, 0.90f,
                        new float[] { 20.0f, 23.6f, 29.6f, 39.4f, 49.6f, 59.6f, 69.5f, 82.9f, 99.9f, 109.9f, 119.8f, 129.8f },
                        new float[] { -0.018f, 0.033f, 0.093f, 0.142f, 0.193f, 0.251f, 0.301f, 0.458f, 0.560f, 0.742f, 0.884f, 0.964f }),

                    Curve("17(c)", 9144f, 0.90f,
                        new float[] { 20.0f, 23.9f, 30.1f, 39.9f, 50.2f, 60.0f, 70.2f, 83.5f, 100.3f, 110.4f, 120.6f, 130.5f },
                        new float[] { 0.017f, 0.017f, 0.046f, 0.094f, 0.136f, 0.177f, 0.206f, 0.243f, 0.299f, 0.397f, 0.477f, 0.519f }),

                    Curve("17(d)", 13720f, 0.90f,
                        new float[] { 20.0f, 23.7f, 29.8f, 39.8f, 49.8f, 59.6f, 69.6f, 82.6f, 99.6f, 109.3f, 119.3f, 129.5f },
                        new float[] { 0.049f, 0.049f, 0.049f, 0.048f, 0.078f, 0.100f, 0.116f, 0.128f, 0.159f, 0.211f, 0.254f, 0.274f }),

                    Curve("17(e)", 6096f, 1.80f,
                        new float[] { 83.0f, 100.0f, 110.1f, 120.1f, 130.0f },
                        new float[] { 0.438f, 0.597f, 0.916f, 1.175f, 1.338f }),

                    Curve("17(f)", 12190f, 2.20f,
                        new float[] { 82.9f, 100.0f, 110.0f, 120.0f, 129.9f },
                        new float[] { 0.263f, 0.429f, 0.606f, 0.797f, 0.898f }),

                    Curve("17(g)", 17830f, 2.15f,
                        new float[] { 82.9f, 99.9f, 110.0f, 120.1f, 130.1f },
                        new float[] { 0.106f, 0.154f, 0.245f, 0.317f, 0.357f })
                };
            }
        }

        private static MavF100OperatingPointCurve Curve(
            string panel, float altitudeM, float mach, float[] pla, float[] fraction)
        {
            MavF100OperatingPointCurve c = new MavF100OperatingPointCurve();
            c.panel = panel;
            c.altitudeM = altitudeM;
            c.mach = mach;
            c.powerLeverAngleDeg = pla;
            c.netThrustFraction = fraction;
            return c;
        }

        // ------------------------------------------------------------ power lever angle

        /// <summary>
        /// Minimum power lever angle at the three subsonic conditions below 10 km, TP-1034
        /// printed p. 11: "the idle setting was 20 deg".
        /// </summary>
        public const float IdlePowerLeverAngleDegLowAltitude = 20f;

        /// <summary>
        /// Minimum power lever angle at 13.72 km / Mach 0.9, TP-1034 printed p. 11:
        /// "For the 13.72 km/M n = 0.9 condition, the idle setting was 30 deg."
        ///
        /// Recorded because it disagrees with the abscissa of figure 17(d), whose leftmost markers
        /// digitize at about 20, 24 and 30 deg. The figure's own axis is labelled "CORRESPONDING
        /// power lever angle", i.e. the PLA matching the set of control variables rather than a
        /// commanded detent, which is the most likely explanation. Not resolved here, and nothing
        /// depends on resolving it.
        /// </summary>
        public const float IdlePowerLeverAngleDegHighAltitude = 30f;

        /// <summary>
        /// Maximum-augmentation power lever angle, TP-1034 printed p. 11: "the maximum thrust
        /// setting of 130 deg".
        /// </summary>
        public const float MaximumAugmentationPowerLeverAngleDeg = 130f;

        /// <summary>
        /// Lowest power lever angle permitted at the supersonic conditions, TP-1034 printed p. 11:
        /// "For the three supersonic conditions, power settings lower than 83 deg were not
        /// permitted." Consistent with the EEC minimum-airflow schedule of TP-1373 printed p. 5.
        /// </summary>
        public const float SupersonicMinimumPowerLeverAngleDeg = 83f;

        /// <summary>
        /// Military power lever angle per NASA TM X-3261 printed p. 6: "the sea-level, static,
        /// military-power (PLA = 73 deg) condition".
        ///
        /// KEPT SEPARATE from TP-1034's 83 deg on purpose. TP-1034 treats 83 deg as the top of
        /// non-augmented operation, and figure 17(a) shows its augmentation plateau there. The two
        /// reports evidently do not share a power lever convention. Averaging them, or picking
        /// one, would be inventing a third convention that neither document supports.
        /// </summary>
        public const float MilitaryPowerLeverAngleDegTmX3261 = 73f;

        // --------------------------------------------------------- inlet recovery (B1-B5)

        /// <summary>
        /// Supersonic total-pressure recovery coefficient, NASA TM X-3261 equation (B3):
        /// eta = 1.0 - 0.075 * (M0 - 1.0)^1.35 for M0 &gt; 1.0, and 1.0 otherwise.
        ///
        /// Confirmed character by character against the Appendix C FORTRAN listing, which prints
        /// ETA0 = 1.0 - .075*(M0-1.)**1.35, so the garbled equation glyphs in the scanned page are
        /// not load-bearing.
        ///
        /// This is TM X-3261's phrase "a steady-state representation of a TYPICAL inlet recovery",
        /// which is the standard reference schedule - it is NOT a measurement of the F-15 inlet,
        /// still less of NASA 836's, and the F-15 has a variable-geometry inlet this ignores
        /// entirely.
        /// </summary>
        public const float SupersonicRecoveryCoefficient = 0.075f;

        public const float SupersonicRecoveryExponent = 1.35f;

        public const string InletRecoveryCitation =
            "NASA TM X-3261, equations (B1)-(B5), printed p. 13, cross-checked against the "
            + "appendix C FORTRAN listing. Described in the source as a TYPICAL inlet recovery, "
            + "not as F-15 or NASA 836 inlet data.";

        // ------------------------------------------------------------- ram drag (B52/B56)

        /// <summary>
        /// Ram-drag coefficient of NASA TM X-3261 equation (B52) and NASA TP-1034 equation (B56):
        ///
        ///     F_net = F_gross - 20.041 * w2 * M0 * sqrt(T0)
        ///
        /// with thrust in newtons, airflow in kg/sec and temperature in kelvin, per the symbol
        /// lists of both reports.
        ///
        /// The constant is not empirical: 20.041 is sqrt(gamma * R) for air, sqrt(1.4 * 287.05)
        /// = 20.047, so the term is just w2 * V0 written with the local speed of sound expanded.
        /// That identity is why this equation can be implemented with confidence while the gross
        /// thrust equation beside it cannot - that one needs the component maps.
        /// </summary>
        public const float RamDragCoefficient = 20.041f;

        public const string RamDragCitation =
            "NASA TM X-3261 eq. (B52), printed p. 22; NASA TP-1034 eq. (B56), printed p. 21. "
            + "Coefficient verified as sqrt(gamma*R) for air.";

        // --------------------------------------------------------- TP-1373 normalizers

        /// <summary>
        /// Design corrected airflow, 98.4 kg/sec. TP-1373 plots corrected airflow as "percent of
        /// 98.4 kg/sec" (figures 6(a), 6(c)) and refers to that axis as percent of DESIGN
        /// corrected airflow (printed p. 12), so the number is the engine's design value and not
        /// merely a drawing convenience.
        ///
        /// Prototype series 2 7/8 engines. CompatibleSupport, not exact-target.
        /// </summary>
        public const float DesignCorrectedAirflowKgPerSec = 98.4f;

        /// <summary>
        /// The gross-thrust axis normalizer of TP-1373 figures 6(b) and 6(d), 111.2 kN.
        ///
        /// **This must never be used as the design maximum net thrust of figure 17.** It is a
        /// plot scale. See <see cref="AxisNormalizerIsNotTheDesignMaximum"/> for the full reason;
        /// the short version is that it is the wrong quantity twice over - gross where figure 17
        /// is net, and nominal where figure 17's normalizer is a design point.
        /// </summary>
        public const float GrossThrustAxisNormalizerN = 111200f;

        /// <summary>
        /// Why 111.2 kN cannot stand in for the missing design maximum net thrust.
        ///
        /// Recorded at length because it is the single most inviting wrong turn available here:
        /// the pack contains exactly one round thrust number, and the deck is short by exactly one
        /// scalar. They are not the same scalar.
        ///
        /// 1. **Gross, not net.** TP-1373 figure 6(b) was re-read from the page image: its
        ///    abscissa is "F_g, percent of 111.2 kN", with F_g defined in the symbol list as
        ///    "gross thrust, kN". Figure 17's ordinate is NET thrust. Between them sits the ram
        ///    drag, which at the conditions TP-1373 actually tested is a large fraction of the
        ///    gross thrust, not a correction.
        ///
        /// 2. **Nominal, not a design point.** The user reports that NASA TP-1228 states 111 kN
        ///    (25 000 lbf) to be an arbitrarily chosen nominal CORRECTED gross-thrust
        ///    normalization value. TP-1228 is not in this repository and that statement has not
        ///    been verified here - it is carried on the user's authority, in the same way the
        ///    R3 roll-damper figure is. It is consistent with what TP-1373's own page shows: the
        ///    text never calls 111.2 kN a design, maximum or rated value, and 25 000 lbf is what
        ///    a round plot scale looks like.
        ///
        /// 3. **Possibly a third quantity again.** TP-1373's axis is plain F_g; the reported
        ///    TP-1228 usage is CORRECTED gross thrust, F_g/delta. If both are right then the same
        ///    round number is serving as a scale for two different quantities in two different
        ///    reports, which is exactly what a nominal normalizer does and exactly what a physical
        ///    rating does not.
        ///
        /// Using it anyway would rescale all 63 digitized points by a number chosen for the
        /// convenience of an axis, and every result downstream would look sourced.
        /// </summary>
        public const string AxisNormalizerIsNotTheDesignMaximum =
            "111.2 kN is TP-1373's plot-axis scale for GROSS thrust (figure 6(b), verified from "
            + "the page image: 'F_g, percent of 111.2 kN'), and is reported by the user to be "
            + "described in TP-1228 as an arbitrarily chosen nominal CORRECTED gross-thrust "
            + "normalization - a statement not verified in this repository, since TP-1228 is not "
            + "held here. Figure 17's normalizer is design maximum NET thrust at a specific "
            + "design point. Wrong thrust quantity, and a nominal scale rather than a design "
            + "value. Equivalence would have to be proven from a source, not assumed.";

        // ------------------------------------------------- TP-1373 nozzle mode schedule

        /// <summary>
        /// Free-stream Mach number selecting the nozzle area-ratio schedule, TP-1373 printed p. 7:
        /// "the low mode schedule is used for M0 &lt; 1.1, and the high mode schedule is used for
        /// M0 &gt; 1.1". TP-1782 printed p. 11 independently confirms that the divergent area ratio
        /// is scheduled on nozzle throat area and free-stream Mach number.
        /// </summary>
        public const float NozzleAreaRatioModeSwitchMach = 1.1f;

        // --------------------------------------------- TP-1373 EEC minimum-power schedule

        /// <summary>
        /// Mach number below which the EEC permits idle power, TP-1373 printed p. 5: "Below Mach
        /// 0.90 the EEC allows engine operating power lever angle to go idle."
        /// </summary>
        public const float EecIdlePermittedBelowMach = 0.90f;

        /// <summary>
        /// Mach number at which the EEC minimum reaches intermediate power, TP-1373 printed p. 5:
        /// "The minimum allowable value increases linearly with Mach number to intermediate power
        /// at a Mach number of 1.4. It remains constant at this level for higher Mach numbers."
        ///
        /// The whole schedule is printed prose - both endpoints, the interpolation law between
        /// them, and the behaviour above. That is rare in this pack and is why it can be
        /// implemented as written.
        /// </summary>
        public const float EecIntermediateFloorMach = 1.4f;

        public const string EecMinimumPowerCitation =
            "NASA TP-1373, printed p. 5. Prototype series 2 7/8 control schedules, which the same "
            + "page notes differ from both series 2 and series 3 production engines.";

        // ------------------------------------------------------ TP-1782 cross-validation

        public const float FlightValidationMinMach = 0.6f;
        public const float FlightValidationMaxMach = 1.5f;
        public const float FlightValidationMinAltitudeM = 6000f;
        public const float FlightValidationMaxAltitudeM = 13700f;

        /// <summary>
        /// Agreement between the simplified gross thrust model and the gas generator method in
        /// flight, TP-1782 concluding remarks: "the two methods of gross thrust calculation agreed
        /// within +/-3 percent" over 66 evaluation points.
        ///
        /// This bounds the AGREEMENT OF TWO CALCULATIONS, not the error of either against truth,
        /// and it is about GROSS thrust. It is an oracle only.
        /// </summary>
        public const float FlightMethodAgreementPercent = 3f;

        public const string FlightValidationCitation =
            "NASA TP-1782, printed pp. 1 and 17. Engine P680059 installed in the LEFT engine "
            + "position of an F-15; only that engine was flown. CROSS-VALIDATION ONLY: the report "
            + "publishes percentage agreement, and withholds the SGTM coefficients K1, K2, E "
            + "and Cv entirely.";
    }
}
