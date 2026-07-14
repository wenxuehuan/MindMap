namespace SWDT;

internal readonly record struct LayoutFootprint(double Start, double End)
{
    public LayoutFootprint Union(LayoutFootprint other)
    {
        return new LayoutFootprint(Math.Min(Start, other.Start), Math.Max(End, other.End));
    }

}

internal readonly record struct LayoutBranchSpan(int StartIndex, int EndIndex);

internal static class LayoutFootprintGeometry
{
    public static IReadOnlyList<LayoutBranchSpan> CreateMergedBranchGroups(
        int branchCount,
        IEnumerable<LayoutBranchSpan> sourceSpans)
    {
        if (branchCount <= 0)
        {
            return [];
        }

        List<LayoutBranchSpan> mergedSourceSpans = [];
        foreach (LayoutBranchSpan span in sourceSpans
            .Where(span => span.StartIndex >= 0 && span.EndIndex >= span.StartIndex && span.StartIndex < branchCount)
            .Select(span => new LayoutBranchSpan(span.StartIndex, Math.Min(span.EndIndex, branchCount - 1)))
            .OrderBy(span => span.StartIndex)
            .ThenBy(span => span.EndIndex))
        {
            if (mergedSourceSpans.Count == 0 || span.StartIndex > mergedSourceSpans[^1].EndIndex)
            {
                mergedSourceSpans.Add(span);
                continue;
            }

            LayoutBranchSpan previous = mergedSourceSpans[^1];
            mergedSourceSpans[^1] = new LayoutBranchSpan(
                previous.StartIndex,
                Math.Max(previous.EndIndex, span.EndIndex));
        }

        List<LayoutBranchSpan> groups = [];
        int sourceSpanIndex = 0;
        for (int branchIndex = 0; branchIndex < branchCount;)
        {
            if (sourceSpanIndex < mergedSourceSpans.Count &&
                mergedSourceSpans[sourceSpanIndex].StartIndex == branchIndex)
            {
                LayoutBranchSpan sourceSpan = mergedSourceSpans[sourceSpanIndex++];
                groups.Add(sourceSpan);
                branchIndex = sourceSpan.EndIndex + 1;
            }
            else
            {
                groups.Add(new LayoutBranchSpan(branchIndex, branchIndex));
                branchIndex++;
            }
        }

        return groups;
    }

    public static IReadOnlyList<double> GetCumulativeForwardOffsets(
        IReadOnlyList<LayoutFootprint> footprints,
        double gap)
    {
        if (footprints.Count == 0)
        {
            return [];
        }

        double safeGap = Math.Max(0, gap);
        double cumulativeOffset = 0;
        double occupiedEnd = footprints[0].End;
        double[] offsets = new double[footprints.Count];

        for (int index = 1; index < footprints.Count; index++)
        {
            double shiftedStart = footprints[index].Start + cumulativeOffset;
            double requiredOffset = Math.Max(0, occupiedEnd + safeGap - shiftedStart);
            cumulativeOffset += requiredOffset;
            offsets[index] = cumulativeOffset;
            occupiedEnd = Math.Max(occupiedEnd, footprints[index].End + cumulativeOffset);
        }

        return offsets;
    }
}
