namespace SWDT;

public sealed record DocumentHistoryEntry(
    string RootJson,
    string CanvasSettingsJson,
    string ConnectionsJson,
    string DocumentMarkdown,
    List<Guid> SelectedNodeIds,
    Guid? SelectedNodeId,
    Guid? SelectedConnectionId,
    double CanvasScale,
    double CanvasOffsetX,
    double CanvasOffsetY,
    bool IsDirty);
