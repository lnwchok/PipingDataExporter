using System.Collections.Generic;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// One business item (one EC instance on one element) - the source of a single spreadsheet row.
    /// </summary>
    internal sealed class ItemRecord
    {
        public ItemRecord(string elementId, string schemaName, string className, IReadOnlyList<KeyValuePair<string, string>> properties)
        {
            ElementId = elementId;
            SchemaName = schemaName;
            ClassName = className;
            Properties = properties;
        }

        public string ElementId { get; }

        public string SchemaName { get; }

        public string ClassName { get; }

        /// <summary>
        /// Primitive property values in EC class declaration order. A list rather than a dictionary so
        /// column order stays deterministic and a property named like an identity column can't collide
        /// with it.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Properties { get; }
    }
}
