using System;
using System.Collections.Generic;
using System.IO;
using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.DgnPlatformNET.Elements;
using Bentley.ECObjects.Instance;
using Bentley.ECObjects.Schema;
using Bentley.MstnPlatformNET;
using ClosedXML.Excel;

namespace PipingDataExporter
{
    public class PipingItemsExporter
    {
        public static void ExportPipingItems(string unparsed)
        {
            try
            {
                RunExport();
            }
            catch (Exception ex)
            {
                MessageCenter.Instance.ShowMessage(
                    MessageType.Error,
                    "Piping Items Export Failed",
                    ex.ToString(),
                    MessageAlert.Balloon
                );
            }
        }

        private static void RunExport()
        {
            DgnModel activeModel = Session.Instance.GetActiveDgnModel();
            DgnFile activeFile = Session.Instance.GetActiveDgnFile();

            var rows = new List<Dictionary<string, string>>();
            var columnOrder = new List<string>();
            var columnSet = new HashSet<string>();
            int elementsSkipped = 0;

            using (IEnumerator<Element> enumerator = activeModel.GetGraphicElements().GetEnumerator())
            {
                while (true)
                {
                    Element element;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }
                        element = enumerator.Current;
                    }
                    catch
                    {
                        // enumeration itself failed (e.g. bad interop data) - can't recover the enumerator position, stop here
                        break;
                    }

                    try
                    {
                        DgnECInstanceCollection instances = DgnECManager.Manager.GetElementProperties(element, ECQueryProcessFlags.SearchAllClasses);

                        foreach (IDgnECInstance instance in instances)
                        {
                            var row = new Dictionary<string, string>
                            {
                                ["ElementId"] = element.ElementId.ToString(),
                                ["SchemaName"] = instance.ClassDefinition.Schema.Name,
                                ["ClassName"] = instance.ClassDefinition.Name
                            };

                            foreach (IECProperty property in instance.ClassDefinition.Properties(true))
                            {
                                IECPropertyValue propValue;
                                try
                                {
                                    propValue = instance.GetPropertyValue(property.Name);
                                }
                                catch
                                {
                                    continue;
                                }

                                if (propValue == null || !propValue.IsPrimitive)
                                {
                                    continue; // skip struct/array properties - not a flat cell value
                                }

                                string column = property.Name;
                                string value = propValue.IsNull ? string.Empty : propValue.StringValue;

                                row[column] = value;
                                if (columnSet.Add(column))
                                {
                                    columnOrder.Add(column);
                                }
                            }

                            rows.Add(row);
                        }
                    }
                    catch
                    {
                        elementsSkipped++;
                    }
                }
            }

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("PipingItems");

                var headers = new List<string> { "ElementId", "SchemaName", "ClassName" };
                headers.AddRange(columnOrder);

                for (int col = 0; col < headers.Count; col++)
                {
                    sheet.Cell(1, col + 1).Value = headers[col];
                }

                for (int r = 0; r < rows.Count; r++)
                {
                    var row = rows[r];
                    for (int col = 0; col < headers.Count; col++)
                    {
                        if (row.TryGetValue(headers[col], out string value))
                        {
                            sheet.Cell(r + 2, col + 1).Value = value;
                        }
                    }
                }

                sheet.SheetView.FreezeRows(1);
                sheet.Columns().AdjustToContents();

                string directory = Path.GetDirectoryName(activeFile.GetFileName());
                string baseName = Path.GetFileNameWithoutExtension(activeFile.GetFileName());
                string outputPath = Path.Combine(directory, $"{baseName}_PipingItems_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                workbook.SaveAs(outputPath);

                MessageCenter.Instance.ShowMessage(
                    MessageType.Info,
                    "Piping Items Exported!",
                    $"{rows.Count} piping item(s) exported to {outputPath}" +
                        (elementsSkipped > 0 ? $" ({elementsSkipped} element(s) skipped due to errors)" : string.Empty),
                    MessageAlert.None
                );
            }
        }
    }
}
