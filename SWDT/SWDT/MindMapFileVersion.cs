using System.Text.Json;

namespace SWDT;

public sealed class UnsupportedMindMapFileVersionException(int version)
    : NotSupportedException($"Unsupported SWDT file format version: {version}.")
{
    public int Version { get; } = version;
}

public static class MindMapFileVersion
{
    public static int ReadAndValidate(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty(nameof(MindMapFile.FormatVersion), out JsonElement versionElement) ||
            !versionElement.TryGetInt32(out int version))
        {
            return 1;
        }

        if (version < 1 || version > MindMapFile.CurrentFormatVersion)
        {
            throw new UnsupportedMindMapFileVersionException(version);
        }

        return version;
    }
}
