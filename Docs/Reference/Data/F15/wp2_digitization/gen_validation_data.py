"""Generate Validation/MavF15Nasa836ValidationData.cs from the digitized JSON files.

Every series is OriginalPrimary x Exact836 x ValidationOnly. Values are written exactly as
digitized (rounded only below the digitization resolution); nothing is interpolated or smoothed.

Inputs (in the working directory): tm2008_fig12_13.json (vec_extract.py), tm2008_fig14.json and
tm2008_fig15.json (digitize_panels.py), tm2012_fig27a..29b.json (make_quad_specs.py).
Usage: python gen_validation_data.py <path to MavF15Nasa836ValidationData.cs>
"""
import json
import math
import sys

OUT = sys.argv[1]

TM2008 = 'NASA/TM-2008-214634'
TM2012 = 'NASA/TM-2012-215978'

BASELINE_2008 = ('NASA F-15B 836 BASELINE configuration of the TM-2008-214634 baseline flight '
                 'series: standard air-data nose boom plus the added sideslip vane; no experimental '
                 'nose boom (report pp.5-6, PDF p.10)')
BASELINE_2012 = ('simulated baseline F-15B test airplane (836 without the spike), CAS off, DFRC '
                 'nonlinear simulation; the curve labelled "F-15B" (TM-2012-215978 p.11)')

ALT = {15000: '15,000 ft', 25000: '25,000 ft', 30000: '30,000 ft', 35000: '35,000 ft',
       40000: '40,000 ft', 45000: '45,000 ft'}
SHAPE = {15000: 'closed circle', 25000: 'closed square', 30000: 'closed triangle (up)',
         35000: 'closed diamond', 40000: 'closed triangle (down)', 45000: 'closed triangle (right)'}


def fmt(v, digits):
    s = ('%.' + str(digits) + 'f') % v
    if '.' in s:
        s = s.rstrip('0')
        if s.endswith('.'):
            s = s + '0'
    if s in ('-0.0', '-0'):
        s = '0.0'
    return s


def digits_for(resolution):
    """Decimal places that keep ~1/10 of the digitization resolution."""
    return max(1, int(math.ceil(-math.log10(resolution / 10.0))))


series = []


def add(sid, report, figure, page, kind, configuration, condition, xq, xu, yq, yu, digitization,
        xunc, yunc, xs, ys, xdig, ydig, max_span):
    series.append(dict(id=sid, report=report, figure=figure, page=page, kind=kind,
                       configuration=configuration, condition=condition, xq=xq, xu=xu, yq=yq,
                       yu=yu, digitization=digitization, xunc=xunc, yunc=yunc, xs=xs, ys=ys,
                       xdig=xdig, ydig=ydig, max_span=max_span))


# ---------------------------------------------------------------- TM-2008 figs 12 and 13 (vector)
vec = json.load(open('tm2008_fig12_13.json'))
FIGS = {
    'fig12': dict(figure='Figure 12', page='PDF p.19 (printed p.15), art 080001', yq='Cn_beta',
                  yu='1/deg', name='CnBeta', ydig=7),
    'fig13': dict(figure='Figure 13', page='PDF p.20 (printed p.16), art 080002', yq='Cm_alpha',
                  yu='1/deg', name='CmAlpha', ydig=6),
}
# Trend-line altitude: from the report text (TM-2008 p.16, fig. 13 only) and the nearest baseline
# markers to each drawn line. Fig. 12's lines carry no stated altitude.
TREND_ALT = {('fig12', 0): (None, 'subsonic trend'), ('fig12', 1): (None, 'supersonic trend'),
             ('fig13', 3): (None, 'subsonic trend'), ('fig13', 2): (30000, 'supersonic trend'),
             ('fig13', 1): (40000, 'supersonic trend'), ('fig13', 0): (45000, 'supersonic trend')}
