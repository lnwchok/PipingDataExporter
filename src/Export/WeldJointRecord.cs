using System.Collections.Generic;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// One item nested under a weld - the level the Explorer &gt; Items tree shows below a "Weld"
    /// node (a JOINT instance labelled e.g. SOCKET_WELD_JOINT).
    /// </summary>
    internal sealed class WeldJointRecord
    {
        public WeldJointRecord(
            string weldElementId,
            string weldInstanceId,
            string weldClassName,
            string relationshipClass,
            string direction,
            string schemaName,
            string className,
            string instanceId,
            string label,
            IReadOnlyList<KeyValuePair<string, string>> properties)
        {
            WeldElementId = weldElementId;
            WeldInstanceId = weldInstanceId;
            WeldClassName = weldClassName;
            RelationshipClass = relationshipClass;
            Direction = direction;
            SchemaName = schemaName;
            ClassName = className;
            InstanceId = instanceId;
            Label = label;
            Properties = properties;
        }

        /// <summary>Join key back to the weld's row on the PipingItems sheet.</summary>
        public string WeldElementId { get; }

        public string WeldInstanceId { get; }

        public string WeldClassName { get; }

        /// <summary>The relationship this item was reached through, e.g. JOINT_HAS_FASTENER.</summary>
        public string RelationshipClass { get; }

        /// <summary>Which end of the relationship this item sat on - "Source" or "Target".</summary>
        public string Direction { get; }

        public string SchemaName { get; }

        public string ClassName { get; }

        public string InstanceId { get; }

        /// <summary>
        /// What the Explorer tree shows for this node - "SOCKET_WELD_JOINT" and the like. This is the
        /// column the BOQ counts weld joints by. See <see cref="InstanceLabel"/>.
        /// </summary>
        public string Label { get; }

        public IReadOnlyList<KeyValuePair<string, string>> Properties { get; }
    }
}
