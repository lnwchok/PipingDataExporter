using System.Collections.Generic;
using Bentley.ECObjects.Instance;
using Bentley.ECObjects.Schema;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// The two ways of pulling flat name/value pairs off an EC instance.
    /// </summary>
    internal static class EcProperties
    {
        /// <summary>
        /// Every declared property of <paramref name="classDefinition"/>, inherited ones included,
        /// looked up by name on the instance. Struct and array properties have no flat cell value and
        /// are skipped.
        /// </summary>
        public static List<KeyValuePair<string, string>> ReadDeclared(IECInstance instance, IECClass classDefinition)
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

        /// <summary>
        /// Every primitive value the instance actually holds, nested struct and array members
        /// included, keyed by <c>AccessString</c> so nested ones come through dotted.
        /// <para>
        /// A plain <c>foreach</c> over an instance only walks top-level properties; the recursive walk
        /// needs <see cref="IECValueContainer.GetEnumerator"/> with <c>includeAll: true</c>. Falls back
        /// to <see cref="ReadDeclared"/> if the container walk is unavailable or throws, so a class
        /// that does not support it still yields its flat properties.
        /// </para>
        /// </summary>
        public static List<KeyValuePair<string, string>> ReadAll(IECInstance instance, IECClass classDefinition)
        {
            var container = instance as IECValueContainer;
            if (container == null)
            {
                return ReadDeclared(instance, classDefinition);
            }

            List<KeyValuePair<string, string>> properties;
            try
            {
                properties = ReadContainer(container);
            }
            catch
            {
                return ReadDeclared(instance, classDefinition);
            }

            return properties.Count > 0 ? properties : ReadDeclared(instance, classDefinition);
        }

        private static List<KeyValuePair<string, string>> ReadContainer(IECValueContainer container)
        {
            var properties = new List<KeyValuePair<string, string>>();

            // Driven by hand rather than with foreach: MoveNext() runs *outside* a foreach body's
            // try/catch, so one unreadable value mid-walk would otherwise take the whole element down.
            using (IEnumerator<IECPropertyValue> values = container.GetEnumerator(true))
            {
                while (true)
                {
                    IECPropertyValue value;
                    try
                    {
                        if (!values.MoveNext())
                        {
                            break;
                        }

                        value = values.Current;
                    }
                    catch
                    {
                        break; // the enumerator's position can't be recovered - keep what was read
                    }

                    if (value == null || !value.IsPrimitive || string.IsNullOrEmpty(value.AccessString))
                    {
                        continue;
                    }

                    string text;
                    if (value.IsNull)
                    {
                        text = string.Empty;
                    }
                    else if (!value.TryGetStringValue(out text))
                    {
                        continue;
                    }

                    properties.Add(new KeyValuePair<string, string>(value.AccessString, text ?? string.Empty));
                }
            }

            return properties;
        }
    }
}