for key, meta in FIGS.items():
    f = vec[key]
    sx, sy = f['x_scale_per_pt'], abs(f['y_scale_per_pt'])
    for alt in sorted(ALT):
        pts = [p for p in f['points'] if p['altitudeFt'] == alt]
        if not pts:
            continue
        hm = max(p['halfMarkerMach'] for p in pts)
        hv = max(p['halfMarkerValue'] for p in pts)
        add('TM2008_F%s_%s_BASELINE_PE_%dFT' % (key[3:], meta['name'], alt), TM2008, meta['figure'],
            meta['page'], 'FlightParameterEstimate', BASELINE_2008,
            'altitude %s (symbol legend); Mach per point; angle of attack not stated' % ALT[alt],
            'Mach', '-', meta['yq'], meta['yu'],
            'VECTOR extraction from the PDF path geometry: %s markers (black fill = baseline), '
            'centre = marker bounding-box centre; axes calibrated on the plot grid lines '
            '(max residual %.1e Mach, %.1e %s). Uncertainty is the marker half-size: the plotted '
            'anchor inside the marker is not known.' % (SHAPE[alt], f['x_calibration_max_residual'],
                                                        f['y_calibration_max_residual'], meta['yu']),
            hm, hv, [p['mach'] for p in pts], [p['value'] for p in pts], 4, meta['ydig'], None)
    for i, t in enumerate(f['trends']):
        alt, what = TREND_ALT[(key, i)]
        label = ('%dFT' % alt) if alt else ('SUBSONIC' if t[0][0] < 1.0 else 'SUPERSONIC')
        cond = ('baseline trend at %s (report text p.16; assigned to this line by nearest baseline '
                'markers)' % ALT[alt]) if alt else ('%s; altitude not stated for this line' % what)
        add('TM2008_F%s_%s_BASELINE_TREND_%s' % (key[3:], meta['name'], label), TM2008,
            meta['figure'], meta['page'], 'AuthorTrendLine', BASELINE_2008, cond, 'Mach', '-',
            meta['yq'], meta['yu'],
            'VECTOR extraction: the vertices of the authors\' solid black "parameter estimation '
            'results trend" line, exactly as drawn (a trend through the estimates, not data). '
            'Uncertainty is half the 1-pt stroke.',
            0.5 * sx, 0.5 * sy, [p[0] for p in t], [p[1] for p in t], 4, meta['ydig'] + 1, float('inf'))

# ---------------------------------------------------------------- TM-2008 figs 14 and 15 (raster)
RASTER_2008 = {
    'tm2008_fig14.json': dict(figure='Figure 14', page='PDF p.21 (printed p.17), art 070174',
                              cond='baseline flight, subsonic push-over/pull-up; Mach, altitude and '
                                   'weight NOT STATED in the report', tag='F14_POPU'),
    'tm2008_fig15.json': dict(figure='Figure 15', page='PDF p.22 (printed p.18), art 070175',
                              cond='baseline flight, supersonic rudder sweep; Mach, altitude and '
                                   'weight NOT STATED in the report', tag='F15_RUDDER_SWEEP'),
}
NAME = {'alpha': 'ALPHA', 'pitch rate': 'Q', 'normal acceleration': 'NZ', 'stabilator': 'STAB',
        'sideslip': 'BETA', 'roll rate': 'P', 'yaw rate': 'R', 'lateral acceleration': 'AY',
        'surface deflection': 'SURF', 'angle of attack': 'ALPHA', 'horizontal tail (input)': 'HTAIL',
        'rudder (input)': 'RUDDER'}
