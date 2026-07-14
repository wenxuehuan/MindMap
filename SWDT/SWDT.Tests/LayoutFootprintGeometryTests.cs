using Xunit;

namespace SWDT.Tests;

public sealed class LayoutFootprintGeometryTests
{
    [Fact]
    public void GetCumulativeForwardOffsets_WhenSummaryExtendsFirstBranch_MovesFollowingBranches()
    {
        LayoutFootprint[] footprints =
        [
            new(100, 520),
            new(270, 340),
            new(360, 420)
        ];

        IReadOnlyList<double> result = LayoutFootprintGeometry.GetCumulativeForwardOffsets(footprints, 12);

        Assert.Equal([0, 262, 262], result);
    }

    [Fact]
    public void GetCumulativeForwardOffsets_WhenBranchesAlreadyHaveSpace_DoesNotMove()
    {
        LayoutFootprint[] footprints = [new(100, 180), new(200, 280), new(300, 360)];

        IReadOnlyList<double> result = LayoutFootprintGeometry.GetCumulativeForwardOffsets(footprints, 12);

        Assert.Equal([0, 0, 0], result);
    }

    [Fact]
    public void GetCumulativeForwardOffsets_AccumulatesIndependentCollisions()
    {
        LayoutFootprint[] footprints = [new(0, 100), new(80, 180), new(170, 250)];

        IReadOnlyList<double> result = LayoutFootprintGeometry.GetCumulativeForwardOffsets(footprints, 10);

        Assert.Equal([0, 30, 50], result);
    }

    [Fact]
    public void Union_ExpandsToIncludeSummaryDecoration()
    {
        LayoutFootprint structural = new(100, 240);
        LayoutFootprint summary = new(40, 520);

        LayoutFootprint result = structural.Union(summary);

        Assert.Equal(new LayoutFootprint(40, 520), result);
    }

    [Fact]
    public void CreateMergedBranchGroups_OverlappingSourceSpansBecomeOneGroup()
    {
        IReadOnlyList<LayoutBranchSpan> result = LayoutFootprintGeometry.CreateMergedBranchGroups(
            6,
            [new LayoutBranchSpan(1, 3), new LayoutBranchSpan(2, 4)]);

        Assert.Equal(
            [new LayoutBranchSpan(0, 0), new LayoutBranchSpan(1, 4), new LayoutBranchSpan(5, 5)],
            result);
    }

    [Fact]
    public void CreateMergedBranchGroups_AdjacentIndependentSpansStaySeparate()
    {
        IReadOnlyList<LayoutBranchSpan> result = LayoutFootprintGeometry.CreateMergedBranchGroups(
            5,
            [new LayoutBranchSpan(0, 1), new LayoutBranchSpan(2, 3)]);

        Assert.Equal(
            [new LayoutBranchSpan(0, 1), new LayoutBranchSpan(2, 3), new LayoutBranchSpan(4, 4)],
            result);
    }

    [Fact]
    public void CreateMergedBranchGroups_WithoutSummariesReturnsSingletonBranches()
    {
        IReadOnlyList<LayoutBranchSpan> result = LayoutFootprintGeometry.CreateMergedBranchGroups(3, []);

        Assert.Equal(
            [new LayoutBranchSpan(0, 0), new LayoutBranchSpan(1, 1), new LayoutBranchSpan(2, 2)],
            result);
    }

    [Fact]
    public void ExtendedChildFootprint_PropagatesIntoAncestorSpacing()
    {
        LayoutFootprint structuralChild = new(100, 240);
        LayoutFootprint summarySubtree = new(180, 520);
        LayoutFootprint augmentedChild = structuralChild.Union(summarySubtree);

        IReadOnlyList<double> result = LayoutFootprintGeometry.GetCumulativeForwardOffsets(
            [augmentedChild, new LayoutFootprint(270, 350)],
            12);

        Assert.Equal([0, 262], result);
    }

    [Fact]
    public void FullSummaryFootprint_WhenItExtendsBackward_MovesEntireSourceGroupForward()
    {
        LayoutFootprint sourceGroup = new LayoutFootprint(150, 220)
            .Union(new LayoutFootprint(-40, 300));

        IReadOnlyList<double> result = LayoutFootprintGeometry.GetCumulativeForwardOffsets(
            [new LayoutFootprint(0, 100), sourceGroup, new LayoutFootprint(330, 390)],
            12);

        Assert.Equal([0, 152, 152], result);
    }

    [Fact]
    public void GetCumulativeForwardOffsets_AfterOffsetsAreApplied_IsIdempotent()
    {
        LayoutFootprint[] original = [new(100, 520), new(270, 340), new(360, 420)];
        IReadOnlyList<double> firstOffsets = LayoutFootprintGeometry.GetCumulativeForwardOffsets(original, 12);
        LayoutFootprint[] shifted = original
            .Select((footprint, index) => new LayoutFootprint(
                footprint.Start + firstOffsets[index],
                footprint.End + firstOffsets[index]))
            .ToArray();

        IReadOnlyList<double> secondOffsets = LayoutFootprintGeometry.GetCumulativeForwardOffsets(shifted, 12);

        Assert.Equal([0, 0, 0], secondOffsets);
    }
}
