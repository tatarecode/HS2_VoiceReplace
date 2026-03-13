using System.Security.Cryptography;
using System.Text;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    private void SyncSelectedSampleAssetsToTextFields()
    {
        var normal = GetSampleAssetById(_normalSampleAssetId);
        var ero = GetSampleAssetById(_eroSampleAssetId) ?? normal;
        _txtNormalSample.Text = normal != null ? GetAssetAbsolutePath(normal) : "";
        _txtEroSample.Text = ero != null ? GetAssetAbsolutePath(ero) : "";
        _txtNormalSegment.Text = "";
        _txtEroSegment.Text = "";
        _manualNormalSegment = null;
        _manualEroSegment = null;
    }

    private SampleAssetItem? GetSampleAssetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return _sampleAssets.FirstOrDefault(x =>
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted);
    }

    private string GetAssetAbsolutePath(SampleAssetItem item)
    {
        if (Path.IsPathRooted(item.RelativeWavPath))
            return item.RelativeWavPath;
        return Path.GetFullPath(Path.Combine(_outputRootFixed, item.RelativeWavPath));
    }

    private string ToRelativeUnderOutput(string fullPath)
    {
        var full = Path.GetFullPath(fullPath);
        if (!full.StartsWith(_outputRootFixed, StringComparison.OrdinalIgnoreCase))
            return full;
        return Path.GetRelativePath(_outputRootFixed, full);
    }

    private void SaveSampleAssetsCatalog()
    {
        Directory.CreateDirectory(SampleAssetsRoot);
        EnsureSelectedSampleAssets();
        var cat = new SampleAssetsCatalog
        {
            Version = 1,
            HashAlgorithmVersion = SampleHashAlgorithmVersion,
            NormalSampleAssetId = _normalSampleAssetId,
            EroSampleAssetId = _eroSampleAssetId,
            Items = _sampleAssets.OrderBy(x => x.IsDeleted).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList(),
        };
        var json = System.Text.Json.JsonSerializer.Serialize(cat, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SampleAssetsCatalogPath, json, new UTF8Encoding(false));
    }

    private static string ComputeFileSha256Hex(string path)
    {
        using var fs = File.OpenRead(path);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (double DurationSec, int SampleRateHz, int Channels) ProbeWavInfo(string path)
    {
        // Parse only the minimal RIFF chunks we need so the catalog can be refreshed without
        // spawning ffprobe for every sample asset.
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        if (new string(br.ReadChars(4)) != "RIFF")
            throw new InvalidOperationException(UiTextCatalog.Get("error.wavMissingRiffHeader"));
        br.ReadInt32();
        if (new string(br.ReadChars(4)) != "WAVE")
            throw new InvalidOperationException(UiTextCatalog.Get("error.wavMissingWaveHeader"));

        int channels = 1;
        int sampleRate = 32000;
        int bitsPerSample = 16;
        long dataBytes = 0;
        while (fs.Position + 8 <= fs.Length)
        {
            var chunkId = new string(br.ReadChars(4));
            var chunkSize = br.ReadInt32();
            var next = fs.Position + chunkSize + (chunkSize % 2);
            if (chunkId == "fmt ")
            {
                br.ReadInt16();
                channels = br.ReadInt16();
                sampleRate = br.ReadInt32();
                br.ReadInt32();
                br.ReadInt16();
                bitsPerSample = br.ReadInt16();
            }
            else if (chunkId == "data")
            {
                dataBytes = chunkSize;
            }
            fs.Position = Math.Min(next, fs.Length);
        }

        var bytesPerSample = Math.Max(1, channels * Math.Max(1, bitsPerSample / 8));
        var totalSamples = dataBytes / bytesPerSample;
        var duration = sampleRate > 0 ? totalSamples / (double)sampleRate : 0.0;
        return (duration, sampleRate, channels);
    }
}

