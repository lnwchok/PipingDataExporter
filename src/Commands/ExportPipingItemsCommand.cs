using System;
using System.IO;
using System.Text;
using Bentley.DgnPlatformNET;
using PipingDataExporter.Export;

namespace PipingDataExporter
{
    /// <summary>
    /// <c>PipingDataExporter ExportPipingItems</c> - scans the active model's business items and
    /// writes them to an .xlsx next to the active DGN.
    /// </summary>
    public static class ExportPipingItemsCommand
    {
        private const string CommandTitle = "Piping Items Export";
        private const string OutputSuffix = "_PipingItems";
        private const string OutputExtension = ".xlsx";
        private const string TimestampFormat = "yyyyMMdd_HHmmss";

        /// <summary>Key-in handler. Referenced by name from Commands.xml.</summary>
        public static void ExportPipingItems(string unparsed)
        {
            KeyinCommand.Run(CommandTitle, () =>
            {
                DgnModel activeModel = ActiveSession.RequireModel();
                string outputPath = BuildOutputPath(ActiveSession.RequireFile());

                ModelScanResult scan = ModelItemScanner.Scan(activeModel);
                ItemWorkbookWriter.Write(scan.Table, outputPath);

                KeyinCommand.ReportInfo("Piping Items Exported!", Summarize(scan, outputPath));
            });
        }

        /// <summary>&lt;ActiveDgnName&gt;_PipingItems_&lt;timestamp&gt;.xlsx, beside the active DGN.</summary>
        private static string BuildOutputPath(DgnFile activeFile)
        {
            string dgnPath = activeFile.GetFileName(); // full path, not just the filename
            string directory = Path.GetDirectoryName(dgnPath);

            if (string.IsNullOrEmpty(directory))
            {
                throw new KeyinCommandException("Could not determine the active DGN's folder, so there is nowhere to write the export. Save the DGN to disk first.");
            }

            string fileName = Path.GetFileNameWithoutExtension(dgnPath)
                + OutputSuffix + "_" + DateTime.Now.ToString(TimestampFormat) + OutputExtension;

            return Path.Combine(directory, fileName);
        }

        private static string Summarize(ModelScanResult scan, string outputPath)
        {
            var summary = new StringBuilder();
            summary.Append(scan.Table.Records.Count).Append(" piping item(s) exported to ").Append(outputPath);

            if (scan.SkippedElements > 0)
            {
                summary.Append(" (").Append(scan.SkippedElements).Append(" element(s) skipped due to errors)");
            }

            if (scan.EnumerationAborted)
            {
                summary.Append(" - WARNING: the scan stopped early, so the export does not cover the whole model.");
            }

            if (scan.FirstError != null)
            {
                summary.Append(" First error: ").Append(scan.FirstError);
            }

            return summary.ToString();
        }
    }
}
