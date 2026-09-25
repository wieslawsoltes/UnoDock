using System.Collections;
using UnoDock.Layout;

namespace UnoDock.Testing;

public static class SourceIdentityTests
{
    public static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var tools in new[]
        {
            false,
            true
        }

        )
        {
            var prefix = "source identity (" + (tools ? "tools" : "documents") + "): ";
            tests.Test(prefix + "tracking removal never calls payload Equals or GetHashCode", () =>
            {
                using var manager = new DockingManager();
                var keep = new EqualityTrap();
                var remove = new EqualityTrap();
                Set(manager, new[] { keep, remove }, tools);
                var model = Models(manager, tools).Single(m => ReferenceEquals(m.Content, keep));
                Set(manager, new[] { keep }, tools);
                Check.Equal(1, Models(manager, tools).Length);
                Check.Same(model, Models(manager, tools).Single());
                Set(manager, null, tools);
                Check.Equal(0, Models(manager, tools).Length);
            });
            foreach (var keepDirect in new[]
            {
                false,
                true
            }

            )
                tests.Test(prefix + "removing one model/payload alias preserves the remaining owner: " + keepDirect, () =>
                {
                    using var manager = new DockingManager();
                    var payload = new EqualityTrap();
                    LayoutContent model = tools ? new LayoutAnchorable
                    {
                        Content = payload
                    }

                    : new LayoutDocument
                    {
                        Content = payload
                    };
                    if (model is LayoutDocument document)
                        manager.Layout.RootPanel.Children.Add(new LayoutDocumentPane(document));
                    else
                        manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane((LayoutAnchorable)model));
                    var parent = model.Parent;
                    Set(manager, new object[] { payload, model }, tools);
                    Check.Equal(1, Models(manager, tools).Length);
                    Set(manager, new[] { keepDirect ? (object)model : payload }, tools);
                    Check.Same(parent, model.Parent);
                    Check.Same(model, Models(manager, tools).Single());
                    Set(manager, null, tools);
                    Check.True(model.Parent == null);
                });
            tests.Test(prefix + "strategy replacement cannot receive the previous strategy's AfterInsert", () =>
            {
                using var manager = new DockingManager();
                var oldAfter = 0;
                var newAfter = 0;
                var next = new Strategy
                {
                    After = () => newAfter++
                };
                manager.LayoutUpdateStrategy = new Strategy
                {
                    Before = _ =>
                    {
                        manager.LayoutUpdateStrategy = next;
                        return false;
                    },
                    After = () => oldAfter++
                };
                Set(manager, new[] { new object() }, tools);
                Check.Equal(1, Models(manager, tools).Length);
                Check.Equal(0, oldAfter);
                Check.Equal(1, newAfter);
            });
            tests.Test(prefix + "removed default destination is retried without losing source content", () =>
            {
                using var manager = new DockingManager();
                var before = 0;
                var after = 0;
                manager.LayoutUpdateStrategy = new Strategy
                {
                    Before = target =>
                    {
                        if (before++ == 0 && target is LayoutElement element)
                            element.Parent?.RemoveChild(element);
                        return false;
                    },
                    After = () => after++
                };
                var value = new object();
                Set(manager, new[] { value }, tools);
                Check.Equal(2, before);
                Check.Equal(1, after);
                Check.Same(value, Models(manager, tools).Single().Content);
            });
            tests.Test(prefix + "replacement during GetEnumerator never evaluates stale Current", () =>
            {
                using var manager = new DockingManager();
                var current = new object();
                Set(manager, new EnumeratorReplacement(() => Set(manager, new[] { current }, tools)), tools);
                Check.Same(current, Models(manager, tools).Single().Content);
            });
        }

        return tests.Run(output, "source-identity");
    }

    private static void Set(DockingManager manager, IEnumerable? source, bool tools)
    {
        if (tools)
            manager.AnchorablesSource = source!;
        else
            manager.DocumentsSource = source!;
    }

    private static LayoutContent[] Models(DockingManager manager, bool tools) => manager.Layout.Descendents().OfType<LayoutContent>().Where(m => tools ? m is LayoutAnchorable : m is LayoutDocument).ToArray();
    private sealed class EqualityTrap
    {
        public override bool Equals(object? obj) => throw new ApplicationException("Payload equality must not be invoked by identity tracking.");
        public override int GetHashCode() => throw new ApplicationException("Payload hashing must not be invoked by identity tracking.");
        public override string ToString() => "Opaque content";
    }

    private sealed class EnumeratorReplacement(Action replace) : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            replace();
            return new NeverRead();
        }

        private sealed class NeverRead : IEnumerator, IDisposable
        {
            public object Current => throw new ApplicationException("The superseded iterator was read.");

            public bool MoveNext() => throw new ApplicationException("The superseded iterator was advanced.");
            public void Reset() => throw new NotSupportedException();
            public void Dispose()
            {
            }
        }
    }

    private sealed class Strategy : ILayoutUpdateStrategy
    {
        internal Func<ILayoutContainer, bool>? Before;
        internal Action? After;
        public bool BeforeInsertDocument(LayoutRoot root, LayoutDocument document, ILayoutContainer target) => Before?.Invoke(target) ?? false;
        public void AfterInsertDocument(LayoutRoot root, LayoutDocument document) => After?.Invoke();
        public bool BeforeInsertAnchorable(LayoutRoot root, LayoutAnchorable anchorable, ILayoutContainer target) => Before?.Invoke(target) ?? false;
        public void AfterInsertAnchorable(LayoutRoot root, LayoutAnchorable anchorable) => After?.Invoke();
    }
}
