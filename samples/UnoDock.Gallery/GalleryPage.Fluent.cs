using Microsoft.UI.Xaml.Automation;
using UnoDock.Themes;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private SampleTheme? _appliedGalleryTheme;
    private readonly GalleryChromeResources _galleryChrome = new();
    private readonly WorkbenchResources _galleryWorkbench = new();
    private readonly Dictionary<MenuBarItem, ControlTemplate?> _legacyMenuTemplates = [];
    private Button CreateToolbarButton(string text, string command, string name)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)_galleryChrome["Gallery.ToolbarButton"]
        };
        button.Click += (_, _) => ExecuteSampleCommand(command);
        AutomationProperties.SetAutomationId(button, "SampleToolbar-" + command);
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        return button;
    }

    private void ApplyGalleryPresentation(SampleTheme theme)
    {
        if (_sampleShell == null)
            return;
        var fluent = theme != SampleTheme.Generic;
        if (fluent)
        {
            if (!_sampleShell.Resources.MergedDictionaries.Contains(_galleryWorkbench))
                _sampleShell.Resources.MergedDictionaries.Add(_galleryWorkbench);
            var managerStyle = (Style)_galleryWorkbench["UnoDock.WorkbenchManagerStyle"];
            if (_appliedGalleryTheme != theme)
            {
                // Resolve the scoped palette before style ThemeResources
                // capture their values. Merely reassigning an identical Style on
                // the pinned host can leave the previous theme's frame brushes.
                Dock.Refresh();
                if (ReferenceEquals(Dock.Style, managerStyle))
                    Dock.ClearValue(StyleProperty);
            }

            Dock.Style = managerStyle;
            var surfaceStyle = (Style)_galleryChrome["Gallery.Surface"];
            if (!ReferenceEquals(_sampleShell.Style, surfaceStyle))
            {
                _sampleShell.ClearValue(Panel.BackgroundProperty);
                _sampleShell.Style = surfaceStyle;
            }

            var statusStyle = (Style)_galleryChrome["Gallery.Status"];
            if (!ReferenceEquals(_status.Style, statusStyle))
            {
                _status.ClearValue(TextBlock.ForegroundProperty);
                _status.Style = statusStyle;
            }
        }
        else
        {
            _sampleShell.Resources.MergedDictionaries.Remove(_galleryWorkbench);
            if (ReferenceEquals(Dock.Style, _galleryWorkbench["UnoDock.WorkbenchManagerStyle"]))
                Dock.ClearValue(StyleProperty);
            _sampleShell.ClearValue(StyleProperty);
            _sampleShell.Background = SampleChrome.Color(0xf0f0f0);
            _status.ClearValue(StyleProperty);
            _status.Foreground = SampleChrome.Default(false).Foreground;
        }

        _appliedGalleryTheme = theme;
        foreach (var (menu, legacyTemplate) in _legacyMenuTemplates)
        {
            if (fluent)
                menu.ClearValue(TemplateProperty);
            else
                menu.Template = legacyTemplate;
        }
    }
}
