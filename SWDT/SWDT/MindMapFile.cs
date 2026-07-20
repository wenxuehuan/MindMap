namespace SWDT;

public sealed class MindMapFile
{
    public const int CurrentFormatVersion = 2;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public MindMapNode? Root { get; set; }

    public CanvasSettings CanvasSettings { get; set; } = new();

    public List<MindMapConnection> Connections { get; set; } = [];

    public string DocumentMarkdown { get; set; } = string.Empty;

    public List<MarkdownAsset> MarkdownAssets { get; set; } = [];
}
