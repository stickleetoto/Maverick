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
a,de=np.meshgrid(A*d2r,DE*d2r); a=a.ravel(); de=de.ravel()
M=np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]
res=[]
for p in itertools.permutations(range(5)):
    y=CX[list(p)].ravel(); c=np.linalg.lstsq(M,y,rcond=None)[0]
    res.append((np.max(abs(c/ref-1)),p,c))
res.sort(key=lambda t:t[0])
for e,p,c in res[:5]: print(e,p,np.round(c/ref,6))
# also: de breakpoints alternative sets with symmetric/asymmetric values for the same 5 rows
for debp in ([-25,-12.5,0,12.5,25],[-24,-12,0,12,24],[-25,-10,0,10,25],[-20,-10,0,10,20],[-24,-12,0,12,24.0]):
    dd=np.array(debp,float)*d2r
    a2,de2=np.meshgrid(A*d2r,dd); a2=a2.ravel(); de2=de2.ravel()
    M2=np.c_[np.ones_like(a2),a2,de2**2,de2,a2*de2,a2**2,a2**3]
    c=np.linalg.lstsq(M2,CX.ravel(),rcond=None)[0]; print(debp,np.round(c/ref,5))
