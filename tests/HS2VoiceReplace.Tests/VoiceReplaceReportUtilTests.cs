using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceReplaceReportUtilTests
{
    [Fact]
    public void ParseSeedReportStatusMap_ReadsStatusAndNote()
    {
        var lines = new[]
        {
            "relative_path,bucket,source_file,style_file,output_file,status,exit_code,note",
            "\"adv/a.wav\",\"normal\",\"src\",\"style\",\"out\",\"fallback_src\",\"0\",\"copied\"",
        };

        var parsed = VoiceReplaceReportUtil.ParseSeedReportStatusMap(lines);

        Assert.Equal(("fallback_src", "copied"), parsed["adv/a.wav"]);
    }

    [Fact]
    public void BuildMergedSeedReportLines_UsesExistingStatusOrFallsBackToDisk()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_report_merge_");
        try
        {
            var outWavRoot = Path.Combine(tempRoot.FullName, "wav");
            Directory.CreateDirectory(Path.Combine(outWavRoot, "adv"));
            File.WriteAllText(Path.Combine(outWavRoot, "adv", "b.wav"), "exists");

            var lines = VoiceReplaceReportUtil.BuildMergedSeedReportLines(
                new[]
                {
                    ("adv/a.wav", "normal", "src-a"),
                    ("adv/b.wav", "normal", "src-b"),
                    ("adv/c.wav", "normal", "src-c"),
                },
                outWavRoot,
                new Dictionary<string, (string Status, string Note)>
                {
                    ["adv/a.wav"] = ("fallback_src", "copied"),
                });

            Assert.Contains(lines, line => line.Contains("\"adv/a.wav\"") && line.Contains("\"fallback_src\"") && line.Contains("\"copied\""));
            Assert.Contains(lines, line => line.Contains("\"adv/b.wav\"") && line.Contains("\"ok\""));
            Assert.Contains(lines, line => line.Contains("\"adv/c.wav\"") && line.Contains("\"pending\""));
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }
}

