using System.ComponentModel;
using Microsoft.Windows.Shell;
using Xceed.Wpf.AvalonDock.Controls;

namespace UnoDock.Gallery;

/// <summary>Expose the protected navigator entry point in the sample without reflection.</summary>
public sealed class GalleryDockingManager : DockingManager
{
    public void OpenNavigator() => ShowNavigatorWindow();
}

public sealed partial class GalleryPage
{
    private void ShowWindowLifecycleLab()
    {
        var panel = new StackPanel { Padding = new(24), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Window lifecycle and navigator", FontSize = 26 });
        panel.Children.Add(new TextBlock
        {
            Text = "Maximize and serialize: saved coordinates must remain the normal rectangle. Cancel a close, dock the editor back, and switch between documents/tools with Ctrl+Tab. Escape cancels the navigator; releasing Control commits and restores editor focus.",
            TextWrapping = TextWrapping.Wrap, MaxWidth = 850
        });
        var rows = new ObservableCollection<string>();
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var saved = new TextBox { AcceptsReturn = true, IsReadOnly = true, MinHeight = 130, MaxHeight = 240 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var protect = new CheckBox { Content = "Cancel specimen window closing", IsChecked = false };
        LayoutDocument? specimen = null;
        LayoutFloatingWindowControl? observed = null;
        void Record(string text)
        {
            rows.Insert(0, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text);
            while (rows.Count > 100) rows.RemoveAt(rows.Count - 1);
        }
        void Describe()
        {
            var model = specimen;
            status.Text = model == null ? "Create a specimen to start." :
                $"Persisted normal DIPs: ({model.FloatingLeft:F1}, {model.FloatingTop:F1}), {model.FloatingWidth:F1} × {model.FloatingHeight:F1}\n" +
                $"Model maximized: {model.IsMaximized}; floating: {model.IsFloating}; native host: {observed?.NativeWindow != null}";
        }
        void Initialized(object? sender, EventArgs e) { Record("Initialized: derived model is available"); Describe(); }
        void Closing(object? sender, CancelEventArgs e) { e.Cancel |= protect.IsChecked == true; Record("Closing; cancelled=" + e.Cancel); }
        void Closed(object? sender, EventArgs e) { Record("Closed: permanent control lifetime ended"); Describe(); }
        void StateChanged(object? sender, EventArgs e) { Record("StateChanged; maximized=" + observed?.IsMaximized); Describe(); }
        void Detach()
        {
            if (observed == null) return;
            observed.Initialized -= Initialized; observed.Closing -= Closing;
            observed.Closed -= Closed; observed.StateChanged -= StateChanged; observed = null;
        }
        void Create(bool native)
        {
            Detach(); specimen?.Close();
            Dock.FloatingWindowMode = native ? FloatingWindowMode.Native : FloatingWindowMode.InSurface;
            var editor = new StackPanel { Spacing = 12, Padding = new(16) };
            editor.Children.Add(new TextBox { Text = "First editor: click to record its focus", AcceptsReturn = true });
            editor.Children.Add(new TextBox { Text = "Second editor: navigator returns to the last focused editor", AcceptsReturn = true });
            specimen = Document("lifecycle-specimen-" + _nextDocument++, "Lifecycle specimen", editor);
            specimen.FloatingLeft = 90; specimen.FloatingTop = 100; specimen.FloatingWidth = 540; specimen.FloatingHeight = 320;
            var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First(); pane.Children.Add(specimen);
            using (Dock.BeginLayoutUpdate())
            {
                observed = Dock.CreateFloatingWindow(specimen, false);
                observed.Initialized += Initialized; observed.Closing += Closing;
                observed.Closed += Closed; observed.StateChanged += StateChanged;
            }
            Dock.Refresh(); Describe();
        }
        Add("Create managed", () => Create(false)); Add("Create native", () => Create(true));
        Add("Maximize", () => { if (observed != null) SystemCommands.MaximizeWindow(observed); });
        Add("Minimize", () => { if (observed != null) SystemCommands.MinimizeWindow(observed); });
        Add("Restore", () => { if (observed != null) SystemCommands.RestoreWindow(observed); });
        Add("Close", () => observed?.Close()); Add("Dock", () => specimen?.Dock());
        Add("Serialize", () =>
        {
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(Dock).Serialize(writer);
            saved.Text = writer.ToString(); Record("Serialized current model; inspect FloatingWidth/Height and IsMaximized");
        });
        Add("Navigator", Dock.OpenNavigator);
        panel.Children.Add(new ScrollViewer { Content = actions, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        panel.Children.Add(protect); panel.Children.Add(status); panel.Children.Add(saved);
        panel.Children.Add(new ListView { ItemsSource = rows, MaxHeight = 240 });
        var lab = Document("window-lifecycle-lab-" + _nextDocument++, "Window lifecycle", new ScrollViewer { Content = panel });
        lab.Closed += (_, _) => Detach();
        panel.Unloaded += (_, _) => { if (!ReferenceEquals(lab.Root, Dock.Layout)) Detach(); };
        Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First().Children.Add(lab); lab.IsActive = true; Describe();
        void Add(string title, Action action)
        {
            var button = new Button { Content = title };
            button.Click += (_, _) => { try { action(); Describe(); } catch (Exception error) { Record(error.Message); } };
            actions.Children.Add(button);
        }
    }
}
