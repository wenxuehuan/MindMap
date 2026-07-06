namespace SWDT;

public sealed class MindMapConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceNodeId { get; set; }

    public Guid TargetNodeId { get; set; }

    public string Title { get; set; } = "关系";

    public string Color { get; set; } = "#2563EB";

    public double Thickness { get; set; } = 2;

    public string DashStyle { get; set; } = "Solid";

    public string Shape { get; set; } = "Curve";

    public string ArrowStyle { get; set; } = "None";
}
