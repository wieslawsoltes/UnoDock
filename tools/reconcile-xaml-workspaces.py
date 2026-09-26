"""Reconcile PR13's consumer workspaces onto the newer binding/density contract."""
from pathlib import Path
import subprocess

OLD = 'f4f605f64723aaef22e4dd53755b11d1d7c07227'
BASE = 'ab5a698e0839a5f0cf56e170a8d82232ec5cf482'

def original(path):
    return subprocess.check_output(['git', 'show', OLD + ':' + path]).decode()

def write(path, text):
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding='utf-8')

def replace(path, old, new, count=1):
    text = Path(path).read_text()
    if text.count(old) != count:
        raise RuntimeError(f'{path}: expected {count}, got {text.count(old)}: {old[:100]!r}')
    write(path, text.replace(old, new))

files = '''samples/UnoDock.Gallery/XamlDeclarativeWorkspace.xaml
samples/UnoDock.Gallery/XamlDeclarativeWorkspace.xaml.cs
samples/UnoDock.Gallery/XamlDocumentView.xaml
samples/UnoDock.Gallery/XamlDocumentView.xaml.cs
samples/UnoDock.Gallery/XamlMvvmWorkspace.xaml
samples/UnoDock.Gallery/XamlMvvmWorkspace.xaml.cs
samples/UnoDock.Gallery/XamlSamplesPage.xaml
samples/UnoDock.Gallery/XamlSamplesPage.xaml.cs
samples/UnoDock.Gallery/XamlTemplateWorkspace.xaml
samples/UnoDock.Gallery/XamlTemplateWorkspace.xaml.cs
samples/UnoDock.Gallery/XamlWorkspaceActions.cs
samples/UnoDock.Gallery/XamlWorkspaceItem.cs
samples/UnoDock.Gallery/XamlWorkspaceResources.xaml
samples/UnoDock.Gallery/XamlWorkspaceTemplateSelector.cs
samples/UnoDock.Gallery/XamlWorkspaceViewModel.cs
src/UnoDock/Themes/DockChromeResources.xaml
src/UnoDock/Themes/DockChromeResources.xaml.cs
src/UnoDock/Themes/ResourceDictionaryTheme.cs
src/UnoDock/Themes/Theme.cs
src/UnoDock/Controls/NavigatorListBox.cs
src/UnoDock/Internal/DockMenuRow.cs
tests/UnoDock.VisualTests/XamlWorkspaceTests.cs
tests/UnoDock.VisualTests/XamlWorkspaceTests.Bindings.cs
tests/metadata/test_xaml_sources.py'''.splitlines()
for path in files:
    write(path, original(path))

# Keep newer binding/ownership internals. Generalize only theme observation.
replace('src/UnoDock/Themes/FluentTheme.cs', '    internal event EventHandler? Changed;\n', '')
replace('src/UnoDock/Themes/FluentTheme.cs', '        Changed?.Invoke(this, EventArgs.Empty);', '        InvalidateTheme();')
write('src/UnoDock/DockingManager.Xaml.cs', '''using UnoDock.Themes;

namespace UnoDock;

public partial class DockingManager
{
    private Theme? _observedTheme;
    private Microsoft.Windows.Shell.SystemParameters2? _themeParameters;

    private void ObserveXamlTheme(Theme? theme)
    {
        if (ReferenceEquals(theme, _observedTheme))
            return;
        if (_observedTheme != null)
            _observedTheme.Changed -= OnXamlThemeChanged;
        _observedTheme = theme;
        if (theme != null)
            theme.Changed += OnXamlThemeChanged;
    }

    private void OnXamlThemeChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, Theme))
            return;
        UpdateThemeDictionary();
        InvalidateView();
    }

    private void UpdateThemeDictionary()
    {
        var dictionary = Theme?.GetResourceDictionary();
        if (ReferenceEquals(_themeResources, dictionary))
            return;
        if (_themeResources != null)
            Resources.MergedDictionaries.Remove(_themeResources);
        _themeResources = dictionary;
        if (dictionary != null)
            Resources.MergedDictionaries.Add(dictionary);
    }

    private void ObserveThemeParameters()
    {
        if (_themeParameters != null)
            return;
        _themeParameters = Microsoft.Windows.Shell.SystemParameters2.Current;
        _themeParameters.PropertyChanged += OnThemeParametersChanged;
    }

    private void ReleaseThemeParameters()
    {
        if (_themeParameters == null)
            return;
        _themeParameters.PropertyChanged -= OnThemeParametersChanged;
        _themeParameters = null;
    }

    private void OnThemeParametersChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_disposed && _loaded && args.PropertyName is "HighContrast" or "WindowGlassColor" or "UxThemeName")
            InvalidateView();
    }
}
''')
replace('src/UnoDock/DockingManager.cs', '''                ObserveXamlTheme(Theme as FluentTheme);
                if (_themeResources != null)
                    Resources.MergedDictionaries.Remove(_themeResources);
                _themeResources = Theme?.GetResourceDictionary();
                if (_themeResources != null)
                    Resources.MergedDictionaries.Add(_themeResources);''', '''                ObserveXamlTheme(Theme);
                UpdateThemeDictionary();''')
