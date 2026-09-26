using System;
using System.Numerics;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// A small deterministic eigenvalue solver for dense real matrices, VALIDATION-SIDE ONLY.
    ///
    /// Method: diagonal balancing by powers of two (exact in floating point), Householder reduction
    /// to upper Hessenberg form, then single-shift QR iteration in complex arithmetic with the
    /// Wilkinson shift and Givens rotations until every subdiagonal entry deflates. Eigenvectors come
    /// from complex inverse iteration on the ORIGINAL matrix.
    ///
    /// Written for the 8x8 Jacobians of the research source model. No external library, no
    /// randomness: the same matrix always gives the same eigenvalues in the same order. Nothing in the
    /// runtime aircraft code may call it.
    /// </summary>
    public static class MavValidationEigenSolver
    {
        public const int MaxIterationsPerEigenvalue = 200;

        private const double MachineEpsilon = 2.220446049250313e-16;

        /// <summary>
        /// All eigenvalues of a real square matrix. The matrix is not modified. Returns false when an
        /// eigenvalue fails to deflate within <see cref="MaxIterationsPerEigenvalue"/> iterations.
        ///
        /// Eigenvalues whose imaginary part is below the QR round-off of the balanced matrix are
        /// returned as exactly real; complex ones are returned as exact conjugate pairs, the positive
        /// imaginary part first.
        /// </summary>
        public static bool Eigenvalues(double[,] a, out double[] re, out double[] im)
        {
            int n = a.GetLength(0);
            re = new double[n];
            im = new double[n];
            if (n == 0 || a.GetLength(1) != n)
                return false;

            double[,] h = (double[,])a.Clone();
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (double.IsNaN(h[i, j]) || double.IsInfinity(h[i, j]))
                        return false;
                }
            }

            Balance(h, n);
            ToHessenberg(h, n);

            Complex[,] c = new Complex[n, n];
            double norm = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    c[i, j] = new Complex(h[i, j], 0.0);
                    norm = Math.Max(norm, Math.Abs(h[i, j]));
                }
            }

            Complex[] eig = new Complex[n];
            if (!ShiftedQr(c, n, norm, eig))
                return false;

            PairConjugates(eig, n, norm, re, im);
            return true;
        }

        /// <summary>
        /// The eigenvector of <paramref name="a"/> for eigenvalue (re, im), by complex inverse
        /// iteration on the original matrix, scaled so its largest-magnitude component is exactly 1.
        /// <paramref name="relativeResidual"/> is ||A v - lambda v|| / (||A|| ||v||), max norms.
        /// </summary>
        public static bool Eigenvector(double[,] a, double re, double im,
            out double[] vectorRe, out double[] vectorIm, out double relativeResidual)
        {
            int n = a.GetLength(0);
            vectorRe = new double[n];
            vectorIm = new double[n];
            relativeResidual = double.PositiveInfinity;

            double norm = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    norm = Math.Max(norm, Math.Abs(a[i, j]));
            }

            if (norm == 0.0)
                return false;

            // A shift a hair away from the eigenvalue keeps the solve nonsingular; inverse iteration
            // converges in one or two steps because the separation from every other eigenvalue is
            // many orders larger. Kept real for a real eigenvalue so its vector stays real.
            Complex lambda = new Complex(re, im);
            double offset = 1e-10 * Math.Max(1.0, lambda.Magnitude);
            Complex shifted = lambda + new Complex(offset, 0.0);

            Complex[,] m = new Complex[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    m[i, j] = new Complex(a[i, j], 0.0);
                m[i, i] -= shifted;
            }

            int[] pivot = new int[n];
            LuFactor(m, n, pivot, norm);

            Complex[] x = new Complex[n];
            for (int i = 0; i < n; i++)
                x[i] = new Complex(1.0, 0.0);

            for (int iteration = 0; iteration < 4; iteration++)
            {
                LuSolve(m, n, pivot, x);
                Normalize(x, n);
            }

            Complex[] ax = new Complex[n];
            double residual = 0.0;
            for (int i = 0; i < n; i++)
            {
                Complex s = Complex.Zero;
                for (int j = 0; j < n; j++)
                    s += a[i, j] * x[j];
                ax[i] = s - lambda * x[i];
                residual = Math.Max(residual, ax[i].Magnitude);
            }

            for (int i = 0; i < n; i++)
            {
                vectorRe[i] = x[i].Real;
                vectorIm[i] = x[i].Imaginary;
            }

            relativeResidual = residual / norm;
            return !double.IsNaN(relativeResidual);
        }

        public static double Trace(double[,] a)
        {
            double t = 0.0;
            for (int i = 0; i < a.GetLength(0); i++)
                t += a[i, i];
            return t;
        }

        /// <summary>Determinant by LU with partial pivoting, in real arithmetic.</summary>
        public static double Determinant(double[,] a)
        {
            int n = a.GetLength(0);
            double[,] m = (double[,])a.Clone();
            double det = 1.0;
            for (int k = 0; k < n; k++)
            {
                int p = k;
                for (int i = k + 1; i < n; i++)
                {
                    if (Math.Abs(m[i, k]) > Math.Abs(m[p, k]))
                        p = i;
                }

                if (m[p, k] == 0.0)
                    return 0.0;

                if (p != k)
                {
                    for (int j = 0; j < n; j++)
                    {
                        double t = m[k, j];
                        m[k, j] = m[p, j];
                        m[p, j] = t;
                    }

                    det = -det;
                }

                det *= m[k, k];
                for (int i = k + 1; i < n; i++)
                {
                    double f = m[i, k] / m[k, k];
                    for (int j = k; j < n; j++)
                        m[i, j] -= f * m[k, j];
                }
            }

            return det;
        }

        // ---------------------------------------------------------------- internals

        /// <summary>
        /// Diagonal similarity D^-1 A D with D a power of two per row/column, so no rounding is
        /// introduced, chosen to bring each row and column norm together.
        /// </summary>
        private static void Balance(double[,] a, int n)
        {
            const double radix = 2.0;
            const double radixSquared = radix * radix;
            bool converged = false;
            int sweeps = 0;
            while (!converged && sweeps < 100)
            {
                converged = true;
                sweeps++;
                for (int i = 0; i < n; i++)
                {
                    double row = 0.0, column = 0.0;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i)
                            continue;
                        row += Math.Abs(a[i, j]);
                        column += Math.Abs(a[j, i]);
                    }

                    if (row == 0.0 || column == 0.0)
                        continue;

                    double total = row + column;
                    double f = 1.0;
                    double g = row / radix;
                    while (column < g)
                    {
                        f *= radix;
                        column *= radixSquared;
                    }

                    g = row * radix;
                    while (column > g)
                    {
                        f /= radix;
                        column /= radixSquared;
                    }

                    if ((column + row) / f < 0.95 * total)
                    {
                        converged = false;
                        double inverse = 1.0 / f;
                        for (int j = 0; j < n; j++)
                            a[i, j] *= inverse;
                        for (int j = 0; j < n; j++)
                            a[j, i] *= f;
                    }
                }
            }
        }

        /// <summary>Householder similarity to upper Hessenberg form, in place.</summary>
        private static void ToHessenberg(double[,] a, int n)
        {
            double[] v = new double[n];
            for (int k = 0; k < n - 2; k++)
            {
                double norm = 0.0;
                for (int i = k + 1; i < n; i++)
                    norm += a[i, k] * a[i, k];
                norm = Math.Sqrt(norm);
                if (norm == 0.0)
                    continue;

                double alpha = a[k + 1, k] > 0.0 ? -norm : norm;
                double vNorm = 0.0;
                for (int i = k + 1; i < n; i++)
                {
                    v[i] = a[i, k];
                    if (i == k + 1)
                        v[i] -= alpha;
                    vNorm += v[i] * v[i];
                }

                if (vNorm == 0.0)
                    continue;

                double scale = 2.0 / vNorm;

                // Left: rows k+1..n-1 of every column.
                for (int j = 0; j < n; j++)
                {
                    double s = 0.0;
                    for (int i = k + 1; i < n; i++)
                        s += v[i] * a[i, j];
                    s *= scale;
                    for (int i = k + 1; i < n; i++)
                        a[i, j] -= s * v[i];
                }

                // Right: columns k+1..n-1 of every row.
                for (int i = 0; i < n; i++)
                {
                    double s = 0.0;
                    for (int j = k + 1; j < n; j++)
                        s += a[i, j] * v[j];
                    s *= scale;
                    for (int j = k + 1; j < n; j++)
                        a[i, j] -= s * v[j];
                }

                for (int i = k + 2; i < n; i++)
                    a[i, k] = 0.0;
            }
        }

        /// <summary>Single-shift complex QR on an upper Hessenberg matrix; eigenvalues into eig.</summary>
        private static bool ShiftedQr(Complex[,] h, int n, double norm, Complex[] eig)
        {
            int hi = n - 1;
            int iterations = 0;
            Complex[] cs = new Complex[n];
            Complex[] sn = new Complex[n];

            while (hi >= 0)
            {
                if (hi == 0)
                {
                    eig[0] = h[0, 0];
                    break;
                }

                int lo = hi;
                while (lo > 0)
                {
                    double s = h[lo - 1, lo - 1].Magnitude + h[lo, lo].Magnitude;
                    if (s == 0.0)
                        s = norm;
                    if (h[lo, lo - 1].Magnitude <= MachineEpsilon * s)
                    {
                        h[lo, lo - 1] = Complex.Zero;
                        break;
                    }

                    lo--;
                }

                if (lo == hi)
                {
                    eig[hi] = h[hi, hi];
                    hi--;
                    iterations = 0;
                    continue;
                }

                iterations++;
                if (iterations > MaxIterationsPerEigenvalue)
                    return false;

                Complex shift;
                if (iterations % 11 == 0)
                {
                    // Exceptional shift, to break a rare stagnation cycle.
                    shift = h[hi, hi] + new Complex(h[hi, hi - 1].Magnitude, 0.0);
                }
                else
                {
                    Complex a = h[hi - 1, hi - 1], b = h[hi - 1, hi], c = h[hi, hi - 1], d = h[hi, hi];
                    Complex half = 0.5 * (a - d);
                    Complex root = Complex.Sqrt(half * half + b * c);
                    Complex mean = 0.5 * (a + d);
                    Complex mu1 = mean + root, mu2 = mean - root;
                    shift = (mu1 - d).Magnitude <= (mu2 - d).Magnitude ? mu1 : mu2;
                }

                for (int k = lo; k <= hi; k++)
                    h[k, k] -= shift;

                // H - shift = QR: Givens rotations from the left zero the subdiagonal.
                for (int k = lo; k < hi; k++)
                {
                    Complex x = h[k, k], y = h[k + 1, k];
                    double r = Math.Sqrt(x.Magnitude * x.Magnitude + y.Magnitude * y.Magnitude);
                    Complex c = r == 0.0 ? Complex.One : x / r;
                    Complex s = r == 0.0 ? Complex.Zero : y / r;
                    cs[k] = c;
                    sn[k] = s;
                    for (int j = k; j <= hi; j++)
                    {
                        Complex u = h[k, j], w = h[k + 1, j];
                        h[k, j] = Complex.Conjugate(c) * u + Complex.Conjugate(s) * w;
                        h[k + 1, j] = -s * u + c * w;
                    }
                }

                // RQ: the same rotations from the right.
                for (int k = lo; k < hi; k++)
                {
                    Complex c = cs[k], s = sn[k];
                    int last = Math.Min(k + 1, hi);
                    for (int i = lo; i <= last; i++)
                    {
                        Complex u = h[i, k], w = h[i, k + 1];
                        h[i, k] = u * c + w * s;
                        h[i, k + 1] = -u * Complex.Conjugate(s) + w * Complex.Conjugate(c);
                    }
                }

                for (int k = lo; k <= hi; k++)
                    h[k, k] += shift;
            }

            return true;
        }

        /// <summary>
        /// A real matrix has real eigenvalues and conjugate pairs. The complex QR returns them with
        /// round-off in the imaginary parts; this snaps round-off to exact real values and makes the
        /// pairs exact conjugates, ordered by descending real part, then positive imaginary first.
        /// </summary>
        private static void PairConjugates(Complex[] eig, int n, double norm, double[] re, double[] im)
        {
            double realSnap = 1e3 * MachineEpsilon * Math.Max(norm, 1e-300);
            Complex[] e = (Complex[])eig.Clone();
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(e[i].Imaginary) <= realSnap)
                    e[i] = new Complex(e[i].Real, 0.0);
            }

            bool[] used = new bool[n];
            Complex[] ordered = new Complex[n];
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (used[i])
                    continue;
                used[i] = true;
                if (e[i].Imaginary == 0.0)
                {
                    ordered[count++] = e[i];
                    continue;
                }

                int partner = -1;
                double best = double.PositiveInfinity;
                for (int j = 0; j < n; j++)
                {
                    if (used[j] || e[j].Imaginary == 0.0)
                        continue;
                    double distance = (e[j] - Complex.Conjugate(e[i])).Magnitude;
                    if (distance < best)
                    {
                        best = distance;
                        partner = j;
                    }
                }

                if (partner < 0)
                {
                    ordered[count++] = new Complex(e[i].Real, 0.0);
                    continue;
                }

                used[partner] = true;
                double pr = 0.5 * (e[i].Real + e[partner].Real);
                double pi = 0.5 * (Math.Abs(e[i].Imaginary) + Math.Abs(e[partner].Imaginary));
                ordered[count++] = new Complex(pr, pi);
                ordered[count++] = new Complex(pr, -pi);
            }

            // Stable insertion sort: descending real part, then positive imaginary first.
            for (int i = 1; i < count; i++)
            {
                Complex key = ordered[i];
                int j = i - 1;
                while (j >= 0 && Before(key, ordered[j]))
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }

                ordered[j + 1] = key;
            }

            for (int i = 0; i < n; i++)
            {
                re[i] = ordered[i].Real;
                im[i] = ordered[i].Imaginary;
            }
        }

        private static bool Before(Complex a, Complex b)
        {
            if (a.Real != b.Real)
                return a.Real > b.Real;
            return a.Imaginary > b.Imaginary;
        }

        private static void LuFactor(Complex[,] m, int n, int[] pivot, double norm)
        {
            double tiny = MachineEpsilon * norm;
            for (int k = 0; k < n; k++)
            {
                int p = k;
                for (int i = k + 1; i < n; i++)
                {
                    if (m[i, k].Magnitude > m[p, k].Magnitude)
                        p = i;
                }

                pivot[k] = p;
                if (p != k)
                {
                    for (int j = 0; j < n; j++)
                    {
                        Complex t = m[k, j];
                        m[k, j] = m[p, j];
                        m[p, j] = t;
                    }
                }

                if (m[k, k].Magnitude < tiny)
                    m[k, k] = new Complex(tiny, 0.0);

                for (int i = k + 1; i < n; i++)
                {
                    m[i, k] /= m[k, k];
                    for (int j = k + 1; j < n; j++)
                        m[i, j] -= m[i, k] * m[k, j];
                }
            }
        }

        private static void LuSolve(Complex[,] m, int n, int[] pivot, Complex[] x)
        {
            for (int k = 0; k < n; k++)
            {
                if (pivot[k] != k)
                {
                    Complex t = x[k];
                    x[k] = x[pivot[k]];
                    x[pivot[k]] = t;
                }
            }

            for (int i = 1; i < n; i++)
            {
                for (int j = 0; j < i; j++)
                    x[i] -= m[i, j] * x[j];
            }

            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = i + 1; j < n; j++)
                    x[i] -= m[i, j] * x[j];
                x[i] /= m[i, i];
            }
        }

        /// <summary>Scale so the largest-magnitude component is exactly 1 (fixes the arbitrary phase).</summary>
        private static void Normalize(Complex[] x, int n)
        {
            int largest = 0;
            for (int i = 1; i < n; i++)
            {
                if (x[i].Magnitude > x[largest].Magnitude)
                    largest = i;
            }

            Complex d = x[largest];
            if (d == Complex.Zero)
                return;
            for (int i = 0; i < n; i++)
                x[i] /= d;
        }
    }
}
