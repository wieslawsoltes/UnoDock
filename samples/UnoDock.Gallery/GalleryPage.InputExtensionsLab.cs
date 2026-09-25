using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Data;
using UnoDock.Compatibility;
using UnoDock.Controls;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage
{
    private void ShowInputExtensionsLab()
    {
        // A separate native workspace keeps this laboratory visible when its tabs move.
        // Browser hosts use an ordinary laboratory document with the same managed dock.
        var rows = new ObservableCollection<string>();
        void Record(string value)
        {
            rows.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {value}");
            while (rows.Count > 120)
                rows.RemoveAt(rows.Count - 1);
        }

        var state = new InputLabState();
        var manager = new InputLabManager(state, Record)
        {
            MinHeight = 260,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        var documents = new ObservableCollection<LayoutDocument>();
        var pane = new LayoutDocumentPane();
        manager.BoundPane = pane;
        manager.Layout = new LayoutRoot
        {
            RootPanel = new LayoutPanel(pane)
        };
        manager.DocumentsSource = documents;
        var next = 0;
        void AddDocument()
        {
            var id = ++next;
            documents.Add(new LayoutDocument { Title = "Editor " + id, ContentId = "input-lab-" + id, Content = new TextBox { Text = "Edit, select, reorder or drag this tab. Hook events appear below.", AcceptsReturn = true, Padding = new(20) } });
        }

        AddDocument();
        AddDocument();
        AddDocument();
        var grid = new Grid
        {
            Padding = new(14),
            RowSpacing = 8
        };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = new(170) });
        var caption = new TextBlock
        {
            Text = "Protected input and bindable selection — real overrides, not synthetic events",
            FontSize = 20,
            TextWrapping = TextWrapping.Wrap
        };
        grid.Children.Add(caption);
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var vetoPress = new CheckBox
        {
            Content = "Veto tab press"
        };
        vetoPress.Checked += (_, _) => state.VetoPress = true;
        vetoPress.Unchecked += (_, _) => state.VetoPress = false;
        var vetoDrop = new CheckBox
        {
            Content = "Veto drop"
        };
        vetoDrop.Checked += (_, _) => state.VetoDrop = true;
        vetoDrop.Unchecked += (_, _) => state.VetoDrop = false;
        var redirect = new CheckBox
        {
            Content = "Redirect selection 1 → 2"
        };
        redirect.Checked += (_, _) => state.Redirect = true;
        redirect.Unchecked += (_, _) => state.Redirect = false;
        var choices = new ComboBox
        {
            ItemsSource = pane.Children,
            DisplayMemberPath = nameof(LayoutDocument.Title),
            MinWidth = 115
        };
        choices.SetBinding(ComboBox.SelectedIndexProperty, new Binding { Source = state, Path = new(nameof(state.Index)), Mode = BindingMode.TwoWay });
        var add = new Button
        {
            Content = "Add source item"
        };
        add.Click += (_, _) => AddDocument();
        toolbar.Children.Add(vetoPress);
        toolbar.Children.Add(vetoDrop);
        toolbar.Children.Add(redirect);
        toolbar.Children.Add(choices);
        toolbar.Children.Add(add);
        var scroll = new ScrollViewer
        {
            Content = toolbar,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);
        Grid.SetRow(manager, 2);
        grid.Children.Add(manager);
        var events = new ListView
        {
            ItemsSource = rows,
            FontSize = 12
        };
        Grid.SetRow(events, 3);
        grid.Children.Add(events);
        if (OperatingSystem.IsBrowser())
        {
            var document = Document("input-lab-" + _nextDocument++, "Input extensions", grid);
            document.Closed += (_, _) => manager.Dispose();
            var target = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First();
            target.Children.Add(document);
            document.IsActive = true;
        }
        else
        {
            var window = new Window
            {
                Title = "UnoDock — Input extensions",
                Content = grid
            };
            window.AppWindow.Resize(new() { Width = 1050, Height = 740 });
            var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
            window.Closed += (_, _) =>
            {
                registration.Dispose();
                manager.Dispose();
            };
            window.Activate();
        }
    }
}
