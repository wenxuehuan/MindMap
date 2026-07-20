using System.Text.Json.Serialization;

namespace SWDT;

public sealed class MindMapNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "新主题";

    public bool IsCanvasRoot { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public string FillColor { get; set; } = "#FFFFFF";

    public string BorderColor { get; set; } = "#CBD5E1";

    public string TextColor { get; set; } = "#0F172A";

    public string FontFamily { get; set; } = "";

    public double FontSize { get; set; }

    public string FontWeight { get; set; } = "";

    public string FontStyle { get; set; } = "";

    public string TextAlignment { get; set; } = "";

    public string Shape { get; set; } = "RoundedRectangle";

    public double BorderThickness { get; set; } = 1;

    public string ConnectorColor { get; set; } = "";

    public double ConnectorThickness { get; set; }

    public string ConnectorDashStyle { get; set; } = "";

    public string ConnectorShape { get; set; } = "";

    public string ConnectorArrowStyle { get; set; } = "";

    public string LayoutDirection { get; set; } = "";

    public double HorizontalGap { get; set; }

    public double VerticalGap { get; set; }

    public bool IsSummary { get; set; }

    public bool IsCollapsed { get; set; }

    public bool HasNote { get; set; }

    public string NoteMarkdown { get; set; } = string.Empty;

    public List<Guid> SummarySourceIds { get; set; } = [];

    public List<MindMapNode> Children { get; set; } = [];

    [JsonIgnore]
    public MindMapNode? Parent { get; set; }
}
