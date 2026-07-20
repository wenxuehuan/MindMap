using System.Text.Json;
using Xunit;

namespace SWDT.Tests;

public sealed class MindMapFileMarkdownTests
{
    [Fact]
    public void SerializeRoundTrip_PreservesMarkdownNoteAndAsset()
    {
        MindMapNode root = new() { HasNote = true, NoteMarkdown = "备注 **内容**" };
        MarkdownAsset asset = new() { FileName = "图.png", Data = [1, 2, 3] };
        MindMapFile source = new()
        {
            Root = root,
            DocumentMarkdown = $"# 总文档\n\n![图]({asset.Uri})",
            MarkdownAssets = [asset]
        };

        string json = JsonSerializer.Serialize(source);
        MindMapFile? result = JsonSerializer.Deserialize<MindMapFile>(json);

        Assert.NotNull(result);
        Assert.Equal(MindMapFile.CurrentFormatVersion, result.FormatVersion);
        Assert.Equal(source.DocumentMarkdown, result.DocumentMarkdown);
        Assert.Equal(root.NoteMarkdown, result.Root!.NoteMarkdown);
        Assert.True(result.Root.HasNote);
        Assert.Equal(asset.Data, Assert.Single(result.MarkdownAssets).Data);
    }
}
