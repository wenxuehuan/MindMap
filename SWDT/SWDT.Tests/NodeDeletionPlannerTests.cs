using Xunit;

namespace SWDT.Tests;

public sealed class NodeDeletionPlannerTests
{
    [Fact]
    public void Create_WhenParentAndChildAreSelected_DeletesSubtreeOnce()
    {
        MindMapNode grandchild = new();
        MindMapNode child = NodeWithChildren(grandchild);
        MindMapNode root = CanvasWithRoots(NodeWithChildren(child));

        NodeDeletionPlan result = NodeDeletionPlanner.Create(root, new HashSet<Guid> { child.Id, grandchild.Id });

        Assert.Equal([child], result.Targets);
        Assert.Equal(2, result.RemovedNodeIds.Count);
        Assert.Contains(child.Id, result.RemovedNodeIds);
        Assert.Contains(grandchild.Id, result.RemovedNodeIds);
    }

    [Fact]
    public void Create_WhenSeparateBranchesAreSelected_DeletesBothSubtrees()
    {
        MindMapNode firstDescendant = new();
        MindMapNode first = NodeWithChildren(firstDescendant);
        MindMapNode second = new();
        MindMapNode visibleRoot = NodeWithChildren(first, second);
        MindMapNode root = CanvasWithRoots(visibleRoot);

        NodeDeletionPlan result = NodeDeletionPlanner.Create(root, new HashSet<Guid> { first.Id, second.Id });

        Assert.Equal([first, second], result.Targets);
        Assert.Equal(3, result.RemovedNodeIds.Count);
        Assert.False(result.RemovesAllVisibleRoots);
    }

    [Fact]
    public void Create_WhenAllVisibleRootsAreSelected_ProtectsRootInvariant()
    {
        MindMapNode firstRoot = new();
        MindMapNode secondRoot = new();
        MindMapNode root = CanvasWithRoots(firstRoot, secondRoot);

        NodeDeletionPlan result = NodeDeletionPlanner.Create(root, new HashSet<Guid> { firstRoot.Id, secondRoot.Id });

        Assert.True(result.RemovesAllVisibleRoots);
    }

    private static MindMapNode CanvasWithRoots(params MindMapNode[] roots)
    {
        MindMapNode canvas = new() { IsCanvasRoot = true, Children = [.. roots] };
        LinkParents(canvas);
        return canvas;
    }

    private static MindMapNode NodeWithChildren(params MindMapNode[] children)
    {
        return new MindMapNode { Children = [.. children] };
    }

    private static void LinkParents(MindMapNode node, MindMapNode? parent = null)
    {
        node.Parent = parent;
        foreach (MindMapNode child in node.Children)
        {
            LinkParents(child, node);
        }
    }
}
