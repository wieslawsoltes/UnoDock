using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Xml;

namespace UnoDock.Layout.Serialization;
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
    public void Deserialize(XmlReader reader) => Restore(() => LayoutSnapshotXml.Read(reader, ReadLimits));
    public void Deserialize(TextReader reader) => Restore(() => LayoutSnapshotXml.Read(reader, ReadLimits));
    public void Deserialize(Stream stream) => Restore(() => LayoutSnapshotXml.Read(stream, ReadLimits));
    public void Deserialize(string filepath) { using var stream = File.OpenRead(filepath); Deserialize(stream); }

    private void Restore(Func<LayoutSnapshotNode> read)
    {
        // Acquire before calling a user-supplied reader, which can itself invoke
        // application code. Parsing still never mutates the currently attached tree.
        StartDeserialization();
        Exception? failure = null;
        try
        {
            var detached = LayoutXml.ReadRoot(read());
            EnsureCurrentRestore();
            FixupLayout(detached);
            EnsureCurrentRestore();
            Manager.Layout = detached;
            if (!ReferenceEquals(Manager.Layout, detached) || !ReferenceEquals(detached.Manager, Manager))
                throw new InvalidOperationException("A layout callback replaced the restored workspace during commit.");
        }
        catch (Exception error) { failure = error; }
        try { EndDeserialization(); }
        catch (Exception cleanup)
        {
            if (failure != null) throw new AggregateException("Restoring a layout and releasing its scope both failed.", failure, cleanup);
            throw;
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
