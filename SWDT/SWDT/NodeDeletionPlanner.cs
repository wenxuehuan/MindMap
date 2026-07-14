namespace SWDT;

internal sealed record NodeDeletionPlan(
    IReadOnlyList<MindMapNode> Targets,
    IReadOnlySet<Guid> RemovedNodeIds,
    bool RemovesAllVisibleRoots);

internal static class NodeDeletionPlanner
{
    public static NodeDeletionPlan Create(MindMapNode root, IReadOnlySet<Guid> selectedNodeIds)
    {
        List<MindMapNode> visibleRoots = root.IsCanvasRoot ? root.Children : [root];
        List<MindMapNode> selectedNodes = Traverse(root)
            .Where(node => !node.IsCanvasRoot && selectedNodeIds.Contains(node.Id))
            .ToList();
        List<MindMapNode> targets = selectedNodes
            .Where(node => !HasSelectedAncestor(node, selectedNodeIds))
            .ToList();
        HashSet<Guid> removedNodeIds = targets
            .SelectMany(Traverse)
            .Select(node => node.Id)
            .ToHashSet();
        int removedRootCount = visibleRoots.Count(node => removedNodeIds.Contains(node.Id));

        return new NodeDeletionPlan(
            targets,
            removedNodeIds,
            visibleRoots.Count > 0 && removedRootCount == visibleRoots.Count);
    }

    private static bool HasSelectedAncestor(MindMapNode node, IReadOnlySet<Guid> selectedNodeIds)
    {
        MindMapNode? ancestor = node.Parent;
        while (ancestor is not null)
        {
            if (selectedNodeIds.Contains(ancestor.Id))
            {
                return true;
            }

            ancestor = ancestor.Parent;
        }

        return false;
    }

    private static IEnumerable<MindMapNode> Traverse(MindMapNode node)
    {
        yield return node;
        foreach (MindMapNode child in node.Children)
        {
            foreach (MindMapNode descendant in Traverse(child))
            {
                yield return descendant;
            }
        }
    }
}
