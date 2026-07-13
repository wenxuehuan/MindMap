namespace SWDT;

internal enum NodeLayoutAxis
{
    Horizontal,
    Vertical
}

internal readonly record struct NodeLayoutSize(double Width, double Height);

internal readonly record struct NodeLayoutOffset(double X, double Y);

internal readonly record struct NodeLayoutRect(double Left, double Top, double Right, double Bottom);

internal static class NodeLayoutGeometry
{
    public static NodeLayoutOffset GetAnchoredOffset(
        string direction,
        NodeLayoutSize oldSize,
        NodeLayoutSize newSize)
    {
        double centeredX = (oldSize.Width - newSize.Width) / 2;
        double centeredY = (oldSize.Height - newSize.Height) / 2;

        return direction switch
        {
            "Left" or "DownLeft" => new NodeLayoutOffset(oldSize.Width - newSize.Width, centeredY),
            "Up" => new NodeLayoutOffset(centeredX, oldSize.Height - newSize.Height),
            "Down" => new NodeLayoutOffset(centeredX, 0),
            "Root" => new NodeLayoutOffset(centeredX, centeredY),
            _ => new NodeLayoutOffset(0, centeredY)
        };
    }

    public static double GetBackwardSeparation(
        NodeLayoutRect fixedRect,
        NodeLayoutRect movingRect,
        double gap,
        NodeLayoutAxis axis)
    {
        double fixedStart = axis == NodeLayoutAxis.Horizontal ? fixedRect.Left : fixedRect.Top;
        double movingEnd = axis == NodeLayoutAxis.Horizontal ? movingRect.Right : movingRect.Bottom;
        return Math.Min(0, fixedStart - gap - movingEnd);
    }

    public static double GetForwardSeparation(
        NodeLayoutRect fixedRect,
        NodeLayoutRect movingRect,
        double gap,
        NodeLayoutAxis axis)
    {
        double fixedEnd = axis == NodeLayoutAxis.Horizontal ? fixedRect.Right : fixedRect.Bottom;
        double movingStart = axis == NodeLayoutAxis.Horizontal ? movingRect.Left : movingRect.Top;
        return Math.Max(0, fixedEnd + gap - movingStart);
    }
}
