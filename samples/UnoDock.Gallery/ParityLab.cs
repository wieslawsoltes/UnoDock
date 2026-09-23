using UnoDock.Controls;
using Windows.Foundation;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowParityLab()
    {
        LayoutDocument? document = null;
        var panel = new StackPanel { Spacing = 12, Padding = new(24) };
        panel.Children.Add(new TextBlock { Text = "Docking parity lab", FontSize = 26 });
        panel.Children.Add(new TextBlock
        {
            Text = "Inspect arranged drop areas, validate all 19 target kinds, execute a selected plan, or exercise shared menu bindings. Plans revalidate ownership and capabilities when executed.",
            TextWrapping = TextWrapping.Wrap
        });
        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var contents = new ComboBox { Header = "Content to move", HorizontalAlignment = HorizontalAlignment.Stretch, DisplayMemberPath = "Title" };
        var plans = new ComboBox { Header = "Validated drop plans", HorizontalAlignment = HorizontalAlignment.Stretch };
        var areas = new TextBox { AcceptsReturn = true, IsReadOnly = true, MaxHeight = 220, FontFamily = new FontFamily("Consolas") };
        panel.Children.Add(summary); panel.Children.Add(contents); panel.Children.Add(plans);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refresh = new Button { Content = "Refresh measured areas" };
        var execute = new Button { Content = "Execute selected plan" };
        actions.Children.Add(refresh); actions.Children.Add(execute); panel.Children.Add(actions);
        panel.Children.Add(areas);
        var native = new CheckBox
        {
            Content = "Use native floating windows (desktop)",
            IsChecked = Dock.FloatingWindowMode != FloatingWindowMode.InSurface,
            IsEnabled = !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS()
        };
        native.Checked += (_, _) => { Dock.FloatingWindowMode = FloatingWindowMode.Native; Dock.Refresh(); };
        native.Unchecked += (_, _) => { Dock.FloatingWindowMode = FloatingWindowMode.InSurface; Dock.Refresh(); };
        var floatSelected = new Button { Content = "Float selected content" };
        floatSelected.Click += (_, _) =>
        {
            if (contents.SelectedItem is LayoutContent selected && selected.CanFloat) { selected.Float(); Refresh(); }
        };
        var overflow = new Button { Content = "Open 40 tabs for drag scrolling" };
        overflow.Click += (_, _) =>
        {
            var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
            if (pane == null) return;
            for (var i = 0; i < 40; i++)
            {
                var id = "scroll-lab-" + _nextDocument++;
                pane.Children.Add(Document(id, "Scroll test " + i.ToString("D2"), new TextBox { Text = "Retained editor " + id, AcceptsReturn = true }));
            }
            Refresh();
        };
        panel.Children.Add(native);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { floatSelected, overflow } });

        var duplicate = new CheckBox { Content = "Selected pane allows duplicate Title + ContentId", IsChecked = true };
        duplicate.Checked += (_, _) => SetDuplicates(true);
        duplicate.Unchecked += (_, _) => SetDuplicates(false);
        panel.Children.Add(duplicate);
        var custom = new CheckBox { Content = "Use a shared, data-bound document context menu" };
        var menu = new ContextMenuEx();
        foreach (var command in new[] { ("Activate", "ActivateCommand"), ("Float", "FloatCommand"), ("Dock as document", "DockAsDocumentCommand"), ("Close", "CloseCommand"), ("Close others", "CloseAllButThisCommand") })
        {
            var item = new MenuItemEx { Text = command.Item1 };
            item.SetBinding(MenuFlyoutItem.CommandProperty, new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath(command.Item2) });
            menu.Items.Add(item);
        }
        custom.Checked += (_, _) => { Dock.DocumentContextMenu = menu; Dock.Refresh(); };
        custom.Unchecked += (_, _) => { Dock.DocumentContextMenu = null; Dock.Refresh(); };
        panel.Children.Add(custom);
        panel.Children.Add(new TextBlock
        {
            Text = "Lazy editors: adding model tabs does not create their content presenters. Visiting a tab realizes it once; revisiting retains its editor. Linux/X11 native cross-window capture, insertion, previews and occlusion have automated input tests. Native WinUI uses content-island coordinates. Other native hosts require an adapter. Hold a dragged tab near a header edge to scroll; Escape cancels. OS title-bar docking is not yet implemented.",
            TextWrapping = TextWrapping.Wrap, Opacity = .7
        });
        refresh.Click += (_, _) => Refresh();
        contents.SelectionChanged += (_, _) => UpdatePlans();
        execute.Click += (_, _) =>
        {
            if (plans.SelectedItem is not ComboBoxItem { Tag: DockDropPlan plan }) return;
            Log(plan.Execute() ? "Validated drop executed." : "Drop canceled, unchanged, or no longer valid.");
            Refresh();
        };
        document = Document("parity-lab-" + _nextDocument++, "Parity lab", new ScrollViewer { Content = panel });
        Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First().Children.Add(document);
        document.IsActive = true;
        panel.Loaded += (_, _) => Refresh();

        void SetDuplicates(bool allow)
        {
            if (contents.SelectedItem is LayoutContent { Parent: LayoutDocumentPane p }) p.AllowDuplicateContent = allow;
            else if (contents.SelectedItem is LayoutContent { Parent: LayoutAnchorablePane a }) a.AllowDuplicateContent = allow;
            UpdatePlans();
        }
        void Refresh()
        {
            Dock.Refresh(); Dock.UpdateLayout();
            var selected = contents.SelectedItem;
            var models = Dock.Layout.Descendents().OfType<LayoutContent>().Where(c => !ReferenceEquals(c, document)).ToArray();
            contents.ItemsSource = models;
            contents.SelectedItem = models.FirstOrDefault(c => ReferenceEquals(c, selected)) ?? models.FirstOrDefault();
            var measured = Dock.GetDropAreas();
            summary.Text = $"{models.Length + 1} model contents · {Dock.RealizedContentCount} realized presenters · {measured.Count} measured drop areas";
            areas.Text = string.Join(Environment.NewLine, measured.Select(a => $"{a.Type}: {a.DetectionRect.X:0}, {a.DetectionRect.Y:0}, {a.DetectionRect.Width:0} × {a.DetectionRect.Height:0}"));
            UpdatePlans();
        }
        void UpdatePlans()
        {
            if (contents.SelectedItem is not LayoutContent content) { plans.ItemsSource = Array.Empty<ComboBoxItem>(); return; }
            var choices = new List<ComboBoxItem>();
            foreach (var group in Dock.Layout.Descendents().OfType<ILayoutGroup>())
                foreach (var type in Enum.GetValues<DropTargetType>())
                    if (DockDropPlan.Create(content, group, type, new Rect(0, 0, Math.Max(1, Dock.ActualWidth), Math.Max(1, Dock.ActualHeight))) is { } plan)
                        choices.Add(new ComboBoxItem { Content = $"{type} → {group.GetType().Name} ({group.ChildrenCount} children)", Tag = plan });
            plans.ItemsSource = choices; plans.SelectedIndex = choices.Count == 0 ? -1 : 0;
        }
    }
}
