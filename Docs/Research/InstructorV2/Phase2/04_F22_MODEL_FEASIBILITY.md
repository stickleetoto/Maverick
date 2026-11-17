# 04 — F-22 Research-Model Feasibility

**Question:** can a technically defensible F-22 research model be built from public sources?

**Answer: No.** This phase confirms the repository's earlier verdict (`Docs/Research/Aerospace/Aircraft/F22/PUBLIC_SOURCE_FEASIBILITY.md`) with one more time-boxed search. No public source gives reference geometry, mass properties, inertias, aerodynamic coefficients, control-law gains, actuator data or a thrust deck for any F-22 configuration (YF-22, EMD or F-22A).

Evidence classes as in doc 02. Sources: doc 09.

---

## 1. Item-by-item

| Item a 6-DoF model needs | Public status | Evidence |
|---|---|---|
| Physical dimensions (span, length, height) | identity only (USAF fact sheet) | Repo library (`USAF-F22-FACT-SHEET`) |
| Reference S, c̄, b, moment reference | **not publicly located** | Repo library; this phase found nothing new |
| Mass at a defined loading, CG | not located (fact-sheet weights have no loading definition) | Repo library |
| Inertias | not located | Repo library |
| Static / dynamic aerodynamic coefficients | not located; tunnel/CFD leads unread or content unknown (`AIAA-99-4015`, `AIAA-2013-0972`) | Repo library |
| **New this phase:** AIAA 2000-1695 (Moses & Huttsell), early F-22 13.3 %-scale model in the Transonic Dynamics Tunnel | **fin-buffet pressures and spectra only, M ≈ 0.09–0.1; no reference geometry, mass or force/moment coefficients** | PV (sub-agent read the NTRS OCR text) |
| Thrust-vectoring architecture | identity: two-dimensional pitch vectoring, ±20° (manufacturer page); integration details not public | Repo library; Phase 1 doc 02 §8 |
| Flight-control architecture | partial: triplex FLCS, pitch TV integration, 1996 design philosophy; flight-test update with selectable test laws, ARI reductions, nose-kick observation | Repo library; Phase 1 doc 02 §8 (`F22-SETP-1999`, PV) |
| FCS gains, actuator rates/limits | not located | Repo library |
| Installed thrust deck, engine dynamics | not located (F119 proprietary) | Repo library |
| Performance statements | fact-sheet level; high-α flight-test exploration statements (EMD) | Repo library |
| YF-22 public material | narrative and PIO case history; no coefficient or mass set | Repo library |
| Non-F-22 look-alikes | subscale "YF-22" research UAVs (WVU, 2004–2005) with identified derivatives — **not F-22 data** | SV (snippets) |

---

## 2. What a "model" would require inventing

Reference geometry, mass properties, every aerodynamic coefficient and derivative, Mach dependence, control-surface effectiveness, the entire control law, actuator dynamics and thrust. That is the whole model. Building it would be a fabricated aircraft with an F-22 name. The brief and the repository's source policy both forbid this.

---

## 3. What is defensible instead

| Option | Description | Label |
|---|---|---|
| Do nothing | keep F-22 out of the simulator | — |
| Generic "fifth-generation-like" study aircraft | a clearly fictional aircraft built from public *generic* research configurations (e.g. NASA generic fighter tunnel models), never named or presented as an F-22 | separate project; outside this research |
| Thrust-vectoring research | study pitch-vectoring control allocation on a configuration that has public TV data (VISTA/MATV for the F-16 family, NF-15B 837 for the F-15 family) — each with its own data limits | P3 research only |

---

## 4. Verdict

| Question | Answer |
|---|---|
| Defensible production F-22A model? | **No** |
| Defensible YF-22 or EMD research model? | **No** |
| Anything worth monitoring? | tunnel/CFD leads `AIAA-99-4015`, `AIAA-2013-0972` (repository library; unread) — only if they turn out to print coefficients with a defined configuration |
| Recommended action | **close the topic** for Maverick's aircraft roadmap; revisit only if a public coefficient set with a defined configuration appears |
