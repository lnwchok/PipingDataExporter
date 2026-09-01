using System.Collections.Generic;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// The weld half of a scan: the items nested under every weld, plus the union of the property
    /// columns they use. Welds themselves are not repeated here - they are already rows on the
    /// PipingItems sheet, which <see cref="WeldJointRecord.WeldElementId"/> joins back to.
    /// </summary>
    internal sealed class WeldJointTable
    {
        private readonly List<WeldJointRecord> _joints = new List<WeldJointRecord>();
        private readonly PropertyColumnUnion _jointColumns = new PropertyColumnUnion();

        public IReadOnlyList<WeldJointRecord> Joints => _joints;

        /// <summary>Property columns in first-seen order.</summary>
        public IReadOnlyList<string> JointPropertyColumns => _jointColumns.Columns;

        public void Add(WeldJointRecord record)
        {
            _joints.Add(record);
            _jointColumns.AddAll(record.Properties);
        }
    }
}
