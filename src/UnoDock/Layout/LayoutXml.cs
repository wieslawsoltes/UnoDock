using System.Globalization;
using System.Xml;

namespace UnoDock.Layout;
/// <summary>Explicit, trimming-safe XML codec. Every accepted node and property is listed here.</summary>
internal static class LayoutXml
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    internal static void ReadInto(LayoutElement element, XmlReader reader)
    {
        var snapshot = LayoutSnapshotXml.Read(reader);
        using var batch = (element.Root as LayoutRoot)?.BeginUpdate();
        Populate(element, snapshot);
        if (element is LayoutRoot root)
            FixReferences(root);
    }

    internal static LayoutRoot ReadRoot(LayoutSnapshotNode snapshot)
    {
        if (snapshot.Name != nameof(LayoutRoot))
            throw new XmlException("Expected LayoutRoot.");
        var root = new LayoutRoot();
        using (root.BeginUpdate())
        {
            Populate(root, snapshot);
            FixReferences(root);
        }

        return root;
    }

    internal static void WriteBody(LayoutElement element, XmlWriter writer)
    {
        AssignIds(element);
        var node = Capture(element);
        foreach (var (key, value) in node.Attributes)
            writer.WriteAttributeString(key, value);
        foreach (var child in node.Children)
            LayoutSnapshotXml.Write(child, writer);
    }

    internal static LayoutSnapshotNode CaptureRoot(LayoutRoot root)
    {
        AssignIds(root);
        return Capture(root);
    }

    private static void AssignIds(LayoutElement root)
    {
        var elements = root.Descendents().Prepend(root).OfType<LayoutElement>().Where(e => e is ILayoutContainer).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var sequence = 0;
        foreach (var element in elements)
        {
            if (element.SerializationId.Length != 0 && ids.Add(element.SerializationId))
                continue;
            do
            {
                element.SerializationId = "p" + (++sequence).ToString(Invariant);
            }
            while (!ids.Add(element.SerializationId));
        }
    }

    private static LayoutSnapshotNode Capture(LayoutElement element, string? name = null)
    {
        var node = new LayoutSnapshotNode(name ?? element.GetType().Name);
        void Put(string key, object? value)
        {
            if (value == null)
                return;
            node.Attributes[key] = value switch
            {
                double d => d.ToString("R", Invariant),
                bool b => XmlConvert.ToString(b),
                DateTime dt => XmlConvert.ToString(dt, XmlDateTimeSerializationMode.RoundtripKind),
                _ => Convert.ToString(value, Invariant)!
            };
        }

        if (element is ILayoutContainer && element is not LayoutRoot)
            Put("Id", element.SerializationId);
        if (element is ILayoutOrientableGroup orientable)
            Put("Orientation", orientable.Orientation);
        if (element is ILayoutPositionableElement position)
        {
            Put("DockWidth", Length(position.DockWidth));
            Put("DockHeight", Length(position.DockHeight));
            Put("DockMinWidth", position.DockMinWidth);
            Put("DockMinHeight", position.DockMinHeight);
            Put("FloatingLeft", position.FloatingLeft);
            Put("FloatingTop", position.FloatingTop);
            Put("FloatingWidth", position.FloatingWidth);
            Put("FloatingHeight", position.FloatingHeight);
            Put("IsMaximized", position.IsMaximized);
            Put("CanRepositionItems", position.CanRepositionItems);
            Put("AllowDuplicateContent", position.AllowDuplicateContent);
        }

        if (element is LayoutContent content)
        {
            Put("Title", content.Title);
            Put("ContentId", content.ContentId);
            Put("IsSelected", content.IsSelected);
            Put("IsActive", content.IsActive);
            Put("CanClose", content.CanClose);
            Put("CanFloat", content.CanFloat);
            Put("IsEnabled", content.IsEnabled);
            Put("FloatingLeft", content.FloatingLeft);
            Put("FloatingTop", content.FloatingTop);
            Put("FloatingWidth", content.FloatingWidth);
            Put("FloatingHeight", content.FloatingHeight);
            Put("IsMaximized", content.IsMaximized);
            Put("IsLastFocusedDocument", content.IsLastFocusedDocument);
            Put("LastActivationTimeStamp", content.LastActivationTimeStamp?.ToString("MM/dd/yyyy HH:mm:ss", Invariant));
            if (content is LayoutDocument document)
            {
                Put("CanMove", document.CanMove);
                Put("Description", document.Description);
            }

            if (content is LayoutAnchorable anchorable)
            {
                Put("CanHide", anchorable.CanHide);
                Put("CanAutoHide", anchorable.CanAutoHide);
                Put("CanDockAsTabbedDocument", anchorable.CanDockAsTabbedDocument);
                Put("AutoHideWidth", anchorable.AutoHideWidth);
                Put("AutoHideHeight", anchorable.AutoHideHeight);
                Put("AutoHideMinWidth", anchorable.AutoHideMinWidth);
                Put("AutoHideMinHeight", anchorable.AutoHideMinHeight);
            }
        }

        if (element is ILayoutPreviousContainer previous && previous.PreviousContainer is LayoutElement container && container.SerializationId.Length != 0)
        {
            Put("PreviousContainerId", container.SerializationId);
            Put("PreviousContainerIndex", previous.PreviousContainerIndex);
        }

        if (element is LayoutAnchorablePane ap)
            Put("Name", ap.Name);
        if (element is LayoutDocumentPane dp)
            Put("ShowHeader", dp.ShowHeader);
        if (element is ILayoutContentSelector selector)
            Put("SelectedContentIndex", selector.SelectedContentIndex);
        switch (element)
        {
            case LayoutRoot root:
                node.Children.Add(Capture(root.RootPanel, "RootPanel"));
                node.Children.Add(Capture(root.TopSide, "TopSide"));
                node.Children.Add(Capture(root.RightSide, "RightSide"));
                node.Children.Add(Capture(root.BottomSide, "BottomSide"));
                node.Children.Add(Capture(root.LeftSide, "LeftSide"));
                var floats = new LayoutSnapshotNode("FloatingWindows");
                foreach (var f in root.FloatingWindows)
                    floats.Children.Add(Capture(f));
                node.Children.Add(floats);
                var hidden = new LayoutSnapshotNode("Hidden");
                foreach (var h in root.Hidden)
                    hidden.Children.Add(Capture(h));
                node.Children.Add(hidden);
                break;
            case LayoutDocumentFloatingWindow floating when floating.RootDocument != null:
                node.Children.Add(Capture(floating.RootDocument));
                break;
            case LayoutAnchorableFloatingWindow floating when floating.RootPanel != null:
                node.Children.Add(Capture(floating.RootPanel));
                break;
            case ILayoutContainer group:
                foreach (var child in group.Children)
                    node.Children.Add(Capture((LayoutElement)child));
                break;
        }

        return node;
    }

    private static void Populate(LayoutElement element, LayoutSnapshotNode node)
    {
        string? S(string key) => node.Attributes.GetValueOrDefault(key);
        void D(string key, Action<double> set)
        {
            if (S(key) is { } text)
            {
                if (!double.TryParse(text, NumberStyles.Float, Invariant, out var value) || !double.IsFinite(value))
                    throw new XmlException("Invalid finite number: " + key);
                set(value);
            }
        }

        void B(string key, Action<bool> set)
        {
            if (S(key) is { } text)
            {
                if (text == "1")
                    set(true);
                else if (text == "0")
                    set(false);
                else if (bool.TryParse(text, out var value))
                    set(value);
                else
                    throw new XmlException("Invalid boolean: " + key);
            }
        }

        int I(string key, int fallback = 0) => S(key) is not { } text ? fallback : int.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : throw new XmlException("Invalid integer: " + key);
        element.SerializationId = S("Id") ?? "";
        if (element is ILayoutOrientableGroup orientable && S("Orientation") is { } o)
            orientable.Orientation = o switch
            {
                "Horizontal" => Orientation.Horizontal,
                "Vertical" => Orientation.Vertical,
                _ => throw new XmlException("Invalid orientation.")
            };
        if (element is ILayoutPositionableElement position)
        {
            if (S("DockWidth") is { } width)
                position.DockWidth = ParseLength(width);
            if (S("DockHeight") is { } height)
                position.DockHeight = ParseLength(height);
            D("DockMinWidth", v => position.DockMinWidth = v);
            D("DockMinHeight", v => position.DockMinHeight = v);
            D("FloatingLeft", v => position.FloatingLeft = v);
            D("FloatingTop", v => position.FloatingTop = v);
            D("FloatingWidth", v => position.FloatingWidth = v);
            D("FloatingHeight", v => position.FloatingHeight = v);
            B("IsMaximized", v => position.IsMaximized = v);
            B("CanRepositionItems", v => position.CanRepositionItems = v);
            B("AllowDuplicateContent", v => position.AllowDuplicateContent = v);
        }

        if (element is LayoutContent content)
        {
            content.Title = S("Title");
            content.ContentId = S("ContentId");
            B("CanClose", v => content.CanClose = v);
            B("CanFloat", v => content.CanFloat = v);
            B("IsEnabled", v => content.IsEnabled = v);
            D("FloatingLeft", v => content.FloatingLeft = v);
            D("FloatingTop", v => content.FloatingTop = v);
            D("FloatingWidth", v => content.FloatingWidth = v);
            D("FloatingHeight", v => content.FloatingHeight = v);
            B("IsMaximized", v => content.IsMaximized = v);
            if (S("LastActivationTimeStamp") is { } dt)
                content.LastActivationTimeStamp = ParseTimestamp(dt);
            B("IsLastFocusedDocument", v => content.IsLastFocusedDocument = v);
            if (content is LayoutDocument document)
            {
                B("CanMove", v => document.CanMove = v);
                document.Description = S("Description");
            }

            if (content is LayoutAnchorable anchorable)
            {
                B("CanHide", v => anchorable.CanHide = v);
                B("CanAutoHide", v => anchorable.CanAutoHide = v);
                B("CanDockAsTabbedDocument", v => anchorable.CanDockAsTabbedDocument = v);
                D("AutoHideWidth", v => anchorable.AutoHideWidth = v);
                D("AutoHideHeight", v => anchorable.AutoHideHeight = v);
                D("AutoHideMinWidth", v => anchorable.AutoHideMinWidth = v);
                D("AutoHideMinHeight", v => anchorable.AutoHideMinHeight = v);
            }

            B("IsSelected", v => content.IsSelected = v); // Activation is fixed after the complete tree is attached.
            var activationTime = content.LastActivationTimeStamp;
            B("IsActive", v => content.SetActive(v));
            content.LastActivationTimeStamp = activationTime;
            content.SetPrevious(null, I("PreviousContainerIndex"), S("PreviousContainerId"));
        }

        if (element is LayoutAnchorGroup anchorGroup)
        {
            anchorGroup.PreviousContainerId = S("PreviousContainerId");
            anchorGroup.PreviousContainerIndex = I("PreviousContainerIndex");
        }

        if (element is LayoutAnchorablePane ap)
            ap.Name = S("Name");
        if (element is LayoutDocumentPane dp)
            B("ShowHeader", v => dp.ShowHeader = v);
        if (element is LayoutRoot root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            root.FloatingWindows.Clear();
            root.Hidden.Clear();
            foreach (var child in node.Children)
            {
                if (!seen.Add(child.Name))
                    throw new XmlException("Duplicate layout root slot: " + child.Name);
                switch (child.Name)
                {
                    case "RootPanel":
                        root.RootPanel = Build<LayoutPanel>(child);
                        break;
                    case "TopSide":
                        root.TopSide = Build<LayoutAnchorSide>(child);
                        break;
                    case "RightSide":
                        root.RightSide = Build<LayoutAnchorSide>(child);
                        break;
                    case "BottomSide":
                        root.BottomSide = Build<LayoutAnchorSide>(child);
                        break;
                    case "LeftSide":
                        root.LeftSide = Build<LayoutAnchorSide>(child);
                        break;
                    case "FloatingWindows":
                        foreach (var f in child.Children)
                            root.FloatingWindows.Add(Create(f) as LayoutFloatingWindow ?? throw new XmlException("Expected floating window."));
                        break;
                    case "Hidden":
                        foreach (var h in child.Children)
                            root.Hidden.Add(Create(h) as LayoutAnchorable ?? throw new XmlException("Expected hidden anchorable."));
                        break;
                    default:
                        throw new XmlException("Unknown root slot: " + child.Name);
                }
            }
        }
        else if (element is LayoutDocumentFloatingWindow df)
        {
            if (node.Children.Count > 1)
                throw new XmlException("A document floating window has one document.");
            df.RootDocument = node.Children.Count == 0 ? null : Build<LayoutDocument>(node.Children[0]);
        }
        else if (element is LayoutAnchorableFloatingWindow af)
        {
            if (node.Children.Count > 1)
                throw new XmlException("An anchorable floating window has one root group.");
            af.RootPanel = node.Children.Count == 0 ? null : Build<LayoutAnchorablePaneGroup>(node.Children[0]);
        }
        else if (element is ILayoutGroup group)
        {
            while (group.ChildrenCount > 0)
                group.RemoveChildAt(group.ChildrenCount - 1);
            var selectedIndex = -1;
            foreach (var child in node.Children)
            {
                var item = Create(child);
                // Restore explicit selection only after collection insertion has
                // completed its first-child selection initialization.
                if (item is LayoutContent { IsSelected: true })
                    selectedIndex = group.ChildrenCount;
                group.InsertChildAt(group.ChildrenCount, item);
            }

            if (group is ILayoutContentSelector selection && selectedIndex >= 0)
                selection.SelectedContentIndex = selectedIndex;
        }
        else if (node.Children.Count > 0)
            throw new XmlException("A content node cannot have child layout nodes.");
        if (element is ILayoutContentSelector selector && S("SelectedContentIndex") != null)
            selector.SelectedContentIndex = Math.Clamp(I("SelectedContentIndex", -1), -1, ((ILayoutContainer)element).ChildrenCount - 1);
        (element as ILayoutElementWithVisibility)?.ComputeVisibility();
    }

    private static T Build<T>(LayoutSnapshotNode node)
        where T : LayoutElement, new()
    {
        var value = new T();
        Populate(value, node);
        return value;
    }

    private static LayoutElement Create(LayoutSnapshotNode node) => node.Name switch
    {
        nameof(LayoutPanel) => Build<LayoutPanel>(node),
        nameof(LayoutDocumentPane) => Build<LayoutDocumentPane>(node),
        nameof(LayoutDocumentPaneGroup) => Build<LayoutDocumentPaneGroup>(node),
        nameof(LayoutAnchorablePane) => Build<LayoutAnchorablePane>(node),
        nameof(LayoutAnchorablePaneGroup) => Build<LayoutAnchorablePaneGroup>(node),
        nameof(LayoutAnchorGroup) => Build<LayoutAnchorGroup>(node),
        nameof(LayoutAnchorSide) => Build<LayoutAnchorSide>(node),
        nameof(LayoutDocument) => Build<LayoutDocument>(node),
        nameof(LayoutAnchorable) => Build<LayoutAnchorable>(node),
        nameof(LayoutDocumentFloatingWindow) => Build<LayoutDocumentFloatingWindow>(node),
        nameof(LayoutAnchorableFloatingWindow) => Build<LayoutAnchorableFloatingWindow>(node),
        _ => throw new XmlException("Unknown layout node: " + node.Name)
    };
    private static void FixReferences(LayoutRoot root)
    {
        var ids = new Dictionary<string, ILayoutContainer>(StringComparer.Ordinal);
        foreach (var container in root.Descendents().OfType<ILayoutContainer>())
        {
            var id = ((LayoutElement)container).SerializationId;
            if (id.Length > 0 && !ids.TryAdd(id, container))
                throw new XmlException("Duplicate layout container ID: " + id);
        }

        foreach (var content in root.Descendents().OfType<LayoutContent>())
            if (content.PreviousContainerId is { Length: > 0 } id && ids.TryGetValue(id, out var previous))
                content.SetPrevious(previous, content.PreviousContainerIndex);
        foreach (var group in root.Descendents().OfType<LayoutAnchorGroup>())
            if (group.PreviousContainerId is { Length: > 0 } id && ids.TryGetValue(id, out var previous))
                group.PreviousContainer = previous;
        var lastFocused = root.Descendents().OfType<LayoutContent>().FirstOrDefault(c => c.IsLastFocusedDocument);
        root.LastFocusedDocument = lastFocused;
        foreach (var c in root.Descendents().OfType<LayoutContent>())
            if (!ReferenceEquals(c, lastFocused))
                c.IsLastFocusedDocument = false;
        var active = root.Descendents().OfType<LayoutContent>().FirstOrDefault(c => c.IsActive && c.IsEnabled && c is not LayoutAnchorable { IsHidden: true });
        foreach (var c in root.Descendents().OfType<LayoutContent>())
            if (!ReferenceEquals(c, active))
                c.SetActive(false);
        if (active != null)
        {
            var timestamp = active.LastActivationTimeStamp;
            root.ActiveContent = active;
            active.LastActivationTimeStamp = timestamp;
        }
    }

    private static DateTime ParseTimestamp(string text)
    {
        // Accept the observed original invariant format and earlier UnoDock ISO layouts.
        if (DateTime.TryParseExact(text, "MM/dd/yyyy HH:mm:ss", Invariant, DateTimeStyles.None, out var value))
            return value;
        try
        {
            return XmlConvert.ToDateTime(text, XmlDateTimeSerializationMode.RoundtripKind);
        }
        catch (FormatException e)
        {
            throw new XmlException("Invalid LastActivationTimeStamp.", e);
        }
    }

    private static string Length(GridLength length) => length.IsAuto ? "Auto" : length.Value.ToString("R", Invariant) + (length.IsStar ? "*" : "");
    private static GridLength ParseLength(string text)
    {
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return GridLength.Auto;
        var star = text.EndsWith('*');
        if (star)
            text = text[..^1];
        if (star && text.Length == 0)
            return new(1, GridUnitType.Star);
        if (!double.TryParse(text, NumberStyles.Float, Invariant, out var value) || !double.IsFinite(value) || value < 0)
            throw new XmlException("Invalid grid length.");
        return new(value, star ? GridUnitType.Star : GridUnitType.Pixel);
    }
}
