"""Independent offline check of the WP-3D eigensolver: recompute the eigenvalues of the 170 exported
Jacobians with numpy (LAPACK geev) and compare them with the dataset's eigenvalues.

Not part of the Unity suites (no runtime dependency on numpy). Usage: python crosscheck_numpy.py
"""
import csv
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    jacobians = {}
    with open(os.path.join(HERE, 'f15_research_stability_jacobians.csv')) as f:
        reader = csv.reader(f)
        next(reader)
        for row in reader:
            jacobians[int(row[0])] = np.array([float(x) for x in row[3:]]).reshape(8, 8)

    spectra = {}
    with open(os.path.join(HERE, 'f15_research_stability_table_vii.csv')) as f:
        for row in csv.DictReader(f):
            spectra.setdefault(int(row['point']), []).append(complex(float(row['real_per_s']), float(row['imag_rad_s'])))

    worst, where, n = 0.0, None, 0
    for point, a in sorted(jacobians.items()):
        lapack = list(np.linalg.eigvals(a))
        for mine in spectra[point]:
            k = min(range(len(lapack)), key=lambda i: abs(lapack[i] - mine))
            rel = abs(lapack[k] - mine) / max(1.0, abs(mine))
            if rel > worst:
                worst, where = rel, (point, mine, lapack[k])
            lapack.pop(k)
            n += 1
    print('numpy %s LAPACK vs validation eigensolver: %d eigenvalues, largest relative difference %.2e at %s'
          % (np.__version__, n, worst, where))


if __name__ == '__main__':
    main()
