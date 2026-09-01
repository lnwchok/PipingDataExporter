using System;
using System.Collections.Generic;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.ECObjects.Instance;
using Bentley.ECObjects.Schema;

namespace PipingDataExporter.Export
{
    /// <summary>
    /// Collects the item level nested under each weld - what Explorer &gt; Items shows below a "Weld"
    /// node, i.e. the JOINT instance labelled SOCKET_WELD_JOINT.
    /// <para>
    /// Those items are reached through the weld instance's own relationships
    /// (<c>IECInstance.GetRelationshipInstances()</c>), which in OpenPlant carry JOINT_HAS_FASTENER
    /// back from the weld to its joint. Verified against a real model; an explicit related-instance
    /// <c>ECQuery</c> returns the same items and is not needed.
    /// </para>
    /// <para>
    /// A JOINT carries almost nothing you can track it by - no line number, no size, no spec. Those
    /// live on the weld element, so every nested row also gets the weld element's own properties under
    /// a <c>Weld.</c> prefix, making the WeldJoints sheet usable for a take-off on its own.
    /// </para>
    /// </summary>
    internal sealed class WeldJointReader
    {
        /// <summary>OpenPlant_3D's weld class. WELD derives from FASTENER, which JOINT_HAS_FASTENER targets.</summary>
        public const string DefaultWeldClassName = "WELD";

        /// <summary>Keeps the weld's tracking columns from colliding with the nested item's own properties.</summary>
        private const string WeldPropertyPrefix = "Weld.";

        /// <summary>Identity of the weld a nested row hangs off, plus the columns it is tracked by.</summary>
        private sealed class WeldContext
        {
            public WeldContext(string elementId, string instanceId, string className, IReadOnlyList<KeyValuePair<string, string>> properties)
            {
                ElementId = elementId;
                InstanceId = instanceId;
                ClassName = className;
                Properties = properties;
            }

            public string ElementId { get; }

            public string InstanceId { get; }

            public string ClassName { get; }

            /// <summary>Already prefixed, ready to prepend to a nested item's own properties.</summary>
            public IReadOnlyList<KeyValuePair<string, string>> Properties { get; }
        }

        private readonly string _weldClassName;
        private readonly WeldJointTable _table = new WeldJointTable();

        private int _welds;
        private int _weldsWithErrors;
        private string _firstError;

        public WeldJointReader(string weldClassName)
        {
            _weldClassName = string.IsNullOrWhiteSpace(weldClassName) ? DefaultWeldClassName : weldClassName.Trim();
        }

        public string WeldClassName => _weldClassName;

        public WeldJointTable Table => _table;

        /// <summary>Welds seen, whether or not anything was nested under them.</summary>
        public int Welds => _welds;

        /// <summary>Welds whose nested items could not be read in full.</summary>
        public int WeldsWithErrors => _weldsWithErrors;

        /// <summary>First failure encountered, kept so the user gets a reason and not just a count.</summary>
        public string FirstError => _firstError;

        /// <summary>
        /// Reads the items nested under every weld among <paramref name="instances"/> - the EC
        /// instances already fetched for one element by <see cref="ModelItemScanner"/>.
        /// </summary>
        public void Read(string elementId, IReadOnlyList<IDgnECInstance> instances)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                IDgnECInstance instance = instances[i];

                IECClass classDefinition;
                try
                {
                    classDefinition = instance.ClassDefinition;
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    continue;
                }

                if (classDefinition == null || !IsWeld(classDefinition))
                {
                    continue;
                }

                _welds++;

                try
                {
                    AddRelatedInstances(
                        new WeldContext(elementId, InstanceIdOf(instance), classDefinition.Name, ReadWeldContext(instances, i)),
                        instance);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
        }

        /// <summary>
        /// The weld element's own properties, prefixed and flattened into one list: the weld instance
        /// first, then the other EC instances sharing its element, first non-empty value per name
        /// winning. Which class actually carries LINENUMBER, the size and the spec varies by dataset -
        /// merging the whole element sidesteps having to know.
        /// </summary>
        private List<KeyValuePair<string, string>> ReadWeldContext(IReadOnlyList<IDgnECInstance> instances, int weldIndex)
        {
            var properties = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            AddWeldContext(properties, seen, instances[weldIndex]);

            for (int i = 0; i < instances.Count; i++)
            {
                if (i != weldIndex)
                {
                    AddWeldContext(properties, seen, instances[i]);
                }
            }

            return properties;
        }

