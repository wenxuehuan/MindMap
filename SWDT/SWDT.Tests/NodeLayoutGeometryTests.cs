using Xunit;

namespace SWDT.Tests;

public sealed class NodeLayoutGeometryTests
{
    [Theory]
    [InlineData("Right", "Vertical")]
    [InlineData("Left", "Vertical")]
    [InlineData("DownRight", "Vertical")]
    [InlineData("DownLeft", "Vertical")]
    [InlineData("Down", "Horizontal")]
    [InlineData("Up", "Horizontal")]
    public void GetChildGroupAxis_UsesPerpendicularSiblingAxis(
        string direction,
        string expectedAxisName)
    {
        NodeLayoutAxis expected = Enum.Parse<NodeLayoutAxis>(expectedAxisName);

        Assert.Equal(expected, NodeLayoutGeometry.GetChildGroupAxis(direction));
    }

    [Theory]
    [InlineData("Vertical", -15)]
    [InlineData("Horizontal", 15)]
    public void GetCenteredChildGroupOffset_UsesFirstAndLastSubtreeCenters(
        string axisName,
        double expected)
    {
        NodeLayoutAxis axis = Enum.Parse<NodeLayoutAxis>(axisName);
        NodeLayoutRect firstBounds = axis == NodeLayoutAxis.Vertical
            ? new NodeLayoutRect(400, 60, 500, 100)
            : new NodeLayoutRect(50, 400, 90, 500);
        NodeLayoutRect lastBounds = axis == NodeLayoutAxis.Vertical
            ? new NodeLayoutRect(400, 200, 500, 260)
            : new NodeLayoutRect(270, 400, 330, 500);

        double result = NodeLayoutGeometry.GetCenteredChildGroupOffset(
            new NodeLayoutRect(100, 100, 300, 180),
            firstBounds,
            lastBounds,
            axis);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetCenteredChildGroupOffset_WithSingleChild_AlignsCenters()
    {
        NodeLayoutRect childBounds = new(400, 180, 500, 220);

        double result = NodeLayoutGeometry.GetCenteredChildGroupOffset(
            new NodeLayoutRect(100, 100, 300, 180),
            childBounds,
            childBounds,
            NodeLayoutAxis.Vertical);

        Assert.Equal(-60, result);
    }

    [Theory]
    [InlineData("Right", 370, 100)]
    [InlineData("Left", 30, 100)]
    [InlineData("DownRight", 370, 100)]
    [InlineData("DownLeft", 30, 100)]
    [InlineData("Down", 100, 192)]
    [InlineData("Up", 100, 88)]
    public void GetSummaryChildOrigin_StartsAtSummaryEdge(
        string direction,
        double expectedX,
        double expectedY)
    {
        NodeLayoutOffset result = NodeLayoutGeometry.GetSummaryChildOrigin(
            direction,
            new NodeLayoutRect(100, 100, 300, 180),
            70,
            12);

        Assert.Equal(new NodeLayoutOffset(expectedX, expectedY), result);
    }

    [Theory]
    [InlineData("Left", -180, -18)]
    [InlineData("DownLeft", -180, -18)]
    [InlineData("Right", 0, -18)]
    [InlineData("Down", -90, 0)]
    [InlineData("Up", -90, -36)]
    [InlineData("Root", -90, -18)]
    public void GetAnchoredOffset_PreservesConnectionSide(
        string direction,
        double expectedX,
        double expectedY)
    {
        NodeLayoutOffset result = NodeLayoutGeometry.GetAnchoredOffset(
            direction,
            new NodeLayoutSize(100, 42),
            new NodeLayoutSize(280, 78));

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
    }

    [Fact]
    public void GetAnchoredOffset_WhenSizeReturnsToBaseline_HasNoResidualOffset()
    {
        NodeLayoutOffset result = NodeLayoutGeometry.GetAnchoredOffset(
            "Left",
            new NodeLayoutSize(100, 42),
            new NodeLayoutSize(100, 42));

        Assert.Equal(new NodeLayoutOffset(0, 0), result);
    }

    [Theory]
    [InlineData("Vertical", -22)]
    [InlineData("Horizontal", -112)]
    public void GetBackwardSeparation_MovesOnlyTowardNegativeAxis(
        string axisName,
        double expected)
    {
        NodeLayoutAxis axis = Enum.Parse<NodeLayoutAxis>(axisName);
        NodeLayoutRect fixedRect = new(100, 100, 200, 180);
        NodeLayoutRect movingRect = new(100, 40, 200, 110);

        double result = NodeLayoutGeometry.GetBackwardSeparation(fixedRect, movingRect, 12, axis);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Vertical", 22)]
    [InlineData("Horizontal", 112)]
    public void GetForwardSeparation_MovesOnlyTowardPositiveAxis(
        string axisName,
        double expected)
    {
        NodeLayoutAxis axis = Enum.Parse<NodeLayoutAxis>(axisName);
        NodeLayoutRect fixedRect = new(100, 100, 200, 180);
        NodeLayoutRect movingRect = new(100, 170, 200, 250);

        double result = NodeLayoutGeometry.GetForwardSeparation(fixedRect, movingRect, 12, axis);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Separation_WhenGapAlreadyExists_DoesNotMoveBranch()
    {
        NodeLayoutRect fixedRect = new(100, 100, 200, 180);
        NodeLayoutRect before = new(100, 20, 200, 80);
        NodeLayoutRect after = new(100, 200, 200, 260);

        Assert.Equal(0, NodeLayoutGeometry.GetBackwardSeparation(
            fixedRect,
            before,
            12,
            NodeLayoutAxis.Vertical));
        Assert.Equal(0, NodeLayoutGeometry.GetForwardSeparation(
            fixedRect,
            after,
            12,
            NodeLayoutAxis.Vertical));
    }
}
