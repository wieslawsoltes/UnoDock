"""Temporary: compile one-attribute XAML probes against UnoDock on native WinUI."""
from pathlib import Path
import shutil, subprocess, sys

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'artifacts' / 'probes'
PROBES = {
    'manager-only': '',
    'empty-root': '<layout:LayoutRoot/>',
    'root-empty-panel': '<layout:LayoutRoot><layout:LayoutRoot.RootPanel><layout:LayoutPanel/></layout:LayoutRoot.RootPanel></layout:LayoutRoot>',
    'panel-orientation': '<layout:LayoutRoot><layout:LayoutRoot.RootPanel><layout:LayoutPanel Orientation="Horizontal"/></layout:LayoutRoot.RootPanel></layout:LayoutRoot>',
    'document-pane': '<layout:LayoutRoot><layout:LayoutRoot.RootPanel><layout:LayoutPanel><layout:LayoutDocumentPane/></layout:LayoutPanel></layout:LayoutRoot.RootPanel></layout:LayoutRoot>',
    'document': '<layout:LayoutRoot><layout:LayoutRoot.RootPanel><layout:LayoutPanel><layout:LayoutDocumentPane><layout:LayoutDocument Title="D" ContentId="d"/></layout:LayoutDocumentPane></layout:LayoutPanel></layout:LayoutRoot.RootPanel></layout:LayoutRoot>',
    'root-implicit-panel': '<layout:LayoutRoot><layout:LayoutPanel/></layout:LayoutRoot>',
    'full-tools-and-documents': '<layout:LayoutRoot><layout:LayoutRoot.RootPanel><layout:LayoutPanel Orientation="Horizontal"><layout:LayoutAnchorablePaneGroup DockMinWidth="150"><layout:LayoutAnchorablePane DockWidth="210" CanRepositionItems="False"><layout:LayoutAnchorable Title="A" ContentId="a" AutoHideWidth="200" CanClose="False"/></layout:LayoutAnchorablePane></layout:LayoutAnchorablePaneGroup><layout:LayoutDocumentPaneGroup><layout:LayoutDocumentPane><layout:LayoutDocument Title="D" ContentId="d"/></layout:LayoutDocumentPane></layout:LayoutDocumentPaneGroup></layout:LayoutPanel></layout:LayoutRoot.RootPanel></layout:LayoutRoot>',
}
PROJECT = '''<Project Sdk="Uno.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-windows10.0.26100.0</TargetFrameworks>
    <OutputType>WinExe</OutputType><UnoSingleProject>true</UnoSingleProject><IsPackable>false</IsPackable>
    <WindowsPackageType>None</WindowsPackageType><ApplicationId>org.unodock.probe</ApplicationId>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="{lib}" /></ItemGroup>
</Project>'''
APP_XAML = '<Application x:Class="Probe.App" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"/>'
APP_CS = 'namespace Probe; public partial class App : Microsoft.UI.Xaml.Application { public App() => InitializeComponent(); protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) => new Microsoft.UI.Xaml.Window { Content = new ProbePage() }.Activate(); }'
PAGE = '''<Page x:Class="Probe.ProbePage" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:dock="using:UnoDock" xmlns:layout="using:UnoDock.Layout">
  <dock:DockingManager>{snippet}</dock:DockingManager>
</Page>'''
PAGE_CS = 'namespace Probe; public sealed partial class ProbePage : Microsoft.UI.Xaml.Controls.Page { public ProbePage() => InitializeComponent(); }'

results = []
for name, snippet in PROBES.items():
    d = WORK / name
    shutil.rmtree(d, ignore_errors=True); d.mkdir(parents=True)
    (d / 'Probe.csproj').write_text(PROJECT.format(lib=ROOT / 'src/UnoDock/UnoDock.csproj'))
    (d / 'App.xaml').write_text(APP_XAML); (d / 'App.xaml.cs').write_text(APP_CS)
    (d / 'ProbePage.xaml').write_text(PAGE.format(snippet=snippet)); (d / 'ProbePage.xaml.cs').write_text(PAGE_CS)
    r = subprocess.run(['msbuild', str(d / 'Probe.csproj'), '-restore', '-p:Configuration=Release', '-p:Platform=x64', '-p:UnoDockLibraryFrameworks=net10.0-windows10.0.26100.0', '-v:minimal', '-nologo'], capture_output=True, text=True)
    errors = sorted({line.strip()[:160] for line in r.stdout.splitlines() if ' error ' in line and 'ProbePage' in line} or {line.strip()[-200:] for line in r.stdout.splitlines() if ' error ' in line})
    results.append((name, r.returncode, errors))
    print(f'{name}: {"OK" if r.returncode == 0 else "FAIL"} {errors[:2]}', flush=True)
sys.exit(0)
