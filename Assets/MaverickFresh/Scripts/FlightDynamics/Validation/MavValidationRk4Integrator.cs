using System;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>x-dot = f(x). Returns false when f cannot be evaluated at x.</summary>
    public delegate bool MavValidationDerivative(double[] x, double[] xDot);

    /// <summary>A sampled fixed-step trajectory: time[k] and state[k] every sampleEvery steps, t = 0 first.</summary>
    public sealed class MavValidationTrajectory
    {
        public bool completed;
        public string reason;
        public double dt;
        public int steps;
        public int sampleEvery;
        public double[] time;
        public double[][] state;
    }

    /// <summary>
    /// Classical fixed-step fourth-order Runge-Kutta, VALIDATION-SIDE ONLY.
    ///
    /// Operates on a pure x-dot = f(x) and nothing else: no Unity Time, no Rigidbody, no Transform,
    /// no MonoBehaviour. Deterministic: the same f, x0, dt and step count always give the same
    /// samples. Time is n * dt, never accumulated, so every sample time is exact.
    /// </summary>
    public static class MavValidationRk4Integrator
    {
        public static MavValidationTrajectory Integrate(
            MavValidationDerivative f, double[] x0, double dt, int steps, int sampleEvery)
        {
            int n = x0.Length;
            int samples = steps / sampleEvery + 1;
            MavValidationTrajectory t = new MavValidationTrajectory
            {
                dt = dt,
                steps = steps,
                sampleEvery = sampleEvery,
                time = new double[samples],
                state = new double[samples][]
            };

            if (!(dt > 0.0) || steps < 0 || sampleEvery < 1)
            {
                t.reason = "dt must be positive, steps non-negative, sampleEvery at least 1";
                return t;
            }

            double[] x = (double[])x0.Clone();
            double[] k1 = new double[n], k2 = new double[n], k3 = new double[n], k4 = new double[n], y = new double[n];
            t.time[0] = 0.0;
            t.state[0] = (double[])x.Clone();
            int sample = 1;
            double half = 0.5 * dt, sixth = dt / 6.0;

            for (int s = 1; s <= steps; s++)
            {
                if (!f(x, k1))
                    return Fail(t, s, sample);
                for (int i = 0; i < n; i++)
                    y[i] = x[i] + half * k1[i];
                if (!f(y, k2))
                    return Fail(t, s, sample);
                for (int i = 0; i < n; i++)
                    y[i] = x[i] + half * k2[i];
                if (!f(y, k3))
                    return Fail(t, s, sample);
                for (int i = 0; i < n; i++)
                    y[i] = x[i] + dt * k3[i];
                if (!f(y, k4))
                    return Fail(t, s, sample);
                for (int i = 0; i < n; i++)
                    x[i] += sixth * (k1[i] + 2.0 * k2[i] + 2.0 * k3[i] + k4[i]);

                if (s % sampleEvery == 0 && sample < samples)
                {
                    t.time[sample] = s * dt;
                    t.state[sample] = (double[])x.Clone();
                    sample++;
                }
            }

            t.completed = true;
            t.reason = "completed";
            return t;
        }

        private static MavValidationTrajectory Fail(MavValidationTrajectory t, int step, int samples)
        {
            Array.Resize(ref t.time, samples);
            Array.Resize(ref t.state, samples);
            t.completed = false;
            t.reason = "x-dot not evaluable at step " + step + " (t = " + (step * t.dt).ToString("G6") + " s)";
            return t;
        }
    }
}
