import re,sys
t=open(sys.argv[1],encoding='utf-8').read()
t=t[t.index('Table 3 - Parameter'):t.index('===== PAGE 6')]
lines=[l.strip() for l in t.splitlines()[1:] if l.strip()]
tab={}
for i in range(0,len(lines)-1,2):
    k=lines[i].replace(' ',''); v=lines[i+1].replace('–','-')
    tab[k]=v
src=open(sys.argv[2],encoding='utf-8').read()
nums=re.findall(r'([+-]?\s*\d\.\d{6}e[+-]\d\d)',src)
code=[float(n.replace(' ','')) for n in nums]
print(len(tab),"table entries;",len(code),"numbers in code")
def val(v):
    v=v.replace(' ','')
    if 'e' not in v: v=re.sub(r'(\d)-(\d\d)$',r'\1e-\2',v); print("  NOTE malformed in paper ->",v)
    return float(v)
tv={k:val(v) for k,v in tab.items()}
# match by absolute value set (sign may be applied as '-' operator in code)
codeabs=sorted(abs(c) for c in code)
missing=[(k,v) for k,v in tv.items() if not any(abs(abs(v)-c)<1e-12*max(1,c) for c in codeabs)]
print("table values not found in code:",missing)
signed=[]
for m in re.finditer(r'(\(|=|\+|-)\s*(\d\.\d{6}e[+-]\d\d)',src):
    op,n=m.groups(); signed.append(-float(n) if op=='-' else float(n))
keys=sorted(tv, key=lambda k:(k[0],int(k[1:])))
bad=[(k,tv[k],s) for k,s in zip(keys,signed) if abs(tv[k]-s)>1e-12*abs(s)]
print("signed mismatches in letter order:",bad)
