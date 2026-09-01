using System;
using System.Collections.Generic;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// The text the Explorer &gt; Items tree shows for an instance.
    /// <para>
    /// Schemas declare this with an <c>InstanceLabelSpecification</c> custom attribute naming one
    /// property - <c>JOINT</c>, for example, points at <c>TYPE</c>, which is why its nodes read
    /// "SOCKET_WELD_JOINT". Rather than evaluate custom attributes, this picks the first of the
    /// handful of properties those specifications actually name in the OpenPlant schemas. If a class
    /// turns out to label itself from some other property, the raw value is still in its own column.
    /// </para>
    /// </summary>
    internal static class InstanceLabel
    {
        private static readonly string[] LabelProperties = { "TYPE", "NAME", "DISPLAY_NAME", "SHORT_DESCRIPTION", "COMPONENT_NAME" };

        public static string Of(IReadOnlyList<KeyValuePair<string, string>> properties)
        {
            foreach (string labelProperty in LabelProperties)
            {
                foreach (KeyValuePair<string, string> property in properties)
                {
                    if (string.Equals(property.Key, labelProperty, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(property.Value))
                    {
                        return property.Value;
                    }
                }
            }

            return string.Empty;
        }
    }
}
