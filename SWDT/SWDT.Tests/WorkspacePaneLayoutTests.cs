using Xunit;

namespace SWDT.Tests;

public sealed class WorkspacePaneLayoutTests
{
    [Theory]
    [InlineData("Markdown", true, true)]
    [InlineData("Markdown", false, false)]
    [InlineData("MindMap", true, false)]
    [InlineData("MindMap", false, true)]
    public void GetMarkdownPaneOnLeft_UsesDraggedPaneAndDropSide(
        string draggedPaneName,
        bool dropOnLeft,
        bool expected)
    {
        WorkspacePaneKind draggedPane = Enum.Parse<WorkspacePaneKind>(draggedPaneName);

        Assert.Equal(expected, WorkspacePaneLayout.GetMarkdownPaneOnLeft(draggedPane, dropOnLeft));
    }
}
