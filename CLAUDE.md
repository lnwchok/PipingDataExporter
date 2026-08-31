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

- Source lives under `src/`: `Commands/` holds one file per key-in handler, `Export/` holds the export pipeline (`ModelItemScanner` → `ElementItemReader` → `ItemTable` → `ItemWorkbookWriter`), and the root holds the AddIn entry point plus shared plumbing (`KeyinCommand`, `ActiveSession`). New commands get their own file in `Commands/`.
- Handler classes and their methods must be `public static` and take a single `string` — and the class must stay in a namespace that matches its `Function=` string. Supporting types are `internal`.
- Keep key-in handlers thin: guard, resolve the active model/file, call into `Export/`, report. Nothing that touches the Bentley API belongs inline in a handler.
- `tools/` holds developer-side helpers that are not part of the DLL: `Verify-Keyins.ps1` (the post-build key-in check) and `boq_report.html`, a self-contained browser page that reads an exported `.xlsx` client-side and renders a Bill of Quantities. The latter vendors SheetJS inline — there is no build step, so edit the file directly, and note that the BOQ column mapping depends on the exporter's property names (e.g. `NOMINAL_DIAMETER_REDUCING_END`); a renamed or unpopulated property renders as an empty cell rather than an error.
- `Commands.xml` handler `Function=` strings must be updated in lockstep with any namespace/class/method rename — nothing checks this at compile time, and a mismatch fails silently at runtime. `pwsh -File tools/Verify-Keyins.ps1` catches it after a build: it reflects the built DLL and resolves every handler, plus checks the embedded resource name, the KeyinTree namespace, and `MdlTaskID`. Run it after any rename.
