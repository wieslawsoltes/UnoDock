using Microsoft.UI.Xaml.Markup;
using UnoDock.Controls;
using UnoDock.Themes;
using Windows.Foundation;
using System.Xml.Linq;

namespace UnoDock.Testing;

public static class DockGuideTests
{
    public static async Task<int> Run(DockingManager templateSource, string output)
    {
        var tests = new TestRunner();
        using var dock = new DockingManager
        {
            Width = 1000,
            Height = 640,
            Template = templateSource.Template,
            RequestedTheme = ElementTheme.Light,
            FloatingWindowMode = FloatingWindowMode.InSurface,
            CrossWindowCoordinates = null
        };
        var overlay = new OverlayWindow
        {
            Width = 1000,
            Height = 640
        };
        var scene = new Grid
        {
            Width = 1000,
            Height = 640
        };
        scene.Children.Add(dock);
        scene.Children.Add(overlay);
        var window = new Window
        {
            Content = scene,
            Title = "UnoDock guide regression laboratory"
        };
        window.AppWindow.Resize(new()
        {
            Width = 1064,
            Height = 740
        });
        window.Activate();
        var registration = Microsoft.Windows.Shell.SystemCommands.RegisterWindow(window);
        try
        {
            tests.Test("default mode requires explicit guides rather than guessing a body drop", () =>
            {
                using var manager = new DockingManager();
                Check.Equal(DockingGuideMode.GuidesOnly, manager.DockingGuideMode);
                Check.False(manager.ShowDocumentPaneToolGuides);
            });
            tests.Test("document compass exposes only document pane targets", async () =>
            {
                await Reset();
                var guides = Guides(Document());
                Check.Equal(5, guides.Count);
                Check.True(guides.All(g => g.Type >= DropTargetType.DocumentPaneDockLeft && g.Type <= DropTargetType.DocumentPaneDockInside));
            });
            tests.Test("extended tool compass exposes root, document and separate tool targets", async () =>
            {
                await Reset();
                var guides = Guides(Tool());
                Check.Equal(13, guides.Count);
                Check.Equal(4, guides.Count(g => g.Type >= DropTargetType.DocumentPaneDockAsAnchorableLeft));
            });
            tests.Test("each displayed guide resolves to the same validated intent", async () =>
            {
                await Reset();
                var content = Tool();
                foreach (var guide in Guides(content))
                {
                    var plan = dock.GetDropPlan(content, Center(guide.DetectionRect));
                    Check.True(plan != null);
                    Check.Equal(guide.Type, plan!.Type);
                    Check.Same(guide.Plan.Target, plan.Target);
                }
            });
            tests.Test("root-edge guide wins over the underlying tool pane", async () =>
            {
                await Reset();
                var c = Tool();
                var guide = Guides(c).Single(g => g.Type == DropTargetType.DockingManagerDockLeft);
                var p = dock.GetDropPlan(c, Center(guide.DetectionRect))!;
                Check.Same(dock.Layout.RootPanel, p.Target);
                Check.Equal(DropTargetType.DockingManagerDockLeft, p.Type);
            });
            tests.Test("guide-only mode leaves neutral pane content undocked", async () =>
            {
                await Reset();
                dock.DockingGuideMode = DockingGuideMode.GuidesOnly;
                Check.True(dock.GetDropPlan(Tool(), new(310, 170)) == null);
                var center = Guides(Tool()).Single(g => g.Type == DropTargetType.DocumentPaneDockInside);
                Check.True(dock.GetDropPlan(Tool(), Center(center.DetectionRect)) != null);
            });
            tests.Test("guide-only mode keeps visible tab insertion", async () =>
            {
                await Reset();
                dock.DockingGuideMode = DockingGuideMode.GuidesOnly;
                var tab = dock.FindVisualChildren<LayoutDocumentTabItem>().First(t => ReferenceEquals(t.Model, Document()));
                var point = tab.TransformToVisual(dock).TransformPoint(new(6, 6));
                Check.Equal(DropTargetType.DocumentPaneDockInside, dock.GetDropPlan(Tool(), point)!.Type);
            });
            tests.Test("edge-only compatibility mode suppresses glyphs and retains legacy zones", async () =>
            {
                await Reset();
                dock.DockingGuideMode = DockingGuideMode.EdgesOnly;
                Check.Equal(0, Guides(Tool()).Count);
                Check.True(dock.GetDropPlan(Tool(), new(310, 170)) != null);
            });
            tests.Test("CanMove removes document guide intents", async () =>
            {
                await Reset();
                var c = Document();
                c.CanMove = false;
                Check.Equal(0, Guides(c).Count);
            });
            tests.Test("CanRepositionItems removes source tool guide intents", async () =>
            {
                await Reset();
                var c = Tool();
                ((LayoutAnchorablePane)c.Parent!).CanRepositionItems = false;
                Check.Equal(0, Guides(c).Count);
            });
            tests.Test("tool docking permission suppresses document glyphs but keeps outer tool glyphs", async () =>
            {
                await Reset();
                var c = Tool();
                c.CanDockAsTabbedDocument = false;
                var guides = Guides(c);
                Check.Equal(8, guides.Count);
                Check.False(guides.Any(g => g.Type >= DropTargetType.DocumentPaneDockLeft && g.Type <= DropTargetType.DocumentPaneDockInside));
            });
            tests.Test("mixed-orientation permission filters compass before hit testing", async () =>
            {
                await Reset();
                var c = Document();
                var pane = (LayoutDocumentPane)c.Parent!;
                var parent = (ILayoutGroup)pane.Parent!;
                var group = new LayoutDocumentPaneGroup
                {
                    Orientation = Orientation.Horizontal
                };
                parent.ReplaceChild(pane, group);
                group.Children.Add(pane);
                var second = new LayoutDocumentPane(c);
                group.Children.Add(second);
                await Settle();
                dock.AllowMixedOrientation = false;
                var target = dock.FindVisualChildren<LayoutDocumentPaneControl>().Single(v => ReferenceEquals(v.Model, pane));
                var point = target.TransformToVisual(dock).TransformPoint(new(target.ActualWidth / 2, target.ActualHeight / 2));
                var guides = dock.GetDockingGuides(c, point);
                Check.False(guides.Any(g => g.Type is DropTargetType.DocumentPaneDockTop or DropTargetType.DocumentPaneDockBottom));
            });
            tests.Test("root replacement invalidates snapshots and prevents execution", async () =>
            {
                await Reset();
                var g = Guides(Tool()).First();
                dock.Layout = new();
                Check.False(g.Plan.CanExecute);
                Check.False(g.HitTest(Center(g.DetectionRect)));
                Check.False(g.Plan.Execute());
            });
            tests.Test("capability changes invalidate previously displayed guide", async () =>
            {
                await Reset();
                var c = Document();
                var g = Guides(c).First();
                c.CanMove = false;
                Check.False(g.HitTest(Center(g.DetectionRect)));
                Check.False(g.Plan.Execute());
            });
            tests.Test("foreign manager models have no displayed intents", async () =>
            {
                await Reset();
                using var other = new DockingManager();
                var c = new LayoutDocument();
                other.Layout = new()
                {
                    RootPanel = new(new LayoutDocumentPane(c))
                };
                Check.Equal(0, Guides(c).Count);
            });
            tests.Test("outer tool glyph executes as a tool pane beside documents", async () =>
            {
                await Reset();
                var c = Tool();
                var g = Guides(c).Single(g => g.Type == DropTargetType.DocumentPaneDockAsAnchorableLeft);
                Check.True(g.Plan.Execute());
                Check.True(c.Parent is LayoutAnchorablePane);
                Check.False(ReferenceEquals(c.Parent, g.Plan.Target));
            });
            tests.Test("inner tool glyph executes as a document tab", async () =>
            {
                await Reset();
                var c = Tool();
                var g = Guides(c).Single(g => g.Type == DropTargetType.DocumentPaneDockInside);
                Check.True(g.Plan.Execute());
                Check.Same(g.Plan.Target, c.Parent);
                Check.True(c.Parent is LayoutDocumentPane);
            });
            tests.Test("preview cancellation preserves layout through guide execution", async () =>
            {
                await Reset();
                var c = Tool();
                var parent = c.Parent;
                var g = Guides(c).Last();
                RoutedEventHandler veto = (_, e) => ((DockEventArgs)e).Cancel = true;
                dock.PreviewDock += veto;
                try
                {
                    Check.False(g.Plan.Execute());
                    Check.Same(parent, c.Parent);
                }
                finally
                {
                    dock.PreviewDock -= veto;
                }
            });
            tests.Test("overlay remains open over neutral space without guessing a preview", async () =>
            {
                await Reset();
                var guides = Guides(Tool());
                overlay.ShowGuides(guides, null, dock);
                await Settle();
                Check.True(overlay.IsOpen);
                Check.True(overlay.CurrentPlan == null);
                Check.Equal(13, overlay.DisplayedGuides.Count);
            });
            tests.Test("overlay renders all guide regions at their hit-test rectangles", async () =>
            {
                await Reset();
                var guides = Guides(Tool());
                overlay.ShowGuides(guides, guides[4].Plan, dock);
                await Settle();
                foreach (var g in guides)
                {
                    var v = Glyph(g.Type);
                    var r = v.TransformToVisual(overlay).TransformBounds(new(0, 0, v.ActualWidth, v.ActualHeight));
                    Check.Near(g.DetectionRect.X, r.X, 1);
                    Check.Near(g.DetectionRect.Y, r.Y, 1);
                    Check.Near(g.DetectionRect.Width, r.Width, 1);
                    Check.Near(g.DetectionRect.Height, r.Height, 1);
                }
            });
            tests.Test("guide containers survive hover and palette changes", async () =>
            {
                await Reset();
                var c = Tool();
                var guides = Guides(c);
                overlay.ShowGuides(guides, null, dock);
                await Settle();
                var v = Glyph(guides[4].Type);
                overlay.ShowGuides(Guides(c), guides[4].Plan, dock);
                Check.Same(v, Glyph(guides[4].Type));
                dock.Theme = new FluentTheme(ElementTheme.Dark);
                dock.Refresh();
                overlay.ShowGuides(Guides(c), null, dock);
                Check.Same(v, Glyph(guides[4].Type));
            });
            tests.Test("overlay never captures pointer or focus", async () =>
            {
                await Reset();
                overlay.ShowGuides(Guides(Tool()), null, dock);
                Check.False(overlay.IsHitTestVisible);
                Check.False(overlay.IsTabStop);
            });
            tests.Test("hide releases plan and target references", async () =>
            {
                await Reset();
                overlay.ShowGuides(Guides(Tool()), null, dock);
                overlay.Hide();
                Check.False(overlay.IsOpen);
                Check.Equal(0, overlay.DisplayedGuides.Count);
                Check.False(overlay.FindVisualChildren<FrameworkElement>().Any(e => e.Name.StartsWith("Guide_", StringComparison.Ordinal)));
            });
            tests.Test("duplicate identity rejected before replacing current overlay", async () =>
            {
                await Reset();
                var g = Guides(Tool()).First();
                overlay.ShowGuides([g], g.Plan, dock);
                Check.Throws<ArgumentException>(() => overlay.ShowGuides([g, g], null, dock));
                Check.Same(g.Plan, overlay.CurrentPlan);
            });
            tests.Test("overlay rejects stale intent rather than drawing a forbidden target", async () =>
            {
                await Reset();
                var c = Tool();
                var g = Guides(c);
                ((LayoutAnchorablePane)c.Parent!).CanRepositionItems = false;
                overlay.ShowGuides(g, g[0].Plan, dock);
                Check.False(overlay.IsOpen);
                Check.Equal(0, overlay.DisplayedGuides.Count);
            });
            tests.Test("guide size override changes hit regions and rendering together", async () =>
            {
                await Reset();
                dock.Resources["UnoDock.GuideSize"] = 44d;
                var guides = Guides(Tool());
                overlay.ShowGuides(guides, null, dock);
                await Settle();
                foreach (var g in guides)
                {
                    Check.Near(g.Type <= DropTargetType.DockingManagerDockBottom ? g.Type is DropTargetType.DockingManagerDockLeft or DropTargetType.DockingManagerDockRight ? 48 : 43.5 : 44, g.DetectionRect.Width);
                    Check.Near(g.DetectionRect.Width, Glyph(g.Type).ActualWidth, 1);
                }
            });
            tests.Test("invalid density falls back without propagating NaN", async () =>
            {
                await Reset();
                dock.Resources["UnoDock.GuideSize"] = double.NaN;
                Check.True(Guides(Document()).All(g => Math.Abs(g.DetectionRect.Width - 88d / 3) < 1e-6));
            });
            tests.Test("invalid dependency-property mode is rolled back before failure", () =>
            {
                var original = dock.DockingGuideMode;
                Check.Throws<ArgumentOutOfRangeException>(() => dock.SetValue(DockingManager.DockingGuideModeProperty, (DockingGuideMode)999));
                Check.Equal(original, dock.DockingGuideMode);
            });
            tests.Test("RTL guide rendering and hit regions share the same coordinate frame", async () =>
            {
                await Reset();
                scene.FlowDirection = FlowDirection.RightToLeft;
                await Settle();
                var guides = Guides(Tool());
                overlay.ShowGuides(guides, null, dock);
                await Settle();
                foreach (var g in guides)
                {
                    var v = Glyph(g.Type);
                    var r = v.TransformToVisual(overlay).TransformBounds(new(0, 0, v.ActualWidth, v.ActualHeight));
                    Check.Near(g.DetectionRect.X, r.X, 1);
                    Check.Near(g.DetectionRect.Y, r.Y, 1);
                }
            });
            tests.Test("invalid pointer and mode arguments fail immediately", async () =>
            {
                await Reset();
                Check.Throws<ArgumentOutOfRangeException>(() => dock.GetDockingGuides(Tool(), new(double.NaN, 0)));
                Check.Throws<ArgumentOutOfRangeException>(() => dock.DockingGuideMode = (DockingGuideMode)999);
            });
            tests.Test("custom overlay canvas accepts retained guide visuals", async () =>
            {
                await Reset();
                var guides = Guides(Tool());
                overlay.ShowGuides(guides, null, dock);
                await Settle();
                var v = Glyph(guides[0].Type);
                overlay.Template = (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Canvas x:Name='PART_DockingGuideCanvas'/></ControlTemplate>");
                overlay.ApplyTemplate();
                await Settle();
                overlay.ShowGuides(guides, null, dock);
                Check.Same(v, Glyph(guides[0].Type));
                overlay.ClearValue(Control.TemplateProperty);
                overlay.ApplyTemplate();
            });
            foreach (var toolMode in new[]
            {
                false,
                true
            }

            )
            {
                var useTool = toolMode;
                tests.Test("original stock guide geometry replay: " + (useTool ? "tool" : "document"), async () =>
                {
                    await Reset();
                    dock.ShowDocumentPaneToolGuides = false;
                    dock.DockingGuideMode = DockingGuideMode.GuidesOnly;
                    var target = new LayoutDocumentPane(new LayoutDocument { Title = "Target", ContentId = "target" });
                    var tools = new LayoutAnchorablePane
                    {
                        DockWidth = new(200)
                    };
                    var source = useTool ? (LayoutContent)new LayoutAnchorable
                    {
                        Title = "Dragged tool"
                    }

                    : new LayoutDocument
                    {
                        Title = "Dragged document"
                    };
                    if (useTool)
                        tools.Children.Add((LayoutAnchorable)source);
                    else
                        target.Children.Add(source);
                    tools.Children.Add(new LayoutAnchorable { Title = "Other tool" });
                    var panel = new LayoutPanel(tools);
                    panel.Children.Add(target);
                    dock.Layout = new()
                    {
                        RootPanel = panel
                    };
                    source.FloatingLeft = 40;
                    source.FloatingTop = 60;
                    source.FloatingWidth = 280;
                    source.FloatingHeight = 180;
                    source.Float();
                    await Settle();
                    var area = dock.GetDropAreas().OfType<DropArea<FrameworkElement>>().Single(a => a.AreaElement is ILayoutControl c && ReferenceEquals(c.Model, target));
                    var guides = dock.GetDockingGuides(source, Center(area.DetectionRect));
                    var file = useTool ? "guides-tool-message.xml" : "guides-document-message.xml";
                    using var stream = typeof(DockGuideTests).Assembly.GetManifestResourceStream("VisualFixtures." + file)!;
                    var reference = XDocument.Load(stream);
                    var named = reference.Root!.Elements("Element").Where(e => MapReference((string?)e.Attribute("name")) != null).ToArray();
                    Check.Equal(named.Length, guides.Count);
                    overlay.ShowGuides(guides, null, dock);
                    await Settle();
                    var differences = new XElement("GuideGeometry", new XAttribute("referenceProbe", "5298acd6024c134925286fc80b83b04608a17304"));
                    foreach (var item in named)
                    {
                        var type = MapReference((string?)item.Attribute("name"))!.Value;
                        var g = guides.Single(g => g.Type == type);
                        var visual = Glyph(type);
                        var actual = visual.TransformToVisual(overlay).TransformBounds(new(0, 0, visual.ActualWidth, visual.ActualHeight));
                        var expected = new Rect((double)item.Attribute("x")!, (double)item.Attribute("y")!, (double)item.Attribute("width")!, (double)item.Attribute("height")!);
                        Check.Near(expected.X, g.DetectionRect.X, 1);
                        Check.Near(expected.Y, g.DetectionRect.Y, 1);
                        Check.Near(expected.Width, g.DetectionRect.Width, 1);
                        Check.Near(expected.Height, g.DetectionRect.Height, 1);
                        Check.Near(expected.X, actual.X, 1);
                        Check.Near(expected.Y, actual.Y, 1);
                        Check.Near(expected.Width, actual.Width, 1);
                        Check.Near(expected.Height, actual.Height, 1);
                        differences.Add(new XElement("Guide", new XAttribute("type", type), new XAttribute("dx", actual.X - expected.X), new XAttribute("dy", actual.Y - expected.Y), new XAttribute("dWidth", actual.Width - expected.Width), new XAttribute("dHeight", actual.Height - expected.Height)));
                    }

                    var directory = Path.Combine(output, "visuals");
                    Directory.CreateDirectory(directory);
                    var name = useTool ? "guides-reference-tool" : "guides-reference-document";
                    new XDocument(differences).Save(Path.Combine(directory, name + ".xml"));
                    await VisualCapture.Save(overlay, Path.Combine(directory, name + ".png"));
                });
            }

            foreach (var name in new[]
            {
                "guides-document",
                "guides-tool",
                "guides-tool-active",
                "guides-dark",
                "guides-rtl"
            }

            )
            {
                var scenario = name;
                tests.Test("capture actual docking compass: " + name, async () =>
                {
                    await Reset();
                    if (scenario == "guides-dark")
                        dock.Theme = new FluentTheme(ElementTheme.Dark);
                    if (scenario == "guides-rtl")
                        scene.FlowDirection = FlowDirection.RightToLeft;
                    var content = scenario == "guides-document" ? (LayoutContent)Document() : Tool();
                    await Settle();
                    var guides = Guides(content);
                    Check.True(guides.Count > 0);
                    overlay.ShowGuides(guides, scenario == "guides-tool-active" ? guides.Last().Plan : null, dock);
                    await Settle();
                    Check.True(overlay.FindVisualChildren<FrameworkElement>().Count(v => v.Name.StartsWith("Guide_", StringComparison.Ordinal)) == guides.Count);
                    var directory = Path.Combine(output, "visuals");
                    Directory.CreateDirectory(directory);
                    await VisualCapture.Save(scene, Path.Combine(directory, scenario + ".png"));
                    new XDocument(new XElement("Guides", new XAttribute("scenario", scenario), guides.Select(g => new XElement("Target", new XAttribute("type", g.Type), new XAttribute("x", g.DetectionRect.X), new XAttribute("y", g.DetectionRect.Y), new XAttribute("width", g.DetectionRect.Width), new XAttribute("height", g.DetectionRect.Height))))).Save(Path.Combine(directory, scenario + ".xml"));
                });
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                tests.Test("native guide geometry projects outside the primary client and executes in the target root", async () =>
                {
                    await WithNativeTarget(async (source, target, native, coordinates) =>
                    {
                        var point = coordinates.Translate(native, new(native.ActualWidth / 2, native.ActualHeight / 2), dock);
                        var guides = dock.GetDockingGuides(source, point);
                        Check.Equal(5, guides.Count);
                        Check.True(guides.All(g => g.Type >= DropTargetType.AnchorablePaneDockLeft && g.Type <= DropTargetType.AnchorablePaneDockInside));
                        foreach (var guide in guides)
                        {
                            var projected = coordinates.Translate(dock, Center(guide.DetectionRect), native);
                            Check.True(projected.X >= 0 && projected.Y >= 0 && projected.X < native.ActualWidth && projected.Y < native.ActualHeight);
                            Check.Equal(guide.Type, dock.GetDropPlan(source, Center(guide.DetectionRect))!.Type);
                        }

                        var view = dock.GetLayoutItemFromModel(source).View;
                        var parent = target.Parent;
                        Check.True(guides.Single(g => g.Type == DropTargetType.AnchorablePaneDockInside).Plan.Execute());
                        await Settle();
                        Check.Same(parent, source.Parent);
                        Check.Same(view, dock.GetLayoutItemFromModel(source).View);
                    });
                });
                tests.Test("strict native client rejects blank-body drop while retaining visible guides", async () =>
                {
                    await WithNativeTarget(async (source, target, native, coordinates) =>
                    {
                        var point = coordinates.Translate(native, new(55, native.ActualHeight - 65), dock);
                        Check.Equal(5, dock.GetDockingGuides(source, point).Count);
                        Check.True(dock.GetDropPlan(source, point) == null);
                        await Task.CompletedTask;
                    });
                });
            }

            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            {
                tests.Test("native pointer hits root guide and docks at workspace edge", async () =>
                {
                    await Reset();
                    dock.DockingGuideMode = DockingGuideMode.GuidesOnly;
                    var c = Tool();
                    var target = Guides(c).Single(g => g.Type == DropTargetType.DockingManagerDockRight);
                    var source = dock.FindVisualChildren<LayoutAnchorableTabItem>().First(t => ReferenceEquals(t.Model, c));
                    using var input = new X11TestInput();
                    await input.Begin(source, new(12, 10));
                    await input.Drop(dock, Center(target.DetectionRect));
                    await Settle();
                    Check.True(c.Parent is LayoutAnchorablePane);
                    Check.Equal(AnchorSide.Right, c.GetSide());
                });
                tests.Test("native cross-window blank-body release does not create an unintended floating window", async () =>
                {
                    await WithNativeTarget(async (source, target, native, coordinates) =>
                    {
                        var tab = dock.FindVisualChildren<LayoutAnchorableTabItem>().First(t => ReferenceEquals(t.Model, source));
                        var parent = source.Parent;
                        var count = dock.FloatingWindows.Count();
                        using var input = new X11TestInput();
                        window.Activate();
                        await Task.Delay(120);
                        await input.Begin(tab, new(12, 10));
                        input.MoveTo(native, new(55, native.ActualHeight - 65));
                        await Task.Delay(120);
                        var adorners = native.FindVisualChildren<OverlayWindow>().Single(o => o.DisplayedGuides.Count > 0);
                        Check.Equal(5, adorners.DisplayedGuides.Count);
                        foreach (var guide in adorners.DisplayedGuides)
                        {
                            var visual = adorners.FindVisualChildren<FrameworkElement>().Single(v => v.Name == "Guide_" + guide.Type);
                            var actual = visual.TransformToVisual(adorners).TransformPoint(default);
                            Check.Near(guide.DetectionRect.X, actual.X, 1);
                            Check.Near(guide.DetectionRect.Y, actual.Y, 1);
                        }

                        input.Release();
                        await Settle();
                        Check.Same(parent, source.Parent);
                        Check.False(source.IsFloating);
                        Check.Equal(count, dock.FloatingWindows.Count());
                        Check.False(adorners.IsOpen);
                    });
                });
                tests.Test("native capture cancellation clears all guide adorners", async () =>
                {
                    await Reset();
                    dock.DockingGuideMode = DockingGuideMode.GuidesOnly;
                    var c = Document();
                    var source = dock.FindVisualChildren<LayoutDocumentTabItem>().First(t => ReferenceEquals(t.Model, c));
                    using var input = new X11TestInput();
                    await input.Begin(source, new(12, 10));
                    input.MoveTo(dock, new(600, 320));
                    await Task.Delay(150);
                    Check.True(dock.FindVisualChildren<OverlayWindow>().Any(o => o.DisplayedGuides.Count > 0));
                    input.Escape();
                    input.Release();
                    await Settle();
                    Check.False(dock.FindVisualChildren<OverlayWindow>().Any(o => o.IsOpen));
                });
            }

            return await tests.Run(output, "docking-guides");
        }
        finally
        {
            registration.Dispose();
            overlay.Hide();
            window.Content = null;
            window.Close();
        }

        async Task WithNativeTarget(Func<LayoutAnchorable, LayoutAnchorable, LayoutFloatingWindowControl, DesktopWindowCoordinates, Task> action)
        {
            await Reset();
            using var coordinates = new DesktopWindowCoordinates();
            dock.CrossWindowCoordinates = coordinates;
            dock.FloatingWindowMode = FloatingWindowMode.Native;
            dock.DockingGuideMode = DockingGuideMode.GuidesOnly;
            window.AppWindow.Move(new()
            {
                X = 25,
                Y = 25
            });
            var source = Tool();
            var target = dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(t => t.ContentId == "properties");
            target.FloatingLeft = 1250;
            target.FloatingTop = 100;
            target.FloatingWidth = 420;
            target.FloatingHeight = 320;
            try
            {
                target.Float();
                dock.Refresh();
                var deadline = DateTime.UtcNow.AddSeconds(4);
                while (!dock.FloatingWindows.Any(w => w.NativeWindow != null && w.IsLoaded && w.ActualWidth > 0))
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException("Native guide target did not load.");
                    await Task.Delay(25);
                }

                var native = dock.FloatingWindows.Single(w => w.NativeWindow != null);
                native.NativeWindow!.Activate();
                Rect? previous = null;
                var stable = 0;
                while (stable < 3)
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException("Native guide frame did not finish arranging.");
                    native.UpdateLayout();
                    var frame = FloatingChromeProbe.Bounds(native.NativeWindow!);
                    var scale = native.XamlRoot!.RasterizationScale;
                    var arranged = native.IsLoaded && native.NativeWindow!.AppWindow.IsVisible && Math.Abs(native.ActualWidth * scale - frame.Width) <= 1 && Math.Abs(native.ActualHeight * scale - frame.Height) <= 1 && native.FindVisualChildren<LayoutAnchorablePaneControl>().Any(pane => pane.IsLoaded && pane.ActualWidth > 0 && pane.ActualHeight > 0);
                    var current = new Rect(frame.X, frame.Y, frame.Width, frame.Height);
                    stable = arranged && previous == current ? stable + 1 : 0;
                    previous = current;
                    await Task.Delay(25);
                }

                await action(source, target, native, coordinates);
            }
            finally
            {
                dock.Layout = new();
                dock.Refresh();
                await Task.Delay(40);
                dock.FloatingWindowMode = FloatingWindowMode.InSurface;
                dock.CrossWindowCoordinates = null;
                window.Activate();
            }
        }

