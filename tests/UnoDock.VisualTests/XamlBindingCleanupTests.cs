using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class XamlBindingCleanupTests
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
            var category = tool ? "tool" : "document";
            foreach (var stage in new[]
            {
                "definition",
                "default",
                "command"
            }

            )
            {
                tests.Test($"XAML cleanup: {category} disposal completes after {stage} observer failure", () =>
                {
                    using var fixture = new Fixture(tool);
                    if (stage == "definition")
                    {
                        fixture.Bind(fixture.Source);
                    }

                    var item = fixture.Item;
                    var view = item.View;
                    var command = item.FloatCommand!;
                    var failure = new InvalidOperationException("expected " + stage + " cleanup failure");
                    var property = stage == "command" ? LayoutItem.CloseCommandProperty : LayoutItem.TitleProperty;
                    var calls = 0;
                    var token = item.RegisterPropertyChangedCallback(property, (_, _) =>
                    {
                        calls++;
                        throw failure;
                    });
                    Exception? error;
                    try
                    {
                        error = Capture(item.Dispose);
                    }
                    finally
                    {
                        item.UnregisterPropertyChangedCallback(property, token);
                    }

                    Check.Same(failure, error);
                    Check.Equal(1, calls);
                    AssertDisposed(fixture, view);
                    Check.False(command.CanExecute(null));
                    fixture.Source.Title = "Old source changed after disposal";
                    Check.True(item.Title == null && item.ContentId == null);
                    item.Dispose();
                });
            }

            tests.Test($"XAML cleanup: {category} aggregates binding failures in delivery order", () =>
            {
                using var fixture = new Fixture(tool);
                fixture.Bind(fixture.Source);
                var item = fixture.Item;
                var view = item.View;
                var first = new InvalidOperationException("title cleanup");
                var second = new InvalidOperationException("identity cleanup");
                var firstToken = item.RegisterPropertyChangedCallback(LayoutItem.TitleProperty, (_, _) => throw first);
                var secondToken = item.RegisterPropertyChangedCallback(LayoutItem.ContentIdProperty, (_, _) => throw second);
                Exception? error;
                try
                {
                    error = Capture(item.Dispose);
                }
                finally
                {
                    item.UnregisterPropertyChangedCallback(LayoutItem.TitleProperty, firstToken);
                    item.UnregisterPropertyChangedCallback(LayoutItem.ContentIdProperty, secondToken);
                }

                Check.True(error is AggregateException);
                var failures = ((AggregateException)error!).InnerExceptions;
                Check.Equal(2, failures.Count);
                Check.Same(first, failures[0]);
                Check.Same(second, failures[1]);
                AssertDisposed(fixture, view);
            });
            tests.Test($"XAML cleanup: {category} failed replacement retires all old source bindings", () =>
            {
                using var fixture = new Fixture(tool);
                fixture.Bind(fixture.Source);
                var item = fixture.Item;
                var title = fixture.Model.Title;
                var id = fixture.Model.ContentId;
                var next = new XamlDocument
                {
                    Title = "Replacement source"
                };
                var failure = new InvalidOperationException("replace cleanup");
                var token = item.RegisterPropertyChangedCallback(LayoutItem.TitleProperty, (_, _) => throw failure);
                Exception? error;
                try
                {
                    error = Capture(() => fixture.Bind(next));
                }
                finally
                {
                    item.UnregisterPropertyChangedCallback(LayoutItem.TitleProperty, token);
                }

                Check.Same(failure, error);
                Check.True(item.GetBindingExpression(LayoutItem.TitleProperty) == null);
                Check.True(item.GetBindingExpression(LayoutItem.ContentIdProperty) == null);
                Check.Equal(title, fixture.Model.Title);
                Check.Equal(id, fixture.Model.ContentId);
                fixture.Source.Title = "Obsolete source notification";
                Check.Equal(title, fixture.Model.Title);
                Check.Equal(id, fixture.Model.ContentId);
                fixture.Bind(next);
                Check.Equal(next.Title, item.Title);
                Check.Equal(next.Title, fixture.Model.ContentId);
                fixture.Model.Title = "Recovered reverse binding";
                Check.Equal(fixture.Model.Title, next.Title);
            });
            tests.Test($"XAML cleanup: {category} a callback-installed application binding survives retirement", () =>
            {
                using var fixture = new Fixture(tool);
                fixture.Bind(fixture.Source);
                var item = fixture.Item;
                var application = new XamlDocument
                {
                    Title = "Application-owned identity"
                };
                var binding = new Binding
                {
                    Source = application,
                    Path = new("Title"),
                    Mode = BindingMode.OneWay
                };
                var failure = new InvalidOperationException("replacement callback");
                var observed = false;
                var token = item.RegisterPropertyChangedCallback(LayoutItem.TitleProperty, (_, _) =>
                {
                    if (observed)
                    {
                        return;
                    }

                    observed = true;
                    item.SetBinding(LayoutItem.ContentIdProperty, binding);
                    throw failure;
                });
                Exception? error;
                try
                {
                    error = Capture(() => fixture.Bind(new XamlDocument()));
                }
                finally
                {
                    item.UnregisterPropertyChangedCallback(LayoutItem.TitleProperty, token);
                }

                Check.Same(failure, error);
                Check.True(observed);
                Check.Same(binding, item.GetBindingExpression(LayoutItem.ContentIdProperty)?.ParentBinding);
                application.Title = "Application binding is still live";
                Check.Equal(application.Title, item.ContentId);
                fixture.Bind(new XamlDocument { Title = "Independent next request" });
                Check.Same(binding, item.GetBindingExpression(LayoutItem.ContentIdProperty)?.ParentBinding);
                Check.Equal(application.Title, item.ContentId);
            });
            tests.Test($"XAML cleanup: {category} disposal during replacement cannot install a successor binding", () =>
            {
                using var fixture = new Fixture(tool);
                fixture.Bind(fixture.Source);
                var item = fixture.Item;
                var view = item.View;
                var token = item.RegisterPropertyChangedCallback(LayoutItem.TitleProperty, (_, _) => item.Dispose());
                try
                {
                    fixture.Bind(new XamlDocument { Title = "Must never become attached" });
                }
                finally
                {
                    item.UnregisterPropertyChangedCallback(LayoutItem.TitleProperty, token);
                }

                AssertDisposed(fixture, view);
                Check.True(item.GetBindingExpression(LayoutItem.ContentIdProperty) == null);
            });
        }
    }

    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static void AssertDisposed(Fixture fixture, ContentPresenter view)
    {
        var item = fixture.Item;
        Check.True(item.Model == null && item.DataContext == null);
        Check.False(item.IsViewCreated);
        Check.Throws<ObjectDisposedException>(() => _ = item.View);
        Check.True(view.Content == null && view.ContentTemplate == null && view.DataContext == null);
        foreach (var property in new[]
        {
            LayoutItem.TitleProperty,
            LayoutItem.ContentIdProperty,
            LayoutItem.CanCloseProperty,
            LayoutItem.CanFloatProperty
        }

        )
        {
            Check.True(item.GetBindingExpression(property) == null, "A retired adapter retained an owned binding.");
        }

        Check.True(item.CloseCommand == null && item.FloatCommand == null && item.ActivateCommand == null);
        Check.Same(fixture.Source, fixture.Model.Content);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly XamlDocument Source = new()
        {
            Title = "Source title"
        };
        internal readonly LayoutContent Model;
        internal readonly DockingManager Manager;
        internal readonly LayoutItem Item;
        internal Fixture(bool tool)
        {
            Model = tool ? new LayoutAnchorable() : new LayoutDocument();
            Model.Title = "Model title";
            Model.ContentId = "model-id";
            Model.Content = Source;
            ILayoutPanelElement pane = tool ? new LayoutAnchorablePane((LayoutAnchorable)Model) : new LayoutDocumentPane((LayoutDocument)Model);
            Manager = new DockingManager
            {
                Layout = new()
                {
                    RootPanel = new(pane)
                },
                FloatingWindowMode = FloatingWindowMode.InSurface
            };
            Item = Manager.GetLayoutItemFromModel(Model);
        }

        internal void Bind(XamlDocument source)
        {
            LayoutItemBindings.SetBindings(Item, new() { new() { Property = "Title", Path = "Title", Source = source, Mode = BindingMode.TwoWay }, new() { Property = "ContentId", Path = "Title", Source = source, Mode = BindingMode.TwoWay } });
        }

        public void Dispose() => Manager.Dispose();
    }
}
