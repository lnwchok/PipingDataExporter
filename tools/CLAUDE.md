# tools/ — developer-side helpers

Loaded when working under `tools/`. Project constraints live in the root [CLAUDE.md](../CLAUDE.md).

## Team share (`tools/Publish-ToShare.ps1`)

Copies exactly two files — `bin\x64\Release\net48\PipingDataExporter.dll` and `boq_report.html` — to
`\\192.165.0.5\Piping_Group\XX_Backup\Sitthichok\Devs\OpenPlant_BQ_Extractor` (override with
`-Destination`), overwriting what is there. It copies whatever DLL is on disk; pass `-Build` after any
C# change, or the share gets a stale binary. Don't add `ClosedXML`/OpenXml DLLs or the `.pdb` — the
host ships its own ClosedXML (see root CLAUDE.md) and teammates don't debug.

## BOQ report (`tools/boq_report.html`)

A single standalone HTML file with SheetJS vendored inline — no build step and no server, so edit the file directly and reload the browser. It starts empty and parses whatever `.xlsx` you hand it; no project data is baked in. IBM Plex is pulled from Google Fonts, so it falls back to Segoe UI offline — layout is unaffected.

Two tabs share one aggregation pipeline (`sizeInfo` → `aggregateSummary` → `renderSummaryRows` / `renderRows`; By Line calls `aggregateSummary` once per line group): **Overall Summary** (whole model) and **By Line Number** (grouped by `LINENUMBER`). Both sort by material code first, blanks last.

**There are two exports, and they answer to filters differently on purpose.**

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

**Export selected** sits in the By Line Number panel head and writes the *ticked* line groups to one
`By Line BOQ` sheet: `Line Number | Type | Material Code | Description | Size 1 | Size 2 | Spec | Qty |
Unit`, one row block per line in page order, materials then weld joints. Weld joints **are** in this
one — unlike the summary export — because the By Line tab is the checking view, where joints already
continue the material table; `Type` (`Material` / `Weld Joint`) is what tells the two kinds of row
apart once they share a sheet, and a joint's Material Code is blank because it has none. Qty is a
number and blank codes export blank, same as the summary export.

Unlike the summary export it **does** carry the filter bar — this sheet is "the lines I ticked, as I
am looking at them". That makes a partial take-off possible, which is exactly what the summary export
refuses to risk, so the file is made to identify itself instead: the name becomes
`<base>-BOQ-Lines-filtered.xlsx` and the status line says the filters were applied. Don't quietly drop
either signal. If every selected line filters down to nothing it reports that and writes no file —
a header-only workbook would read as a complete export of nothing.

The **line-number filter** next to it is a substring match (`lineMatches`), not a dropdown: line
numbers spell out size, service and spec (`25-FW5020-S6TE`), so typing `FW` or `S6TE` sweeps a whole
family. It has to live in that panel head rather than the shared `.filter-bar` because *items carry
no line number* — only the enclosing `lines[]` group does — so the filter is meaningless on the
Summary tab. For the same reason `lineFilterActive()` is kept out of `isFilterActive()`: folding it in
would make the summary table and `#result-count` report themselves as filtered when nothing on that
tab had changed.

Two things about the selection are load-bearing. `render()` rebuilds `#line-list` wholesale, so the
ticks live in the module-level `selectedLines` and are re-applied from it on every pass — otherwise a
keystroke in the search box wipes them; and the per-line `change`/`click` handlers are **delegated on
`#line-list`**, with `stopPropagation()` on the click, because a checkbox inside `<summary>` toggles
its `<details>` as well as itself. The tick handler deliberately does *not* call `render()` (that
would collapse every open group mid-interaction) and "Clear filters" deliberately does not clear the
ticks — a selection is a choice the user made, not a view setting. "Select all" is scoped to the
visible lines; a ticked line the filter has since hidden still exports, and the counter says how many
those are rather than letting them vanish.

**`VALVE_WRENCH` and `HAND_WHEEL` are excluded from the material BOQ too.** They are valve operators, modelled as separate elements sitting on a valve that is already counted, so keeping them inflated valve quantities. `handleFile` filters them out alongside `WELD` (via `VALVE_OPERATOR_CLASSES`) before aggregating, so they appear in neither the Summary nor By Line Number.

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
