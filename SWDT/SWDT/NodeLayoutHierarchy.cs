namespace SWDT;

internal static class NodeLayoutHierarchy
{
    public static MindMapNode? GetDirectChildUnderAncestor(MindMapNode node, MindMapNode ancestor)
    {
        MindMapNode current = node;
        while (current.Parent is not null && current.Parent != ancestor)
        {
            current = current.Parent;
        }

        return current.Parent == ancestor ? current : null;
    }

    public static LayoutBranchSpan? FindSourceBranchSpan(
        MindMapNode ancestor,
        IReadOnlyList<MindMapNode> branches,
        IEnumerable<MindMapNode> sources)
    {
        HashSet<Guid> sourceBranchIds = sources
            .Select(source => GetDirectChildUnderAncestor(source, ancestor))
            .Where(branch => branch is not null)
            .Select(branch => branch!.Id)
            .ToHashSet();

        List<int> sourceIndexes = branches
            .Select((branch, index) => sourceBranchIds.Contains(branch.Id) ? index : -1)
            .Where(index => index >= 0)
            .ToList();

        return sourceIndexes.Count == 0
            ? null
            : new LayoutBranchSpan(sourceIndexes.Min(), sourceIndexes.Max());
    }

    public static MindMapNode GetStructuralConnectorStyle(MindMapNode parent, MindMapNode child)
    {
        return parent.IsSummary ? child : parent;
    }

    public static MindMapNode? FindChildStyleSource(
        MindMapNode parent,
        IEnumerable<MindMapNode> availableNodes)
    {
        if (!parent.IsSummary)
        {
            return parent;
        }

        Dictionary<Guid, MindMapNode> nodeById = availableNodes
            .Where(node => !node.IsSummary)
            .ToDictionary(node => node.Id);
        foreach (Guid sourceId in parent.SummarySourceIds)
        {
            if (nodeById.TryGetValue(sourceId, out MindMapNode? source))
            {
                return source;
            }
        }

        return parent.Parent is { IsCanvasRoot: false, IsSummary: false } structuralParent
            ? structuralParent
            : null;
    }

    public static IEnumerable<MindMapNode> TraverseStructuralNodes(MindMapNode node)
    {
        if (node.IsSummary)
        {
            yield break;
        }

        yield return node;
        if (node.IsCollapsed)
        {
            yield break;
        }

        foreach (MindMapNode child in node.Children)
        {
            foreach (MindMapNode descendant in TraverseStructuralNodes(child))
            {
                yield return descendant;
            }
        }
    }
}
