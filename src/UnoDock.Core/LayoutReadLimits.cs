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
