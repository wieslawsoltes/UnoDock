using System.Collections;
using System.Collections.ObjectModel;
using UnoDock.Layout;

namespace UnoDock.Testing;

public static class SourceOwnershipTests
{
    public static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var tools in new[] { false, true })
        {
            var kind = tools ? "tools" : "documents";
            Add("enumerator replacement never materializes stale values", () =>
            {
                using var manager = new DockingManager(); var stale = new PoisonValue(); var current = new object();
                var sequence = new CallbackSequence([stale], () => Set(manager, new[] { current }, tools));
                Set(manager, sequence, tools);
                Check.Equal(1, sequence.Disposed); Check.Equal(0, stale.Calls);
                Check.Same(current, Models(manager, tools).Single().Content);
            });
            Add("enumerator Current replacement cancels the old snapshot", () =>
            {
                using var manager = new DockingManager(); var current = new object(); var stale = new PoisonValue();
                var sequence = new CallbackSequence([stale], null, () => Set(manager, new[] { current }, tools));
                Set(manager, sequence, tools); Check.Equal(0, stale.Calls);
                Check.Equal(1, sequence.Disposed); Check.Same(current, Models(manager, tools).Single().Content);
            });
            Add("enumerator Dispose replacement is checked before applying either source", () =>
            {
                using var manager = new DockingManager(); var current = new object(); var stale = new PoisonValue();
                Set(manager, new CallbackSequence([stale], null, null, () => Set(manager, new[] { current }, tools)), tools);
                Check.Equal(0, stale.Calls); Check.Same(current, Models(manager, tools).Single().Content);
            });
            Add("enumerator disposal of manager cannot reattach its old layout", () =>
            {
                using var manager = new DockingManager(); var root = manager.Layout;
                var sequence = new CallbackSequence([new PoisonValue()], manager.Dispose);
                Set(manager, sequence, tools); Check.Equal(1, sequence.Disposed);
                Check.True(root.Manager == null); Check.Equal(0, Models(manager, tools).Length);
            });
            foreach (var property in new[] { "title", "id", "string" })
                Add("descriptor replacement stops stale publication: " + property, () =>
                {
                    using var manager = new DockingManager(); var current = new object();
                    var descriptor = new CallbackDescriptor(property, () => Set(manager, new[] { current }, tools));
                    Set(manager, new object[] { descriptor }, tools);
                    Check.Same(current, Models(manager, tools).Single().Content); Check.Equal(1, descriptor.Callbacks);
                });
            Add("BeforeInsert replacement skips default placement and stale AfterInsert", () =>
            {
                using var manager = new DockingManager(); var stale = new object(); var current = new object(); var after = new List<LayoutContent>();
                manager.LayoutUpdateStrategy = new Strategy
                {
                    Before = (_, model, _) => { if (ReferenceEquals(model.Content, stale)) Set(manager, new[] { current }, tools); return false; },
                    After = (_, model) => after.Add(model)
                };
                Set(manager, new[] { stale }, tools);
                Check.Equal(1, after.Count); Check.Same(current, after.Single().Content);
                Check.Same(current, Models(manager, tools).Single().Content);
            });
            Add("BeforeInsert same-source mutation retries retained value exactly once", () =>
            {
                using var manager = new DockingManager(); var a = new object(); var b = new object();
                var source = new ObservableCollection<object> { a }; var once = false; var after = 0;
                manager.LayoutUpdateStrategy = new Strategy
                {
                    Before = (_, _, _) => { if (!once) { once = true; source.Add(b); } return false; },
                    After = (_, _) => after++
                };
                Set(manager, source, tools); Check.Equal(2, Models(manager, tools).Length); Check.Equal(2, after);
                Check.Equal(1, Models(manager, tools).Count(m => ReferenceEquals(m.Content, a)));
            });
            Add("BeforeInsert handles detached content without repeated callback loops", () =>
            {
                using var manager = new DockingManager(); var calls = 0; var source = new ObservableCollection<object>();
                manager.LayoutUpdateStrategy = new Strategy { Before = (_, _, _) => { calls++; return true; } };
                Set(manager, source, tools); source.Add(new object()); using (manager.BeginLayoutUpdate()) { }
                Check.Equal(1, calls); Check.Equal(0, Models(manager, tools).Length);
            });
            Add("BeforeInsert custom same-root placement is not overridden", () =>
            {
                using var manager = new DockingManager(); ILayoutContainer? expected = null;
                manager.LayoutUpdateStrategy = new Strategy { Before = (root, model, _) => { expected = Place(root, model); return false; } };
                Set(manager, new[] { new object() }, tools); Check.Same(expected, Models(manager, tools).Single().Parent);
            });
            Add("BeforeInsert transfer to another manager is never stolen back", () =>
            {
                using var manager = new DockingManager(); using var other = new DockingManager(); LayoutContent? moved = null; var calls = 0;
                manager.LayoutUpdateStrategy = new Strategy
                {
                    Before = (_, model, _) => { moved = model; Place(other.Layout, model); return false; }, After = (_, _) => calls++
                };
                Set(manager, new[] { new object() }, tools); Check.Same(other.Layout, moved!.Root);
                Check.Equal(0, calls); Check.Equal(0, Models(manager, tools).Length);
                Set(manager, null, tools); Check.Same(other.Layout, moved.Root);
            });
            Add("BeforeInsert manager disposal suppresses placement and AfterInsert", () =>
            {
                using var manager = new DockingManager(); var root = manager.Layout; var calls = 0;
                manager.LayoutUpdateStrategy = new Strategy { Before = (_, _, _) => { manager.Dispose(); return false; }, After = (_, _) => calls++ };
                Set(manager, new[] { new object() }, tools); Check.Equal(0, calls);
                Check.True(root.Manager == null); Check.Equal(0, Models(manager, tools).Length);
            });
            Add("throwing AfterInsert keeps tracking so later removal cannot orphan a view", () =>
            {
                using var manager = new DockingManager(); var source = new ObservableCollection<object>(); var value = new object();
                var strategy = new Strategy { After = (_, _) => throw new ApplicationException("after failure") };
                manager.LayoutUpdateStrategy = strategy; Set(manager, source, tools);
                Check.Throws<ApplicationException>(() => source.Add(value)); Check.Equal(1, Models(manager, tools).Length);
                strategy.After = null; source.Remove(value); Check.Equal(0, Models(manager, tools).Length);
            });
            Add("throwing BeforeInsert allows a later retry of the same source entry", () =>
            {
                using var manager = new DockingManager(); var value = new object();
                var strategy = new Strategy { Before = (_, _, _) => throw new ApplicationException("before failure") };
                manager.LayoutUpdateStrategy = strategy;
                Check.Throws<ApplicationException>(() => Set(manager, new[] { value }, tools));
                Check.Equal(0, Models(manager, tools).Length); strategy.Before = null; using (manager.BeginLayoutUpdate()) { }
                Check.Same(value, Models(manager, tools).Single().Content);
            });
            Add("removal cannot detach a model transferred to another manager", () =>
            {
                using var manager = new DockingManager(); using var other = new DockingManager(); var value = new object();
                var source = new ObservableCollection<object> { value }; Set(manager, source, tools);
                var model = Models(manager, tools).Single(); var parent = Place(other.Layout, model);
                source.Remove(value); Check.Same(parent, model.Parent); Check.Same(other.Layout, model.Root);
            });
            Add("removal cannot delete a model repurposed by application Content replacement", () =>
            {
                using var manager = new DockingManager(); var value = new object(); var replacement = new object();
                var source = new ObservableCollection<object> { value }; Set(manager, source, tools);
                var model = Models(manager, tools).Single(); var parent = model.Parent; model.Content = replacement;
                source.Remove(value); Check.Same(parent, model.Parent); Check.Same(replacement, model.Content);
            });
            Add("foreign direct model fails preflight without removing either source", () =>
            {
                using var manager = new DockingManager(); using var other = new DockingManager(); var original = new object();
                Set(manager, new[] { original }, tools);
                LayoutContent foreign = tools ? new LayoutAnchorable() : new LayoutDocument(); Place(other.Layout, foreign);
                Check.Throws<InvalidOperationException>(() => Set(manager, new object[] { foreign }, tools));
                Check.Same(original, Models(manager, tools).Single().Content); Check.Same(other.Layout, foreign.Root);
                Set(manager, null, tools); Check.Equal(0, Models(manager, tools).Length); Check.Same(other.Layout, foreign.Root);
            });
            Add("same-root direct model is adopted without duplicate insertion", () =>
            {
                using var manager = new DockingManager(); LayoutContent model = tools ? new LayoutAnchorable() : new LayoutDocument();
                var parent = Place(manager.Layout, model); var source = new ObservableCollection<object> { model };
                Set(manager, source, tools); Check.Equal(1, Models(manager, tools).Length); Check.Same(parent, model.Parent);
                source.Clear(); Check.True(model.Parent == null);
            });
            Add("nulls and duplicate reference entries remain deduplicated", () =>
            {
                using var manager = new DockingManager(); var value = new object();
                Set(manager, new object?[] { null, value, value, null }, tools);
                Check.Equal(1, Models(manager, tools).Length); Check.Same(value, Models(manager, tools).Single().Content);
            });
            Add("root replacement during descriptors never fills the detached old root", () =>
            {
                using var manager = new DockingManager(); var original = manager.Layout;
                var descriptor = new CallbackDescriptor("title", () => manager.Layout = new());
                Set(manager, new object[] { descriptor }, tools);
                Check.Equal(0, original.Descendents().OfType<LayoutContent>().Count());
                Check.Equal(1, Models(manager, tools).Length); Check.Same(descriptor, Models(manager, tools).Single().Content);
            });
            Add("pending pass detects replace-then-reinstate source identity", () =>
            {
                using var manager = new DockingManager(); var source = new ObservableCollection<object> { new object() }; var once = false; var after = 0;
                manager.LayoutUpdateStrategy = new Strategy
                {
                    Before = (_, _, _) => { if (!once) { once = true; Set(manager, null, tools); Set(manager, source, tools); } return false; },
                    After = (_, _) => after++
                };
                Set(manager, source, tools); Check.Equal(1, after); Check.Equal(1, Models(manager, tools).Length);
            });
            Add("non-converging callbacks are bounded and the manager remains recoverable", () =>
            {
                using var manager = new DockingManager(); var source = new ObservableCollection<object> { new object() };
                manager.LayoutUpdateStrategy = new Strategy { Before = (_, _, _) => { source[0] = new object(); return false; } };
                Check.Throws<InvalidOperationException>(() => Set(manager, source, tools));
                manager.LayoutUpdateStrategy = null; using (manager.BeginLayoutUpdate()) { }
                Check.Equal(1, Models(manager, tools).Length); Check.Same(source[0], Models(manager, tools).Single().Content);
            });
            void Add(string name, Action action) => tests.Test("source ownership (" + kind + "): " + name, action);
        }
        tests.Test("source ownership: document enumeration replacing tool source never enumerates stale tools", () =>
        {
            using var manager = new DockingManager(); var tool = new object(); var doc = new object();
            using (manager.BeginLayoutUpdate())
            {
                manager.AnchorablesSource = new CallbackSequence([], () => throw new ApplicationException("stale tool enumeration"));
                manager.DocumentsSource = new CallbackSequence([doc], () => manager.AnchorablesSource = new[] { tool });
            }
            Check.Same(doc, Models(manager, false).Single().Content); Check.Same(tool, Models(manager, true).Single().Content);
        });
        tests.Test("source ownership: both enumerations complete before either removal", () =>
        {
            using var manager = new DockingManager(); var doc = new object(); var tool = new object();
            manager.DocumentsSource = new[] { doc }; manager.AnchorablesSource = new[] { tool };
            var batch = manager.BeginLayoutUpdate(); manager.DocumentsSource = Array.Empty<object>();
            manager.AnchorablesSource = new CallbackSequence([], () => throw new ApplicationException("enumeration"));
            Check.Throws<ApplicationException>(batch.Dispose);
            Check.Same(doc, Models(manager, false).Single().Content); Check.Same(tool, Models(manager, true).Single().Content);
        });
        return tests.Run(output, "source-ownership");
    }
    private static void Set(DockingManager manager, IEnumerable? source, bool tools)
    { if (tools) manager.AnchorablesSource = source!; else manager.DocumentsSource = source!; }
    private static LayoutContent[] Models(DockingManager manager, bool tools) => manager.Layout.Descendents().OfType<LayoutContent>()
        .Where(model => tools ? model is LayoutAnchorable : model is LayoutDocument).ToArray();
    private static ILayoutContainer Place(LayoutRoot root, LayoutContent model)
    {
        if (model is LayoutDocument document)
        { var pane = new LayoutDocumentPane(document); root.RootPanel.Children.Add(pane); return pane; }
        var tools = new LayoutAnchorablePane((LayoutAnchorable)model); root.RootPanel.Children.Add(tools); return tools;
    }
    private sealed class Strategy : ILayoutUpdateStrategy
    {
        internal Func<LayoutRoot, LayoutContent, ILayoutContainer, bool>? Before;
        internal Action<LayoutRoot, LayoutContent>? After;
        public bool BeforeInsertDocument(LayoutRoot root, LayoutDocument document, ILayoutContainer target) => Before?.Invoke(root, document, target) ?? false;
        public void AfterInsertDocument(LayoutRoot root, LayoutDocument document) => After?.Invoke(root, document);
        public bool BeforeInsertAnchorable(LayoutRoot root, LayoutAnchorable anchorable, ILayoutContainer target) => Before?.Invoke(root, anchorable, target) ?? false;
        public void AfterInsertAnchorable(LayoutRoot root, LayoutAnchorable anchorable) => After?.Invoke(root, anchorable);
    }
    private sealed class PoisonValue
    {
        internal int Calls;
        public override string ToString() { Calls++; throw new ApplicationException("stale value evaluated"); }
    }
    private sealed class CallbackDescriptor(string property, Action callback) : IDockContent
    {
        internal int Callbacks;
        private void Invoke(string name) { if (property == name && Callbacks++ == 0) callback(); }
        public string Title { get { Invoke("title"); return property == "string" ? null! : "Title"; } }
        public string ContentId { get { Invoke("id"); return "descriptor"; } }
        public override string ToString() { Invoke("string"); return "String title"; }
    }
    private sealed class CallbackSequence(object[] values, Action? move = null, Action? current = null, Action? dispose = null) : IEnumerable
    {
        private bool _moved, _read, _disposed;
        internal int Disposed;
        public IEnumerator GetEnumerator() => new Iterator(this);
        private object Read(int index) { if (!_read) { _read = true; current?.Invoke(); } return values[index]; }
        private bool Next(ref int index) { if (!_moved) { _moved = true; move?.Invoke(); } return ++index < values.Length; }
        private void Release() { Disposed++; if (!_disposed) { _disposed = true; dispose?.Invoke(); } }
        private sealed class Iterator(CallbackSequence owner) : IEnumerator, IDisposable
        {
            private int _index = -1;
            public object Current => owner.Read(_index);
            public bool MoveNext() => owner.Next(ref _index);
            public void Reset() => throw new NotSupportedException();
            public void Dispose() => owner.Release();
        }
    }
}