replace('src/UnoDock/DockingManager.cs', '        _loaded = true;', '        _loaded = true;\n        ObserveThemeParameters();')
replace('src/UnoDock/DockingManager.cs', '        _loaded = false;', '        _loaded = false;\n        ReleaseThemeParameters();')
replace('src/UnoDock/DockingManager.cs', '        ObserveXamlTheme(null);', '        ObserveXamlTheme(null);\n        ReleaseThemeParameters();')

path = 'src/UnoDock/Internal/DockThemeResources.cs'
replace(path, 'manager.Theme is FluentTheme;', 'manager.Theme is FluentTheme or ResourceDictionaryTheme;')
replace(path, '        var theme = owner is DockingManager manager ? EffectiveTheme(manager) : owner.ActualTheme;', '''        var theme = owner is DockingManager manager ? EffectiveTheme(manager) : owner.ActualTheme;
        var themeName = Microsoft.Windows.Shell.SystemParameters2.Current.HighContrast ? "HighContrast" : theme == ElementTheme.Dark ? "Dark" : "Light";''')
replace(path, 'Find(current.Resources, key, theme, skip,', 'Find(current.Resources, key, themeName, skip,')
replace(path, 'Find(app.Resources, key, theme, skip,', 'Find(app.Resources, key, themeName, skip,')
text = Path(path).read_text()
start = text.index('    private static object? Find(ResourceDictionary dictionary,')
write(path, text[:start] + '''    private static object? Find(ResourceDictionary dictionary, string key, string themeName, ResourceDictionary? skip, HashSet<ResourceDictionary> visited)
        => Find(dictionary, key, themeName, skip, visited, null);

    private static object? Find(ResourceDictionary dictionary, string key, string themeName, ResourceDictionary? skip, HashSet<ResourceDictionary> visited, string? alternate)
    {
        if (ReferenceEquals(dictionary, skip) || !visited.Add(dictionary))
            return null;
        if (dictionary.Count > 0 && dictionary.Keys.Contains(key))
            return dictionary[key];
        if (alternate != null && dictionary.Count > 0 && dictionary.Keys.Contains(alternate))
            return dictionary[alternate];
        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (Find(dictionary.MergedDictionaries[i], key, themeName, skip, visited, alternate) is { } merged)
                return merged;
        var themes = dictionary.ThemeDictionaries;
        if (themes.Count > 0)
        {
            if (themes.Keys.Contains(themeName) && themes[themeName] is ResourceDictionary selected)
                return Find(selected, key, themeName, skip, visited, alternate);
            if (themes.Keys.Contains("Default") && themes["Default"] is ResourceDictionary defaults)
                return Find(defaults, key, themeName, skip, visited, alternate);
        }
        return null;
    }
}
''')
# Replace stock getters only; preserve the current density/palette implementation.
path = 'src/UnoDock/Internal/DockChrome.cs'
text = Path(path).read_text()
old = original(path)
a = text.index('    [ThreadStatic]')
b = text.index('    internal static DockPalette Palette(')
write(path, text[:a] + old[old.index('    [ThreadStatic]'):old.index('    internal static DockPalette Palette(')] + text[b:])

