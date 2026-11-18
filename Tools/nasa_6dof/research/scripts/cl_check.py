import numpy as np, itertools
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.]); B=np.array([0,5,10,15,20,25,30.])
vals=[0.]*12+[-.001,-.004,-.008,-.012,-.016,-.022,-.022,-.021,-.015,-.008,-.013,-.015,-.003,-.009,-.017,-.024,-.030,-.041,-.045,-.040,-.016,-.002,-.010,-.019,-.001,-.010,-.020,-.030,-.039,-.054,-.057,-.054,-.023,-.006,-.014,-.027,.000,-.010,-.022,-.034,-.047,-.060,-.069,-.067,-.033,-.036,-.035,-.035,.007,-.010,-.023,-.034,-.049,-.063,-.081,-.079,-.060,-.058,-.062,-.059,.009,-.011,-.023,-.037,-.050,-.068,-.089,-.088,-.091,-.076,-.077,-.076]
CL=np.array(vals).reshape(7,12)
ref=np.array([-1.058583e-01,-5.776677e-01,-1.672435e-02,1.357256e-01,2.172952e-01,3.464156e+00,-2.835451e+00,-1.098104e+00])
bb,aa=np.meshgrid(B*d2r,A*d2r,indexing='ij'); b=bb.ravel(); a=aa.ravel(); y=CL.ravel()
X=np.c_[b,a*b,a**2*b,b**2,a*b**2,a**3*b,a**4*b,a**2*b**2]
c=np.linalg.lstsq(X,y,rcond=None)[0]; print("Cl OLS half",np.max(abs(c/ref-1)),np.round(c/ref,6))
print("CY: -0.02/deg->",-0.02/d2r*1, " 0.021/20 per deg ->",0.021/20/d2r," 0.086/30->",0.086/30/d2r, " CZ de: 0.19/25->",0.19/25/d2r)
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
# candidate ordering by total degree then alpha power
cands={'b':(0,1),'ab':(1,1),'a2b':(2,1),'b2':(0,2),'ab2':(1,2),'a3b':(3,1),'a4b':(4,1),'a2b2':(2,2)}
names=list(cands)
best=[]
for order in itertools.permutations(range(8)):
    # require final kept includes something; test single drops only for speed
    for drop in range(8):
        keep=set(range(8))-{drop}
        c=mof(X,y,keep,list(order)); e=np.max(abs(c/ref-1)); best.append((e,order,drop))
best.sort(key=lambda t:t[0]); 
for e,o,d in best[:5]: print(f"{e:.2e}",[names[i] for i in o],"drop",names[d])
