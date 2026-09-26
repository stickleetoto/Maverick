"""Write the six quad_*.json specs for TM-2012-215978 figs. 27-29 and run digitize_quad.py on each.

Frames are pixel boxes in the embedded images (found with raster_digitize.axis_frames); axis limits
and intermediate grid values were read from the rendered pages (PDF pp.36-38).
"""
import json
import subprocess

G = lambda: [{"name": "baseline F-15B simulation", "colour": "green", "kind": "simulation", "max_gap_px": 9}]


def pitch(frames, lim):
    (pq, pa, pn, pt) = lim
    tl, tr, bl, br = frames
    return [
        {"name": "pitch rate", "units": "deg/s", "frame": tl, "limits": pq[0], "grid": pq[1], "traces": G()},
        {"name": "angle of attack", "units": "deg", "frame": tr, "limits": pa[0], "grid": pa[1], "traces": G()},
        {"name": "normal acceleration", "units": "g", "frame": bl, "limits": pn[0], "grid": pn[1], "traces": G()},
        {"name": "horizontal tail (input)", "units": "deg", "frame": br, "limits": pt[0], "grid": pt[1], "traces": G()}]


def yaw(frames, lim, ay_note):
    (sb, yr, ay, rd) = lim
    tl, tr, bl, br = frames
    return [
        {"name": "sideslip", "units": "deg", "frame": tl, "limits": sb[0], "grid": sb[1], "traces": G()},
        {"name": "yaw rate", "units": "deg/s", "frame": tr, "limits": yr[0], "grid": yr[1], "traces": G()},
        {"name": "lateral acceleration", "units": "g (as printed)", "note": ay_note, "frame": bl, "limits": ay[0], "grid": ay[1], "traces": G()},
        {"name": "rudder (input)", "units": "deg", "frame": br, "limits": rd[0], "grid": rd[1], "traces": G()}]


def q(x0, x1, y0, y1):
    return [x0, x1, y0, y1]


specs = {
    '27a': pitch([q(130, 846, 20, 450), q(977, 1693, 20, 450), q(130, 846, 508, 938), q(977, 1693, 508, 938)],
                 [((20, -20), [10, 0, -10]), ((15, -5), [10, 5, 0]), ((3, -1), [2, 1, 0]), ((5, -10), [0, -5])]),
    '27b': yaw([q(100, 648, 17, 448), q(778, 1326, 17, 448), q(100, 648, 506, 937), q(778, 1326, 506, 937)],
               [((2, -2), [1, 0, -1]), ((3, -3), [2, 1, 0, -1, -2]), ((0.003, -0.003), [0.002, 0.001, 0, -0.001, -0.002]), ((2, -2), [1, 0, -1])],
               "axis printed with a x10^-3 multiplier"),
    '28a': pitch([q(122, 685, 13, 450), q(818, 1382, 13, 450), q(122, 685, 498, 936), q(818, 1382, 498, 936)],
                 [((20, -20), [10, 0, -10]), ((8, -4), [6, 4, 2, 0, -2]), ((3, -2), [2, 1, 0, -1]), ((4, -6), [2, 0, -2, -4])]),
    '28b': yaw([q(107, 665, 15, 441), q(796, 1351, 15, 441), q(107, 665, 508, 936), q(796, 1351, 508, 936)],
               [((3, -2), [2, 1, 0, -1]), ((6, -6), [4, 2, 0, -2, -4]), ((0.006, -0.006), [0.004, 0.002, 0, -0.002, -0.004]), ((2, -2), [1, 0, -1])],
               "axis printed with a x10^-3 multiplier"),
    '29a': pitch([q(124, 682, 16, 453), q(816, 1378, 16, 453), q(124, 682, 500, 936), q(816, 1378, 500, 936)],
                 [((10, -15), [5, 0, -5, -10]), ((6, -2), [4, 2, 0]), ((3, -1), [2, 1, 0]), ((8, -4), [6, 4, 2, 0, -2])]),
    '29b': yaw([q(110, 483, 10, 301), q(570, 944, 10, 301), q(110, 483, 336, 625), q(570, 944, 336, 625)],
               [((2, -2), [1, 0, -1]), ((4, -4), [2, 0, -2]), ((0.010, -0.010), [0.005, 0, -0.005]), ((2, -2), [1, 0, -1])],
               "axis printed in g without a multiplier"),
}

for k, panels in specs.items():
    spec = {"image": "tm2012_fig%s_raw.png" % k, "overlay": "tm2012_fig%s_overlay.png" % k,
            "output": "tm2012_fig%s.json" % k, "dt": 0.1, "t_limits": [0, 10], "t_grid": [2, 4, 6, 8],
            "panels": panels}
    json.dump(spec, open('quad_%s.json' % k, 'w'), indent=1)
    print('=====', k)
    print(subprocess.run(['python', 'digitize_quad.py', 'quad_%s.json' % k], capture_output=True, text=True).stdout)
