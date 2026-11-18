import csv, sys, os, glob
base = sys.argv[1]
for case in sys.argv[2:]:
    d = glob.glob(os.path.join(base, case+'*'))[0]
    print('#####', os.path.basename(d))
    for f in sorted(glob.glob(os.path.join(d,'*.csv'))):
        with open(f, newline='') as fh:
            r = list(csv.reader(fh))
        hdr = r[0]; rows = [list(map(float,x)) for x in r[1:] if x]
        t = [x[0] for x in rows]
        dt = t[1]-t[0]
        def col(name):
            if name in hdr: i = hdr.index(name); return [x[i] for x in rows]
            return None
        alt = col('altitudeMsl_ft'); vz = col('feVelocity_ft_s_Z'); g = col('localGravity_ft_s2'); M=col('mach'); rho=col('airDensity_slug_ft3')
        print(os.path.basename(f), 'ncols',len(hdr),'nrows',len(rows),'t0',t[0],'tend',t[-1],'dt',round(dt,6),
              'dup-cols', [h for h in set(hdr) if hdr.count(h)>1])
        def at(c, tt):
            if c is None: return None
            best = min(range(len(t)), key=lambda k: abs(t[k]-tt)); return c[best]
        for tt in (0,10,20,30):
            print('   t=%g alt=%s vD=%s g=%s mach=%s rho=%s'%(tt, at(alt,tt), at(vz,tt), at(g,tt), at(M,tt), at(rho,tt)))
