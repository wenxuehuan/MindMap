using Xunit;

namespace SWDT.Tests;

public sealed class MindMapFileVersionTests
{
    [Fact]
    public void ReadAndValidate_WhenVersionIsMissing_TreatsFileAsVersionOne()
    {
        Assert.Equal(1, MindMapFileVersion.ReadAndValidate("{\"Root\":{}}"));
    }

    [Fact]
    public void ReadAndValidate_WhenVersionIsNewer_RejectsFile()
    {
        UnsupportedMindMapFileVersionException exception = Assert.Throws<UnsupportedMindMapFileVersionException>(
            () => MindMapFileVersion.ReadAndValidate("{\"FormatVersion\":99}"));
        Assert.Equal(99, exception.Version);
    }

    [Fact]
    public void ReadAndValidate_WhenVersionIsCurrent_AcceptsFile()
    {
        Assert.Equal(MindMapFile.CurrentFormatVersion, MindMapFileVersion.ReadAndValidate("{\"FormatVersion\":2}"));
    }
}
