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
            "\"normal_name\",\"normal-sample\"",
            "\"ero_name\",\"ero-sample\"",
            "\"seedvc_summary\",\"engine=V1, steps=25\"",
        };

        var parsed = PartialRebuildGridDataUtil.ParseRunLevelSampleSignatures(rows);

        Assert.Equal("sig-n", parsed.Normal);
        Assert.Equal("sig-e", parsed.Ero);
        Assert.Equal("normal-sample", parsed.NormalName);
        Assert.Equal("ero-sample", parsed.EroName);
        Assert.Equal("engine=V1, steps=25", parsed.SeedVcSummary);
    }

    [Fact]
    public void ParseSampleSignatureMap_ReadsPerRowSignatures()
    {
        var rows = new[]
        {
            "relative_path,bucket,output_file,sig_normal,sig_ero,sig_used,sample_normal_name,sample_ero_name,sample_used_name,seed_vc_summary",
            "\"adv/test.wav\",\"normal\",\"C:\\\\out.wav\",\"n1\",\"e1\",\"n1\",\"normal-sample\",\"ero-sample\",\"normal-sample\",\"engine=V1, steps=25\"",
        };

        var parsed = PartialRebuildGridDataUtil.ParseSampleSignatureMap(rows);

        Assert.True(parsed.TryGetValue("adv/test.wav", out var row));
        Assert.Equal("n1", row.Normal);
        Assert.Equal("e1", row.Ero);
        Assert.Equal("n1", row.Used);
        Assert.Equal("normal-sample", row.NormalName);
        Assert.Equal("ero-sample", row.EroName);
        Assert.Equal("normal-sample", row.UsedName);
        Assert.Equal("engine=V1, steps=25", row.SeedVcSummary);
    }

    [Fact]
    public void ResolveDisplayedRowSampleSignatures_PrefersRunLevelWhenPerRowMapIsBlank()
    {
        var run = new PartialRebuildGridDataUtil.RunSampleSignatures("run-n", "run-e", "normal-sample", "ero-sample", "engine=V1");
        var map = new PartialRebuildGridDataUtil.RowSampleSignature("", "", "");

        var resolved = PartialRebuildGridDataUtil.ResolveDisplayedRowSampleSignatures("normal", run, map);

        Assert.Equal("run-n", resolved.Normal);
        Assert.Equal("run-e", resolved.Ero);
        Assert.Equal("run-n", resolved.Used);
        Assert.Equal("normal-sample", resolved.NormalName);
        Assert.Equal("ero-sample", resolved.EroName);
        Assert.Equal("normal-sample", resolved.UsedName);
        Assert.Equal("engine=V1", resolved.SeedVcSummary);
    }

    [Fact]
    public void ResolveDisplayedRowSampleSignatures_PreservesPreviousValuesWhenCurrentSourcesAreEmpty()
    {
        var run = new PartialRebuildGridDataUtil.RunSampleSignatures("", "");
        var previous = new PartialRebuildGridDataUtil.RowSampleSignature("prev-n", "prev-e", "prev-used", "prev-normal", "prev-ero", "prev-ero", "engine=V2");

        var resolved = PartialRebuildGridDataUtil.ResolveDisplayedRowSampleSignatures("ero", run, null, previous);

        Assert.Equal("prev-n", resolved.Normal);
        Assert.Equal("prev-e", resolved.Ero);
        Assert.Equal("prev-e", resolved.Used);
        Assert.Equal("prev-normal", resolved.NormalName);
        Assert.Equal("prev-ero", resolved.EroName);
        Assert.Equal("prev-ero", resolved.UsedName);
        Assert.Equal("engine=V2", resolved.SeedVcSummary);
    }

    [Fact]
    public void ResolveDisplayRawStatus_PromotesExistingOutputToOkWhenUsedSignatureMatches()
    {
        var status = PartialRebuildGridDataUtil.ResolveDisplayRawStatus(
            "normal",
            rawStatus: "",
            convertedExists: true,
            new PartialRebuildGridDataUtil.RowSampleSignature("sig-n", "sig-e", "sig-n"));

        Assert.Equal("ok", status);
    }

    [Fact]
    public void ResolveDisplayRawStatus_PromotesExistingOutputToPendingWhenExpectedSignatureExistsButUsedIsUnknown()
    {
        var status = PartialRebuildGridDataUtil.ResolveDisplayRawStatus(
            "ero",
            rawStatus: "",
            convertedExists: true,
            new PartialRebuildGridDataUtil.RowSampleSignature("sig-n", "sig-e", ""));

        Assert.Equal("pending", status);
    }
}

