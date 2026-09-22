// Public-API black-box observations only: no source-body or private reflection access.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;

internal static class ConverterProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Usage: ConverterProbe <output-directory>");
        Directory.CreateDirectory(args[0]);
        var cases = new XElement("ConverterObservations");
        var names = new[] { "BoolToVisibilityConverter", "InverseBoolToVisibilityConverter", "AnchorSideToAngleConverter", "AnchorSideToOrientationConverter", "NullToDoNothingConverter", "AnchorableContextMenuAutoHideHeaderConverter", "LayoutItemFromLayoutModelConverter", "ActivateCommandLayoutItemFromLayoutModelConverter", "AutoHideCommandLayoutItemFromLayoutModelConverter", "HideCommandLayoutItemFromLayoutModelConverter" };
        var inputs = new[] { "null", "true", "false", "zero", "one", "string", "visible", "collapsed", "left", "right", "top", "bottom", "invalid-side", "document", "tool", "attached-document", "attached-tool" };
        var manager = new DockingManager();
        var document = new LayoutDocument { Title = "Document", ContentId = "document" };
        var tool = new LayoutAnchorable { Title = "Tool", ContentId = "tool" };
        manager.Layout.RootPanel.Children.Add(new LayoutDocumentPane(document));
        manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(tool));
        foreach (var name in names)
        {
            var type = typeof(DockingManager).Assembly.GetType("Xceed.Wpf.AvalonDock.Converters." + name, true);
            var converter = (IValueConverter)Activator.CreateInstance(type);
            foreach (var key in inputs)
                foreach (var reverse in new[] { false, true })
                    foreach (var target in new[] { "object", "visibility", "bool", "nullable-bool" })
                    {
                        var record = new XElement("Case", new XAttribute("Converter", name), new XAttribute("Input", key), new XAttribute("Reverse", reverse), new XAttribute("Target", target));
                        var value = Input(key, document, tool);
                        var targetType = target == "visibility" ? typeof(Visibility) : target == "bool" ? typeof(bool) : target == "nullable-bool" ? typeof(bool?) : typeof(object);
                        try
                        {
                            var result = reverse ? converter.ConvertBack(value, targetType, null, CultureInfo.InvariantCulture) : converter.Convert(value, targetType, null, CultureInfo.InvariantCulture);
                            record.Add(Result(result, value, manager, document, tool));
                        }
                        catch (Exception error) { record.Add(new XElement("Exception", new XAttribute("Type", error.GetType().FullName))); }
                        cases.Add(record);
                    }
        }
        var multiType = typeof(DockingManager).Assembly.GetType("Xceed.Wpf.AvalonDock.Converters.AnchorableContextMenuHideVisibilityConverter", true);
        var multi = (IMultiValueConverter)Activator.CreateInstance(multiType);
        foreach (var flags in new[] { "empty", "true", "false", "true,true", "true,false", "false,true", "false,false", "null", "true,null", "true,true,true" })
            foreach (var reverse in new[] { false, true })
            {
                var record = new XElement("MultiCase", new XAttribute("Input", flags), new XAttribute("Reverse", reverse));
                try
                {
                    var values = flags == "empty" ? new object[0] : flags.Split(',').Select(v => v == "null" ? null : (object)bool.Parse(v)).ToArray();
                    var result = reverse ? (object)multi.ConvertBack(Visibility.Visible, new[] { typeof(bool), typeof(bool) }, null, CultureInfo.InvariantCulture) : multi.Convert(values, typeof(Visibility), null, CultureInfo.InvariantCulture);
                    record.Add(Result(result, values, manager, document, tool));
                }
                catch (Exception error) { record.Add(new XElement("Exception", new XAttribute("Type", error.GetType().FullName))); }
                cases.Add(record);
            }
        new XDocument(cases).Save(Path.Combine(args[0], "converters.xml"));
        Console.WriteLine("Observed " + cases.Elements().Count() + " deterministic public converter calls.");
        return 0;
    }
    private static object Input(string key, LayoutDocument document, LayoutAnchorable tool)
    {
        switch (key)
        {
            case "null": return null; case "true": return true; case "false": return false;
            case "zero": return 0; case "one": return 1; case "string": return "text";
            case "visible": return Visibility.Visible; case "collapsed": return Visibility.Collapsed;
            case "left": return AnchorSide.Left; case "right": return AnchorSide.Right; case "top": return AnchorSide.Top; case "bottom": return AnchorSide.Bottom;
            case "invalid-side": return (AnchorSide)999; case "document": return new LayoutDocument(); case "tool": return new LayoutAnchorable();
            case "attached-document": return document; case "attached-tool": return tool;
            default: throw new ArgumentOutOfRangeException(nameof(key));
        }
    }
    private static XElement Result(object result, object input, DockingManager manager, LayoutDocument document, LayoutAnchorable tool)
    {
        if (result == null) return new XElement("Null");
        if (ReferenceEquals(result, Binding.DoNothing)) return new XElement("DoNothing");
        if (ReferenceEquals(result, DependencyProperty.UnsetValue)) return new XElement("UnsetValue");
        if (ReferenceEquals(result, input)) return new XElement("InputIdentity");
        var di = manager.GetLayoutItemFromModel(document); var ti = manager.GetLayoutItemFromModel(tool);
        if (ReferenceEquals(result, di)) return new XElement("DocumentItem");
        if (ReferenceEquals(result, ti)) return new XElement("ToolItem");
        if (ReferenceEquals(result, di.ActivateCommand) || ReferenceEquals(result, ti.ActivateCommand)) return new XElement("ActivateCommand");
        if (result is System.Windows.Input.ICommand) return new XElement("Command");
        if (result is object[] values) return new XElement("Array", values.Select(v => Result(v, null, manager, document, tool)));
        var type = result.GetType();
        if (type.IsEnum || result is string || type.IsPrimitive) return new XElement("Scalar", new XAttribute("Type", type.FullName), Convert.ToString(result, CultureInfo.InvariantCulture));
        return new XElement("Object", new XAttribute("Type", type.FullName));
    }
}
