using System.Text;
using Xunit;

namespace SWDT.Tests;

public sealed class MarkdownFileInteropTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"swdt-tests-{Guid.NewGuid():N}");

    public MarkdownFileInteropTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Import_LocalImage_EmbedsAndRewritesReference()
    {
        string imagePath = Path.Combine(_directory, "示例.png");
        File.WriteAllBytes(imagePath, [1, 2, 3, 4]);
        string markdownPath = Path.Combine(_directory, "document.md");
        File.WriteAllText(markdownPath, "# 标题\n\n![图片](示例.png)", Encoding.UTF8);

        MarkdownImportResult result = MarkdownFileInterop.Import(markdownPath);

        MarkdownAsset asset = Assert.Single(result.ImportedAssets);
        Assert.Empty(result.MissingFiles);
        Assert.Contains(asset.Uri, result.Markdown);
        Assert.Equal([1, 2, 3, 4], asset.Data);
    }

    [Fact]
    public void Import_DataUri_EmbedsImage()
    {
        string markdownPath = Path.Combine(_directory, "document.md");
        File.WriteAllText(markdownPath, "![x](data:image/png;base64,AQID)");

        MarkdownImportResult result = MarkdownFileInterop.Import(markdownPath);

        MarkdownAsset asset = Assert.Single(result.ImportedAssets);
        Assert.Equal([1, 2, 3], asset.Data);
        Assert.Contains(asset.Uri, result.Markdown);
    }

    [Fact]
    public void Export_WritesRelativeAssetAndMarkdownWithoutBom()
    {
        MarkdownAsset asset = new()
        {
            FileName = "same.png",
            MediaType = "image/png",
            Data = [7, 8, 9]
        };
        string markdownPath = Path.Combine(_directory, "notes.md");

        MarkdownFileInterop.Export(markdownPath, $"![x]({asset.Uri})", [asset]);

        string markdown = File.ReadAllText(markdownPath);
        Assert.Contains("notes.assets/same-", markdown);
        string exportedImage = Assert.Single(Directory.GetFiles(Path.Combine(_directory, "notes.assets")));
        Assert.Equal([7, 8, 9], File.ReadAllBytes(exportedImage));
        Assert.NotEqual(0xEF, File.ReadAllBytes(markdownPath)[0]);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
