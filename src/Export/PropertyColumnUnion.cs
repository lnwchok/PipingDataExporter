using System;
using System.Collections.Generic;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// The union of the property names seen across a set of records, in first-seen order.
    /// <para>
    /// Different piping component types carry different item properties, so a sheet's columns can
    /// only be known once every record has been added: each row fills in the columns it has and
    /// leaves the rest blank.
    /// </para>
    /// </summary>
    internal sealed class PropertyColumnUnion
    {
        private readonly List<string> _columns = new List<string>();
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Column names in first-seen order.</summary>
        public IReadOnlyList<string> Columns => _columns;

        public void AddAll(IEnumerable<KeyValuePair<string, string>> properties)
        {
            foreach (KeyValuePair<string, string> property in properties)
            {
                if (_known.Add(property.Key))
                {
                    _columns.Add(property.Key);
                }
            }
        }
    }
}
