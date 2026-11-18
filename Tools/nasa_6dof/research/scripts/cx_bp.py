import numpy as np
from scipy.optimize import least_squares
d2r=np.pi/180
A=np.array([-10,-5,0,5,10,15,20,25,30,35,40,45.])
CX=np.array([
[-.099,-.081,-.081,-.063,-.025,.044,.097,.113,.145,.167,.174,.166],
[-.048,-.038,-.040,-.021,.016,.083,.127,.137,.162,.177,.179,.167],
[-.022,-.020,-.021,-.004,.032,.094,.128,.130,.154,.161,.155,.138],
[-.040,-.038,-.039,-.025,.006,.062,.087,.085,.100,.110,.104,.091],
[-.083,-.073,-.076,-.072,-.046,.012,.024,.025,.043,.053,.047,.040]])
ref=np.array([-1.943367e-02,2.136104e-01,-2.903457e-01,-3.348641e-03,-2.060504e-01,6.988016e-01,-9.035381e-01])
def coefs(bp):
    a,de=np.meshgrid(A*d2r,np.array(bp)*d2r); a=a.ravel(); de=de.ravel()
    M=np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]
    return np.linalg.lstsq(M,CX.ravel(),rcond=None)[0]
def f(x): return (coefs([x[0],x[1],x[4],x[2],x[3]])-ref)/abs(ref)
for x0 in ([-24,-12,12,24,0],[-25,-12.5,12.5,25,0]):
    r=least_squares(f,x0); print(np.round(r.x,4), np.max(abs(r.fun)))
