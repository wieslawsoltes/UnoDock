using System.Xml;

namespace UnoDock.Core;

public sealed class LayoutSnapshotNode(string name)
{
    public string Name { get; } = XmlConvert.VerifyNCName(name ?? throw new ArgumentNullException(nameof(name)));
    public SortedDictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);
    public List<LayoutSnapshotNode> Children { get; } = [];
}
