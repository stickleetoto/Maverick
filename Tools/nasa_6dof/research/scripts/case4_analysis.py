import csv, sys, os, math
D = sys.argv[1]
FT = 0.3048; SLUG = 14.593902937206364; SLUGFT3 = SLUG/FT**3
def load(n):
    with open(os.path.join(D, 'Atmos_04_sim_%02d.csv' % n), newline='') as fh:
        r = list(csv.reader(fh))
    hdr = r[0]; rows = [list(map(float, x)) for x in r[1:] if x]
    out = {}
    for i, h in enumerate(hdr):
        if h not in out: out[h] = [x[i] for x in rows]
    return out
sims = {n: load(n) for n in (2, 4, 5, 6)}
def grid(s):
    t = s['time']
    if len(t) > 400:
        idx = [10*k for k in range(301)]
    else:
        idx = list(range(301))
    for k, j in enumerate(idx):
        assert abs(t[j] - 0.1*k) < 1e-6, (k, t[j])
    return {h: [v[j] for j in idx] for h, v in s.items()}
G = {n: grid(s) for n, s in sims.items()}
def angdiff(a, b):
    return (a-b+180) % 360 - 180
sigs = ['altitudeMsl_ft','feVelocity_ft_s_Z','localGravity_ft_s2','airDensity_slug_ft3','mach','dynamicPressure_lbf_ft2',
        'eulerAngle_deg_Yaw','eulerAngle_deg_Pitch','eulerAngle_deg_Roll','bodyAngularRateWrtEi_deg_s_Roll',
        'aero_bodyForce_lbf_X','aero_bodyForce_lbf_Y','aero_bodyForce_lbf_Z','eiPosition_ft_X','latitude_deg','feVelocity_ft_s_X']
print('== inter-tool max |diff| over 0..30 s (0.1 s grid)')
pairs = [(4,5),(4,6),(5,6),(2,4),(2,5)]
for sg in sigs:
    line = '%-34s' % sg
    for a,b in pairs:
        if sg in G[a] and sg in G[b]:
            if 'euler' in sg:
                m = max(abs(angdiff(x,y)) for x,y in zip(G[a][sg],G[b][sg]))
            else:
                m = max(abs(x-y) for x,y in zip(G[a][sg],G[b][sg]))
            line += ' %d-%d:%.3e' % (a,b,m)
        else: line += ' %d-%d:   n/a   ' % (a,b)
    print(line)
print('== final values t=30')
for n in (2,4,5,6):
    g = G[n]; print(n, {k: g[k][-1] for k in ('altitudeMsl_ft','feVelocity_ft_s_Z','localGravity_ft_s2','airDensity_slug_ft3','eulerAngle_deg_Yaw','eulerAngle_deg_Pitch','eulerAngle_deg_Roll') if k in g})

MU = 3.986004418e14; RS = 6371007.1809; RE_US76 = 6356766.0; G0 = 9.80665
m = 1.0*SLUG; S = 0.1963495*FT*FT; CD = 0.1; h0 = 30000*FT
def us76(hgeom, geopot=True, R=287.0531):
    h = RE_US76*hgeom/(RE_US76+hgeom) if geopot else hgeom
    T = 288.15 - 0.0065*h
    p = 101325.0*(T/288.15)**(-G0/(-0.0065*R))
    return p/(R*T), T, p
