namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Independent implementation of the compact global nonlinear polynomial
    /// F-16 aerodynamic model published by Eugene A. Morelli.
    ///
    /// Inputs are radians and nondimensional body rates p-hat/q-hat/r-hat.
    /// This class is deliberately pure: no Rigidbody access and no Unity forces.
    /// </summary>
    public static class MavF16MorelliPolynomial
    {
        public static MavAeroCoefficients Evaluate(
            float alphaRad,
            float betaRad,
            float elevatorRad,
            float aileronRad,
            float rudderRad,
            float pHat,
            float qHat,
            float rHat,
            float xCgCbar,
            float xCgReferenceCbar,
            float chordOverSpan)
        {
            double a = alphaRad;
            double b = betaRad;
            double de = elevatorRad;
            double da = aileronRad;
            double dr = rudderRad;

            double a2 = a * a;
            double a3 = a2 * a;
            double a4 = a3 * a;
            double a5 = a4 * a;
            double b2 = b * b;
            double b3 = b2 * b;
            double de2 = de * de;
            double de3 = de2 * de;

            double cx0 =
                -1.943367e-02
                + 2.136104e-01 * a
                - 2.903457e-01 * de2
                - 3.348641e-03 * de
                - 2.060504e-01 * a * de
                + 6.988016e-01 * a2
                - 9.035381e-01 * a3;

            double cxq =
                4.833383e-01
                + 8.644627e+00 * a
                + 1.131098e+01 * a2
                - 7.422961e+01 * a3
                + 6.075776e+01 * a4;

            double cy0 =
                -1.145916e+00 * b
                + 6.016057e-02 * da
                + 1.642479e-01 * dr;

            double cyp =
                -1.006733e-01
                + 8.679799e-01 * a
                + 4.260586e+00 * a2
                - 6.923267e+00 * a3;

            double cyr =
                8.071648e-01
                + 1.189633e-01 * a
                + 4.177702e+00 * a2
                - 9.162236e+00 * a3;

            double cz0 =
                (-1.378278e-01
                 - 4.211369e+00 * a
                 + 4.775187e+00 * a2
                 - 1.026225e+01 * a3
                 + 8.399763e+00 * a4)
                * (1.0 - b2)
                - 4.354000e-01 * de;

            double czq =
                -3.054956e+01
                - 4.132305e+01 * a
                + 3.292788e+02 * a2
                - 6.848038e+02 * a3
                + 4.080244e+02 * a4;

            double cl0 =
                -1.058583e-01 * b
                - 5.776677e-01 * a * b
                - 1.672435e-02 * a2 * b
                + 1.357256e-01 * b2
                + 2.172952e-01 * a * b2
                + 3.464156e+00 * a3 * b
                - 2.835451e+00 * a4 * b
                - 1.098104e+00 * a2 * b2;

            double clp =
                -4.126806e-01
                - 1.189974e-01 * a
                + 1.247721e+00 * a2
                - 7.391132e-01 * a3;

            double clr =
                6.250437e-02
                + 6.067723e-01 * a
                - 1.101964e+00 * a2
                + 9.100087e+00 * a3
                - 1.192672e+01 * a4;

            double clDa =
                -1.463144e-01
                - 4.073901e-02 * a
                + 3.253159e-02 * b
                + 4.851209e-01 * a2
                + 2.978850e-01 * a * b
                - 3.746393e-01 * a2 * b
                - 3.213068e-01 * a3;

            double clDr =
                2.635729e-02
                - 2.192910e-02 * a
                - 3.152901e-03 * b
                - 5.817803e-02 * a * b
                + 4.516159e-01 * a2 * b
                - 4.928702e-01 * a3 * b
                - 1.579864e-02 * b2;

            double cm0 =
                -2.029370e-02
                + 4.660702e-02 * a
                - 6.012308e-01 * de
                - 8.062977e-02 * a * de
                + 8.320429e-02 * de2
                + 5.018538e-01 * a2 * de
                + 6.378864e-01 * de3
                + 4.226356e-01 * a * de2;

            double cmq =
                -5.159153e+00
                - 3.554716e+00 * a
                - 3.598636e+01 * a2
                + 2.247355e+02 * a3
                - 4.120991e+02 * a4
                + 2.411750e+02 * a5;

            double cn0 =
                2.993363e-01 * b
                + 6.594004e-02 * a * b
                - 2.003125e-01 * b2
                - 6.233977e-02 * a * b2
                - 2.107885e+00 * a2 * b
                + 2.141420e+00 * a2 * b2
                + 8.476901e-01 * a3 * b;

            double cnp =
                2.677652e-02
                - 3.298246e-01 * a
                + 1.926178e-01 * a2
                + 4.013325e+00 * a3
                - 4.404302e+00 * a4;

            double cnr =
                -3.698756e-01
                - 1.167551e-01 * a
                - 7.641297e-01 * a2;

            double cnDa =
                -3.348717e-02
                + 4.276655e-02 * a
                + 6.573646e-03 * b
                + 3.535831e-01 * a * b
                - 1.373308e+00 * a2 * b
                + 1.237582e+00 * a3 * b
                + 2.302543e-01 * a2
                - 2.512876e-01 * a3
                + 1.588105e-01 * b3
                - 5.199526e-01 * a * b3;

            double cnDr =
                -8.115894e-02
                - 1.156580e-02 * a
                + 2.514167e-02 * b
                + 2.038748e-01 * a * b
                - 3.337476e-01 * a2 * b
                + 1.004297e-01 * a2;

            double cx = cx0 + cxq * qHat;
            double cy = cy0 + cyp * pHat + cyr * rHat;
            double cz = cz0 + czq * qHat;

            double cl = cl0 + clp * pHat + clr * rHat + clDa * da + clDr * dr;

            double cgDelta = xCgReferenceCbar - xCgCbar;
            double cm = cm0 + cmq * qHat + cz * cgDelta;
            double cn = cn0 + cnp * pHat + cnr * rHat + cnDa * da + cnDr * dr
                        - cy * cgDelta * chordOverSpan;

            MavAeroCoefficients result = new MavAeroCoefficients();
            result.cx = (float)cx;
            result.cy = (float)cy;
            result.cz = (float)cz;
            result.cl = (float)cl;
            result.cm = (float)cm;
            result.cn = (float)cn;
            return result;
        }
    }
}
