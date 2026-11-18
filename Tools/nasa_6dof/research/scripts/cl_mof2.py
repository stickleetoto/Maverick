import numpy as np, itertools
exec(open(__file__.replace('cl_mof2.py','cl_check.py')).read().split('def mof')[0])
from itertools import combinations
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
deg=[(0,1),(1,1),(2,1),(0,2),(1,2),(3,1),(4,1),(2,2)]
orders={'listed':list(range(8)),'bydeg':sorted(range(8),key=lambda i:(sum(deg[i]),-deg[i][0]))}
# also full-beta odd-extended data
b2=np.r_[-b[b>0],b]; a2=np.r_[a[b>0],a]; y2=np.r_[-y[b>0],y]
X2=np.c_[b2,a2*b2,a2**2*b2,b2**2,a2*b2**2,a2**3*b2,a2**4*b2,a2**2*b2**2]
for dname,XX,yy in (("half",X,y),("odd-ext",X2,y2)):
  for name,order in orders.items():
    best=(9,None)
    for r in range(0,4):
        for drop in combinations(range(8),r):
            c=mof(XX,yy,set(range(8))-set(drop),order); e=np.max(abs(c/ref-1))
            if e<best[0]: best=(e,drop)
    print(dname,name,best)
