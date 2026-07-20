using System.IO;
using System.Text.RegularExpressions;

namespace SWDT;

public static partial class MarkdownAssetReferences
{
    private const string AssetScheme = "swdt-asset";

    public static string CreateUri(MarkdownAsset asset)
    {
        string fileName = Uri.EscapeDataString(Path.GetFileName(asset.FileName));
        return $"{AssetScheme}://{asset.Id:D}/{fileName}";
    }

    public static IReadOnlySet<Guid> FindReferencedAssetIds(IEnumerable<string?> markdownValues)
    {
        HashSet<Guid> result = [];
        foreach (string? markdown in markdownValues)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                continue;
            }

            foreach (Match match in AssetUriRegex().Matches(markdown))
            {
                if (Guid.TryParse(match.Groups["id"].Value, out Guid id))
                {
                    result.Add(id);
                }
            }
        }

        return result;
    }

    public static List<MarkdownAsset> GetReferencedAssets(
        IEnumerable<MarkdownAsset> assets,
        IEnumerable<string?> markdownValues)
    {
        IReadOnlySet<Guid> referencedIds = FindReferencedAssetIds(markdownValues);
        return assets.Where(asset => referencedIds.Contains(asset.Id)).ToList();
    }

    public static IEnumerable<string?> EnumerateDocumentMarkdown(MindMapDocument document)
    {
        yield return document.DocumentMarkdown;
        foreach (MindMapNode node in Traverse(document.Root))
        {
            if (node.HasNote)
            {
                yield return node.NoteMarkdown;
            }
        }
    }

    private static IEnumerable<MindMapNode> Traverse(MindMapNode node)
    {
        yield return node;
        foreach (MindMapNode child in node.Children)
        {
            foreach (MindMapNode descendant in Traverse(child))
            {
                yield return descendant;
            }
        }
    }

    [GeneratedRegex(@"swdt-asset://(?<id>[0-9a-fA-F-]{36})(?:/[^\s\)\]\}\""']*)?", RegexOptions.CultureInvariant)]
    private static partial Regex AssetUriRegex();
}
