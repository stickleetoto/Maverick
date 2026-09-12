# TP-1538 Table VI Transcription — Workflow and Raw File

**Status: NOT TRANSCRIBED.** The template below is empty on purpose. Until a person fills it in and
the validator accepts it, the reference propulsion model keeps reporting
`HasAuthoritativeData = false` and `thrust = 0 N`.

---

## Why this is a manual job

The data is public and permitted. NASA **TP-1538** is an unrestricted NASA Technical Paper on NTRS,
and **Table VI, "THRUST VALUES USED IN SIMULATION"** tabulates idle, military and maximum thrust
against Mach and altitude, in both SI and US units. No release restriction applies, and the
U.S.-release-gated MATLAB package is not needed.

What cannot be trusted is the machine extraction. The OCR of that table interleaves columns — idle,
military and maximum rows come out shuffled together with the altitude headers. Here is a verbatim
sample of what text extraction produces:

```
Tidle 7 562
5 916
0.2 2 824 1 890 3 069 4 492 6 783iii i 535 3 358 5 026
267 -l 334 i 557 4 048 6 049
```

Row labels, altitude columns and Mach rows are not separable from that with any confidence.
Transcribing it would be inventing numbers that look sourced, which is the one failure mode this
programme treats as unacceptable. So the values must be **read visually from the rendered page**.

## Procedure

1. Open TP-1538 (NTRS document `19800005879`) and go to **Table VI**, around **pages 92–93**.
   Sub-table **(a)** is SI units (N); sub-table **(b)** is US units (lb).
2. Confirm the axes before reading any value. As printed they are:
   - altitudes, m: `0, 3048, 6096, 9144, 12192, 15240`
   - Mach: `0, 0.2, 0.4, 0.6, 0.8, 1.0`
   If the rendered page disagrees with either list, **change the list, not the data**.
3. Transcribe **both** unit tables. This is not redundancy — it is the primary error check. A single
   mis-keyed digit will almost certainly break the 4.44822 N/lbf relationship, which the validator
   tests at every grid point.
4. Fill in the provenance header honestly, including `METHOD`, which must record that values were read
   visually.
5. Run the validator (`Maverick > Flight Dynamics > Validate TP-1538 Thrust Transcription`, or the
   offline `p5b5check` suite). It refuses unless:
   - both unit blocks are present for all three power settings;
   - SI and US agree within 1% at every point;
   - `idle < military < maximum` at every point;
   - military and maximum thrust **do not rise** with altitude at fixed Mach;
   - every value is finite;
   - the provenance header is complete.
6. Record the reported hash in this document, so the reviewed transcription can be proven later to be
   the one in use.

### Checks deliberately NOT performed

- **Monotonicity in Mach.** Ram recovery can raise thrust with Mach before it falls again. Asserting
  monotonicity here would reject correct data.
- **Sign of idle thrust.** TP-1538's idle figures go negative at high Mach and altitude; that is the
  model representing net installed idle thrust, not an error. Only the *ordering* is enforced.
- **Absolute plausibility against any other engine.** The F110/F100 figures are what they are; a
  "looks about right" check would be an opinion pretending to be a test.

## Format

Line oriented, `KEY: value`. `#` begins a comment. Unknown keys are errors, not skipped lines — a
silently ignored line is a silently missing row.

```
SOURCE: NASA TP-1538
TABLE: VI
PAGES: <as rendered>
TRANSCRIBED_BY: <name>
TRANSCRIBED_ON: <YYYY-MM-DD>
METHOD: visual reading of the rendered PDF page, both unit tables independently

ALTITUDES_M: 0, 3048, 6096, 9144, 12192, 15240
MACHS: 0.0, 0.2, 0.4, 0.6, 0.8, 1.0

POWER: idle
UNITS: SI
ROW: <six values, altitude 0 .. 15240, at Mach 0.0>
ROW: <... at Mach 0.2>
ROW: <... at Mach 0.4>
ROW: <... at Mach 0.6>
ROW: <... at Mach 0.8>
ROW: <... at Mach 1.0>
UNITS: US
ROW: <same six points in lb>
... six rows ...

POWER: mil
UNITS: SI
... six rows ...
UNITS: US
... six rows ...

POWER: max
UNITS: SI
... six rows ...
UNITS: US
... six rows ...
```

One `ROW` per Mach, values ordered by increasing altitude.

---

## RAW TRANSCRIPTION

Everything below this line is the machine-read file. Leave it exactly as-is until transcribing.

```transcription
SOURCE: NASA TP-1538
TABLE: VI
PAGES:
TRANSCRIBED_BY:
TRANSCRIBED_ON:
METHOD:

ALTITUDES_M: 0, 3048, 6096, 9144, 12192, 15240
MACHS: 0.0, 0.2, 0.4, 0.6, 0.8, 1.0

# No POWER blocks yet. The validator reports NOT TRANSCRIBED while this is true,
# and the reference propulsion model stays non-authoritative.
```

## Verified hash

`(none — not transcribed)`

## After a verified transcription exists

It still must not be connected to the live aircraft in this phase. Wiring the normalized SI table into
`MavThrustDeck` with frozen provenance, and enabling powered flight, is Phase **5D** work. Phase
**5C-R** is an unpowered cutover and requires `propulsionIntentionallyDisabled = true`.
