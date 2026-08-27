using System;
using System.Collections.Generic;
using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.Elements;

namespace PipingDataExporter.Export
{
    /// <summary>What a scan produced, plus what it couldn't read.</summary>
    internal sealed class ModelScanResult
    {
        public ModelScanResult(ItemTable table, int skippedElements, bool enumerationAborted, string firstError)
        {
            Table = table;
            SkippedElements = skippedElements;
            EnumerationAborted = enumerationAborted;
            FirstError = firstError;
        }

        public ItemTable Table { get; }

        /// <summary>Elements whose items could not be read; they contribute no rows.</summary>
        public int SkippedElements { get; }

        /// <summary>True if enumeration stopped early - the scan does not cover the whole model.</summary>
        public bool EnumerationAborted { get; }

        /// <summary>First failure encountered, kept so the user gets a reason and not just a count.</summary>
        public string FirstError { get; }
    }

    /// <summary>
    /// Walks the graphic elements of a model and collects every business item found on them.
    /// </summary>
    internal static class ModelItemScanner
    {
        public static ModelScanResult Scan(DgnModel model)
        {
            var table = new ItemTable();
            int skippedElements = 0;
            bool enumerationAborted = false;
            string firstError = null;

            // The enumerator is driven by hand rather than with foreach: MoveNext() runs *outside* a
            // foreach body's try/catch, so a bad element mid-enumeration would otherwise be unprotected.
            using (IEnumerator<Element> elements = model.GetGraphicElements().GetEnumerator())
            {
                while (true)
                {
                    Element element;
                    try
                    {
                        if (!elements.MoveNext())
                        {
                            break;
                        }

                        element = elements.Current;
                    }
                    catch (Exception ex)
                    {
                        // The enumerator's position can't be recovered, so the scan has to stop here -
                        // record that it is incomplete rather than reporting a partial scan as a full one.
                        enumerationAborted = true;
                        firstError = firstError ?? Describe(ex);
                        break;
                    }

                    try
                    {
                        foreach (ItemRecord record in ElementItemReader.Read(element))
                        {
                            table.Add(record);
                        }
                    }
                    catch (Exception ex)
                    {
                        skippedElements++;
                        firstError = firstError ?? Describe(ex);
                    }
                }
            }

            return new ModelScanResult(table, skippedElements, enumerationAborted, firstError);
        }

        private static string Describe(Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }
}
