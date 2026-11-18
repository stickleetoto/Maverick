import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, brick_schemes as b
times=[float(t) for t in range(2,31,2)]; ref=b.reference(times)
for name,tq in (("A",b.tq_explicit),("D",b.make_midpoint(2))):
    s=b.run(tq,0.02); row=[]
    for t in (2.0,10.0,20.0,30.0):
        R,_=s[t]; Rr,_=ref[t]; c=np.clip((np.trace(Rr.T@R)-1)/2,-1,1); row.append(f"t={t:.0f}:{np.degrees(np.arccos(c)):.3f}")
    print(name, " ".join(row))
