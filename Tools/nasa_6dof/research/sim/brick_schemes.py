# Scratch model of candidate rotational schemes for NASA check case 2 (torque-free brick).
# Models the PhysX step as read from PhysX 4.1 DyBodyCoreIntegrator.h: w_world += h*Iw^-1*tau, then
# R <- Rot(h*w_world) * R (closed-form exponential map). Double precision (NOT float32, NOT Unity).
import numpy as np
D = np.pi/180
Ib = np.diag([0.001894220, 0.006211019, 0.007194665])
Ibi = np.linalg.inv(Ib)
w0 = np.array([10.,20.,30.])*D
T_END = 30.0
def skew(a): return np.array([[0,-a[2],a[1]],[a[2],0,-a[0]],[-a[1],a[0],0]])
def rot(v):
    th = np.linalg.norm(v)
    if th < 1e-300: return np.eye(3)
    k = skew(v/th); return np.eye(3)+np.sin(th)*k+(1-np.cos(th))*k@k
def euler_rhs(w): return Ibi @ (-np.cross(w, Ib@w))
# reference: RK4 on (R, w_b) with R' = R skew(w_b), h=1e-3, re-orthonormalised
def reference(times):
    h=1e-3; R=np.eye(3); w=w0.copy(); out={}; n=int(round(T_END/h)); tset={int(round(t/h)):t for t in times}
    def f(R,w): return R@skew(w), euler_rhs(w)
    for i in range(1,n+1):
        k1=f(R,w); k2=f(R+h/2*k1[0],w+h/2*k1[1]); k3=f(R+h/2*k2[0],w+h/2*k2[1]); k4=f(R+h*k3[0],w+h*k3[1])
        R=R+h/6*(k1[0]+2*k2[0]+2*k3[0]+k4[0]); w=w+h/6*(k1[1]+2*k2[1]+2*k3[1]+k4[1])
        u,_,vt=np.linalg.svd(R); R=u@vt
        if i in tset: out[tset[i]]=(R.copy(),w.copy())
    return out
# "torque" laws: given body rate w (start of step), return body torque Maverick would hand to AddRelativeTorque
def tq_explicit(w,h): return -np.cross(w, Ib@w)
def tq_implicit_euler_1newton(w,h):   # Catto GDC2015 slide 76 / Bullet ..._Implicit_Body
    f = h*np.cross(w, Ib@w); J = Ib + h*(skew(w)@Ib - skew(Ib@w))
    w2 = w - np.linalg.solve(J,f); return Ib@(w2-w)/h
def tq_heun(w,h):                     # predictor-corrector, gyro at averaged rate
    wp = w + h*euler_rhs(w); wm = 0.5*(w+wp); return -np.cross(wm, Ib@wm)
def make_midpoint(iters):
    def tq(w,h):                      # implicit midpoint on Euler eqs: I(w2-w1)+h*wm x I wm = 0, wm=(w1+w2)/2
        w2 = w + h*euler_rhs(w)
        for _ in range(iters):
            wm=0.5*(w+w2); F = Ib@(w2-w) + h*np.cross(wm, Ib@wm)
            J = Ib + 0.5*h*(skew(wm)@Ib - skew(Ib@wm)); w2 = w2 - np.linalg.solve(J,F)
        return Ib@(w2-w)/h
    return tq
def run(tq, h, half_step_init=False, sample_shift=0.0):
    R=np.eye(3); w_w=w0.copy()
    if half_step_init:  # treat stored rate as w_{1/2}: one-time RK-accurate half step (initialisation only)
        w_w = w0 + 0.5*h*euler_rhs(w0) + 0.125*h*h*(np.array(jac(w0))@euler_rhs(w0))
    n=int(round(T_END/h)); samp={}
    for i in range(1,n+1):
        wb = R.T@w_w
        tb = tq(wb,h)                  # body torque
        Iw = R@Ib@R.T
        w_w = w_w + h*np.linalg.solve(Iw, R@tb)   # PhysX velocity update (start-of-step world inertia)
        R = rot(h*w_w)@R                          # PhysX exponential-map orientation update
        t=i*h
        if abs(t/2-round(t/2))<1e-9 and t>0: samp[round(t,6)]=(R.copy(), R.T@w_w)
    return samp
def jac(w): return Ibi@(-(skew(w)@Ib - skew(Ib@w)))
def metrics(samp, ref, refshift=None):
    H0=Ib@w0; T0=0.5*w0@Ib@w0; e=dict(w=0,att=0,H=0,T=0,Tsigned=0)
    for t,(R,w) in samp.items():
        Rr,wr = (refshift or ref)[t]
        e['w']=max(e['w'],np.linalg.norm(w-wr)/D)
        c=np.clip((np.trace(Rr.T@R)-1)/2,-1,1); e['att']=max(e['att'],np.degrees(np.arccos(c)))
        e['H']=max(e['H'],np.linalg.norm(R@Ib@w-H0)/np.linalg.norm(H0))
        dT=(0.5*w@Ib@w-T0)/T0
        if abs(dT)>abs(e['Tsigned']): e['Tsigned']=dT
    return e
if __name__=="__main__":
    times=[float(t) for t in range(2,31,2)]
    ref=reference(times)
    schemes=[("A explicit (current)",tq_explicit,False),("B implicit Euler, 1 Newton (Catto/Bullet)",tq_implicit_euler_1newton,False),
             ("C predictor-corrector (Heun gyro)",tq_heun,False),("D implicit midpoint, 2 Newton",make_midpoint(2),False),
             ("E = D + half-step rate init",make_midpoint(2),True)]
    for dt in (0.02,0.01,0.005):
        print(f"dt={dt}")
        for name,tq,hs in schemes:
            m=metrics(run(tq,dt,hs),ref)
            print(f"  {name:45s} |dw|max={m['w']:.4e} deg/s  att={m['att']:.4f} deg  |dH|/H={m['H']:.3e}  dT/T(signed,max)={m['Tsigned']:+.3e}")
