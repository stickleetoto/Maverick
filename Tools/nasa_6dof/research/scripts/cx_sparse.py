import numpy as np, itertools
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.])
DE=np.array([-24,-12,0,12,24.])
CX=np.array([
[-.099,-.081,-.081,-.063,-.025,.044,.097,.113,.145,.167,.174,.166],
[-.048,-.038,-.040,-.021,.016,.083,.127,.137,.162,.177,.179,.167],
[-.022,-.020,-.021,-.004,.032,.094,.128,.130,.154,.161,.155,.138],
[-.040,-.038,-.039,-.025,.006,.062,.087,.085,.100,.110,.104,.091],
[-.083,-.073,-.076,-.072,-.046,.012,.024,.025,.043,.053,.047,.040]])
ref=np.array([-1.943367e-02,2.136104e-01,-2.903457e-01,-3.348641e-03,-2.060504e-01,6.988016e-01,-9.035381e-01])
a,de=np.meshgrid(A*d2r,DE*d2r); a=a.ravel(); de=de.ravel(); y=CX.ravel()
M=np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]
P=np.linalg.pinv(M); dc=ref-P@y
# scale rows of dc for relative comparison
w=1/np.maximum(abs(ref),1e-9)
best=[]
for k in (1,2,3):
    for idx in itertools.combinations(range(60),k):
        T=P[:,idx]
        g=np.linalg.lstsq(T*w[:,None],dc*w,rcond=None)[0]
        r=(T@g-dc)*w
        best.append((np.max(abs(r)),k,idx,g))
best.sort(key=lambda t:t[0])
for e,k,idx,g in best[:12]:
    print(f"maxrel={e:.2e} k={k}",[(DE[i//12],A[i%12],round(float(gi),5)) for i,gi in zip(idx,g)])
