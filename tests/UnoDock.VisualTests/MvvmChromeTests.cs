using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Gallery;
using UnoDock.Themes;
using UnoDock.VisualValidation;
using Windows.Foundation;
using System.Xml.Linq;

namespace UnoDock.Testing;
internal static class MvvmChromeTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        using var dock = new DockingManager
        {
            Width = 1000,
            Height = 640,
            FontSize = 12,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        var window = new Window
        {
            Content = dock,
            Title = "UnoDock compact MVVM chrome"
        };
        window.AppWindow.Resize(new() { Width = 1100, Height = 800 });
        window.Activate();
        try
        {
            await Wait(() => dock.IsLoaded && dock.ActualWidth > 0);
            foreach (var scene in new[]
            {
                "generic",
                "light",
                "dark",
                "rtl"
            }

            )
                tests.Test("MVVM chrome: compact geometry, bound commands and capture: " + scene, async () =>
                {
                    dock.RequestedTheme = scene == "dark" ? ElementTheme.Dark : ElementTheme.Light;
                    dock.Theme = scene == "generic" ? new GenericTheme() : new FluentTheme(dock.RequestedTheme);
                    dock.FlowDirection = scene == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    using var workspace = new MvvmWorkspace(dock, _ =>
                    {
                    }, new MemoryStorage());
                    await Settle();
                    var document = workspace.Documents[0];
                    var editor = Editor(document);
                    var commandBar = editor.FindVisualChildren<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "MvvmEditorCommandBar");
                    var statusBar = editor.FindVisualChildren<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "MvvmEditorStatusBar");
                    Check.Near(31, commandBar.ActualHeight);
                    Check.Near(23, statusBar.ActualHeight);
                    var buttons = editor.FindVisualChildren<SampleButton>().ToArray();
                    Check.Equal(3, buttons.Length);
                    foreach (var button in buttons)
                    {
                        Check.Near(24, button.ActualHeight);
                        var point = button.TransformToVisual(commandBar).TransformPoint(new Point(0, 0));
                        Check.True(point.Y >= 0 && point.Y + button.ActualHeight <= commandBar.ActualHeight, "Command is clipped by its bar.");
                        Check.True(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                    }

                    var save = buttons.Single(b => AutomationProperties.GetAutomationId(b) == "MvvmEditorSave");
                    Check.Same(document.SaveCommand, save.Command);
                    Check.False(save.IsEnabled);
                    document.Text = "retained chrome test\n";
                    await Wait(() => save.IsEnabled);
                    Check.Near(editor.ActualHeight - 54, editor.Editor.ActualHeight, .1);
                    var directory = Path.Combine(output, "visuals");
                    Directory.CreateDirectory(directory);
                    await VisualCapture.Save(dock, Path.Combine(directory, "mvvm-compact-" + scene + ".png"));
                    new XDocument(VisualScene.Measure(dock, "mvvm-compact-" + scene)).Save(Path.Combine(directory, "mvvm-compact-" + scene + ".xml"));
                });
            tests.Test("MVVM chrome: theme changes retain command instances, bindings and editor draft", async () =>
            {
                using var workspace = new MvvmWorkspace(dock, _ =>
                {
                }, new MemoryStorage());
                await Settle();
                var document = workspace.Documents[0];
                var editor = Editor(document);
                var buttons = editor.FindVisualChildren<SampleButton>().ToArray();
                editor.Editor.Text = "draft kept through theme changes";
                await Wait(() => document.IsDirty);
                foreach (var theme in new[]
                {
                    ElementTheme.Light,
                    ElementTheme.Dark,
                    ElementTheme.Light
                }

                )
                {
                    dock.RequestedTheme = theme;
                    dock.Theme = new FluentTheme(theme);
                    await Settle();
                    Check.Same(editor, Editor(document));
                    Check.Equal("draft kept through theme changes", document.Text);
                    var current = editor.FindVisualChildren<SampleButton>().ToArray();
                    for (var i = 0; i < buttons.Length; i++)
                        Check.Same(buttons[i], current[i]);
                    Check.Same(document.SaveCommand, current.Single(b => AutomationProperties.GetAutomationId(b) == "MvvmEditorSave").Command);
                }
            });
            return await tests.Run(output, "mvvm-chrome");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }

        async Task Settle()
        {
            dock.Refresh();
            dock.UpdateLayout();
            await Task.Delay(80);
            dock.UpdateLayout();
        }

        MvvmDocumentEditor Editor(WorkspaceDocument document)
        {
            var model = dock.Layout.Descendents().OfType<LayoutDocument>().Single(d => ReferenceEquals(d.Content, document));
            return dock.GetLayoutItemFromModel(model).View.FindVisualChildren<MvvmDocumentEditor>().Single();
        }
    }

    private sealed class MemoryStorage : IWorkspaceStorage
    {
        public Task WriteAsync(string contentId, string text) => Task.CompletedTask;
    }

    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 150 && !condition(); i++)
            await Task.Delay(20);
        Check.True(condition(), "Compact editor condition did not converge.");
    }
}
