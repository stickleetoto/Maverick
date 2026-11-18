import zipfile, sys, re
import xml.etree.ElementTree as ET
NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
z = zipfile.ZipFile(sys.argv[1])
names = z.namelist()
ss = []
if 'xl/sharedStrings.xml' in names:
    root = ET.fromstring(z.read('xl/sharedStrings.xml'))
    for si in root.findall('m:si', NS):
        ss.append(''.join(t.text or '' for t in si.iter('{%s}t' % NS['m'])))
wb = ET.fromstring(z.read('xl/workbook.xml'))
sheets = [s.get('name') for s in wb.find('m:sheets', NS)]
print('sheets:', sheets)
pat = sys.argv[2] if len(sys.argv) > 2 else None
for i, name in enumerate(sheets, 1):
    path = 'xl/worksheets/sheet%d.xml' % i
    if path not in names: continue
    root = ET.fromstring(z.read(path))
    rows = []
    for row in root.iter('{%s}row' % NS['m']):
        vals = []
        for c in row.findall('m:c', NS):
            v = c.find('m:v', NS); t = c.get('t')
            if v is None:
                isn = c.find('m:is', NS)
                vals.append(''.join(x.text or '' for x in isn.iter('{%s}t' % NS['m'])) if isn is not None else '')
                continue
            vals.append(ss[int(v.text)] if t == 's' else v.text)
        rows.append((row.get('r'), vals))
    print('=== sheet', name, 'rows', len(rows))
    for r, vals in rows:
        line = ' | '.join(vals)
        if pat is None or re.search(pat, line, re.I) or r in ('1', '2', '3'):
            print(r, line[:400])
