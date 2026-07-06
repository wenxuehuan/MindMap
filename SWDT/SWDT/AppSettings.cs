namespace SWDT;

public sealed class AppSettings
{
    public List<string> RecentFiles { get; set; } = [];

    public string ThemeMode { get; set; } = "System";

    public string Language { get; set; } = "zh-CN";
}
