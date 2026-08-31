# PipingDataExporter

A small AddIn for OpenPlant Modeler (Bentley CONNECT Edition) that exports your piping data to Excel.

## What it does

Scans every element in the active model, reads its business-item data (the same data shown in Explorer > Items), and writes it to an `.xlsx` next to your open DGN. Columns adapt automatically to whatever properties your pipes, valves, and instruments actually have.

## Requirements

- OpenPlant CONNECT Edition (OpenPlant Modeler) installed
- .NET Framework 4.8 SDK (or Visual Studio) to build it

## Install

Build it, then copy the DLL into OpenPlant Modeler's own program folder — that's the only deploy step needed. **Close OpenPlant Modeler first**, or the copy will silently fail because the running app has the file locked.

```
dotnet build PipingDataExporter.csproj -c Release -p:Platform=x64
```

Copy DLL file to OpenPlant Folder

```
copy bin\x64\Release\net48\PipingDataExporter.dll "C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler\"
```

## How to Use

Open OpenPlant Modeler and load the AddIn once per session, then run the export from the key-in browser (`Utilities > Key-in`). The `.xlsx` shows up right next to your DGN file when it's done.

```
mdl load PipingDataExporter
PipingDataExporter ExportPipingItems
```

## BOQ report

`tools/boq_report.html` turns an export into a Bill of Quantities. Open it in any browser and drop the `.xlsx` on **Upload new export** in the sidebar. It's a single self-contained file — no server, no build step, and nothing leaves your machine.

Two views, both quantifying pipe in metres, stud bolts in sets, and everything else each:

- **Overall Summary** — every item in the model, aggregated and sorted by material code.
- **By Line Number** — the same take-off split per pipe line, so you can price one line at a time.

Search and the size/spec/rating filters apply to both, and there's a light/dark toggle that remembers your choice.

Reducing components show two sizes: a 40 × 30 reducer reads `40` in Size 1 and `30` in Size 2. Material Code comes from the `PIECE_MARK` property — it shows `—` when your catalog doesn't populate one, which is common.

## Learn more

See [CLAUDE.md](CLAUDE.md) for architecture, troubleshooting, and build/deploy details if you're developing or debugging this AddIn.
