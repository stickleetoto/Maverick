# stdlib-only: atmosphere-model difference and rotating-Earth magnitude estimates for case 11 / 13.x
import math, sys
ft = 0.3048
def mav_atm(h_m):  # mirrors MavAtmosphereModel troposphere: geometric altitude used as geopotential
    T0, p0, R, g, L = 288.15, 101325.0, 287.05287, 9.80665, -0.0065
    T = T0 + L*h_m; p = p0*(T/T0)**(-g/(L*R)); return T, p, p/(R*T), math.sqrt(1.4*R*T)
def us76(h_m):     # US 1976: geopotential H = r0 h/(r0+h), r0 = 6356766 m
    H = 6356766.0*h_m/(6356766.0+h_m); return mav_atm(H)
h = 10013*ft
slug_ft3 = 515.378818
nasa_rho = 0.0017548379458813387*slug_ft3; nasa_a = 1077.3519651228748*ft; nasa_T = 482.9791755286174/1.8
for name, f in (("Maverick (geometric as geopotential)", mav_atm), ("US1976 geopotential", us76)):
    T, p, rho, a = f(h)
    print(f"{name}: T={T:.4f} K rho={rho:.6f} kg/m3 a={a:.4f} m/s | rel to NASA SIM05: drho={rho/nasa_rho-1:+.3e} da={a/nasa_a-1:+.3e} dT={T-nasa_T:+.4f} K")
# dynamic-pressure sensitivity for altitude step of 100 ft
T1,p1,r1,a1 = us76(h); T2,p2,r2,a2 = us76(h+100*ft)
print(f"rho change over +100 ft: {r2/r1-1:+.4e}")
# KEAS at trim
V = 565.6854; rho0 = 1.225
print("KEAS(trim) =", V*math.sqrt(nasa_rho/rho0)/1.6878098571, "kt")
# rotating-Earth magnitudes
Om = 7.2921159e-5; lat = math.radians(36.01916667); a_e = 20925646.3; f = 1/298.257223563; e2 = f*(2-f)
RN = a_e/math.sqrt(1-e2*math.sin(lat)**2); RM = RN*(1-e2)/(1-e2*math.sin(lat)**2)
hft = 10013.0; VN = VE = 400.0; Vt = math.hypot(VN, VE)
cor_h = 2*Om*math.sin(lat)*Vt; eot = 2*Om*math.cos(lat)*VE
cent_path = VN**2/(RM+hft) + VE**2/(RN+hft)
centrif = Om**2*(RN+hft)*math.cos(lat)  # magnitude, acting away from spin axis
centrif_v = centrif*math.cos(lat)
print(f"R_N={RN:.0f} ft R_M={RM:.0f} ft")
print(f"horizontal Coriolis 2*Om*sin(lat)*V = {cor_h:.5f} ft/s^2 (to the right)")
for T in (20, 30, 60, 180):
    print(f"   uncompensated lateral drift at {T:3d} s: {0.5*cor_h*T*T:8.1f} ft; track rotation {math.degrees(cor_h/Vt*T):.3f} deg")
print(f"vertical Eotvos 2*Om*cos(lat)*VE = {eot:.5f} ft/s^2 (up); path-curvature V^2/R = {cent_path:.5f}; centrifugal vertical = {centrif_v:.5f}")
print(f"total vertical relief = {eot+cent_path+centrif_v:.5f} ft/s^2 = {100*(eot+cent_path+centrif_v)/32.18857:.3f} % of J2 gravitation")
print(f"equivalent bank to cancel horizontal Coriolis = {math.degrees(math.atan(cor_h/32.05)):.4f} deg (left wing down in N hemisphere)")
psi_rate = math.degrees(Om*math.sin(lat) + VE*math.tan(lat)/(RN+hft))
print(f"yaw drift of inertially-held heading wrt local north: -{psi_rate:.5f} deg/s -> {psi_rate*180:.3f} deg over 180 s")
q_tr = math.degrees(-(Om*math.cos(lat)*math.sin(math.radians(45))) - Vt/((RN+RM)/2+hft))
print(f"pitch rate needed to stay level (body y) ~ {q_tr:.5f} deg/s (SIM 04 trim logs -0.0039)")
dist = Vt*180
print(f"ground distance 180 s = {dist:.0f} ft; tangent-plane drop d^2/2R = {dist**2/(2*(RN+hft)):.1f} ft")
# effective flat-world gravity reproducing SIM 05 trim at t=0
for m in (637.1595, 637.16, 637.26):
    th = math.radians(2.6389261150480663); FZ = -20401.300545620466
    print(f"m={m}: g_eff = -FZ/(m cos theta) = {-FZ/(m*math.cos(th)):.5f} ft/s^2")
