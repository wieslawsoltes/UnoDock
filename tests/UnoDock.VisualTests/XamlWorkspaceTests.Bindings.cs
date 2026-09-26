using System.Reflection;
using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock.Testing;

internal static partial class XamlWorkspaceTests
{
    private static void AddBindingAndPaletteTests(TestRunner tests, string output)
    {
        tests.Test("XAML item binding: a shared definition has independent per-item binding expressions", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var models = host.Manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
            var first = host.Manager.GetLayoutItemFromModel(models[0]);
            var second = host.Manager.GetLayoutItemFromModel(models[1]);
            Check.Same(LayoutItemBindings.GetTitle(first), LayoutItemBindings.GetTitle(second));
            Check.False(ReferenceEquals(first.GetBindingExpression(LayoutItem.TitleProperty)!.ParentBinding, second.GetBindingExpression(LayoutItem.TitleProperty)!.ParentBinding));
            first.Title = "Only first changes";
            await Wait(() => host.Page.ViewModel.Documents[0].Title == first.Title);
            Check.False(host.Page.ViewModel.Documents[1].Title == first.Title);
        });
        tests.Test("XAML item binding: explicit local values survive style replacement", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var model = host.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
            var adapter = host.Manager.GetLayoutItemFromModel(model);
            adapter.ClearValue(LayoutItem.TitleProperty);
            adapter.Title = "Local value";
            var authored = host.Manager.LayoutItemContainerStyle;
            host.Manager.LayoutItemContainerStyle = new Style(typeof(LayoutItem));
            host.Manager.LayoutItemContainerStyle = authored;
            host.Page.ViewModel.Documents[0].Title = "Source must not replace local";
            Check.Equal("Local value", adapter.Title);
            Check.Equal("Local value", model.Title);
        });
        tests.Test("XAML item binding: removing a style restores defaults and releases old source updates", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var model = host.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
            var adapter = host.Manager.GetLayoutItemFromModel(model);
            host.Manager.LayoutItemContainerStyle = null;
            model.Title = "Layout-owned title";
            await Wait(() => adapter.Title == model.Title);
            host.Page.ViewModel.Documents[0].Title = "Detached binding source";
            Check.Equal("Layout-owned title", model.Title);
        });
        tests.Test("XAML item style: BasedOn scalar values are synchronized only after final style resolution", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var parent = new Style(typeof(LayoutItem));
            parent.Setters.Add(new Setter(LayoutItem.TitleProperty, "Base title"));
            parent.Setters.Add(new Setter(LayoutItem.CanFloatProperty, false));
            var derived = new Style(typeof(LayoutItem))
            {
                BasedOn = parent
            };
            derived.Setters.Add(new Setter(LayoutItem.TitleProperty, "Derived title"));
            host.Manager.LayoutItemContainerStyle = derived;
            foreach (var model in host.Manager.Layout.Descendents().OfType<LayoutContent>())
            {
                Check.Equal("Derived title", model.Title);
                Check.False(model.CanFloat);
            }
        });
        tests.Test("XAML item binding: replacement content rebinds to the new application object", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var model = host.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
            var old = (XamlWorkspaceItem)model.Content!;
            // Withdraw source ownership before explicitly replacing the payload.
            host.Manager.DocumentsSource = null;
            host.Manager.Layout.RootPanel.Children.Add(new LayoutDocumentPane(model));
            var adapter = host.Manager.GetLayoutItemFromModel(model);
            var replacement = new XamlWorkspaceItem
            {
                Title = "Replacement source",
                ContentId = "replacement-id"
            };
            model.Content = replacement;
            await Wait(() => model.Title == replacement.Title && model.ContentId == replacement.ContentId);
            old.Title = "Obsolete source";
            Check.Equal("Replacement source", adapter.Title);
            replacement.Title = "Replacement changed";
            await Wait(() => adapter.Title == replacement.Title);
        });
        tests.Test("XAML item binding: disposed adapters release per-item bindings", async () =>
        {
            using var host = Mvvm();
            await host.Show();
            var model = host.Manager.Layout.Descendents().OfType<LayoutDocument>().First();
            var adapter = host.Manager.GetLayoutItemFromModel(model);
            host.Manager.Dispose();
            Check.True(adapter.GetBindingExpression(LayoutItem.TitleProperty) == null);
            host.Page.ViewModel.Documents[0].Title = "After dispose";
            Check.False(model.Title == "After dispose");
        });
        tests.Test("theme: null native resource dictionary cannot invalidate a reusable theme", () =>
        {
            var theme = new ResourceDictionaryTheme();
            var resources = theme.Resources;
            Check.Throws<ArgumentNullException>(() => theme.SetValue(ResourceDictionaryTheme.ResourcesProperty, null));
            Check.Same(resources, theme.Resources);
            theme.Resources = new ResourceDictionary();
            theme.Refresh();
        });
        tests.Test("theme: invalid native floating mode is rejected before host transitions", () =>
        {
            using var manager = new DockingManager
            {
                FloatingWindowMode = FloatingWindowMode.InSurface
            };
            Check.Throws<ArgumentOutOfRangeException>(() => manager.SetValue(DockingManager.FloatingWindowModeProperty, (FloatingWindowMode)127));
            Check.Equal(FloatingWindowMode.InSurface, manager.FloatingWindowMode);
        });
        tests.Test("theme: selected HighContrast dictionary has no opposite-theme fallback", () =>
        {
            var dictionary = new ResourceDictionary();
            var contrast = new ResourceDictionary
            {
                ["token"] = "contrast"
            };
            dictionary.ThemeDictionaries["HighContrast"] = contrast;
            dictionary.ThemeDictionaries["Light"] = new ResourceDictionary
            {
                ["token"] = "light",
                ["onlyLight"] = true
            };
            dictionary.ThemeDictionaries["Default"] = new ResourceDictionary
            {
                ["onlyLight"] = true
            };
            var find = typeof(DockingManager).Assembly.GetType("UnoDock.Internal.DockThemeResources", true)!.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(method => method.Name == "Find" && method.GetParameters().Length == 5);
            object? Resolve(string key) => find.Invoke(null, [dictionary, key, "HighContrast", null, new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance)]);
            Check.Equal("contrast", Resolve("token"));
            Check.True(Resolve("onlyLight") == null);
        });
        foreach (var requested in new[]
        {
            ElementTheme.Light,
            ElementTheme.Dark
        }

        )
            tests.Test("compiled XAML: inline ResourceDictionaryTheme and live palette switching: " + requested, async () =>
            {
                using var host = Templates();
                host.Page.RequestedTheme = requested;
                await host.Show();
                var model = host.Manager.Layout.Descendents().OfType<LayoutDocument>().Single();
                var view = host.Manager.GetLayoutItemFromModel(model).View;
                host.Page.TogglePalette();
                Check.True(host.Manager.Theme is ResourceDictionaryTheme);
                var theme = (ResourceDictionaryTheme)host.Manager.Theme!;
                var selected = (ResourceDictionary)theme.Resources.ThemeDictionaries[requested.ToString()];
                Check.Same(selected["UnoDock.AccentBrush"], PaletteProperty<Brush>(host.Manager, "Accent"));
                Check.Equal(34d, Metric(host.Manager, "TabHeight"));
                await Task.Delay(80);
                await VisualCapture.Save(host.Page, System.IO.Path.Combine(output, $"xaml-palette-{requested.ToString().ToLowerInvariant()}.png"));
                host.Page.TogglePalette();
                Check.Same(view, host.Manager.GetLayoutItemFromModel(model).View);
                Check.Same(host.Page.ViewModel.PrimaryItem, model.Content);
            });
    }
}
