using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class PartialRebuildGridDataUtilTests
{
    [Fact]
    public void ParseCsvLine_HandlesQuotedCommasAndEscapedQuotes()
    {
        var cols = PartialRebuildGridDataUtil.ParseCsvLine("\"a,b\",\"c\"\"d\",plain");
        Assert.Equal(new[] { "a,b", "c\"d", "plain" }, cols);
    }

    [Fact]
    public void TryBuildRelativePathFromVoiceTextAsset_ConvertsBundlePathToRelativeWav()
    {
        var ok = PartialRebuildGridDataUtil.TryBuildRelativePathFromVoiceTextAsset(
            @"abdata/sound/data/pcm/c02/adv/30.unity3d",
            "hsa_02_000_00_00_00_00",
            out var rel);

        Assert.True(ok);
        Assert.Equal("adv/hsa_02_000_00_00_00_00.wav", rel);
    }

    [Fact]
    public void BuildDisplayStatus_MapsPendingWithOutput()
    {
        var status = PartialRebuildGridDataUtil.BuildDisplayStatus(UiLanguage.En, "pending", convertedExists: true);
        Assert.Equal("Output exists (not in current run)", status);
    }

    [Fact]
    public void ParseRunLevelSampleSignatures_ReadsNormalAndEro()
    {
        var rows = new[]
        {
            "kind,sha256",
            "\"normal\",\"sig-n\"",
            "\"ero\",\"sig-e\"",
            "\"combined\",\"sig-c\"",
        };

        var parsed = PartialRebuildGridDataUtil.ParseRunLevelSampleSignatures(rows);

        Assert.Equal("sig-n", parsed.Normal);
        Assert.Equal("sig-e", parsed.Ero);
    }

    [Fact]
    public void ParseSampleSignatureMap_ReadsPerRowSignatures()
    {
        var rows = new[]
        {
            "relative_path,bucket,output_file,sig_normal,sig_ero,sig_used",
            "\"adv/test.wav\",\"normal\",\"C:\\\\out.wav\",\"n1\",\"e1\",\"n1\"",
        };

        var parsed = PartialRebuildGridDataUtil.ParseSampleSignatureMap(rows);

        Assert.True(parsed.TryGetValue("adv/test.wav", out var row));
        Assert.Equal("n1", row.Normal);
        Assert.Equal("e1", row.Ero);
        Assert.Equal("n1", row.Used);
    }
}