for fn, meta in RASTER_2008.items():
    d = json.load(open(fn))
    spx = d['time']['seconds_per_px']
    tres = d['time']['max_residual_px']
    for p in d['panels']:
        for tr in p['traces']:
            kept = [s for s in tr['samples'] if not s[2]]
            dropped = len(tr['samples']) - len(kept) + len(tr['omitted_times'])
            unc_px = 3.0 + max(tres, p['calibration_max_residual_px'])
            if tr['name'].startswith('baseline simulation'):
                kind, who = 'SimulationTimeHistory', 'SIM'
            else:
                kind, who = 'FlightMeasuredTimeHistory', 'FLIGHT'
            q = NAME[p['name']]
            if q == 'SURF':
                q = {'rudder': 'RUDDER', 'aileron': 'AILERON', 'differential': 'DIFFTAIL'}[tr['name'].split()[0]]
            quantity = p['name'] if p['name'] != 'surface deflection' else tr['name'].replace(' (flight-measured)', '')
            add('TM2008_%s_%s_%s' % (meta['tag'], q, who), TM2008, meta['figure'], meta['page'], kind,
                BASELINE_2008 + ('; open-loop simulation driven by flight-measured surface positions '
                                 '(report p.17)' if who == 'SIM' else ''),
                meta['cond'], 'time', 's', quantity, p['units'],
                'RASTER colour segmentation of the embedded figure image (%s trace), one median row '
                'per pixel column, sampled every 0.1 s; axes calibrated on the frame and grid lines '
                '(max residual %.1f px time, %.1f px value). %d samples omitted where the trace was '
                'hidden, overlapped or ambiguous; never filled. Uncertainty = (3 px + residual).'
                % (tr['colour'], tres, p['calibration_max_residual_px'], dropped),
                unc_px * spx, unc_px * p['value_per_px'], [s[0] for s in kept], [s[1] for s in kept],
                1, digits_for(p['value_per_px']), 0.15)

# ---------------------------------------------------------------- TM-2012 figs 27-29 (raster)
COND = {'27': ('Figure 27', 'PDF p.36 (printed p.32)', 1, 'Mach 0.60, 25,000 ft'),
        '28': ('Figure 28', 'PDF p.37 (printed p.33)', 2, 'Mach 0.95, 35,000 ft'),
        '29': ('Figure 29', 'PDF p.38 (printed p.34)', 3, 'Mach 1.80, 45,000 ft')}
ART = {'27a': '110222', '27b': '110223', '28a': '110224', '28b': '110225', '29a': '110226', '29b': '110227'}
for k in ('27a', '27b', '28a', '28b', '29a', '29b'):
    d = json.load(open('tm2012_fig%s.json' % k))
    fig, page, fc, cond = COND[k[:2]]
    doublet = 'pitch stick doublet' if k[2] == 'a' else 'rudder pedal doublet'
    for p in d['panels']:
        tr = p['traces'][0]
        unc_px = 3.0 + max(p['time_residual_px'], p['value_residual_px'])
        q = NAME[p['name']]
        note = ''
        if p['name'] == 'lateral acceleration':
            note = (' SOURCE ANOMALY: this axis is printed %s, and its magnitude looks small for the '
                    'sideslip shown; stored exactly as printed, not corrected.' % p['unit_scale_note'])
        add('TM2012_F%s_COND%d_%s_BASELINE_SIM' % (k, fc, q), TM2012, fig + '(%s)' % k[2], page + ', art ' + ART[k],
            'SimulationTimeHistory', BASELINE_2012,
            'representative flight condition %d (report table 2, p.8): %s; CAS off; %s; fuel '
            'weight not stated for this figure' % (fc, cond, doublet),
            'time', 's', p['name'], p['units'],
            'RASTER colour segmentation (green dashed "F-15B" curve, hue-separated from the '
            '"with spike" curve) of a JPEG-compressed figure image, sampled every 0.1 s; axes '
            'calibrated on the frame and dotted grid lines (max residual %.1f px time, %.1f px '
            'value). %d samples omitted where the dashed curve was hidden under the other curve '
            'or ambiguous; never filled. Uncertainty = (3 px + residual).%s'
            % (p['time_residual_px'], p['value_residual_px'], tr['omitted'], note),
            unc_px * p['seconds_per_px'], unc_px * p['value_per_px'],
            [s[0] for s in tr['samples']], [s[1] for s in tr['samples']],
            1, digits_for(p['value_per_px']), 0.15)

