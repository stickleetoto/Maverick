"""Digitize a stack of time-history panels sharing one time axis (TM-2008-214634 figs. 14-15).

Writes JSON and an overlay PNG (a ring on every kept sample) for visual verification.

Usage: python digitize_panels.py fig14.json
"""
import json
import sys
import numpy as np
from PIL import Image, ImageDraw
from raster_digitize import load, grey_lines, trace_columns, sample, group


def fit(points):
    """Least squares value = a + b*pixel over (pixel, value) pairs; returns (fn, maxres_px, |dv/dpx|)."""
    p = np.array([q[0] for q in points], float)
    v = np.array([q[1] for q in points], float)
    b, a = np.polyfit(p, v, 1)
    res_px = np.max(np.abs((a + b * p - v) / b)) if len(p) > 2 else 0.0
    return (lambda x: a + b * x), float(res_px), float(abs(b))


def digitize(spec):
    im = load(spec['image'])
    out = {'image': spec['image'], 'panels': []}
    overlay = Image.open(spec['image']).convert('RGB')
    dr = ImageDraw.Draw(overlay)

    # time axis: vertical grid lines at the printed interior labels
    tx = spec['time_axis']
    p0 = spec['panels'][0]
    vlines = grey_lines(im, tx['x0'], tx['x1'], p0['y0'], p0['y1'], 'v')
    if len(vlines) != len(tx['grid_values']):
        raise SystemExit('time grid mismatch %s vs %s' % (vlines, tx['grid_values']))
    tpts = list(zip(vlines, tx['grid_values']))
    t_of_x, t_res_px, dt_dpx = fit(tpts)
    x_of_t, _, _ = fit([(v, p) for p, v in tpts])
    out['time'] = {'grid_px': vlines, 'max_residual_px': t_res_px, 'seconds_per_px': dt_dpx,
                   'frame_left_t': t_of_x(tx['x0']), 'frame_right_t': t_of_x(tx['x1'])}

    step = spec.get('dt', 0.1)
    t_lo = max(tx['t_min'], t_of_x(tx['x0'] + 4))
    t_hi = min(tx['t_max'], t_of_x(tx['x1'] - 4))
    n0 = int(np.ceil(t_lo / step - 1e-9))
    n1 = int(np.floor(t_hi / step + 1e-9))
    ts = [round(k * step, 4) for k in range(n0, n1 + 1)]

    for ps in spec['panels']:
        hl = grey_lines(im, tx['x0'], tx['x1'], ps['y0'], ps['y1'], 'h')
        if len(hl) != len(ps['grid_values']):
            raise SystemExit('panel %s grid mismatch %s vs %s' % (ps['name'], hl, ps['grid_values']))
        pts = list(zip(hl, ps['grid_values'])) + [(ps['y0'], ps['top']), (ps['y1'], ps['bottom'])]
        v_of_y, res_px, dv_dpx = fit(pts)
        y_of_v, _, _ = fit([(v, p) for p, v in pts])
        panel = {'name': ps['name'], 'units': ps['units'], 'grid_px': hl,
                 'calibration_max_residual_px': res_px, 'value_per_px': dv_dpx, 'traces': []}
        for tr in ps['traces']:
            cols = trace_columns(im, tx['x0'], tx['x1'], ps['y0'], ps['y1'], tr['colour'],
                                 inset=tr.get('inset', 3))
            smp = sample(cols, x_of_t, v_of_y, ts, max_gap_px=tr.get('max_gap_px', 3),
                         max_spread_px=tr.get('max_spread_px', 10))
            kept = [s for s in smp if s is not None]
            panel['traces'].append({
                'name': tr['name'], 'colour': tr['colour'], 'kind': tr['kind'],
                'samples': [[s[0], round(s[1], 5), s[2]] for s in kept],
                'omitted_times': [t for t, s in zip(ts, smp) if s is None],
            })
            rgb = {'blue': (255, 0, 255), 'green': (255, 128, 0), 'red': (0, 0, 0),
                   'orange': (0, 0, 255), 'black': (255, 0, 0)}[tr['colour']]
            for s in kept:
                x = x_of_t(s[0])
                y = y_of_v(s[1])
                dr.ellipse([x - 2, y - 2, x + 2, y + 2], outline=rgb)
        out['panels'].append(panel)
    overlay.save(spec['overlay'])
    return out


if __name__ == '__main__':
    spec = json.load(open(sys.argv[1]))
    res = digitize(spec)
    json.dump(res, open(spec['output'], 'w'), indent=1)
    print('time grid', [round(v, 1) for v in res['time']['grid_px']], 'res px %.2f' % res['time']['max_residual_px'],
          's/px %.5f' % res['time']['seconds_per_px'],
          'frame t %.3f..%.3f' % (res['time']['frame_left_t'], res['time']['frame_right_t']))
    for p in res['panels']:
        print(p['name'], 'grid', [round(v, 1) for v in p['grid_px']], 'res px %.2f' % p['calibration_max_residual_px'],
              'units/px %.5f' % p['value_per_px'])
        for t in p['traces']:
            flagged = sum(1 for s in t['samples'] if s[2])
            print('   %-28s n=%d omitted=%d flagged=%d  range %.3f..%.3f' % (
                t['name'], len(t['samples']), len(t['omitted_times']), flagged,
                min(s[1] for s in t['samples']), max(s[1] for s in t['samples'])))
