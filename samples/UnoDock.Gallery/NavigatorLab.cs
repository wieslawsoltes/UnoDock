using System.Windows.Input;
using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Themes;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowNavigatorLab()
    {
        var manager = new GalleryDockingManager { MinHeight = 400, RequestedTheme = ElementTheme.Light };
        var panel = new Grid();
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new() { Height = new(23) });
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new(6, 3, 6, 3) };
        var buttons = new List<SampleButton>();
        var activationCommands = new List<NavigatorLabCommand>();
        var status = new TextBlock { FontSize = 11, Margin = new(8, 3, 8, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        AutomationProperties.SetAutomationId(status, "NavigatorLabStatus");
        var blocked = false; var activations = 0; LayoutRoot? observedRoot = null;
        void UpdateStatus()
        {
            status.Text = $"Active: {manager.Layout.ActiveContent?.Title ?? "none"}  |  Activation: {(blocked ? "blocked by command" : "allowed")}  |  Committed: {activations}";
        }
        EventHandler updated = (_, _) => UpdateStatus();
        void Paint()
        {
            var dark = manager.RequestedTheme == ElementTheme.Dark;
            foreach (var button in buttons) button.Configure(SampleChrome.Default(dark));
            panel.RequestedTheme = manager.RequestedTheme;
            panel.Background = SampleChrome.Color(dark ? 0x252526u : 0xf0f0f0u);
            status.Foreground = SampleChrome.Default(dark).Foreground;
        }
        Add("Open navigator", "open", manager.OpenNavigator);
        Add("3 documents", "three", () => Populate(3));
        Add("40 documents", "forty", () => Populate(40));
        Add("200 documents", "many", () => Populate(200));
        var policy = Add("Block activation", "policy", () => { blocked = !blocked; UpdatePolicy(); });
        Add("Light / dark", "theme", () =>
        {
            manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            manager.Theme = manager.RequestedTheme == ElementTheme.Dark ? new FluentTheme(ElementTheme.Dark) : new GenericTheme();
            manager.Refresh(); Paint();
        });
        Add("RTL / LTR", "direction", () => { manager.FlowDirection = manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; manager.Refresh(); });
        Add("20 pt", "large", () => { manager.Resources["UnoDock.FontSize"] = 20d; manager.Refresh(); });
        Add("12 pt", "normal", () => { manager.Resources.Remove("UnoDock.FontSize"); manager.Refresh(); });
        var bar = new ScrollViewer { Content = commands, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var help = new TextBlock { FontSize = 12, Margin = new(8, 2, 8, 6), TextWrapping = TextWrapping.Wrap,
            Text = "Ctrl+Tab previews without activating. Up/Down, Home/End and Left/Right navigate; Enter or Control release commits, Escape cancels. Block activation demonstrates a real CanExecute veto. Edits remain in their existing buffers." };
        panel.Children.Add(bar); Grid.SetRow(help, 1); panel.Children.Add(help); Grid.SetRow(manager, 2); panel.Children.Add(manager);
        Grid.SetRow(status, 3); panel.Children.Add(status);
        Populate(3); Paint();
        var document = new LayoutDocument { Title = "Navigator quality", ContentId = "navigator-lab:" + Guid.NewGuid().ToString("N"), Content = panel };
        var root = Dock.Layout; var disposed = false;
        EventHandler? changed = null;
        void Release()
        {
            if (disposed) return; disposed = true;
            Dock.LayoutChanged -= changed;
            if (observedRoot != null) observedRoot.Updated -= updated;
            foreach (var command in activationCommands) command.Detach();
            activationCommands.Clear(); manager.Dispose();
        }
        changed = (_, _) => { if (!ReferenceEquals(Dock.Layout, root)) Release(); };
        Dock.LayoutChanged += changed; document.Closed += (_, _) => Release();
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;

        void UpdatePolicy()
        {
            policy.Content = blocked ? "Allow activation" : "Block activation";
            AutomationProperties.SetName(policy, policy.Content as string ?? "Activation policy");
            foreach (var command in activationCommands) command.Refresh();
            UpdateStatus();
        }
        void Populate(int count)
        {
            if (observedRoot != null) observedRoot.Updated -= updated;
            foreach (var command in activationCommands) command.Detach();
            activationCommands.Clear();
            var documents = new LayoutDocumentPane();
            for (var i = 0; i < count; i++) documents.Children.Add(new LayoutDocument
            {
                ContentId = "navigator-editor:" + i, Title = $"Document {i:D3}.cs", Description = $"Project / Source / Document {i:D3}.cs",
                Content = new TextBox { Text = $"// Document {i:D3}\n// Edit, switch to another document, then return here.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(14) }
            });
            var tools = new LayoutAnchorablePane { DockWidth = new(200) };
            foreach (var title in new[] { "Solution Explorer", "Properties", "Output" })
                tools.Children.Add(new() { Title = title, ContentId = "navigator-tool:" + title, Content = new TextBox { Text = title, AcceptsReturn = true } });
            var workspace = new LayoutPanel(tools); workspace.Children.Add(documents); manager.Layout = new() { RootPanel = workspace };
            observedRoot = manager.Layout; observedRoot.Updated += updated; activations = 0;
            documents.Children[0].IsActive = true; manager.Refresh();
            foreach (var model in manager.Layout.Descendents().OfType<LayoutContent>())
            {
                var command = new NavigatorLabCommand(() => { activations++; model.IsActive = true; UpdateStatus(); },
                    () => !blocked && model.IsEnabled && ReferenceEquals(model.Root, manager.Layout));
                activationCommands.Add(command); manager.GetLayoutItemFromModel(model).ActivateCommand = command;
            }
            UpdateStatus();
        }
        SampleButton Add(string title, string id, Action action)
        {
            var button = SampleChrome.Button(title, action); button.Padding = new(7, 1, 7, 1); button.Height = 25;
            AutomationProperties.SetAutomationId(button, "NavigatorLab-" + id);
            buttons.Add(button); commands.Children.Add(button); return button;
        }
    }
    private sealed class NavigatorLabCommand(Action execute, Func<bool> allowed) : ICommand
    {
        private Action? _execute = execute;
        private Func<bool>? _allowed = allowed;
        public bool CanExecute(object? parameter) => _execute != null && _allowed?.Invoke() == true;
        public void Execute(object? parameter) => _execute?.Invoke();
        public event EventHandler? CanExecuteChanged;
        internal void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        internal void Detach() { _execute = null; _allowed = null; CanExecuteChanged = null; }
    }
}
