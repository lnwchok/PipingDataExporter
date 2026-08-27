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

## Learn more

See [CLAUDE.md](CLAUDE.md) for architecture, troubleshooting, and build/deploy details if you're developing or debugging this AddIn.
