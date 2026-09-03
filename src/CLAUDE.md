# src/ — OpenPlant SDK notes

Loaded when working under `src/`. General project constraints live in the root [CLAUDE.md](../CLAUDE.md).

## OpenPlant / DgnPlatformNET API notes (verified by reflecting the installed SDK, not from docs)

The managed API surface lives under `Bentley.DgnPlatformNET.*` (the `Bentley.DgnPlatform.*` namespace without "NET" is the raw native-interop layer — its types like `DgnECManager` are opaque native handle structs with no usable managed members; don't reference that namespace directly).

Reading an element's business-item / EC data (what backs Explorer > Items):
```csharp
DgnModel activeModel = Session.Instance.GetActiveDgnModel();
foreach (Element element in activeModel.GetElements())
{
    DgnECInstanceCollection instances = DgnECManager.Manager.GetElementProperties(
        element, ECQueryProcessFlags.SearchAllClasses);   // Bentley.DgnPlatformNET.DgnEC

    foreach (IDgnECInstance instance in instances)
    {
        var container = (IECValueContainer)instance;       // Bentley.ECObjects.Instance
        using (var e = container.GetEnumerator(true))       // true = includeAll (recurse structs)
        {
            while (e.MoveNext())
            {
                IECPropertyValue pv = e.Current;
                string column = pv.AccessString;
                string value  = pv.IsNull ? "" : pv.StringValue;
            }
        }
    }
}
```
Key gotchas found by reflection, not obvious from IntelliSense alone:
- `IECInstance` (real type: `Bentley.ECObjects.Instance.IECInstance`, defined in `Bentley.ECObjects3.dll`) does *not* expose `ClassDefinition` itself — that property lives on `IECNamedValueContainer`, one of the interfaces `IDgnECInstance` composes in. Cast/access through the instance directly (`instance.ClassDefinition` resolves fine via interface composition), but don't go looking for it on `IECInstance`'s own declared members.
- Plain `foreach (var pv in instance)` only walks top-level properties (parameterless `IEnumerable<IECPropertyValue>.GetEnumerator()`). To also get nested struct/array properties (needed for a complete "all properties" dump), cast to `IECValueContainer` and call `GetEnumerator(includeAll: true)` explicitly, as above.
- `Bentley.ECObjects3.dll` isn't referenced by `ustation`/`Bentley.DgnPlatformNET` by default — it lives at `Assemblies\ECFramework\Bentley.ECObjects3.dll` under the product install dir, not next to the main DLLs. `Bentley.Platform.dll` (same `ECFramework` folder) is a further transitive dependency needed once you touch `IECValueContainer`/`IECClass` (its `IExtendable` interface lives there) — the compiler error for a missing reference here is `CS0012`, not a missing-type error, which makes it non-obvious what to add.
- `Session.Instance.GetActiveDgnFile().GetFileName()` returns the full path (not just the filename) — used to derive the export's output directory.

If you need to explore further Bentley API surface and the reference docs aren't at hand, reflecting the installed DLLs directly (via `[Reflection.Assembly]::LoadFrom` in PowerShell, catching `ReflectionTypeLoadException` and loading transitive deps as errors surface) is reliable and was how everything above was confirmed — guessing method signatures for this SDK is not, since wrong native-interop signatures tend to fail silently or throw unhelpful marshaling errors rather than clean compile errors.

