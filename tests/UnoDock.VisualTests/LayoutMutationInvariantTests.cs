using System.Collections.Specialized;
using System.ComponentModel;
using UnoDock.Layout;

namespace UnoDock.Testing;

/// <summary>Observe both sides of every ownership relationship inside callbacks,
/// not just after the operation returns. No private implementation is invoked.</summary>
internal static class LayoutMutationInvariantTests
{
    internal static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var operation in new[] { "insert", "remove", "replace", "clear", "slot", "transfer" })
        foreach (var phase in new[] { "changing", "changed", "collection" })
        {
            if (operation == "slot" && phase == "collection") continue;
            tests.Test($"ownership/{operation}/{phase}: exceptions preserve both directions", () =>
            {
                var f = new Fixture();
                var failure = new InvalidOperationException("expected observer failure");
                var observed = 0;
                var owner = operation == "transfer" ? f.Other : f.Pane;
                var watched = operation is "insert" or "transfer" ? f.Extra : f.A;
                Action mutate = operation switch
                {
                    "insert" => () => f.Pane.Children.Add(f.Extra),
                    "remove" => () => f.Pane.Children.Remove(f.A),
                    "replace" => () => f.Pane.Children[0] = f.Extra,
                    "clear" => () => f.Pane.Children.Clear(),
                    "slot" => () => f.Window.RootDocument = f.Extra,
                    _ => () => f.Other.Children.Add(f.Extra)
                };
                if (operation == "slot")
                {
                    f.Pane.Children.Remove(f.A);
                    f.Window.RootDocument = f.A;
                }
                if (operation == "transfer") f.Pane.Children.Add(f.Extra);
                void Observe()
                {
                    f.AssertTree();
                    observed++;
                    throw failure;
                }
                PropertyChangingEventHandler changing = (_, e) => { if (e.PropertyName == "Parent") Observe(); };
                PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == "Parent") Observe(); };
                NotifyCollectionChangedEventHandler collection = (_, _) => Observe();
                if (phase == "changing") watched.PropertyChanging += changing;
                if (phase == "changed") watched.PropertyChanged += changed;
                if (phase == "collection") owner.Children.CollectionChanged += collection;
                try
                {
                    var error = Capture(mutate);
                    Check.True(Contains(error, failure), "The observer exception was lost.");
                    Check.True(observed > 0);
                }
                finally
                {
                    watched.PropertyChanging -= changing;
                    watched.PropertyChanged -= changed;
                    owner.Children.CollectionChanged -= collection;
                }
                f.AssertTree();
                // The same collection and update batch remain usable after failure.
                var recovery = new LayoutDocument { Title = "Recovery" };
                f.Other.Children.Add(recovery);
                Check.Same(f.Other, recovery.Parent);
                f.Other.Children.Remove(recovery);
                Check.True(recovery.Parent == null);
            });
        }
        foreach (var property in new[] { "Count", "Item[]" })
            tests.Test("ownership: collection property exception still completes publication/selection: " + property, () =>
            {
                var f = new Fixture(); var failure = new InvalidOperationException("collection property"); var collectionEvents = 0;
                PropertyChangedEventHandler handler = (_, e) => { f.AssertTree(); if (e.PropertyName == property) throw failure; };
                ((INotifyPropertyChanged)f.Pane.Children).PropertyChanged += handler;
                f.Pane.Children.CollectionChanged += (_, _) => collectionEvents++;
                var error = Capture(() => f.Pane.Children.Remove(f.A));
                Check.True(Contains(error, failure));
                Check.Equal(1, collectionEvents); Check.Same(f.B, f.Pane.SelectedContent); f.AssertTree();
            });
        foreach (var name in new[] { "added", "removed" })
            tests.Test("ownership: throwing root " + name + " observer still invalidates", () =>
            {
                var f = new Fixture(); var failure = new InvalidOperationException("root event"); var updates = 0;
                f.Root.Updated += (_, _) => { updates++; f.AssertTree(); };
                EventHandler<LayoutElementEventArgs> handler = (_, _) => throw failure;
                if (name == "added") f.Root.ElementAdded += handler; else f.Root.ElementRemoved += handler;
                var error = Capture(name == "added" ? () => f.Pane.Children.Add(f.Extra) : () => f.Pane.Children.Remove(f.A));
                Check.True(Contains(error, failure)); Check.True(updates > 0); f.AssertTree();
            });
        foreach (var timing in new[] { "changing", "changed" })
        foreach (var restore in new[] { false, true })
            tests.Test($"ownership: reentrant transfer {timing}, ABA={restore}", () =>
            {
                var f = new Fixture(); var once = false;
                void Transfer(string? name)
                {
                    if (once || name != "Parent") return;
                    once = true; f.Other.Children.Add(f.Extra);
                    if (restore) f.Other.Children.Remove(f.Extra);
                    f.AssertTree();
                }
                f.Extra.PropertyChanging += (_, e) => { if (timing == "changing") Transfer(e.PropertyName); };
                f.Extra.PropertyChanged += (_, e) => { if (timing == "changed") Transfer(e.PropertyName); };
                f.Pane.Children.Add(f.Extra);
                Check.True(once); Check.Same(restore ? null : f.Other, f.Extra.Parent); f.AssertTree();
            });
        tests.Test("ownership: reentrant sibling edits cannot redirect a stale removal index", () =>
        {
            var f = new Fixture(); var once = false;
            f.B.PropertyChanging += (_, e) =>
            {
                if (once || e.PropertyName != "Parent") return;
                once = true; f.Pane.Children.Remove(f.A);
            };
            f.Pane.Children.Remove(f.B);
            Check.Same(f.Pane, f.B.Parent); Check.True(f.A.Parent == null); f.AssertTree();
        });
        tests.Test("ownership: invalid move cannot remove a child", () =>
        {
            var f = new Fixture();
            Check.Throws<ArgumentOutOfRangeException>(() => f.Pane.Children.Move(0, f.Pane.Children.Count));
            Check.Equal(2, f.Pane.Children.Count); Check.Same(f.A, f.Pane.Children[0]); f.AssertTree();
        });
        tests.Test("ownership: callback exceptions aggregate in delivery order", () =>
        {
            var f = new Fixture(); var first = new InvalidOperationException("parent"); var second = new InvalidOperationException("collection");
            f.Extra.PropertyChanged += (_, e) => { if (e.PropertyName == "Parent") throw first; };
            f.Pane.Children.CollectionChanged += (_, _) => throw second;
            var error = Capture(() => f.Pane.Children.Add(f.Extra));
            var failures = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : [error];
            Check.Equal(2, failures.Length); Check.Same(first, failures[0]); Check.Same(second, failures[1]); f.AssertTree();
        });
        foreach (var hook in new[] { "parent-changing", "parent-changed", "root-changed" })
            tests.Test("ownership: overridable " + hook + " exception cannot break membership", () =>
            {
                var root = new LayoutRoot(); var pane = new LayoutDocumentPane(); root.RootPanel = new(pane);
                var failure = new InvalidOperationException(hook);
                var document = new ThrowingDocument { Hook = hook, Failure = failure };
                var error = Capture(() => pane.Children.Add(document));
                Check.True(Contains(error, failure));
                Check.Equal(pane.Children.Contains(document), ReferenceEquals(document.Parent, pane));
                foreach (var child in pane.Children) Check.Same(pane, child.Parent);
            });
        foreach (var phase in new[] { "old-changing", "old-property", "old-event", "new-property", "new-selection", "new-timestamp", "new-event", "root-property" })
        foreach (var withdraw in new[] { false, true })
            tests.Test($"activation: latest request wins at {phase}, withdraw={withdraw}", () =>
            {
                var f = new Fixture(); f.Root.ActiveContent = f.A; var once = false;
                void Redirect()
                {
                    if (once) return;
                    once = true; f.AssertActive();
                    f.Root.ActiveContent = f.Extra;
                    if (withdraw) f.Root.ActiveContent = f.B;
                }
                f.Other.Children.Add(f.Extra);
                f.A.PropertyChanging += (_, e) => { if (phase == "old-changing" && e.PropertyName == "IsActive") Redirect(); };
                f.A.PropertyChanged += (_, e) => { if (phase == "old-property" && e.PropertyName == "IsActive") Redirect(); };
                f.A.IsActiveChanged += (_, _) => { if (phase == "old-event") Redirect(); };
                f.B.PropertyChanged += (_, e) =>
                {
                    if (phase == "new-property" && e.PropertyName == "IsActive" || phase == "new-timestamp" && e.PropertyName == "LastActivationTimeStamp") Redirect();
                };
                f.B.IsSelectedChanged += (_, _) => { if (phase == "new-selection" && f.B.IsSelected) Redirect(); };
                f.B.IsActiveChanged += (_, _) => { if (phase == "new-event") Redirect(); };
                f.Root.PropertyChanged += (_, e) => { if (phase == "root-property" && e.PropertyName == "ActiveContent") Redirect(); };
                f.Root.ActiveContent = f.B;
                Check.True(once, "The callback boundary was not exercised.");
                Check.Same(withdraw ? f.B : f.Extra, f.Root.ActiveContent); f.AssertActive(); f.AssertTree();
            });
        foreach (var before in new[] { false, true })
            tests.Test("activation: throwing " + (before ? "changing" : "changed") + " observer preserves one active item", () =>
            {
                var f = new Fixture(); f.Root.ActiveContent = f.A;
                var failure = new InvalidOperationException("active observer");
                PropertyChangingEventHandler changing = (_, e) => { if (before && e.PropertyName == "IsActive") { f.AssertActive(); throw failure; } };
                PropertyChangedEventHandler changed = (_, e) => { if (!before && e.PropertyName == "IsActive") { f.AssertActive(); throw failure; } };
                f.A.PropertyChanging += changing; f.A.PropertyChanged += changed;
                var error = Capture(() => f.Root.ActiveContent = f.B);
                Check.True(Contains(error, failure)); f.AssertActive();
                f.A.PropertyChanging -= changing; f.A.PropertyChanged -= changed;
                f.Root.ActiveContent = f.B; Check.Same(f.B, f.Root.ActiveContent); f.AssertActive();
            });
        tests.Test("activation: every observable phase has matching flags and root pointer", () =>
        {
            var f = new Fixture(); f.Root.ActiveContent = f.A; var observations = 0;
            foreach (var content in new[] { f.A, f.B })
            {
                content.PropertyChanging += (_, _) => { observations++; f.AssertActive(); };
                content.PropertyChanged += (_, _) => { observations++; f.AssertActive(); };
                content.IsActiveChanged += (_, _) => { observations++; f.AssertActive(); };
            }
            f.Root.ActiveContent = f.B;
            Check.True(observations > 5); f.AssertActive();
        });
        tests.Test("activation: removing the target during notification repairs the active pointer", () =>
        {
            var f = new Fixture(); f.Root.ActiveContent = f.A; var once = false;
            f.B.IsActiveChanged += (_, _) => { if (!once && f.B.IsActive) { once = true; f.Pane.Children.Remove(f.B); } };
            f.Root.ActiveContent = f.B;
            Check.True(once); Check.False(f.B.IsActive); Check.Same(f.A, f.Root.ActiveContent); f.AssertActive();
        });
        tests.Test("activation: nonconverging callbacks fail boundedly and leave a reusable root", () =>
        {
            var f = new Fixture(); f.Root.ActiveContent = f.A;
            PropertyChangedEventHandler redirect = (_, e) =>
            {
                if (e.PropertyName == "ActiveContent") f.Root.ActiveContent = ReferenceEquals(f.Root.ActiveContent, f.A) ? f.B : f.A;
            };
            f.Root.PropertyChanged += redirect;
            Check.True(Capture(() => f.Root.ActiveContent = f.B) != null);
            f.Root.PropertyChanged -= redirect; f.AssertActive();
            f.Root.ActiveContent = f.A; Check.Same(f.A, f.Root.ActiveContent); f.AssertActive();
        });
        tests.Test("ownership: deterministic transfers preserve all parent links after callback failures", () =>
        {
            var f = new Fixture(); var random = new Random(147031); var docs = Enumerable.Range(0, 12).Select(i => new LayoutDocument { Title = "D" + i }).ToArray();
            for (var i = 0; i < 250; i++)
            {
                var document = docs[random.Next(docs.Length)]; var destination = random.Next(2) == 0 ? f.Pane : f.Other;
                if (ReferenceEquals(document.Parent, destination)) continue;
                PropertyChangedEventHandler fail = (_, e) => { if (e.PropertyName == "Parent") throw new InvalidOperationException("seeded failure"); };
                var inject = i % 3 == 0;
                if (inject) document.PropertyChanged += fail;
                Capture(() => destination.Children.Add(document));
                document.PropertyChanged -= fail;
                f.AssertTree();
                foreach (var item in docs)
                {
                    var count = f.Pane.Children.Concat(f.Other.Children).Count(c => ReferenceEquals(c, item));
                    Check.Equal(item.Parent == null ? 0 : 1, count);
                }
            }
        });
        return tests.Run(output, "layout-mutation-invariants");
    }

    private static Exception? Capture(Action action)
    {
        try { action(); return null; } catch (Exception error) { return error; }
    }
    private static bool Contains(Exception? error, Exception expected) => ReferenceEquals(error, expected) ||
        error is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(e => ReferenceEquals(e, expected));

    private sealed class Fixture
    {
        internal readonly LayoutRoot Root = new();
        internal readonly LayoutDocument A = new() { Title = "A" }, B = new() { Title = "B" }, Extra = new() { Title = "C" };
        internal readonly LayoutDocumentPane Pane = new(), Other = new();
        internal readonly LayoutDocumentFloatingWindow Window = new();
        internal Fixture()
        {
            Pane.Children.Add(A); Pane.Children.Add(B);
            var panel = new LayoutPanel(Pane); panel.Children.Add(Other);
            Root.RootPanel = panel; Root.FloatingWindows.Add(Window);
        }
        internal void AssertTree()
        {
            var seen = new HashSet<ILayoutElement>(ReferenceEqualityComparer.Instance);
            Visit(Root);
            foreach (var content in new[] { A, B, Extra })
            {
                if (content.Parent is { } parent) Check.True(parent.Children.Any(c => ReferenceEquals(c, content)), "Parent points to a container which does not list the child.");
                Check.Equal(content.Parent != null, seen.Contains(content));
            }
            void Visit(ILayoutElement node)
            {
                Check.True(seen.Add(node), "Duplicate or cyclic ownership.");
                if (node is not ILayoutContainer container) return;
                foreach (var child in container.Children.ToArray())
                {
                    Check.Same(container, child.Parent); Visit(child);
                }
            }
        }
        internal void AssertActive()
        {
            var contents = Root.Descendents().OfType<LayoutContent>().ToArray();
            foreach (var content in contents) Check.Equal(ReferenceEquals(content, Root.ActiveContent), content.IsActive);
            Check.Equal(Root.ActiveContent == null ? 0 : 1, contents.Count(c => c.IsActive));
        }
    }

    private sealed class ThrowingDocument : LayoutDocument
    {
        internal string Hook = "";
        internal Exception Failure = null!;
        protected override void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue)
        {
            base.OnParentChanging(oldValue, newValue);
            if (Hook == "parent-changing") throw Failure;
        }
        protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
        {
            base.OnParentChanged(oldValue, newValue);
            if (Hook == "parent-changed") throw Failure;
        }
        protected override void OnRootChanged(ILayoutRoot? oldRoot, ILayoutRoot? newRoot)
        {
            base.OnRootChanged(oldRoot, newRoot);
            if (Hook == "root-changed") throw Failure;
        }
    }
}
