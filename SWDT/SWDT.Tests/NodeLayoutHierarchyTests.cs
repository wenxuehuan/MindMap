using Xunit;

namespace SWDT.Tests;

public sealed class NodeLayoutHierarchyTests
{
    [Fact]
    public void FindSourceBranchSpan_WhenSourcesAreNested_ReturnsEntireOwningRange()
    {
        MindMapNode firstSource = new();
        MindMapNode secondSource = new();
        MindMapNode firstBranch = NodeWithChildren(firstSource);
        MindMapNode secondBranch = new();
        MindMapNode thirdBranch = NodeWithChildren(secondSource);
        MindMapNode ancestor = NodeWithChildren(firstBranch, secondBranch, thirdBranch);
        LinkParents(ancestor);

        LayoutBranchSpan? result = NodeLayoutHierarchy.FindSourceBranchSpan(
            ancestor,
            [firstBranch, secondBranch, thirdBranch],
            [firstSource, secondSource]);

        Assert.Equal(new LayoutBranchSpan(0, 2), result);
    }

    [Fact]
    public void FindSourceBranchSpan_WhenNoSourceBelongsToAncestor_ReturnsNull()
    {
        MindMapNode branch = new();
        MindMapNode ancestor = NodeWithChildren(branch);
        MindMapNode unrelatedSource = new();
        LinkParents(ancestor);

        LayoutBranchSpan? result = NodeLayoutHierarchy.FindSourceBranchSpan(
            ancestor,
            [branch],
            [unrelatedSource]);

        Assert.Null(result);
    }

    [Fact]
    public void GetStructuralConnectorStyle_WhenParentIsSummary_UsesChildStyle()
    {
        MindMapNode summary = new() { IsSummary = true };
        MindMapNode child = new();

        MindMapNode result = NodeLayoutHierarchy.GetStructuralConnectorStyle(summary, child);

        Assert.Same(child, result);
    }

    [Fact]
    public void FindChildStyleSource_WhenParentIsSummary_UsesFirstRegularSource()
    {
        MindMapNode firstSource = new();
        MindMapNode secondSource = new();
        MindMapNode summary = new()
        {
            IsSummary = true,
            SummarySourceIds = [secondSource.Id, firstSource.Id]
        };

        MindMapNode? result = NodeLayoutHierarchy.FindChildStyleSource(
            summary,
            [firstSource, summary, secondSource]);

        Assert.Same(secondSource, result);
    }

    [Fact]
    public void TraverseStructuralNodes_ExcludesSummaryAndItsDescendants()
    {
        MindMapNode summaryChild = new() { Title = "Summary child" };
        MindMapNode summary = new()
        {
            Title = "Summary",
            IsSummary = true,
            Children = [summaryChild]
        };
        MindMapNode regularChild = new() { Title = "Regular child" };
        MindMapNode root = new()
        {
            Title = "Root",
            Children = [regularChild, summary]
        };

        List<MindMapNode> result = NodeLayoutHierarchy.TraverseStructuralNodes(root).ToList();

        Assert.Equal([root, regularChild], result);
        Assert.DoesNotContain(summaryChild, result);
    }

    [Fact]
    public void TraverseStructuralNodes_WhenBranchIsCollapsed_ExcludesDescendants()
    {
        MindMapNode child = new() { Title = "Child" };
        MindMapNode root = new()
        {
            Title = "Root",
            IsCollapsed = true,
            Children = [child]
        };

        List<MindMapNode> result = NodeLayoutHierarchy.TraverseStructuralNodes(root).ToList();

        Assert.Equal([root], result);
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