# ---------------------------------------------------------------- excluded figures
EXCLUDED = [
    (TM2008, 'Figures 12-13, open symbols and dashed lines',
     'research configuration (24-ft experimental nose boom) results and worst-case preflight '
     'predictions for it: ResearchModified, not the exact target'),
    (TM2008, 'Figures 16-19',
     'research-configuration (experimental nose boom) flight/simulation time histories'),
    (TM2012, 'Figures 7-12',
     'flight/simulation comparisons for the spike-equipped airplane'),
    (TM2012, 'Figures 15-18 and 20-23',
     'ordinate UNSCALED in the public report (only "+", "0", "-" marks), so no value can be read; '
     'the estimates are also for the spike-extended airplane'),
    (TM2012, 'Figure 19', 'piloted simulation of the spike-equipped airplane with a projected '
     'worst-case Cmq'),
    (TM2012, 'Figures 24-26',
     'CAS-ON baseline simulation: exact scope, but the response depends on the unavailable FCS; '
     'held, not digitized in this pass'),
    (TM2012, 'Figures 27-29, "with spike" curves', 'spike-equipped configuration'),
    (TM2012, 'Figures 30-33',
     'CAS-off Dutch-roll and short-period flight estimates are for the SPIKE-EXTENDED airplane '
     '(report p.11); the bands are simulation stress-analysis regions for that configuration at '
     'three fuel weights (legend 2,000 / 8,000 / 12,000)'),
    (TM2012, 'Figures 34-35', 'handling-qualities evaluations of the spike-equipped airplane'),
]


def arr(vals, digits):
    return ', '.join(fmt(v, digits) for v in vals)


def num(v):
    if v == float('inf'):
        return 'double.PositiveInfinity'
    return repr(float('%.4g' % v))


lines = []
w = lines.append
w('// <auto-generated>')
w('// Generated from digitized NASA figures by Docs/Reference/Data/F15/wp2_digitization/ (see its')
w('// README). Do not hand-edit a value: regenerate, so every number keeps its digitization record.')
w('// Values are as digitized, never smoothed. Method: Docs/Reference/F15_836_VALIDATION_DATA_V1.0.md.')
w('// </auto-generated>')
w('using System.Collections.Generic;')
w('using System.Collections.ObjectModel;')
w('using MaverickFresh.FlightDynamics.F15;')
w('')
w('namespace MaverickFresh.FlightDynamics.Validation')
w('{')
w('    public static partial class MavF15Nasa836ValidationData')
w('    {')
w('        private static ReadOnlyCollection<MavF15Nasa836ValidationSeries> BuildSeries()')
w('        {')
w('            List<MavF15Nasa836ValidationSeries> s = new List<MavF15Nasa836ValidationSeries>(%d);' % len(series))
for se in series:
    w('')
    w('            s.Add(new MavF15Nasa836ValidationSeries(')
    w('                %s,' % json.dumps(se['id']))
    w('                %s, %s, %s,' % (json.dumps(se['report']), json.dumps(se['figure']), json.dumps(se['page'])))
    w('                MavF15ValidationDataKind.%s,' % se['kind'])
    w('                %s,' % json.dumps(se['configuration']))
    w('                %s,' % json.dumps(se['condition']))
    w('                %s, %s, %s, %s,' % (json.dumps(se['xq']), json.dumps(se['xu']), json.dumps(se['yq']), json.dumps(se['yu'])))
    w('                %s,' % json.dumps(se['digitization']))
    w('                %s, %s, %s,' % (num(se['xunc']), num(se['yunc']), num(se['max_span']) if se['max_span'] is not None else '0.0'))
    w('                new double[] { %s },' % arr(se['xs'], se['xdig']))
    w('                new double[] { %s }));' % arr(se['ys'], se['ydig']))
w('')
w('            return s.AsReadOnly();')
w('        }')
w('')
w('        private static ReadOnlyCollection<MavF15ExcludedValidationFigure> BuildExcluded()')
w('        {')
w('            return new List<MavF15ExcludedValidationFigure>')
w('            {')
for rep, fig, why in EXCLUDED:
    w('                new MavF15ExcludedValidationFigure(%s, %s, %s),' % (json.dumps(rep), json.dumps(fig), json.dumps(why)))
w('            }.AsReadOnly();')
w('        }')
w('    }')
w('}')
open(OUT, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
print('series', len(series), 'samples', sum(len(s['xs']) for s in series))
