using System;
using System.Collections.Generic;
using ClosedXML.Excel;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// Writes a scan to a two-sheet .xlsx workbook. Both sheets have the same shape: the identity
    /// columns each of its rows always has, then one column per property found anywhere in that sheet's
    /// records.
    /// <list type="bullet">
    /// <item><description><c>PipingItems</c> - every business item in the model, one row per EC instance.</description></item>
    /// <item><description><c>WeldJoints</c> - the item level nested under each weld, joined back on WeldElementId.</description></item>
    /// </list>
    /// </summary>
    internal static class ItemWorkbookWriter
    {
        private const int HeaderRow = 1;

        /// <summary>Name and value kept together so the header and the cells below it can't drift apart.</summary>
        private sealed class Column<TRecord>
        {
            public Column(string name, Func<TRecord, string> valueOf)
            {
                Name = name;
                ValueOf = valueOf;
            }

            public string Name { get; }

            public Func<TRecord, string> ValueOf { get; }
        }

        private static readonly Column<ItemRecord>[] ItemColumns =
        {
            new Column<ItemRecord>("ElementId", record => record.ElementId),
            new Column<ItemRecord>("SchemaName", record => record.SchemaName),
            new Column<ItemRecord>("ClassName", record => record.ClassName)
        };

        private static readonly Column<WeldJointRecord>[] WeldJointColumns =
        {
            new Column<WeldJointRecord>("WeldElementId", record => record.WeldElementId),
            new Column<WeldJointRecord>("WeldInstanceId", record => record.WeldInstanceId),
            new Column<WeldJointRecord>("WeldClassName", record => record.WeldClassName),
            new Column<WeldJointRecord>("RelationshipClass", record => record.RelationshipClass),
            new Column<WeldJointRecord>("Direction", record => record.Direction),
            new Column<WeldJointRecord>("SchemaName", record => record.SchemaName),
            new Column<WeldJointRecord>("ClassName", record => record.ClassName),
            new Column<WeldJointRecord>("InstanceId", record => record.InstanceId),
            new Column<WeldJointRecord>("Label", record => record.Label)
        };

        public static void Write(ModelScanResult scan, string outputPath)
        {
            using (var workbook = new XLWorkbook())
            {
                WriteSheet(workbook, "PipingItems", ItemColumns, scan.Table.PropertyColumns, scan.Table.Records, record => record.Properties);
                WriteSheet(workbook, "WeldJoints", WeldJointColumns, scan.WeldJoints.JointPropertyColumns, scan.WeldJoints.Joints, record => record.Properties);

                workbook.SaveAs(outputPath);
            }
        }

        /// <summary>
        /// Added even when it has no records: an empty sheet with its headers says "looked, found
        /// nothing", which a missing sheet does not.
        /// </summary>
        private static void WriteSheet<TRecord>(
            XLWorkbook workbook,
            string sheetName,
            Column<TRecord>[] identityColumns,
            IReadOnlyList<string> propertyColumns,
            IReadOnlyList<TRecord> records,
            Func<TRecord, IReadOnlyList<KeyValuePair<string, string>>> propertiesOf)
        {
            IXLWorksheet sheet = workbook.Worksheets.Add(sheetName);

            Dictionary<string, int> propertyColumnIndex = WriteHeader(sheet, identityColumns, propertyColumns);
            WriteRows(sheet, identityColumns, records, propertiesOf, propertyColumnIndex);

            sheet.SheetView.FreezeRows(HeaderRow);
            sheet.Columns().AdjustToContents();
        }

        /// <summary>
        /// Writes the header row and returns the column number each property column landed in.
        /// </summary>
        private static Dictionary<string, int> WriteHeader<TRecord>(IXLWorksheet sheet, Column<TRecord>[] identityColumns, IReadOnlyList<string> propertyColumns)
        {
            int column = 1;

            foreach (Column<TRecord> identityColumn in identityColumns)
            {
                sheet.Cell(HeaderRow, column++).Value = identityColumn.Name;
            }

            var propertyColumnIndex = new Dictionary<string, int>(propertyColumns.Count, StringComparer.Ordinal);
            foreach (string propertyColumn in propertyColumns)
            {
                propertyColumnIndex[propertyColumn] = column;
                sheet.Cell(HeaderRow, column++).Value = propertyColumn;
            }

            return propertyColumnIndex;
        }

        private static void WriteRows<TRecord>(
            IXLWorksheet sheet,
            Column<TRecord>[] identityColumns,
            IReadOnlyList<TRecord> records,
            Func<TRecord, IReadOnlyList<KeyValuePair<string, string>>> propertiesOf,
            Dictionary<string, int> propertyColumnIndex)
        {
            int row = HeaderRow;

            foreach (TRecord record in records)
            {
                row++;

                for (int i = 0; i < identityColumns.Length; i++)
                {
                    sheet.Cell(row, i + 1).Value = identityColumns[i].ValueOf(record);
                }

                foreach (KeyValuePair<string, string> property in propertiesOf(record))
                {
                    int column;
                    if (propertyColumnIndex.TryGetValue(property.Key, out column))
                    {
                        sheet.Cell(row, column).Value = property.Value;
                    }
                }
            }
        }
    }
}
