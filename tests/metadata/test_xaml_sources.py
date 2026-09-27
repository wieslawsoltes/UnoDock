#!/usr/bin/env python3
"""Static consumer contract checks; real binding/visual behavior is tested by Uno."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
UI = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'

class XamlSources(unittest.TestCase):
    def test_all_consumer_dictionaries_and_views_are_well_formed(self):
        paths = list((ROOT / 'samples/UnoDock.Gallery').glob('Xaml*.xaml'))
        self.assertGreaterEqual(len(paths), 6)
        for path in paths:
            ET.parse(path)

    def test_workspace_code_does_not_construct_layout_or_editors(self):
        for stem in ('XamlDeclarativeWorkspace', 'XamlMvvmWorkspace', 'XamlTemplateWorkspace'):
            path = ROOT / 'samples/UnoDock.Gallery' / (stem + '.xaml.cs')
            text = path.read_text()
            self.assertIn('InitializeComponent()', text)
            for forbidden in ('new LayoutRoot', 'new LayoutPanel', 'new TextBox', 'XamlReader.Load'):
                self.assertNotIn(forbidden, text)

    def test_no_runtime_xaml_parser_in_product(self):
        for path in (ROOT / 'src/UnoDock').rglob('*.cs'):
            if any(part in ('obj', 'bin') for part in path.parts):
                continue
            self.assertNotIn('XamlReader.Load', path.read_text(), str(path))

    def test_compiled_stock_templates_have_named_keys_and_target_types(self):
        root = ET.parse(ROOT / 'src/UnoDock/Themes/DockChromeResources.xaml').getroot()
        keys = [child.get(X + 'Key') for child in root]
        self.assertEqual(len(keys), len(set(keys)))
        templates = {child.get(X + 'Key'): (child.tag.removeprefix(UI), child.get('TargetType'))
                     for child in root if child.tag != UI + 'Style'}
        self.assertEqual(templates, {
            'UnoDock.ChromeButtonTemplate': ('ControlTemplate', 'ContentControl'),
            'UnoDock.ChromeThumbTemplate': ('ControlTemplate', 'Thumb'),
            'UnoDock.NavigatorListTemplate': ('ControlTemplate', 'ListBox'),
            'UnoDock.FluentNavigatorRowTemplate': ('ControlTemplate', 'ListBoxItem'),
            'UnoDock.NavigatorItemTemplate': ('DataTemplate', None),
            'UnoDock.NavigatorItemsPanel': ('ItemsPanelTemplate', None),
            'UnoDock.MenuRowTemplate': ('ControlTemplate', 'MenuFlyoutItem'),
            'UnoDock.MenuPresenterTemplate': ('ControlTemplate', 'MenuFlyoutPresenter'),
        })
        self.assertEqual(len(keys), 11)

    def test_fluent_navigator_composes_a_native_button_without_copying_its_template(self):
        root = ET.parse(ROOT / 'src/UnoDock/Themes/DockChromeResources.xaml').getroot()
        template = next(child for child in root if child.get(X + 'Key') == 'UnoDock.FluentNavigatorRowTemplate')
        button = template.find('.//' + UI + 'Button')
        self.assertIsNotNone(button)
        self.assertEqual('PART_NavigatorAction', button.get(X + 'Name'))
        self.assertEqual('{StaticResource UnoDock.FluentChromeButtonStyle}', button.get('Style'))
        self.assertIsNone(button.get('Template'))
        self.assertFalse(button.findall('.//' + UI + 'ControlTemplate'))
        marker = template.find('.//' + UI + 'Border')
        self.assertEqual('False', marker.get('IsHitTestVisible'))
        self.assertEqual('PART_NavigatorSelection', marker.get(X + 'Name'))

    def test_fluent_styles_inherit_native_templates_without_copying_them(self):
        root = ET.parse(ROOT / 'src/UnoDock/Themes/DockChromeResources.xaml').getroot()
        styles = {child.get(X + 'Key'): child for child in root.findall(UI + 'Style')}
        expected = {
            'UnoDock.FluentChromeButtonStyle': ('Button', 'DefaultButtonStyle'),
            'UnoDock.FluentMenuRowStyle': ('MenuFlyoutItem', 'DefaultMenuFlyoutItemStyle'),
            'UnoDock.FluentMenuPresenterStyle': ('MenuFlyoutPresenter', 'DefaultMenuFlyoutPresenterStyle'),
        }
        self.assertEqual(set(expected), set(styles))
        for key, (target, base) in expected.items():
            style = styles[key]
            self.assertEqual(target, style.get('TargetType'))
            self.assertEqual('{StaticResource ' + base + '}', style.get('BasedOn'))
            self.assertFalse(style.findall('.//' + UI + 'ControlTemplate'))
            self.assertFalse(any(setter.get('Property') == 'Template'
                                 for setter in style.findall(UI + 'Setter')))

    def test_custom_templates_keep_required_docking_parts(self):
        root = ET.parse(ROOT / 'samples/UnoDock.Gallery/XamlTemplateWorkspace.xaml').getroot()
        templates = root.findall('.//' + UI + 'ControlTemplate')
        self.assertEqual(2, len(templates))
        for template in templates:
            names = {element.get(X + 'Name'): element for element in template.iter() if element.get(X + 'Name')}
            self.assertIn('PART_AutoHideArea', names)
            self.assertEqual(UI + 'ContentPresenter', names['PART_LayoutHost'].tag)

    def test_live_item_styles_use_explicit_per_item_binding_definitions(self):
        root = ET.parse(ROOT / 'samples/UnoDock.Gallery/XamlWorkspaceResources.xaml').getroot()
        style = next(element for element in root if element.get(X + 'Key') == 'XamlSample.ItemStyle')
        self.assertEqual(1, len(style))
        self.assertEqual('controls:LayoutItemBindings.Bindings', style[0].get('Property'))
        bindings = [element for element in style.iter() if element.tag.endswith('}LayoutItemBinding')]
        self.assertEqual(['Title', 'ContentId', 'CanClose', 'IsSelected'], [element.get('Property') for element in bindings])

if __name__ == '__main__':
    unittest.main()
