namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// HOW a number reached us. Separate from <see cref="MavF15ConfigurationScope"/>, which says
    /// WHICH aircraft it describes: a value can be an exact original for the wrong airframe, or a
    /// faithful reproduction of the right family, and one axis cannot say both.
    /// </summary>
    public enum MavF15SourceLineage
    {
        /// <summary>Not stated. Allowed only on a candidate that carries no value.</summary>
        Unspecified = 0,

        /// <summary>The value as printed in the originating document, held and read.</summary>
        OriginalPrimary = 1,

        /// <summary>
        /// A public document reproducing a value it attributes to another source that is NOT held
        /// - for example an AFIT thesis table attributed to MDC A4172. Saying "taken from McDonnell
        /// report" is not the same as holding the McDonnell report.
        /// </summary>
        PublicReproduction = 2,

        /// <summary>A constant or table of a simulation program, reproduced in a listing.</summary>
        DerivedSimulator = 3,

        /// <summary>A polynomial or other fit to data the fitter held and the reader does not.</summary>
        CurveFit = 4,

        /// <summary>A consistency relation between sources. Checks; never selects.</summary>
        CrossValidation = 5
    }

    /// <summary>WHICH aircraft configuration a value describes.</summary>
    public enum MavF15ConfigurationScope
    {
        /// <summary>Not stated. Allowed only on a candidate that carries no value.</summary>
        Unspecified = 0,

        /// <summary>NASA F-15B 836 / 74-0141 itself.</summary>
        Exact836 = 1,

        /// <summary>Production F-15A-D family, including the F/TF-15 designations of 1976.</summary>
        ProductionF15Family = 2,

        /// <summary>A preproduction airframe, e.g. F-15 No. 8 in NASA TM-72861.</summary>
        Preproduction = 3,

        /// <summary>A research-modified airframe, e.g. NF-15B 837 with canards and PW-229s.</summary>
        ResearchModified = 4,

        /// <summary>
        /// A model its own author says represents no particular aircraft - Brumbaugh's AIAA
        /// Controls Design Challenge model (NASA CR-186019) says exactly that.
        /// </summary>
        NotRepresentativeOfAnyAircraft = 5
    }

    /// <summary>
    /// Acquisition status of the two McDonnell source families public F-15 research keeps
    /// citing. Full lineage: Docs/Reference/F15_A4172_SOURCE_LINEAGE_V0.1.md and
    /// Docs/Reference/F15_DN1180_SOURCE_LINEAGE_V0.1.md.
    ///
    /// Neither original is held, so nothing in this project may carry
    /// <see cref="MavF15SourceLineage.OriginalPrimary"/> with either as its source. Every value
    /// attributed to them is a <see cref="MavF15SourceLineage.PublicReproduction"/>.
    /// </summary>
    public static class MavF15McDonnellSources
    {
        /// <summary>
        /// MDC A4172. Titles as cited: "F/TF-15 Stability Derivatives, Mass and Inertia
        /// Characteristics Flight Test [Data] Basis", Part I "Mass and Inertia Characteristics",
        /// Part II "Aerodynamic Coefficients and Stability and Control Derivatives"; Parts I and
        /// II Rev. C, August 1976; Part I dated 1 August 1976, contract F33657-70-0300; Part I
        /// Supplement 1, 4 October 1979, USAF Series Manual A-11-2-2-1-1, Aero/Inertia.
        /// </summary>
        public const string MdcA4172Identity =
            "MDC A4172, F/TF-15 Stability Derivatives, Mass and Inertia Characteristics Flight "
            + "Test Basis (Part I mass/inertia; Part II aerodynamic coefficients and stability and "
            + "control derivatives); Rev. C Aug 1976; Part I Supplement 1 Oct 1979; USAF Series "
            + "Manual A-11-2-2-1-1 Aero/Inertia.";

        /// <summary>No public original of any part of MDC A4172 was located.</summary>
        public const bool MdcA4172OriginalHeld = false;

        /// <summary>McDonnell Aircraft, "F-15 Flight Control System Description", October 1981.</summary>
        public const string Dn1180Identity =
            "McDonnell Aircraft Company, F-15 Flight Control System Description, Design Note "
            + "DN-1180.01-238-458 (Rev. D), October 1981.";

        /// <summary>No public original of DN-1180.01-238-458 was located.</summary>
        public const bool Dn1180OriginalHeld = false;
    }
}
