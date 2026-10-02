# Preview 14 namespace migration

Preview 14 intentionally changes product CLR type identity from the reference
library's root namespace (pinned in contracts/reference.json) to `UnoDock.*`. This is a source **and binary breaking change**;
recompile dependents. No forwarding assemblies or compatibility aliases are supplied.

```csharp
using UnoDock;
using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Layout.Serialization;
using UnoDock.Themes;
```

```xml
<dock:DockingManager xmlns:dock="using:UnoDock"
                     xmlns:layout="using:UnoDock.Layout">
  <dock:DockingManager.Layout>
    <layout:LayoutRoot>
      <layout:LayoutPanel>
        <layout:LayoutDocumentPane>
          <layout:LayoutDocument Title="Document" ContentId="document" />
        </layout:LayoutDocumentPane>
      </layout:LayoutPanel>
    </layout:LayoutRoot>
  </dock:DockingManager.Layout>
</dock:DockingManager>
```

Assembly/package names and the `UnoDock.Core` namespace do not change. The separate
`Microsoft.Windows.Shell` compatibility surface does not contain a reference-library namespace and
is retained. Stable layout XML element names and application ContentId values are not
renamed. Application strings containing assembly-qualified CLR names need explicit
migration by the application; arbitrary CLR object serialization is not supported.

## Reference integrity

Original metadata and public observations are evidence, not product API. Recorded
contracts spell the reference root namespace with the neutral token `Reference`; only
contracts/reference.json pins the original namespace, and the generators apply that
substitution themselves, so re-running the reference workflows reproduces the recorded
files. Original-runner probes obtain their namespace imports from the same pin at build
time. Tests which consume recorded full type names translate `Reference.*` to
`UnoDock.*` at their input boundary. The code text in `tools/VisualScene/SceneContent.cs`
is scene content only; editor text does not affect the recorded pane geometry.

`contracts/type-mappings.json` enumerates each renamed original exported type, including
generic definition spellings. The resolved metadata comparator is unchanged. The old
diagnostic allowlist is byte-for-byte equal after JSON parsing; only its mapping digest
changes. `contracts/namespace-migration.json` records the reference inventory/comparator
file hashes and the diagnostic-list digest. The migration tests verify these constraints
and the emitted product namespaces. This rename is never counted as a parity gain.

## Protected lifecycle adapters

Manager/grid `OnInitialized(EventArgs)` now runs once on first Loaded, after the derived
constructor. Native unload/reload does not run it again. This is explicitly not WPF's
BeginInit/EndInit/Initialized timing. `DockingManager.LogicalChildren` is a protected
virtual snapshot of manager-owned views, not a reconstruction of WPF's logical tree.
`DocumentPaneTabPanel.OnMouseLeave(DockMouseEventArgs)` sees native pointer exits from
the panel boundary and completes the adapter back to the native routed event. These
methods are usable extension points; they are virtual additions, not WPF overrides.
