import sys, os, math, struct, importlib.util
here = os.path.dirname(os.path.abspath(__file__))
# reuse loader by exec of the analysis module's top part would print; reimplement minimal pieces
import csv
D = sys.argv[1]
FT = 0.3048; SLUG = 14.593902937206364
def load(n):
    with open(os.path.join(D, 'Atmos_04_sim_%02d.csv' % n), newline='') as fh:
        r = list(csv.reader(fh))
    hdr = r[0]; rows = [list(map(float, x)) for x in r[1:] if x]
    out = {}
    for i, h in enumerate(hdr):
        if h not in out: out[h] = [x[i] for x in rows]
    if len(out['time']) > 400:
        out = {h: v[::10] for h, v in out.items()}
    return out
S4, S5, S6 = load(4), load(5), load(6)
MU = 3.986004418e14; RS = 6371007.1809; RE76 = 6356766.0; G0 = 9.80665
m = SLUG; S = 0.1963495*FT*FT; CD = 0.1; h0 = 30000*FT
def rho(h, geo=True):
    hh = RE76*h/(RE76+h) if geo else h
    T = 288.15-0.0065*hh
    return 101325.0*(T/288.15)**(G0/(0.0065*287.0531))/(287.0531*T)
def f(h, v, geo=True, inv=True):
    g = MU/(RS+h)**2 if inv else MU/(RS+h0)**2
    return -v, g-0.5*rho(h, geo)*v*abs(v)*S*CD/m
def rk4(geo=True, inv=True, hs=0.001):
    h, v = h0, 0.0; out = [(h, v)]
    for k in range(1, 30001):
        k1 = f(h, v, geo, inv); k2 = f(h+0.5*hs*k1[0], v+0.5*hs*k1[1], geo, inv)
        k3 = f(h+0.5*hs*k2[0], v+0.5*hs*k2[1], geo, inv); k4 = f(h+hs*k3[0], v+hs*k3[1], geo, inv)
        h += hs/6*(k1[0]+2*k2[0]+2*k3[0]+k4[0]); v += hs/6*(k1[1]+2*k2[1]+2*k3[1]+k4[1])
        if k % 100 == 0: out.append((h, v))
    return out
ref = rk4()
# pointwise envelope rule (R1 style) for altitude and vD
for name, key, conv in (('alt', 'altitudeMsl_ft', lambda s: s[0]/FT), ('vD', 'feVelocity_ft_s_Z', lambda s: s[1]/FT)):
    fails = 0; worst = 0
    for k in range(1, 301):
        dr = abs(conv(ref[k]) - S4[key][k]); env = max(abs(S5[key][k]-S4[key][k]), abs(S6[key][k]-S4[key][k])) + 1e-9
        if dr > env: fails += 1; worst = max(worst, dr/env)
    print('envelope rule %s: samples outside %d/300, worst ratio %.3f' % (name, fails, worst))
# float32 emulation of PhysX-like semi-implicit Euler for model B
def f32(x): return struct.unpack('f', struct.pack('f', x))[0]
B = rk4(geo=False)
for dt in (0.02, 0.01, 0.005, 0.0025):
    n = int(round(30/dt)); every = int(round(0.1/dt))
    for prec in ('f64', 'f32'):
        q = f32 if prec == 'f32' else (lambda x: x)
        y, vy = q(h0), 0.0  # vy is upward velocity (Unity +y)
        mx_h = mx_v = 0.0
        for k in range(1, n+1):
            _, a = f(y, -vy, geo=False)
            vy = q(vy + q(-a*dt)); y = q(y + q(vy*dt))
            if k % every == 0:
                j = k // every
                mx_h = max(mx_h, abs(y - B[j][0])); mx_v = max(mx_v, abs(-vy - B[j][1]))
        print('dt %.4f %s  max|dh| %.4f m  max|dv| %.6f m/s' % (dt, prec, mx_h, mx_v))
# sensitivity control: inverse-square vs constant g(h0), same geometric atmosphere
Cst = rk4(geo=False, inv=False)
print('control: const-g(h0) minus inv-sq at 30 s: dh %.3f ft, dv %.4f ft/s; max|dv| %.4f ft/s' % (
    (Cst[300][0]-B[300][0])/FT, (Cst[300][1]-B[300][1])/FT, max(abs(Cst[k][1]-B[k][1]) for k in range(301))/FT))
# relative density difference geometric vs geopotential across the run
print('density rel diff geometric vs geopotential: at 30000 ft %.4e, at 16231 ft %.4e' % (
    rho(h0, False)/rho(h0)-1, rho(16231*FT, False)/rho(16231*FT)-1))
# drag-to-weight at end
h, v = ref[300]
print('drag/weight at 30 s: %.4f; terminal speed there %.1f ft/s' % (0.5*rho(h)*v*v*S*CD/(m*MU/(RS+h)**2), math.sqrt(2*m*MU/(RS+h)**2/(rho(h)*S*CD))/FT))