def solve(gmode, geopot, h_step=0.001, R=287.0531, scheme='rk4'):
    g_const = MU/(RS+h0)**2
    def g(h): return MU/(RS+h)**2 if gmode=='inv' else (g_const if gmode=='const_h0' else G0)
    def f(h, v):
        rho = us76(h, geopot, R)[0]
        a = g(h) - 0.5*rho*v*abs(v)*S*CD/m
        return -v, a
    h, v = h0, 0.0
    out = {0: (h, v)}
    n = int(round(30.0/h_step)); every = int(round(0.1/h_step))
    for k in range(1, n+1):
        if scheme == 'rk4':
            k1 = f(h, v); k2 = f(h+0.5*h_step*k1[0], v+0.5*h_step*k1[1])
            k3 = f(h+0.5*h_step*k2[0], v+0.5*h_step*k2[1]); k4 = f(h+h_step*k3[0], v+h_step*k3[1])
            h += h_step/6*(k1[0]+2*k2[0]+2*k3[0]+k4[0]); v += h_step/6*(k1[1]+2*k2[1]+2*k3[1]+k4[1])
        else:
            _, a = f(h, v); v += a*h_step; h -= v*h_step
        if k % every == 0: out[k//every] = (h, v)
    return out
print('== density check at 30000 ft (slug/ft3): R=287.0531 geopot %.12e  R=287.05287 %.12e  geometric %.12e' % (
    us76(h0)[0]/SLUGFT3, us76(h0,True,287.05287)[0]/SLUGFT3, us76(h0,False)[0]/SLUGFT3))
print('   NASA rho t0: sim4 %.12e sim5 %.12e sim6 %.12e' % (G[4]['airDensity_slug_ft3'][0], G[5]['airDensity_slug_ft3'][0], G[6]['airDensity_slug_ft3'][0]))
print('   g at h0 ft/s2 %.10f  NASA sim5 %.10f' % (MU/(RS+h0)**2/FT, G[5]['localGravity_ft_s2'][0]))
print('   r0 ft %.4f  NASA eiPosition_X sim5 %.4f' % ((RS+h0)/FT, G[5]['eiPosition_ft_X'][0]))
ref = solve('inv', True, 0.001)
ref2 = solve('inv', True, 0.0005)
print('== reference self-check (h=1ms vs 0.5ms): max|dh| ft %.3e max|dv| ft/s %.3e' % (
    max(abs(ref[k][0]-ref2[k][0]) for k in ref)/FT, max(abs(ref[k][1]-ref2[k][1]) for k in ref)/FT))
def cmp(r, label):
    line = label
    for n in (4,5,6,2):
        dh = max(abs(r[k][0]/FT - G[n]['altitudeMsl_ft'][k]) for k in range(301))
        dv = max(abs(r[k][1]/FT - G[n]['feVelocity_ft_s_Z'][k]) for k in range(301))
        line += ' | sim%d dh %.4f ft dv %.5f ft/s' % (n, dh, dv)
    print(line)
cmp(ref, 'NASA-equiv ref (inv-sq, US76 geopot R=287.0531)')
cmp(solve('inv', True, 0.001, 287.05287), 'NASA-equiv ref, R=287.05287')
variants = {
 'F1 const g(h0), US76 geopot': solve('const_h0', True),
 'F2 const 9.80665, geometric-atm': solve('g0', False),
 'F3 const g(h0), geometric-atm': solve('const_h0', False),
 'B  inv-sq g, geometric-atm': solve('inv', False),
}
print('== model-convention effects vs NASA-equiv reference (max over 30 s and value at 30 s)')
for k, r in variants.items():
    dh = [ (r[i][0]-ref[i][0])/FT for i in range(301)]; dv = [ (r[i][1]-ref[i][1])/FT for i in range(301)]
    print('%-34s max|dh| %.3f ft (t30 %+.3f ft)  max|dv| %.4f ft/s (t30 %+.4f ft/s)' % (k, max(map(abs,dh)), dh[-1], max(map(abs,dv)), dv[-1]))
print('== semi-implicit Euler vs RK4 for model B (expected Maverick integration error, double precision)')
rb = variants['B  inv-sq g, geometric-atm']
prev = None
for dt in (0.02, 0.01, 0.005, 0.0025):
    se = solve('inv', False, dt, scheme='se')
    dh = max(abs(se[i][0]-rb[i][0]) for i in range(301)); dv = max(abs(se[i][1]-rb[i][1]) for i in range(301))
    order = '' if prev is None else ' order_h %.3f order_v %.3f' % (math.log(prev[0]/dh,2), math.log(prev[1]/dv,2))
    prev = (dh, dv)
    print('dt %.4f  max|dh| %.4f m (%.3f ft)  max|dv| %.5f m/s%s' % (dt, dh, dh/FT, dv, order))
def fall_nodrag(gmode):
    h, v = h0, 0.0; hs = 1e-3
    for k in range(30000):
        gg = lambda hh: MU/(RS+hh)**2 if gmode=='inv' else MU/(RS+h0)**2
        hm = h - 0.5*hs*v; vm = v + 0.5*hs*gg(h)
        h -= hs*vm; v += hs*gg(hm)
    return h, v
a = fall_nodrag('inv'); b = fall_nodrag('const')
print('== drag-free 30 s: inv-sq vs const g(h0): dh %.3f ft, dv %.4f ft/s; drop %.1f ft' % ((a[0]-b[0])/FT, (a[1]-b[1])/FT, (h0-a[0])/FT))
for hft in (30000, 16231.3):
    print('g at %g ft: %.6f ft/s2' % (hft, MU/(RS+hft*FT)**2/FT))
w = [math.radians(x) for x in (10,20,30)]; wn = math.sqrt(sum(x*x for x in w))
def euler_at(t):
    ang = wn*t; ax = [x/wn for x in w]
    q0 = math.cos(ang/2); q1, q2, q3 = [math.sin(ang/2)*x for x in ax]
    yaw = math.degrees(math.atan2(2*(q1*q2+q0*q3), q0*q0+q1*q1-q2*q2-q3*q3))
    pitch = math.degrees(math.asin(max(-1,min(1,-2*(q1*q3-q0*q2)))))
    roll = math.degrees(math.atan2(2*(q2*q3+q0*q1), q0*q0-q1*q1-q2*q2+q3*q3))
    return yaw, pitch, roll
print('== analytic attitude (fixed-axis rotation) vs tools, max |d| deg over run')
for n in (4,5,6,2):
    mx = 0
    for k in range(301):
        e = euler_at(k*0.1)
        for j, nm in enumerate(('Yaw','Pitch','Roll')):
            mx = max(mx, abs(angdiff(e[j], G[n]['eulerAngle_deg_'+nm][k])))
    print(' sim%d %.3e deg' % (n, mx))
print(' analytic t=30', euler_at(30.0), ' |w| deg/s', math.degrees(wn))
# body-axis drag reconstruction check vs sim5 at t=30 using its own euler + qbar
k = 300; g5 = G[5]
qbar = g5['dynamicPressure_lbf_ft2'][k]; Dmag = qbar*0.1963495*0.1
print('== sim5 t=30 |aero force| %.6f lbf vs qbar*S*CD %.6f lbf' % (math.sqrt(sum(g5['aero_bodyForce_lbf_'+a][k]**2 for a in 'XYZ')), Dmag))
