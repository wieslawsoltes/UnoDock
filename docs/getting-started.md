# Getting started

## Prerequisites

Install a .NET 10 SDK compatible with `global.json`. UnoDock currently pins `Uno.Sdk` 6.7.30; use the checked-in version unless intentionally reviewing a platform upgrade. Native desktop prerequisites vary by operating system; consult [Uno's setup guide](https://platform.uno/docs/articles/get-started.html).

```sh
git clone https://github.com/wieslawsoltes/UnoDock.git
cd UnoDock

dotnet run --project samples/UnoDock.Gallery \
  -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

The desktop gallery demonstrates documents, tool windows, auto-hide, layout persistence, MVVM sources, XAML templates, typed property inspection, and a Fluent Ctrl+Tab navigator. The `Samples` menu exposes additional compiled workspaces.

## Add the library from source

Reference `src/UnoDock/UnoDock.csproj` from an Uno application. Keep the UI package versions consistent across the solution. The repository version does not imply the corresponding package has already been published.

```xml
<ItemGroup>
  <ProjectReference Include="../UnoDock/src/UnoDock/UnoDock.csproj" />
</ItemGroup>
```

Keep Uno's normal `XamlControlsResources` in application resources. To opt into the workbench palette, merge a `WorkbenchResources` dictionary in a scope containing the docking manager:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:themes="using:UnoDock.Themes">
    <ResourceDictionary.MergedDictionaries>
        <themes:WorkbenchResources />
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

Then use `Style="{StaticResource UnoDock.WorkbenchManagerStyle}"`, `ChromeDensity="Comfortable"`, and a `FluentTheme` on the manager. Application-local values and resources retain precedence. Use [the XAML guide](xaml-workbench.md) for complete layout and source-binding examples.

## Build the browser experience

```sh
dotnet workload install wasm-tools
dotnet publish samples/UnoDock.Gallery -c Release -f net10.0-browserwasm \
  -p:UnoDockTargetFrameworks=net10.0-browserwasm \
  -p:UnoDockLibraryFrameworks=net10.0 \
  -p:WasmShellWebAppBasePath=/UnoDock/gallery/ \
  -o artifacts/publish

python3 -m pip install Markdown==3.8.2
python3 tools/build-browser-site.py --publish artifacts/publish --output artifacts/site
```

Serve the site under `/UnoDock/`, matching the published base path. For example, create `artifacts/serve/UnoDock` pointing to `artifacts/site`, then run `python3 -m http.server 8765 --directory artifacts/serve`. Open `http://localhost:8765/UnoDock/playground/`. Do not open generated HTML through `file://`.

The playground loads the real gallery assembly inside a dedicated workspace host. Its browser launcher avoids desktop-only AppWindow initialization; the ordinary gallery route remains available at `/UnoDock/gallery/`.
