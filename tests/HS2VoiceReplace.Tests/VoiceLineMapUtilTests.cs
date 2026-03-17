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

    [Fact]
    public void EnumerateVoiceLineTextAssetFiles_Finds_ListDirectories_Recursively()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_voiceline_scan_");
        try
        {
            var dir = Path.Combine(tempRoot.FullName, "nested", "list_h_sound_voice_30");
            Directory.CreateDirectory(dir);
            var textAsset = Path.Combine(dir, "sample.TextAsset");
            File.WriteAllText(textAsset, "line");
            File.WriteAllText(Path.Combine(dir, "ignore.txt"), "x");

            var files = VoiceLineMapUtil.EnumerateVoiceLineTextAssetFiles(tempRoot.FullName);

            Assert.Single(files);
            Assert.Equal(textAsset, files[0]);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public void BuildVoiceLineMapFromTextAssetFiles_Merges_FirstEntry_PerClip_AcrossFiles()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_voiceline_files_");
        try
        {
            var dir = Path.Combine(tempRoot.FullName, "list_h_sound_voice_30");
            Directory.CreateDirectory(dir);
            var fileA = Path.Combine(dir, "a.TextAsset");
            var fileB = Path.Combine(dir, "b.TextAsset");
            File.WriteAllLines(fileA, new[]
            {
                "こんにちは\tunused\tabdata/sound/data/pcm/c02/adv/30.unity3d\thsa_02_000_00_00_00_00",
            });
            File.WriteAllLines(fileB, new[]
            {
                "上書きされない\tunused\tabdata/sound/data/pcm/c02/adv/30.unity3d\thsa_02_000_00_00_00_00",
                "別セリフ\tunused\tabdata/sound/data/pcm/c02/h/50.unity3d\thsh_02_00_00_000_00_00",
            });

            var map = VoiceLineMapUtil.BuildVoiceLineMapFromTextAssetFiles(new[] { fileA, fileB });

            Assert.Equal("こんにちは", map["adv/hsa_02_000_00_00_00_00.wav"]);
            Assert.Equal("別セリフ", map["h/hsh_02_00_00_000_00_00.wav"]);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public void ChoosePreferredVoiceLineMap_PrefersNonEmptyCachedMap()
    {
        var cached = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adv/a.wav"] = "cached line",
        };
        var rebuilt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adv/b.wav"] = "rebuilt line",
        };

        var actual = VoiceLineMapUtil.ChoosePreferredVoiceLineMap(cached, rebuilt);

        Assert.Single(actual);
        Assert.Equal("cached line", actual["adv/a.wav"]);
    }

    [Fact]
    public void ChoosePreferredVoiceLineMap_UsesRebuiltMapWhenCacheIsEmpty()
    {
        var cached = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rebuilt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adv/b.wav"] = "rebuilt line",
        };

        var actual = VoiceLineMapUtil.ChoosePreferredVoiceLineMap(cached, rebuilt);

        Assert.Single(actual);
        Assert.Equal("rebuilt line", actual["adv/b.wav"]);
    }
}

