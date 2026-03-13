using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class GridStatusMapUtilTests
{
    [Fact]
    public void ParseStatusMap_ReadsRelativePathAndStatusColumns()
    {
        var lines = new[]
        {
            "relative_path,status,note",
            "\"adv/a.wav\",ok,\"done\"",
            "\"h/int/b.wav\",failed,\"boom\"",
        };

        var map = GridStatusMapUtil.ParseStatusMap(lines);

        Assert.Equal("ok", map["adv/a.wav"]);
        Assert.Equal("failed", map["h/int/b.wav"]);
    }

    [Fact]
    public void ParseStatusMap_ReturnsEmpty_WhenRequiredColumnsAreMissing()
    {
        var lines = new[]
        {
            "path,result",
            "adv/a.wav,ok",
        };

        var map = GridStatusMapUtil.ParseStatusMap(lines);

        Assert.Empty(map);
    }
}

