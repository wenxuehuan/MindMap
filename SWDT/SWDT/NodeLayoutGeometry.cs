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
    public static NodeLayoutAxis GetChildGroupAxis(string direction)
    {
        return direction is "Down" or "Up"
            ? NodeLayoutAxis.Horizontal
            : NodeLayoutAxis.Vertical;
    }

    public static double GetCenteredChildGroupOffset(
        NodeLayoutRect parentRect,
        NodeLayoutRect firstChildBounds,
        NodeLayoutRect lastChildBounds,
        NodeLayoutAxis axis)
    {
        double parentCenter = axis == NodeLayoutAxis.Horizontal
            ? (parentRect.Left + parentRect.Right) / 2
            : (parentRect.Top + parentRect.Bottom) / 2;
        double firstChildCenter = axis == NodeLayoutAxis.Horizontal
            ? (firstChildBounds.Left + firstChildBounds.Right) / 2
            : (firstChildBounds.Top + firstChildBounds.Bottom) / 2;
        double lastChildCenter = axis == NodeLayoutAxis.Horizontal
            ? (lastChildBounds.Left + lastChildBounds.Right) / 2
            : (lastChildBounds.Top + lastChildBounds.Bottom) / 2;

        return parentCenter - (firstChildCenter + lastChildCenter) / 2;
    }

    public static NodeLayoutOffset GetSummaryChildOrigin(
        string direction,
        NodeLayoutRect summaryRect,
        double horizontalGap,
        double verticalGap)
    {
        return direction switch
        {
            "Left" or "DownLeft" => new NodeLayoutOffset(summaryRect.Left - horizontalGap, summaryRect.Top),
            "Down" => new NodeLayoutOffset(summaryRect.Left, summaryRect.Bottom + verticalGap),
            "Up" => new NodeLayoutOffset(summaryRect.Left, summaryRect.Top - verticalGap),
            _ => new NodeLayoutOffset(summaryRect.Right + horizontalGap, summaryRect.Top)
        };
    }

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
