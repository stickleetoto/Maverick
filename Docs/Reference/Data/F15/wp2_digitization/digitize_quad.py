"""Digitize a 2x2 MATLAB-style time-history figure (TM-2012-215978 figs 24-29 layout).

Each panel carries its own frame and its own time calibration (the two columns of panels do not
share pixel x). Calibration is the frame edges at the printed axis limits PLUS the interior
dotted grid lines at the printed intermediate labels; the fit residual is reported.

Usage: python digitize_quad.py quad_27a.json
"""
import json
import sys
import numpy as np
from PIL import Image, ImageDraw
from raster_digitize import load, grey_lines, sample, group

MASKS = {
    'green': lambda r, g, b: (g - r > 18) & (g - b > 8) & (g < 215),
    'blue': lambda r, g, b: (b - g > 25) & (b - r > 15) & (b < 220),
}


def fit(points):
    p = np.array([q[0] for q in points], float)
    v = np.array([q[1] for q in points], float)
    b, a = np.polyfit(p, v, 1)
    res_px = float(np.max(np.abs((a + b * p - v) / b)))
    return (lambda x: a + b * x), res_px, float(abs(b))


def match_grid(found, expected_px, tol):
    """Pair each expected grid position with the nearest found line within tol."""
    pairs = []
    for e in expected_px:
        near = [f for f in found if abs(f - e) <= tol]
        if near:
            pairs.append(min(near, key=lambda f: abs(f - e)))
        else:
            pairs.append(None)
    return pairs


def columns(im, x0, x1, y0, y1, colour, inset=4):
    sub = im[y0 + inset:y1 - inset + 1, :]
    r, g, b = sub[..., 0], sub[..., 1], sub[..., 2]
    mask = MASKS[colour](r, g, b)
    cols = {}
    for x in range(x0 + inset, x1 - inset + 1):
        ys = np.nonzero(mask[:, x])[0]
        if len(ys):
            cols[x] = (float(np.median(ys)) + y0 + inset, len(ys), int(ys.min()) + y0 + inset, int(ys.max()) + y0 + inset)
    return cols


def digitize(spec):
    im = load(spec['image'])
    overlay = Image.open(spec['image']).convert('RGB')
    dr = ImageDraw.Draw(overlay)
    out = {'image': spec['image'], 'panels': []}
    step = spec.get('dt', 0.1)
    for ps in spec['panels']:
        x0, x1, y0, y1 = ps['frame']
        t0, t1 = ps.get('t_limits', spec['t_limits'])
        tgrid = ps.get('t_grid', spec['t_grid'])
        # time calibration
        rough_t = lambda x: t0 + (x - x0) * (t1 - t0) / (x1 - x0)
        rough_x = lambda t: x0 + (t - t0) * (x1 - x0) / (t1 - t0)
        vfound = grey_lines(im, x0, x1, y0, y1, 'v')
        vpairs = match_grid(vfound, [rough_x(t) for t in tgrid], 6)
        tpts = [(x0, t0), (x1, t1)] + [(p, t) for p, t in zip(vpairs, tgrid) if p is not None]
        t_of_x, t_res, dt_dpx = fit(tpts)
        x_of_t, _, _ = fit([(t, p) for p, t in tpts])
        # value calibration
        top, bottom = ps['limits']
        rough_y = lambda v: y0 + (top - v) * (y1 - y0) / (top - bottom)
        hfound = grey_lines(im, x0, x1, y0, y1, 'h')
        hpairs = match_grid(hfound, [rough_y(v) for v in ps['grid']], 6)
        vpts = [(y0, top), (y1, bottom)] + [(p, v) for p, v in zip(hpairs, ps['grid']) if p is not None]
        v_of_y, v_res, dv_dpx = fit(vpts)
        y_of_v, _, _ = fit([(v, p) for p, v in vpts])

        n0 = int(np.ceil((t_of_x(x0 + 5)) / step - 1e-9))
        n1 = int(np.floor((t_of_x(x1 - 5)) / step + 1e-9))
        ts = [round(k * step, 4) for k in range(max(n0, int(round(t0 / step))), n1 + 1)]
        panel = {'name': ps['name'], 'units': ps['units'], 'unit_scale_note': ps.get('note', ''),
                 'time_grid_found': sum(1 for p in vpairs if p is not None), 'time_grid_expected': len(tgrid),
                 'value_grid_found': sum(1 for p in hpairs if p is not None), 'value_grid_expected': len(ps['grid']),
                 'time_residual_px': t_res, 'value_residual_px': v_res,
                 'seconds_per_px': dt_dpx, 'value_per_px': dv_dpx, 'traces': []}
        for tr in ps['traces']:
            cols = columns(im, x0, x1, y0, y1, tr['colour'])
            smp = sample(cols, x_of_t, v_of_y, ts, max_gap_px=tr.get('max_gap_px', 4),
                         max_spread_px=tr.get('max_spread_px', 12))
            kept = [s for s in smp if s is not None and not s[2]]
            panel['traces'].append({'name': tr['name'], 'colour': tr['colour'], 'kind': tr['kind'],
                                    'samples': [[s[0], s[1]] for s in kept],
                                    'omitted': len(ts) - len(kept)})
            ring = (255, 0, 255) if tr['colour'] == 'green' else (255, 140, 0)
            for s in kept:
                x, y = x_of_t(s[0]), y_of_v(s[1])
                dr.ellipse([x - 3, y - 3, x + 3, y + 3], outline=ring)
        out['panels'].append(panel)
    overlay.save(spec['overlay'])
    return out


if __name__ == '__main__':
    spec = json.load(open(sys.argv[1]))
    res = digitize(spec)
    json.dump(res, open(spec['output'], 'w'), indent=1)
    for p in res['panels']:
        print('%-22s tgrid %d/%d res %.2fpx  vgrid %d/%d res %.2fpx  %.4f %s/px' % (
            p['name'], p['time_grid_found'], p['time_grid_expected'], p['time_residual_px'],
            p['value_grid_found'], p['value_grid_expected'], p['value_residual_px'], p['value_per_px'], p['units']))
        for t in p['traces']:
            v = [s[1] for s in t['samples']]
            print('     %-26s n=%d omitted=%d  %.4f..%.4f' % (t['name'], len(v), t['omitted'], min(v), max(v)))
