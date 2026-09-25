using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnoDock;
using UnoDock.Layout;
using UnoDock.Layout.Serialization;
using UnoDock.Core;

namespace UnoDock.Testing;

using LayoutPanel = UnoDock.Layout.LayoutPanel;

/// <summary>Tests against outputs produced by the original public API, not an implementation translation.</summary>
public static class InteropTests
{
    public static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        var assembly = typeof(InteropTests).Assembly;
        var fixtures = assembly.GetManifestResourceNames().Where(n => n.StartsWith("ReferenceFixtures.", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        tests.Test("reference fixture set is complete", () => Check.Equal(14, fixtures.Length));
        foreach (var name in fixtures.Where(n => !n.EndsWith("public-defaults.xml", StringComparison.Ordinal)))
        {
            tests.Test("original layout import and roundtrip: " + name, () =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                var xml = XDocument.Load(stream);
                using var manager = new DockingManager();
                var serializer = new XmlLayoutSerializer(manager);
                serializer.LayoutSerializationCallback += (_, e) => e.Content = e.Model.ContentId;
                using (var reader = xml.CreateReader())
                    serializer.Deserialize(reader);
                CheckContents(xml, manager.Layout);
                using var saved = new StringWriter(CultureInfo.InvariantCulture);
                serializer.Serialize(saved);
                var roundtrip = XDocument.Parse(saved.ToString());
                foreach (var floating in roundtrip.Descendants("LayoutDocumentFloatingWindow"))
                    Check.Equal("LayoutDocument", floating.Elements().Single().Name.LocalName);
                foreach (var floating in roundtrip.Descendants("LayoutAnchorableFloatingWindow"))
                    Check.Equal("LayoutAnchorablePaneGroup", floating.Elements().Single().Name.LocalName);
                serializer.Deserialize(new StringReader(saved.ToString()));
                CheckContents(xml, manager.Layout);
                foreach (var group in manager.Layout.Descendents().OfType<ILayoutContentSelector>())
                {
                    var children = ((ILayoutContainer)group).Children.OfType<LayoutContent>();
                    Check.True(children.Count(c => c.IsSelected) <= 1, "Multiple selected siblings after XML import.");
                    if (group.SelectedContent != null)
                        Check.True(group.SelectedContent.IsSelected);
                }

                if (name.EndsWith("hidden.xml", StringComparison.Ordinal))
                {
                    var hidden = manager.Layout.Hidden.Single();
                    Check.True(hidden.PreviousContainer != null);
                    hidden.Show();
                    Check.False(hidden.IsHidden);
                    Check.True(hidden.Parent is LayoutAnchorablePane);
                }

                if (name.EndsWith("auto-hide.xml", StringComparison.Ordinal))
                {
                    var group = manager.Layout.LeftSide.Children.Single();
                    var previous = group.PreviousContainer;
                    Check.True(previous != null);
                    var tool = group.Children.Single();
                    tool.ToggleAutoHide();
                    Check.Same(previous, tool.Parent);
                }

                if (name.EndsWith("floating-document.xml", StringComparison.Ordinal))
                {
                    var doc = manager.Layout.FloatingWindows.OfType<LayoutDocumentFloatingWindow>().Single().RootDocument!;
                    Check.Near(-150, doc.FloatingLeft);
                    Check.Near(80, doc.FloatingTop);
                    Check.Near(760, doc.FloatingWidth);
                    Check.Near(510, doc.FloatingHeight);
                }
            });
        }

        using (var defaults = assembly.GetManifestResourceStream("ReferenceFixtures.public-defaults.xml")!)
        {
            var document = XDocument.Load(defaults);
            foreach (var record in document.Root!.Elements("Type"))
            {
                var typeName = record.Attribute("Name")!.Value;
                tests.Test("observed public defaults: " + typeName, () =>
                {
                    var type = typeof(DockingManager).Assembly.GetType(typeName.Replace("Xceed.Wpf.AvalonDock", "UnoDock", StringComparison.Ordinal), throwOnError: true)!;
                    var instance = Activator.CreateInstance(type)!;
                    try
                    {
                        foreach (var property in record.Elements("Property"))
                        {
                            var name = property.Attribute("Name")!.Value;
                            var reflected = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                            Check.True(reflected != null, "Missing property: " + name);
                            var actual = reflected!.GetValue(instance);
                            if (property.Attribute("Null") != null)
                            {
                                Check.True(actual == null, name + " must default to null.");
                                continue;
                            }

                            var text = actual is GridLength length ? length.IsAuto ? "Auto" : length.Value.ToString("R", CultureInfo.InvariantCulture) + (length.IsStar ? "*" : "") : Convert.ToString(actual, CultureInfo.InvariantCulture);
                            Check.True(property.Attribute("Value")?.Value == text, $"{name}: expected {property.Attribute("Value")?.Value}; actual {text}.");
                        }
                    }
                    finally
                    {
                        (instance as IDisposable)?.Dispose();
                    }
                });
            }
        }

        foreach (var side in new[]
        {
            AnchorableShowStrategy.Left,
            AnchorableShowStrategy.Right,
            AnchorableShowStrategy.Top,
            AnchorableShowStrategy.Bottom
        }

        )
        {
            foreach (var outermost in new[]
            {
                false,
                true
            }

            )
            {
                tests.Test($"public AddToLayout topology: {side}, Most={outermost}", () =>
                {
                    using var manager = new DockingManager();
                    Load(manager, "basic.xml");
                    var added = new LayoutAnchorable
                    {
                        ContentId = "added",
                        Title = "Added"
                    };
                    added.AddToLayout(manager, side | (outermost ? AnchorableShowStrategy.Most : 0));
                    var original = ReadFixture($"add-{(outermost ? "most-" : "")}{side.ToString().ToLowerInvariant()}.xml");
                    using var saved = new StringWriter(CultureInfo.InvariantCulture);
                    new XmlLayoutSerializer(manager).Serialize(saved);
                    Check.Equal(Topology(original.Root!), Topology(XDocument.Parse(saved.ToString()).Root!));
                });
            }
        }

        foreach (var boundary in new[]
        {
            0,
            1,
            2,
            3
        }

        )
            tests.Test("same-pane insertion boundary " + boundary, () =>
            {
                using var manager = new DockingManager();
                var docs = Enumerable.Range(0, 3).Select(i => new LayoutDocument { ContentId = i.ToString() }).ToArray();
                var pane = new LayoutDocumentPane();
                foreach (var doc in docs)
                    pane.Children.Add(doc);
                manager.Layout = new()
                {
                    RootPanel = new(pane)
                };
                DockOperations.Dock(docs[0], pane, DockPosition.Inside, boundary);
                Check.Equal(Math.Max(0, boundary - 1), pane.Children.IndexOf(docs[0]));
            });
        tests.Test("XML restores explicit non-first selection", () =>
        {
            using var manager = new DockingManager();
            new XmlLayoutSerializer(manager).Deserialize(new StringReader("<LayoutRoot><RootPanel><LayoutDocumentPane><LayoutDocument ContentId='a'/><LayoutDocument ContentId='b' IsSelected='True'/></LayoutDocumentPane></RootPanel></LayoutRoot>"));
            var pane = manager.Layout.Descendents().OfType<LayoutDocumentPane>().Single();
            Check.Equal("b", pane.SelectedContent!.ContentId);
            Check.Equal(1, pane.Children.Count(c => c.IsSelected));
        });
        tests.Test("invalid legacy timestamp does not mutate layout", () =>
        {
            using var manager = new DockingManager();
            var original = manager.Layout;
            Check.Throws<XmlException>(() => new XmlLayoutSerializer(manager).Deserialize(new StringReader("<LayoutRoot><RootPanel><LayoutDocumentPane><LayoutDocument LastActivationTimeStamp='not a timestamp'/></LayoutDocumentPane></RootPanel></LayoutRoot>")));
            Check.Same(original, manager.Layout);
        });
        tests.Test("mixed orientation policy rejects before mutation", () =>
        {
            using var manager = new DockingManager();
            var a = new LayoutDocument();
            var b = new LayoutDocument();
            var first = new LayoutDocumentPane(a);
            first.Children.Add(b);
            var group = new LayoutDocumentPaneGroup
            {
                Orientation = Orientation.Horizontal
            };
            group.Children.Add(first);
            group.Children.Add(new LayoutDocumentPane(new LayoutDocument()));
            manager.Layout = new()
            {
                RootPanel = new(group)
            };
            Check.False(DockOperations.CanDock(b, first, DockPosition.Bottom));
            DockOperations.Dock(b, first, DockPosition.Bottom);
            Check.Same(first, b.Parent);
            var item = manager.GetLayoutItemFromModel(b);
            Check.False(item.NewHorizontalTabGroupCommand!.CanExecute(null));
            manager.AllowMixedOrientation = true;
            Check.True(DockOperations.CanDock(b, first, DockPosition.Bottom));
            Check.True(item.NewHorizontalTabGroupCommand!.CanExecute(null));
            DockOperations.Dock(b, first, DockPosition.Bottom);
            Check.False(ReferenceEquals(first, b.Parent));
        });
        tests.Test("CanMove prevents restoring floating document", () =>
        {
            using var manager = new DockingManager();
            var doc = new LayoutDocument();
            manager.Layout = new()
            {
                RootPanel = new(new LayoutDocumentPane(doc))
            };
            doc.Float();
            Check.True(doc.IsFloating);
            doc.CanMove = false;
            doc.Dock();
            Check.True(doc.IsFloating);
        });
        tests.Test("CanRepositionItems prevents dock-as-document", () =>
        {
            using var manager = new DockingManager();
            var tool = new LayoutAnchorable();
            var pane = new LayoutAnchorablePane(tool)
            {
                CanRepositionItems = false
            };
            manager.Layout = new()
            {
                RootPanel = new(pane)
            };
            tool.DockAsDocument();
            Check.Same(pane, tool.Parent);
        });
        return await tests.Run(output, "interop");
    }

