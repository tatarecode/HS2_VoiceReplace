using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceLineMapUtilTests
{
    [Fact]
    public void BuildVoiceLineMapFromTextAssetLines_ParsesFirstValidEntryPerClip()
    {
        var lines = new[]
        {
            "こんにちは\tunused\tabdata/sound/data/pcm/c02/adv/30.unity3d\thsa_02_000_00_00_00_00",
            "上書きされない\tunused\tabdata/sound/data/pcm/c02/adv/30.unity3d\thsa_02_000_00_00_00_00",
            "別セリフ\tunused\tabdata/sound/data/pcm/c02/h/50.unity3d\thsh_02_00_00_000_00_00",
        };

        var map = VoiceLineMapUtil.BuildVoiceLineMapFromTextAssetLines(lines);

        Assert.Equal("こんにちは", map["adv/hsa_02_000_00_00_00_00.wav"]);
        Assert.Equal("別セリフ", map["h/hsh_02_00_00_000_00_00.wav"]);
    }

    [Fact]
    public void ParseAndSerializeVoiceLineMapCsv_RoundTripsQuotes()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adv/test.wav"] = "He said \"hello\"",
            ["h/test2.wav"] = "line 2",
        };

        var lines = VoiceLineMapUtil.SerializeVoiceLineMapCsv(source);
        var parsed = VoiceLineMapUtil.ParseVoiceLineMapCsv(lines);

        Assert.Equal(source["adv/test.wav"], parsed["adv/test.wav"]);
        Assert.Equal(source["h/test2.wav"], parsed["h/test2.wav"]);
    }

    [Fact]
    public void SaveVoiceLineMapCsv_WritesExpectedHeader()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_voiceline_");
        try
        {
            var path = Path.Combine(tempRoot.FullName, "voice_line_map.csv");
            var map = new Dictionary<string, string> { ["adv/test.wav"] = "sample line" };

            VoiceLineMapUtil.SaveVoiceLineMapCsv(path, map);

            var text = File.ReadAllText(path);
            Assert.Contains("relative_path,voice_line", text);
            Assert.Contains("adv/test.wav", text);
            Assert.Contains("sample line", text);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }
}

