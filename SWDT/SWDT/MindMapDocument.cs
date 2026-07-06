using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SWDT;

public sealed class MindMapDocument : INotifyPropertyChanged
{
    private string? _filePath;
    private bool _isDirty = true;

    public MindMapDocument(MindMapNode root, string untitledName)
    {
        Root = root;
        UntitledName = untitledName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MindMapNode Root { get; set; }

    public CanvasSettings CanvasSettings { get; set; } = new();

    public List<MindMapConnection> Connections { get; set; } = [];

    public string UntitledName { get; }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value)
            {
                return;
            }

            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
            {
                return;
            }

            _isDirty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName
    {
        get
        {
            string name = FilePath is null ? UntitledName : Path.GetFileName(FilePath);
            return IsDirty ? $"{name}*" : name;
        }
    }

    public MindMapNode? SelectedNode { get; set; }

    public HashSet<Guid> SelectedNodeIds { get; } = [];

    public Guid? SelectedConnectionId { get; set; }

    public Stack<DocumentHistoryEntry> UndoStack { get; } = [];

    public Stack<DocumentHistoryEntry> RedoStack { get; } = [];

    public double CanvasScale { get; set; } = 1;

    public double CanvasOffsetX { get; set; }

    public double CanvasOffsetY { get; set; }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void MarkClean()
    {
        IsDirty = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
