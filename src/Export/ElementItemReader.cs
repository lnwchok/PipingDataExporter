using System.Collections.Generic;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.DgnPlatformNET.Elements;
using Bentley.ECObjects.Instance;
using Bentley.ECObjects.Schema;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// Reads an element's EC business items - the same data the Explorer &gt; Items tab shows.
    /// </summary>
    internal static class ElementItemReader
    {
        /// <summary>
        /// Returns one record per EC instance attached to <paramref name="element"/>.
        /// <para>
        /// Eager, not a lazy iterator: the interop calls in here must run inside the caller's
        /// per-element try/catch, and a deferred <c>yield return</c> would execute them outside it.
        /// </para>
        /// </summary>
        public static List<ItemRecord> Read(Element element)
        {
            var records = new List<ItemRecord>();

            DgnECInstanceCollection instances = DgnECManager.Manager.GetElementProperties(element, ECQueryProcessFlags.SearchAllClasses);
            if (instances == null)
            {
                return records;
            }

            string elementId = element.ElementId.ToString();

            foreach (IDgnECInstance instance in instances)
            {
                IECClass classDefinition = instance.ClassDefinition;

                records.Add(new ItemRecord(
                    elementId,
                    classDefinition.Schema.Name,
                    classDefinition.Name,
                    ReadPrimitiveProperties(instance, classDefinition)));
            }

            return records;
        }

        private static List<KeyValuePair<string, string>> ReadPrimitiveProperties(IDgnECInstance instance, IECClass classDefinition)
        {
            var properties = new List<KeyValuePair<string, string>>();

            // Properties(true) walks inherited properties too.
            foreach (IECProperty property in classDefinition.Properties(true))
            {
                IECPropertyValue propertyValue;
                try
                {
                    propertyValue = instance.GetPropertyValue(property.Name);
                }
                catch
                {
                    continue; // not readable on this instance
                }

                if (propertyValue == null || !propertyValue.IsPrimitive)
                {
                    continue; // struct/array property - no flat cell value to write
                }

                properties.Add(new KeyValuePair<string, string>(
                    property.Name,
                    propertyValue.IsNull ? string.Empty : propertyValue.StringValue));
            }

            return properties;
        }
    }
}
