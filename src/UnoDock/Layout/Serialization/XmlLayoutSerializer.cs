using System.Xml;

namespace UnoDock.Layout.Serialization;

public class LayoutSerializationCallbackEventArgs(LayoutContent model, object? previousContent) : CancelEventArgs
{
    public LayoutContent Model { get; private set; } = model;
    public object? Content { get; set; } = previousContent;
}
public abstract class LayoutSerializer
{
    private IDisposable? _transaction;
    private Dictionary<string, LayoutContent> _previous = new(StringComparer.Ordinal);
    public LayoutSerializer(DockingManager manager) => Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    public DockingManager Manager { get; }
    public event EventHandler<LayoutSerializationCallbackEventArgs>? LayoutSerializationCallback;
    protected void StartDeserialization()
    {
        if (_transaction != null) throw new InvalidOperationException("A serializer instance is not reentrant.");
        _previous = Manager.Layout.Descendents().OfType<LayoutContent>().Where(c => c.ContentId != null)
            .GroupBy(c => c.ContentId!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _transaction = Manager.BeginLayoutUpdate();
    }
    protected void EndDeserialization() { _transaction?.Dispose(); _transaction = null; _previous.Clear(); }
    protected virtual void FixupLayout(LayoutRoot layout)
    {
        using var batch = layout.BeginUpdate();
        foreach (var content in layout.Descendents().OfType<LayoutContent>().ToArray())
        {
            var previous = content.ContentId is { } id ? _previous.GetValueOrDefault(id) : null;
            var args = new LayoutSerializationCallbackEventArgs(content, previous?.Content);
            LayoutSerializationCallback?.Invoke(this, args);
            if (args.Cancel) { content.Parent?.RemoveChild(content); continue; }
            content.Content = args.Content;
            content.IconSource = previous?.IconSource;
            content.ToolTip = previous?.ToolTip;
        }
        layout.CollectGarbage();
    }
}
public class XmlLayoutSerializer : LayoutSerializer
{
    public XmlLayoutSerializer(DockingManager manager) : base(manager) { }
    public LayoutReadLimits ReadLimits { get; set; } = new();
    public void Serialize(XmlWriter writer) => LayoutSnapshotXml.Write(LayoutXml.CaptureRoot(Manager.Layout), writer);
    public void Serialize(TextWriter writer)
    {
        using var xml = XmlWriter.Create(writer, new() { Indent = true, OmitXmlDeclaration = true, CloseOutput = false, NewLineChars = "\n" });
        Serialize(xml);
    }
    public void Serialize(Stream stream)
    {
        using var xml = XmlWriter.Create(stream, new() { Indent = true, Encoding = new System.Text.UTF8Encoding(false), CloseOutput = false, NewLineChars = "\n" });
        Serialize(xml);
    }
    public void Serialize(string filepath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filepath);
        var full = Path.GetFullPath(filepath); var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var file = File.Create(temporary)) Serialize(file); File.Move(temporary, full, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Deserialize(XmlReader reader) => Restore(LayoutSnapshotXml.Read(reader, ReadLimits));
    public void Deserialize(TextReader reader) => Restore(LayoutSnapshotXml.Read(reader, ReadLimits));
    public void Deserialize(Stream stream) => Restore(LayoutSnapshotXml.Read(stream, ReadLimits));
    public void Deserialize(string filepath) { using var stream = File.OpenRead(filepath); Deserialize(stream); }
    private void Restore(LayoutSnapshotNode snapshot)
    {
        // Parse and validate before touching the live manager; callback failures also preserve its layout.
        var detached = LayoutXml.ReadRoot(snapshot);
        StartDeserialization();
        try { FixupLayout(detached); Manager.Layout = detached; }
        finally { EndDeserialization(); }
    }
}
