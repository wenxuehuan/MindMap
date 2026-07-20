using Xunit;

namespace SWDT.Tests;

public sealed class MarkdownAssetReferencesTests
{
    [Fact]
    public void FindReferencedAssetIds_ReturnsDistinctInternalReferences()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        IReadOnlySet<Guid> result = MarkdownAssetReferences.FindReferencedAssetIds(
        [
            $"![one](swdt-asset://{first:D}/one.png)",
            $"![again](swdt-asset://{first:D}/again.png) ![two](swdt-asset://{second:D}/two.jpg)"
        ]);

        Assert.Equal(2, result.Count);
        Assert.Contains(first, result);
        Assert.Contains(second, result);
    }

    [Fact]
    public void GetReferencedAssets_FiltersOrphanedAssets()
    {
        MarkdownAsset referenced = new();
        MarkdownAsset orphaned = new();

        List<MarkdownAsset> result = MarkdownAssetReferences.GetReferencedAssets(
            [referenced, orphaned],
            [$"![image]({referenced.Uri})"]);

        Assert.Single(result);
        Assert.Same(referenced, result[0]);
    }

    [Fact]
    public void EnumerateDocumentMarkdown_IncludesOnlyCreatedNodeNotes()
    {
        MindMapNode root = new()
        {
            NoteMarkdown = "not-created",
            Children =
            [
                new MindMapNode { HasNote = true, NoteMarkdown = "created" }
            ]
        };
        MindMapDocument document = new(root, "test") { DocumentMarkdown = "document" };

        Assert.Equal(["document", "created"], MarkdownAssetReferences.EnumerateDocumentMarkdown(document));
    }
}