        LayoutDocument Document() => dock.Layout.Descendents().OfType<LayoutDocument>().Single(d => d.ContentId == "editor");
        LayoutAnchorable Tool() => dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(t => t.ContentId == "explorer");
        IReadOnlyList<DockGuideTarget> Guides(LayoutContent c)
        {
            var pane = dock.FindVisualChildren<LayoutDocumentPaneControl>().Single(v => ReferenceEquals(v.Model, Document().Parent));
            return dock.GetDockingGuides(c, pane.TransformToVisual(dock).TransformPoint(new(pane.ActualWidth / 2, pane.ActualHeight / 2)));
        }

        FrameworkElement Glyph(DropTargetType type) => overlay.FindVisualChildren<FrameworkElement>().Single(v => v.Name == "Guide_" + type);
        static DropTargetType? MapReference(string? name) => name switch
        {
            "PART_DockingManagerDropTargetLeft" => DropTargetType.DockingManagerDockLeft,
            "PART_DockingManagerDropTargetTop" => DropTargetType.DockingManagerDockTop,
            "PART_DockingManagerDropTargetRight" => DropTargetType.DockingManagerDockRight,
            "PART_DockingManagerDropTargetBottom" => DropTargetType.DockingManagerDockBottom,
            "PART_DocumentPaneDropTargetLeft" => DropTargetType.DocumentPaneDockLeft,
            "PART_DocumentPaneDropTargetTop" => DropTargetType.DocumentPaneDockTop,
            "PART_DocumentPaneDropTargetRight" => DropTargetType.DocumentPaneDockRight,
            "PART_DocumentPaneDropTargetBottom" => DropTargetType.DocumentPaneDockBottom,
            "PART_DocumentPaneDropTargetInto" => DropTargetType.DocumentPaneDockInside,
            _ => null
        };
        static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
        async Task Settle()
        {
            dock.Refresh();
            scene.UpdateLayout();
            await Task.Delay(65);
            scene.UpdateLayout();
        }

        async Task Reset()
        {
            overlay.Hide();
            overlay.ClearValue(Control.TemplateProperty);
            scene.FlowDirection = FlowDirection.LeftToRight;
            dock.Resources.Remove("UnoDock.GuideSize");
            dock.Theme = null;
            dock.RequestedTheme = ElementTheme.Light;
            dock.DockingGuideMode = DockingGuideMode.GuidesAndEdges;
            dock.ShowDocumentPaneToolGuides = true;
            dock.AllowMixedOrientation = false;
            VisualScene.Populate(dock);
            window.Activate();
            await Settle();
        }
    }
}