for stem in ('XamlDeclarativeWorkspace', 'XamlMvvmWorkspace', 'XamlTemplateWorkspace'):
    path = 'samples/UnoDock.Gallery/' + stem + '.xaml'
    replace(path, '<themes:FluentTheme Density="Comfortable"/>', '<themes:FluentTheme/>')
    replace(path, '<dock:DockingManager ', '<dock:DockingManager ChromeDensity="Comfortable" ')
replace('samples/UnoDock.Gallery/XamlTemplateWorkspace.xaml.cs', '''new Themes.FluentTheme
    {
        Density = Themes.DockDensity.Comfortable
    }''', 'new Themes.FluentTheme()')
replace('samples/UnoDock.Gallery/XamlWorkspaceActions.cs', '''                theme.Density = (DockDensity)(((int)theme.Density + 1) % 3);
                Status = "Density: " + theme.Density;''', '''                _manager.ChromeDensity = _manager.ChromeDensity switch
                {
                    DockChromeDensity.Compact => DockChromeDensity.Comfortable,
                    DockChromeDensity.Comfortable => DockChromeDensity.Spacious,
                    _ => DockChromeDensity.Compact
                };
                Status = "Density: " + _manager.ChromeDensity;''')
path = 'samples/UnoDock.Gallery/XamlWorkspaceResources.xaml'
text = Path(path).read_text()
start = text.index('    <Style x:Key="XamlSample.ItemStyle"')
write(path, text[:start] + '''    <Style x:Key="XamlSample.ItemStyle" TargetType="controls:LayoutItem">
        <Setter Property="controls:LayoutItemBindings.Bindings">
            <Setter.Value>
                <controls:LayoutItemBindingCollection>
                    <controls:LayoutItemBinding Property="Title" Path="Title" Mode="TwoWay"/>
                    <controls:LayoutItemBinding Property="ContentId" Path="ContentId"/>
                    <controls:LayoutItemBinding Property="CanClose" Path="CanClose" Mode="TwoWay"/>
                    <controls:LayoutItemBinding Property="IsSelected" Path="IsSelected" Mode="TwoWay"/>
                </controls:LayoutItemBindingCollection>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
''')
replace('samples/UnoDock.Gallery/App.xaml', '''    <Setter Property="Title" Value="{Binding Title}"/><Setter Property="ContentId" Value="{Binding ContentId}"/>''', '''    <Setter Property="controls:LayoutItemBindings.Bindings">
     <Setter.Value><controls:LayoutItemBindingCollection>
      <controls:LayoutItemBinding Property="Title" Path="Title" Mode="TwoWay"/>
      <controls:LayoutItemBinding Property="ContentId" Path="ContentId"/>
     </controls:LayoutItemBindingCollection></Setter.Value>
    </Setter>''')
replace('samples/UnoDock.Gallery/GalleryPage.XamlSamples.cs', '    private void ShowXamlWorkbench()', '    private void ShowXamlSamples() => OpenXamlSample("XAML workspaces", new XamlSamplesPage());\n    private void ShowXamlWorkbench()')
replace('samples/UnoDock.Gallery/GalleryPage.Samples.cs', '        AddMenu("Samples", ', '        AddMenu("Samples", ("XAML workspaces", "xaml-workspaces", ShowXamlSamples), ')
replace('samples/UnoDock.Gallery/App.xaml.cs', '                        ("xaml-workbench",', '                        ("xaml-workspaces", true, () => Testing.XamlWorkspaceTests.Run(output)),\n                        ("xaml-workbench",')

