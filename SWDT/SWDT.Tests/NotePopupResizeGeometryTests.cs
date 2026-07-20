using Xunit;

namespace SWDT.Tests;

public sealed class NotePopupResizeGeometryTests
{
    [Fact]
    public void Resize_RightBottom_ChangesSizeWithoutMovingOrigin()
    {
        NotePopupBounds result = NotePopupResizeGeometry.Resize(
            new NotePopupBounds(100, 80, 520, 360),
            NotePopupResizeEdge.Right | NotePopupResizeEdge.Bottom,
            40,
            30,
            1000,
            800,
            360,
            240);

        Assert.Equal(new NotePopupBounds(100, 80, 560, 390), result);
    }

    [Fact]
    public void Resize_LeftTop_MovesOriginAndPreservesOppositeCorner()
    {
        NotePopupBounds result = NotePopupResizeGeometry.Resize(
            new NotePopupBounds(100, 80, 520, 360),
            NotePopupResizeEdge.Left | NotePopupResizeEdge.Top,
            -40,
            -30,
            1000,
            800,
            360,
            240);

        Assert.Equal(new NotePopupBounds(60, 50, 560, 390), result);
    }

    [Fact]
    public void Resize_LeftTop_RespectsMinimumSize()
    {
        NotePopupBounds result = NotePopupResizeGeometry.Resize(
            new NotePopupBounds(100, 80, 520, 360),
            NotePopupResizeEdge.Left | NotePopupResizeEdge.Top,
            300,
            300,
            1000,
            800,
            360,
            240);

        Assert.Equal(new NotePopupBounds(260, 200, 360, 240), result);
    }

    [Fact]
    public void Resize_RightBottom_StaysInsideViewport()
    {
        NotePopupBounds result = NotePopupResizeGeometry.Resize(
            new NotePopupBounds(100, 80, 520, 360),
            NotePopupResizeEdge.Right | NotePopupResizeEdge.Bottom,
            1000,
            1000,
            700,
            500,
            360,
            240);

        Assert.Equal(new NotePopupBounds(100, 80, 600, 420), result);
    }
}
