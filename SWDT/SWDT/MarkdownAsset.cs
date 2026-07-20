namespace SWDT;

public sealed class MarkdownAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = "image.png";

    public string MediaType { get; set; } = "image/png";

    public byte[] Data { get; set; } = [];

    public string Uri => MarkdownAssetReferences.CreateUri(this);
}
