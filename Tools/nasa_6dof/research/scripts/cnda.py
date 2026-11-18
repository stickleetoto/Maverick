import numpy as np, itertools
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.]); B=np.array([-30,-20,-10,0,10,20,30.])
v=[.001,-.027,-.017,-.013,-.012,-.016,.001,.017,.011,.017,.008,.016,.002,-.014,-.016,-.016,-.014,-.019,-.021,.002,.012,.016,.015,.011,-.006,-.008,-.006,-.006,-.005,-.008,-.005,.007,.004,.007,.006,.006,-.011,-.011,-.010,-.009,-.008,-.006,.000,.004,.007,.010,.004,.010,-.015,-.015,-.014,-.012,-.011,-.008,-.002,.002,.006,.012,.011,.011,-.024,-.010,-.004,-.002,-.001,.003,.014,.006,-.001,.004,.004,.006,-.022,.002,-.003,-.005,-.003,-.001,-.009,-.009,-.001,.003,-.002,.001]
T=np.array(v).reshape(7,12)
ref=np.array([-3.348717e-02,4.276655e-02,6.573646e-03,3.535831e-01,-1.373308e+00,1.237582e+00,2.302543e-01,-2.512876e-01,1.588105e-01,-5.199526e-01])
bb,aa=np.meshgrid(B*d2r,A*d2r,indexing='ij'); b=bb.ravel(); a=aa.ravel()
X=np.c_[np.ones_like(a),a,b,a*b,a**2*b,a**3*b,a**2,a**3,b**3,a*b**3]
for s in (20,21.5):
    y=T.ravel()/(s*d2r); c=np.linalg.lstsq(X,y,rcond=None)[0]
    print(s,np.round(c/ref,5))
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
y=T.ravel()/(20*d2r)
# natural generation order by degree: 1, a, b, a2, ab, b2.., use subsets of our 10 terms ordered by total degree then
deg=[(0,0),(1,0),(0,1),(1,1),(2,1),(3,1),(2,0),(3,0),(0,3),(1,3)]
orders={'listed':list(range(10)),'bydeg':sorted(range(10),key=lambda i:(sum(deg[i]),-deg[i][0]))}
for name,order in orders.items():
    best=(9,None)
    for r in range(0,4):
        for drop in itertools.combinations(range(10),r):
            c=mof(X,y,set(range(10))-set(drop),order); e=np.max(abs(c/ref-1))
            if e<best[0]: best=(e,drop)
    print(name,best)
