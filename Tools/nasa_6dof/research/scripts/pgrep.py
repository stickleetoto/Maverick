import sys,re
sys.stdout.reconfigure(encoding='utf-8',errors='replace')
t=open(sys.argv[1],encoding='utf-8',errors='replace').read()
pages=t.split('\f')
pat=re.compile(sys.argv[2])
if len(sys.argv)>3:
    for n in sys.argv[3:]:
        print('=== PAGE',n); print(pages[int(n)-1])
    sys.exit()
for i,p in enumerate(pages):
    if pat.search(p):
        m=[l.strip()[:110] for l in p.splitlines() if pat.search(l)][:3]
        print(i+1, m)
