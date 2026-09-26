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
        self.assertEqual(len(keys), 7)
        for template in root.findall(UI + 'ControlTemplate'):
            self.assertTrue(template.get('TargetType'))

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
        self.assertEqual(4, len(style))
        for setter in style:
            self.assertTrue(setter.get('Property', '').startswith('controls:LayoutItemBindings.'))
            self.assertTrue(any(element.tag.endswith('LayoutBinding') for element in setter.iter()))

if __name__ == '__main__':
    unittest.main()
