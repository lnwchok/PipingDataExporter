using System.Collections.Generic;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.ECObjects.Schema;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// Reads an element's EC business items - the same data the Explorer &gt; Items tab shows.
    /// </summary>
    internal static class ElementItemReader
    {
        /// <summary>
        /// Returns one record per EC instance in <paramref name="instances"/> - the instances already
        /// fetched for the element by <see cref="ModelItemScanner"/>, which owns the interop call so
        /// that the weld pass can reuse the same fetch.
        /// </summary>
        public static List<ItemRecord> Read(string elementId, IReadOnlyList<IDgnECInstance> instances)
        {
            var records = new List<ItemRecord>();

            foreach (IDgnECInstance instance in instances)
            {
                IECClass classDefinition = instance.ClassDefinition;

                records.Add(new ItemRecord(
                    elementId,
                    classDefinition.Schema.Name,
                    classDefinition.Name,
                    EcProperties.ReadDeclared(instance, classDefinition)));
            }

            return records;
        }
    }
}
