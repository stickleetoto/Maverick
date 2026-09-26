"""Vector extraction of NASA/TM-2008-214634 figures 12 and 13 (baseline = closed black symbols).

Positions come straight from the PDF path geometry - no raster reading. The axis calibration uses
the plot's own grid lines, whose values are the printed tick labels.

Usage: python vec_extract.py 20080015840.pdf tm2008_fig12_13.json
"""
import json
import sys
import pymupdf

PDF = sys.argv[1]
OUT = sys.argv[2]

BLACK = (0.0, 0.0, 0.0)


def is_black(c):
    return c is not None and all(abs(a - b) < 0.02 for a, b in zip(c, BLACK))


def is_blue(c):
    return c is not None and c[2] > 0.9 and c[0] < 0.1 and c[1] < 0.1


def marker_shape(dr):
    """Classify one black marker drawing. Returns (shape, cx, cy, halfheight) or None."""
    items = dr['items']
    r = dr['rect']
    kinds = ''.join(it[0] for it in items)
    cx = (r.x0 + r.x1) / 2.0
    cy = (r.y0 + r.y1) / 2.0
    w = r.x1 - r.x0
    h = r.y1 - r.y0
    if kinds == 'cccc' and 2.5 < w < 5 and 2.5 < h < 5:
        return ('circle', cx, cy, h / 2)
    if kinds == 'llll' and 2.5 < w < 5 and 3 < h < 6:
        return ('diamond', cx, cy, h / 2)
    if kinds == 'lll' and 2.5 < w < 5 and 2.5 < h < 5:
        pts = [items[0][1], items[1][1], items[2][1]]
        # the apex is the vertex farthest from the opposite side's line; classify by where the
        # lone vertex sits relative to the bbox
        xs = sorted(p.x for p in pts)
        ys = sorted(p.y for p in pts)
        # two vertices share a y (flat top/bottom) -> up/down; share an x -> left/right
        if abs(ys[0] - ys[1]) < 0.05 or abs(ys[1] - ys[2]) < 0.05:
            flat_at_bottom = abs(ys[1] - ys[2]) < 0.05
            return ('triangle_up' if flat_at_bottom else 'triangle_down', cx, cy, h / 2)
        if abs(xs[0] - xs[1]) < 0.05:
            return ('triangle_right', cx, cy, h / 2)
        return ('triangle_left', cx, cy, h / 2)
    if kinds == 'l' and dr.get('width') and dr['width'] > 2.5:
        # a filled square drawn as a thick vertical stroke
        (p0, p1) = items[0][1], items[0][2]
        return ('square', (p0.x + p1.x) / 2.0, (p0.y + p1.y) / 2.0, dr['width'] / 2.0)
    return None


ALTITUDE_BY_SHAPE = {
    'circle': 15000,
    'square': 25000,
    'triangle_up': 30000,
    'diamond': 35000,
    'triangle_down': 40000,
    'triangle_right': 45000,
}


def grid(page):
    """Return (xs, ys) of the grey grid lines on the page."""
    for dr in page.get_drawings():
        c = dr.get('color')
        if c and abs(c[0] - 0.65) < 0.02 and dr['type'] == 's' and len(dr['items']) >= 10:
            xs = sorted({round(it[1].x, 3) for it in dr['items'] if abs(it[1].x - it[2].x) < 0.01})
            ys = sorted({round(it[1].y, 3) for it in dr['items'] if abs(it[1].y - it[2].y) < 0.01})
            return xs, ys, dr
    raise SystemExit('no grid')


def split_subpaths(dr):
    """Split a stroke's items into continuous subpaths."""
    subs = []
    cur = []
    last = None
    for it in dr['items']:
        if it[0] == 'l':
            a, b = it[1], it[2]
        elif it[0] == 'c':
            a, b = it[1], it[4]
        else:
            continue
        if last is None or abs(last.x - a.x) > 0.05 or abs(last.y - a.y) > 0.05:
            if cur:
                subs.append(cur)
            cur = [(a.x, a.y)]
        cur.append((b.x, b.y))
        last = b
    if cur:
        subs.append(cur)
    return subs


