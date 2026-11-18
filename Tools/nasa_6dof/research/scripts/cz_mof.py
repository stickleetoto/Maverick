import numpy as np, itertools
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.])
CZ=np.array([.770,.241,-.100,-.416,-.731,-1.053,-1.366,-1.646,-1.917,-2.120,-2.248,-2.229])
ref=np.array([-1.378278e-01,-4.211369e+00,4.775187e+00,-1.026225e+01,8.399763e+00])
a=A*d2r
X=np.c_[np.ones_like(a),a,a**2,a**3,a**4]
# Gram-Schmidt with expansion coefficients
def mof(X,y,keep,order):
    n=X.shape[1]; P=[];E=[]  # E[k]: expansion of p_k in ordinary regressors
    for k in order:
        p=X[:,k].copy(); e=np.zeros(n); e[k]=1
        for pj,ej in zip(P,E):
            g=pj@X[:,k]/(pj@pj); p-=g*pj; e-=g*ej
        P.append(p);E.append(e)
    c=np.zeros(n)
    for idx,(p,e) in enumerate(zip(P,E)):
        if order[idx] in keep: c+= (p@y)/(p@p)*e
    return c
order=[0,1,2,3,4]
for r in range(1,6):
    for keep in itertools.combinations(range(5),r):
        c=mof(X,CZ,set(keep),order)
        err=np.max(abs(c/ref-1))
        if err<0.05: print(keep,err,np.round(c/ref,6))
# Any keep-set in any order?
best=[]
for order in itertools.permutations(range(5)):
    for r in range(1,6):
        for keep in itertools.combinations(range(5),r):
            c=mof(X,CZ,set(keep),list(order)); err=np.max(abs(c/ref-1)); best.append((err,order,keep))
best.sort(key=lambda t:t[0]); print(best[:5])
