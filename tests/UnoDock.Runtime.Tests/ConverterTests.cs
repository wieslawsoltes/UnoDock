using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoDock;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Converters;
using UnoDock.Layout;

namespace UnoDock.Testing;
/// <summary>Replays independently captured original public converter calls. Expected results
/// are read from checked-in fixtures, never computed by a second copy of the implementation.</summary>
public static partial class ConverterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        using var manager = new DockingManager();
        var document = new LayoutDocument
        {
            Title = "Document",
            ContentId = "document"
        };
        var tool = new LayoutAnchorable
        {
            Title = "Tool",
            ContentId = "tool"
        };
        manager.Layout.RootPanel.Children.Add(new LayoutDocumentPane(document));
        manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(tool));
        using var fixture = typeof(ConverterTests).Assembly.GetManifestResourceStream("ConverterFixtures.converters.xml") ?? throw new InvalidOperationException("Missing original converter observations.");
        var cases = XDocument.Load(fixture).Root!.Elements().ToArray();
        tests.Test("complete original converter fixture matrix", () => Check.Equal(1380, cases.Length));
        var converters = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var record in cases)
        {
            var name = (string? )record.Attribute("Converter") ?? nameof(AnchorableContextMenuHideVisibilityConverter);
            var key = (string)record.Attribute("Input")!;
            var reverse = (bool)record.Attribute("Reverse")!;
            var targetKey = (string? )record.Attribute("Target") ?? "visibility";
            if (!converters.TryGetValue(name, out var converter))
                converters[name] = converter = Activator.CreateInstance(typeof(DockingManager).Assembly.GetType("UnoDock.Converters." + name, true)!)!;
            tests.Test($"reference {name}: {key} -> {targetKey}, reverse={reverse}", () =>
            {
                var input = record.Name == "MultiCase" ? Values(key) : Input(key, document, tool);
                var methodName = reverse ? "ConvertBack" : "Convert";
                var targetType = Target(targetKey);
                var expected = record.Elements().Single();
                Func<object?> invoke = record.Name == "MultiCase" ? () => reverse ? ((IMultiValueConverter)converter).ConvertBack(Visibility.Visible, [typeof(bool), typeof(bool)], null!, Invariant) : ((IMultiValueConverter)converter).Convert((object[])input!, targetType, null!, Invariant) : () => Invoke(converter, methodName, input, targetType);
                Verify(expected, invoke, input, manager, document, tool, name);
            });
        }

        tests.Test("DoNothing is distinct from UnsetValue", () => Check.False(ReferenceEquals(BindingValue.DoNothing, DependencyProperty.UnsetValue)));
        tests.Test("native adapter explicitly returns fallback sentinel", () =>
        {
            IValueConverter converter = new NullToDoNothingConverter();
            Check.Same(DependencyProperty.UnsetValue, converter.Convert(null!, typeof(object), null!, ""));
            Check.Same(BindingValue.DoNothing, new NullToDoNothingConverter().Convert(null!, typeof(object), null!, Invariant));
        });
        tests.Test("native language overload preserves valid conversion", () => Check.Equal(Visibility.Collapsed, new InverseBoolToVisibilityConverter().Convert(true, typeof(Visibility), null!, "pl-PL")));
        tests.Test("native reverse direction retains original exception", () => Check.Throws<NotImplementedException>(() => new AnchorSideToAngleConverter().ConvertBack(90d, typeof(AnchorSide), null!, "")));
        tests.Test("attached tool commands retain exact identities", () =>
        {
            var item = (LayoutAnchorableItem)manager.GetLayoutItemFromModel(tool);
            Check.Same(item.AutoHideCommand, new AutoHideCommandLayoutItemFromLayoutModelConverter().Convert(tool, typeof(object), null!, Invariant));
            Check.Same(item.HideCommand, new HideCommandLayoutItemFromLayoutModelConverter().Convert(tool, typeof(object), null!, Invariant));
        });
        tests.Test("item converter does not reinterpret an item as a model", () => Check.Equal<object?>(null, new LayoutItemFromLayoutModelConverter().Convert(manager.GetLayoutItemFromModel(document), typeof(object), null!, Invariant)));
        tests.Test("detached former document does not retain a manager command", () =>
        {
            var model = new LayoutDocument();
            var pane = new LayoutDocumentPane(model);
            manager.Layout.RootPanel.Children.Add(pane);
            var converter = new ActivateCommandLayoutItemFromLayoutModelConverter();
            Check.True(converter.Convert(model, typeof(object), null!, Invariant) != null);
            pane.Children.Remove(model);
            Check.Equal<object?>(null, converter.Convert(model, typeof(object), null!, Invariant));
        });
        tests.Test("converter declaration attributes", () =>
        {
            var attribute = typeof(AnchorSideToAngleConverter).GetCustomAttribute<ValueConversionAttribute>()!;
            Check.Equal(typeof(AnchorSide), attribute.SourceType);
            Check.Equal(typeof(double), attribute.TargetType);
            Check.Equal(typeof(object), typeof(AnchorSideToAngleConverter).BaseType);
            Check.False(typeof(AnchorSideToAngleConverter).GetMethod("Convert", [typeof(object), typeof(Type), typeof(object), typeof(CultureInfo)])!.IsVirtual);
        });
        tests.Test("auto-hide caption respects culture-specific strings", () =>
        {
            var old = UnoDock.Properties.Resources.Culture;
            var translations = UnoDock.Properties.Resources.Translations;
            translations.TryGetValue("pl-PL", out var previous);
            try
            {
                translations["pl-PL"] = new Dictionary<string, string>
                {
                    ["Window_Restore"] = "Przywróć",
                    ["Anchorable_AutoHide"] = "Ukryj automatycznie"
                };
                UnoDock.Properties.Resources.Culture = CultureInfo.GetCultureInfo("pl-PL");
                var converter = new AnchorableContextMenuAutoHideHeaderConverter();
                Check.Equal("Przywróć", converter.Convert(true, typeof(string), null!, Invariant));
                Check.Equal("Ukryj automatycznie", converter.Convert(false, typeof(string), null!, Invariant));
            }
            finally
            {
                UnoDock.Properties.Resources.Culture = old;
                if (previous != null)
                    translations["pl-PL"] = previous;
                else
                    translations.Remove("pl-PL");
            }
        });
        ExtendedTests(tests, manager, document, tool);
        BindingTests(tests);
        return await tests.Run(output, "converters");
    }

    private static object? Invoke(object converter, string method, object? input, Type target)
    {
        try
        {
            return converter.GetType().GetMethod(method, [typeof(object), typeof(Type), typeof(object), typeof(CultureInfo)])!.Invoke(converter, [input, target, null, Invariant]);
        }
        catch (TargetInvocationException error)when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static Type Target(string key) => key switch
    {
        "bool" => typeof(bool),
        "nullable-bool" => typeof(bool? ),
        "visibility" => typeof(Visibility),
        _ => typeof(object)};
    private static object? Input(string key, LayoutDocument document, LayoutAnchorable tool) => key switch
    {
        "null" => null,
        "true" => true,
        "false" => false,
        "zero" => 0,
        "one" => 1,
        "string" => "text",
        "visible" => Visibility.Visible,
        "collapsed" => Visibility.Collapsed,
        "left" => AnchorSide.Left,
        "right" => AnchorSide.Right,
        "top" => AnchorSide.Top,
        "bottom" => AnchorSide.Bottom,
        "invalid-side" => (AnchorSide)999,
        "document" => new LayoutDocument(),
        "tool" => new LayoutAnchorable(),
        "attached-document" => document,
        "attached-tool" => tool,
        _ => throw new ArgumentOutOfRangeException(nameof(key))};
    private static object[] Values(string key) => key == "empty" ? [] : key.Split(',').Select(value => value == "null" ? null! : (object)bool.Parse(value)).ToArray();
    private static void Verify(XElement expected, Func<object?> invoke, object? input, DockingManager manager, LayoutDocument document, LayoutAnchorable tool, string converter)
    {
        if (expected.Name == "Exception")
        {
            Exception? actualException = null;
            try
            {
                invoke();
            }
            catch (Exception error)
            {
                actualException = error;
            }

            Check.Equal((string? )expected.Attribute("Type"), actualException?.GetType().FullName);
            return;
        }

        var result = invoke();
        switch (expected.Name.LocalName)
        {
            case "Null":
                Check.Equal<object?>(null, result);
                break;
            case "DoNothing":
                Check.Same(BindingValue.DoNothing, result);
                break;
            case "UnsetValue":
                Check.Same(DependencyProperty.UnsetValue, result);
                break;
            case "InputIdentity":
                Check.Same(input, result);
                break;
            case "DocumentItem":
                Check.Same(manager.GetLayoutItemFromModel(document), result);
                break;
            case "ToolItem":
                Check.Same(manager.GetLayoutItemFromModel(tool), result);
                break;
            case "ActivateCommand":
                Check.Same(manager.GetLayoutItemFromModel((LayoutContent)input!).ActivateCommand, result);
                break;
            case "Command":
                var item = (LayoutAnchorableItem)manager.GetLayoutItemFromModel(tool);
                Check.Same(converter.StartsWith("AutoHide", StringComparison.Ordinal) ? item.AutoHideCommand : item.HideCommand, result);
                break;
            case "Scalar":
                var type = (string)expected.Attribute("Type")!;
                if (type == "System.Windows.Visibility")
                    type = typeof(Visibility).FullName!;
                if (type == "System.Windows.Controls.Orientation")
                    type = typeof(Orientation).FullName!;
                Check.Equal(type.Replace("Xceed.Wpf.AvalonDock", "UnoDock", StringComparison.Ordinal), result?.GetType().FullName);
                Check.Equal(expected.Value, Convert.ToString(result, Invariant));
                break;
            case "Object" when (string? )expected.Attribute("Type") == "System.Windows.Controls.Image":
                Check.True(result is Image { Source: BitmapImage });
                Check.Equal(input, ((BitmapImage)((Image)result!).Source).UriSource);
                break;
            case "Image":
                Check.True(result is Image { Source: BitmapImage });
                var image = (Image)result!;
                Check.Equal((bool)expected.Attribute("SameUri")!, Equals(input, ((BitmapImage)image.Source).UriSource));
                Check.Equal((string? )expected.Attribute("Stretch"), image.Stretch.ToString());
                break;
            default:
                throw new InvalidOperationException("Unrecognized reference result: " + expected);
        }
    }

    private static void BindingTests(TestRunner tests)
    {
        tests.Test("DoNothing transfer preserves value and ignores fallback", () =>
        {
            var target = new ContentControl
            {
                Content = "retained"
            };
            var source = new Source();
            var converter = new NullToDoNothingConverter();
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => source.Value, value => converter.Convert(value!, typeof(object), null!, Invariant), [source], fallbackValue: "fallback", useFallbackValue: true);
            Check.Equal("retained", target.Content);
            source.Value = "next";
            Check.Equal("next", target.Content);
            source.Value = null;
            Check.Equal("next", target.Content);
        });
        tests.Test("UnsetValue explicitly uses fallback", () =>
        {
            var target = new ContentControl
            {
                Content = "old"
            };
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => null, _ => DependencyProperty.UnsetValue, fallbackValue: "fallback", useFallbackValue: true);
            Check.Equal("fallback", target.Content);
        });
        tests.Test("UnsetValue without fallback retains destination", () =>
        {
            var target = new ContentControl
            {
                Content = "old"
            };
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => null, _ => DependencyProperty.UnsetValue);
            Check.Equal("old", target.Content);
        });
        tests.Test("null fallback is a value, not a missing option", () =>
        {
            var target = new ContentControl
            {
                Content = "old"
            };
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => null, _ => DependencyProperty.UnsetValue, useFallbackValue: true);
            Check.Equal<object?>(null, target.Content);
        });
        tests.Test("binding unregisters all subscriptions on disposal", () =>
        {
            var target = new ContentControl();
            var source = new Source
            {
                Value = "first"
            };
            var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => source.Value, value => value, [source, source]);
            Check.Equal(1, source.Subscribers);
            binding.Dispose();
            binding.Dispose();
            Check.Equal(0, source.Subscribers);
            source.Value = "second";
            Check.Equal("first", target.Content);
            Check.Throws<ObjectDisposedException>(binding.UpdateTarget);
        });
        tests.Test("binding constructor failure removes subscriptions", () =>
        {
            var source = new Source();
            Check.Throws<FormatException>(() => new ConverterBinding(new ContentControl(), ContentControl.ContentProperty, () => null, _ => throw new FormatException(), [source]));
            Check.Equal(0, source.Subscribers);
        });
        tests.Test("two-way DoNothing suppresses source writes", () =>
        {
            var target = new ContentControl();
            var source = new Source
            {
                Value = "source"
            };
            var writes = 0;
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => source.Value, value => value, [source], writeSource: value =>
            {
                writes++;
                source.Value = value;
            }, convertBack: value => Equals(value, "skip") ? BindingValue.DoNothing : value);
            Check.Equal(0, writes);
            target.Content = "skip";
            Check.Equal(0, writes);
            Check.Equal("source", source.Value);
            target.Content = "edited";
            Check.Equal(1, writes);
            Check.Equal("edited", source.Value);
        });
        tests.Test("two-way UnsetValue suppresses source writes", () =>
        {
            var target = new ContentControl();
            var writes = 0;
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => "source", value => value, writeSource: _ => writes++, convertBack: _ => DependencyProperty.UnsetValue, fallbackValue: "fallback", useFallbackValue: true);
            target.Content = "edited";
            Check.Equal(0, writes);
        });
        tests.Test("source notification reentrancy converges to newest value", () =>
        {
            var target = new ContentControl();
            var source = new Source
            {
                Value = 0
            };
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => source.Value, value =>
            {
                if ((int)value! < 3)
                    source.Value = (int)value + 1;
                return value;
            }, [source]);
            Check.Equal(3, target.Content);
        });
        tests.Test("non-convergent source notification fails without leaked observer", () =>
        {
            var source = new Source
            {
                Value = 0
            };
            Check.Throws<InvalidOperationException>(() => new ConverterBinding(new ContentControl(), ContentControl.ContentProperty, () => source.Value, value =>
            {
                source.Value = (int)value! + 1;
                return value;
            }, [source]));
            Check.Equal(0, source.Subscribers);
        });
        tests.Test("property-name filter and all-properties notification", () =>
        {
            var source = new Source
            {
                Value = "first"
            };
            var target = new ContentControl();
            var reads = 0;
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () =>
            {
                reads++;
                return source.Value;
            }, v => v, [source], sourceProperty: "Value");
            source.Notify("Other");
            Check.Equal(1, reads);
            source.Notify(null);
            Check.Equal(2, reads);
        });
        tests.Test("multiple notification sources update one projection", () =>
        {
            var left = new Source
            {
                Value = "a"
            };
            var right = new Source
            {
                Value = "b"
            };
            var target = new ContentControl();
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => $"{left.Value}:{right.Value}", v => v, [left, right]);
            Check.Equal("a:b", target.Content);
            right.Value = "c";
            Check.Equal("a:c", target.Content);
        });
        tests.Test("background source notifications marshal to UI thread", async () =>
        {
            var source = new Source
            {
                Value = "old"
            };
            var target = new ContentControl();
            var uiThread = Environment.CurrentManagedThreadId;
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () =>
            {
                Check.Equal(uiThread, Environment.CurrentManagedThreadId);
                return source.Value;
            }, v => v, [source]);
            await Task.Run(() => source.Value = "background");
            for (var i = 0; i < 50 && !Equals(target.Content, "background"); i++)
                await Task.Delay(10);
            Check.Equal("background", target.Content);
        });
        tests.Test("native binding cannot be replaced accidentally", () =>
        {
            var target = new ContentControl();
            target.SetBinding(ContentControl.ContentProperty, new Binding { Source = "native" });
            Check.Throws<InvalidOperationException>(() => new ConverterBinding(target, ContentControl.ContentProperty, () => "ours", v => v));
        });
        tests.Test("second converter binding cannot take over a target", () =>
        {
            var target = new ContentControl();
            using var first = new ConverterBinding(target, ContentControl.ContentProperty, () => "first", v => v);
            Check.Throws<InvalidOperationException>(() => new ConverterBinding(target, ContentControl.ContentProperty, () => "second", v => v));
            Check.Equal("first", target.Content);
            first.Dispose();
            using var next = new ConverterBinding(target, ContentControl.ContentProperty, () => "next", v => v);
            Check.Equal("next", target.Content);
        });
        tests.Test("constructor rollback releases binding property ownership", () =>
        {
            var target = new ContentControl();
            Check.Throws<FormatException>(() => new ConverterBinding(target, ContentControl.ContentProperty, () => null, _ => throw new FormatException()));
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => "recovered", v => v);
            Check.Equal("recovered", target.Content);
        });
        tests.Test("native binding installed after attachment is not overwritten", () =>
        {
            var target = new ContentControl();
            using var binding = new ConverterBinding(target, ContentControl.ContentProperty, () => "first", v => v);
            target.SetBinding(ContentControl.ContentProperty, new Binding { Source = "native" });
            Check.Throws<InvalidOperationException>(binding.UpdateTarget);
            Check.True(target.GetBindingExpression(ContentControl.ContentProperty) != null);
        });
        tests.Test("two-way attachment requires both delegates", () => Check.Throws<ArgumentException>(() => new ConverterBinding(new ContentControl(), ContentControl.ContentProperty, () => null, v => v, writeSource: _ =>
        {
        })));
    }

    private sealed class Source : INotifyPropertyChanged
    {
        private object? _value;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }

        public object? Value
        {
            get => _value;
            set
            {
                _value = value;
                Notify(nameof(Value));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _changed += value;
                Subscribers++;
            }

            remove
            {
                _changed -= value;
                Subscribers--;
            }
        }

        public void Notify(string? propertyName) => _changed?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
