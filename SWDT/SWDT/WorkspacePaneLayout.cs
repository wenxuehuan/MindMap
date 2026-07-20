namespace SWDT;

internal enum WorkspacePaneKind
{
    MindMap,
    Markdown
}

internal static class WorkspacePaneLayout
{
    public static bool GetMarkdownPaneOnLeft(WorkspacePaneKind draggedPane, bool dropOnLeft)
    {
        return draggedPane == WorkspacePaneKind.Markdown
            ? dropOnLeft
            : !dropOnLeft;
    }
}
