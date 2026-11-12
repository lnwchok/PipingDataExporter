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
## Conventions

- Source lives under `src/`: `Commands/` holds one file per key-in handler, `Export/` holds the export pipeline, and the root holds the AddIn entry point plus shared plumbing (`KeyinCommand`, `ActiveSession`). New commands get their own file in `Commands/`. `ModelItemScanner` owns the single `GetElementProperties` call per element and materialises the result into a `List<IDgnECInstance>` that it feeds to *both* `ElementItemReader` (→ `ItemTable` → `PipingItems` sheet) and `WeldJointReader` (→ `WeldJointTable` → `WeldJoints` sheet); `ItemWorkbookWriter` writes both sheets through one generic routine. Don't push the EC call back down into a reader — that would double the interop cost of the pass.
- Handler classes and their methods must be `public static` and take a single `string` — and the class must stay in a namespace that matches its `Function=` string. Supporting types are `internal`.
- Keep key-in handlers thin: guard, resolve the active model/file, call into `Export/`, report. Nothing that touches the Bentley API belongs inline in a handler.
- `tools/` holds developer-side helpers that are not part of the DLL: `Verify-Keyins.ps1` (the post-build key-in check), `Publish-ToShare.ps1` (copies the Release x64 DLL + `boq_report.html` + `README.md` to the team share; `-Build` rebuilds first), and `boq_report.html`, a self-contained browser page that reads an exported `.xlsx` client-side and renders a Bill of Quantities. See [tools/CLAUDE.md](tools/CLAUDE.md) before editing it.

## Notes that live closer to the code

These load automatically when you work in the directory, so they are not repeated here:

- [src/CLAUDE.md](src/CLAUDE.md) — OpenPlant / DgnPlatformNET API notes, verified by reflecting the installed SDK.
- [src/Export/CLAUDE.md](src/Export/CLAUDE.md) — the item level nested under a Weld (Explorer > Items), and how `WeldJointReader` reaches it.
- [tools/CLAUDE.md](tools/CLAUDE.md) — the BOQ report (`tools/boq_report.html`).
