using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaaTools.Core.SynagogueExcel;

/// <summary>
/// Loads the field schema from the embedded TargetFieldSchema.json resource.
/// </summary>
public static class TargetFieldSchemaProvider
{
    private const string ResourceName = "FaaTools.Core.SynagogueExcel.TargetFieldSchema.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TargetFieldSchema LoadDefault()
    {
        var assembly = typeof(TargetFieldSchemaProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {ResourceName}");

        return JsonSerializer.Deserialize<TargetFieldSchema>(stream, Options)
            ?? throw new InvalidOperationException("Could not parse the Synagogue Excel field schema.");
    }
}
