using System.Xml;

namespace UnoDock.Core;

public sealed record LayoutReadLimits(int MaxDepth = 128, int MaxNodes = 100_000, int MaxAttributesPerNode = 128, long MaxCharacters = 16 * 1024 * 1024)
{
    internal void Validate()
    {
        if (MaxDepth < 1 || MaxDepth > 1024 || MaxNodes < 1 || MaxAttributesPerNode < 0 || MaxCharacters < 1)
            throw new ArgumentOutOfRangeException(nameof(LayoutReadLimits));
    }
}
public sealed class LayoutSnapshotNode(string name)
{
    public string Name { get; } = XmlConvert.VerifyNCName(name ?? throw new ArgumentNullException(nameof(name)));
    public SortedDictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);
    public List<LayoutSnapshotNode> Children { get; } = [];
}

/// <summary>Bounded, explicit XML tree codec. Never creates CLR types or resolves external entities.</summary>
public static class LayoutSnapshotXml
{
    private static XmlReaderSettings Settings(LayoutReadLimits limits) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false,
        MaxCharactersInDocument = limits.MaxCharacters, MaxCharactersFromEntities = limits.MaxCharacters,
        IgnoreComments = true, IgnoreProcessingInstructions = true
    };
    public static LayoutSnapshotNode Read(Stream stream, LayoutReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream); limits ??= new(); limits.Validate();
        using var reader = XmlReader.Create(stream, Settings(limits));
        var result = Read(reader, limits); EnsureEnd(reader); return result;
    }
    public static LayoutSnapshotNode Read(TextReader text, LayoutReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(text); limits ??= new(); limits.Validate();
        using var reader = XmlReader.Create(text, Settings(limits));
        var result = Read(reader, limits); EnsureEnd(reader); return result;
    }
    public static LayoutSnapshotNode Read(XmlReader reader, LayoutReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(reader); limits ??= new(); limits.Validate();
        if (reader.ReadState == ReadState.Initial && !reader.Read()) throw new XmlException("Empty layout document.");
        while (reader.NodeType is XmlNodeType.XmlDeclaration or XmlNodeType.Whitespace or XmlNodeType.Comment or XmlNodeType.ProcessingInstruction)
            if (!reader.Read()) throw new XmlException("Empty layout document.");
        var count = 0; long characters = 0;
        return Element(1);
        LayoutSnapshotNode Element(int depth)
        {
            if (depth > limits.MaxDepth || ++count > limits.MaxNodes) throw new XmlException("Layout node/depth limit exceeded.");
            if (reader.NodeType != XmlNodeType.Element) throw new XmlException("Expected a layout element, not " + reader.NodeType);
            if (reader.NamespaceURI.Length != 0) throw new XmlException("Namespaced layout elements are not supported.");
            var node = new LayoutSnapshotNode(reader.LocalName);
            characters += node.Name.Length;
            if (reader.AttributeCount > limits.MaxAttributesPerNode) throw new XmlException("Layout attribute limit exceeded.");
            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    characters += reader.Name.Length + reader.Value.Length;
                    if (characters > limits.MaxCharacters) throw new XmlException("Layout character limit exceeded.");
                    // Standard serializer namespace declarations do not encode layout state.
                    if (reader.Prefix == "xmlns" || reader.Name == "xmlns") continue;
                    if (reader.NamespaceURI.Length != 0) throw new XmlException("Namespaced layout attributes are not supported.");
                    if (!node.Attributes.TryAdd(reader.LocalName, reader.Value)) throw new XmlException("Duplicate layout attribute.");
                } while (reader.MoveToNextAttribute());
                reader.MoveToElement();
            }
            var empty = reader.IsEmptyElement; reader.Read();
            if (empty) return node;
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType == XmlNodeType.Element) node.Children.Add(Element(depth + 1));
                else if (reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or XmlNodeType.Comment or XmlNodeType.ProcessingInstruction)
                { characters += reader.Value.Length; if (characters > limits.MaxCharacters) throw new XmlException("Layout character limit exceeded."); reader.Read(); }
                else throw new XmlException("Unexpected layout content: " + reader.NodeType);
            }
            reader.Read(); return node;
        }
    }
    private static void EnsureEnd(XmlReader reader)
    {
        while (!reader.EOF)
        {
            if (reader.NodeType is not (XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or XmlNodeType.Comment or XmlNodeType.ProcessingInstruction or XmlNodeType.None))
                throw new XmlException("Trailing layout content.");
            reader.Read();
        }
    }
    public static void Write(LayoutSnapshotNode node, XmlWriter writer)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(writer);
        var seen = new HashSet<LayoutSnapshotNode>(ReferenceEqualityComparer.Instance);
        WriteElement(node, 1);
        void WriteElement(LayoutSnapshotNode current, int depth)
        {
            if (depth > 128 || !seen.Add(current)) throw new InvalidOperationException("Layout snapshots must be bounded ownership trees.");
            writer.WriteStartElement(current.Name);
            foreach (var (key, value) in current.Attributes) writer.WriteAttributeString(key, value);
            foreach (var child in current.Children) WriteElement(child, depth + 1);
            writer.WriteEndElement();
        }
    }
}
