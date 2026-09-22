using System.Globalization;
using System.Resources;
namespace Xceed.Wpf.AvalonDock.Properties;
/// <summary>Independent English fallback strings. Applications can replace values in Translations by culture name.</summary>
public class Resources
{
    public static CultureInfo? Culture { get; set; }
    public static ResourceManager ResourceManager { get; } = new StringResources();
    public static IDictionary<string, IDictionary<string, string>> Translations { get; } = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> English = new()
    {
        ["Anchorable_AutoHide"] = "Auto Hide",
        ["Anchorable_BtnAutoHide_Hint"] = "Pin or auto-hide this tool",
        ["Anchorable_BtnClose_Hint"] = "Close tool",
        ["Anchorable_CxMenu_Hint"] = "Tool actions",
        ["Anchorable_Dock"] = "Dock",
        ["Anchorable_DockAsDocument"] = "Dock as document",
        ["Anchorable_Float"] = "Float",
        ["Anchorable_Hide"] = "Hide",
        ["Document_BtnPinned_Hint"] = "Pin document",
        ["Document_Close"] = "Close",
        ["Document_CloseAll"] = "Close all documents",
        ["Document_CloseAllButThis"] = "Close other documents",
        ["Document_CxMenu_Hint"] = "Document actions",
        ["Document_DockAsDocument"] = "Dock as document",
        ["Document_Float"] = "Float",
        ["Document_MoveToNextTabGroup"] = "Move to next tab group",
        ["Document_MoveToPreviousTabGroup"] = "Move to previous tab group",
        ["Document_NewHorizontalTabGroup"] = "New horizontal tab group",
        ["Document_NewVerticalTabGroup"] = "New vertical tab group",
        ["Window_Maximize"] = "Maximize",
        ["Window_Restore"] = "Restore",
    };
    public static string Anchorable_AutoHide => ResourceManager.GetString(nameof(Anchorable_AutoHide), Culture)!;
    public static string Anchorable_BtnAutoHide_Hint => ResourceManager.GetString(nameof(Anchorable_BtnAutoHide_Hint), Culture)!;
    public static string Anchorable_BtnClose_Hint => ResourceManager.GetString(nameof(Anchorable_BtnClose_Hint), Culture)!;
    public static string Anchorable_CxMenu_Hint => ResourceManager.GetString(nameof(Anchorable_CxMenu_Hint), Culture)!;
    public static string Anchorable_Dock => ResourceManager.GetString(nameof(Anchorable_Dock), Culture)!;
    public static string Anchorable_DockAsDocument => ResourceManager.GetString(nameof(Anchorable_DockAsDocument), Culture)!;
    public static string Anchorable_Float => ResourceManager.GetString(nameof(Anchorable_Float), Culture)!;
    public static string Anchorable_Hide => ResourceManager.GetString(nameof(Anchorable_Hide), Culture)!;
    public static string Document_BtnPinned_Hint => ResourceManager.GetString(nameof(Document_BtnPinned_Hint), Culture)!;
    public static string Document_Close => ResourceManager.GetString(nameof(Document_Close), Culture)!;
    public static string Document_CloseAll => ResourceManager.GetString(nameof(Document_CloseAll), Culture)!;
    public static string Document_CloseAllButThis => ResourceManager.GetString(nameof(Document_CloseAllButThis), Culture)!;
    public static string Document_CxMenu_Hint => ResourceManager.GetString(nameof(Document_CxMenu_Hint), Culture)!;
    public static string Document_DockAsDocument => ResourceManager.GetString(nameof(Document_DockAsDocument), Culture)!;
    public static string Document_Float => ResourceManager.GetString(nameof(Document_Float), Culture)!;
    public static string Document_MoveToNextTabGroup => ResourceManager.GetString(nameof(Document_MoveToNextTabGroup), Culture)!;
    public static string Document_MoveToPreviousTabGroup => ResourceManager.GetString(nameof(Document_MoveToPreviousTabGroup), Culture)!;
    public static string Document_NewHorizontalTabGroup => ResourceManager.GetString(nameof(Document_NewHorizontalTabGroup), Culture)!;
    public static string Document_NewVerticalTabGroup => ResourceManager.GetString(nameof(Document_NewVerticalTabGroup), Culture)!;
    public static string Window_Maximize => ResourceManager.GetString(nameof(Window_Maximize), Culture)!;
    public static string Window_Restore => ResourceManager.GetString(nameof(Window_Restore), Culture)!;
    private sealed class StringResources : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture)
        {
            var current = culture ?? CultureInfo.CurrentUICulture;
            while (!Equals(current, CultureInfo.InvariantCulture))
            {
                if (Translations.TryGetValue(current.Name, out var strings) && strings.TryGetValue(name, out var text)) return text;
                current = current.Parent;
            }
            return English.GetValueOrDefault(name);
        }
        public override string? GetString(string name) => GetString(name, Culture);
    }
}
