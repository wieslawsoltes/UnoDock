# Localization

Docking chrome — context menus, caption and tab buttons, the system menu and
their accessible names — is available in English and thirteen bundled
languages:

| Culture | Language | Culture | Language |
|---|---|---|---|
| `cs` | Czech | `nl` | Dutch |
| `de` | German | `pt` | Portuguese |
| `es` | Spanish | `ro` | Romanian |
| `fr` | French | `ru` | Russian |
| `hu` | Hungarian | `sv` | Swedish |
| `it` | Italian | `zh-Hans` | Chinese (Simplified) |
| `ja` | Japanese | | |

Strings follow `CultureInfo.CurrentUICulture` unless
`UnoDock.Properties.Resources.Culture` is set. Regional cultures fall back to
their parent (`pt-BR` → `pt`, `de-AT` → `de`, `zh-CN` → `zh-Hans`) and then to
English.

Applications can override any string, or add a language, by culture name;
application entries take precedence over the bundled ones:

```csharp
using UnoDock.Properties;

Resources.Translations["pl"] = new Dictionary<string, string>
{
    ["Document_Close"] = "Zamknij",
    ["Document_CloseAll"] = "Zamknij wszystkie",
};
```

The keys are the public string properties of `UnoDock.Properties.Resources`
(for example `Document_CloseAllButThis`, `Anchorable_AutoHide`,
`Window_Restore`, `Pane_OpenDocuments`). Refresh open menus by reopening them;
new chrome picks up the current culture when it is created or refreshed.

The `localization` desktop suite verifies that every bundled culture translates
every key, regional fallback, application overrides and a localized default
document menu.