        private void AddWeldContext(List<KeyValuePair<string, string>> properties, HashSet<string> seen, IDgnECInstance instance)
        {
            try
            {
                IECClass classDefinition = instance.ClassDefinition;
                if (classDefinition == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, string> property in EcProperties.ReadAll(instance, classDefinition))
                {
                    // Blanks are skipped rather than claimed, so a name the weld leaves empty can still
                    // be filled in by a sibling instance on the same element that populates it.
                    if (string.IsNullOrEmpty(property.Value) || !seen.Add(property.Key))
                    {
                        continue;
                    }

                    properties.Add(new KeyValuePair<string, string>(WeldPropertyPrefix + property.Key, property.Value));
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void AddRelatedInstances(WeldContext context, IECInstance weld)
        {
            IECRelationshipInstanceCollection relationships;
            try
            {
                relationships = weld.GetRelationshipInstances();
            }
            catch (Exception ex)
            {
                Fail(ex);
                return;
            }

            if (relationships == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Relationships are delay-instantiated, so MoveNext() itself does native work and can throw -
            // and that happens outside a foreach body try/catch. Drive the enumerator by hand.
            using (IEnumerator<IECRelationshipInstance> walk = relationships.GetEnumerator())
            {
                while (true)
                {
                    IECRelationshipInstance relationship;
                    try
                    {
                        if (!walk.MoveNext())
                        {
                            break;
                        }

                        relationship = walk.Current;
                    }
                    catch (Exception ex)
                    {
                        // The enumerator position cannot be recovered - keep what was read so far.
                        Fail(ex);
                        break;
                    }

                    AddFarSide(context, relationship, seen);
                }
            }
        }

        /// <summary>Emits the end of <paramref name="relationship"/> that is not the weld itself.</summary>
        private void AddFarSide(WeldContext context, IECRelationshipInstance relationship, HashSet<string> seen)
        {
            if (relationship == null)
            {
                return;
            }

            try
            {
                IECInstance source = relationship.Source;
                IECInstance target = relationship.Target;

                bool weldIsSource = source != null && InstanceIdOf(source) == context.InstanceId;
                IECInstance far = weldIsSource ? target : source;
                if (far == null)
                {
                    return;
                }

                string relationshipClass = relationship.ClassDefinition != null ? relationship.ClassDefinition.Name : string.Empty;
                Add(context, relationshipClass, weldIsSource ? "Target" : "Source", far, seen);
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void Add(WeldContext context, string relationshipClass, string direction, IECInstance instance, HashSet<string> seen)
        {
            try
            {
                IECClass classDefinition = instance.ClassDefinition;
                if (classDefinition == null)
                {
                    return;
                }

                string instanceId = InstanceIdOf(instance);
                if (!seen.Add(classDefinition.Name + "|" + instanceId))
                {
                    return;
                }

                // Weld columns first: they are what the sheet is tracked by, so they land to the left of
                // the nested item's own properties.
                var properties = new List<KeyValuePair<string, string>>(context.Properties);
                properties.AddRange(EcProperties.ReadAll(instance, classDefinition));

                _table.Add(new WeldJointRecord(
                    context.ElementId,
                    context.InstanceId,
                    context.ClassName,
                    relationshipClass,
                    direction,
                    classDefinition.Schema.Name,
                    classDefinition.Name,
                    instanceId,
                    InstanceLabel.Of(properties),
                    properties));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        /// <summary>True for the weld class itself and anything derived from it.</summary>
        private bool IsWeld(IECClass classDefinition)
        {
            return IsWeld(classDefinition, new HashSet<string>(StringComparer.Ordinal));
        }

        private bool IsWeld(IECClass classDefinition, HashSet<string> visited)
        {
            if (classDefinition == null || !visited.Add(classDefinition.Schema.FullName + ":" + classDefinition.Name))
            {
                return false;
            }

            if (string.Equals(classDefinition.Name, _weldClassName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            IECClass[] baseClasses = classDefinition.BaseClasses;
            if (baseClasses == null)
            {
                return false;
            }

            foreach (IECClass baseClass in baseClasses)
            {
                if (IsWeld(baseClass, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static string InstanceIdOf(IECInstance instance)
        {
            try
            {
                return instance.InstanceId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void Fail(Exception ex)
        {
            _weldsWithErrors++;
            _firstError = _firstError ?? ex.GetType().Name + ": " + ex.Message;
        }
    }
}
