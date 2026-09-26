using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class XamlModelBindingTests
{
    internal static void Add(TestRunner tests)
    {
        foreach (var name in new[]
        {
            "Title",
            "ContentId",
            "CanClose",
            "CanFloat",
            "CanMove"
        }

        )
        {
            tests.Test("XAML TwoWay: model " + name + " reaches source through the same expression", () =>
            {
                var source = new XamlDocument();
                var document = new LayoutDocument
                {
                    Content = source
                };
                using var manager = new DockingManager
                {
                    Layout = new()
                    {
                        RootPanel = new(new LayoutDocumentPane(document))
                    }
                };
                var item = manager.GetLayoutItemFromModel(document);
                var path = name == "CanFloat" ? "CanClose" : name;
                LayoutItemBindings.SetBindings(item, new() { new() { Property = name, Path = path, Mode = BindingMode.TwoWay } });
                var property = (DependencyProperty)typeof(LayoutDocumentItem).GetField(name + "Property", System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)!.GetValue(null)!;
                var expression = item.GetBindingExpression(property)?.ParentBinding;
                Check.True(expression != null);
                object updated = name is "Title" or "ContentId" ? "model-originated" : false;
                document.GetType().GetProperty(name)!.SetValue(document, updated);
                Check.Equal(updated, source.GetType().GetProperty(path)!.GetValue(source));
                Check.Equal(updated, item.GetValue(property));
                Check.Same(expression, item.GetBindingExpression(property)?.ParentBinding);
            });
        }

        tests.Test("XAML OneWay: model changes do not write into the declared source", () =>
        {
            var state = new XamlDocument
            {
                Title = "Source owns this"
            };
            var document = new LayoutDocument
            {
                Content = state
            };
            using var manager = new DockingManager
            {
                Layout = new()
                {
                    RootPanel = new(new LayoutDocumentPane(document))
                }
            };
            var item = manager.GetLayoutItemFromModel(document);
            LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title", Mode = BindingMode.OneWay } });
            var expression = item.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding;
            document.Title = "Model-only edit";
            Check.Equal("Source owns this", state.Title);
            Check.Same(expression, item.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding);
        });
        tests.Test("XAML TwoWay: application binding replacement withdraws the old model publication", () =>
        {
            var old = new XamlDocument
            {
                Title = "Old"
            };
            var current = new XamlDocument
            {
                Title = "Application owned"
            };
            var document = new LayoutDocument
            {
                Content = old
            };
            using var manager = new DockingManager
            {
                Layout = new()
                {
                    RootPanel = new(new LayoutDocumentPane(document))
                }
            };
            var item = manager.GetLayoutItemFromModel(document);
            LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title", Mode = BindingMode.TwoWay } });
            var replacement = new Binding
            {
                Path = new("Title"),
                Source = current,
                Mode = BindingMode.TwoWay
            };
            item.SetBinding(LayoutItem.TitleProperty, replacement);
            document.Title = "Do not push into application binding";
            Check.Equal("Application owned", current.Title);
            Check.Equal("Application owned", item.Title);
            Check.Same(replacement, item.GetBindingExpression(LayoutItem.TitleProperty)?.ParentBinding);
        });
        tests.Test("XAML TwoWay: a source callback can replace definitions during reverse publication", () =>
        {
            var old = new XamlDocument
            {
                Title = "Old"
            };
            var current = new XamlDocument
            {
                Title = "Replacement"
            };
            var document = new LayoutDocument
            {
                Content = old
            };
            using var manager = new DockingManager
            {
                Layout = new()
                {
                    RootPanel = new(new LayoutDocumentPane(document))
                }
            };
            var item = manager.GetLayoutItemFromModel(document);
            LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title", Mode = BindingMode.TwoWay } });
            var once = false;
            old.PropertyChanged += (_, args) =>
            {
                if (once || args.PropertyName != "Title")
                    return;
                once = true;
                LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title", Source = current, Mode = BindingMode.TwoWay } });
            };
            document.Title = "Reverse write";
            Check.True(once);
            Check.Equal("Replacement", document.Title);
            Check.Equal("Replacement", item.Title);
            old.Title = "Obsolete source";
            Check.Equal("Replacement", document.Title);
            document.Title = "New reverse write";
            Check.Equal("New reverse write", current.Title);
        });
    }
}
