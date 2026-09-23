using System.Globalization;
using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Gallery;

namespace UnoDock.Testing;

internal static class InspectorQualityTests
{
    internal static async Task<int> Run(string output)
    {
        using var page = new GalleryPage { Width = 1000, Height = 720 };
        var window = new Window { Content = page, Title = "UnoDock property inspector acceptance" };
        window.AppWindow.Resize(new() { Width = 1100, Height = 830 }); window.Activate();
        try
        {
            await Wait(() => page.IsLoaded && page.PropertyInspector?.VisibleFieldCount > 0);
            var tests = new TestRunner();
            Add("inspector: booleans use real CheckBox editors and write through", () =>
            {
                var check = Field<CheckBox>("CanFloat"); Check.True(check.IsChecked == true);
                check.IsChecked = false; Check.False(Document().CanFloat);
                Document().CanFloat = true; Check.True(check.IsChecked == true);
            });
            Add("inspector: enum choices write through and reject unnamed numeric values", () =>
            {
                var choice = Field<ComboBox>("TextWrapping"); choice.SelectedItem = "Wrap";
                Check.Equal(TextWrapping.Wrap, Editor().TextWrapping);
                Check.False(Inspector().TryEdit("TextWrapping", "999")); Check.Equal(TextWrapping.Wrap, Editor().TextWrapping);
                Check.True(Inspector().TryEdit("TextWrapping", "NoWrap"));
            });
            Add("inspector: margin and padding support one two and four components", () =>
            {
                Check.True(Inspector().TryEdit("Margin", "1,2,3,4")); Check.Equal(new Thickness(1, 2, 3, 4), Editor().Margin);
                Check.True(Inspector().TryEdit("Padding", "5,6")); Check.Equal(new Thickness(5, 6, 5, 6), Editor().Padding);
                Check.True(Inspector().TryEdit("Margin", "-1")); Check.Equal(new Thickness(-1), Editor().Margin);
                Check.False(Inspector().TryEdit("Padding", "-1")); Check.Equal(new Thickness(5, 6, 5, 6), Editor().Padding);
            });
            foreach (var value in new[] { "1,2,3", "NaN", "1,Infinity", "1,2,3,4,5", "1,,2,3" })
                Add("inspector: invalid thickness is rejected: " + value, () =>
                {
                    var before = Editor().Margin; Check.False(Inspector().TryEdit("Margin", value)); Check.Equal(before, Editor().Margin);
                });
            Add("inspector: fractional dimensions use lossless displayed round trips", () =>
            {
                var editor = Editor(); editor.Width = 123.456789123456; editor.Height = 234.56789123456;
                Check.Equal(editor.Width.ToString("R", CultureInfo.InvariantCulture), Field<TextBox>("Width").Text);
                Check.Equal(editor.Height.ToString("R", CultureInfo.InvariantCulture), Field<TextBox>("Height").Text);
            });
            AddAsync("inspector: focus and blur do not rewrite an unchanged fractional number", async () =>
            {
                var editor = Editor(); editor.FontSize = 13.123456789; var before = editor.FontSize; var writes = 0;
                var token = editor.RegisterPropertyChangedCallback(Control.FontSizeProperty, (_, _) => writes++);
                try
                {
                    Inspector().Filter("FontSize"); await Settle();
                    Check.True(Field<TextBox>("FontSize").Focus(FocusState.Programmatic));
                    Check.True(Search().Focus(FocusState.Programmatic)); await Settle();
                    Check.Equal(before, editor.FontSize); Check.Equal(0, writes); Check.True(Inspector().LastError == null);
                }
                finally { editor.UnregisterPropertyChangedCallback(Control.FontSizeProperty, token); }
            });
            Add("inspector: unchanged brush sentinel preserves a non-solid brush", () =>
            {
                var brush = new LinearGradientBrush(); Editor().Background = brush;
                Check.Equal("(brush)", Field<TextBox>("Background").Text);
                Check.True(Inspector().TryEdit("Background", "(brush)")); Check.Same(brush, Editor().Background);
            });
            Add("inspector: unchanged solid color retains brush identity", () =>
            {
                var brush = new SolidColorBrush(Microsoft.UI.Colors.Teal); Editor().Background = brush;
                Check.True(Inspector().TryEdit("Background", Field<TextBox>("Background").Text)); Check.Same(brush, Editor().Background);
            });
            Add("inspector: malformed color does not replace the original brush", () =>
            {
                var before = Editor().Background; Check.False(Inspector().TryEdit("Background", "##ffffff")); Check.Same(before, Editor().Background);
                Check.True(Inspector().TryEdit("Background", "#80332211")); Check.Equal((byte)128, ((SolidColorBrush)Editor().Background).Color.A);
            });
            AddAsync("inspector: obsolete checkbox cannot write to a former selected document", async () =>
            {
                var oldDocument = Document(); var stale = Field<CheckBox>("CanFloat");
                var next = page.Dock.Layout.Descendents().OfType<LayoutDocument>().First(d => d.ContentId == "document1");
                next.IsActive = true; await Wait(() => Inspector().SelectedContentId == "document1");
                stale.IsChecked = false;
                Check.True(oldDocument.CanFloat); Check.True(next.CanFloat);
            });
            AddAsync("inspector: content replacement invalidates obsolete editor callbacks", async () =>
            {
                var original = Editor(); var stale = Field<CheckBox>("IsReadOnly"); var replacement = new TextBox();
                Document().Content = replacement; await Settle(); stale.IsChecked = true;
                Check.False(original.IsReadOnly); Check.False(replacement.IsReadOnly);
            });
            AddAsync("inspector: unload and replacement invalidate native row events", async () =>
            {
                var old = Document(); var stale = Field<CheckBox>("CanFloat");
                page.SwitchSample(SampleKind.Workspace); await Settle(); stale.IsChecked = false; Check.True(old.CanFloat);
            });
            AddAsync("inspector: a dirty draft survives unrelated model notifications", async () =>
            {
                Inspector().Filter("FontSize"); await Settle(); var input = Field<TextBox>("FontSize");
                input.Focus(FocusState.Programmatic); input.Text = "27.75"; Document().Title = "Live title"; await Settle();
                Check.Equal("27.75", input.Text); Check.Equal(12d, Editor().FontSize);
                Search().Focus(FocusState.Programmatic); await Settle(); Check.Equal(27.75, Editor().FontSize);
            });
            AddAsync("inspector: competing application edits are not overwritten by draft blur", async () =>
            {
                Inspector().Filter("FontSize"); await Settle(); var input = Field<TextBox>("FontSize");
                input.Focus(FocusState.Programmatic); input.Text = "27.75"; Editor().FontSize = 21.5;
                Check.Equal("27.75", input.Text); Search().Focus(FocusState.Programmatic); await Settle();
                Check.Equal(21.5, Editor().FontSize); Check.True(Inspector().LastError?.Contains("outside", StringComparison.Ordinal) == true);
            });
            AddAsync("inspector: root replacement in a write callback cannot publish stale errors", async () =>
            {
                var old = Document(); var stale = Field<CheckBox>("CanFloat");
                PropertyChangedEventHandler callback = (_, e) => { if (e.PropertyName == "Title") page.SwitchSample(SampleKind.Workspace); };
                old.PropertyChanged += callback;
                try { Check.False(Inspector().TryEdit("Title", "Replace from callback")); }
                finally { old.PropertyChanged -= callback; }
                await Settle(); stale.IsChecked = false; Check.True(old.CanFloat);
                Check.Equal(SampleKind.Workspace, page.CurrentSample);
            });
            AddAsync("inspector: content callback redirection retains the replacement editor", async () =>
            {
                var old = Editor(); var document = Document(); var next = new TextBox { FontSize = 19 };
                var token = old.RegisterPropertyChangedCallback(Control.FontSizeProperty, (_, _) => document.Content = next);
                try { Check.False(Inspector().TryEdit("FontSize", "20")); }
                finally { old.UnregisterPropertyChangedCallback(Control.FontSizeProperty, token); }
                await Settle(); Check.Same(next, document.Content); Check.Equal("19", Field<TextBox>("FontSize").Text);
            });
            Add("inspector: throwing application setter observer does not poison the next edit", () =>
            {
                PropertyChangedEventHandler callback = (_, e) => { if (e.PropertyName == "Title") throw new InvalidOperationException("Application rejected update"); };
                var document = Document(); document.PropertyChanged += callback;
                try { Check.False(Inspector().TryEdit("Title", "Rejected")); }
                finally { document.PropertyChanged -= callback; }
                Check.True(Inspector().TryEdit("Title", "Recovered")); Check.Equal("Recovered", document.Title); Check.True(Inspector().LastError == null);
            });
            Add("inspector: collapse search and ordering retain the same editor instances", () =>
            {
                var inspector = Inspector(); var input = Field<TextBox>("FontSize"); var count = inspector.VisibleFieldCount;
                inspector.ToggleCategory("Appearance"); Check.True(inspector.VisibleFieldCount < count);
                inspector.Filter("FontSize"); Check.Equal(1, inspector.VisibleFieldCount); Check.Same(input, Field<TextBox>("FontSize"));
                inspector.Filter(""); Check.True(inspector.VisibleFieldCount < count);
                inspector.SetOrder(true); Check.Equal(count, inspector.VisibleFieldCount); Check.Same(input, Field<TextBox>("FontSize"));
                inspector.SetOrder(false); Check.True(inspector.VisibleFieldCount < count);
                inspector.ToggleCategory("Appearance"); Check.Equal(count, inspector.VisibleFieldCount);
            });
            Add("inspector: column resizing retains editor identity and validates bounds", () =>
            {
                var inspector = Inspector(); var input = Field<TextBox>("FontSize"); inspector.NameColumnWidth = 120;
                Check.Equal(120d, inspector.NameColumnWidth); Check.Same(input, Field<TextBox>("FontSize"));
                Check.Throws<ArgumentOutOfRangeException>(() => inspector.NameColumnWidth = double.NaN);
                Check.Throws<ArgumentOutOfRangeException>(() => inspector.NameColumnWidth = 1);
            });
            AddAsync("inspector: field focus supplies public description and accessible help", async () =>
            {
                Inspector().Filter("FontSize"); await Settle(); var field = Field<TextBox>("FontSize"); Check.True(field.Focus(FocusState.Programmatic));
                await Wait(() => Inspector().SelectedPropertyName == "FontSize");
                Check.Equal("FontSize", Inspector().SelectedPropertyName); Check.True(AutomationProperties.GetHelpText(field).Contains("DIPs"));
            });
            Add("sample: programmatic theme changes keep the visible selector in sync", () =>
            {
                var picker = page.FindVisualChildren<ComboBox>().Single(e => AutomationProperties.GetAutomationId(e) == "ThemeSelector");
                page.SetSampleTheme(SampleTheme.Dark); Check.Equal(2, picker.SelectedIndex);
                page.SetSampleTheme(SampleTheme.Light); Check.Equal(1, picker.SelectedIndex);
                page.SetSampleTheme(SampleTheme.Generic); Check.Equal(0, picker.SelectedIndex);
            });
            Add("inspector: read-only identity and unknown properties never write", () =>
            {
                Check.False(Inspector().TryEdit("ContentId", "changed")); Check.False(Inspector().TryEdit("UnknownProperty", "anything")); Check.Equal("document2", Document().ContentId);
            });
            AddAsync("inspector: float dock and activation preserve retained editor registrations", async () =>
            {
                page.Dock.FloatingWindowMode = FloatingWindowMode.InSurface; var editor = Editor();
                var input = Field<TextBox>("FontSize"); Document().Float(); await Settle();
                Document().Dock(); await Settle(); Check.Same(editor, Editor()); Check.Same(input, Field<TextBox>("FontSize"));
                Editor().FontSize = 17.5; Check.Equal("17.5", input.Text);
            });
            foreach (var scene in new[] { "classic", "dark", "rtl", "filtered", "invalid", "narrow" })
                AddAsync("inspector: capture typed property editors: " + scene, async () =>
                {
                    page.SetSampleTheme(scene == "dark" ? SampleTheme.Dark : SampleTheme.Generic);
                    if (scene == "rtl") page.Dock.FlowDirection = FlowDirection.RightToLeft;
                    if (scene == "filtered") Inspector().Filter("Appearance");
                    if (scene == "invalid") Inspector().TryEdit("FontSize", "NaN");
                    if (scene == "narrow") page.Width = 640;
                    await Settle();
                    await VisualCapture.Save(page, Path.Combine(output, "visuals", "inspector-" + scene + ".png"));
                });
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                AddAsync("XTEST: native checkbox click edits a real document capability", async () =>
                {
                    Inspector().Filter("CanFloat"); await Settle(); using var input = new X11TestInput();
                    var field = Field<CheckBox>("CanFloat"); input.MoveTo(field, new(10, field.ActualHeight / 2));
                    input.Press(); await Task.Delay(30); input.Release(); await Wait(() => !Document().CanFloat);
                });
                AddAsync("XTEST: Escape cancels a conflicting property draft before focus leaves", async () =>
                {
                    Inspector().Filter("FontSize"); await Settle(); var field = Field<TextBox>("FontSize"); field.Focus(FocusState.Programmatic);
                    field.Text = "28"; Editor().FontSize = 19;
                    using var input = new X11TestInput(); input.Escape(); await Wait(() => field.Text == "19");
                    Search().Focus(FocusState.Programmatic); await Settle(); Check.Equal(19d, Editor().FontSize); Check.True(Inspector().LastError == null);
                });
            }
            return await tests.Run(output, "inspector-quality");

            SamplePropertyInspector Inspector() => page.PropertyInspector ?? throw new InvalidOperationException("Missing inspector");
            LayoutDocument Document() => page.Dock.Layout.Descendents().OfType<LayoutDocument>().Single(d => d.ContentId == "document2");
            TextBox Editor() => (TextBox)Document().Content!;
            T Field<T>(string name) where T : FrameworkElement => Inspector().FindVisualChildren<T>().Single(e => AutomationProperties.GetAutomationId(e) == "Property-" + name);
            TextBox Search() => Inspector().FindVisualChildren<TextBox>().Single(e => AutomationProperties.GetAutomationId(e) == "PropertySearch");
            void Add(string name, Action action) => AddAsync(name, () => { action(); return Task.CompletedTask; });
            void AddAsync(string name, Func<Task> action) => tests.Test(name, async () =>
            {
                page.Width = 1000; page.SwitchSample(SampleKind.Classic); page.SetSampleTheme(SampleTheme.Generic);
                page.Dock.FlowDirection = FlowDirection.LeftToRight; Document().IsActive = true;
                await Wait(() => Inspector().SelectedContentId == "document2" && Inspector().VisibleFieldCount > 0);
                await Settle(); await action();
            });
            async Task Settle() { page.Dock.Refresh(); page.UpdateLayout(); await Task.Delay(50); page.UpdateLayout(); }
        }
        finally { window.Content = null; window.Close(); }
    }
    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(20);
        Check.True(condition(), "Inspector state did not converge in the bounded wait.");
    }
}
