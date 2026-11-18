import numpy as np
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.])
DE=np.array([-24,-12,0,12,24.])
B1=np.array([0,5,10,15,20,25,30.])
CX=np.array([
[-.099,-.081,-.081,-.063,-.025,.044,.097,.113,.145,.167,.174,.166],
[-.048,-.038,-.040,-.021,.016,.083,.127,.137,.162,.177,.179,.167],
[-.022,-.020,-.021,-.004,.032,.094,.128,.130,.154,.161,.155,.138],
[-.040,-.038,-.039,-.025,.006,.062,.087,.085,.100,.110,.104,.091],
[-.083,-.073,-.076,-.072,-.046,.012,.024,.025,.043,.053,.047,.040]])
CZ=np.array([.770,.241,-.100,-.416,-.731,-1.053,-1.366,-1.646,-1.917,-2.120,-2.248,-2.229])
CN=np.array([
[0]*12,
[.018,.019,.018,.019,.019,.018,.013,.007,.004,-.014,-.017,-.033],
[.038,.042,.042,.042,.043,.039,.030,.017,.004,-.035,-.047,-.057],
[.056,.057,.059,.058,.058,.053,.032,.012,.002,-.046,-.071,-.073],
[.064,.077,.076,.074,.073,.057,.029,.007,.012,-.034,-.065,-.041],
[.074,.086,.093,.089,.080,.062,.049,.022,.028,-.012,-.002,-.013],
[.079,.090,.106,.106,.096,.080,.068,.030,.064,.015,.011,-.001]])
def fit(X,y,names,ref,label):
    c,res,rk,sv=np.linalg.lstsq(X,y,rcond=None)
    r=y-X@c
    print(f"== {label}: n={len(y)} rms resid={np.sqrt(np.mean(r**2)):.5f} max={np.max(abs(r)):.5f}")
    for n,ci,ri in zip(names,c,ref):
        print(f"  {n:8s} refit={ci:+.6e} morelli={ri:+.6e} ratio={ci/ri if ri else float('nan'):.4f}")
    return c
# CX
a,de=np.meshgrid(A*d2r,DE*d2r); a=a.ravel(); de=de.ravel(); y=CX.ravel()
names=['1','a','de2','de','a*de','a2','a3']
X=np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]
ref=[-1.943367e-02,2.136104e-01,-2.903457e-01,-3.348641e-03,-2.060504e-01,6.988016e-01,-9.035381e-01]
c=fit(X,y,names,ref,"CX0 on NESC 12x5 grid")
# also restricted alpha ranges
for amax in (40,35,30):
    m=A*0+1
    sel=(a<=amax*d2r+1e-9)
    fit(X[sel],y[sel],names,ref,f"CX0 alpha<= {amax}")
# CX residual of Morelli at the grid
Xm=X@np.array(ref)
print("Morelli CX at a=5,de=0:",(np.array([1,5*d2r,0,0,0,(5*d2r)**2,(5*d2r)**3])@ref))
# CZ
a=A*d2r
X=np.c_[np.ones_like(a),a,a**2,a**3,a**4]
ref=[-1.378278e-01,-4.211369e+00,4.775187e+00,-1.026225e+01,8.399763e+00]
fit(X,CZ,['1','a','a2','a3','a4'],ref,"CZ0 on NESC 12 pts")
pm=X@ref
print(" Morelli CZ0 vs table:")
for ai,t,p in zip(A,CZ,pm): print(f"   a={ai:5.1f} table={t:+.3f} morelli={p:+.6f} d={p-t:+.4f}")
# CN
names=['b','a*b','b2','a*b2','a2*b','a2*b2','a3*b']
ref=[2.993363e-01,6.594004e-02,-2.003125e-01,-6.233977e-02,-2.107885e+00,2.141420e+00,8.476901e-01]
bb,aa=np.meshgrid(B1*d2r,A*d2r,indexing='ij'); b=bb.ravel(); a=aa.ravel(); y=CN.ravel()
def Xcn(a,b): return np.c_[b,a*b,b**2,a*b**2,a**2*b,a**2*b**2,a**3*b]
fit(Xcn(a,b),y,names,ref,"Cn0 on NESC beta 0..30 (half)")
b2=np.r_[-b[b>0],b]; a2=np.r_[a[b>0],a]; y2=np.r_[-y[b>0],y]
fit(Xcn(a2,b2),y2,names,ref,"Cn0 on NESC beta -30..30 (odd-extended)")
for bmax in (20,10):
    s=b<=bmax*d2r+1e-9
    fit(Xcn(a[s],b[s]),y[s],names,ref,f"Cn0 beta 0..{bmax}")
# Cn_beta at small beta from Morelli: d/db at b=0 = ref0 + ref1 a + ref4 a2 + ref6 a3
for ad in (20,22.5,25,27.5,30,32.5,35):
    x=ad*d2r
    cnb=(ref[0]+ref[1]*x+ref[4]*x*x+ref[6]*x**3)*d2r
    # also secant at beta=5
    b5=5*d2r
    cn5=Xcn(np.array([x]),np.array([b5]))@ref
    print(f"  a={ad}: Morelli Cn_beta(0)={cnb:+.5f}/deg  Cn(b=5)/5={cn5[0]/5:+.5f}/deg")
