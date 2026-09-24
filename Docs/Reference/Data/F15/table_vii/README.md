# Baumann Table VII — transcription

These files transcribe Baumann, AFIT/GAE/ENY/89D-01 (DTIC ADA217366), Appendix C, Table VII, "Tabulation of Low α Equilibrium Conditions", PDF pp.124–133.

| File | What |
|---|---|
| `baumann_table_vii_part1.txt` | first half, PDF pp.124–128: `point stabilator_deg alpha_deg beta_deg p_rad_s` |
| `baumann_table_vii_part2.txt` | second half, PDF pp.129–133: `point q_rad_s r_rad_s theta_deg phi_deg V_kft_s` |
| `gen_table_vii.py` | writes `Assets/.../Validation/MavF15BaumannTableViiData.cs` from the two files |

- **Values:** each value is the printed decimal, verbatim.
- **Illegible fields:** `NaN`.
- **Notes:** text after `#` says what was damaged in the scan and how the stored value was obtained.
- **Row numbers:** each half keeps its **printed** point numbers. How the halves pair into equilibrium states is a finding, documented in `Docs/Reference/F15_TABLE_VII_EQUILIBRIUM_VALIDATION_V1.0.md` §2. It is not encoded here.

**How it was read:**
1. Every row was read from the rendered page images, at 150 dpi for the table and 400–500 dpi zooms for damaged glyphs.
2. That reading was checked value by value against the PDF's text layer.
3. Every disagreement was re-read on the image.

The text layer drops or garbles some rows (e.g. first-half points 7–8, second-half point 13). Everywhere else the two readings agree verbatim.

Regenerate after any correction:

```
python gen_table_vii.py "../../../../../Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannTableViiData.cs"
```
