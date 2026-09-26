using System.Collections.Generic;
using System.Collections.ObjectModel;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>One printed row of Table VII's first half (PDF pp.124-128), exactly as printed.</summary>
    public struct MavF15TableViiPart1Row
    {
        public int point;
        public double stabilatorDeg;
        public double alphaDeg;
        public double betaDeg;
        public double rollRateRadSec;

        /// <summary>Empty, or what was damaged in the scan and how the value was obtained.</summary>
        public string note;
    }

    /// <summary>One printed row of Table VII's second half (PDF pp.129-133), exactly as printed.</summary>
    public struct MavF15TableViiPart2Row
    {
        public int point;
        public double pitchRateRadSec;
        public double yawRateRadSec;
        public double pitchAngleDeg;
        public double bankAngleDeg;

        /// <summary>As printed: thousands of ft/s.</summary>
        public double trueVelocityKftPerSec;
        public string note;
    }

    /// <summary>How a published equilibrium state was assembled from the two printed halves.</summary>
    public enum MavF15TableViiPairing
    {
        /// <summary>Symmetric points 1-91: both halves share the printed point number.</summary>
        SymmetricSameLabel = 0,

        /// <summary>
        /// Turning points: p, alpha, beta and stabilator from the first half and q from the second
        /// half share the printed number i; r, theta, phi and V are printed two rows lower, at i + 2.
        /// </summary>
        TurningDisplacedColumns = 1
    }

    /// <summary>One published equilibrium state, in the source's own units.</summary>
    public struct MavF15TableViiState
    {
        public int part1Point;
        public int part2PitchRatePoint;
        public int part2OtherColumnsPoint;
        public MavF15TableViiPairing pairing;

        public double stabilatorDeg;
        public double alphaDeg;
        public double betaDeg;
        public double pRadSec;
        public double qRadSec;
        public double rRadSec;
        public double thetaDeg;
        public double phiDeg;
        public double trueVelocityFtPerSec;
    }

    /// <summary>
    /// Baumann (AFIT/GAE/ENY/89D-01, DTIC ADA217366) Table VII, "Tabulation of Low alpha Equilibrium
    /// Conditions": 201 stable equilibria of the research model, computed by AUTO continuation in
    /// stabilator deflection, with aileron, rudder and differential stabilator at 0 deg, thrust fixed
    /// at 8,300 lb and altitude fixed at 20,000 ft (caption, PDF p.124).
    ///
    /// RESEARCH-MODEL OUTPUT, VALIDATION ONLY. Printed by the thesis that computed it, so the values
    /// are OriginalPrimary for the research model. They describe the AFIT research model, never
    /// NASA 836, and no file outside the Validation folder may read them.
    ///
    /// STORED EXACTLY AS PRINTED, TWO HALVES. The table prints each point across two pages of four
    /// and five columns. Both halves are stored under their printed point numbers.
    ///
    /// HOW THE HALVES PAIR is a finding, not an assumption. See
    /// Docs/Reference/F15_TABLE_VII_EQUILIBRIUM_VALIDATION_V1.0.md.
    ///   - Points 1-91 (symmetric) pair by printed number.
    ///   - Turning points: r, theta, phi and V are printed two rows below the p, alpha, beta,
    ///     stabilator and q of the same state.
    ///   - Both steady-state kinematic equations (theta-dot = phi-dot = 0) close to print precision
    ///     under that pairing, and fail under same-number pairing. They involve no aerodynamics, so
    ///     the pairing does not depend on the model being tested.
    ///   - First-half points 92-116, 200 and 201 have no demonstrable second-half companion: the
    ///     candidates are mirror images or absent. They are not assembled.
    /// </summary>
    public static partial class MavF15BaumannTableVii
    {
        public const string Citation =
            "Baumann, AFIT/GAE/ENY/89D-01, DTIC ADA217366, Appendix C Table VII, PDF pp.124-133 "
            + "(printed pp.109-118)";

        public const string ResearchConfigurationId = MavF15AfitResearchIdentity.ConfigurationId;

        /// <summary>From the table caption. Every point shares these.</summary>
        public const double CaptionThrustLbf = 8300.0;
        public const double CaptionAltitudeFt = 20000.0;
        public const double CaptionAileronDeg = 0.0;
        public const double CaptionRudderDeg = 0.0;
        public const double CaptionDifferentialStabilatorDeg = 0.0;

        public const int PrintedPointCount = 201;
        public const int LastSymmetricPoint = 91;
        public const int FirstPairedTurningPoint = 117;
        public const int LastPairedTurningPoint = 199;
        public const int TurningColumnDisplacement = 2;

        private static ReadOnlyCollection<MavF15TableViiPart1Row> part1;
        private static ReadOnlyCollection<MavF15TableViiPart2Row> part2;

        public static ReadOnlyCollection<MavF15TableViiPart1Row> Part1
        {
            get { return part1 ?? (part1 = BuildPart1()); }
        }

        public static ReadOnlyCollection<MavF15TableViiPart2Row> Part2
        {
            get { return part2 ?? (part2 = BuildPart2()); }
        }

        /// <summary>
        /// Every equilibrium state that can be assembled from the printed halves by the demonstrated
        /// pairing, skipping any state with an illegible field. States come back in first-half
        /// point order.
        /// </summary>
        public static List<MavF15TableViiState> PairedStates()
        {
            List<MavF15TableViiState> states = new List<MavF15TableViiState>(180);
            for (int i = 1; i <= LastSymmetricPoint; i++)
                Add(states, i, i, i, MavF15TableViiPairing.SymmetricSameLabel);

            for (int i = FirstPairedTurningPoint; i <= LastPairedTurningPoint; i++)
            {
                Add(states, i, i, i + TurningColumnDisplacement,
                    MavF15TableViiPairing.TurningDisplacedColumns);
            }

            return states;
        }

        /// <summary>First-half points that are printed but never assembled into a state, and why.</summary>
        public static string UnassembledReason(int part1Point)
        {
            if (part1Point >= 92 && part1Point <= 116)
                return "no demonstrable second-half companion: the matching second-half rows are the "
                       + "mirror-image turn (opposite bank and yaw rate) or absent";
            if (part1Point == 200 || part1Point == 201)
                return "its displaced second-half columns would fall at points 202-203, which are not printed";
            return null;
        }

        private static void Add(
            List<MavF15TableViiState> states, int p1, int p2q, int p2rest, MavF15TableViiPairing pairing)
        {
            MavF15TableViiPart1Row a = Part1[p1 - 1];
            MavF15TableViiPart2Row b = Part2[p2q - 1];
            MavF15TableViiPart2Row c = Part2[p2rest - 1];

            MavF15TableViiState s = new MavF15TableViiState
            {
                part1Point = p1,
                part2PitchRatePoint = p2q,
                part2OtherColumnsPoint = p2rest,
                pairing = pairing,
                stabilatorDeg = a.stabilatorDeg,
                alphaDeg = a.alphaDeg,
                betaDeg = a.betaDeg,
                pRadSec = a.rollRateRadSec,
                qRadSec = b.pitchRateRadSec,
                rRadSec = c.yawRateRadSec,
                thetaDeg = c.pitchAngleDeg,
                phiDeg = c.bankAngleDeg,
                trueVelocityFtPerSec = c.trueVelocityKftPerSec * 1000.0
            };

            if (double.IsNaN(s.stabilatorDeg) || double.IsNaN(s.alphaDeg) || double.IsNaN(s.betaDeg)
                || double.IsNaN(s.pRadSec) || double.IsNaN(s.qRadSec) || double.IsNaN(s.rRadSec)
                || double.IsNaN(s.thetaDeg) || double.IsNaN(s.phiDeg) || double.IsNaN(s.trueVelocityFtPerSec))
                return;

            states.Add(s);
        }

        private static ReadOnlyCollection<MavF15TableViiPart1Row> BuildPart1()
        {
            List<MavF15TableViiPart1Row> rows = new List<MavF15TableViiPart1Row>(PrintedPointCount);
            for (int i = 0; i < Part1Values.GetLength(0); i++)
            {
                rows.Add(new MavF15TableViiPart1Row
                {
                    point = (int)Part1Values[i, 0],
                    stabilatorDeg = Part1Values[i, 1],
                    alphaDeg = Part1Values[i, 2],
                    betaDeg = Part1Values[i, 3],
                    rollRateRadSec = Part1Values[i, 4],
                    note = NoteFor(Part1Notes, (int)Part1Values[i, 0])
                });
            }

            return rows.AsReadOnly();
        }

        private static ReadOnlyCollection<MavF15TableViiPart2Row> BuildPart2()
        {
            List<MavF15TableViiPart2Row> rows = new List<MavF15TableViiPart2Row>(PrintedPointCount);
            for (int i = 0; i < Part2Values.GetLength(0); i++)
            {
                rows.Add(new MavF15TableViiPart2Row
                {
                    point = (int)Part2Values[i, 0],
                    pitchRateRadSec = Part2Values[i, 1],
                    yawRateRadSec = Part2Values[i, 2],
                    pitchAngleDeg = Part2Values[i, 3],
                    bankAngleDeg = Part2Values[i, 4],
                    trueVelocityKftPerSec = Part2Values[i, 5],
                    note = NoteFor(Part2Notes, (int)Part2Values[i, 0])
                });
            }

            return rows.AsReadOnly();
        }

        private static string NoteFor(string[] notes, int point)
        {
            string key = point.ToString();
            for (int i = 0; i + 1 < notes.Length; i += 2)
            {
                if (notes[i] == key)
                    return notes[i + 1];
            }

            return "";
        }
    }
}
