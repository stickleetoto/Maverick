# F/A-18 NASA TM-107601 Acquisition Note V0.1

Status: **ACQUIRED PUBLIC PRIMARY SOURCE — SOURCE/PROVENANCE RECORD ONLY**

Configuration tag: `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`

## Source identity

- Title: *Simulation Model of a Twin-Tail, High Performance Airplane*
- Report: NASA-TM-107601 / N92-33517
- Authors: Carey S. Buttrill, P. Douglas Arbuckle, Keith D. Hoffler
- Organization: NASA Langley Research Center / ViGYAN, Inc.
- Publication: July 1992
- acquired filename: `19920024293.pdf`
- PDF page count: 184
- SHA256: `34ed8edd9452aed3afbd7677c3847b888ec4b234224e0dd016c114564f5bb9f3`
- distribution statement in report metadata: `Unclassified - Unlimited`

## Visually/textually verified anchors

- report pp. 17-20 / PDF pp. 23-26: Table 3.3 dimensional data, Table 3.4 aerodynamic reference dimensions, Table 3.5 weight/CG/inertia.
- report p. 29 / PDF p. 35: Table 5.1 aerodynamic reference center and aerodynamic data-source/configuration statement.
- report pp. 52-58 / PDF pp. 58-64: engine model architecture and installation geometry.
- report pp. 59-68 / PDF pp. 65-74: sensor model.
- report pp. 69-82 / PDF pp. 75-88: actuator models; Tables 8.6 and 8.7 and speedbrake section.
- report pp. 83-129 / PDF pp. 89-135: OFP 8.3.3 inner-loop CAS architecture; pitch/lateral/directional gain/filter tables and scheduled-function figures.

## Source significance

This acquired PDF materially upgrades `f18bas` from a citation-level lead to a primary public source with page-level anchors for reference geometry, source-defined mass properties, control-surface definitions, actuator model documentation, engine-model architecture, sensor models, and simplified OFP 8.3.3 inner-loop control-law documentation.

The report also documents the aerodynamic reconstruction architecture and its A7247/A8575 source lineage. However, the complete numeric aerodynamic lookup arrays and complete engine static-performance lookup tables are not printed in the report. Those remain separate acquisition targets.

## Configuration warning

`f18bas` is a source-defined NASA research simulation. It is not automatically identical to NASA F-18 HARV BuNo 160780 Phase I or to an operational F/A-18A/B/C/D. The preliminary thrust-vectoring representation in TM-107601 uses a different vane concept from the final HARV installation and must remain configuration-separated.

## R0 consequence

- `f18bas` source provenance: `CLOSED_EXACT`
- reference geometry/source-defined mass-property documentation: materially closed for `f18bas`
- actuator/system architecture documentation: materially closed for `f18bas`
- aerodynamic-model structure: closed at architecture/provenance level
- aerodynamic coefficient arrays: still open
- static engine tables: still open
- exact Phase-I HARV field-by-field compatibility: still pending
