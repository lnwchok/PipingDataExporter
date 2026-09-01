# CLAUDE.md

Guidance for working on this repo in this session or future ones.

## What this is

An OpenPlant Modeler (Bentley CONNECT Edition) C# AddIn — a `.NET Framework 4.8` DLL loaded in-process by MicroStation/OpenPlant Modeler, invoked via key-ins. Not a standalone app; it can't run or be tested outside the host application.

Two commands currently exist, both defined in `Commands.xml` and implemented in C#:
- `PipingDataExporter DrawLine` — throwaway scaffolding, draws a test 3D line in the active model.
- `PipingDataExporter ExportPipingItems` — the real feature: walks every graphic element in the active model, reads its EC business-item properties (the same data the Explorer > Items tab shows), and writes them to an `.xlsx` next to the active DGN.

## Build

```
dotnet build PipingDataExporter.csproj -c Debug          # or -c Release -p:Platform=x64
```

Output DLL: `bin/<config>/net48/PipingDataExporter.dll` (or `bin/x64/Release/net48/...` for the Release+x64 combo).

```
pwsh -File tools/Verify-Keyins.ps1                                    # after any build/rename
```

No test suite — beyond that static check, verification is manual, inside OpenPlant Modeler (see README).

## Deploying and running

Verified working end-to-end in OpenPlant Modeler on 2026-08-27, with the `src/` layout below. The
step-by-step version of this lives in README's "How to Use" — keep the two in sync.

