namespace SWDT;

internal static class NodeLayoutHierarchy
{
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
