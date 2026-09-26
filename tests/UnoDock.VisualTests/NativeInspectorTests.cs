using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls.Primitives;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class NativeInspectorTests
{
    internal static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        Add("native inspector: native ListView containers and automation are used", async f =>
        {
            Check.True(f.List.Items.Count > f.Inspector.VisibleFieldCount);
            Check.True(f.List.Items.All(item => item is ListViewItem));
            Check.True(FrameworkElementAutomationPeer.CreatePeerForElement(f.List) is ListViewAutomationPeer);
            Check.False(f.Inspector.FindVisualChildren<SampleButton>().Any());
            Check.True(f.Inspector.FindVisualChildren<ToggleButton>().Any(button => AutomationProperties.GetAutomationId(button) == "PropertyCategory-Appearance"));
            Check.True(f.Field<TextBox>("FontSize").Template != null);
            Check.True(f.Field<CheckBox>("CanFloat").Template != null);
            Check.True(f.Field<ComboBox>("TextWrapping").Template != null);
            await Task.CompletedTask;
        });
        Add("native inspector: row selection supplies the property description", async f =>
        {
            var row = f.Row("FontSize");
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(row);
            var selection = peer?.GetPattern(PatternInterface.SelectionItem) as ISelectionItemProvider;
            Check.True(selection != null);
            selection!.Select();
            await Wait(() => f.Inspector.SelectedPropertyName == "FontSize");
            Check.Same(row, f.List.SelectedItem);
            Check.True(AutomationProperties.GetHelpText(row).Contains("DIPs", StringComparison.Ordinal));
        });
        Add("native inspector: editor focus and row selection remain synchronized", async f =>
        {
            f.Inspector.Filter("FontSize");
            await f.Settle();
            Check.True(f.Field<TextBox>("FontSize").Focus(FocusState.Programmatic));
            await Wait(() => ReferenceEquals(f.List.SelectedItem, f.Row("FontSize")));
            Check.Equal("FontSize", f.Inspector.SelectedPropertyName);
        });
        Add("native inspector: native category toggle updates collapse state", async f =>
        {
            var count = f.Inspector.VisibleFieldCount;
            var toggle = f.Inspector.FindVisualChildren<ToggleButton>().Single(button => AutomationProperties.GetAutomationId(button) == "PropertyCategory-Appearance");
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(toggle);
            var provider = peer?.GetPattern(PatternInterface.Toggle) as IToggleProvider;
            Check.True(provider != null);
            provider!.Toggle();
            await Wait(() => f.Inspector.VisibleFieldCount < count);
            Check.Equal(Visibility.Collapsed, f.Row("FontSize").Visibility);
            provider.Toggle();
            await Wait(() => f.Inspector.VisibleFieldCount == count);
        });
        Add("native inspector: filtering collapses containers without blank rows", async f =>
        {
            var row = f.Row("FontSize");
            var editor = f.Field<TextBox>("FontSize");
            f.Inspector.Filter("FontSize");
            await f.Settle();
            Check.Equal(1, f.Inspector.VisibleFieldCount);
            Check.Equal(2, f.List.Items.OfType<ListViewItem>().Count(item => item.Visibility == Visibility.Visible));
            Check.Same(row, f.Row("FontSize"));
            Check.Same(editor, f.Field<TextBox>("FontSize"));
            f.Inspector.Filter("no such field");
            await f.Settle();
            Check.Equal(0, f.List.Items.OfType<ListViewItem>().Count(item => item.Visibility == Visibility.Visible));
        });
        Add("native inspector: order changes preserve native containers and typed editors", async f =>
        {
            var row = f.Row("FontSize");
            var editor = f.Field<TextBox>("FontSize");
            f.Inspector.SetOrder(true);
            await f.Settle();
            Check.Equal(f.Inspector.VisibleFieldCount, f.List.Items.Count);
            Check.Same(row, f.Row("FontSize"));
            Check.Same(editor, f.Field<TextBox>("FontSize"));
            f.Inspector.SetOrder(false);
            await f.Settle();
            Check.Same(row, f.Row("FontSize"));
            Check.Same(editor, f.Field<TextBox>("FontSize"));
        });
        Add("native inspector: presentation filtering does not implicitly commit a draft", async f =>
        {
            f.Inspector.Filter("FontSize");
            await f.Settle();
            var editor = f.Field<TextBox>("FontSize");
            var before = f.Editor.FontSize;
            Check.True(editor.Focus(FocusState.Programmatic));
            editor.Text = "27.75";
            f.Inspector.Filter("Title");
            await f.Settle();
            Check.Equal(before, f.Editor.FontSize);
            f.Inspector.Filter("FontSize");
            await f.Settle();
            Check.Same(editor, f.Field<TextBox>("FontSize"));
            Check.Equal("27.75", editor.Text);
            Check.True(editor.Focus(FocusState.Programmatic));
            Check.True(f.Search.Focus(FocusState.Programmatic));
            await Wait(() => f.Editor.FontSize == 27.75);
        });
        Add("native inspector: presentation sorting cannot commit a focused draft", async f =>
        {
            f.Inspector.Filter("FontSize");
            await f.Settle();
            var editor = f.Field<TextBox>("FontSize");
            var before = f.Editor.FontSize;
            editor.Focus(FocusState.Programmatic);
            editor.Text = "31.5";
            f.Inspector.SetOrder(true);
            f.Inspector.SetOrder(false);
            await f.Settle();
            Check.Equal(before, f.Editor.FontSize);
            Check.Equal("31.5", editor.Text);
        });
        Add("native inspector: typed editors retain original parsing and conflict rules", async f =>
        {
            Check.True(f.Inspector.TryEdit("Padding", "5,6"));
            Check.Equal(new Thickness(5, 6, 5, 6), f.Editor.Padding);
            var before = f.Editor.FontSize;
            Check.False(f.Inspector.TryEdit("FontSize", "NaN"));
            Check.Equal(before, f.Editor.FontSize);
            Check.False(f.Inspector.TryEdit("TextWrapping", "999"));
            Check.False(f.Inspector.TryEdit("ContentId", "changed"));
            Check.True(f.Field<TextBlock>("ContentId").IsTextSelectionEnabled);
            f.Field<CheckBox>("CanFloat").IsChecked = false;
            Check.False(f.Document.CanFloat);
            f.Document.CanFloat = true;
            Check.True(f.Field<CheckBox>("CanFloat").IsChecked == true);
            await Task.CompletedTask;
        });
        Add("native inspector: retained old native editors cannot edit a replacement selection", async f =>
        {
            var stale = f.Field<CheckBox>("CanFloat");
            var before = f.Document;
            var next = f.Page.Dock.Layout.Descendents().OfType<LayoutDocument>().First(document => !ReferenceEquals(document, before));
            next.IsActive = true;
            await Wait(() => f.Inspector.SelectedContentId == next.ContentId);
            stale.IsChecked = false;
            Check.True(before.CanFloat && next.CanFloat);
        });
        foreach (var mode in new[] { "light", "dark", "rtl", "narrow", "invalid" })
        {
            Add("native inspector: Fluent presentation retains editors: " + mode, async f =>
            {
                var editor = f.Field<TextBox>("FontSize");
                var row = f.Row("FontSize");
                f.Page.SetSampleTheme(mode == "dark" ? SampleTheme.Dark : SampleTheme.Light);
                if (mode == "rtl")
                {
                    f.Inspector.FlowDirection = FlowDirection.RightToLeft;
                }
                if (mode == "narrow")
                {
                    f.Page.Width = 640;
                }
                if (mode == "invalid")
                {
                    f.Inspector.TryEdit("FontSize", "NaN");
                }
                f.Inspector.NameColumnWidth = 112;
                await f.Settle();
                Check.Same(editor, f.Field<TextBox>("FontSize"));
                Check.Same(row, f.Row("FontSize"));
                Check.True(f.List.ActualWidth > 0 && f.List.ActualHeight > 0);
                var path = Path.Combine(output, "visuals", "native-inspector-" + mode + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await VisualCapture.Save(f.Page, path);
            });
        }
        Add("native inspector: disposing rejects retained row callbacks", async f =>
        {
            var input = f.Field<CheckBox>("CanFloat");
            var document = f.Document;
            f.Inspector.Dispose();
            input.IsChecked = false;
            Check.True(document.CanFloat);
            Check.Equal(0, f.List.Items.Count);
            Check.Throws<ObjectDisposedException>(() => f.Inspector.TryEdit("Title", "stale"));
            await Task.CompletedTask;
        });
        return tests.Run(output, "native-inspector");

        void Add(string name, Func<Fixture, Task> body) => tests.Test(name, async () =>
        {
            using var fixture = new Fixture();
            await fixture.Show();
            await body(fixture);
        });
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly GalleryPage Page = new() { Width = 1000, Height = 720 };
        private readonly Window _window;
        internal SamplePropertyInspector Inspector => Page.PropertyInspector!;
        internal LayoutDocument Document => Page.Dock.Layout.Descendents().OfType<LayoutDocument>().Single(document => document.ContentId == "document2");
        internal TextBox Editor => (TextBox)Document.Content!;
        internal ListView List => Inspector.FindVisualChildren<ListView>().Single(list => AutomationProperties.GetAutomationId(list) == "PropertyTable");
        internal TextBox Search => Inspector.FindVisualChildren<TextBox>().Single(editor => AutomationProperties.GetAutomationId(editor) == "PropertySearch");

        internal Fixture()
        {
            _window = new Window { Content = Page, Title = "UnoDock native property inspector acceptance" };
            _window.AppWindow.Resize(new() { Width = 1100, Height = 830 });
            _window.Activate();
        }

        internal async Task Show()
        {
            await Wait(() => Page.IsLoaded && Page.PropertyInspector?.VisibleFieldCount > 0);
            Document.IsActive = true;
            await Wait(() => Inspector.SelectedContentId == "document2");
            await Settle();
        }

        internal async Task Settle()
        {
            Page.UpdateLayout();
            await Task.Delay(70);
        }

        internal T Field<T>(string name) where T : FrameworkElement => Inspector.FindVisualChildren<T>().Single(element => AutomationProperties.GetAutomationId(element) == "Property-" + name);
        internal ListViewItem Row(string name) => List.Items.OfType<ListViewItem>().Single(item => AutomationProperties.GetAutomationId(item) == "PropertyRow-" + name);

        public void Dispose()
        {
            _window.Content = null;
            Page.Dispose();
            _window.Close();
        }
    }

    private static async Task Wait(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 120 && !predicate(); attempt++)
        {
            await Task.Delay(25);
        }
        Check.True(predicate(), "Native inspector did not reach the required state.");
    }
}
