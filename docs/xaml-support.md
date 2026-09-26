# Consolidated XAML workspaces

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
