using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using UnoDock.Layout;
using UnoDock.Layout.Serialization;

namespace UnoDock.Testing;

public static class RestoreOwnershipTests
{
    public static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        tests.Test("restore: same serializer recursion rejected without corrupting the outer scope", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var serializer = new XmlLayoutSerializer(manager); var calls = 0;
            serializer.LayoutSerializationCallback += (_, _) => { calls++; Check.Throws<InvalidOperationException>(() => serializer.Deserialize(new StringReader(xml))); };
            serializer.Deserialize(new StringReader(xml)); Check.Equal(2, calls); Check.Equal(2, Documents(manager).Length);
        });
        tests.Test("restore: separate serializer cannot bypass the same manager lease", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += (_, _) => Check.Throws<InvalidOperationException>(() => new XmlLayoutSerializer(manager).Deserialize(new StringReader(xml)));
            serializer.Deserialize(new StringReader(xml)); Check.Equal(2, Documents(manager).Length);
        });
        tests.Test("restore: independent manager may restore inside a content resolver", () =>
        {
            using var first = Workspace(); using var second = Workspace(); var secondRoot = second.Layout;
            var xml = Capture(first); var serializer = new XmlLayoutSerializer(first); var once = false;
            serializer.LayoutSerializationCallback += (_, _) => { if (!once) { once = true; new XmlLayoutSerializer(second).Deserialize(new StringReader(xml)); } };
            serializer.Deserialize(new StringReader(xml)); Check.False(ReferenceEquals(secondRoot, second.Layout));
            Check.Same(first, first.Layout.Manager); Check.Same(second, second.Layout.Manager);
        });
        tests.Test("restore: replacing the workspace during a resolver aborts all stale callbacks", () =>
        {
            using var manager = Workspace(); var replacement = new LayoutRoot(); var xml = Capture(manager); var count = 0;
            var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += (_, _) => { count++; manager.Layout = replacement; };
            Check.Throws<InvalidOperationException>(() => serializer.Deserialize(new StringReader(xml)));
            Check.Same(replacement, manager.Layout); Check.Same(manager, replacement.Manager); Check.Equal(1, count);
        });
        tests.Test("restore: replace then restore original root still invalidates the transaction", () =>
        {
            using var manager = Workspace(); var original = manager.Layout; var xml = Capture(manager);
            var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += (_, _) => { manager.Layout = new(); manager.Layout = original; };
            Check.Throws<InvalidOperationException>(() => serializer.Deserialize(new StringReader(xml))); Check.Same(original, manager.Layout);
        });
        tests.Test("restore: throwing content resolver preserves root and permits later retry", () =>
        {
            using var manager = Workspace(); var original = manager.Layout; var xml = Capture(manager);
            var serializer = new XmlLayoutSerializer(manager);
            EventHandler<LayoutSerializationCallbackEventArgs> fail = (_, _) => throw new ApplicationException("resolver failure");
            serializer.LayoutSerializationCallback += fail;
            Check.Throws<ApplicationException>(() => serializer.Deserialize(new StringReader(xml))); Check.Same(original, manager.Layout);
            serializer.LayoutSerializationCallback -= fail; serializer.Deserialize(new StringReader(xml)); Check.Equal(2, Documents(manager).Length);
        });
        tests.Test("restore: malformed XML does not strand a lease", () =>
        {
            using var manager = Workspace(); var original = manager.Layout; var xml = Capture(manager);
            var serializer = new XmlLayoutSerializer(manager);
            Check.Throws<System.Xml.XmlException>(() => serializer.Deserialize(new StringReader("<LayoutRoot>")));
            Check.Same(original, manager.Layout); serializer.Deserialize(new StringReader(xml)); Check.Equal(2, Documents(manager).Length);
        });
        tests.Test("restore: caller reader replacing layout cannot cause stale publication", () =>
        {
            using var manager = Workspace(); var replacement = new LayoutRoot(); var xml = Capture(manager);
            using var reader = new CallbackReader(xml, () => manager.Layout = replacement);
            Check.Throws<InvalidOperationException>(() => new XmlLayoutSerializer(manager).Deserialize(reader)); Check.Same(replacement, manager.Layout);
        });
        tests.Test("restore: reader callbacks cannot recursively enter a separate serializer", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var called = false;
            using var reader = new CallbackReader(xml, () => { called = true; Check.Throws<InvalidOperationException>(() => new XmlLayoutSerializer(manager).Deserialize(new StringReader(xml))); });
            new XmlLayoutSerializer(manager).Deserialize(reader); Check.True(called); Check.Equal(2, Documents(manager).Length);
        });
        tests.Test("restore: disposal during resolver cannot resurrect the manager", () =>
        {
            using var manager = Workspace(); var original = manager.Layout; var serializer = new XmlLayoutSerializer(manager); var xml = Capture(manager);
            serializer.LayoutSerializationCallback += (_, _) => manager.Dispose();
            Check.Throws<InvalidOperationException>(() => serializer.Deserialize(new StringReader(xml))); Check.Same(original, manager.Layout); Check.True(original.Manager == null);
        });
        tests.Test("restore: override replacing root is validated even without calling base fixup", () =>
        {
            using var manager = Workspace(); var replacement = new LayoutRoot(); var serializer = new ReplacingSerializer(manager, replacement);
            Check.Throws<InvalidOperationException>(() => serializer.Deserialize(new StringReader(Capture(manager)))); Check.Same(replacement, manager.Layout);
        });
        tests.Test("restore: commit callback replacement remains application owned", () =>
        {
            using var manager = Workspace(); var replacement = new LayoutRoot(); var xml = Capture(manager); var once = false;
            manager.LayoutChanged += (_, _) => { if (!once) { once = true; manager.Layout = replacement; } };
            Check.Throws<InvalidOperationException>(() => new XmlLayoutSerializer(manager).Deserialize(new StringReader(xml)));
            Check.Same(replacement, manager.Layout); Check.Same(manager, replacement.Manager);
        });
        tests.Test("restore: previous contents are immutable snapshots during callbacks", () =>
        {
            using var manager = Workspace(); var originals = Documents(manager); var expected = originals[1].Content; var xml = Capture(manager);
            var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += (_, e) => { if (e.Model.ContentId == "first") originals[1].Content = new object(); };
            serializer.Deserialize(new StringReader(xml)); Check.Same(expected, Documents(manager).Single(d => d.ContentId == "second").Content);
        });
        tests.Test("restore: callbacks may omit a node without invalidating unrelated nodes", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += (_, e) => e.Cancel = e.Model.ContentId == "first";
            serializer.Deserialize(new StringReader(xml)); Check.Equal("second", Documents(manager).Single().ContentId);
        });
        tests.Test("restore: moved callback node is not mutated in its new root", () =>
        {
            using var manager = Workspace(); using var other = new DockingManager();
            var pane = new LayoutDocumentPane(); other.Layout.RootPanel.Children.Add(pane);
            var serializer = new XmlLayoutSerializer(manager); var sentinel = new object();
            serializer.LayoutSerializationCallback += (_, e) => { if (e.Model.ContentId == "first") { e.Model.Content = sentinel; pane.Children.Add((LayoutDocument)e.Model); } };
            serializer.Deserialize(new StringReader(Capture(manager)));
            Check.Same(sentinel, pane.Children.Single().Content); Check.Equal(1, Documents(manager).Length);
        });
        tests.Test("restore: content setter may transfer ownership without later icon or tooltip writes", () =>
        {
            using var manager = Workspace(); using var other = new DockingManager();
            var pane = new LayoutDocumentPane(); other.Layout.RootPanel.Children.Add(pane);
            var icon = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(); var tooltip = new object();
            var serializer = new XmlLayoutSerializer(manager);
            serializer.LayoutSerializationCallback += (_, e) =>
            {
                if (e.Model.ContentId != "first") return;
                var moved = false;
                e.Model.PropertyChanged += (_, change) =>
                {
                    if (moved || change.PropertyName != nameof(LayoutContent.Content)) return;
                    moved = true; e.Model.IconSource = icon; e.Model.ToolTip = tooltip;
                    pane.Children.Add((LayoutDocument)e.Model);
                };
            };
            serializer.Deserialize(new StringReader(Capture(manager)));
            Check.Same(icon, pane.Children.Single().IconSource); Check.Same(tooltip, pane.Children.Single().ToolTip);
            Check.Equal(1, Documents(manager).Length);
        });
        tests.Test("restore: observable source adopts restored models without duplicate content", () =>
        {
            using var manager = new DockingManager(); var a = new object(); var b = new object();
            var source = new ObservableCollection<object> { a, b }; manager.DocumentsSource = source;
            var models = Documents(manager); models[0].ContentId = "a"; models[1].ContentId = "b";
            var xml = Capture(manager); new XmlLayoutSerializer(manager).Deserialize(new StringReader(xml));
            Check.Equal(2, Documents(manager).Length); Check.Equal(1, Documents(manager).Count(d => ReferenceEquals(d.Content, a)));
            source.Remove(a); Check.Equal(1, Documents(manager).Length); Check.Same(b, Documents(manager)[0].Content);
        });
        tests.Test("restore: lease is held through source reconciliation callbacks", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var reentryRejected = false;
            var serializer = new XmlLayoutSerializer(manager); var once = false;
            serializer.LayoutSerializationCallback += (_, _) =>
            {
                if (once) return; once = true;
                manager.DocumentsSource = new CallbackSequence(() => { Check.Throws<InvalidOperationException>(() => new XmlLayoutSerializer(manager).Deserialize(new StringReader(xml))); reentryRejected = true; });
            };
            serializer.Deserialize(new StringReader(xml)); Check.True(reentryRejected);
            manager.DocumentsSource = null; new XmlLayoutSerializer(manager).Deserialize(new StringReader(xml));
        });
        tests.Test("restore: primary and cleanup exceptions are both observable and lease released", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var serializer = new XmlLayoutSerializer(manager);
            EventHandler<LayoutSerializationCallbackEventArgs> fail = (_, _) => { manager.DocumentsSource = new CallbackSequence(() => throw new ApplicationException("enumeration failure")); throw new InvalidOperationException("resolver failure"); };
            serializer.LayoutSerializationCallback += fail;
            AggregateException? aggregate = null;
            try { serializer.Deserialize(new StringReader(xml)); } catch (AggregateException error) { aggregate = error; }
            Check.True(aggregate != null); Check.Equal(2, aggregate!.InnerExceptions.Count);
            Check.Equal("resolver failure", aggregate.InnerExceptions[0].Message); Check.Equal("enumeration failure", aggregate.InnerExceptions[1].Message);
            manager.DocumentsSource = null; serializer.LayoutSerializationCallback -= fail; serializer.Deserialize(new StringReader(xml));
        });
        tests.Test("restore: caller-owned stream and writer stay open", () =>
        {
            using var manager = Workspace(); var serializer = new XmlLayoutSerializer(manager); using var stream = new MemoryStream();
            serializer.Serialize(stream); Check.True(stream.CanWrite); stream.Position = 0; serializer.Deserialize(stream); Check.True(stream.CanRead);
            using var writer = new StringWriter(); serializer.Serialize(writer); writer.Write(" "); Check.True(writer.ToString().Length > 0);
        });
        tests.Test("restore: repeated successful restores never retain previous resolver state", () =>
        {
            using var manager = Workspace(); var xml = Capture(manager); var serializer = new XmlLayoutSerializer(manager);
            for (var i = 0; i < 24; i++)
            {
                var content = new object(); Documents(manager)[0].Content = content; serializer.Deserialize(new StringReader(xml));
                Check.Same(content, Documents(manager)[0].Content); Check.Same(manager, manager.Layout.Manager);
            }
        });
        return tests.Run(output, "restore-ownership");
    }
    private static LayoutDocument[] Documents(DockingManager manager) => manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
    private static DockingManager Workspace() => new() { Layout = new() { RootPanel = new LayoutPanel(new LayoutDocumentPane(
        new LayoutDocument { ContentId = "first", Title = "First", Content = new object() },
        new LayoutDocument { ContentId = "second", Title = "Second", Content = new object() })) } };
    private static string Capture(DockingManager manager) { using var text = new StringWriter(); new XmlLayoutSerializer(manager).Serialize(text); return text.ToString(); }
    private sealed class ReplacingSerializer(DockingManager manager, LayoutRoot replacement) : XmlLayoutSerializer(manager)
    { protected override void FixupLayout(LayoutRoot layout) => Manager.Layout = replacement; }
    private sealed class CallbackSequence(Action callback) : IEnumerable
    { public IEnumerator GetEnumerator() { callback(); return Array.Empty<object>().GetEnumerator(); } }
    private sealed class CallbackReader(string text, Action callback) : StringReader(text)
    {
        private bool _called;
        private void Invoke() { if (!_called) { _called = true; callback(); } }
        public override int Read(char[] buffer, int index, int count) { Invoke(); return base.Read(buffer, index, count); }
        public override int Read(Span<char> buffer) { Invoke(); return base.Read(buffer); }
        public override int Read() { Invoke(); return base.Read(); }
    }
}
