using System.Text.Json.Serialization;

namespace UnoDock.Browser;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrowserDockSnapshot))]
[JsonSerializable(typeof(BrowserDockRequest))]
[JsonSerializable(typeof(BrowserDockReport))]
[JsonSerializable(typeof(BrowserDockItem))]
internal partial class BrowserDockJsonContext : JsonSerializerContext
{
}
