using Xunit;

namespace SWDT.Tests;

public sealed class DocumentWindowTitleTests
{
    [Fact]
    public void Format_SavedFile_ShowsCurrentFileNameAndSavedStatus()
    {
        MindMapDocument document = CreateDocument();
        document.FilePath = @"C:\Maps\project.swdtmap";
        document.MarkClean();

        string title = DocumentWindowTitle.Format(document, "已保存", "未保存");

        Assert.Equal("project.swdtmap - 已保存", title);
    }

    [Fact]
    public void Format_DirtyFile_ShowsCurrentFileNameAndUnsavedStatus()
    {
        MindMapDocument document = CreateDocument();
        document.FilePath = @"C:\Maps\project.swdtmap";
        document.MarkDirty();

        string title = DocumentWindowTitle.Format(document, "已保存", "未保存");

        Assert.Equal("project.swdtmap - 未保存", title);
    }

    [Fact]
    public void Format_UntitledDocument_ShowsUntitledNameAndUnsavedStatus()
    {
        MindMapDocument document = CreateDocument();

        string title = DocumentWindowTitle.Format(document, "已保存", "未保存");

        Assert.Equal("未命名 1 - 未保存", title);
    }

    private static MindMapDocument CreateDocument()
    {
        return new MindMapDocument(new MindMapNode(), "未命名 1");
    }
}
