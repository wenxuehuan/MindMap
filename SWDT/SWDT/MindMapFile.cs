namespace SWDT;

public sealed class MindMapFile
{
    public MindMapNode? Root { get; set; }

    public CanvasSettings CanvasSettings { get; set; } = new();

    public List<MindMapConnection> Connections { get; set; } = [];
}
