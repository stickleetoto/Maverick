import numpy as np
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
def X(a,de): return np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]
a,de=np.meshgrid(A*d2r,DE*d2r); a=a.ravel(); de=de.ravel(); y=CX.ravel()
M=X(a,de); P=np.linalg.pinv(M)
c0=P@y
dc=ref-c0
print("dc",dc)
# hypothesis: delta y = g(de_j) constant across alpha. Build basis
G=np.zeros((60,5))
for j in range(5): G[:,j]=(de==DE[j]*d2r)
T=P@G  # 7x5 map
g,res,rk,sv=np.linalg.lstsq(T,dc,rcond=None)
print("g (per de row -24..24):",g, "residual of dc:",T@g-dc)
# single-row hypothesis: each row j offset by k
for j in range(5):
    k=np.dot(T[:,j],dc)/np.dot(T[:,j],T[:,j]); r=T[:,j]*k-dc
    print(f"row de={DE[j]}: k={k:+.5f} resid max={abs(r).max():.2e}")
# hypothesis: de breakpoints scaled by s and/or alpha-only? try s scan with g
for s in (25/24, 1.0):
    M2=X(a,de*s); c=np.linalg.pinv(M2)@y
    print("s",s,c, c/ref)
# Hypothesis: Morelli used only some de rows
import itertools
for rows in itertools.combinations(range(5),4):
    sel=np.isin(de,DE[list(rows)]*d2r)
    c=np.linalg.lstsq(M[sel],y[sel],rcond=None)[0]
    print(rows, np.round(c/ref,4))