- **Deploy target is the OpenPlant Modeler program folder itself**, `C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\` — not
  `mdlapps\` and not `Prg\` (which is the only folder on `MS_ADDINPATH`). That folder is
  `ustation.exe`'s own directory and therefore on the CLR's default probing path, which is what makes
  the single-DLL deploy work at all — and is the same mechanism that resolves the `ClosedXML`
  reference to the host's own copy. Deploy the `-c Release -p:Platform=x64` output; the host is 64-bit.
- **Nothing in the configuration references the DLL, so it does not auto-load.** Each session begins
  with the key-in `mdl load PipingDataExporter` (the argument is the `MdlTaskID` / assembly name).
  An "AddIn Loaded!" message confirms it. A "key-in not recognised" report almost always means this
  step was skipped, not that the key-in table is broken.
- **The host keeps the deployed DLL open while the AddIn is loaded.** Close OpenPlant Modeler before
  copying a new build over it: otherwise the copy fails with a sharing violation and testing silently
  continues against the previous binary.

## Hard constraints — get these wrong and it fails silently

- **`MdlTaskID` must equal the built DLL's filename** (no extension). `PipingDataExporter.csproj` sets `<AssemblyName>PipingDataExporter</AssemblyName>` explicitly rather than relying on the `.csproj` filename, so renaming the project file alone is not enough — keep both in sync if you ever rename again.
- **`Commands.xml` KeyinTree schema is strict about namespace.** The root element must be `<KeyinTree xmlns="http://www.bentley.com/schemas/1.0/MicroStation/AddIn/KeyinTree.xsd">`. Get the namespace wrong and MicroStation loads the AddIn but silently fails to register any key-ins — no error, the commands just don't exist. `KeyinHandler Function=` values are `Namespace.ClassName.MethodName` strings, not compiled references — a typo there fails the same silent way.
- `Commands.xml` is embedded as a resource (`LogicalName=CommandTable.xml`) via the `<EmbeddedResource>` block in the `.csproj` — it is not deployed as a loose file.
- Reference assemblies are pulled straight from the installed product via absolute `HintPath`s (`C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\...`, all `<Private>False</Private>`). This repo only builds on a machine with OpenPlant CONNECT Edition installed at that path. If assembly resolution errors mention a missing type, it's almost always a missing `HintPath` reference to one more Bentley DLL — see the dependency notes below before guessing.
- After a project rename, delete `obj/`/`bin/` before rebuilding — MSBuild's incremental cache holds onto the old assembly name and produces confusing partial-success states otherwise.
- **Don't add `ClosedXML` as a NuGet `PackageReference`.** OpenPlant Modeler already ships its own `ClosedXML.dll` (v0.96.0.0, plus `DocumentFormat.OpenXml.dll`/`ExcelNumberFormat.dll`) directly in the `OpenPlantModeler\` install folder for its own reporting features, and that folder is on the CLR's default probing path (it's `ustation.exe`'s own directory). Referencing a different version pulls in a second copy with a mismatched identity, which fails at runtime — not at compile time — with `System.IO.FileLoadException: ... manifest definition does not match the assembly reference`. `PipingDataExporter.csproj` instead references Bentley's exact `ClosedXML.dll` via `HintPath` with `<Private>False</Private>`, same pattern as the other Bentley references, so there's only ever one copy in play. If you need a newer ClosedXML API, reflect the installed DLL first to confirm the method actually exists there.
- **A command method invoked directly from a key-in is the top of the managed call stack — nothing native catches what escapes it.** An unhandled exception there crashes the entire OpenPlant Modeler process (Bentley's crash logger records it as CLR exception `0xe0434352`, usually with no captured message). Worse, `StackOverflowException` and native access-violation/corrupted-state exceptions from Bentley's interop layer are architecturally uncatchable in .NET Framework regardless of try/catch placement — if a crash reproduces identically after adding a top-level try/catch, that's the signature to look for, and the fix is to isolate/avoid the offending native call, not to add more catches. Every `KeyinHandler`-invoked method should have an outer `try/catch` that reports failures via `MessageCenter` instead of letting them propagate — route every handler through `KeyinCommand.Run` (`src/KeyinCommand.cs`) rather than writing the `try/catch` by hand. Note also that a `foreach` loop's `try/catch` around the loop *body* does not protect against the enumerator's `MoveNext()` throwing — that happens outside the body on each iteration, so a bad element mid-enumeration is still unprotected unless you drive the enumerator manually (see `ModelItemScanner.Scan`'s explicit `IEnumerator<Element>` loop).

## OpenPlant / DgnPlatformNET API notes (verified by reflecting the installed SDK, not from docs)

The managed API surface lives under `Bentley.DgnPlatformNET.*` (the `Bentley.DgnPlatform.*` namespace without "NET" is the raw native-interop layer — its types like `DgnECManager` are opaque native handle structs with no usable managed members; don't reference that namespace directly).

Reading an element's business-item / EC data (what backs Explorer > Items):
```csharp
DgnModel activeModel = Session.Instance.GetActiveDgnModel();
foreach (Element element in activeModel.GetElements())
{
    DgnECInstanceCollection instances = DgnECManager.Manager.GetElementProperties(
        element, ECQueryProcessFlags.SearchAllClasses);   // Bentley.DgnPlatformNET.DgnEC

    foreach (IDgnECInstance instance in instances)
    {
        var container = (IECValueContainer)instance;       // Bentley.ECObjects.Instance
        using (var e = container.GetEnumerator(true))       // true = includeAll (recurse structs)
        {
            while (e.MoveNext())
            {
                IECPropertyValue pv = e.Current;
                string column = pv.AccessString;
                string value  = pv.IsNull ? "" : pv.StringValue;
            }
        }
    }
}
```
Key gotchas found by reflection, not obvious from IntelliSense alone:
- `IECInstance` (real type: `Bentley.ECObjects.Instance.IECInstance`, defined in `Bentley.ECObjects3.dll`) does *not* expose `ClassDefinition` itself — that property lives on `IECNamedValueContainer`, one of the interfaces `IDgnECInstance` composes in. Cast/access through the instance directly (`instance.ClassDefinition` resolves fine via interface composition), but don't go looking for it on `IECInstance`'s own declared members.
- Plain `foreach (var pv in instance)` only walks top-level properties (parameterless `IEnumerable<IECPropertyValue>.GetEnumerator()`). To also get nested struct/array properties (needed for a complete "all properties" dump), cast to `IECValueContainer` and call `GetEnumerator(includeAll: true)` explicitly, as above.
- `Bentley.ECObjects3.dll` isn't referenced by `ustation`/`Bentley.DgnPlatformNET` by default — it lives at `Assemblies\ECFramework\Bentley.ECObjects3.dll` under the product install dir, not next to the main DLLs. `Bentley.Platform.dll` (same `ECFramework` folder) is a further transitive dependency needed once you touch `IECValueContainer`/`IECClass` (its `IExtendable` interface lives there) — the compiler error for a missing reference here is `CS0012`, not a missing-type error, which makes it non-obvious what to add.
- `Session.Instance.GetActiveDgnFile().GetFileName()` returns the full path (not just the filename) — used to derive the export's output directory.

If you need to explore further Bentley API surface and the reference docs aren't at hand, reflecting the installed DLLs directly (via `[Reflection.Assembly]::LoadFrom` in PowerShell, catching `ReflectionTypeLoadException` and loading transitive deps as errors surface) is reliable and was how everything above was confirmed — guessing method signatures for this SDK is not, since wrong native-interop signatures tend to fail silently or throw unhelpful marshaling errors rather than clean compile errors.

## Conventions

- Source lives under `src/`: `Commands/` holds one file per key-in handler, `Export/` holds the export pipeline, and the root holds the AddIn entry point plus shared plumbing (`KeyinCommand`, `ActiveSession`). New commands get their own file in `Commands/`. `ModelItemScanner` owns the single `GetElementProperties` call per element and materialises the result into a `List<IDgnECInstance>` that it feeds to *both* `ElementItemReader` (→ `ItemTable` → `PipingItems` sheet) and `WeldJointReader` (→ `WeldJointTable` → `WeldJoints` sheet); `ItemWorkbookWriter` writes both sheets through one generic routine. Don't push the EC call back down into a reader — that would double the interop cost of the pass.
- Handler classes and their methods must be `public static` and take a single `string` — and the class must stay in a namespace that matches its `Function=` string. Supporting types are `internal`.
- Keep key-in handlers thin: guard, resolve the active model/file, call into `Export/`, report. Nothing that touches the Bentley API belongs inline in a handler.
- `tools/` holds developer-side helpers that are not part of the DLL: `Verify-Keyins.ps1` (the post-build key-in check) and `boq_report.html`, a self-contained browser page that reads an exported `.xlsx` client-side and renders a Bill of Quantities. See the BOQ report notes below before editing it.

## The item level nested under a Weld (Explorer > Items)

Explorer > Items shows a `SOCKET_WELD_JOINT` node *underneath* each `Weld` node. Verified against the
installed schemas (`OpenPlant_3D.01.08.ecschema.xml` under
`OpenPlant CONNECT Edition\OrthographicsManager\Settings\Backup\Metric\DataSet\Schemas\`):

- **`SOCKET_WELD_JOINT` is not an EC class.** It is a `JOINT_NAME` value from the joint-type catalog in
  `OpenPlant_3D_Supplemental_Modeling.01.08.ecschema.xml`. Grepping the schemas for a class by that
  name is a dead end.
- The nested node is a **`JOINT`** instance ("Indicates two ports are connected"). `JOINT` carries an
  `InstanceLabelSpecification` custom attribute naming `TYPE`, which is why its tree node reads
  `SOCKET_WELD_JOINT`. Its properties are `TYPE`, `PORT1_GUID`, `PORT2_GUID` plus the audit fields
  inherited from `PLANT_BASE_OBJECT`.
- `WELD` derives from `FASTENER`, and the link is `ECRelationshipClass JOINT_HAS_FASTENER`
  (Source `JOINT`, Target `FASTENER`, `strengthDirection="forward"`) — so from a weld it is traversed
  **`RelatedInstanceDirection.Backward`**. `PORT_HAS_JOINT` reaches the same data from the other side.
- `Bentley.OpenPlantModeler.SDK.dll` has **no** weld/joint API (only `ComponentConnectivity` /
  `PipingTapConnection`). The EC route is the only route.

**Which runtime mechanism yields the JOINT was settled by running the export against a real model.**
Three were tried; the results, so nobody re-derives them:

| Path | Result |
| --- | --- |
| `IECInstance.GetRelationshipInstances()` on the weld instance | **Works — this is what `WeldJointReader` uses.** |
| An `ECQuery` over the weld class with a `JOINT_HAS_FASTENER`/Backward `RelatedInstanceSelectCriteria`, run through `DgnECManager.Manager.FindInstances` | Also works, and returns the same items. Dropped as redundant — it cost two extra `ECFramework` references (`Bentley.EC.Persistence3`, `Bentley.ECSystem3`) and a per-element `FindInstances` call. Restore it from git history if the simple path ever comes back empty. |
| The JOINT as a second EC instance on the weld's own element | No — the sibling instances on a weld element are not the JOINT. They do carry the tracking properties, though, which is why `WeldJointReader` still reads them (see below). |

Rows are deduped by class + instance id within one weld.

**A JOINT has nothing you can track it by** — its properties are only `TYPE`, `PORT1_GUID`,
`PORT2_GUID` and audit fields. No line number, no size, no spec. So every `WeldJoints` row also
carries the weld element's own properties under a `Weld.` prefix: the weld instance first, then the
other EC instances sharing its element, first non-empty value per name winning. Merging the whole
element rather than naming the fields is deliberate — the schema defines `SPECIFICATION`, `SPECID`,
`DESIGN_SPECIFICATION`, `NOMINAL_DIAMETER` *and* `NOMINAL_SIZE`, and which ones a given dataset
populates is not knowable from the schema. Blanks are skipped rather than claimed, so a name the weld
leaves empty can still be filled in by a sibling instance.

Related API notes, all confirmed by reflection:

- Relationships are delay-instantiated (`IECRelationshipInstanceCollection` raises
  `RelationshipDelayInstantiated`), so enumerating one does native work and its `MoveNext()` must be
  driven by hand like `ModelItemScanner.Scan`'s element loop.
- If the `ECQuery` path is ever restored it needs `Bentley.EC.Persistence3` and `Bentley.ECSystem3`
  referenced from `Assemblies\ECFramework\` (missing them surfaces as `CS0012`, not a missing-type
  error), and note that `ECQuery.SelectClause` has **no setter** — the returned `SelectCriteria` has
  to be mutated in place.
- `IECValueContainer.GetEnumerator(includeAll: true)` takes **one** bool (`IECInstance` has a
  two-bool overload). `IECPropertyValue.TryGetStringValue(out string)` avoids a try/catch per value.
- `InstanceLabel` (`src/Export/InstanceLabel.cs`) approximates the tree label by probing
  `TYPE`/`NAME`/... rather than evaluating `InstanceLabelSpecification` custom attributes. If a class
  labels itself from some other property the raw value is still in its own column. The resulting
  `Label` column on the `WeldJoints` sheet is what a BOQ counts weld joints by, so a change to that
  probe order changes BOQ output.

## BOQ report (`tools/boq_report.html`)

A single standalone HTML file with SheetJS vendored inline — no build step and no server, so edit the file directly and reload the browser. It starts empty and parses whatever `.xlsx` you hand it; no project data is baked in. IBM Plex is pulled from Google Fonts, so it falls back to Segoe UI offline — layout is unaffected.

Two tabs share one aggregation pipeline (`sizeInfo` → `aggregateSummary` / `aggregateLineItems` → `renderSummaryRows` / `renderRows`): **Overall Summary** (whole model) and **By Line Number** (grouped by `LINENUMBER`). Both sort by material code first, blanks last.

**Welds are deliberately excluded from the material BOQ.** `WELD` is not in `CLASS_ORDER`, and `handleFile` filters `ClassName === "WELD"` out of the `PipingItems` rows before aggregating. Weld quantity comes from the `WeldJoints` sheet instead — that is the only place the joint *type* exists (`SOCKET_WELD_JOINT` vs `BUTT_WELD_JOINT`, which price differently), and the two sheets are 1:1, so counting both would double them. If you ever put `WELD` back into `CLASS_ORDER`, drop the `WeldJoints` read at the same time.

Weld joints run through their own small pipeline (`aggregateWeldJoints` → `renderWeldRows`), keyed on joint label × `Weld.NOMINAL_DIAMETER` × spec, quantity a plain count. They surface in two places, in two different framings:

- The **Weld Joints** panel below the material table in the Summary tab — the construction take-off, carrying the inch size and **WID** (weld inch-diameter: nominal inches × joint count, unit `DB`). The total is also a stat tile.
- Inside each line group in By Line Number, **appended to the material table itself** rather than a table of their own — that tab is the checking view, read straight down, so joints continue the list after the materials. No WID there. `renderRows` takes a `weldFromIndex` so the first weld row can be given a divider.

Line groups are the *union* of both sheets' line numbers, not just the material sheet's.

- **Nominal size is a designation, not a measurement** — DN15 is 1/2", not 15/25.4 = 0.59". WID comes off the `DN_TO_NPS` table in `nominalInches`; the pre-existing `mmToNearestEighthInch` is for real measured lengths (bolt diameters) and must not be used here. This dataset carries sizes the standard has no entry for (30 and 75), so `nominalInches` snaps to the nearest DN and marks the cell with `*` plus a hover note rather than silently presenting it as exact.
- **Weld spec comes from `Weld.SERVICE`, not `Weld.SPECIFICATION`** — the latter is blank on all 1297 welds in this dataset, while `SERVICE` agrees with `SPECIFICATION` wherever the material rows carry both. Ten welds report `?`; those fall back to the dominant `SPECIFICATION` of their line's material via `specOfLine`.
- Weld-joint items are shaped with the same `cls`/`desc`/`size`/`spec`/`rating`/`pieceMark` fields as material items, so `rowMatches` filters them and `renderRows` draws them unchanged. `rating` stays blank (a JOINT has none), so an active rating filter empties the weld tables; the spec and size filters work on both sides. Keep the shape if you touch `aggregateWeldJoints`.
- An export with no `WeldJoints` sheet still loads: `hasWeldSheet` is false, the weld table shows a "re-export" note, and the stat tiles read — rather than 0.
- **Lines legitimately have no welds.** In `PIP_01` 11 of 99 lines have none, and every one is correct: the `SG` lines are galvanized NPT/threaded piping (unions and screwed fittings) and the `T1xx-N8-*` lines are bolted flange tie-ins. Verified end to end — all 1297 `WELD` elements produced exactly one JOINT and no JOINT was orphaned — so an empty weld list is a modelling fact, not a dropped relationship. Re-check that ratio before suspecting `WeldJointReader`.

- **Column mapping depends on the exporter's property names, and a wrong one fails silently** — a renamed or unpopulated property renders as an empty cell, never an error. Check a real export before trusting a new mapping.
- **`NOMINAL_DIAMETER_RUN_END` repeats the *reducing* (small) end on a `CONCENTRIC_PIPE_REDUCER`.** The large end must come from `NOMINAL_DIAMETER`; reading the run end makes both size columns identical (a 40 × 30 reducer renders "30 × 30"). `EQUAL_PIPE_TEE` is deliberately excluded from the branch-end rule for the same reason — its branch always repeats `NOMINAL_DIAMETER`.
- Items keep fields they no longer display: By Line hides `cls` and `rating` but still sorts on `classRank` and filters on both, and every item keeps the combined `size` string because the size dropdown matches on it. Don't drop these when editing the renderers.
- **A published "Piping BOQ" artifact exists as a separate copy and has diverged from this file before.** Publishing does not write back to the repo, and editing here does not update the artifact. Treat this file as the source of truth, and decide explicitly whether a change warrants republishing.
- `Commands.xml` handler `Function=` strings must be updated in lockstep with any namespace/class/method rename — nothing checks this at compile time, and a mismatch fails silently at runtime. `pwsh -File tools/Verify-Keyins.ps1` catches it after a build: it reflects the built DLL and resolves every handler, plus checks the embedded resource name, the KeyinTree namespace, and `MdlTaskID`. Run it after any rename.
