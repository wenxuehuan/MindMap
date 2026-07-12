namespace SWDT;

public sealed class CanvasSettings
{
    public string CanvasBackgroundColor { get; set; } = "#FFFFFF";

    public bool ShowGrid { get; set; }

    public double GridSize { get; set; } = 24;

    public string GridColor { get; set; } = "#E5E7EB";

    public bool SnapToGrid { get; set; }

    public string LayoutDirection { get; set; } = "Right";

    public double HorizontalGap { get; set; } = 70;

    public double VerticalGap { get; set; } = 12;
}
