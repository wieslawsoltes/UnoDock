using System.Globalization;
using System.Reflection;
using UnoDock.Layout;
using Strings = UnoDock.Properties.Resources;

namespace UnoDock.Testing;
/// <summary>Bundled chrome translations, culture fallback, application
/// overrides and localized default menus.</summary>
internal static class LocalizationTests
{
    private static readonly string[] Keys = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(string)).Select(p => p.Name).ToArray();
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        var original = Strings.Culture;
        tests.Test("every bundled culture translates every chrome string", () =>
        {
            Check.Equal(13, Strings.BundledCultures.Count);
            foreach (var culture in Strings.BundledCultures)
                foreach (var key in Keys)
                {
                    var text = Strings.ResourceManager.GetString(key, new CultureInfo(culture));
                    Check.True(!string.IsNullOrWhiteSpace(text), $"{culture} has no {key}.");
                    Check.True(text != Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture), $"{culture} {key} is untranslated.");
                }
        });
        tests.Test("regional cultures fall back to their bundled parent", () =>
        {
            Check.Equal("Schließen", Strings.ResourceManager.GetString("Document_Close", new CultureInfo("de-AT")));
            Check.Equal("Fechar", Strings.ResourceManager.GetString("Document_Close", new CultureInfo("pt-BR")));
            Check.Equal("Zavřít", Strings.ResourceManager.GetString("Document_Close", new CultureInfo("cs-CZ")));
            Check.Equal("关闭", Strings.ResourceManager.GetString("Document_Close", new CultureInfo("zh-CN")));
            Check.Equal("Close", Strings.ResourceManager.GetString("Document_Close", new CultureInfo("ko-KR")));
        });
        tests.Test("application translations win over bundled ones", () =>
        {
            Strings.Translations["de"] = new Dictionary<string, string>
            {
                ["Document_Close"] = "Zumachen"
            };
            try
            {
                Check.Equal("Zumachen", Strings.ResourceManager.GetString("Document_Close", new CultureInfo("de-DE")));
                Check.Equal("Alle schließen", Strings.ResourceManager.GetString("Document_CloseAll", new CultureInfo("de-DE")));
            }
            finally
            {
                Strings.Translations.Remove("de");
            }
        });
        tests.Test("the default document menu follows Resources.Culture", async () =>
        {
            var manager = new DockingManager
            {
                FloatingWindowMode = FloatingWindowMode.InSurface
            };
            var document = new LayoutDocument
            {
                Title = "Localized",
                ContentId = "localized"
            };
            manager.Layout = new()
            {
                RootPanel = new(new LayoutDocumentPane(document))
            };
            var window = new Window
            {
                Content = manager
            };
            window.Activate();
            try
            {
                await Task.Delay(200);
                Strings.Culture = new CultureInfo("fr-FR");
                var item = manager.GetLayoutItemFromModel(document);
                var menu = (MenuFlyout)item.GetType().GetMethod("GetDefaultContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, [manager])!;
                menu.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(menu, null);
                var texts = menu.Items.OfType<MenuFlyoutItem>().Select(i => i.Text).ToArray();
                Check.True(texts.Contains("Fermer") && texts.Contains("Tout fermer"), "Menu rows: " + string.Join(", ", texts));
            }
            finally
            {
                Strings.Culture = original;
                window.Close();
            }
        });
        return await tests.Run(output, "localization");
    }
}
