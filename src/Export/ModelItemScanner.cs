using System;
using System.Collections.Generic;
using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.DgnPlatformNET.Elements;

namespace PipingDataExporter.Export
{
    /// <summary>What a scan produced, plus what it couldn't read.</summary>
    internal sealed class ModelScanResult
    {
        public ModelScanResult(ItemTable table, WeldJointTable weldJoints, string weldClassName, int welds, int skippedElements, int weldsWithErrors, bool enumerationAborted, string firstError)
        {
            Table = table;
            WeldJoints = weldJoints;
            WeldClassName = weldClassName;
            Welds = welds;
            SkippedElements = skippedElements;
            WeldsWithErrors = weldsWithErrors;
            EnumerationAborted = enumerationAborted;
            FirstError = firstError;
        }

        public ItemTable Table { get; }

        /// <summary>The item level nested under the model's welds, collected in the same pass.</summary>
        public WeldJointTable WeldJoints { get; }

        /// <summary>Welds seen, whether or not anything was nested under them.</summary>
        public int Welds { get; }

        /// <summary>The class the weld pass matched on, echoed back because the key-in can override it.</summary>
        public string WeldClassName { get; }

        /// <summary>Elements whose items could not be read; they contribute no rows.</summary>
        public int SkippedElements { get; }

        /// <summary>Welds whose nested items could not be read in full; the weld's own row is still written.</summary>
        public int WeldsWithErrors { get; }

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
        public static ModelScanResult Scan(DgnModel model, string weldClassName)
        {
            var table = new ItemTable();
            var weldReader = new WeldJointReader(weldClassName);
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
                        string elementId = element.ElementId.ToString();

                        // Fetched once and materialised so both readers work off the same instances -
                        // re-enumerating a native-backed collection is not worth the risk, and the EC
                        // call is the expensive part of the pass.
                        List<IDgnECInstance> instances = ReadInstances(element);

                        foreach (ItemRecord record in ElementItemReader.Read(elementId, instances))
                        {
                            table.Add(record);
                        }

                        weldReader.Read(elementId, instances);
                    }
                    catch (Exception ex)
                    {
                        skippedElements++;
                        firstError = firstError ?? Describe(ex);
                    }
                }
            }

            return new ModelScanResult(
                table,
                weldReader.Table,
                weldReader.WeldClassName,
                weldReader.Welds,
                skippedElements,
                weldReader.WeldsWithErrors,
                enumerationAborted,
                firstError ?? weldReader.FirstError);
        }

        /// <summary>
        /// The collection is deliberately not disposed: the <see cref="IDgnECInstance"/> handles taken
        /// out of it are read after this returns, and disposing their owner can invalidate them.
        /// </summary>
        private static List<IDgnECInstance> ReadInstances(Element element)
        {
            var instances = new List<IDgnECInstance>();

            DgnECInstanceCollection found = DgnECManager.Manager.GetElementProperties(element, ECQueryProcessFlags.SearchAllClasses);
            if (found == null)
            {
                return instances;
            }

            foreach (IDgnECInstance instance in found)
            {
                if (instance != null)
                {
                    instances.Add(instance);
                }
            }

            return instances;
        }

        private static string Describe(Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }
}