    private static XDocument ReadFixture(string file)
    {
        using var stream = typeof(InteropTests).Assembly.GetManifestResourceStream("ReferenceFixtures." + file)!;
        return XDocument.Load(stream);
    }

    private static void Load(DockingManager manager, string file)
    {
        using var stream = typeof(InteropTests).Assembly.GetManifestResourceStream("ReferenceFixtures." + file)!;
        new XmlLayoutSerializer(manager).Deserialize(stream);
    }

    private static string Topology(XElement node) => node.Name.LocalName + "[" + (string?)node.Attribute("Orientation") + ":" + (string?)node.Attribute("ContentId") + "](" + string.Join(',', node.Elements().Where(e => e.Name.LocalName is not ("TopSide" or "LeftSide" or "BottomSide" or "RightSide" or "FloatingWindows" or "Hidden")).Select(Topology)) + ")";
    private static void CheckContents(XDocument source, LayoutRoot layout)
    {
        var contents = source.Descendants().Where(e => e.Name.LocalName is "LayoutDocument" or "LayoutAnchorable").ToArray();
        var actual = layout.Descendents().OfType<LayoutContent>().ToDictionary(c => c.ContentId!, StringComparer.Ordinal);
        Check.Equal(contents.Length, actual.Count);
        foreach (var node in contents)
        {
            var id = node.Attribute("ContentId")!.Value;
            var content = actual[id];
            Check.Equal(node.Attribute("Title")?.Value, content.Title);
            Check.Equal(id, content.Content as string);
            Check.Equal(2000, content.LastActivationTimeStamp!.Value.Year);
            foreach (var property in new[]
            {
                "CanClose",
                "CanHide",
                "IsLastFocusedDocument"
            }

            )
            {
                if (node.Attribute(property) is not { } attribute)
                    continue;
                var value = content.GetType().GetProperty(property)!.GetValue(content);
                Check.Equal(bool.Parse(attribute.Value), (bool)value!);
            }
        }

        var focused = contents.FirstOrDefault(e => e.Attribute("IsLastFocusedDocument")?.Value == "True");
        if (focused != null)
            Check.Equal(focused.Attribute("ContentId")!.Value, layout.LastFocusedDocument?.ContentId);
    }
}
