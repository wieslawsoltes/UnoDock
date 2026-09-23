using System.Globalization;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Themes;
using Strings = UnoDock.Properties.Resources;

namespace UnoDock.Testing;

/// <summary>Public menu observation replay and live native controls. Reference
/// opening is a public protocol, not a claim of original pointer equivalence.</summary>
public static class MenuQualityTests
{
    private sealed class TestCommand : ICommand
    {
        private EventHandler? _changed;
        internal bool Allowed = true;
        internal int Executions, Subscriptions, Queries;
        internal Action? Query, Action;
        public event EventHandler? CanExecuteChanged
        { add { _changed += value; Subscriptions++; } remove { _changed -= value; Subscriptions--; } }
        public bool CanExecute(object? parameter) { Queries++; Query?.Invoke(); return Allowed; }
        public void Execute(object? parameter) { Executions++; Action?.Invoke(); }
        internal void Raise() => _changed?.Invoke(this, EventArgs.Empty);
    }

    public static async Task<int> Run(DockingManager templateSource, string output)
    {
        var tests = new TestRunner();
        using var dock = new DockingManager { Width = 1000, Height = 640, Template = templateSource.Template,
            RequestedTheme = ElementTheme.Light, FloatingWindowMode = FloatingWindowMode.InSurface };
        var window = new Window { Content = dock, Title = "UnoDock compact menus" };
        window.AppWindow.Resize(new() { Width = 1040, Height = 720 }); window.Activate();
        using var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        LayoutDocument doc = null!, other = null!; LayoutAnchorable tool = null!;
        LayoutDocumentPane pane = null!; MenuFlyout? opened = null;
        try
        {
            using var source = typeof(MenuQualityTests).Assembly.GetManifestResourceStream("VisualFixtures.menu-observations.xml")!;
            var observations = XDocument.Load(source);
            foreach (var scenario in observations.Root!.Elements("Scenario"))
            {
                var name = (string)scenario.Attribute("name")!;
                Add("original public menu order, labels, states and row geometry: " + name, async () =>
                {
                    await Reset(name);
                    var menu = await Open(name.StartsWith("tool", StringComparison.Ordinal) ? tool : doc);
                    var rows = menu.Items.OfType<MenuFlyoutItem>().ToArray(); var expected = scenario.Elements("Item").ToArray();
                    Check.Equal(expected.Length, rows.Length); Check.Equal(rows.Length, menu.Items.Count);
                    for (var i = 0; i < rows.Length; i++)
                    {
                        var row = rows[i]; var data = expected[i];
                        Check.Equal((string)data.Attribute("command")!, row.Tag as string);
                        Check.Equal((string)data.Attribute("header")!, row.Text);
                        Check.Equal((string)data.Attribute("visibility")!, row.Visibility.ToString());
                        Check.Equal((bool)data.Attribute("enabled")!, row.IsEnabled);
                        Check.Equal(row.IsEnabled, row.Command.CanExecute(null));
                        if (row.Visibility == Visibility.Visible) Check.Near((double)data.Attribute("height")!, row.ActualHeight, .15);
                    }
                    var presenter = Presenter();
                    Check.Near((double)scenario.Attribute("height")!, presenter.ActualHeight, .25);
                    Check.Near((double)scenario.Attribute("fontSize")!, presenter.FontSize);
                    Check.True(presenter.ActualWidth >= 234 && presenter.ActualWidth < 330, "Compact menu width outside bounded font-dependent range.");
                }, reset: false);
            }
            Add("one item retains menu and all row identities through 100 refreshes", async () =>
            {
                var target = Target(doc); var menu = Menu(doc); var rows = menu.Items.ToArray();
                for (var i = 0; i < 100; i++) { doc.Title = "Document " + i; dock.Refresh(); }
                await Settle(); Check.Same(menu, target.ContextFlyout);
                for (var i = 0; i < rows.Length; i++) Check.Same(rows[i], menu.Items[i]);
            });
            Add("document and tool own distinct action sets with no separators", () =>
            {
                var documents = Menu(doc); var tools = Menu(tool); Check.False(ReferenceEquals(documents, tools));
                Check.Equal(9, documents.Items.Count); Check.Equal(6, tools.Items.Count);
                Check.False(tools.Items.OfType<MenuFlyoutItem>().Any(r => r.Text.Contains("Tab Group", StringComparison.Ordinal)));
                return Task.CompletedTask;
            });
            Add("open menu updates capability without replacing its presenter or rows", async () =>
            {
                var menu = await Open(doc); var row = Row(menu, "FloatCommand"); var presenter = Presenter();
                doc.CanFloat = false; await Settle(); Check.False(row.IsEnabled); Check.Same(presenter, Presenter());
                doc.CanFloat = true; await Settle(); Check.True(row.IsEnabled); Check.Same(row, Row(Menu(doc), "FloatCommand"));
            });
            Add("open menu collapses newly unavailable group commands", async () =>
            {
                var menu = await Open(doc); other.Close(); await Settle();
                Check.Equal(Visibility.Collapsed, Row(menu, "NewHorizontalTabGroupCommand").Visibility);
                Check.Equal(Visibility.Collapsed, Row(menu, "NewVerticalTabGroupCommand").Visibility);
                Check.False(Row(menu, "CloseAllButThisCommand").IsEnabled);
            });
            Add("command subscriptions exist only during the opening session", async () =>
            {
                var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                var menu = Menu(doc); Check.Equal(0, command.Subscriptions);
                for (var i = 0; i < 3; i++)
                {
                    await Open(doc); Check.Equal(1, command.Subscriptions);
                    menu.Hide(); await Settle(); Check.Equal(0, command.Subscriptions);
                }
            });
            Add("open command replacement detaches old observer and delegates new command", async () =>
            {
                var old = new TestCommand(); var next = new TestCommand(); var item = dock.GetLayoutItemFromModel(doc);
                item.FloatCommand = old; var menu = await Open(doc); var row = Row(menu, "FloatCommand");
                item.FloatCommand = next; await Settle(); Check.Equal(0, old.Subscriptions); Check.Equal(1, next.Subscriptions);
                row.Command.Execute(null); Check.Equal(0, old.Executions); Check.Equal(1, next.Executions);
                menu.Hide(); await Settle(); Check.Equal(0, next.Subscriptions);
            });
            Add("worker command notifications marshal to UI and coalesce a burst", async () =>
            {
                var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                var menu = await Open(doc); var before = command.Queries; command.Allowed = false;
                await Task.Run(() => { for (var i = 0; i < 200; i++) command.Raise(); });
                await Settle(); Check.False(Row(menu, "FloatCommand").IsEnabled);
                Check.True(command.Queries - before < 40, "Command burst caused an unbounded UI refresh storm.");
                menu.Hide(); await Settle(); var closed = command.Queries; await Task.Run(command.Raise); await Task.Delay(90); Check.Equal(closed, command.Queries);
            });
            Add("queued notifications cannot act after root replacement", async () =>
            {
                var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                var menu = await Open(doc); var row = Row(menu, "FloatCommand"); var wrapper = row.Command;
                command.Raise(); dock.Layout = new(); await Settle();
                Check.Equal(0, command.Subscriptions); Check.False(wrapper.CanExecute(null)); wrapper.Execute(null); Check.Equal(0, command.Executions);
            });
            Add("retained menu cannot execute on a document moved to another root", () =>
            {
                var menu = Menu(doc); var row = Row(menu, "CloseCommand");
                var replacement = new LayoutDocumentPane(); var foreign = new LayoutRoot { RootPanel = new(replacement) };
                pane.Children.Remove(doc); replacement.Children.Add(doc);
                row.Command.Execute(null); Check.Same(foreign, doc.Root); Check.False(row.Command.CanExecute(null));
                return Task.CompletedTask;
            });
            Add("CanExecute replacement cannot execute a superseded command", () =>
            {
                var item = dock.GetLayoutItemFromModel(doc); var menu = Menu(doc); var old = new TestCommand(); var next = new TestCommand();
                item.FloatCommand = old; old.Query = () => { old.Query = null; item.FloatCommand = next; };
                Row(menu, "FloatCommand").Command.Execute(null); Check.Equal(0, old.Executions); Check.Equal(0, next.Executions);
                Row(menu, "FloatCommand").Command.Execute(null); Check.Equal(1, next.Executions); return Task.CompletedTask;
            });
            Add("CanExecute root replacement cannot execute a stale command", () =>
            {
                var item = dock.GetLayoutItemFromModel(doc); var menu = Menu(doc); var command = new TestCommand(); item.FloatCommand = command;
                command.Query = () => { command.Query = null; dock.Layout = new(); };
                Row(menu, "FloatCommand").Command.Execute(null); Check.Equal(0, command.Executions); return Task.CompletedTask;
            });
            Add("application command exception is propagated without disabling future invocations", () =>
            {
                var item = dock.GetLayoutItemFromModel(doc); var menu = Menu(doc); var command = new TestCommand(); item.FloatCommand = command;
                command.Action = () => throw new InvalidOperationException("application-command");
                var row = Row(menu, "FloatCommand"); Check.Equal("application-command", Check.Throws<InvalidOperationException>(() => row.Command.Execute(null)).Message);
                command.Action = null; row.Command.Execute(null); Check.Equal(2, command.Executions); return Task.CompletedTask;
            });
            Add("custom menu takeover ends subscriptions and never changes its template or command", async () =>
            {
                var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                var old = await Open(doc); var customCommand = new TestCommand();
                var custom = new MenuFlyout(); var customRow = new MenuFlyoutItem { Text = "Application", Command = customCommand }; custom.Items.Add(customRow);
                var template = customRow.Template; dock.DocumentContextMenu = custom; await Settle();
                Check.Same(custom, Menu(doc)); Check.Same(template, customRow.Template); Check.Same(customCommand, customRow.Command);
                Check.Equal(0, command.Subscriptions); Check.False(Row(old, "FloatCommand").Command.CanExecute(null));
                dock.DocumentContextMenu = null; await Settle(); Check.Same(old, Menu(doc));
            });
            Add("shared custom menu context follows the current header and restores local overrides", async () =>
            {
                var own = new object(); var overridden = new MenuFlyoutItem { Text = "Local", DataContext = own };
                var automatic = new MenuFlyoutItem { Text = "Automatic" }; var menu = new MenuFlyout(); menu.Items.Add(automatic); menu.Items.Add(overridden);
                dock.DocumentContextMenu = menu; await Settle(); await Open(doc);
                Check.Same(dock.GetLayoutItemFromModel(doc), automatic.DataContext); Check.Same(own, overridden.DataContext);
                menu.Hide(); await Settle(); Check.Same(DependencyProperty.UnsetValue, automatic.ReadLocalValue(FrameworkElement.DataContextProperty));
                await Open(other); Check.Same(dock.GetLayoutItemFromModel(other), automatic.DataContext); Check.Same(own, overridden.DataContext);
            });
            Add("auto-hide rail custom menu receives tool adapter context", async () =>
            {
                tool.ToggleAutoHide(); var menu = new MenuFlyout(); var row = new MenuFlyoutItem { Text = "Tool context" }; menu.Items.Add(row);
                dock.AnchorableContextMenu = menu; await Settle(); await Open(tool); Check.Same(dock.GetLayoutItemFromModel(tool), row.DataContext);
                menu.Hide(); await Settle(); Check.Same(DependencyProperty.UnsetValue, row.ReadLocalValue(FrameworkElement.DataContextProperty));
            });
            Add("localization updates retained row labels at reopening", async () =>
            {
                var menu = Menu(doc); var row = Row(menu, "CloseCommand"); var culture = Strings.Culture;
                try
                {
                    Strings.Translations["zz"] = new Dictionary<string, string> { ["Document_Close"] = "Independent translated close" };
                    Strings.Culture = CultureInfo.GetCultureInfo("zz"); await Open(doc);
                    Check.Same(row, Row(menu, "CloseCommand")); Check.Equal("Independent translated close", row.Text);
                }
                finally { Strings.Culture = culture; Strings.Translations.Remove("zz"); }
            });
            Add("bulk close respects protected and cancelled documents", () =>
            {
                var cancelled = new LayoutDocument(); pane.Children.Add(cancelled); cancelled.Closing += (_, e) => e.Cancel = true;
                other.CanClose = false; dock.GetLayoutItemFromModel(doc).CloseAllCommand!.Execute(null);
                Check.True(doc.Parent == null); Check.Same(pane, other.Parent); Check.Same(pane, cancelled.Parent); return Task.CompletedTask;
            });
            Add("bulk close from another adapter cannot recursively enter the same root", () =>
            {
                var initiator = dock.GetLayoutItemFromModel(doc); var second = dock.GetLayoutItemFromModel(other); var calls = 0;
                doc.Closing += (_, _) => { calls++; second.CloseAllCommand!.Execute(null); };
                initiator.CloseAllCommand!.Execute(null); Check.Equal(1, calls); Check.True(doc.Parent == null); Check.True(other.Parent == null); return Task.CompletedTask;
            });
            Add("bulk close stops on root replacement without closing old or new remainder", () =>
            {
                var replacement = new LayoutDocument(); var next = new LayoutRoot { RootPanel = new(new LayoutDocumentPane(replacement)) };
                doc.Closing += (_, _) => dock.Layout = next;
                dock.GetLayoutItemFromModel(doc).CloseAllCommand!.Execute(null);
                Check.Same(next, dock.Layout); Check.True(other.Parent != null); Check.True(replacement.Parent != null); return Task.CompletedTask;
            });
            Add("bulk close does not close a remainder moved to another workspace", () =>
            {
                var destination = new LayoutDocumentPane(); var foreign = new LayoutRoot { RootPanel = new(destination) };
                doc.Closing += (_, _) => { pane.Children.Remove(other); destination.Children.Add(other); };
                dock.GetLayoutItemFromModel(doc).CloseAllCommand!.Execute(null); Check.Same(foreign, other.Root); return Task.CompletedTask;
            });
            Add("bulk close exception releases its reentrancy guard", () =>
            {
                EventHandler<CancelEventArgs> callback = (_, _) => throw new InvalidOperationException("close-observer"); doc.Closing += callback;
                var command = dock.GetLayoutItemFromModel(doc).CloseAllCommand!;
                Check.Throws<InvalidOperationException>(() => command.Execute(null)); doc.Closing -= callback;
                Check.True(command.CanExecute(null)); command.Execute(null); Check.True(doc.Parent == null); Check.True(other.Parent == null); return Task.CompletedTask;
            });
            Add("close all but this retains initiating document and protected peers", () =>
            {
                var protectedDoc = new LayoutDocument { CanClose = false }; pane.Children.Add(protectedDoc);
                dock.GetLayoutItemFromModel(doc).CloseAllButThisCommand!.Execute(null);
                Check.Same(pane, doc.Parent); Check.Same(pane, protectedDoc.Parent); Check.True(other.Parent == null); return Task.CompletedTask;
            });
            Add("group navigation follows document-pane-group siblings not unrelated root panes", async () =>
            {
                var next = new LayoutDocumentPane(new LayoutDocument()); var group = new LayoutDocumentPaneGroup(pane); group.Children.Add(next);
                dock.Layout.RootPanel.Children.Add(group); await Settle(); var menu = Menu(doc);
                Check.True(Row(menu, "MoveToNextTabGroupCommand").IsEnabled);
                Row(menu, "MoveToNextTabGroupCommand").Command.Execute(null); await Settle(); Check.Same(next, doc.Parent);
                Check.True(Row(Menu(doc), "MoveToPreviousTabGroupCommand").IsEnabled);
            });
            foreach (var dark in new[] { false, true })
            foreach (var explicitTheme in new[] { false, true })
                Add($"coherent menu foreground/background: dark={dark}, explicit={explicitTheme}", async () =>
                {
                    dock.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
                    if (explicitTheme) dock.Theme = new FluentTheme(!dark ? ElementTheme.Dark : ElementTheme.Light);
                    await Settle(); var menu = await Open(doc); var foreground = ((SolidColorBrush)Row(menu, "CloseCommand").Foreground).Color;
                    var background = ((SolidColorBrush)Presenter().Background).Color; var isDark = explicitTheme ? !dark : dark;
                    Check.True(isDark ? background.R < 100 && foreground.R > 180 : background.R > 180 && foreground.R < 100);
                });
            Add("open theme change updates the existing presenter and row contrast", async () =>
            {
                var menu = await Open(doc); var presenter = Presenter(); var row = Row(menu, "CloseCommand");
                dock.Theme = new FluentTheme(ElementTheme.Dark); await Settle();
                Check.Same(presenter, Presenter());
                Check.True(((SolidColorBrush)presenter.Background).Color.R < 100);
                Check.True(((SolidColorBrush)row.Foreground).Color.R > 180);
            });
            Add("collapsing the focused command leaves focus on an enabled visible row", async () =>
            {
                var menu = await Open(doc); var row = Row(menu, "CloseCommand"); Check.True(row.Focus(FocusState.Keyboard));
                doc.CanClose = false; await Settle();
                var focused = FocusManager.GetFocusedElement(dock.XamlRoot ?? throw new InvalidOperationException("Menu host is detached.")) as MenuFlyoutItem;
                Check.True(focused != null && focused.IsEnabled && focused.Visibility == Visibility.Visible);
                Check.False(ReferenceEquals(focused, row));
            });
            Add("disabled model prevents even enabled application menu command execution", async () =>
            {
                var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command; var menu = await Open(doc);
                var wrapper = Row(menu, "FloatCommand").Command;
                doc.IsEnabled = false; await Settle(); Check.False(wrapper.CanExecute(null)); wrapper.Execute(null); Check.Equal(0, command.Executions);
            });
            Add("disposed menu clears application commands and rejects retained wrappers", async () =>
            {
                var command = new TestCommand(); var item = dock.GetLayoutItemFromModel(doc); item.FloatCommand = command;
                var menu = await Open(doc); var row = Row(menu, "FloatCommand"); var wrapper = row.Command;
                ((IDisposable)item).Dispose(); await Task.Delay(60);
                Check.True(row.Command == null); Check.False(wrapper.CanExecute(null)); Check.Equal(0, command.Subscriptions);
            });
            Add("menu palette and density overrides apply without replacing rows", async () =>
            {
                var menu = Menu(doc); var row = Row(menu, "CloseCommand"); var brush = new SolidColorBrush(Microsoft.UI.Colors.DarkBlue);
                dock.Resources["UnoDock.MenuBrush"] = brush; dock.Resources["UnoDock.MenuRowHeight"] = 34d; dock.Resources["UnoDock.MenuMinWidth"] = 300d;
                await Settle(); await Open(doc); Check.Same(brush, Presenter().Background); Check.Same(row, Row(menu, "CloseCommand")); Check.Near(34, row.ActualHeight, .15); Check.True(Presenter().ActualWidth >= 300);
            });
            Add("large fonts expand row geometry instead of clipping", async () =>
            {
                dock.Resources["UnoDock.FontSize"] = 24d; await Settle(); var menu = await Open(doc);
                foreach (var row in menu.Items.OfType<MenuFlyoutItem>().Where(r => r.Visibility == Visibility.Visible))
                { Check.True(row.ActualHeight >= 38); Check.Near(24, row.FontSize); Check.True(row.FindVisualChildren<TextBlock>().Single(t => t.Text == row.Text).ActualHeight > 20); }
            });
            Add("real menu items retain native Invoke automation and matching names", async () =>
            {
                var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command; var menu = await Open(doc); var row = Row(menu, "FloatCommand");
                Check.Equal(row.Text, AutomationProperties.GetName(row)); var peer = FrameworkElementAutomationPeer.CreatePeerForElement(row)!;
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke(); await Settle(); Check.Equal(1, command.Executions);
            });
            foreach (var name in new[] { "document", "tool", "tool-autohidden", "dark", "rtl", "large-font" })
                Add("capture compact native menu: " + name, async () =>
                {
                    await Reset(name.StartsWith("tool", StringComparison.Ordinal) ? name : "document");
                    if (name == "dark") dock.Theme = new FluentTheme(ElementTheme.Dark);
                    if (name == "rtl") dock.FlowDirection = FlowDirection.RightToLeft;
                    if (name == "large-font") dock.Resources["UnoDock.FontSize"] = 22d;
                    await Settle(); var menu = await Open(name.StartsWith("tool", StringComparison.Ordinal) ? tool : doc); var presenter = Presenter();
                    await VisualCapture.Save(presenter, System.IO.Path.Combine(output, "visuals", "menu-" + name + ".png"));
                    new XDocument(new XElement("Menu", new XAttribute("name", name), new XAttribute("width", presenter.ActualWidth), new XAttribute("height", presenter.ActualHeight),
                        menu.Items.OfType<MenuFlyoutItem>().Select(r => new XElement("Row", new XAttribute("command", r.Tag!), new XAttribute("text", r.Text),
                            new XAttribute("enabled", r.IsEnabled), new XAttribute("visibility", r.Visibility), new XAttribute("height", r.ActualHeight)))))
                        .Save(System.IO.Path.Combine(output, "visuals", "menu-" + name + ".xml"));
                }, reset: false);
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                Add("XTEST pointer click on native menu row executes once", async () =>
                {
                    var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                    var menu = await Open(doc); var row = Row(menu, "FloatCommand"); using var input = new X11TestInput();
                    input.MoveTo(row, new(row.ActualWidth / 2, row.ActualHeight / 2)); await Task.Delay(60); input.Press(); await Task.Delay(40); input.Release(); await Settle();
                    Check.Equal(1, command.Executions);
                });
                Add("XTEST Escape dismisses menu without executing and releases observers", async () =>
                {
                    var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                    var menu = await Open(doc); Row(menu, "FloatCommand").Focus(FocusState.Keyboard);
                    using var input = new X11TestInput(); input.Escape(); await Settle(); Check.Equal(0, command.Executions); Check.Equal(0, command.Subscriptions);
                });
                Add("XTEST Enter invokes focused native menu item", async () =>
                {
                    var command = new TestCommand(); dock.GetLayoutItemFromModel(doc).FloatCommand = command;
                    var menu = await Open(doc); Check.True(Row(menu, "FloatCommand").Focus(FocusState.Keyboard));
                    using var input = new X11TestInput(); input.KeyPress(0xff0d); await Settle(); Check.Equal(1, command.Executions);
                });
            }
            return await tests.Run(output, "menu-quality");
        }
        finally
        {
            opened?.Hide(); dock.Dispose(); window.Close();
            var owner = Uno.UI.ApplicationHelper.Windows.FirstOrDefault(w => ReferenceEquals(w.Content?.XamlRoot, templateSource.XamlRoot)); owner?.Activate();
            await Task.Delay(60);
        }
        void Add(string name, Func<Task> body, bool reset = true) => tests.Test(name, async () =>
        {
            try { if (reset) await Reset(); await body(); }
            finally { opened?.Hide(); opened = null; await Task.Delay(40); }
        });
        async Task Reset(string name = "document")
        {
            opened?.Hide(); opened = null; dock.Theme = null; dock.RequestedTheme = ElementTheme.Light;
            dock.DocumentContextMenu = dock.AnchorableContextMenu = null;
            foreach (var key in new[] { "FontSize", "MenuBrush", "MenuRowHeight", "MenuMinWidth" }) dock.Resources.Remove("UnoDock." + key);
            dock.FlowDirection = FlowDirection.LeftToRight;
            doc = new() { ContentId = "doc", Title = "Workspace.cs", Content = new TextBox { AcceptsReturn = true, Text = "// Owned menu test\n// Second row" } };
            other = new() { ContentId = "other", Title = "Readme.md" }; pane = new(doc);
            if (name != "document-single") pane.Children.Add(other);
            tool = new() { ContentId = "tool", Title = "Solution Explorer", Content = new TextBlock { Text = "Owned tool content" } };
            var panel = new LayoutPanel(new LayoutAnchorablePane(tool) { DockWidth = new(200) }); panel.Children.Add(pane);
            if (name == "document-multiple-groups") panel.Children.Add(new LayoutDocumentPane(new LayoutDocument { ContentId = "third" }));
            dock.Layout = new() { RootPanel = panel };
            if (name == "document-no-close") doc.CanClose = false;
            if (name == "document-no-float") doc.CanFloat = false;
            if (name == "tool-no-hide") tool.CanHide = false;
            if (name == "tool-no-autohide") tool.CanAutoHide = false;
            if (name == "tool-no-document") tool.CanDockAsTabbedDocument = false;
            if (name == "tool-autohidden") tool.ToggleAutoHide();
            await Settle();
        }
        async Task Settle() { dock.Refresh(); dock.UpdateLayout(); await Task.Delay(90); dock.UpdateLayout(); }
        FrameworkElement Target(LayoutContent model) => model is LayoutAnchorable { IsAutoHidden: true }
            ? dock.FindVisualChildren<LayoutAnchorControl>().Single(c => ReferenceEquals(c.Model, model))
            : model is LayoutAnchorable
                ? dock.FindVisualChildren<LayoutAnchorablePaneControl>().First(c => ReferenceEquals(c.Model, model.Parent))
                : dock.FindVisualChildren<LayoutTabItemBase>().First(c => ReferenceEquals(c.Model, model));
        MenuFlyout Menu(LayoutContent model) => (MenuFlyout)Target(model).ContextFlyout;
        async Task<MenuFlyout> Open(LayoutContent model)
        { var menu = Menu(model); opened = menu; menu.ShowAt(Target(model)); await Settle(); return menu; }
        MenuFlyoutPresenter Presenter() => VisualTreeHelper.GetOpenPopupsForXamlRoot(dock.XamlRoot)
            .SelectMany(p => p.Child is MenuFlyoutPresenter m ? new[] { m } : p.Child.FindVisualChildren<MenuFlyoutPresenter>()).Single();
        static MenuFlyoutItem Row(MenuFlyout menu, string command) => menu.Items.OfType<MenuFlyoutItem>().Single(r => (string?)r.Tag == command);
    }
}
