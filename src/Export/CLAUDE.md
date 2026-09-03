# src/Export/ — the weld/joint export

Loaded when working under `src/Export/`. SDK-wide notes are in [src/CLAUDE.md](../CLAUDE.md); project constraints in the root [CLAUDE.md](../../CLAUDE.md).

## The item level nested under a Weld (Explorer > Items)

Explorer > Items shows a `SOCKET_WELD_JOINT` node *underneath* each `Weld` node. Verified against the
installed schemas (`OpenPlant_3D.01.08.ecschema.xml` under
`OpenPlant CONNECT Edition\OrthographicsManager\Settings\Backup\Metric\DataSet\Schemas\`):

- **`SOCKET_WELD_JOINT` is not an EC class.** It is a `JOINT_NAME` value from the joint-type catalog in
  `OpenPlant_3D_Supplemental_Modeling.01.08.ecschema.xml`. Grepping the schemas for a class by that
  name is a dead end.
- The nested node is a **`JOINT`** instance ("Indicates two ports are connected"). `JOINT` carries an
  `InstanceLabelSpecification` custom attribute naming `TYPE`, which is why its tree node reads
  `SOCKET_WELD_JOINT`. Its properties are `TYPE`, `PORT1_GUID`, `PORT2_GUID` plus the audit fields
  inherited from `PLANT_BASE_OBJECT`.
- `WELD` derives from `FASTENER`, and the link is `ECRelationshipClass JOINT_HAS_FASTENER`
  (Source `JOINT`, Target `FASTENER`, `strengthDirection="forward"`) — so from a weld it is traversed
  **`RelatedInstanceDirection.Backward`**. `PORT_HAS_JOINT` reaches the same data from the other side.
- `Bentley.OpenPlantModeler.SDK.dll` has **no** weld/joint API (only `ComponentConnectivity` /
  `PipingTapConnection`). The EC route is the only route.

**Which runtime mechanism yields the JOINT was settled by running the export against a real model.**
Three were tried; the results, so nobody re-derives them:

| Path | Result |
| --- | --- |
| `IECInstance.GetRelationshipInstances()` on the weld instance | **Works — this is what `WeldJointReader` uses.** |
| An `ECQuery` over the weld class with a `JOINT_HAS_FASTENER`/Backward `RelatedInstanceSelectCriteria`, run through `DgnECManager.Manager.FindInstances` | Also works, and returns the same items. Dropped as redundant — it cost two extra `ECFramework` references (`Bentley.EC.Persistence3`, `Bentley.ECSystem3`) and a per-element `FindInstances` call. Restore it from git history if the simple path ever comes back empty. |
| The JOINT as a second EC instance on the weld's own element | No — the sibling instances on a weld element are not the JOINT. They do carry the tracking properties, though, which is why `WeldJointReader` still reads them (see below). |

Rows are deduped by class + instance id within one weld.

**A JOINT has nothing you can track it by** — its properties are only `TYPE`, `PORT1_GUID`,
`PORT2_GUID` and audit fields. No line number, no size, no spec. So every `WeldJoints` row also
carries the weld element's own properties under a `Weld.` prefix: the weld instance first, then the
other EC instances sharing its element, first non-empty value per name winning. Merging the whole
element rather than naming the fields is deliberate — the schema defines `SPECIFICATION`, `SPECID`,
`DESIGN_SPECIFICATION`, `NOMINAL_DIAMETER` *and* `NOMINAL_SIZE`, and which ones a given dataset
populates is not knowable from the schema. Blanks are skipped rather than claimed, so a name the weld
leaves empty can still be filled in by a sibling instance.

Related API notes, all confirmed by reflection:

- Relationships are delay-instantiated (`IECRelationshipInstanceCollection` raises
  `RelationshipDelayInstantiated`), so enumerating one does native work and its `MoveNext()` must be
  driven by hand like `ModelItemScanner.Scan`'s element loop.
- If the `ECQuery` path is ever restored it needs `Bentley.EC.Persistence3` and `Bentley.ECSystem3`
  referenced from `Assemblies\ECFramework\` (missing them surfaces as `CS0012`, not a missing-type
  error), and note that `ECQuery.SelectClause` has **no setter** — the returned `SelectCriteria` has
  to be mutated in place.
- `IECValueContainer.GetEnumerator(includeAll: true)` takes **one** bool (`IECInstance` has a
  two-bool overload). `IECPropertyValue.TryGetStringValue(out string)` avoids a try/catch per value.
- `InstanceLabel` (`src/Export/InstanceLabel.cs`) approximates the tree label by probing
  `TYPE`/`NAME`/... rather than evaluating `InstanceLabelSpecification` custom attributes. If a class
  labels itself from some other property the raw value is still in its own column. The resulting
  `Label` column on the `WeldJoints` sheet is what a BOQ counts weld joints by, so a change to that
  probe order changes BOQ output.

