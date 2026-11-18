# stdlib-only: inter-tool spread for NESC atmospheric cases 11, 13.1-13.4
import csv, math, sys, os
sys.stdout.reconfigure(encoding='utf-8')
ROOT = sys.argv[1]
CASES = [("11","Atmos_11_TrimCheckSubsonicF16",180.0),
         ("13p1","Atmos_13p1_SubsonicAltitudeChangeF16",20.0),
         ("13p2","Atmos_13p2_SubsonicAirspeedChangeF16",20.0),
         ("13p3","Atmos_13p3_SubsonicHeadingChangeF16",30.0),
         ("13p4","Atmos_13p4_SubsonicLateralSideStepF16",60.0)]
A = 20925646.325  # WGS-84 a, ft
F = 1/298.257223563
E2 = F*(2-F)
def load(path):
    with open(path, newline='') as f:
        r = csv.reader(f); hdr = next(r)
        rows = []
        for row in r:
            if not row or not row[0].strip(): continue
            d = {}
            for h, v in zip(hdr, row):
                if h in d: continue  # duplicate feVelocity_ft_s_Z column in sim_05
                try: d[h] = float(v)
                except ValueError: pass
            rows.append(d)
    return hdr, rows
def interp(rows, t, key):
    # rows sorted by time; linear interpolation
    lo, hi = 0, len(rows)-1
    if t <= rows[0]['time']: return rows[0].get(key)
    if t >= rows[-1]['time']: return rows[-1].get(key) if abs(t-rows[-1]['time'])<1e-3 else None
    while hi-lo > 1:
        m = (lo+hi)//2
        if rows[m]['time'] <= t: lo = m
        else: hi = m
    a, b = rows[lo], rows[hi]
    if key not in a or key not in b: return None
    w = (t-a['time'])/(b['time']-a['time'])
    return a[key]+(b[key]-a[key])*w
def derived(rows, t, lat0, lon0, h0):
    g = lambda k: interp(rows, t, k)
    vn, ve, vd = g('feVelocity_ft_s_X'), g('feVelocity_ft_s_Y'), g('feVelocity_ft_s_Z')
    psi, th, ph = [math.radians(g(k)) for k in ('eulerAngle_deg_Yaw','eulerAngle_deg_Pitch','eulerAngle_deg_Roll')]
    # NED -> body (3-2-1)
    cps, sps, cth, sth, cph, sph = math.cos(psi), math.sin(psi), math.cos(th), math.sin(th), math.cos(ph), math.sin(ph)
    u = cth*cps*vn + cth*sps*ve - sth*vd
    v = (sph*sth*cps-cph*sps)*vn + (sph*sth*sps+cph*cps)*ve + sph*cth*vd
    w = (cph*sth*cps+sph*sps)*vn + (cph*sth*sps-sph*cps)*ve + cph*cth*vd
    V = math.sqrt(vn*vn+ve*ve+vd*vd)
    lat, lon, h = g('latitude_deg'), g('longitude_deg'), g('altitudeMsl_ft')
    phi0 = math.radians(lat0)
    RN = A/math.sqrt(1-E2*math.sin(phi0)**2); RM = RN*(1-E2)/(1-E2*math.sin(phi0)**2)
    north = math.radians(lat-lat0)*(RM+h0); east = math.radians(lon-lon0)*(RN+h0)*math.cos(phi0)
    return dict(V=V, alpha=math.degrees(math.atan2(w,u)), beta=math.degrees(math.asin(v/V)),
                chi=math.degrees(math.atan2(ve,vn)), north=north, east=east, h=h,
                psi=g('eulerAngle_deg_Yaw'), theta=g('eulerAngle_deg_Pitch'), phi=g('eulerAngle_deg_Roll'),
                p=g('bodyAngularRateWrtEi_deg_s_Roll'), q=g('bodyAngularRateWrtEi_deg_s_Pitch'), r=g('bodyAngularRateWrtEi_deg_s_Yaw'),
                FX=g('aero_bodyForce_lbf_X'), FY=g('aero_bodyForce_lbf_Y'), FZ=g('aero_bodyForce_lbf_Z'), vd=vd)
KEYS = ['V','alpha','beta','theta','phi','psi','chi','h','p','q','r','FX','FZ','vd']
for cid, folder, T in CASES:
    sims = {}
    for s in ('02','04','05'):
        p = os.path.join(ROOT, folder, f"Atmos_{cid.replace('p','p')}_sim_{s}.csv")
        hdr, rows = load(p)
        sims[s] = rows
    print(f"\n===== case {cid}  spec duration {T}s")
    for s, rows in sims.items():
        dt = rows[1]['time']-rows[0]['time']
        print(f"  SIM {s}: n={len(rows)} t_end={rows[-1]['time']:.3f} dt={dt:.4f}")
    lat0, lon0, h0 = 36.01916667, -75.67444444, 10013.0
    grid = [round(0.1*i,1) for i in range(int(T*10)+1)]
    D = {s: [derived(rows, t, lat0, lon0, h0) for t in grid] for s, rows in sims.items()}
    # values at t=0 and t=T
    for tt in (0, len(grid)-1):
        print(f"  t={grid[tt]:6.1f}: " + " | ".join(f"S{s}: V={D[s][tt]['V']:.3f} a={D[s][tt]['alpha']:.4f} b={D[s][tt]['beta']:.4f} th={D[s][tt]['theta']:.4f} ph={D[s][tt]['phi']:.4f} psi={D[s][tt]['psi']:.4f} chi={D[s][tt]['chi']:.4f} h={D[s][tt]['h']:.2f}" for s in D))
    for a, b in (('04','05'), ('02','05'), ('02','04')):
        mx = {k: max(abs(D[a][i][k]-D[b][i][k]) for i in range(len(grid)) if D[a][i][k] is not None and D[b][i][k] is not None) for k in KEYS}
        endpos = math.sqrt((D[a][-1]['north']-D[b][-1]['north'])**2 + (D[a][-1]['east']-D[b][-1]['east'])**2 + (D[a][-1]['h']-D[b][-1]['h'])**2)
        print(f"  max|SIM{a}-SIM{b}| over 0..{T}: " + ", ".join(f"{k}={mx[k]:.4g}" for k in KEYS) + f"; end 3D pos diff={endpos:.2f} ft")
    # case-specific response metrics
    for s in D:
        d = D[s]
        hs = [x['h'] for x in d]; Vs = [x['V'] for x in d]; chis = [x['chi'] for x in d]; phis = [x['phi'] for x in d]
        # lateral offset relative to initial 45 deg course line (right positive)
        lat_off = [(-x['north']*math.sin(math.radians(45))+x['east']*math.cos(math.radians(45))) for x in d]
        print(f"  SIM{s}: h[min,max,end]=({min(hs):.2f},{max(hs):.2f},{hs[-1]:.2f}) V[min,max,end]=({min(Vs):.3f},{max(Vs):.3f},{Vs[-1]:.3f}) chi_end={chis[-1]:.3f} phi[min,max]=({min(phis):.3f},{max(phis):.3f}) latoff_end={lat_off[-1]:.1f} ft max_q={max(abs(x['q']) for x in d):.3f} max_p={max(abs(x['p']) for x in d):.3f} deg/s")
