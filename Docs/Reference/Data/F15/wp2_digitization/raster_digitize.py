"""Raster time-history digitizer for NASA figures embedded as images.

Per panel: the frame is found from long dark pixel runs; the axis calibration maps the frame's
edges to the printed end labels (read from the rendered page) and is CHECKED against the interior
grid lines, which must fall on the printed intermediate labels. A trace is found by colour
classification column by column; a column with no pixel of the trace's colour is skipped, never
filled. Samples are taken at a fixed time step, using the nearest detected column within
+/- max_gap_px; beyond that the sample is omitted.
"""
import numpy as np
from PIL import Image


def load(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(int)


def dark_mask(im):
    return im.sum(axis=2) < 200


def frame_lines(im, min_frac_row=0.3, min_frac_col=0.15):
    H, W, _ = im.shape
    dark = dark_mask(im)
    rows = [y for y in range(H) if dark[y].sum() > W * min_frac_row]
    cols = [x for x in range(W) if dark[:, x].sum() > H * min_frac_col]
    return group(rows), group(cols)


def group(vals):
    """Group consecutive integers; return list of (first, last, centre)."""
    out = []
    for v in vals:
        if out and v - out[-1][1] <= 1:
            out[-1][1] = v
        else:
            out.append([v, v])
    return [(a, b, (a + b) / 2.0) for a, b in out]


def grey_lines(im, x0, x1, y0, y1, axis):
    """Positions of light-grey grid lines strictly inside a panel."""
    sub = im[y0 + 3:y1 - 2, x0 + 3:x1 - 2]
    r, g, b = sub[..., 0], sub[..., 1], sub[..., 2]
    grey = (abs(r - g) < 14) & (abs(g - b) < 14) & (r > 120) & (r < 225)
    if axis == 'h':
        frac = grey.mean(axis=1)
        idx = [i for i, f in enumerate(frac) if f > 0.25]
        return [c + y0 + 3 for (_, _, c) in group(idx)]
    frac = grey.mean(axis=0)
    idx = [i for i, f in enumerate(frac) if f > 0.25]
    return [c + x0 + 3 for (_, _, c) in group(idx)]


COLOURS = {
    # name: predicate on (r, g, b) arrays
    'blue': lambda r, g, b: (b > 140) & (r < 90) & (b - g > 30) & (g < 170),
    'green': lambda r, g, b: (g > 120) & (r < 110) & (g - b > 15) & (g - r > 50),
    'red': lambda r, g, b: (r > 190) & (g < 110) & (b < 110),
    'orange': lambda r, g, b: (r > 220) & (g > 110) & (g < 200) & (b < 120),
    'black': lambda r, g, b: (r < 70) & (g < 70) & (b < 70),
}


def trace_columns(im, x0, x1, y0, y1, colour, inset=3):
    """For each column inside the panel, the median row of pixels of this colour (or None)."""
    sub = im[y0 + inset:y1 - inset + 1, :]
    r, g, b = sub[..., 0], sub[..., 1], sub[..., 2]
    mask = COLOURS[colour](r, g, b)
    cols = {}
    for x in range(x0 + inset, x1 - inset + 1):
        ys = np.nonzero(mask[:, x])[0]
        if len(ys):
            cols[x] = (float(np.median(ys)) + y0 + inset, len(ys), int(ys.min()) + y0 + inset, int(ys.max()) + y0 + inset)
    return cols


def calibrate(p0, p1, v0, v1):
    """Linear map pixel->value with p0->v0, p1->v1."""
    s = (v1 - v0) / (p1 - p0)
    return lambda p: v0 + (p - p0) * s, abs(s)


def sample(cols, x_of_t, y_to_v, t_values, max_gap_px=2, max_spread_px=None):
    out = []
    for t in t_values:
        xc = x_of_t(t)
        best = None
        for dx in range(0, max_gap_px + 1):
            for x in (int(round(xc)) - dx, int(round(xc)) + dx):
                if x in cols:
                    best = cols[x]
                    break
            if best:
                break
        if best is None:
            out.append(None)
            continue
        ymed, n, ymin, ymax = best
        if max_spread_px is not None and (ymax - ymin) > max_spread_px:
            # a steep segment or two traces of this colour; the median is still the centre of the
            # coloured run, but flag it
            out.append((round(t, 3), y_to_v(ymed), True))
        else:
            out.append((round(t, 3), y_to_v(ymed), False))
    return out


def longest_run(mask_1d):
    best = cur = 0
    for v in mask_1d:
        cur = cur + 1 if v else 0
        if cur > best:
            best = cur
    return best


def axis_frames(im, thresh=500, min_run=250):
    """Rows/columns carrying a long CONTIGUOUS dark run: axis box edges, not text."""
    H, W, _ = im.shape
    dk = im.sum(axis=2) < thresh
    rows = group([y for y in range(H) if longest_run(dk[y]) >= min_run])
    cols = group([x for x in range(W) if longest_run(dk[:, x]) >= min_run])
    return rows, cols
