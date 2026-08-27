# PipingDataExporter

An OpenPlant Modeler (Bentley CONNECT Edition) AddIn that exports piping intelligence — the business-item data shown in the Explorer > Items tab — to an Excel workbook.

## What it does

- `PipingDataExporter ExportPipingItems` scans every element in the active model, reads all EC business-item properties attached to each one (line number, spec, size, material, tag, component type, etc. — whatever properties exist on that element's item type), and writes them to an `.xlsx` file next to the active DGN.
  - Columns are dynamic: the workbook's header row is the union of every property name found across all scanned elements, so different piping component types (pipes, valves, instruments, ...) each populate only the columns relevant to them.
  - Output file: `<DgnFileName>_PipingItems_<timestamp>.xlsx`, saved in the same folder as the active DGN.
- `PipingDataExporter DrawLine` — a minimal sanity-check command that draws a test line in the active model, useful for confirming the AddIn loaded correctly.

## Requirements

- OpenPlant CONNECT Edition (OpenPlant Modeler) installed, referenced from `C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\`.
- .NET Framework 4.8 SDK / Visual Studio (or `dotnet` CLI with the .NET Framework build tools).

## Build

```
dotnet build PipingDataExporter.csproj -c Release -p:Platform=x64
```

This produces `bin\x64\Release\net48\PipingDataExporter.dll`. It has no bundled dependencies to deploy alongside it — `ClosedXML` is referenced directly against the copy OpenPlant Modeler already ships in its own install folder (rather than pulled from NuGet), so only the single DLL (and its `.pdb`, optionally) needs to go wherever your AddIn load config points. See [CLAUDE.md](CLAUDE.md) for why.

## Install / register the AddIn

1. Build the project (above).
2. Point OpenPlant Modeler's AddIn load configuration (workspace `mdlapps.cfg` or your organization's MicroStation configuration variables) at `PipingDataExporter.dll` in the build output folder.
3. Launch OpenPlant Modeler — the AddIn loads automatically and shows an "AddIn Loaded!" message.

## Usage

In OpenPlant Modeler's key-in browser:

```
PipingDataExporter ExportPipingItems
```

Open a DGN with piping content first, and make sure the model you want scanned is the *active* model. A message box reports how many items were exported and where the `.xlsx` was saved once it finishes.

## Project layout

| File | Purpose |
|---|---|
| `PipingDataExporter.csproj` | Project file — Bentley SDK references (incl. `ClosedXML.dll`), embeds `Commands.xml` |
| `TestAddIn.cs` | AddIn entry point (`TestAddIn`) + the `DrawLine` sanity-check command |
| `PipingItemsExporter.cs` | The `ExportPipingItems` command |
| `Commands.xml` | Key-in definitions, mapping key-in text to the C# methods above |

See [CLAUDE.md](CLAUDE.md) for implementation notes, API gotchas, and build constraints if you're modifying this AddIn.
