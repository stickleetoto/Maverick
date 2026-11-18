# Follow-up: staggered (leapfrog-interpreted) variant. Stored PhysX rate treated as w(t_n - h/2);
# initialised once by a backward half step. Rates compared against the reference at t - h/2.
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, brick_schemes as b
D=b.D
def reference_at(times,h=5e-4):
    R=np.eye(3); w=b.w0.copy(); out={}; n=int(round(b.T_END/h)); tset={int(round(t/h)):t for t in times}
    f=lambda R,w:(R@b.skew(w), b.euler_rhs(w))
    for i in range(1,n+1):
        k1=f(R,w);k2=f(R+h/2*k1[0],w+h/2*k1[1]);k3=f(R+h/2*k2[0],w+h/2*k2[1]);k4=f(R+h*k3[0],w+h*k3[1])
        R=R+h/6*(k1[0]+2*k2[0]+2*k3[0]+k4[0]); w=w+h/6*(k1[1]+2*k2[1]+2*k3[1]+k4[1]); u,_,vt=np.linalg.svd(R); R=u@vt
        if i in tset: out[tset[i]]=(R.copy(),w.copy())
    return out
def run_stag(tq,h):
    f0=b.euler_rhs(b.w0); w_b=b.w0-0.5*h*f0+0.125*h*h*(b.jac(b.w0)@f0)
    R=np.eye(3); w_w=w_b.copy(); n=int(round(b.T_END/h)); samp={}
    for i in range(1,n+1):
        wb=R.T@w_w; tb=tq(wb,h); Iw=R@b.Ib@R.T
        w_w=w_w+h*np.linalg.solve(Iw,R@tb); R=b.rot(h*w_w)@R; t=i*h
        if abs(t/2-round(t/2))<1e-9: samp[round(t,6)]=(R.copy(),R.T@w_w)
    return samp
if __name__=="__main__":
    base=[float(t) for t in range(2,31,2)]
    for dt in (0.02,0.01,0.005):
        need=sorted(set(base+[round(t-dt/2,6) for t in base]))
        ref=reference_at(need)
        for name,tq in (("F stagger + implicit midpoint 2N",b.make_midpoint(2)),("G stagger + Heun",b.tq_heun),("H stagger + explicit",b.tq_explicit)):
            s=run_stag(tq,dt)
            H0=b.Ib@b.w0;T0=0.5*b.w0@b.Ib@b.w0; ew=ea=eH=0; eT=0
            for t,(R,w) in s.items():
                Rr,_=ref[t]; _,wr=ref[round(t-dt/2,6)]  # stored rate after step ~ w(t - h/2)
                ew=max(ew,np.linalg.norm(w-wr)/D); c=np.clip((np.trace(Rr.T@R)-1)/2,-1,1); ea=max(ea,np.degrees(np.arccos(c)))
                eH=max(eH,np.linalg.norm(R@b.Ib@w-H0)/np.linalg.norm(H0)); dT=(0.5*w@b.Ib@w-T0)/T0; eT=dT if abs(dT)>abs(eT) else eT
            print(f"dt={dt} {name:34s} |dw|(vs t-h/2)={ew:.3e}  att={ea:.4e} deg  |dH|/H={eH:.3e}  dT/T={eT:+.3e}")
