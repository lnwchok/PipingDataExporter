using System.Collections.Generic;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// Accumulates item records and the union of the property columns they use.
    /// <para>
    /// Different piping component types carry different item properties, so the sheet's columns can
    /// only be known once every element has been read: each record fills in the columns it has and
    /// leaves the rest blank.
    /// </para>
    /// </summary>
    internal sealed class ItemTable
    {
        private readonly List<ItemRecord> _records = new List<ItemRecord>();
        private readonly PropertyColumnUnion _propertyColumns = new PropertyColumnUnion();

        public IReadOnlyList<ItemRecord> Records => _records;

        /// <summary>Property columns in first-seen order.</summary>
        public IReadOnlyList<string> PropertyColumns => _propertyColumns.Columns;

        public void Add(ItemRecord record)
        {
            _records.Add(record);
            _propertyColumns.AddAll(record.Properties);
        }
    }
}
