using System.Globalization;
using UnoDock.Compatibility;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage
{
    private void ShowConverterLab()
    {
        if (Dock.DocumentsSource != null)
        {
            Log("Leave source-bound mode before adding a converter lab document.");
            return;
        }

        var document = Document("converter-lab-" + _nextDocument++, "Converter & binding lab", new ConverterLab());
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null)
        {
            pane = new();
            Dock.Layout.RootPanel.Children.Add(pane);
        }

        pane.Children.Add(document);
        document.IsActive = true;
    }

    private void ShowNativeWindowLab()
    {
        if (OperatingSystem.IsBrowser())
        {
            Log("Native windows require a desktop head. Browser floating windows remain in the surface.");
            return;
        }

        if (Dock.DocumentsSource != null)
        {
            Log("Leave source-bound mode before opening native tool windows.");
            return;
        }

        Dock.FloatingWindowMode = FloatingWindowMode.Native;
        foreach (var title in new[]
        {
            "Native docking A",
            "Native docking B"
        }

        )
        {
            var tool = new LayoutAnchorable
            {
                ContentId = "native-lab-" + _nextDocument++,
                Title = title,
                Content = new TextBox
                {
                    AcceptsReturn = true,
                    Text = "Drag this TOOL TAB into the other native tool window or into the main workspace. The editor is retained.\n\nNative WinUI, Uno Skia Win32 and X11 use client-coordinate conversion. macOS/custom islands still require an application adapter."
                },
                FloatingLeft = title.EndsWith('A') ? 90 : 700,
                FloatingTop = 140,
                FloatingWidth = 520,
                FloatingHeight = 380
            };
            _content[tool.ContentId] = tool.Content;
            tool.AddToLayout(Dock, AnchorableShowStrategy.Right);
            tool.Float();
        }

        Log("Created two native tool windows. Use their tab headers to test cross-window docking; OS title-bar docking is a separate remaining boundary.");
    }
}
