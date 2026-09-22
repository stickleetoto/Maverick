using System;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Lateral-directional transcription of the AFIT/Baumann F-15 coefficient
    /// routine at the fixed Mach 0.6 / 20,000 ft research condition.
    ///
    /// Source: Davison, AFIT/GAE/ENY/92M-01, Appendix C COEFF routine.
    ///
    /// Implemented source channels:
    /// - CY basic beta dependence, p/r derivatives, aileron, rudder and differential tail
    /// - Cl basic beta dependence, p/r derivatives, aileron, rudder and differential tail
    /// - Cn basic beta dependence, p/r derivatives, aileron, rudder and differential tail
    /// - F-15B two-place-canopy roll/yaw increments
    /// - high-alpha asymmetric CY and Cn increments
    /// - source beta sign-smoothing functions used by the continuation model
    ///
    /// Speedbrake/store increments are omitted exactly as in the clean research use case.
    /// EPA43 is therefore 1.0. The routine returns coefficients only and never applies
    /// Rigidbody loads.
    ///
    /// This is CROSS-VALIDATION / RESEARCH data, not exact NASA 836 target authority.
    /// </summary>
    public static class MavF15BaumannMach06LateralDirectional
    {
        private const double DegPerRad = 57.2957795131;

        private const double DifferentialTailFlex = 0.975;
        private const double SideForceRudderFlex = 0.89;
        private const double RollRudderFlex = 0.85;
        private const double YawRudderFlex = 0.89;

        public static MavAeroCoefficients Evaluate(
            float alphaRad,
            float betaRad,
            MavF15BaumannSurfaceState surface,
            float pHat,
            float rHat)
        {
            double ral = alphaRad;
            double betaDeg = betaRad * DegPerRad;
            double rbeta = betaRad;
            double rabet = Math.Abs(rbeta);

            double aileronDeg = surface.aileronDeg;
            double differentialTailDeg = surface.differentialTailDeg;
            double rudderDeg = surface.rudderDeg;
            double daila = Math.Abs(aileronDeg);
            double rarud = Math.Abs(rudderDeg / DegPerRad);
            double dstbr = surface.symmetricStabilatorDeg / DegPerRad;

            double epa02Small = BetaSignSmall(betaDeg);
            double epa02Large = BetaSignLarge(betaDeg);

            double cfy1 =
                -0.05060386
                - (0.12342073 * ral)
                + (1.04501136 * ral * ral)
                - (0.17239516 * Pow(ral, 3))
                - (2.90979277 * Pow(ral, 4))
                + (3.06782935 * Pow(ral, 5))
                - (0.88422116 * Pow(ral, 6))
                - (0.06578812 * ral * rabet)
                - (0.71521988 * rabet)
                - (0.00000475273 * rabet * rabet)
                - (0.04856168 * ral * dstbr)
                - (0.05943607 * rabet * dstbr)
                + (0.02018534 * dstbr);

            double cfyp = SideForceRollRateDerivative(ral);
            double cfyr = SideForceYawRateDerivative(ral);
            double cydad = SideForceAileronDerivative(ral);
            double cydrd = SideForceRudderDerivative(ral, rarud);
            double cydtd = SideForceDifferentialTailDerivative(ral, dstbr);
            double cyrb = HighAlphaAsymmetricSideForce(ral, rbeta);

            double cy =
                (cfy1 * epa02Large)
                + (cydad * aileronDeg)
                + (cydrd * rudderDeg * SideForceRudderFlex)
                + (cydtd * DifferentialTailFlex * differentialTailDeg)
                + (cfyp * pHat)
                + (cfyr * rHat)
                + cyrb;

            double cml1 =
                -0.00238235
                - (0.04616235 * ral)
                + (0.10553168 * ral * ral)
                + (0.10541585 * Pow(ral, 3))
                - (0.40254765 * Pow(ral, 4))
                + (0.32530491 * Pow(ral, 5))
                - (0.08496121 * Pow(ral, 6))
                + (0.00112288 * Pow(ral, 7))
                - (0.05940477 * rabet * ral)
                - (0.07356236 * rabet)
                - (0.00550119 * rabet * rabet)
                + (0.00326191 * Pow(rabet, 3));

            double cmlp = RollDampingDerivative(ral);
            double cmlr = RollingMomentYawRateDerivative(ral);
            double cldad = RollingMomentAileronDerivative(ral, dstbr);
            double cldrd = RollingMomentRudderDerivative(ral, rarud);
            double cldtd = RollingMomentDifferentialTailDerivative(ral, dstbr);
            double dclb = F15BCanopyRollingMomentIncrement(ral);

            double cl =
                (cml1 * epa02Small)
                + (cldad * aileronDeg)
                + (cldrd * rudderDeg * RollRudderFlex)
                + (cldtd * DifferentialTailFlex * differentialTailDeg)
                + (cmlp * pHat)
                + (cmlr * rHat)
                + (dclb * betaDeg);

            double cmn1 =
                0.01441512
                + (0.02242944 * ral)
                - (0.30472558 * ral * ral)
                + (0.14475549 * Pow(ral, 3))
                + (0.93140112 * Pow(ral, 4))
                - (1.52168677 * Pow(ral, 5))
                + (0.90743413 * Pow(ral, 6))
                - (0.16510989 * Pow(ral, 7))
                - (0.0461968 * Pow(ral, 8))
                + (0.01754292 * Pow(ral, 9))
                - (0.17553807 * ral * rabet)
                + (0.15415649 * ral * rabet * dstbr)
                + (0.14829547 * ral * ral * rabet * rabet)
                - (0.11605031 * ral * ral * rabet * dstbr)
                - (0.06290678 * ral * ral * dstbr * dstbr)
                - (0.01404857 * ral * ral * dstbr * dstbr)
                + (0.07225609 * rabet)
                - (0.08567087 * rabet * rabet)
                + (0.01184674 * Pow(rabet, 3))
                - (0.00519152 * ral * dstbr)
                + (0.03865177 * rabet * dstbr)
                + (0.00062918 * dstbr);

            double cndrd = YawingMomentRudderDerivative(ral, rbeta, rarud, dstbr);
            double cmnp = YawingMomentRollRateDerivative(ral);
            double cmnr = YawDampingDerivative(ral);
            double cndtd = YawingMomentDifferentialTailDerivative(ral, dstbr);
            double cndad = YawingMomentAileronDerivative(ral, daila);

            const double dcnb = -0.00025;
            double cnrb = HighAlphaAsymmetricYawingMoment(ral, rbeta);

            double cn =
                (cmn1 * epa02Small)
                + (cndad * aileronDeg)
                + (cndrd * rudderDeg * YawRudderFlex)
                + (cndtd * DifferentialTailFlex * differentialTailDeg)
                + (cmnp * pHat)
                + (cmnr * rHat)
                + (dcnb * betaDeg)
                + cnrb;

            MavAeroCoefficients result = MavAeroCoefficients.Zero;
            result.cy = (float)cy;
            result.cl = (float)cl;
            result.cn = (float)cn;
            return result;
        }

        private static double BetaSignSmall(double betaDeg)
        {
            if (betaDeg <= -1.0)
                return -1.0;
            if (betaDeg >= 1.0)
                return 1.0;

            double shifted = betaDeg + 1.0;
            return
                -1.0
                + (1.5 * shifted * shifted)
                - (0.5 * shifted * shifted * shifted);
        }

        private static double BetaSignLarge(double betaDeg)
        {
            if (betaDeg <= -5.0)
                return -1.0;
            if (betaDeg >= 5.0)
                return 1.0;

            double shifted = betaDeg + 5.0;
            return
                -1.0
                + (0.06 * shifted * shifted)
                - (0.004 * shifted * shifted * shifted);
        }

        private static double SideForceRollRateDerivative(double ral)
        {
            if (ral < 0.52359998)
            {
                return
                    0.014606188
                    + (2.52405055 * ral)
                    - (5.02687473 * ral * ral)
                    - (106.43222962 * Pow(ral, 3))
                    + (256.80215423 * Pow(ral, 4))
                    + (1256.39636248 * Pow(ral, 5))
                    - (3887.92878173 * Pow(ral, 6))
                    - (2863.16083460 * Pow(ral, 7))
                    + (17382.72226362 * Pow(ral, 8))
                    - (13731.65408408 * Pow(ral, 9));
            }

            if (ral <= 0.610865)
            {
                double d = ral - 0.52359998;
                return
                    0.00236511
                    + (0.52044678 * d)
                    - (12.8597002 * d * d)
                    + (75.46138 * Pow(d, 3));
            }

            return 0.0;
        }

        private static double SideForceYawRateDerivative(double ral)
        {
            if (ral < -0.06981)
                return 0.35;

            if (ral < 0.0)
            {
                double d = ral + 0.06981;
                return
                    0.34999999
                    + (35.4012413 * d * d)
                    - (493.33441162 * Pow(d, 3));
            }

            if (ral <= 0.523599)
            {
                return
                    0.35468605
                    - (2.26998141 * ral)
                    + (51.82178387 * ral * ral)
                    - (718.55069823 * Pow(ral, 3))
                    + (4570.00492172 * Pow(ral, 4))
                    - (14471.88028351 * Pow(ral, 5))
                    + (22026.58930662 * Pow(ral, 6))
                    - (12795.99029404 * Pow(ral, 7));
            }

            if (ral <= 0.61087)
            {
                double d = ral - 0.52359903;
                return
                    0.00193787
                    + (1.78332496 * d)
                    - (41.63198853 * d * d)
                    + (239.97909546 * Pow(d, 3));
            }

            return 0.0;
        }

        private static double SideForceAileronDerivative(double ral)
        {
            if (ral < 0.55851)
            {
                return
                    -0.00020812
                    + (0.00062122 * ral)
                    + (0.00260729 * ral * ral)
                    + (0.00745739 * Pow(ral, 3))
                    - (0.03656114 * Pow(ral, 4))
                    - (0.04532683 * Pow(ral, 5))
                    + (0.20674845 * Pow(ral, 6))
                    - (0.13264434 * Pow(ral, 7))
                    - (0.00193383 * Pow(ral, 8));
            }

            if (ral < 0.61087)
            {
                double d = ral - 0.55851001;
                return
                    0.00023894
                    + (0.00195121 * d)
                    + (0.02459273 * d * d)
                    - (0.1202244 * Pow(d, 3));
            }

            return
                0.27681285
                - (2.02305395 * ral)
                + (6.01180715 * ral * ral)
                - (9.24292188 * Pow(ral, 3))
                + (7.59857819 * Pow(ral, 4))
                - (2.8565527 * Pow(ral, 5))
                + (0.25460503 * Pow(ral, 7))
                - (0.01819815 * Pow(ral, 9));
        }

        private static double SideForceRudderDerivative(double ral, double rarud)
        {
            return
                0.00310199
                + (0.00119963 * ral)
                + (0.02806933 * ral * ral)
                - (0.12408447 * Pow(ral, 3))
                - (0.12032121 * Pow(ral, 4))
                + (0.79150279 * Pow(ral, 5))
                - (0.86544347 * Pow(ral, 6))
                + (0.27845115 * Pow(ral, 7))
                + (0.00122999 * ral * rarud)
                + (0.00145943 * rarud)
                - (0.01211427 * rarud * rarud)
                + (0.00977937 * Pow(rarud, 3));
        }

        private static double SideForceDifferentialTailDerivative(
            double ral,
            double dstbr)
        {
            return
                -0.00157745
                - (0.0020881 * ral)
                + (0.00557239 * ral * ral)
                - (0.00139886 * Pow(ral, 3))
                + (0.04956247 * Pow(ral, 4))
                - (0.0135353 * Pow(ral, 5))
                - (0.11552397 * Pow(ral, 6))
                + (0.11443452 * Pow(ral, 7))
                - (0.03072189 * Pow(ral, 8))
                - (0.01061113 * Pow(ral, 3) * dstbr)
                - (0.00010529 * ral * ral * dstbr * dstbr)
                - (0.00572463 * ral * dstbr * dstbr)
                + (0.01885361 * ral * ral * dstbr)
                - (0.01412258 * ral * Pow(dstbr, 3))
                - (0.00081776 * dstbr)
                + (0.00404354 * dstbr * dstbr)
                + (0.00212189 * Pow(dstbr, 3))
                + (0.00655063 * Pow(dstbr, 4))
                + (0.03341584 * Pow(dstbr, 5));
        }

        private static double HighAlphaAsymmetricSideForce(double ral, double rbeta)
        {
            const double alphaMin = 0.6108652;
            double alphaMax = 90.0 / DegPerRad;
            const double betaMin = -0.0872665;
            const double betaMax = 0.1745329;

            if (ral < alphaMin || rbeta < betaMin || rbeta > betaMax)
                return 0.0;

            const double amplitude = 0.164;
            const double alphaStar = 0.95993;
            const double betaStar = 0.087266;

            return
                amplitude
                * CompactSupportShape(ral, alphaMin, alphaMax, alphaStar)
                * CompactSupportShape(rbeta, betaMin, betaMax, betaStar);
        }

        private static double RollDampingDerivative(double ral)
        {
            if (ral < 0.29671)
            {
                return
                    -0.24963201
                    - (0.03106297 * ral)
                    + (0.12430631 * ral * ral)
                    - (8.95274618 * Pow(ral, 3))
                    + (100.33109929 * Pow(ral, 4))
                    + (275.70069578 * Pow(ral, 5))
                    - (1178.83425699 * Pow(ral, 6))
                    - (2102.66811522 * Pow(ral, 7))
                    + (2274.89785551 * Pow(ral, 8));
            }

            if (ral < 0.34907)
            {
                double d = ral - 0.29671001;
                return
                    -0.1635261
                    - (3.77847099 * d)
                    + (147.47639465 * d * d)
                    - (1295.94799805 * Pow(d, 3));
            }

            return
                -1.37120291
                + (7.06112181 * ral)
                - (13.57010422 * ral * ral)
                + (11.21323850 * Pow(ral, 3))
                - (4.26789425 * Pow(ral, 4))
                + (0.6237381 * Pow(ral, 5));
        }

        private static double RollingMomentYawRateDerivative(double ral)
        {
            if (ral < 0.7854)
            {
                return
                    0.03515391
                    + (0.59296381 * ral)
                    + (2.27456302 * ral * ral)
                    - (3.8097803 * Pow(ral, 3))
                    - (45.83162842 * Pow(ral, 4))
                    + (55.31669213 * Pow(ral, 5))
                    + (194.29237485 * Pow(ral, 6))
                    - (393.22969953 * Pow(ral, 7))
                    + (192.20860739 * Pow(ral, 8));
            }

            if (ral <= 0.87266)
            {
                double d = ral - 0.7853999734;
                return
                    0.0925579071
                    - (0.6000000238 * d)
                    + (1.3515939713 * d * d)
                    + (29.0733299255 * Pow(d, 3));
            }

            return
                -311.126041
                + (1457.23391042 * ral)
                - (2680.19461944 * ral * ral)
                + (2361.44914738 * Pow(ral, 3))
                - (893.83567263 * Pow(ral, 4))
                + (68.23501924 * Pow(ral, 6))
                - (1.72572994 * Pow(ral, 9));
        }

        private static double RollingMomentAileronDerivative(double ral, double dstbr)
        {
            return
                0.00057626
                + (0.00038479 * ral)
                - (0.00502091 * ral * ral)
                + (0.00161407 * Pow(ral, 3))
                + (0.02268829 * Pow(ral, 4))
                - (0.03935269 * Pow(ral, 5))
                + (0.02472827 * Pow(ral, 6))
                - (0.00543345 * Pow(ral, 7))
                + (0.0000007520348 * dstbr * ral)
                + (0.000000390773 * dstbr);
        }

        private static double RollingMomentRudderDerivative(double ral, double rarud)
        {
            return
                0.00013713
                - (0.00035439 * ral)
                - (0.00227912 * ral * ral)
                + (0.00742636 * Pow(ral, 3))
                + (0.00991839 * Pow(ral, 4))
                - (0.04711846 * Pow(ral, 5))
                + (0.046124 * Pow(ral, 6))
                - (0.01379021 * Pow(ral, 7))
                + (0.00003678685 * rarud * ral)
                + (0.00001043751 * rarud)
                - (0.00015866 * rarud * rarud)
                + (0.00016133 * Pow(rarud, 3));
        }

        private static double RollingMomentDifferentialTailDerivative(
            double ral,
            double dstbr)
        {
            return
                0.00066663
                + (0.00074174 * ral)
                + (0.00285735 * ral * ral)
                - (0.02030692 * Pow(ral, 3))
                - (0.00352997 * Pow(ral, 4))
                + (0.0997962 * Pow(ral, 5))
                - (0.14591227 * Pow(ral, 6))
                + (0.08282004 * Pow(ral, 7))
                - (0.0168667 * Pow(ral, 8))
                + (0.00306142 * Pow(ral, 3) * dstbr)
                - (0.00110266 * ral * ral * dstbr * dstbr)
                + (0.00088031 * ral * dstbr * dstbr)
                - (0.00432594 * ral * ral * dstbr)
                - (0.00720141 * ral * Pow(dstbr, 3))
                - (0.00034325 * dstbr)
                + (0.00033433 * dstbr * dstbr)
                + (0.00800183 * Pow(dstbr, 3))
                - (0.00555986 * Pow(dstbr, 4))
                - (0.01841172 * Pow(dstbr, 5));
        }

        private static double F15BCanopyRollingMomentIncrement(double ral)
        {
            if (ral < 0.0)
                return -0.00006;

            if (ral <= 0.209434)
            {
                return
                    -0.00006
                    + (0.0041035078 * ral * ral)
                    - (0.0130618699 * Pow(ral, 3));
            }

            return 0.0;
        }

        private static double YawingMomentRudderDerivative(
            double ral,
            double rbeta,
            double rarud,
            double dstbr)
        {
            return
                -0.00153402
                + (0.00184982 * ral)
                - (0.0068693 * ral * ral)
                + (0.01772037 * Pow(ral, 3))
                + (0.03263787 * Pow(ral, 4))
                - (0.15157163 * Pow(ral, 5))
                + (0.18562888 * Pow(ral, 6))
                - (0.0966163 * Pow(ral, 7))
                + (0.01859168 * Pow(ral, 8))
                + (0.0002587 * ral * dstbr)
                - (0.00018546 * ral * dstbr * rbeta)
                - (0.00000517304 * rbeta)
                - (0.00102718 * ral * rbeta)
                - (0.0000689379 * rbeta * dstbr)
                - (0.00040536 * rbeta * rarud)
                - (0.00000480484 * dstbr * rarud)
                - (0.00041786 * ral * rarud)
                + (0.0000461872 * rbeta)
                + (0.00434094 * rbeta * rbeta)
                - (0.00490777 * Pow(rbeta, 3))
                + (0.000005157867 * rarud)
                + (0.00225169 * rarud * rarud)
                - (0.00208072 * Pow(rarud, 3));
        }

        private static double YawingMomentRollRateDerivative(double ral)
        {
            if (ral < 0.55851)
            {
                return
                    -0.00635409
                    - (1.14153932 * ral)
                    + (2.82119027 * ral * ral)
                    + (54.4739579 * Pow(ral, 3))
                    - (140.89527667 * Pow(ral, 4))
                    - (676.73746128 * Pow(ral, 5))
                    + (2059.18263976 * Pow(ral, 6))
                    + (1579.41664748 * Pow(ral, 7))
                    - (8933.08535712 * Pow(ral, 8))
                    + (6806.54761267 * Pow(ral, 9));
            }

            if (ral <= 0.61087)
            {
                double d = ral - 0.55851;
                return
                    -0.07023239
                    + (1.085815 * d)
                    + (8.852651 * d * d)
                    - (192.6093 * Pow(d, 3));
            }

            return
                -71.03693533
                + (491.32506715 * ral)
                - (1388.11177979 * ral * ral)
                + (2033.48621905 * Pow(ral, 3))
                - (1590.91322362 * Pow(ral, 4))
                + (567.38432316 * Pow(ral, 5))
                - (44.97702536 * Pow(ral, 7))
                + (2.8140669 * Pow(ral, 9));
        }

        private static double YawDampingDerivative(double ral)
        {
            if (ral <= -0.069813)
                return -0.2805;

            if (ral < 0.0)
            {
                double d = ral + 0.0698129982;
                return
                    -0.2804999948
                    + (35.9903717041 * d * d)
                    - (516.1574707031 * Pow(d, 3));
            }

            if (ral <= 0.78539801)
            {
                return
                    -0.28071511
                    - (2.52183924 * ral)
                    + (68.90860031 * ral * ral)
                    - (573.23100511 * Pow(ral, 3))
                    + (2009.08725005 * Pow(ral, 4))
                    - (3385.15675307 * Pow(ral, 5))
                    + (2730.49473149 * Pow(ral, 6))
                    - (848.12322034 * Pow(ral, 7));
            }

            if (ral < 0.95993102)
            {
                double d = ral - 0.78539801;
                return
                    -0.1096954
                    + (0.52893072 * d)
                    - (6.09109497 * d * d)
                    + (17.47834015 * Pow(d, 3));
            }

            return -0.11;
        }

        private static double YawingMomentDifferentialTailDerivative(
            double ral,
            double dstbr)
        {
            return
                0.00058286
                + (0.0007341 * ral)
                - (0.00746113 * ral * ral)
                - (0.00685223 * Pow(ral, 3))
                + (0.03277271 * Pow(ral, 4))
                - (0.02791456 * Pow(ral, 5))
                + (0.00732915 * Pow(ral, 6))
                + (0.00120456 * ral * dstbr)
                - (0.00168102 * dstbr)
                + (0.0006462 * dstbr * dstbr);
        }

        private static double YawingMomentAileronDerivative(double ral, double daila)
        {
            return
                0.00008228887
                - (0.00014015 * ral)
                - (0.0013493 * ral * ral)
                + (0.00020487 * Pow(ral, 3))
                + (0.00561241 * Pow(ral, 4))
                - (0.00634392 * Pow(ral, 5))
                + (0.00193323 * Pow(ral, 6))
                - (2.05815e-17 * ral * daila)
                + (3.794816e-17 * Pow(daila, 3));
        }

        private static double HighAlphaAsymmetricYawingMoment(double ral, double rbeta)
        {
            const double alphaMin = 0.69813;
            double alphaMax = 90.0 / DegPerRad;
            const double betaMin = -0.174532;
            const double betaMax = 0.34906;

            if (ral < alphaMin || rbeta < betaMin || rbeta > betaMax)
                return 0.0;

            const double amplitude = 0.034;
            const double alphaStar = 1.0472;
            const double betaStar = 0.087266;

            return
                amplitude
                * CompactSupportShape(ral, alphaMin, alphaMax, alphaStar)
                * CompactSupportShape(rbeta, betaMin, betaMax, betaStar);
        }

        private static double CompactSupportShape(
            double value,
            double lower,
            double upper,
            double star)
        {
            double starScaled =
                (2.0 * star - (lower + upper)) / (upper - lower);
            double valueScaled =
                (2.0 * value - (lower + upper)) / (upper - lower);

            double numerator =
                (5.0 * starScaled * starScaled)
                - (4.0 * starScaled * valueScaled)
                - 1.0;

            double window = Pow(valueScaled * valueScaled - 1.0, 2);
            double denominator = Pow(starScaled * starScaled - 1.0, 3);

            return numerator * window / denominator;
        }

        private static double Pow(double value, int exponent)
        {
            return Math.Pow(value, exponent);
        }
    }
}
