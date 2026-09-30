using UnoDock.Controls;
using UnoDock.Layout;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;
/// <summary>DocumentTabStripMode.TabView: platform TabView hosting of document
/// tabs with unchanged model, docking and editor-retention behavior.</summary>
internal static class TabViewStripTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        tests.Test("tabview: document panes host their tabs in a TabView and retain editors", async () =>
        {
            using var f = new Fixture();
            await f.Show();
            var editor = f.First.Content;
            f.Manager.DocumentTabStripMode = DocumentTabStripMode.TabView;
            await f.Settle();
            var tabView = f.TabView();
            Check.Equal(2, tabView.TabItems.Count);
            Check.True(tabView.TabItems.Cast<TabViewItem>().All(i => i.Header is LayoutDocumentTabItem), "Each TabViewItem hosts the retained docking tab.");
            Check.Same(editor, f.First.Content);
            Check.Same(f.First, ((TabViewItem)tabView.SelectedItem).Tag);
            Check.True(f.Manager.FindVisualChildren<LayoutAnchorablePaneControl>().All(p => !p.FindVisualChildren<TabView>().Any()), "Tool panes keep the docking strip.");
            await VisualCapture.Save(f.Manager, Path.Combine(output, "tabview", "tabview-light.png"));
        });
        tests.Test("tabview: selecting and closing through the TabView updates the model", async () =>
        {
            using var f = new Fixture();
            f.Manager.DocumentTabStripMode = DocumentTabStripMode.TabView;
            await f.Show();
            var tabView = f.TabView();
            tabView.SelectedItem = tabView.TabItems.Cast<TabViewItem>().Single(i => ReferenceEquals(i.Tag, f.Second));
            await f.Settle();
            Check.True(f.Second.IsSelected && f.Second.IsActive, "TabView selection activates the document.");
            var closing = f.Second;
            var item = tabView.TabItems.Cast<TabViewItem>().Single(i => ReferenceEquals(i.Tag, closing));
            var close = item.FindVisualChildren<Button>().Single(b => b.Name == "CloseButton");
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(close)).Invoke();
            await f.Settle();
            Check.True(closing.Parent == null, "The closed document leaves the layout.");
            Check.Equal(1, f.TabView().TabItems.Count);
        });
        tests.Test("tabview: the TabView strip is a docking header with insertion plans", async () =>
        {
            using var f = new Fixture();
            f.Manager.DocumentTabStripMode = DocumentTabStripMode.TabView;
            await f.Show();
            var tabView = f.TabView();
            var point = tabView.TransformToVisual(f.Manager).TransformPoint(new(tabView.ActualWidth - 20, tabView.ActualHeight / 2));
            var plan = f.Manager.GetDropPlan(f.Other, point);
            Check.True(plan != null, $"No drop plan over the TabView strip at {point} (strip {tabView.ActualWidth}x{tabView.ActualHeight}).");
            Check.Equal(DropTargetType.DocumentPaneDockInside, plan!.Type);
            Check.True(plan.Execute());
            await f.Settle();
            Check.True(ReferenceEquals(f.Other.Parent, f.First.Parent));
            Check.Equal(3, f.TabView().TabItems.Count);
        });
        tests.Test("tabview: returning to the docking strip restores the theme tabs", async () =>
        {
            using var f = new Fixture();
            f.Manager.DocumentTabStripMode = DocumentTabStripMode.TabView;
            await f.Show();
            var editor = f.Second.Content;
            f.Manager.DocumentTabStripMode = DocumentTabStripMode.Docking;
            await f.Settle();
            Check.True(!f.Manager.FindVisualChildren<TabView>().Any(t => t.Visibility == Visibility.Visible), "No visible TabView remains.");
            Check.Equal(2, f.Manager.FindVisualChildren<LayoutDocumentTabItem>().Count(t => t.ActualWidth > 0 && ReferenceEquals(t.Model?.Parent, f.First.Parent)));
            Check.Same(editor, f.Second.Content);
            Check.Throws<ArgumentOutOfRangeException>(() => f.Manager.DocumentTabStripMode = (DocumentTabStripMode)42);
            Check.Equal(DocumentTabStripMode.Docking, f.Manager.DocumentTabStripMode);
        });
        return await tests.Run(output, "tabview-strip");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Manager = new()
        {
            FloatingWindowMode = FloatingWindowMode.InSurface,
            Theme = new FluentTheme(ElementTheme.Light)
        };
        internal readonly LayoutDocument First = new()
        {
            Title = "Program.cs",
            ContentId = "tv-first",
            Content = new TextBox
            {
                Text = "First editor"
            }
        };
        internal readonly LayoutDocument Second = new()
        {
            Title = "App.xaml",
            ContentId = "tv-second",
            Content = new TextBox
            {
                Text = "Second editor"
            }
        };
        internal readonly LayoutDocument Other = new()
        {
            Title = "Other.cs",
            ContentId = "tv-other",
            Content = new TextBox
            {
                Text = "Other editor"
            }
        };
        private readonly Window _window;
        internal Fixture()
        {
            var documents = new LayoutDocumentPane(First);
            documents.Children.Add(Second);
            var tools = new LayoutAnchorablePane(new LayoutAnchorable { Title = "Explorer", ContentId = "tv-tool", Content = new TextBlock { Text = "Tool" } })
            {
                DockWidth = new(220)
            };
            var panel = new LayoutPanel(tools);
            panel.Children.Add(documents);
            panel.Children.Add(new LayoutDocumentPane(Other));
            Manager.Layout = new()
            {
                RootPanel = panel
            };
            // In-surface only: resolve drops without native window stacking, which
            // other desktop applications can influence on an interactive session.
            Manager.CrossWindowCoordinates = null;
            First.IsActive = true;
            _window = new Window
            {
                Content = Manager,
                Title = "UnoDock TabView acceptance"
            };
            _window.AppWindow.Resize(new()
            {
                Width = 1100,
                Height = 700
            });
        }

        internal async Task Show()
        {
            _window.Activate();
            for (var i = 0; i < 80 && (!Manager.IsLoaded || Manager.ActualWidth <= 0); i++)
                await Task.Delay(25);
            await Settle();
        }

        internal async Task Settle()
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(40);
                Manager.UpdateLayout();
            }
        }

        internal TabView TabView() => Manager.FindVisualChildren<LayoutDocumentPaneControl>().First(p => ReferenceEquals(p.Model, First.Parent)).FindVisualChildren<TabView>().Single();
        public void Dispose() => _window.Close();
    }
}