def extract(page_index, fig, x_grid_vals, y_axis):
    """y_axis = (y_value_at_top_edge, step_per_grid_line) listed top->bottom."""
    doc = pymupdf.open(PDF)
    page = doc[page_index]
    gx, gy, gdr = grid(page)
    # Plot edges from the grid lines' own endpoints (the frame's stroke rect is inflated by half its
    # line width). Horizontal grid lines run edge to edge in x; vertical ones edge to edge in y.
    hl = [it for it in gdr['items'] if abs(it[1].y - it[2].y) < 0.01]
    vl = [it for it in gdr['items'] if abs(it[1].x - it[2].x) < 0.01]
    left = min(min(it[1].x, it[2].x) for it in hl)
    right = max(max(it[1].x, it[2].x) for it in hl)
    top = min(min(it[1].y, it[2].y) for it in vl)
    bottom = max(max(it[1].y, it[2].y) for it in vl)
    gx = [v for v in gx if v - left > 0.5 and right - v > 0.5]
    gy = [v for v in gy if v - top > 0.5 and bottom - v > 0.5]
    frame = pymupdf.Rect(left, top, right, bottom)
    xs = [left] + gx + [right]
    x_vals = x_grid_vals
    assert len(xs) == len(x_vals), (len(xs), len(x_vals))
    # least squares linear fit (the lines are equally spaced; the fit reports residual)
    n = len(xs)
    mx = sum(xs) / n
    mv = sum(x_vals) / n
    sx = sum((a - mx) * (b - mv) for a, b in zip(xs, x_vals)) / sum((a - mx) ** 2 for a in xs)
    x0 = mv - sx * mx
    xres = max(abs(x0 + sx * a - b) for a, b in zip(xs, x_vals))
    ys = [top] + gy + [bottom]
    top_val, step = y_axis
    y_vals = [top_val + i * step for i in range(len(ys))]
    my = sum(ys) / len(ys)
    myv = sum(y_vals) / len(y_vals)
    sy = sum((a - my) * (b - myv) for a, b in zip(ys, y_vals)) / sum((a - my) ** 2 for a in ys)
    y0 = myv - sy * my
    yres = max(abs(y0 + sy * a - b) for a, b in zip(ys, y_vals))

    def to_data(px, py):
        return (x0 + sx * px, y0 + sy * py)

    points = []
    trends = []
    legend_x = right + 2  # legend markers sit right of the frame
    for i, dr in enumerate(page.get_drawings()):
        r = dr['rect']
        if r.x0 > legend_x:
            continue
        if dr['type'] in ('f', 'fs') and is_black(dr.get('fill')):
            m = marker_shape(dr)
            if m:
                points.append((i,) + m)
        elif dr['type'] == 's' and is_black(dr.get('color')) and dr.get('width', 0) and abs(dr['width'] - 2.93) < 0.05:
            m = marker_shape(dr)
            if m:
                points.append((i,) + m)
        elif dr['type'] == 's' and is_black(dr.get('color')) and not dr.get('dashes', '').strip('[] 0') \
                and len(dr['items']) > 30 and all(it[0] in 'lc' for it in dr['items']):
            for sub in split_subpaths(dr):
                if len(sub) >= 8:
                    trends.append([to_data(px, py) for px, py in sub])

    out = []
    for (i, shape, cx, cy, hh) in points:
        m, v = to_data(cx, cy)
        _, v_hi = to_data(cx, cy - hh)
        m_lo, _ = to_data(cx - hh, cy)
        out.append({
            'drawing': i,
            'shape': shape,
            'altitudeFt': ALTITUDE_BY_SHAPE[shape],
            'mach': round(m, 4),
            'value': v,
            'halfMarkerMach': round(abs(m - m_lo), 4),
            'halfMarkerValue': abs(v_hi - v),
        })
    out.sort(key=lambda d: (d['altitudeFt'], d['mach']))
    return {
        'figure': fig,
        'page_index': page_index,
        'frame': [frame.x0, frame.y0, frame.x1, frame.y1],
        'x_scale_per_pt': sx,
        'y_scale_per_pt': sy,
        'x_calibration_max_residual': xres,
        'y_calibration_max_residual': yres,
        'points': out,
        'trends': trends,
    }


res12 = extract(18, 12, [0.4, 0.6, 0.8, 1.0, 1.2, 1.4, 1.6, 1.8, 2.0], (0.0045, -0.0005))
res13 = extract(19, 13, [0.4, 0.6, 0.8, 1.0, 1.2, 1.4, 1.6, 1.8, 2.0], (-0.005, -0.005))
json.dump({'fig12': res12, 'fig13': res13}, open(OUT, 'w'), indent=1)
for r in (res12, res13):
    print('Figure', r['figure'], 'frame', [round(v, 2) for v in r['frame']],
          'dx/pt', r['x_scale_per_pt'], 'dy/pt', r['y_scale_per_pt'],
          'calres', r['x_calibration_max_residual'], r['y_calibration_max_residual'])
    for p in r['points']:
        print('  %-15s %6d  M=%.4f  v=%.6f  (+/- %.4f M, %.2e)' % (
            p['shape'], p['altitudeFt'], p['mach'], p['value'], p['halfMarkerMach'], p['halfMarkerValue']))
    for t in r['trends']:
        print('  trend', len(t), 'pts  M %.3f..%.3f' % (t[0][0], t[-1][0]),
              ' v %.5f..%.5f' % (t[0][1], t[-1][1]))
