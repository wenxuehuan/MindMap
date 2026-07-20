using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SWDT;

public sealed record MarkdownImportResult(
    string Markdown,
    List<MarkdownAsset> ImportedAssets,
    List<string> MissingFiles);

public static partial class MarkdownFileInterop
{
    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    public static MarkdownImportResult Import(string markdownFilePath)
    {
        string fullPath = Path.GetFullPath(markdownFilePath);
        string sourceDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string markdown = File.ReadAllText(fullPath);
        List<MarkdownAsset> assets = [];
        List<string> missingFiles = [];

        markdown = ImageRegex().Replace(markdown, match =>
        {
            string destination = UnwrapDestination(match.Groups["destination"].Value);
            if (string.IsNullOrWhiteSpace(destination) ||
                destination.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith("swdt-asset://", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            MarkdownAsset? asset = destination.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? TryCreateAssetFromDataUri(destination)
                : TryCreateAssetFromFile(destination, sourceDirectory, missingFiles);
            if (asset is null)
            {
                return match.Value;
            }

            assets.Add(asset);
            return ReplaceDestination(match, match.Groups["destination"], asset.Uri);
        });

        return new MarkdownImportResult(markdown, assets, missingFiles);
    }

    public static void Export(
        string markdownFilePath,
        string markdown,
        IReadOnlyCollection<MarkdownAsset> availableAssets)
    {
        string fullPath = Path.GetFullPath(markdownFilePath);
        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string baseName = Path.GetFileNameWithoutExtension(fullPath);
        string assetsDirectoryName = $"{baseName}.assets";
        string assetsDirectory = Path.Combine(directory, assetsDirectoryName);
        Dictionary<Guid, MarkdownAsset> assetsById = availableAssets.ToDictionary(asset => asset.Id);
        IReadOnlySet<Guid> referencedIds = MarkdownAssetReferences.FindReferencedAssetIds([markdown]);

        if (referencedIds.Count > 0)
        {
            Directory.CreateDirectory(assetsDirectory);
        }

        foreach (Guid id in referencedIds)
        {
            if (!assetsById.TryGetValue(id, out MarkdownAsset? asset))
            {
                continue;
            }

            string safeName = CreateExportFileName(asset);
            File.WriteAllBytes(Path.Combine(assetsDirectory, safeName), asset.Data);
            markdown = ReplaceAssetUri(markdown, id, $"{assetsDirectoryName}/{safeName}");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, markdown, new UTF8Encoding(false));
    }

    public static bool IsSupportedImageFile(string filePath)
    {
        return MediaTypes.ContainsKey(Path.GetExtension(filePath));
    }

    public static MarkdownAsset CreateAssetFromFile(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string extension = Path.GetExtension(fullPath);
        if (!MediaTypes.TryGetValue(extension, out string? mediaType))
        {
            throw new InvalidOperationException($"Unsupported image format: {extension}");
        }

        return new MarkdownAsset
        {
            FileName = Path.GetFileName(fullPath),
            MediaType = mediaType,
            Data = File.ReadAllBytes(fullPath)
        };
    }

    private static MarkdownAsset? TryCreateAssetFromFile(
        string destination,
        string sourceDirectory,
        ICollection<string> missingFiles)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(destination.Replace('/', Path.DirectorySeparatorChar));
            string path = Path.IsPathRooted(decoded) ? decoded : Path.Combine(sourceDirectory, decoded);
            if (!File.Exists(path) || !IsSupportedImageFile(path))
            {
                missingFiles.Add(destination);
                return null;
            }

            return CreateAssetFromFile(path);
        }
        catch
        {
            missingFiles.Add(destination);
            return null;
        }
    }

    private static MarkdownAsset? TryCreateAssetFromDataUri(string destination)
    {
        Match match = DataUriRegex().Match(destination);
        if (!match.Success)
        {
            return null;
        }

        string mediaType = match.Groups["mediaType"].Value.ToLowerInvariant();
        string extension = mediaType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => string.Empty
        };
        if (extension.Length == 0)
        {
            return null;
        }

        try
        {
            return new MarkdownAsset
            {
                FileName = $"image{extension}",
                MediaType = mediaType,
                Data = Convert.FromBase64String(match.Groups["data"].Value)
            };
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string UnwrapDestination(string destination)
    {
        string value = destination.Trim();
        if (value.Length >= 2 && value[0] == '<' && value[^1] == '>')
        {
            value = value[1..^1];
        }

        int titleSeparator = value.IndexOf(" \"", StringComparison.Ordinal);
        return titleSeparator >= 0 ? value[..titleSeparator] : value;
    }

    private static string ReplaceDestination(Match match, Group group, string destination)
    {
        int relativeIndex = group.Index - match.Index;
        return match.Value.Remove(relativeIndex, group.Length).Insert(relativeIndex, destination);
    }

    private static string CreateExportFileName(MarkdownAsset asset)
    {
        string extension = Path.GetExtension(asset.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = MediaTypes.FirstOrDefault(pair => pair.Value == asset.MediaType).Key ?? ".bin";
        }

        string baseName = Path.GetFileNameWithoutExtension(asset.FileName);
        char[] invalid = Path.GetInvalidFileNameChars();
        string safeBaseName = new(baseName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        safeBaseName = string.IsNullOrWhiteSpace(safeBaseName) ? "image" : safeBaseName;
        return $"{safeBaseName}-{asset.Id:N}{extension.ToLowerInvariant()}";
    }

    private static string ReplaceAssetUri(string markdown, Guid id, string relativePath)
    {
        return Regex.Replace(
            markdown,
            $@"swdt-asset://{id:D}(?:/[^\s\)\]\}}\""']*)?",
            relativePath,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"!\[[^\]]*\]\((?<destination><[^>]+>|[^\)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"^data:(?<mediaType>image/(?:png|jpeg|gif|webp));base64,(?<data>[A-Za-z0-9+/=\r\n]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataUriRegex();
}
