import numpy as np, itertools
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.])
CX=np.array([
[-.099,-.081,-.081,-.063,-.025,.044,.097,.113,.145,.167,.174,.166],
[-.048,-.038,-.040,-.021,.016,.083,.127,.137,.162,.177,.179,.167],
[-.022,-.020,-.021,-.004,.032,.094,.128,.130,.154,.161,.155,.138],
[-.040,-.038,-.039,-.025,.006,.062,.087,.085,.100,.110,.104,.091],
[-.083,-.073,-.076,-.072,-.046,.012,.024,.025,.043,.053,.047,.040]])
CM=np.array([
[.205,.168,.186,.196,.213,.251,.245,.238,.252,.231,.198,.192],
[.081,.077,.107,.110,.110,.141,.127,.119,.133,.108,.081,.093],
[-.046,-.020,-.009,-.005,-.006,.010,.006,-.001,.014,.000,-.013,.032],
[-.174,-.145,-.121,-.127,-.129,-.102,-.097,-.113,-.087,-.084,-.069,-.006],
[-.259,-.202,-.184,-.193,-.199,-.150,-.160,-.167,-.104,-.076,-.041,-.005]])
refx=np.array([-1.943367e-02,2.136104e-01,-2.903457e-01,-3.348641e-03,-2.060504e-01,6.988016e-01,-9.035381e-01])
refm=np.array([-2.029370e-02,4.660702e-02,-6.012308e-01,-8.062977e-02,8.320429e-02,5.018538e-01,6.378864e-01,4.226356e-01])
def mof(X,y,keep,order):
    n=X.shape[1]; P=[];E=[]
    for k in order:
        p=X[:,k].copy(); e=np.zeros(n); e[k]=1
        for pj,ej in zip(P,E):
            g=pj@X[:,k]/(pj@pj); p-=g*pj; e-=g*ej
        P.append(p);E.append(e)
    c=np.zeros(n)
    for idx,(p,e) in enumerate(zip(P,E)):
        if order[idx] in keep: c+=(p@y)/(p@p)*e
    return c
def grid(debp):
    a,de=np.meshgrid(A*d2r,np.array(debp,float)*d2r); return a.ravel(),de.ravel()
for name,tab,ref,cols in (("CX",CX,refx,lambda a,de: np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]),
                          ("Cm",CM,refm,lambda a,de: np.c_[np.ones_like(a),a,de,a*de,de**2,a**2*de,de**3,a*de**2])):
    for debp in ([-24,-12,0,12,24],[-25,-12,0,12,25],[-25,-12.5,0,12.5,25]):
        a,de=grid(debp); X=cols(a,de); y=tab.ravel(); n=X.shape[1]
        c=np.linalg.lstsq(X,y,rcond=None)[0]
        print(name,debp,"OLS maxrel",f"{np.max(abs(c/ref-1)):.2e}",np.round(c/ref,5))
        # MOF in listed order, drop subsets of up to 2
        order=list(range(n)); best=(9,None)
        for r in range(0,3):
            for drop in itertools.combinations(range(n),r):
                keep=set(range(n))-set(drop); c=mof(X,y,keep,order); e=np.max(abs(c/ref-1))
                if e<best[0]: best=(e,drop)
        print("   MOF listed-order best drop",best)