# Retain every old scenario, adapting API names and exact documented dimensions.
path = 'tests/UnoDock.VisualTests/XamlWorkspaceTests.cs'
replace(path, 'Enum.GetValues<DockDensity>()', 'new[] { DockChromeDensity.Compact, DockChromeDensity.Comfortable, DockChromeDensity.Spacious }')
replace(path, '((FluentTheme)manager.Theme!).Density = density;', 'manager.ChromeDensity = density;')
replace(path, 'density == DockDensity.Compact ? 19d : density == DockDensity.Comfortable ? 31d : 43d;', 'density == DockChromeDensity.Compact ? 19d : density == DockChromeDensity.Comfortable ? 27d : 35d;')
replace(path, 'Check.Throws<ArgumentOutOfRangeException>(() => theme.Density = (DockDensity)999);', 'using var manager = new DockingManager { Theme = theme };\n            Check.Throws<ArgumentOutOfRangeException>(() => manager.ChromeDensity = (DockChromeDensity)999);')
replace(path, 'theme.Density = DockDensity.Touch;\n            Check.Equal(DockDensity.Touch, theme.Density);', 'manager.ChromeDensity = DockChromeDensity.Spacious;\n            Check.Equal(DockChromeDensity.Spacious, manager.ChromeDensity);')
replace('tests/UnoDock.VisualTests/XamlWorkspaceTests.Bindings.cs', 'Check.Same(LayoutItemBindings.GetTitle(first), LayoutItemBindings.GetTitle(second));', 'Check.Same(LayoutItemBindings.GetBindings(first), LayoutItemBindings.GetBindings(second));')
path = 'tests/metadata/test_xaml_sources.py'
replace(path, '''        self.assertEqual(4, len(style))
        for setter in style:
            self.assertTrue(setter.get('Property', '').startswith('controls:LayoutItemBindings.'))
            self.assertTrue(any(element.tag.endswith('LayoutBinding') for element in setter.iter()))''', '''        self.assertEqual(1, len(style))
        self.assertEqual('controls:LayoutItemBindings.Bindings', style[0].get('Property'))
        bindings = [element for element in style.iter() if element.tag.endswith('}LayoutItemBinding')]
        self.assertEqual(['Title', 'ContentId', 'CanClose', 'IsSelected'], [element.get('Property') for element in bindings])''')
write('docs/xaml-support.md', '''# Consolidated XAML workspaces

The declarative, MVVM and template workspaces from PR #13 use the same native
binding and density APIs as the existing workbench samples. Open Samples > XAML
workspaces. Layouts, editors and command bars are compiled XAML; application code
supplies commands and payloads. All 37 earlier acceptance scenarios are retained.

Use DockingManager.ChromeDensity (Compact, Comfortable, Spacious or Default), not
a second theme-level density system. The former unpublished Touch profile is
represented by Spacious. Item styles use LayoutItemBindingCollection rather than
a parallel set of attached properties. Tests adapt API names and exact geometry
to that documented contract, not weaker tolerances.

ResourceDictionaryTheme accepts a consumer dictionary in Resources. Replacing it
updates attached managers; Refresh notifies them after in-place edits. Managers
release subscriptions on replacement/disposal. Lookup uses local entries, reverse
merged dictionaries, then the requested theme dictionary. Default is used only
when that named dictionary is absent. HighContrast does not fall through into
unrelated Light/Dark/Default entries. Contrast/color observers exist only while a
manager is loaded. Dictionary tests do not emulate OS high-contrast mode.

Seven stock chrome/menu/navigator templates are compiled in
Themes/DockChromeResources.xaml, retaining current interactions and geometry.
No product stock template uses XamlReader.Load. Runtime consumer XAML is trusted
markup, not an untrusted document sandbox.

Reconciliation preserves both PR histories while keeping the newer ownership,
callback cleanup and binding implementations. See xaml-workbench.md for native
XAML endpoint, lifetime and platform validation boundaries.
''')
print('Reconciled PR13 contributions onto', BASE)
