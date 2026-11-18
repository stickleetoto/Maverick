import numpy as np, itertools
from scipy.optimize import least_squares
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.])
CZ=np.array([.770,.241,-.100,-.416,-.731,-1.053,-1.366,-1.646,-1.917,-2.120,-2.248,-2.229])
ref=np.array([-1.378278e-01,-4.211369e+00,4.775187e+00,-1.026225e+01,8.399763e+00])
def M(al): a=np.asarray(al)*d2r; return np.c_[np.ones_like(a),a,a**2,a**3,a**4]
P=np.linalg.pinv(M(A)); dc=ref-P@CZ
print("dc",dc)
w=1/abs(ref)
best=[]
for k in (1,2,3):
    for idx in itertools.combinations(range(12),k):
        T=P[:,idx]; g=np.linalg.lstsq(T*w[:,None],dc*w,rcond=None)[0]
        best.append((np.max(abs((T@g-dc)*w)),idx,g))
best.sort(key=lambda t:t[0])
for e,idx,g in best[:6]: print(f"{e:.2e}",[(A[i],round(float(x),5)) for i,x in zip(idx,g)])
# subsets of alpha points
for lo in range(0,3):
    for hi in range(10,13):
        c=np.linalg.lstsq(M(A[lo:hi]),CZ[lo:hi],rcond=None)[0]
        print(A[lo],A[hi-1],np.round(c/ref,4))
# fixed-delta-e/beta data: CZ data with dele term from S&L but fit includes de regressor; grids
# hypothesis: fit to CZ(alpha)*(1-b^2) - 0.19*de/25 over grid with de labelled +-25 but data at +-24 etc
def czfull(al,b,de): return np.interp(al,A,CZ)*(1-(b*d2r)**2)-0.19*de/25
for debp_data,debp_reg in (([-24,-12,0,12,24],[-25,-12,0,12,25]),([-25,-12,0,12,25],[-24,-12,0,12,24])):
  for bset in ([0],[0,5,10,15,20,25,30],[-30,-20,-10,0,10,20,30]):
    rows=[];ys=[]
    for al in A:
      for b in bset:
        for dd,dr in zip(debp_data,debp_reg):
          a=al*d2r; bb=b*d2r
          rows.append([(1-bb*bb)*a**k for k in range(5)]+[dr*d2r]); ys.append(czfull(al,b,dd))
    c=np.linalg.lstsq(np.array(rows),np.array(ys),rcond=None)[0]
    print(debp_data,bset,np.round(c[:5]/ref,4),c[5])
