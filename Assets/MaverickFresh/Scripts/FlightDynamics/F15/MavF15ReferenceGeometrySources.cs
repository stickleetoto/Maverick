namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// How far a geometry value may be trusted for the exact NASA F-15B 836 target.
    ///
    /// The two accepting grades are kept apart from family support on purpose. A value that is
    /// merely the familiar F-15 number, however plausible, never reaches them.
    /// </summary>
    public enum MavF15GeometryAuthority
    {
        /// <summary>No held source states the value.</summary>
        Unavailable = 0,

        /// <summary>
        /// Stated for an airframe or aerodynamic model that is not the target - for example the
        /// preproduction canard NF-15B 837. Never target authority.
        /// </summary>
        Incompatible = 1,

        /// <summary>May only be used to check consistency. Never selects a value.</summary>
        CrossValidationOnly = 2,

        /// <summary>
        /// A genuine F-15-family value from a production-aircraft lineage, with no source chain
        /// connecting it to the model that describes NASA 836.
        /// </summary>
        F15FamilySupport = 3,

        /// <summary>
        /// A NASA 836 source states that its baseline aerodynamic model uses the unchanged
        /// production-F-15 coefficient reference dimensions AND names those dimensions. Accepting.
        /// </summary>
        ExplicitProductionReferenceUsedBy836Model = 4,

        /// <summary>A source about NASA 836 itself prints the value. Accepting.</summary>
        DirectExact836 = 5
    }

    /// <summary>
    /// What a geometry value IS. This is separate from how far it may be trusted: a perfectly
    /// sourced physical wingspan is still not a coefficient reference span, and only the three
    /// coefficient-reference kinds may dimensionalize Cx/Cy/Cz/Cl/Cm/Cn.
    /// </summary>
    public enum MavF15GeometryQuantity
    {
        /// <summary>Tip-to-tip span of the airframe, as in a three-view. Physical, not a normalizer.</summary>
        PhysicalWingSpan = 0,

        /// <summary>S, the area an aerodynamic model divides its forces and moments by.</summary>
        CoefficientReferenceArea = 1,

        /// <summary>cbar, the length the pitching moment and pitch rate are normalized by.</summary>
        CoefficientReferenceChord = 2,

        /// <summary>b, the length the rolling/yawing moments and p, r are normalized by.</summary>
        CoefficientReferenceSpan = 3,

        /// <summary>The point about which a model's moment coefficients are referenced.</summary>
        MomentReferenceLocation = 4,

        /// <summary>
        /// A centre-of-gravity location. NOT a moment reference: the two coincide only if a source
        /// says so, and the one NASA table that prints both shows them at different stations.
        /// </summary>
        CenterOfGravityLocation = 5
    }

    /// <summary>One source-stated geometry value, with what it is and how far it may be trusted.</summary>
    public struct MavF15GeometryCandidate
    {
        public string id;
        public MavF15GeometryQuantity quantity;

        /// <summary>The value exactly as printed, in <see cref="sourceUnits"/>.</summary>
        public float sourceValue;
        public string sourceUnits;

        /// <summary>
        /// SI equivalent of <see cref="sourceValue"/> (m^2 for areas, m for lengths). Zero for
        /// values that are not a single SI scalar - a %MAC, or a station triple.
        /// </summary>
        public float siValue;

        /// <summary>
        /// Candidates printed together as one reference set share this id. A coefficient
        /// reference set may only be assembled from a single id.
        /// </summary>
        public string referenceSetId;

        /// <summary>The airframe the source describes.</summary>
        public string airframe;
        public string citation;
        public MavF15GeometryAuthority authority;
        public string reason;

        public bool IsCoefficientReference
        {
            get
            {
                return quantity == MavF15GeometryQuantity.CoefficientReferenceArea
                    || quantity == MavF15GeometryQuantity.CoefficientReferenceChord
                    || quantity == MavF15GeometryQuantity.CoefficientReferenceSpan;
            }
        }

        public bool AuthorityAcceptedForExactTarget
        {
            get
            {
                return authority == MavF15GeometryAuthority.DirectExact836
                    || authority
                        == MavF15GeometryAuthority.ExplicitProductionReferenceUsedBy836Model;
            }
        }

        /// <summary>
        /// Both conditions, together: the value is a coefficient reference quantity AND its
        /// authority is accepted. Neither alone is enough.
        /// </summary>
        public bool MayDimensionalizeExactTargetCoefficients
        {
            get
            {
                return IsCoefficientReference
                    && AuthorityAcceptedForExactTarget
                    && siValue > 0f
                    && !string.IsNullOrEmpty(referenceSetId);
            }
        }
    }

    /// <summary>A coefficient reference set drawn from ONE printed source set.</summary>
    public struct MavF15CoefficientReferenceSet
    {
        public string referenceSetId;
        public float areaM2;
        public float chordM;
        public float spanM;

        public bool IsComplete
        {
            get
            {
                return !string.IsNullOrEmpty(referenceSetId)
                    && areaM2 > 0f && chordM > 0f && spanM > 0f;
            }
        }
    }

    /// <summary>
    /// Every aerodynamic-reference geometry value examined for NASA F-15B 836 / 74-0141, with its
    /// quantity kind, its authority grade and the reason for that grade.
    ///
    /// RESULT OF THE AUDIT: no held primary source for NASA 836 prints S, cbar, a coefficient
    /// reference span or a moment reference, and none states which reference dimensions its
    /// baseline aerodynamic model uses. The exact-target coefficient reference set therefore
    /// stays UNAVAILABLE, and <see cref="MavF15ReferenceData.CreateExactTargetGeometry"/> stays
    /// zero. Full audit: Docs/Reference/F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1.md.
    ///
    /// 608 ft^2 and 15.94 ft ARE present in NASA material - but for NF-15B 837, a preproduction
    /// F-15B with canards, F100-PW-229 engines and thrust-vectoring nozzles, and in the McAir
    /// ARO10 / 1988 F-15 aerobase lineage behind the AFIT research model. Neither is connected by
    /// any source to the model that describes 836.
    /// </summary>
    public static class MavF15ReferenceGeometrySources
    {
        public const float FootToM = 0.3048f;
        public const float SquareFootToM2 = 0.09290304f;

        public const string Nasa836Airframe =
            "NASA F-15B 836 / USAF 74-0141, production two-seat F-15B, two F100-PW-100";

        public const string Nf15b837Airframe =
            "NASA NF-15B 837, PRE-PRODUCTION F-15B, canards (modified F/A-18 stabilators), "
            + "two F100-PW-229 with axisymmetric thrust-vectoring nozzles";

        public const string Aro10Airframe =
            "McAir ARO10 / 1988 F-15 aerobase (production F-15, clean configuration), as "
            + "reproduced in the AFIT research model";

        // ---------------------------------------------------------------- NASA 836: physical

        /// <summary>
        /// Physical wingspan of the NASA F-15B research test bed, 42.8 ft. PHYSICAL GEOMETRY ONLY.
        ///
        /// Printed alongside the 63.7-ft length and 18.7-ft height as the airplane's overall
        /// dimensions in NASA TM-4782 p.10, NASA/TM-2005-213670 p.6, NASA/TM-2006-213674 p.12 and
        /// AIAA 2001-3303 p.6, and dimensioned on the three-view in NASA/TM-2006-213675 p.18.
        /// NASA/TM-2008-214634 p.6 names "tail number 836" and cites TM-4782 and TM-2006-213675 as
        /// that same airplane's research-test-bed history, which is what makes the value 836's.
        ///
        /// None of these sources calls it a reference span, and the one NASA report that prints
        /// both quantities for an F-15 shows them differing: TM-2003-212027 dimensions NF-15B 837
        /// at 42.83 ft on its three-view and uses 42.7 ft as its reference span.
        /// </summary>
        public const float Nasa836PhysicalWingSpanFt = 42.8f;
        public const float Nasa836PhysicalLengthFt = 63.7f;
        public const float Nasa836PhysicalHeightFt = 18.7f;

        // ---------------------------------------------------------------- NASA 836: CG statements

        /// <summary>
        /// AIAA 2001-3303 p.12 (Propulsion Flight Test Fixture, flown on tail 836): the CFD
        /// analysis used an aircraft centre of gravity of 28 percent MAC, "which corresponded to a
        /// fuselage station of 561.7". A CG location for one analysis - NOT a moment reference.
        ///
        /// It is also one equation in two unknowns (leading-edge MAC station, and cbar), so it
        /// cannot determine cbar on its own.
        /// </summary>
        public const float Nasa836PftfAnalysisCgPercentMac = 28f;
        public const float Nasa836PftfAnalysisCgFuselageStationIn = 561.7f;

        // ---------------------------------------------------------------- NF-15B 837

        public const string Nf15b837ReferenceSetId =
            "NF15B_837_NASA_TM_2003_212027_TABLE_1";

        public const string Nf15b837Citation =
            "NASA/TM-2003-212027 (Smith & Moes, NTRS 20030079970) p.8 table 1 'Test aircraft "
            + "reference dimensions', described as 'the reference areas and lengths used for "
            + "nondimensionalizing forces and moments'; figure 2 p.23 marks the airplane '837'. "
            + "Same values in Morelli & Smith, 'Real-Time Dynamic Modeling', NTRS 20100004852 "
            + "p.16 table 1 (and 20080034476 p.15). Verified against rendered page images.";

        public const float Nf15b837ReferenceAreaFt2 = 608f;
        public const float Nf15b837ReferenceChordFt = 15.94f;
        public const float Nf15b837ReferenceSpanFt = 42.7f;

        /// <summary>Three-view span of the same airplane, same report, p.23: 42.83 ft.</summary>
        public const float Nf15b837PhysicalWingSpanFt = 42.83f;

        public const float Nf15b837MomentReferenceFsIn = 557.2f;
        public const float Nf15b837MomentReferenceWlIn = 116.3f;
        public const float Nf15b837MomentReferenceBlIn = 0f;

        /// <summary>
        /// Morelli &amp; Smith table 1: x_cg = 560.40 in against x_ref = 557.2 in. The moment
        /// reference and the CG are printed side by side and are not the same point.
        /// </summary>
        public const float Nf15b837CgFsIn = 560.40f;

        // ---------------------------------------------------------------- ARO10 lineage

        public const string Aro10ReferenceSetId = "MCAIR_ARO10_1988_F15_AEROBASE_VIA_DAVISON";

        public const string Aro10Citation =
            "Davison, AFIT/GAE/ENY/92M-01, Appendix C listing: printed p.121 'primary source ... "
            + "subroutine ARO10 from McAir code used in the F15 baseline simulator'; p.123 "
            + "'DATA CMCGR /.2565/, CNCGR /.2565/ - the aero stability data was taken referenced "
            + "to these CG locations'; p.126-127 'SPAN = WING SPAN = 42.8 FEET = BWING', "
            + "'MAC = MEAN AERODYNAMIC CHORD = 15.94 FEET = CWING'. Pages re-read this pass.";

        /// <summary>
        /// The listing pages re-read in this pass name SREF (p.121, p.123) but do not print its
        /// value. 608 ft^2 is carried from the R2 research-model transcription
        /// (<see cref="MavF15BaumannMach06Reference.WingAreaFt2"/>) and was not re-located here.
        /// </summary>
        public const string Aro10AreaProvenanceNote =
            "SREF is named on listing pp.121/123 but its numeric value was not located in the "
            + "pages re-read this pass; 608 ft^2 is carried from the R2 transcription.";

        public const float Aro10ReferenceSpanFt = 42.8f;
        public const float Aro10ReferenceChordFt = 15.94f;
        public const float Aro10ReferenceAreaFt2 = 608f;
        public const float Aro10MomentReferenceFractionCbar = 0.2565f;

        // ---------------------------------------------------------------- candidates

        /// <summary>Every candidate examined, in audit order. A fresh array on each read.</summary>
        public static MavF15GeometryCandidate[] All
        {
            get
            {
                return new MavF15GeometryCandidate[]
                {
                    // --- NASA 836 itself
                    Candidate(
                        "NASA836_PHYSICAL_SPAN", MavF15GeometryQuantity.PhysicalWingSpan,
                        Nasa836PhysicalWingSpanFt, "ft", Nasa836PhysicalWingSpanFt * FootToM,
                        "NASA836_OVERALL_DIMENSIONS", Nasa836Airframe,
                        "NASA TM-4782 p.10; NASA/TM-2005-213670 p.6; NASA/TM-2006-213674 p.12; "
                        + "AIAA 2001-3303 p.6; NASA/TM-2006-213675 p.18; 836 identity via "
                        + "NASA/TM-2008-214634 p.6.",
                        MavF15GeometryAuthority.DirectExact836,
                        "Stated for the 836 airframe, as an overall dimension beside length and "
                        + "height. Physical geometry: exact, and still not a coefficient "
                        + "reference span."),

                    Candidate(
                        "NASA836_S", MavF15GeometryQuantity.CoefficientReferenceArea,
                        0f, "ft^2", 0f, null, Nasa836Airframe,
                        "Searched: TM-2012-215978, TM-2009-214651, TM-2005-213670, "
                        + "TM-2006-213674, NTRS 20070032807, TM-2008-214634, TM-4782 and "
                        + "further 836 reports - see the audit document.",
                        MavF15GeometryAuthority.Unavailable,
                        "No 836 source prints a reference area, and none names the reference "
                        + "dimensions of the baseline aerodynamic model it updates."),

                    Candidate(
                        "NASA836_CBAR", MavF15GeometryQuantity.CoefficientReferenceChord,
                        0f, "ft", 0f, null, Nasa836Airframe,
                        "As NASA836_S. TM-2012-215978 p.6 defines 'MAC' only as a symbol.",
                        MavF15GeometryAuthority.Unavailable,
                        "MAC is used as the unit of CG position (26.34 % MAC) but its length is "
                        + "never printed."),

                    Candidate(
                        "NASA836_B_REFERENCE", MavF15GeometryQuantity.CoefficientReferenceSpan,
                        0f, "ft", 0f, null, Nasa836Airframe,
                        "As NASA836_S.",
                        MavF15GeometryAuthority.Unavailable,
                        "Only the physical span is printed; no source states the span the "
                        + "baseline model normalizes p, r, Cl and Cn by."),

                    Candidate(
                        "NASA836_MOMENT_REFERENCE", MavF15GeometryQuantity.MomentReferenceLocation,
                        0f, "", 0f, null, Nasa836Airframe,
                        "As NASA836_S.",
                        MavF15GeometryAuthority.Unavailable,
                        "No moment reference location is printed for the 836 model."),

                    Candidate(
                        "NASA836_TABLE1_CG", MavF15GeometryQuantity.CenterOfGravityLocation,
                        MavF15MassReference.XcgPercentMac, "% MAC", 0f, null, Nasa836Airframe,
                        "NASA/TM-2012-215978 p.8 table 1, baseline column.",
                        MavF15GeometryAuthority.DirectExact836,
                        "A CG location, exact for the mass state. Not a moment reference, and "
                        + "not locatable in length units without cbar and the leading-edge MAC "
                        + "station."),

                    Candidate(
                        "NASA836_PFTF_CG", MavF15GeometryQuantity.CenterOfGravityLocation,
                        Nasa836PftfAnalysisCgFuselageStationIn, "in (FS), at 28 % MAC", 0f, null,
                        Nasa836Airframe,
                        "AIAA 2001-3303 p.12; PFTF on tail 836 per NTRS 20100001729 p.1.",
                        MavF15GeometryAuthority.DirectExact836,
                        "The CG of one CFD analysis. One equation in two unknowns - it does not "
                        + "determine cbar - and it is not a moment reference."),

                    // --- NF-15B 837: the table the familiar numbers come from
                    Candidate(
                        "NF15B837_S", MavF15GeometryQuantity.CoefficientReferenceArea,
                        Nf15b837ReferenceAreaFt2, "ft^2", Nf15b837ReferenceAreaFt2 * SquareFootToM2,
                        Nf15b837ReferenceSetId, Nf15b837Airframe, Nf15b837Citation,
                        MavF15GeometryAuthority.Incompatible,
                        "A genuine coefficient reference area - for a different airframe and "
                        + "aerodynamic model: preproduction, canards, F100-PW-229, TV nozzles."),

                    Candidate(
                        "NF15B837_CBAR", MavF15GeometryQuantity.CoefficientReferenceChord,
                        Nf15b837ReferenceChordFt, "ft", Nf15b837ReferenceChordFt * FootToM,
                        Nf15b837ReferenceSetId, Nf15b837Airframe, Nf15b837Citation,
                        MavF15GeometryAuthority.Incompatible,
                        "As NF15B837_S."),

                    Candidate(
                        "NF15B837_B_REFERENCE", MavF15GeometryQuantity.CoefficientReferenceSpan,
                        Nf15b837ReferenceSpanFt, "ft", Nf15b837ReferenceSpanFt * FootToM,
                        Nf15b837ReferenceSetId, Nf15b837Airframe, Nf15b837Citation,
                        MavF15GeometryAuthority.Incompatible,
                        "42.7 ft - neither the 42.8 ft printed for 836 nor the 42.83 ft on 837's "
                        + "own three-view. Reference spans are model-specific."),

                    Candidate(
                        "NF15B837_PHYSICAL_SPAN", MavF15GeometryQuantity.PhysicalWingSpan,
                        Nf15b837PhysicalWingSpanFt, "ft", Nf15b837PhysicalWingSpanFt * FootToM,
                        "NF15B_837_NASA_TM_2003_212027_FIGURE_2", Nf15b837Airframe,
                        "NASA/TM-2003-212027 p.23 figure 2.",
                        MavF15GeometryAuthority.Incompatible,
                        "Physical, and a different airframe."),

                    Candidate(
                        "NF15B837_MOMENT_REFERENCE",
                        MavF15GeometryQuantity.MomentReferenceLocation,
                        Nf15b837MomentReferenceFsIn, "in (FS 557.2, WL 116.3, BL 0.0)", 0f,
                        Nf15b837ReferenceSetId, Nf15b837Airframe, Nf15b837Citation,
                        MavF15GeometryAuthority.Incompatible,
                        "Different airframe. Printed beside a CG of FS 560.40 - not the CG."),

                    // --- ARO10 production-aerobase lineage
                    Candidate(
                        "ARO10_S", MavF15GeometryQuantity.CoefficientReferenceArea,
                        Aro10ReferenceAreaFt2, "ft^2", Aro10ReferenceAreaFt2 * SquareFootToM2,
                        Aro10ReferenceSetId, Aro10Airframe,
                        Aro10Citation + " " + Aro10AreaProvenanceNote,
                        MavF15GeometryAuthority.F15FamilySupport,
                        "Production F-15 aerobase lineage. No source says NASA 836's baseline "
                        + "model uses it."),

                    Candidate(
                        "ARO10_CBAR", MavF15GeometryQuantity.CoefficientReferenceChord,
                        Aro10ReferenceChordFt, "ft", Aro10ReferenceChordFt * FootToM,
                        Aro10ReferenceSetId, Aro10Airframe, Aro10Citation,
                        MavF15GeometryAuthority.F15FamilySupport,
                        "As ARO10_S."),

                    Candidate(
                        "ARO10_B_REFERENCE", MavF15GeometryQuantity.CoefficientReferenceSpan,
                        Aro10ReferenceSpanFt, "ft", Aro10ReferenceSpanFt * FootToM,
                        Aro10ReferenceSetId, Aro10Airframe, Aro10Citation,
                        MavF15GeometryAuthority.F15FamilySupport,
                        "Numerically equal to 836's physical span, which proves nothing about "
                        + "836's model: NF-15B 837 uses 42.7 ft."),

                    Candidate(
                        "ARO10_MOMENT_REFERENCE", MavF15GeometryQuantity.MomentReferenceLocation,
                        Aro10MomentReferenceFractionCbar, "fraction of cbar", 0f,
                        Aro10ReferenceSetId, Aro10Airframe, Aro10Citation,
                        MavF15GeometryAuthority.F15FamilySupport,
                        "The ARO10 listing calls these 'CG locations' the data was referenced "
                        + "to - a moment reference, for the production aerobase."),

                    // --- the only cross-check between 836 and the family geometry
                    Candidate(
                        "NASA836_PFTF_CG_FAMILY_CONSISTENCY",
                        MavF15GeometryQuantity.CenterOfGravityLocation,
                        Nasa836PftfAnalysisCgFuselageStationIn, "in (FS)", 0f, null,
                        "NASA 836 statement checked against NF-15B 837 and ARO10 values",
                        "AIAA 2001-3303 p.12 with NASA/TM-2003-212027 table 1 and the ARO10 "
                        + "listing p.123.",
                        MavF15GeometryAuthority.CrossValidationOnly,
                        "If 837's moment reference FS 557.2 sits at ARO10's 25.65 % of a "
                        + "15.94-ft MAC, the leading-edge MAC is FS 508.14; 836's 28 % MAC at "
                        + "FS 561.7 then implies FS 508.14 as well. Consistent to 0.01 in - and "
                        + "still not a derivation: both premises are assumptions, and any "
                        + "cbar satisfies 836's single equation with a suitable leading edge.")
                };
            }
        }

        // ---------------------------------------------------------------- selection

        /// <summary>
        /// The exact-target coefficient reference set, if the held sources close it. Today they
        /// do not, and this returns false with the reason.
        /// </summary>
        public static bool TrySelectExactTargetReferenceSet(
            out MavF15CoefficientReferenceSet set, out string reason)
        {
            return TrySelectReferenceSet(All, out set, out reason);
        }

        /// <summary>
        /// Assembles S, cbar and b from the given candidates, or refuses.
        ///
        /// Refuses unless ONE reference-set id supplies all three coefficient-reference
        /// quantities with accepted authority. A physical span never qualifies, a family or
        /// incompatible value never qualifies, and values from two different printed sets are
        /// never mixed - dimensionalizing Cl with one model's span and Cm with another model's
        /// chord is not a reference set at all.
        /// </summary>
        public static bool TrySelectReferenceSet(
            MavF15GeometryCandidate[] candidates,
            out MavF15CoefficientReferenceSet set,
            out string reason)
        {
            set = new MavF15CoefficientReferenceSet();

            if (candidates == null || candidates.Length == 0)
            {
                reason = "no candidates";
                return false;
            }

            string chosenSet = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].MayDimensionalizeExactTargetCoefficients)
                    continue;

                if (chosenSet == null)
                {
                    chosenSet = candidates[i].referenceSetId;
                }
                else if (chosenSet != candidates[i].referenceSetId)
                {
                    reason = "accepted coefficient-reference values come from two different "
                        + "reference sets ('" + chosenSet + "' and '"
                        + candidates[i].referenceSetId + "'); they cannot be mixed";
                    return false;
                }
            }

            if (chosenSet == null)
            {
                reason = "no coefficient-reference candidate has exact-target authority "
                    + "(DirectExact836 or ExplicitProductionReferenceUsedBy836Model); S, cbar "
                    + "and reference b remain UNAVAILABLE for NASA 836";
                return false;
            }

            float area = 0f, chord = 0f, span = 0f;
            int areaCount = 0, chordCount = 0, spanCount = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                MavF15GeometryCandidate c = candidates[i];
                if (!c.MayDimensionalizeExactTargetCoefficients || c.referenceSetId != chosenSet)
                    continue;

                if (c.quantity == MavF15GeometryQuantity.CoefficientReferenceArea)
                {
                    area = c.siValue;
                    areaCount++;
                }
                else if (c.quantity == MavF15GeometryQuantity.CoefficientReferenceChord)
                {
                    chord = c.siValue;
                    chordCount++;
                }
                else if (c.quantity == MavF15GeometryQuantity.CoefficientReferenceSpan)
                {
                    span = c.siValue;
                    spanCount++;
                }
            }

            if (areaCount != 1 || chordCount != 1 || spanCount != 1)
            {
                reason = "reference set '" + chosenSet + "' does not supply exactly one S, one "
                    + "cbar and one reference b with accepted authority (S=" + areaCount
                    + ", cbar=" + chordCount + ", b=" + spanCount + ")";
                return false;
            }

            set.referenceSetId = chosenSet;
            set.areaM2 = area;
            set.chordM = chord;
            set.spanM = span;
            reason = "OK";
            return true;
        }

        /// <summary>
        /// The exact-target moment reference location, if any source closes it. Refuses every
        /// centre-of-gravity statement by construction: a CG is not a moment reference.
        /// </summary>
        public static bool TrySelectExactTargetMomentReference(
            out MavF15GeometryCandidate momentReference, out string reason)
        {
            return TrySelectMomentReference(All, out momentReference, out reason);
        }

        public static bool TrySelectMomentReference(
            MavF15GeometryCandidate[] candidates,
            out MavF15GeometryCandidate momentReference,
            out string reason)
        {
            momentReference = new MavF15GeometryCandidate();

            if (candidates != null)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    MavF15GeometryCandidate c = candidates[i];
                    if (c.quantity == MavF15GeometryQuantity.MomentReferenceLocation
                        && c.AuthorityAcceptedForExactTarget)
                    {
                        momentReference = c;
                        reason = "OK";
                        return true;
                    }
                }
            }

            reason = "no moment reference location has exact-target authority; centre-of-"
                + "gravity statements are not accepted in its place";
            return false;
        }

        private static MavF15GeometryCandidate Candidate(
            string id, MavF15GeometryQuantity quantity,
            float sourceValue, string sourceUnits, float siValue,
            string referenceSetId, string airframe, string citation,
            MavF15GeometryAuthority authority, string reason)
        {
            MavF15GeometryCandidate c = new MavF15GeometryCandidate();
            c.id = id;
            c.quantity = quantity;
            c.sourceValue = sourceValue;
            c.sourceUnits = sourceUnits;
            c.siValue = siValue;
            c.referenceSetId = referenceSetId;
            c.airframe = airframe;
            c.citation = citation;
            c.authority = authority;
            c.reason = reason;
            return c;
        }
    }
}
