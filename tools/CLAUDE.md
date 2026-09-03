# tools/ — developer-side helpers

Loaded when working under `tools/`. Project constraints live in the root [CLAUDE.md](../CLAUDE.md).

## BOQ report (`tools/boq_report.html`)

A single standalone HTML file with SheetJS vendored inline — no build step and no server, so edit the file directly and reload the browser. It starts empty and parses whatever `.xlsx` you hand it; no project data is baked in. IBM Plex is pulled from Google Fonts, so it falls back to Segoe UI offline — layout is unaffected.

Two tabs share one aggregation pipeline (`sizeInfo` → `aggregateSummary` / `aggregateLineItems` → `renderSummaryRows` / `renderRows`): **Overall Summary** (whole model) and **By Line Number** (grouped by `LINENUMBER`). Both sort by material code first, blanks last.

**Export XLSX** sits in the Overall Summary panel head and writes that material table — and only it —
to a one-sheet workbook (`Overall Summary`) through the same vendored SheetJS, which is the full build,
so `aoa_to_sheet` / `book_new` / `writeFile` are all there. It exports the **unfiltered** `overall`
array on purpose: a take-off that silently carried whatever the filter bar happened to be set to would
be indistinguishable from a complete one once it left the page. Quantities are written as numbers, not
the renderers' `toFixed(2)` strings, so the Qty column sums in Excel; a blank piece mark exports blank
rather than the table's placeholder dash. Weld joints are not in the file. Note that `writeFile` only
has the browser download path in this bundle (no Node `fs`), and that a *published artifact* copy of
this page cannot download at all — the viewer sandbox blocks page-initiated downloads, so the button
works from the local file, not from the artifact.

**Welds are deliberately excluded from the material BOQ.** `WELD` is not in `CLASS_ORDER`, and `handleFile` filters `ClassName === "WELD"` out of the `PipingItems` rows before aggregating. Weld quantity comes from the `WeldJoints` sheet instead — that is the only place the joint *type* exists (`SOCKET_WELD_JOINT` vs `BUTT_WELD_JOINT`, which price differently), and the two sheets are 1:1, so counting both would double them. If you ever put `WELD` back into `CLASS_ORDER`, drop the `WeldJoints` read at the same time.

Weld joints run through their own small pipeline (`aggregateWeldJoints` → `renderWeldRows`), keyed on joint label × `Weld.NOMINAL_DIAMETER` × spec, quantity a plain count. They surface in two places, in two different framings:

- The **Weld Joints** panel below the material table in the Summary tab — the construction take-off. It reports **WID** only (weld inch-diameter: nominal inches × joint count, unit `DB`), four columns: joint type, spec, WID, unit. Size and joint count are *inputs to that number, not columns* — `rollUpWeldJoints` collapses the per-size items to one row per joint type × spec. The total is also a stat tile. Roll-up happens **after** `rowMatches`, so the size filter still reaches this table even though size is not displayed; don't move it earlier.
- Inside each line group in By Line Number, **appended to the material table itself** rather than a table of their own — that tab is the checking view, read straight down, so joints continue the list after the materials. No WID there. `renderRows` takes a `weldFromIndex` so the first weld row can be given a divider.

Line groups are the *union* of both sheets' line numbers, not just the material sheet's.

- **Nominal size is a designation, not a measurement** — DN15 is 1/2", not 15/25.4 = 0.59". WID comes off the `DN_TO_NPS` table in `nominalInches`; the pre-existing `mmToNearestEighthInch` is for real measured lengths (bolt diameters) and must not be used here. This dataset carries sizes the standard has no entry for (30 and 75), so `nominalInches` snaps to the nearest DN and records it. Once rolled up nearly every row contains one, so `approxNote` states the caveat once beside the panel's total rather than marking rows — keep it stated somewhere if you change that table.
- **Weld spec comes from the line's material `SPECIFICATION`, never from `Weld.SERVICE`.** `SPECIFICATION` (`VP`, `S6`, `SG`, `S4`) and `SERVICE` (`FW`, `PW`, `KW`, `WW`, `CSC`, …) are different fields with different vocabularies — the material rows pair them freely (`VP`/`FW`, `VP`/`KW`, `S6`/`RG`), and the line number spells out both: `25-FW5020-S6TE` is service `FW` in spec `S6`. They coincide only for `SG` and `VP`, which is what made an earlier version fill the weld Spec column from `SERVICE` — that was wrong and put service codes under a Spec heading. The `WeldJoints` sheet exports no `Weld.SPECIFICATION` column at all, so `aggregateWeldJoints` reads it if it ever appears and otherwise takes the dominant `SPECIFICATION` of the material on the weld's line via `specOfLine`; in `PIP_01` that covers all 1848 joints (VP 1538, SG 162, S6 131, S4 17).
- Weld-joint items are shaped with the same `cls`/`desc`/`size`/`spec`/`rating`/`pieceMark` fields as material items, so `rowMatches` filters them and `renderRows` draws them unchanged. `rating` stays blank (a JOINT has none), so an active rating filter empties the weld tables; the spec and size filters work on both sides. Keep the shape if you touch `aggregateWeldJoints`.
- An export with no `WeldJoints` sheet still loads: `hasWeldSheet` is false, the weld table shows a "re-export" note, and the stat tiles read — rather than 0.
- **Lines legitimately have no welds.** In `PIP_01` 11 of 99 lines have none, and every one is correct: the `SG` lines are galvanized NPT/threaded piping (unions and screwed fittings) and the `T1xx-N8-*` lines are bolted flange tie-ins. Verified end to end — all 1297 `WELD` elements produced exactly one JOINT and no JOINT was orphaned — so an empty weld list is a modelling fact, not a dropped relationship. Re-check that ratio before suspecting `WeldJointReader`.

- **Column mapping depends on the exporter's property names, and a wrong one fails silently** — a renamed or unpopulated property renders as an empty cell, never an error. Check a real export before trusting a new mapping.
- **`NOMINAL_DIAMETER_RUN_END` repeats the *reducing* (small) end on a `CONCENTRIC_PIPE_REDUCER`.** The large end must come from `NOMINAL_DIAMETER`; reading the run end makes both size columns identical (a 40 × 30 reducer renders "30 × 30"). `EQUAL_PIPE_TEE` is deliberately excluded from the branch-end rule for the same reason — its branch always repeats `NOMINAL_DIAMETER`.
- Items keep fields they no longer display: By Line hides `cls` and `rating` but still sorts on `classRank` and filters on both, and every item keeps the combined `size` string because the size dropdown matches on it. Don't drop these when editing the renderers.
- **A published "Piping BOQ" artifact exists as a separate copy and has diverged from this file before.** Publishing does not write back to the repo, and editing here does not update the artifact. Treat this file as the source of truth, and decide explicitly whether a change warrants republishing.
