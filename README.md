# PipingDataExporter

An OpenPlant Modeler (Bentley CONNECT Edition) AddIn that exports piping intelligence — the business-item data shown in the Explorer > Items tab — to an Excel workbook.

## What it does

- `PipingDataExporter ExportPipingItems` scans every graphic element in the active model, reads all EC business-item properties attached to each one (line number, spec, size, material, tag, component type, etc. — whatever properties exist on that element's item type), and writes them to an `.xlsx` file next to the active DGN.
  - Columns are dynamic: the workbook's header row is the union of every property name found across all scanned elements, so different piping component types (pipes, valves, instruments, ...) each populate only the columns relevant to them.
  - Output file: `<DgnFileName>_PipingItems_<timestamp>.xlsx`, saved in the same folder as the active DGN.
- `PipingDataExporter DrawLine` — a minimal sanity-check command that draws a test line in the active model, useful for confirming the AddIn loaded correctly.

## Requirements

- OpenPlant CONNECT Edition (OpenPlant Modeler) installed, referenced from `C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\`.
- .NET Framework 4.8 SDK / Visual Studio (or `dotnet` CLI with the .NET Framework build tools).

## How to Use

The whole loop, from a source change to an `.xlsx`. Steps 3 and 4 are the ones that are easy to
forget: the DLL is **not** auto-loaded, and it **cannot be overwritten while OpenPlant Modeler is
running**.

### The short version

```
# 1. close OpenPlant Modeler first - it holds a lock on the deployed DLL
dotnet build PipingDataExporter.csproj -c Release -p:Platform=x64
pwsh -File tools/Verify-Keyins.ps1 -DllPath bin/x64/Release/net48/PipingDataExporter.dll
copy bin\x64\Release\net48\PipingDataExporter.dll "C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\"
# then in OpenPlant Modeler's key-in browser:
#   mdl load PipingDataExporter
#   PipingDataExporter ExportPipingItems
```

### 1. Build

```
dotnet build PipingDataExporter.csproj -c Release -p:Platform=x64
```

Produces `bin\x64\Release\net48\PipingDataExporter.dll`. It must be the **x64** build — the host
process is 64-bit.

### 2. Verify the key-ins still resolve

```
pwsh -File tools/Verify-Keyins.ps1 -DllPath bin/x64/Release/net48/PipingDataExporter.dll
```

Skip this only if you changed nothing but method bodies. A renamed class or namespace that
`Commands.xml` no longer matches still builds and still loads — the key-in just silently doesn't
exist, with no error to tell you why.

### 3. Deploy the DLL

Copy **just** `PipingDataExporter.dll` (the `.pdb` too if you want line numbers in error messages)
into the OpenPlant Modeler program folder:

```
C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\
```

Two things to know about this step:

- **Close OpenPlant Modeler first.** Once the AddIn is loaded, the host holds the file open and the
  copy fails with "file in use" — which looks exactly like a deploy that worked, right up until you
  run the key-in and get the old behaviour.
- It needs **administrator** rights (it's under `Program Files`), and a product update or repair may
  remove it, so redeploy after upgrading OpenPlant Modeler.

That folder is `ustation.exe`'s own directory and therefore on the CLR's default probing path, which
is also why the single DLL is all that has to be deployed: `ClosedXML` and every Bentley reference
resolve to the copies already sitting there. See [CLAUDE.md](CLAUDE.md) for why that matters.

### 4. Load the AddIn

Start OpenPlant Modeler, then in the **key-in browser** (`Utilities > Key-in`):

```
mdl load PipingDataExporter
```

An "AddIn Loaded!" message confirms it. This is **per session** — the DLL sits in the program folder
but nothing in the configuration references it, so it does not load on startup. If the key-ins seem
to have vanished, this is almost always why.

> To load it automatically instead, add it to a configuration variable such as `MS_DGNAPPS` in your
> workspace `.cfg`. That is untested here — the per-session `mdl load` above is the path that is
> known to work.

### 5. Run the export

Open a DGN with piping content and make sure the model you want scanned is the **active** model
(the export reads the active model only, not every model in the file). Then:

```
PipingDataExporter ExportPipingItems
```

To check the AddIn is alive without exporting anything, `PipingDataExporter DrawLine` draws a test
line in the active model.

### What you get

An `.xlsx` written **next to the active DGN**:

```
<DgnFileName>_PipingItems_<yyyyMMdd_HHmmss>.xlsx
```

One row per business item (an element with several item classes attached produces several rows),
with `ElementId`, `SchemaName` and `ClassName` first, then one column per property found anywhere in
the model. The timestamp means repeated runs never overwrite each other.

A message box reports the item count and the full output path when it finishes. Read it — it also
reports trouble:

| Message | Meaning |
|---|---|
| `N element(s) skipped due to errors` | Those elements' items couldn't be read; everything else exported normally |
| `WARNING: the scan stopped early` | Enumeration failed partway — **the export does not cover the whole model** |
| `First error: ...` | The first failure behind either of the above |

### If something goes wrong

| Symptom | Cause |
|---|---|
| Key-in not recognised | The AddIn isn't loaded — run `mdl load PipingDataExporter` (step 4) |
| `mdl load` fails to find it | DLL isn't in the OpenPlant Modeler program folder (step 3), or the filename no longer matches `MdlTaskID` |
| AddIn loads but a key-in does nothing | `Commands.xml` `Function=` no longer matches the code — run the verifier (step 2) |
| Changes don't take effect | The copy in step 3 silently failed because OpenPlant Modeler was running; close it and redeploy |
| "Piping Items Export Failed" box | The command caught the error rather than crashing the host; the box has the detail |
| Export is slow on a large model | Expected. Column auto-fit at the end is the slow part and grows with rows × columns |

## Project layout

| Path | Purpose |
|---|---|
| `PipingDataExporter.csproj` | Project file — Bentley SDK references (incl. `ClosedXML.dll`), embeds `Commands.xml` |
| `Commands.xml` | Key-in definitions, mapping key-in text to the C# handler methods |
| `src/PipingDataExporterAddIn.cs` | AddIn entry point |
| `src/KeyinCommand.cs` | Shared key-in plumbing: the mandatory outer catch-all + user messaging |
| `src/ActiveSession.cs` | Active model/file access, with "nothing is open" reported instead of thrown |
| `src/Commands/` | One file per key-in handler (`DrawLineCommand`, `ExportPipingItemsCommand`) |
| `src/Export/` | The export pipeline: scan the model (`ModelItemScanner`) → read EC items off each element (`ElementItemReader`) → accumulate rows and dynamic columns (`ItemTable`) → write the workbook (`ItemWorkbookWriter`) |
| `tools/Verify-Keyins.ps1` | Post-build check that every key-in in `Commands.xml` actually resolves (see below) |

## What the key-in verifier checks

`tools/Verify-Keyins.ps1` (step 2 of [How to Use](#2-verify-the-key-ins-still-resolve)) reflects the
built DLL and checks the things that fail *silently* at runtime rather than at build time:

- every `Function="Namespace.Class.Method"` in `Commands.xml` resolves to a `public static Method(string)`
- the key-in table is embedded under the logical name `CommandTable.xml`
- its root `KeyinTree` element carries the exact required XML namespace
- `MdlTaskID` still matches the built assembly name

It defaults to the Debug build; pass `-DllPath` for another output. Exit code 0 means everything
resolves, 1 lists what doesn't.

See [CLAUDE.md](CLAUDE.md) for implementation notes, API gotchas, and build constraints if you're modifying this AddIn.
