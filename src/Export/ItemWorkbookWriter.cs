using System;
using System.Collections.Generic;
using ClosedXML.Excel;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// Writes an <see cref="ItemTable"/> to a single-sheet .xlsx workbook: the three identity columns
    /// every row has, then one column per property found anywhere in the model.
    /// </summary>
    internal static class ItemWorkbookWriter
    {
        private const string SheetName = "PipingItems";
        private const int HeaderRow = 1;

        /// <summary>Name and value kept together so the header and the cells below it can't drift apart.</summary>
        private sealed class IdentityColumn
        {
            public IdentityColumn(string name, Func<ItemRecord, string> valueOf)
            {
                Name = name;
                ValueOf = valueOf;
            }

            public string Name { get; }

            public Func<ItemRecord, string> ValueOf { get; }
        }

        private static readonly IdentityColumn[] IdentityColumns =
        {
            new IdentityColumn("ElementId", record => record.ElementId),
            new IdentityColumn("SchemaName", record => record.SchemaName),
            new IdentityColumn("ClassName", record => record.ClassName)
        };

        public static void Write(ItemTable table, string outputPath)
        {
            using (var workbook = new XLWorkbook())
            {
                IXLWorksheet sheet = workbook.Worksheets.Add(SheetName);

                Dictionary<string, int> propertyColumnIndex = WriteHeader(sheet, table);
                WriteRows(sheet, table, propertyColumnIndex);

                sheet.SheetView.FreezeRows(HeaderRow);
                sheet.Columns().AdjustToContents();

                workbook.SaveAs(outputPath);
            }
        }

        /// <summary>
        /// Writes the header row and returns the column number each property column landed in.
        /// </summary>
        private static Dictionary<string, int> WriteHeader(IXLWorksheet sheet, ItemTable table)
        {
            int column = 1;

            foreach (IdentityColumn identityColumn in IdentityColumns)
            {
                sheet.Cell(HeaderRow, column++).Value = identityColumn.Name;
            }

            var propertyColumnIndex = new Dictionary<string, int>(table.PropertyColumns.Count, StringComparer.Ordinal);
            foreach (string propertyColumn in table.PropertyColumns)
            {
                propertyColumnIndex[propertyColumn] = column;
                sheet.Cell(HeaderRow, column++).Value = propertyColumn;
            }

            return propertyColumnIndex;
        }

        private static void WriteRows(IXLWorksheet sheet, ItemTable table, Dictionary<string, int> propertyColumnIndex)
        {
            int row = HeaderRow;

            foreach (ItemRecord record in table.Records)
            {
                row++;

                for (int i = 0; i < IdentityColumns.Length; i++)
                {
                    sheet.Cell(row, i + 1).Value = IdentityColumns[i].ValueOf(record);
                }

                foreach (KeyValuePair<string, string> property in record.Properties)
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
