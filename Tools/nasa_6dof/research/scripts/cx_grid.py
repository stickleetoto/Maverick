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
def lin(xb,yb,x):
    # linear interp with linear extrapolation
    i=np.clip(np.searchsorted(xb,x)-1,0,len(xb)-2)
    t=(x-xb[i])/(xb[i+1]-xb[i]); return yb[i]+t*(yb[i+1]-yb[i])
def table(al,dl):
    # bilinear with extrapolation
    col=np.array([lin(A,CX[j],al) for j in range(5)])
    return lin(DE,col,dl)
def fit(al_grid,de_grid):
    aa,dd=np.meshgrid(al_grid,de_grid); aa=aa.ravel(); dd=dd.ravel()
    y=np.array([table(x,z) for x,z in zip(aa,dd)])
    a=aa*d2r; de=dd*d2r
    M=np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3]
    return np.linalg.lstsq(M,y,rcond=None)[0]
cases={
 'bp x bp':(A,DE),
 'bp x de -25..25 step1':(A,np.arange(-25,26,1.)),
 'bp x de -24..24 step1':(A,np.arange(-24,25,1.)),
 'bp x de -25..25 step5':(A,np.arange(-25,26,5.)),
 'bp x de -24..24 step 6':(A,np.arange(-24,25,6.)),
 'bp x de -25,-12.5..25':(A,np.array([-25,-12.5,0,12.5,25])),
 'a step1 x DE':(np.arange(-10,46,1.),DE),
 'a step2.5 x DE':(np.arange(-10,45.1,2.5),DE),
 'a step1 x de step1':(np.arange(-10,46,1.),np.arange(-24,25,1.)),
 'a -10..40 bp x DE':(A[A<=40],DE),
}
for k,(ag,dg) in cases.items():
    c=fit(ag,dg); print(f"{k:28s}",np.round(c/ref,4))
