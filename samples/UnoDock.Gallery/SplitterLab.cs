using UnoDock.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using System.Globalization;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage
{
    private void ShowSplitterLab()
    {
        var manager = new GalleryDockingManager
        {
            MinHeight = 360,
            RequestedTheme = ElementTheme.Light
        };
        var container = new Grid();
        container.RowDefinitions.Add(new() { Height = GridLength.Auto });
        container.RowDefinitions.Add(new() { Height = GridLength.Auto });
        container.RowDefinitions.Add(new() { Height = GridLength.Auto });
        container.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new(6)
        };
        var status = new TextBlock
        {
            Margin = new(10, 4, 10, 8),
            TextWrapping = TextWrapping.Wrap
        };
        var vertical = false;
        Add("Reset 1* / 2*", () => Populate(false));
        Add("Pixel / star", () => Populate(true));
        Add("Horizontal / vertical", () =>
        {
            vertical = !vertical;
            Populate(false);
        });
        Add("RTL / LTR", () => manager.FlowDirection = manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);
        Add("Light / dark", () =>
        {
            manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
            manager.Refresh();
        });
        Add("Cancel resize", () =>
        {
            foreach (var splitter in manager.FindVisualChildren<LayoutGridResizerControl>())
                splitter.CancelDrag();
        });
        Add("Save XML", () =>
        {
            using var writer = new StringWriter();
            new XmlLayoutSerializer(manager).Serialize(writer);
            status.Text = writer.ToString();
        });
        var bar = new ScrollViewer
        {
            Content = controls,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        container.Children.Add(bar);
        Grid.SetRow(status, 1);
        container.Children.Add(status);
        Grid.SetRow(manager, 3);
        container.Children.Add(manager);
        var numeric = new TextBox
        {
            Text = "220",
            Width = 90,
            MinHeight = 0,
            Height = 26,
            Padding = new(4, 1, 4, 1),
            FontSize = 12
        };
        AutomationProperties.SetName(numeric, "Leading pane size in DIPs");
        var accessible = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new(6, 0, 6, 4)
        };
        accessible.Children.Add(new TextBlock { Text = "Automation size (DIP):", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        accessible.Children.Add(numeric);
        ActionButton("Read range", () => ReportRange());
        ActionButton("Apply size", () =>
        {
            if (!double.TryParse(numeric.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
                throw new ArgumentException("Enter a finite invariant-culture number.");
            Range().SetValue(value);
            manager.Refresh();
            manager.UpdateLayout();
            ReportRange();
        });
        ActionButton("Minimum", () =>
        {
            var value = Range();
            value.SetValue(value.Minimum);
            manager.Refresh();
            manager.UpdateLayout();
            ReportRange();
        });
        ActionButton("Maximum", () =>
        {
            var value = Range();
            value.SetValue(value.Maximum);
            manager.Refresh();
            manager.UpdateLayout();
            ReportRange();
        });
        ActionButton("Focus divider", () =>
        {
            Peer().SetFocus();
            status.Text = "Keyboard: arrows move 10 DIPs; Page Up/Down move 50; Home/End use limits. Escape cancels pointer resizing.";
        });
        var accessibilityBar = new ScrollViewer
        {
            Content = accessible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(accessibilityBar, 2);
        container.Children.Add(accessibilityBar);
        LayoutGridResizerAutomationPeer Peer()
        {
            var splitter = manager.FindVisualChildren<LayoutGridResizerControl>().FirstOrDefault() ?? throw new InvalidOperationException("The divider is not arranged yet.");
            return FrameworkElementAutomationPeer.CreatePeerForElement(splitter) as LayoutGridResizerAutomationPeer ?? throw new InvalidOperationException("The divider automation peer is unavailable.");
        }

        IRangeValueProvider Range() => Peer().GetPattern(PatternInterface.RangeValue) as IRangeValueProvider ?? throw new InvalidOperationException("The divider has no range provider.");
        void ReportRange()
        {
            var value = Range();
            numeric.Text = value.Value.ToString("R", CultureInfo.InvariantCulture);
            status.Text = FormattableString.Invariant($"RangeValue: {value.Value:0.##} DIP; minimum {value.Minimum:0.##}; maximum {value.Maximum:0.##}; read-only {value.IsReadOnly}. Numeric automation shares the guarded resize transaction.");
        }

        void ActionButton(string text, Action action)
        {
            var button = SampleChrome.Button(text, () =>
            {
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    status.Text = error.Message;
                }
            });
            button.Padding = new(6, 2, 6, 2);
            button.Height = 26;
            accessible.Children.Add(button);
        }

        Populate(false);
        var document = new LayoutDocument
        {
            Title = "Splitter quality",
            ContentId = "splitters:" + Guid.NewGuid().ToString("N"),
            Content = container
        };
        var ownerRoot = Dock.Layout;
        EventHandler? ownerChanged = null;
        ownerChanged = (_, _) =>
        {
            if (!ReferenceEquals(ownerRoot, Dock.Layout))
            {
                manager.Dispose();
                Dock.LayoutChanged -= ownerChanged;
            }
        };
        Dock.LayoutChanged += ownerChanged;
        document.Closed += (_, _) =>
        {
            Dock.LayoutChanged -= ownerChanged;
            manager.Dispose();
        };
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null)
        {
            pane = new();
            Dock.Layout.RootPanel.Children.Add(pane);
        }

        pane.Children.Add(document);
        document.IsActive = true;
        void Populate(bool mixed)
        {
            var left = new LayoutAnchorablePane(new LayoutAnchorable { Title = "Tools", ContentId = "resize-tools", Content = new TextBox { Text = "Drag the divider: only the translucent preview moves.\nRelease to commit; Escape cancels.\nMinimum size: 100 DIPs.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(12) } })
            {
                DockWidth = mixed ? new(220) : new(1, GridUnitType.Star),
                DockHeight = mixed ? new(220) : new(1, GridUnitType.Star),
                DockMinWidth = 100,
                DockMinHeight = 100
            };
            var right = new LayoutDocumentPane(new LayoutDocument { Title = "Editor.cs", ContentId = "resize-editor", Content = new TextBox { Text = "// Editor controls do not resize during preview.\n// Star weights remain responsive after commit.\n// Focus the divider: arrows, Home, End and Page Up/Down.\n// Physical Left/Right is RTL-aware.", AcceptsReturn = true, Padding = new(12) } })
            {
                DockWidth = new(2, GridUnitType.Star),
                DockHeight = new(2, GridUnitType.Star),
                DockMinWidth = 100,
                DockMinHeight = 100
            };
            var layout = new LayoutPanel(left)
            {
                Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal
            };
            layout.Children.Add(right);
            manager.Layout = new()
            {
                RootPanel = layout
            };
            left.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is "DockWidth" or "DockHeight")
                    UpdateStatus();
            };
            right.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is "DockWidth" or "DockHeight")
                    UpdateStatus();
            };
            UpdateStatus();
            void UpdateStatus() => status.Text = "Persisted sizes: " + (vertical ? left.DockHeight : left.DockWidth) + " / " + (vertical ? right.DockHeight : right.DockWidth) + ". Dragging leaves these unchanged until commit.";
        }

        void Add(string text, Action action)
        {
            var button = SampleChrome.Button(text, action);
            button.Padding = new(6, 2, 6, 2);
            button.Height = 26;
            controls.Children.Add(button);
        }
    }
}
