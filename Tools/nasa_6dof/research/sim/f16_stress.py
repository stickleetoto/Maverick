# Stress: torque-free tumble with an F-16-like inertia (slug ft^2, Stevens&Lewis/Morelli-style values incl. Ixz)
# and a high initial rate. Only the relative energy / |L| / rate error matter, not the units.
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, brick_schemes as b
Ib=np.array([[9496.,0,-982.],[0,55814.,0],[-982.,0,63100.]])
b.Ib=Ib; b.Ibi=np.linalg.inv(Ib)
for w0 in (np.array([4.0,0.5,0.3]), np.array([1.0,1.0,1.0])):
    b.w0=w0
    ref=b.reference([float(t) for t in range(2,31,2)])
    for dt in (0.02,0.01):
        for name,tq in (("A explicit",b.tq_explicit),("B implicit Euler 1N",b.tq_implicit_euler_1newton),("C Heun",b.tq_heun),("D implicit midpoint 2N",b.make_midpoint(2))):
            m=b.metrics(b.run(tq,dt),ref)
            print(f"w0={w0} dt={dt} {name:24s} |dw|={m['w']:.3e} deg/s att={m['att']:.3f} deg |dH|/H={m['H']:.2e} dT/T={m['Tsigned']:+.2e}")
