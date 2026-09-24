using System.Reflection;
using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Layout;
using UnoDock.VisualValidation;

namespace UnoDock.Testing;

internal static class NavigatorSampleTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        Add("navigator sample: compact real controls and command status are realized", async (page, panel, manager) =>
        {
            var buttons = panel.FindVisualChildren<SampleButton>().ToArray();
            Check.Equal(9, buttons.Length);
            Check.True(buttons.All(button => button.ActualHeight > 0 && button.ActualHeight <= 25));
            Check.Equal(3, manager.Layout.Descendents().OfType<LayoutDocument>().Count());
            var target = manager.Layout.Descendents().OfType<LayoutDocument>().Last();
            var command = manager.GetLayoutItemFromModel(target).ActivateCommand!;
            Check.True(command.CanExecute(null)); command.Execute(null);
            Check.Same(target, manager.Layout.ActiveContent);
            Check.True(Status(panel).Contains("Committed: 1", StringComparison.Ordinal));
            var path = Path.Combine(output, "visuals", "navigator-activation-sample.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); await VisualCapture.Save(panel, path);
        });
        Add("navigator sample: closing the laboratory detaches retained commands", async (page, panel, manager) =>
        {
            var target = manager.Layout.Descendents().OfType<LayoutDocument>().Last();
            var command = manager.GetLayoutItemFromModel(target).ActivateCommand!;
            var document = page.Dock.Layout.Descendents().OfType<LayoutDocument>().Single(d => ReferenceEquals(d.Content, panel));
            document.Close();
            Check.False(command.CanExecute(null)); command.Execute(null);
            await Task.CompletedTask;
        });
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("UNODOCK_NATIVE_INPUT_TESTS") == "1")
            Add("XTEST: navigator sample policy vetoes and then allows an actual Enter commit", async (_, panel, manager) =>
            {
                using var input = new X11TestInput();
                var original = manager.Layout.ActiveContent;
                var target = manager.Layout.Descendents().OfType<LayoutDocument>().Last();
                await Click("policy"); Check.True(Status(panel).Contains("blocked by command", StringComparison.Ordinal));
                await Click("open"); await Wait(() => Current(manager) is { IsLoaded: true, ActualHeight: > 0 });
                var nav = Current(manager)!; nav.SelectedDocument = (LayoutDocumentItem)manager.GetLayoutItemFromModel(target);
                nav.Focus(FocusState.Keyboard); await Task.Delay(50); input.KeyPress(0xff0d);
                await Wait(() => Current(manager) == null); Check.Same(original, manager.Layout.ActiveContent);
                Check.True(Status(panel).Contains("Committed: 0", StringComparison.Ordinal));
                await Click("policy"); await Click("open");
                await Wait(() => Current(manager) is { IsLoaded: true, ActualHeight: > 0 });
                nav = Current(manager)!; nav.SelectedDocument = (LayoutDocumentItem)manager.GetLayoutItemFromModel(target);
                nav.Focus(FocusState.Keyboard); await Task.Delay(50); input.KeyPress(0xff0d);
                await Wait(() => Current(manager) == null && ReferenceEquals(manager.Layout.ActiveContent, target));
                Check.True(Status(panel).Contains("Committed: 1", StringComparison.Ordinal));
                await Click("theme");
                Check.Equal(ElementTheme.Dark, manager.RequestedTheme);
                await VisualCapture.Save(panel, Path.Combine(output, "visuals", "navigator-activation-sample-dark.png"));
                async Task Click(string id)
                {
                    var button = panel.FindVisualChildren<SampleButton>().Single(b => AutomationProperties.GetAutomationId(b) == "NavigatorLab-" + id);
                    input.MoveTo(button, new(button.ActualWidth / 2, button.ActualHeight / 2));
                    input.Press(); await Task.Delay(40); input.Release(); await Task.Delay(60);
                }
            });
        return await tests.Run(output, "navigator-sample");

        void Add(string name, Func<GalleryPage, Grid, DockingManager, Task> body) => tests.Test(name, async () =>
        {
            using var page = new GalleryPage { Width = 1200, Height = 850 };
            var window = new Window { Content = page, Title = "UnoDock navigator sample acceptance" };
            window.AppWindow.Resize(new() { Width = 1300, Height = 930 }); window.Activate();
            LayoutDocument? document = null;
            try
            {
                await Wait(() => page.IsLoaded && page.Dock.ActualHeight > 0);
                page.ExecuteSampleCommand("navigator");
                document = page.Dock.Layout.Descendents().OfType<LayoutDocument>().Single(d => d.ContentId?.StartsWith("navigator-lab:", StringComparison.Ordinal) == true);
                var panel = (Grid)document.Content!;
                await Wait(() => panel.IsLoaded && panel.ActualHeight > 0);
                var manager = panel.FindVisualChildren<DockingManager>().Single();
                await Wait(() => manager.IsLoaded && manager.ActualHeight > 0); await Task.Delay(100);
                await body(page, panel, manager);
            }
            finally { document?.Close(); window.Content = null; window.Close(); }
        });
    }
    private static string Status(Grid panel) => panel.FindVisualChildren<TextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == "NavigatorLabStatus").Text;
    private static NavigatorWindow? Current(DockingManager manager)
    {
        var surface = typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        return (NavigatorWindow?)surface.GetType().GetField("_navigator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(surface);
    }
    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++) await Task.Delay(20);
        Check.True(predicate(), "Navigator sample did not converge within the bounded wait.");
    }
}
