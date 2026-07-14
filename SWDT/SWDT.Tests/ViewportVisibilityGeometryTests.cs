using Xunit;

namespace SWDT.Tests;

public sealed class ViewportVisibilityGeometryTests
{
    private static readonly ViewportVisibilitySize Viewport = new(800, 600);

    [Fact]
    public void GetTranslationDelta_WhenNodeIsVisible_DoesNotMoveViewport()
    {
        ViewportTranslationDelta result = Calculate(new(100, 100, 200, 100));

        Assert.Equal(new ViewportTranslationDelta(0, 0), result);
    }

    [Theory]
    [InlineData(-10, 100, 200, 100, 34, 0)]
    [InlineData(650, 100, 200, 100, -74, 0)]
    [InlineData(100, -20, 200, 100, 0, 44)]
    [InlineData(100, 550, 200, 100, 0, -74)]
    public void GetTranslationDelta_WhenOneEdgeOverflows_MovesMinimumDistance(
        double left,
        double top,
        double width,
        double height,
        double expectedX,
        double expectedY)
    {
        ViewportTranslationDelta result = Calculate(new(left, top, width, height));

        Assert.Equal(new ViewportTranslationDelta(expectedX, expectedY), result);
    }

    [Fact]
    public void GetTranslationDelta_WhenTwoAxesOverflow_AdjustsBothAxes()
    {
        ViewportTranslationDelta result = Calculate(new(760, 570, 100, 80));

        Assert.Equal(new ViewportTranslationDelta(-84, -74), result);
    }

    [Fact]
    public void GetTranslationDelta_AppliesScaleAndExistingTranslation()
    {
        ViewportTranslationDelta result = ViewportVisibilityGeometry.GetTranslationDelta(
            new ViewportVisibilityBounds(300, 150, 100, 80),
            Viewport,
            2,
            50,
            20,
            24);

        Assert.Equal(new ViewportTranslationDelta(-74, 0), result);
    }

    [Fact]
    public void GetTranslationDelta_WhenNodeIsOversized_AlignsTopLeft()
    {
        ViewportTranslationDelta result = Calculate(new(-100, -50, 1000, 700));

        Assert.Equal(new ViewportTranslationDelta(124, 74), result);
    }

    [Fact]
    public void GetTranslationDelta_WhenNodeIsOversized_PrioritizesCaretBounds()
    {
        ViewportTranslationDelta result = ViewportVisibilityGeometry.GetTranslationDelta(
            new ViewportVisibilityBounds(0, 0, 1000, 700),
            Viewport,
            1,
            0,
            0,
            24,
            new ViewportVisibilityBounds(500, 610, 1, 20));

        Assert.Equal(new ViewportTranslationDelta(0, -54), result);
    }

    [Theory]
    [InlineData(0, 600, 1)]
    [InlineData(800, 0, 1)]
    [InlineData(800, 600, 0)]
    public void GetTranslationDelta_WhenViewportOrScaleIsInvalid_DoesNotMove(
        double viewportWidth,
        double viewportHeight,
        double scale)
    {
        ViewportTranslationDelta result = ViewportVisibilityGeometry.GetTranslationDelta(
            new ViewportVisibilityBounds(100, 100, 200, 100),
            new ViewportVisibilitySize(viewportWidth, viewportHeight),
            scale,
            0,
            0,
            24);

        Assert.Equal(new ViewportTranslationDelta(0, 0), result);
    }

    private static ViewportTranslationDelta Calculate(ViewportVisibilityBounds bounds)
    {
        return ViewportVisibilityGeometry.GetTranslationDelta(bounds, Viewport, 1, 0, 0, 24);
    }
}
