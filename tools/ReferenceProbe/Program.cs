// Independently authored black-box probes. Only public properties and methods
// are used. No non-public reflection, IL inspection, source bodies or resources.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Usage: ReferenceProbe <output-directory>");
        var output = args[0]; Directory.CreateDirectory(output);
        var defaults = new XElement("PublicDefaults");
        object[] instances = {
            new DockingManager(), new LayoutDocument(), new LayoutAnchorable(), new LayoutRoot(),
            new LayoutPanel(), new LayoutDocumentPane(), new LayoutAnchorablePane(),
            new LayoutDocumentPaneGroup(), new LayoutAnchorablePaneGroup(), new LayoutAnchorSide(),
            new LayoutAnchorGroup(), new LayoutDocumentFloatingWindow(), new LayoutAnchorableFloatingWindow()
        };
        foreach (var instance in instances.OrderBy(o => o.GetType().FullName, StringComparer.Ordinal))
        {
            var type = instance.GetType(); var record = new XElement("Type", new XAttribute("Name", type.FullName));
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (property.GetIndexParameters().Length != 0 || property.GetMethod == null ||
                    !(property.DeclaringType.Namespace ?? "").StartsWith("Xceed.Wpf.AvalonDock", StringComparison.Ordinal) || !Scalar(property.PropertyType)) continue;
                try { record.Add(Value("Property", property.Name, property.PropertyType, property.GetValue(instance))); }
                catch (TargetInvocationException e) { record.Add(new XElement("GetterError", new XAttribute("Name", property.Name), new XAttribute("Type", e.InnerException.GetType().FullName))); }
            }
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (field.FieldType != typeof(DependencyProperty) || !(field.DeclaringType.Namespace ?? "").StartsWith("Xceed.Wpf.AvalonDock", StringComparison.Ordinal)) continue;
                var property = (DependencyProperty)field.GetValue(null); var metadata = property.GetMetadata(type);
                var item = Value("DependencyProperty", property.Name, property.PropertyType, metadata.DefaultValue);
                if (metadata is FrameworkPropertyMetadata framework)
                {
                    item.SetAttributeValue("AffectsMeasure", framework.AffectsMeasure);
                    item.SetAttributeValue("AffectsArrange", framework.AffectsArrange);
                    item.SetAttributeValue("AffectsRender", framework.AffectsRender);
                    item.SetAttributeValue("BindsTwoWayByDefault", framework.BindsTwoWayByDefault);
                }
                record.Add(item);
            }
            defaults.Add(record);
        }
        new XDocument(defaults).Save(Path.Combine(output, "public-defaults.xml"));
        var basic = Workspace(); Save(basic, output, "basic.xml");
        var hidden = Workspace(); Tool(hidden, "explorer").Hide(); Save(hidden, output, "hidden.xml");
        var autoHide = Workspace(); Tool(autoHide, "explorer").ToggleAutoHide(); Save(autoHide, output, "auto-hide.xml");
        var floating = Workspace(); var document = floating.Layout.Descendents().OfType<LayoutDocument>().First();
        document.Parent.RemoveChild(document);
        document.FloatingLeft = -150; document.FloatingTop = 80; document.FloatingWidth = 760; document.FloatingHeight = 510;
        floating.Layout.FloatingWindows.Add(new LayoutDocumentFloatingWindow { RootDocument = document });
        Save(floating, output, "floating-document.xml");
        var toolFloat = Workspace(); var tool = Tool(toolFloat, "explorer"); tool.Parent.RemoveChild(tool);
        toolFloat.Layout.FloatingWindows.Add(new LayoutAnchorableFloatingWindow { RootPanel = new LayoutAnchorablePaneGroup(new LayoutAnchorablePane(tool)) });
        Save(toolFloat, output, "floating-tool.xml");
        foreach (var side in new[] { AnchorableShowStrategy.Left, AnchorableShowStrategy.Right, AnchorableShowStrategy.Top, AnchorableShowStrategy.Bottom })
        {
            var normal = Workspace(); new LayoutAnchorable { Title = "Added", ContentId = "added" }.AddToLayout(normal, side);
            Save(normal, output, "add-" + side.ToString().ToLowerInvariant() + ".xml");
            var most = Workspace(); new LayoutAnchorable { Title = "Added", ContentId = "added" }.AddToLayout(most, side | AnchorableShowStrategy.Most);
            Save(most, output, "add-most-" + side.ToString().ToLowerInvariant() + ".xml");
        }
        Console.WriteLine("Captured public defaults and 13 layouts using public APIs only.");
        return 0;
    }
    private static bool Scalar(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(GridLength);
    private static XElement Value(string kind, string name, Type type, object value)
    {
        var item = new XElement(kind, new XAttribute("Name", name), new XAttribute("Type", type.FullName));
        if (value == null) item.SetAttributeValue("Null", true);
        else if (value is GridLength length) item.SetAttributeValue("Value", length.IsAuto ? "Auto" : length.IsStar ? length.Value.ToString("R", CultureInfo.InvariantCulture) + "*" : length.Value.ToString("R", CultureInfo.InvariantCulture));
        else if (Scalar(value.GetType())) item.SetAttributeValue("Value", Convert.ToString(value, CultureInfo.InvariantCulture));
        else item.SetAttributeValue("ValueType", value.GetType().FullName);
        return item;
    }
    private static DockingManager Workspace()
    {
        var docs = new LayoutDocumentPane();
        docs.Children.Add(new LayoutDocument { Title = "Program.cs", ContentId = "doc:program", CanClose = false });
        docs.Children.Add(new LayoutDocument { Title = "Notes <draft> & β", ContentId = "doc:notes&β", Description = "fixture" });
        var secondary = new LayoutDocumentPane(); secondary.Children.Add(new LayoutDocument { Title = "Readme", ContentId = "doc:readme" });
        var group = new LayoutDocumentPaneGroup { Orientation = Orientation.Vertical }; group.Children.Add(docs); group.Children.Add(secondary);
        var left = new LayoutAnchorablePane { DockWidth = new GridLength(240) }; left.Children.Add(new LayoutAnchorable { Title = "Explorer", ContentId = "explorer", AutoHideWidth = 270 });
        var right = new LayoutAnchorablePane { DockWidth = new GridLength(2, GridUnitType.Star) }; right.Children.Add(new LayoutAnchorable { Title = "Properties", ContentId = "properties", CanHide = false });
        var panel = new LayoutPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(left); panel.Children.Add(group); panel.Children.Add(right);
        var manager = new DockingManager { Layout = new LayoutRoot { RootPanel = panel } }; docs.Children[0].IsActive = true; return manager;
    }
    private static LayoutAnchorable Tool(DockingManager manager, string id) => manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(c => c.ContentId == id);
    private static void Save(DockingManager manager, string output, string name)
    {
        foreach (var content in manager.Layout.Descendents().OfType<LayoutContent>()) content.LastActivationTimeStamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        new XmlLayoutSerializer(manager).Serialize(Path.Combine(output, name));
    }
}
