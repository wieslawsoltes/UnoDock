// Only the black-box probe uses this alias. It invokes the original public
// serializer, then normalizes generated identities without changing behavior data.
global using XmlLayoutSerializer = UnoDock.ReferenceProbe.CanonicalLayoutSerializer;

namespace UnoDock.ReferenceProbe
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using Xceed.Wpf.AvalonDock;

    internal sealed class CanonicalLayoutSerializer : Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer
    {
        public CanonicalLayoutSerializer(DockingManager manager) : base(manager) { }

        public new void Serialize(string path)
        {
            base.Serialize(path);
            var xml = XDocument.Load(path);
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in xml.Descendants().Attributes("Id"))
                if (!ids.ContainsKey(attribute.Value))
                    ids.Add(attribute.Value, "00000000-0000-0000-0000-" + (ids.Count + 1).ToString("D12", CultureInfo.InvariantCulture));
            foreach (var attribute in xml.Descendants().Attributes().Where(a => a.Name == "Id" || a.Name == "PreviousContainerId"))
                if (ids.TryGetValue(attribute.Value, out var normalized)) attribute.Value = normalized;
            // ContentId, topology, capability flags, selection, dimensions and all
            // other serializer output are retained. Reference links stay coherent.
            xml.Save(path);
        }
    }
}
