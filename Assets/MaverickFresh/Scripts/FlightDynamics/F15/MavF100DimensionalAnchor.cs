using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// One altitude-facility calibration test condition for engine P680059 or P680063.
    ///
    /// Transcribed from NASA TP-1373 table 3, printed p. 9, which reproduces in full the test
    /// matrices of the two facility reports - TP-1069 (engine 059) and TP-1228 (engine 063). Those
    /// two reports are NOT held in this repository; their condition lists are, because TP-1373
    /// prints them.
    /// </summary>
    public struct MavF100CalibrationCondition
    {
        public string engineSerial;
        public float mach;
        public float altitudeM;
        public float inletTotalTemperatureK;
        public float reynoldsNumberIndex;
        public bool distortedInlet;
    }

    /// <summary>
    /// Whether the two engine builds in the R5 pack have been shown to be the same engine.
    ///
    /// Exists as a type rather than a comment because it gates an arithmetic operation: multiplying
    /// TP-1034's normalized net thrust by a dimensional thrust measured on engines 059/063 asserts
    /// that the two builds produce the same thrust, and that assertion has to be made by somebody
    /// who can point at a source.
    /// </summary>
    public struct MavF100ConfigurationEquivalence
    {
        public bool proven;

        /// <summary>The source that proves it. Required non-empty before <see cref="proven"/> counts.</summary>
        public string provingSource;

        public static MavF100ConfigurationEquivalence Unproven
        {
            get { return new MavF100ConfigurationEquivalence(); }
        }

        public bool IsUsable
        {
            get { return proven && !string.IsNullOrEmpty(provingSource); }
        }
    }

    /// <summary>
    /// The sea-level-static anchor: the one route by which the missing design maximum net thrust
    /// could be recovered, and the reasons it does not close with the sources in hand.
    ///
    /// THE MECHANISM, WHICH IS SOUND
    /// -----------------------------
    /// Ram drag is 20.041 * w2 * M0 * sqrt(T0), so at M0 = 0 it is identically zero - not small,
    /// zero, with no airflow value needed. Uninstalled net thrust and gross thrust are therefore
    /// the same number at a static condition.
    ///
    /// TP-1034 figure 17(a) is sea level, Mach 0, and reaches exactly 1.0 at PLA 130, which is how
    /// its normalizer is defined. So:
    ///
    ///     design maximum net thrust
    ///       = uninstalled net thrust at SLS, maximum augmentation
    ///       = GROSS thrust at SLS, maximum augmentation
    ///
    /// That is a real result. It converts the blocker from "a normalizer nobody published" into
    /// "sea-level static maximum-augmentation gross thrust", which is a far more findable quantity
    /// and one that engine test reports routinely contain.
    ///
    /// WHY IT STILL DOES NOT CLOSE
    /// ---------------------------
    /// Three gates, and the chain fails at the first.
    ///
    ///   1. NO STATIC POINT EXISTS IN THE CANDIDATE SOURCES. TP-1069 and TP-1228 are ALTITUDE
    ///      facility calibrations. Their full test matrices are reproduced in TP-1373 table 3, and
    ///      the lowest condition either engine ever ran is Mach 0.80 at 4 020 m. Neither report
    ///      contains a sea-level-static point, so neither can supply this anchor at all. This is
    ///      checkable, not asserted: see <see cref="HasStaticCondition"/>, which searches the
    ///      transcribed matrices and finds nothing.
    ///
    ///   2. AT THEIR ACTUAL CONDITIONS THE IDENTITY IS UNAVAILABLE. At Mach 0.80 ram drag is a
    ///      large fraction of gross thrust, so converting to net needs absolute engine airflow at
    ///      that condition - which TP-1373 publishes only as calibration percentages against the
    ///      unavailable manufacturer deck. And the result would be net thrust at 4 020 m / Mach
    ///      0.80, a condition figure 17 does not plot: its subsonic panels are 0, 3.048, 9.144 and
    ///      13.72 km. There is nothing there to anchor TO.
    ///
    ///   3. THE ENGINE BUILDS ARE NOT SHOWN TO BE THE SAME. TP-1373 printed p. 4 records that the
    ///      calibration engines are prototype series 2 7/8: series 2 cores, a series 3 fan,
    ///      "control schedule differences from both the series 2 and 3 engines", and series 2
    ///      actuated divergent nozzles where series 3 engines have free-floating ones. Nozzle
    ///      actuation and control schedule are precisely what set maximum augmented gross thrust.
    ///
    ///      Worse for equivalence: neither TP-1034 nor TM X-3261 uses the word "series" anywhere.
    ///      Nothing in the pack relates the "(1)" and "(3)" designations of the simulation reports
    ///      to the "series 2 / 2 7/8 / 3" designations of the calibration reports. Equivalence
    ///      cannot be proven from these four documents even in principle, because no document
    ///      relates the two naming schemes.
    ///
    /// So TP-1069/TP-1228 dimensional gross thrust and TP-1034 normalized net thrust stay SEPARATE
    /// datasets. <see cref="MavF100DimensionalGrossThrustDataset"/> is where the first would live.
    /// </summary>
    public static class MavF100DimensionalAnchor
    {
        /// <summary>
        /// Test conditions of NASA TP-1069 (engine P680059), from TP-1373 table 3(a), printed p. 9.
        ///
        /// Eight conditions. The lowest Mach number is 0.80 and the lowest altitude is 4 020 m.
        /// </summary>
        public static MavF100CalibrationCondition[] Engine059Conditions
        {
            get
            {
                return new MavF100CalibrationCondition[]
                {
                    Condition("P680059", 0.80f, 4020f, 296f, 0.89f, false),
                    Condition("P680059", 0.80f, 4020f, 284f, 0.93f, false),
                    Condition("P680059", 0.80f, 4020f, 313f, 0.83f, false),
                    Condition("P680059", 0.89f, 7380f, 278f, 0.66f, false),
                    Condition("P680059", 1.20f, 12100f, 279f, 0.46f, false),
                    Condition("P680059", 1.20f, 12100f, 290f, 0.45f, false),
                    Condition("P680059", 1.40f, 15240f, 301f, 0.34f, false),
                    Condition("P680059", 0.80f, 4020f, 296f, 0.89f, true)
                };
            }
        }

        /// <summary>
        /// Test conditions of NASA TP-1228 (engine P680063), from TP-1373 table 3(b), printed p. 9.
        ///
        /// Eight conditions. The lowest Mach number is 0.80 and the lowest altitude is 4 020 m.
        /// </summary>
        public static MavF100CalibrationCondition[] Engine063Conditions
        {
            get
            {
                return new MavF100CalibrationCondition[]
                {
                    Condition("P680063", 0.80f, 4020f, 296f, 0.89f, false),
                    Condition("P680063", 0.89f, 7380f, 278f, 0.66f, false),
                    Condition("P680063", 0.89f, 7380f, 295f, 0.62f, false),
                    Condition("P680063", 1.40f, 15240f, 301f, 0.34f, false),
                    Condition("P680063", 0.90f, 13720f, 252f, 0.29f, false),
                    Condition("P680063", 1.60f, 9140f, 339f, 0.96f, false),
                    Condition("P680063", 2.00f, 15240f, 390f, 0.58f, false),
                    Condition("P680063", 0.89f, 7380f, 278f, 0.66f, true)
                };
            }
        }

        private static MavF100CalibrationCondition Condition(
            string serial, float mach, float altitudeM, float tt2K, float rni, bool distorted)
        {
            MavF100CalibrationCondition c = new MavF100CalibrationCondition();
            c.engineSerial = serial;
            c.mach = mach;
            c.altitudeM = altitudeM;
            c.inletTotalTemperatureK = tt2K;
            c.reynoldsNumberIndex = rni;
            c.distortedInlet = distorted;
            return c;
        }

        public const string CalibrationConditionCitation =
            "NASA TP-1373 table 3, printed p. 9, which reproduces the test matrices of "
            + "NASA TP-1069 (engine P680059) and NASA TP-1228 (engine P680063). Those two reports "
            + "are not held in this repository; figure 4 on the same page plots the same "
            + "conditions and shows no point below Mach 0.8 or 4 020 m.";

        /// <summary>
        /// Mach number below which a condition counts as static for the ram-drag argument.
        ///
        /// Tight on purpose. The whole value of the static anchor is that ram drag is EXACTLY zero,
        /// so a condition that is merely slow does not qualify - at Mach 0.1 the ram drag is small
        /// but unknown, because the airflow term is unknown.
        /// </summary>
        public const float StaticConditionMachTolerance = 0.001f;

        /// <summary>
        /// Whether either calibration report contains a sea-level-static condition. It does not,
        /// and this searches rather than asserts, so the conclusion survives someone editing the
        /// transcribed matrices.
        /// </summary>
        public static bool HasStaticCondition(MavF100CalibrationCondition[] conditions)
        {
            if (conditions == null)
                return false;

            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i].mach <= StaticConditionMachTolerance)
                    return true;
            }

            return false;
        }

        /// <summary>The lowest Mach number any listed condition reached. 0.80 for both engines.</summary>
        public static float LowestMach(MavF100CalibrationCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
                return float.NaN;

            float lowest = conditions[0].mach;
            for (int i = 1; i < conditions.Length; i++)
            {
                if (conditions[i].mach < lowest)
                    lowest = conditions[i].mach;
            }

            return lowest;
        }

        /// <summary>
        /// Converts a sea-level-static maximum-augmentation GROSS thrust into the design maximum
        /// net thrust that TP-1034 figure 17 is normalized by - if, and only if, all three gates
        /// pass.
        ///
        /// Nothing available today passes gate one, so this never returns a value in the current
        /// repository. It is written out because the gates ARE the finding: they record exactly
        /// what a future source would have to supply, and they refuse anything less. A comment
        /// saying "be careful about configuration" would not stop the multiplication; this does.
        /// </summary>
        public static MavF100ThrustValue DesignMaximumNetThrustFromStaticGross(
            MavF100ThrustValue sealevelStaticMaximumAugmentedGross,
            float machAtMeasurement,
            MavF100ConfigurationEquivalence equivalenceToPw100Dash3)
        {
            if (!sealevelStaticMaximumAugmentedGross.valid)
            {
                return MavF100ThrustValue.Invalid(
                    "no candidate gross thrust: " + sealevelStaticMaximumAugmentedGross.reason);
            }

            // Gate 2, checked first because it is the cheapest: the value has to BE gross thrust.
            if (sealevelStaticMaximumAugmentedGross.quantity != MavF100ThrustQuantity.GrossThrust)
            {
                return MavF100ThrustValue.Invalid(
                    "the anchor identity gross == net holds only for GROSS thrust; this value is "
                    + "labelled " + sealevelStaticMaximumAugmentedGross.quantity);
            }

            // Gate 1: the identity needs M0 = 0 exactly. Anything else leaves a ram drag term
            // that cannot be evaluated without absolute engine airflow.
            if (!(machAtMeasurement >= 0f) || machAtMeasurement > StaticConditionMachTolerance)
            {
                return MavF100ThrustValue.Invalid(
                    "gross thrust equals net thrust only at a static condition. This value was "
                    + "measured at Mach " + machAtMeasurement + ", where ram drag is non-zero and "
                    + "cannot be removed without absolute engine airflow at that condition. "
                    + "Neither TP-1069 nor TP-1228 contains a static point - their lowest "
                    + "condition is Mach 0.80 at 4 020 m.");
            }

            // Gate 3: the builds have to be the same engine, said by a source.
            if (!equivalenceToPw100Dash3.IsUsable)
            {
                return MavF100ThrustValue.Invalid(
                    "configuration equivalence to the F100-PW-100(3) of TP-1034 is not proven. "
                    + "TP-1373 p. 4 records that the calibration engines are prototype series "
                    + "2 7/8 with series 2 cores, control schedules differing from both series 2 "
                    + "and series 3, and series 2 actuated divergent nozzles - and no document in "
                    + "the pack relates the (1)/(3) designations to the series designations at "
                    + "all. A proving source must be named.");
            }

            return MavF100ThrustValue.Of(
                sealevelStaticMaximumAugmentedGross.newtons,
                MavF100ThrustQuantity.UninstalledNetThrust,
                MavF100SourceClass.CompatibleSupport,
                "design maximum net thrust = SLS maximum-augmentation gross thrust, since ram "
                + "drag is identically zero at M0 = 0 per TM X-3261 (B52) / TP-1034 (B56); "
                + "configuration equivalence per: " + equivalenceToPw100Dash3.provingSource);
        }
    }

    /// <summary>
    /// The separate dimensional gross-thrust dataset for engines P680059 and P680063.
    ///
    /// Held apart from <see cref="MavF100NormalizedNetThrustModel"/> deliberately and permanently
    /// until a source proves the two describe the same engine. One is dimensionless net thrust for
    /// the F100-PW-100(3); the other would be dimensional gross thrust for prototype series 2 7/8
    /// engines. Multiplying one by the other is the mistake this separation exists to prevent, and
    /// <see cref="MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross"/> is the only
    /// place the two may ever meet.
    ///
    /// EMPTY TODAY. TP-1069 and TP-1228 are not in this repository. TP-1373 summarises their
    /// results only as percentage calibrations against a manufacturer deck that is also absent, so
    /// no dimensional gross thrust can be reconstructed from what is held.
    ///
    /// The one dimensional gross-thrust plot that IS held - TP-1373 figure 10, printed p. 23 -
    /// is not usable as a dataset: its abscissa is FTIT/theta, augmented and non-augmented points
    /// stack at the same abscissa because the control holds the gas generator at military values
    /// during augmentation, and it covers a single condition. See the source audit.
    /// </summary>
    public static class MavF100DimensionalGrossThrustDataset
    {
        public const MavF100SourceClass SourceClass = MavF100SourceClass.CompatibleSupport;

        public const MavF100ThrustQuantity Quantity = MavF100ThrustQuantity.GrossThrust;

        public const string EngineBuild = "F100 prototype series 2 7/8 (P680059, P680063)";

        /// <summary>True when dimensional gross-thrust points have actually been loaded. They have not.</summary>
        public static bool Available
        {
            get { return false; }
        }

        public const string UnavailableReason =
            "NASA TP-1069 and TP-1228 are not held in this repository. TP-1373 reproduces their "
            + "TEST CONDITIONS (table 3, p. 9) but reports their RESULTS only as percentage "
            + "calibrations against P&W CCD 1088-2.0, which is also absent, so no dimensional "
            + "gross thrust can be reconstructed from what is held.";
    }
}
