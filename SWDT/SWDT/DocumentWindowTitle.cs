using System.IO;

namespace SWDT;

public static class DocumentWindowTitle
{
    public static string Format(MindMapDocument document, string savedStatus, string unsavedStatus)
    {
        string fileName = document.FilePath is null
            ? document.UntitledName
            : Path.GetFileName(document.FilePath);
        string status = document.IsDirty ? unsavedStatus : savedStatus;
        return $"{fileName} - {status}";
    }
}
