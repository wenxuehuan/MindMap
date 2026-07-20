namespace SWDT;

[Flags]
internal enum NotePopupResizeEdge
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8
}

internal readonly record struct NotePopupBounds(double Left, double Top, double Width, double Height);

internal static class NotePopupResizeGeometry
{
    public static NotePopupBounds Resize(
        NotePopupBounds current,
        NotePopupResizeEdge edges,
        double horizontalChange,
        double verticalChange,
        double viewportWidth,
        double viewportHeight,
        double minimumWidth,
        double minimumHeight)
    {
        double left = current.Left;
        double top = current.Top;
        double right = current.Left + current.Width;
        double bottom = current.Top + current.Height;

        if (edges.HasFlag(NotePopupResizeEdge.Left))
        {
            left = Math.Clamp(left + horizontalChange, 0, right - minimumWidth);
        }
        else if (edges.HasFlag(NotePopupResizeEdge.Right))
        {
            right = Math.Clamp(right + horizontalChange, left + minimumWidth, viewportWidth);
        }

        if (edges.HasFlag(NotePopupResizeEdge.Top))
        {
            top = Math.Clamp(top + verticalChange, 0, bottom - minimumHeight);
        }
        else if (edges.HasFlag(NotePopupResizeEdge.Bottom))
        {
            bottom = Math.Clamp(bottom + verticalChange, top + minimumHeight, viewportHeight);
        }

        return new NotePopupBounds(left, top, right - left, bottom - top);
    }
}
