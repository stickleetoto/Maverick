#!/usr/bin/env python3
"""Validate the visually transcribed NASA TP-1538 Table VI dataset."""
from __future__ import annotations
import csv, hashlib, io, json, sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
D = ROOT / "Docs/Reference/Data/F16/TP1538"
RAW, SI, MAN = D/"table_vi_raw_source.csv", D/"table_vi_si_converted.csv", D/"manifest.json"
MACH = ("0.2","0.4","0.6","0.8","1.0")
ALT_M = (0,3048,6096,9144,12192,15240)
ALT_FT = (0,10000,20000,30000,40000,50000)
STATES = ("idle","military","maximum")
FT2M, LB2N = Decimal("0.3048"), Decimal("4.448")
RAW_FIELDS = ("sourceSection","mach","sourceAltitude","sourceAltitudeUnit",
              "idleThrustSource","militaryThrustSource","maximumThrustSource","sourceForceUnit",
              "verificationPassA","verificationPassB")
SI_FIELDS = ("altitudeMeters","mach","idleThrustN","militaryThrustN","maximumThrustN")

def rows(path):
    with path.open(newline="", encoding="utf-8") as f:
        r=csv.DictReader(f); return tuple(r.fieldnames or ()), list(r)

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def req(ok,msg):
    if not ok: raise AssertionError(msg)

def canonical_csv(fields, data):
    s=io.StringIO(newline=""); w=csv.DictWriter(s,fieldnames=fields,lineterminator="\n")
    w.writeheader(); w.writerows(data); return s.getvalue().encode()

def main():
    rf, raw = rows(RAW)
    req(rf==RAW_FIELDS, "raw schema")
    req(len(raw)==60, "raw row count")
    expected_order=( [("SI",m,a) for m in MACH for a in ALT_M]
                    +[("US_CUSTOMARY",m,a) for m in MACH for a in ALT_FT] )
    req([(r["sourceSection"],r["mach"],int(r["sourceAltitude"])) for r in raw]==expected_order,
        "raw grid ordering")
    maps={"SI":{},"US_CUSTOMARY":{}}
    for r in raw:
        sec,m,a=r["sourceSection"],r["mach"],int(r["sourceAltitude"])
        req(sec in maps and m in MACH, "raw section/Mach")
        req(r["verificationPassA"]=="PASS_VISUAL_400DPI_PDFIUM", "visual pass A")
        req(r["verificationPassB"]=="PASS_VISUAL_500DPI_POPPLER", "visual pass B")
        if sec=="SI": req(r["sourceAltitudeUnit"]=="m" and r["sourceForceUnit"]=="N" and a in ALT_M,"SI units/grid")
        else: req(r["sourceAltitudeUnit"]=="ft" and r["sourceForceUnit"]=="lb" and a in ALT_FT,"US units/grid")
        key=(m,a); req(key not in maps[sec],"duplicate raw coordinate")
        maps[sec][key]={s:int(r[f"{s}ThrustSource"]) for s in STATES}

    neg_us={(m,a) for (m,a),v in maps["US_CUSTOMARY"].items() if v["idle"]<0}
    expected_neg={("0.6",0),("0.6",10000),("0.6",20000),
                  ("0.8",0),("0.8",10000),("0.8",20000),("0.8",30000),
                  ("1.0",0),("1.0",10000),("1.0",20000),("1.0",30000),("1.0",40000)}
    req(neg_us==expected_neg,"negative idle coordinate set")
    req(all(v[s]>=0 for sec in maps.values() for v in sec.values() for s in ("military","maximum")),
        "negative non-idle thrust")

    cross=0; max_res=Decimal(0)
    for m in MACH:
        for ft,meters in zip(ALT_FT,ALT_M):
            req(Decimal(ft)*FT2M==Decimal(meters),"ft->m grid")
            u,s=maps["US_CUSTOMARY"][(m,ft)],maps["SI"][(m,meters)]
            for state in STATES:
                n=Decimal(u[state])*LB2N
                req(int(n.quantize(Decimal("1"),rounding=ROUND_HALF_UP))==s[state],
                    f"US/SI mismatch {m=} {ft=} {state=}")
                max_res=max(max_res,abs(Decimal(s[state])-n)); cross+=1
    req(cross==90 and max_res<=Decimal("0.5"),"source cross-check summary")

    sf, derived=rows(SI)
    req(sf==SI_FIELDS and len(derived)==30,"derived schema/count")
    expected_si_order=[(m,Decimal(a)) for m in MACH for a in ALT_M]
    actual=[]
    for r in derived:
        key=(r["mach"],Decimal(r["altitudeMeters"])); actual.append(key)
        req(key in expected_si_order,"derived grid")
        j=ALT_M.index(int(key[1])); u=maps["US_CUSTOMARY"][(key[0],ALT_FT[j])]
        for state in STATES:
            req(Decimal(r[f"{state}ThrustN"])==(Decimal(u[state])*LB2N).quantize(Decimal("0.001")),
                f"derived mismatch {key} {state}")
    req(actual==expected_si_order,"derived grid ordering")

    # Five independent visual/hand-calculation anchors.
    anchors=(("0.2",0,"idle",635,2824),("0.8",10000,"idle",-1900,-8451),
             ("0.6",30000,"military",4660,20728),("1.0",50000,"maximum",5057,22494),
             ("1.0",0,"maximum",28886,128485))
    for m,ft,state,lb,n in anchors:
        meters=int(Decimal(ft)*FT2M)
        req(maps["US_CUSTOMARY"][(m,ft)][state]==lb and maps["SI"][(m,meters)][state]==n,"anchor source")
        req(int((Decimal(lb)*LB2N).quantize(Decimal("1"),rounding=ROUND_HALF_UP))==n,"anchor conversion")

    req(RAW.read_bytes()==canonical_csv(RAW_FIELDS,raw),"raw serialization")
    req(SI.read_bytes()==canonical_csv(SI_FIELDS,derived),"SI serialization")
    mb=MAN.read_bytes(); man=json.loads(mb.decode())
    req(mb==(json.dumps(man,ensure_ascii=False,sort_keys=True,indent=2)+"\n").encode(),"manifest serialization")
    req(man["files"]["rawSourceCsv"]["sha256"]==sha(RAW),"raw hash")
    req(man["files"]["siConvertedCsv"]["sha256"]==sha(SI),"SI hash")
    req(man["transcription"]["ambiguousOrUnavailableThrustCells"]==0,"unavailable cells")
    req(man["conversion"]["crossCheckMismatches"]==0,"manifest conversion")
    req(man["integration"]["connectedToProductionEngine"] is False,"integration boundary")
    req(man["integration"]["productionF16PhysicalThrustExpectedN"]==0,"zero-thrust boundary")

    print("TP-1538 Table VI validation PASS")
    print("raw: 60 coordinate rows / 180 printed thrust cells")
    print("derived SI: 30 coordinate rows / 90 thrust cells")
    print(f"source SI<->US conversion: {cross}/90; max pre-round residual {max_res} N")
    print("visual/hand anchors: 5/5; data hashes: 2/2; canonical serializations: 3/3; unavailable: 0")
    return 0

if __name__=="__main__":
    try: raise SystemExit(main())
    except AssertionError as e:
        print(f"TP-1538 Table VI validation FAIL: {e}",file=sys.stderr); raise SystemExit(1)
