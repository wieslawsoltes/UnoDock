using System.Collections;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Markup;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Themes;
using Windows.Foundation;

namespace UnoDock.Testing;

internal static class SampleQualityTests
{
    private sealed class InitializedManager : DockingManager
    {
        internal int Calls;
        internal Action? Initializing;
        protected override void OnInitialized(EventArgs e)
        {
            Calls++;
            Initializing?.Invoke();
            base.OnInitialized(e);
        }
    }

    private sealed class InitializedPanel(LayoutPanel model) : LayoutGridControl<ILayoutPanelElement>(model)
    {
        internal int Calls;
        protected override void OnInitialized(EventArgs e)
        {
            Calls++;
            base.OnInitialized(e);
        }

        protected override void OnFixChildrenDockLengths()
        {
        }
    }

    private sealed class LeavePanel : DocumentPaneTabPanel
    {
        internal int Leaves;
        protected override void OnMouseLeave(DockMouseEventArgs e)
        {
            Leaves++;
            e.Handled = true;
            base.OnMouseLeave(e);
        }
    }

    internal static async Task<int> Run(GalleryPage source, string output)
    {
        using var page = new GalleryPage
        {
            Width = 1000,
            Height = 720
        };
        var window = new Window
        {
            Content = page,
            Title = "UnoDock sample acceptance"
        };
        window.AppWindow.Resize(new()
        {
            Width = 1100,
            Height = 830
        });
        window.Activate();
        try
        {
            await Wait(() => page.IsLoaded && page.Dock.ActualWidth > 0);
            var tests = new TestRunner();
            tests.Test("namespace: exported product types no longer use Xceed namespaces", () =>
            {
                Check.Equal("UnoDock.DockingManager", typeof(DockingManager).FullName);
                Check.Equal("UnoDock.Layout.LayoutDocument", typeof(LayoutDocument).FullName);
                Check.Equal("UnoDock.Controls.DropDownButton", typeof(UnoDock.Controls.DropDownButton).FullName);
                Check.False(typeof(DockingManager).Assembly.GetExportedTypes().Any(type => type.FullName!.StartsWith("Xceed.", StringComparison.Ordinal)));
            });
            tests.Test("namespace: runtime XAML resolves the renamed manager and model", () =>
            {
                using var manager = (DockingManager)XamlReader.Load("<dock:DockingManager xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:dock='using:UnoDock' xmlns:layout='using:UnoDock.Layout'><layout:LayoutRoot><layout:LayoutPanel><layout:LayoutDocumentPane><layout:LayoutDocument Title='New namespace' ContentId='renamed'/></layout:LayoutDocumentPane></layout:LayoutPanel></layout:LayoutRoot></dock:DockingManager>");
                Check.Equal("renamed", manager.Layout.Descendents().OfType<LayoutDocument>().Single().ContentId);
            });
            Add("classic sample has the public Properties/Documents/Alarms arrangement", () =>
            {
                Check.Equal(2, Docs().Count());
                Check.Equal(5, page.Dock.Layout.Descendents().OfType<LayoutAnchorable>().Count());
                var properties = Tool("properties");
                Check.False(properties.CanHide);
                Check.False(properties.CanClose);
                Check.Near(200, ((ILayoutPositionableElement)properties.Parent!).DockWidth.Value);
                Check.Equal("Alarms", Tool("alarms").Title);
                Check.Same(Tool("alarms").Parent, Tool("journal").Parent);
                Check.Equal(2, page.Dock.Layout.LeftSide.Children.Single().ChildrenCount);
                Check.True(Tool("agenda").IsAutoHidden);
                Check.True(Tool("contacts").IsAutoHidden);
                return Task.CompletedTask;
            });
            Add("compact menu replaces the previous oversized brand and scrolling command strip", () =>
            {
                var menu = page.FindVisualChildren<MenuBar>().Single();
                Check.Equal(5, menu.Items.Count);
                Check.True(menu.ActualHeight <= 28.1);
                Check.True(page.Dock.ActualHeight >= page.ActualHeight - 90);
                Check.True(page.FindVisualChildren<ComboBox>().Any(box => AutomationProperties.GetAutomationId(box) == "ThemeSelector"));
                return Task.CompletedTask;
            });
            Add("inspector follows the last focused document rather than stealing selection", async () =>
            {
                var second = Docs().Single(d => d.ContentId == "document2");
                second.IsActive = true;
                await Wait(() => page.PropertyInspector?.SelectedContentId == "document2");
                Tool("alarms").IsActive = true;
                await Task.Delay(30);
                Check.Equal("document2", page.PropertyInspector!.SelectedContentId);
            });
            Add("inspector commits live font, title and color edits", async () =>
            {
                var second = Docs().Single(d => d.ContentId == "document2");
                second.IsActive = true;
                await Wait(() => page.PropertyInspector?.SelectedContentId == "document2");
                var inspector = page.PropertyInspector!;
                var editor = (TextBox)second.Content!;
                Check.True(inspector.TryEdit("FontSize", "24"));
                Check.Near(24, editor.FontSize);
                Check.True(inspector.TryEdit("Title", "Edited document"));
                Check.Equal("Edited document", second.Title);
                Check.True(inspector.TryEdit("Background", "#FFEEDDCC"));
                Check.Equal((byte)0xee, ((SolidColorBrush)editor.Background).Color.R);
                Check.True(inspector.TryEdit("Width", "Auto"));
                Check.True(double.IsNaN(editor.Width));
            });
            foreach (var value in new[]
            {
                "NaN",
                "Infinity",
                "-1",
                "0",
                "10000",
                "not a number"
            }

            )
                Add("inspector rejects invalid font size: " + value, async () =>
                {
                    var first = Docs().First();
                    await Wait(() => page.PropertyInspector?.SelectedContentId == first.ContentId);
                    var content = (Control)first.Content!;
                    var before = content.FontSize;
                    Check.False(page.PropertyInspector!.TryEdit("FontSize", value));
                    Check.Near(before, content.FontSize);
                    Check.True(page.PropertyInspector.LastError != null);
                });
            Add("inspector keeps ContentId read-only and filters existing editors in place", async () =>
            {
                var inspector = page.PropertyInspector!;
                await Wait(() => inspector.VisibleFieldCount > 0);
                Check.False(inspector.TryEdit("ContentId", "broken"));
                inspector.Filter("FontSize");
                Check.Equal(1, inspector.VisibleFieldCount);
                inspector.Filter("absent-field");
                Check.Equal(0, inspector.VisibleFieldCount);
                inspector.Filter("");
                Check.True(inspector.VisibleFieldCount >= 10);
            });
            Add("layout XML restore retains edited document control identity", async () =>
            {
                var second = Docs().Single(d => d.ContentId == "document2");
                var editor = (TextBox)second.Content!;
                editor.Text = "Unsaved editor buffer";
                second.IsActive = true;
                var xml = page.SerializeSample();
                Check.True(xml.Contains("document2", StringComparison.Ordinal));
                page.RestoreSample(xml);
                await Task.Delay(80);
                var restored = Docs().Single(d => d.ContentId == "document2");
                Check.Same(editor, restored.Content);
                Check.Equal("Unsaved editor buffer", editor.Text);
                Check.Same(page.Dock.Layout, restored.Root);
            });
            Add("theme switches retain the root and both document content instances", async () =>
            {
                var root = page.Dock.Layout;
                var content = Docs().Select(d => d.Content).ToArray();
                foreach (var theme in Enum.GetValues<SampleTheme>())
                {
                    page.SetSampleTheme(theme);
                    await Task.Delay(50);
                    page.Dock.Refresh();
                    Check.Same(root, page.Dock.Layout);
                    Check.True(content.SequenceEqual(Docs().Select(d => d.Content)));
                }

                page.SetSampleTheme(SampleTheme.Generic);
            });
            Add("large text expands actual tool captions and document tabs", async () =>
            {
                var pane = page.Dock.FindVisualChildren<LayoutDocumentPaneControl>().Single();
                var before = pane.FindVisualChildren<LayoutDocumentTabItem>().First().ActualHeight;
                page.ExecuteSampleCommand("large-text");
                await Task.Delay(80);
                page.UpdateLayout();
                var after = pane.FindVisualChildren<LayoutDocumentTabItem>().First().ActualHeight;
                Check.True(after > before + 5, $"Tab height stayed at {after} for large text (before {before}).");
                var title = page.Dock.FindVisualChildren<TextBlock>().First(text => text.Text == "Properties");
                Check.True(title.ActualHeight >= 18);
                page.ExecuteSampleCommand("normal-density");
            });
            Add("sample commands mutate actual MVVM sources and restore the classic model", async () =>
            {
                page.SwitchSample(SampleKind.Binding);
                await Task.Delay(60);
                Check.Equal(2, Docs().Count());
                page.ExecuteSampleCommand("new");
                await Task.Delay(60);
                Check.Equal(3, Docs().Count());
                page.SwitchSample(SampleKind.Workspace);
                await Task.Delay(60);
                Check.True(Docs().Any(d => d.ContentId == "code"));
                page.SwitchSample(SampleKind.Classic);
                await Task.Delay(60);
                Check.Equal(2, Docs().Count());
            });
            Add("narrow sample preserves an accessible document viewport", async () =>
            {
                page.Width = 640;
                page.Height = 480;
                await Task.Delay(80);
                page.UpdateLayout();
                var pane = page.Dock.FindVisualChildren<LayoutDocumentPaneControl>().Single();
                Check.True(pane.ActualWidth >= 200);
                Check.True(pane.ActualHeight >= 300);
                await VisualCapture.Save(page, Path.Combine(output, "visuals", "sample-narrow.png"));
                page.Width = 1000;
                page.Height = 720;
            });
            foreach (var scenario in new[]
            {
                "classic",
                "workspace",
                "dark",
                "rtl",
                "large-text"
            }

            )
                Add("capture independently rendered sample: " + scenario, async () =>
                {
                    page.SwitchSample(scenario == "workspace" ? SampleKind.Workspace : SampleKind.Classic);
                    page.SetSampleTheme(scenario == "dark" ? SampleTheme.Dark : SampleTheme.Generic);
                    page.Dock.FlowDirection = scenario == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    if (scenario == "large-text")
                        page.ExecuteSampleCommand("large-text");
                    await Task.Delay(150);
                    page.Dock.Refresh();
                    page.UpdateLayout();
                    await Task.Delay(30);
                    await VisualCapture.Save(page, Path.Combine(output, "visuals", "sample-" + scenario + ".png"));
                    new XDocument(VisualScene.Measure(page.Dock, scenario)).Save(Path.Combine(output, "visuals", "sample-" + scenario + ".xml"));
                    page.ExecuteSampleCommand("normal-density");
                });
            Add("capture classic documentation scene at the exact reference viewport", async () =>
            {
                page.Width = 1004;
                page.Height = 727;
                foreach (var scenario in new[]
                {
                    "classic",
                    "selected-editor",
                    "rtl"
                }

                )
                {
                    page.SwitchSample(SampleKind.Classic);
                    page.Dock.FlowDirection = scenario == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    if (scenario == "selected-editor")
                        Docs().Single(d => d.ContentId == "document2").IsActive = true;
                    await Task.Delay(150);
                    page.Dock.Refresh();
                    page.UpdateLayout();
                    Check.Near(1000, page.Dock.ActualWidth);
                    Check.Near(640, page.Dock.ActualHeight);
                    await VisualCapture.Save(page.Dock, Path.Combine(output, "visuals", "sample-surface-" + scenario + ".png"));
                    new XDocument(VisualScene.Measure(page.Dock, scenario)).Save(Path.Combine(output, "visuals", "sample-surface-" + scenario + ".xml"));
                }
            });
            tests.Test("initialization: manager hook runs after construction and only once across reattachment", async () =>
            {
                using var manager = new InitializedManager
                {
                    Template = source.Dock.Template,
                    Width = 300,
                    Height = 180
                };
                Check.Equal(0, manager.Calls);
                var original = page.Content;
                var host = new Grid();
                page.Content = host;
                try
                {
                    host.Children.Add(manager);
                    await Wait(() => manager.IsLoaded);
                    Check.Equal(1, manager.Calls);
                    host.Children.Remove(manager);
                    await Task.Delay(30);
                    host.Children.Add(manager);
                    await Wait(() => manager.IsLoaded);
                    Check.Equal(1, manager.Calls);
                }
                finally
                {
                    host.Children.Clear();
                    page.Content = original;
                }
            });
            tests.Test("initialization: callback root replacement is the one rendered", async () =>
            {
                using var manager = new InitializedManager
                {
                    Template = source.Dock.Template,
                    Width = 300,
                    Height = 180
                };
                var replacement = new LayoutRoot
                {
                    RootPanel = new LayoutPanel(new LayoutDocumentPane(new LayoutDocument { Title = "Replacement", ContentId = "replacement" }))
                };
                manager.Initializing = () => manager.Layout = replacement;
                var original = page.Content;
                page.Content = manager;
                try
                {
                    await Wait(() => manager.IsLoaded && manager.FindVisualChildren<LayoutDocumentTabItem>().Any());
                    Check.Same(replacement, manager.Layout);
                }
                finally
                {
                    page.Content = original;
                }
            });
            tests.Test("initialization: grid hook runs once after a derived constructor", async () =>
            {
                var panel = new InitializedPanel(new LayoutPanel())
                {
                    Width = 200,
                    Height = 100
                };
                Check.Equal(0, panel.Calls);
                var original = page.Content;
                page.Content = panel;
                try
                {
                    await Wait(() => panel.IsLoaded);
                    Check.Equal(1, panel.Calls);
                }
                finally
                {
                    page.Content = original;
                }
            });
            tests.Test("logical content enumeration is a stable snapshot across layout replacement", async () =>
            {
                page.SwitchSample(SampleKind.Classic);
                await Task.Delay(60);
                page.Dock.Refresh();
                var iterator = page.Dock.LogicalChildrenPublic;
                page.SwitchSample(SampleKind.Workspace);
                await Task.Delay(60);
                var count = 0;
                while (iterator.MoveNext())
                    count++;
                Check.True(count > 0);
            });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
                tests.Test("XTEST: document header leave dispatches its real protected hook", async () =>
                {
                    var original = page.Content;
                    var root = new Grid();
                    var panel = new LeavePanel
                    {
                        Width = 200,
                        Height = 40,
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    root.Children.Add(panel);
                    page.Content = root;
                    try
                    {
                        await Wait(() => panel.IsLoaded);
                        using var input = new X11TestInput();
                        input.MoveTo(panel, new(20, 20));
                        await Task.Delay(80);
                        var before = panel.Leaves;
                        input.MoveTo(root, new(500, 300));
                        await Wait(() => panel.Leaves > before);
                        Check.Equal(before + 1, panel.Leaves);
                    }
                    finally
                    {
                        page.Content = original;
                    }
                });
            return await tests.Run(output, "sample-quality");
            IEnumerable<LayoutDocument> Docs() => page.Dock.Layout.Descendents().OfType<LayoutDocument>();
            LayoutAnchorable Tool(string id) => page.Dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(tool => tool.ContentId == id);
            void Add(string name, Func<Task> body) => tests.Test(name, async () =>
            {
                page.SwitchSample(SampleKind.Classic);
                page.SetSampleTheme(SampleTheme.Generic);
                page.Dock.FlowDirection = FlowDirection.LeftToRight;
                page.Width = 1000;
                page.Height = 720;
                page.ExecuteSampleCommand("normal-density");
                await Task.Delay(60);
                page.Dock.Refresh();
                page.UpdateLayout();
                await body();
            });
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "Sample condition did not converge in the bounded wait.");
    }
}
