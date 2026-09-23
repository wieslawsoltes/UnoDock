# Preview 14 namespace migration

Preview 14 intentionally changes product CLR type identity from
`Xceed.Wpf.AvalonDock.*` to `UnoDock.*`. This is a source **and binary breaking change**;
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
`Microsoft.Windows.Shell` compatibility surface does not contain an Xceed namespace and
is retained. Stable layout XML element names and application ContentId values are not
renamed. Application strings containing assembly-qualified CLR names need explicit
migration by the application; arbitrary CLR object serialization is not supported.

## Reference integrity

Original metadata, public observations, original-runner probes and historical validation
records deliberately retain their original names. They are evidence, not product API.
Tests which consume original recorded full type names translate at their input boundary.
`tools/VisualScene/SceneContent.cs` retains the historical code-text scene for repeatable
existing visual comparisons; it is not sample API usage guidance.

`contracts/type-mappings.json` enumerates each renamed original exported type, including
generic definition spellings. The resolved metadata comparator is unchanged. The old
diagnostic allowlist is byte-for-byte equal after JSON parsing; only its mapping digest
changes. `contracts/namespace-migration.json` records the original inventory/comparator
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
