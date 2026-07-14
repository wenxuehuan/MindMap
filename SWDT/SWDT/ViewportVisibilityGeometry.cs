namespace SWDT;

internal readonly record struct ViewportVisibilityBounds(
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

internal readonly record struct ViewportVisibilitySize(double Width, double Height);

internal readonly record struct ViewportTranslationDelta(double X, double Y);

internal static class ViewportVisibilityGeometry
{
    public static ViewportTranslationDelta GetTranslationDelta(
        ViewportVisibilityBounds worldBounds,
        ViewportVisibilitySize viewportSize,
        double scale,
        double translationX,
        double translationY,
        double margin,
        ViewportVisibilityBounds? oversizedPriorityViewportBounds = null)
    {
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0 || scale <= 0 ||
            worldBounds.Width < 0 || worldBounds.Height < 0)
        {
            return new ViewportTranslationDelta(0, 0);
        }

        double horizontalMargin = Math.Min(Math.Max(0, margin), Math.Max(0, (viewportSize.Width - 1) / 2));
        double verticalMargin = Math.Min(Math.Max(0, margin), Math.Max(0, (viewportSize.Height - 1) / 2));
        ViewportVisibilityBounds viewportBounds = new(
            worldBounds.Left * scale + translationX,
            worldBounds.Top * scale + translationY,
            worldBounds.Width * scale,
            worldBounds.Height * scale);

        double deltaX = GetAxisTranslationDelta(
            viewportBounds.Left,
            viewportBounds.Right,
            horizontalMargin,
            viewportSize.Width - horizontalMargin,
            oversizedPriorityViewportBounds?.Left,
            oversizedPriorityViewportBounds?.Right);
        double deltaY = GetAxisTranslationDelta(
            viewportBounds.Top,
            viewportBounds.Bottom,
            verticalMargin,
            viewportSize.Height - verticalMargin,
            oversizedPriorityViewportBounds?.Top,
            oversizedPriorityViewportBounds?.Bottom);

        return new ViewportTranslationDelta(deltaX, deltaY);
    }

    private static double GetAxisTranslationDelta(
        double targetStart,
        double targetEnd,
        double visibleStart,
        double visibleEnd,
        double? priorityStart,
        double? priorityEnd)
    {
        double targetLength = targetEnd - targetStart;
        double visibleLength = visibleEnd - visibleStart;
        if (targetLength <= visibleLength)
        {
            return GetContainmentDelta(targetStart, targetEnd, visibleStart, visibleEnd);
        }

        if (priorityStart is double preferredStart && priorityEnd is double preferredEnd)
        {
            return GetContainmentDelta(preferredStart, preferredEnd, visibleStart, visibleEnd);
        }

        return visibleStart - targetStart;
    }

    private static double GetContainmentDelta(
        double targetStart,
        double targetEnd,
        double visibleStart,
        double visibleEnd)
    {
        if (targetStart < visibleStart)
        {
            return visibleStart - targetStart;
        }

        if (targetEnd > visibleEnd)
        {
            return visibleEnd - targetEnd;
        }

        return 0;
    }
}
