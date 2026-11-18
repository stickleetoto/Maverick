import numpy as np
exec(open(__file__.replace('trim_attr.py','cx_mof.py')).read().split('def mof')[0])
from scipy.interpolate import RegularGridInterpolator as RGI
CZ=np.array([.770,.241,-.100,-.416,-.731,-1.053,-1.366,-1.646,-1.917,-2.120,-2.248,-2.229])
def polyx(c,al,de):
    a=al*d2r; e=de*d2r; return np.array([1,a,e*e,e,a*e,a*a,a**3])@c
def polym(c,al,de):
    a=al*d2r; e=de*d2r; return np.array([1,a,e,a*e,e*e,a*a*e,e**3,a*e*e])@c
def ols(tab,cols,debp):
    a,de=np.meshgrid(A*d2r,np.array(debp,float)*d2r); a=a.ravel(); de=de.ravel()
    return np.linalg.lstsq(cols(a,de),tab.ravel(),rcond=None)[0]
cx24=ols(CX,lambda a,de: np.c_[np.ones_like(a),a,de**2,de,a*de,a**2,a**3],[-24,-12,0,12,24])
cm24=ols(CM,lambda a,de: np.c_[np.ones_like(a),a,de,a*de,de**2,a**2*de,de**3,a*de**2],[-24,-12,0,12,24])
tx=RGI((np.array([-24,-12,0,12,24.]),A),CX); tm=RGI((np.array([-24,-12,0,12,24.]),A),CM)
for al,de in ((2.639,-3.233),(1.906,-4.055),(5,0),(0,-24),(10,24)):
    print(f"a={al} de={de}: CX table {tx([de,al])[0]:+.5f} Morelli {polyx(refx,al,de):+.5f} OLS24 {polyx(cx24,al,de):+.5f} | Cm table {tm([de,al])[0]:+.5f} Morelli {polym(refm,al,de):+.5f} OLS24 {polym(cm24,al,de):+.5f}")
