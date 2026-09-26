using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class XamlDisposedNotificationTests
{
    internal static void Add(TestRunner tests)
    {
        foreach (var tool in new[]
        {
            false,
            true
        }

        )
        {
            tests.Test("XAML cleanup: " + (tool ? "tool" : "document") + " in-flight model event cannot revive disposed state", () =>
            {
                LayoutItem? item = null;
                LayoutContent model = tool ? new LayoutAnchorable() : new LayoutDocument();
                var original = new XamlDocument
                {
                    Title = "Original payload"
                };
                var replacement = new XamlDocument
                {
                    Title = "Replacement payload"
                };
                model.Content = original;
                var callbacks = 0;
                // This subscriber precedes the adapter's ModelChanged subscriber.
                // Removing the latter during Dispose cannot alter an event delegate
                // snapshot which the CLR has already captured for this delivery.
                model.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(LayoutContent.Content))
                    {
                        callbacks++;
                        item?.Dispose();
                    }
                };
                ILayoutPanelElement pane = tool ? new LayoutAnchorablePane((LayoutAnchorable)model) : new LayoutDocumentPane((LayoutDocument)model);
                using var manager = new DockingManager
                {
                    Layout = new()
                    {
                        RootPanel = new(pane)
                    },
                    FloatingWindowMode = FloatingWindowMode.InSurface
                };
                item = manager.GetLayoutItemFromModel(model);
                var view = item.View;
                LayoutItemBindings.SetBindings(item, new() { new() { Property = "Title", Path = "Title", Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay } });
                model.Content = replacement;
                Check.Equal(1, callbacks);
                Check.True(item.Model == null && item.DataContext == null, "A captured model notification revived a disposed adapter.");
                Check.False(item.IsViewCreated);
                Check.Throws<ObjectDisposedException>(() => _ = item.View);
                Check.True(view.Content == null && view.ContentTemplate == null && view.DataContext == null);
                Check.True(item.GetBindingExpression(LayoutItem.TitleProperty) == null);
                Check.Same(replacement, model.Content);
                replacement.Title = "Payload remains independently usable";
                Check.True(item.Model == null && item.DataContext == null);
                item.Dispose();
            });
        }
    }
}
