using System.Globalization;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Import a source clip into the managed sample catalog, optionally slicing a user-selected range.
    private async Task<SampleAssetItem> ImportSampleAssetInternalAsync(
        string sourceFile,
        string name,
        StyleSegmentSelection? selection,
        bool silent)
    {
        sourceFile = Path.GetFullPath(sourceFile);
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException(T("error.sampleSourceNotFound"), sourceFile);

        var ffmpegExe = TryFindFfmpegExe();
        var id = Guid.NewGuid().ToString("N");
        var dst = Path.Combine(SampleAssetsActiveRoot, $"{id}.wav");
        Directory.CreateDirectory(SampleAssetsActiveRoot);

        // Normalize every managed sample to mono/32k WAV so downstream preview and VC code
        // sees a stable format regardless of the user's original file.
        if (selection != null)
        {
            if (string.IsNullOrWhiteSpace(ffmpegExe) || !File.Exists(ffmpegExe))
                throw new InvalidOperationException(T("error.sampleRangeNeedsFfmpeg"));
            var st = Math.Max(0, selection.StartSec).ToString("0.###", CultureInfo.InvariantCulture);
            var du = Math.Max(0.2, selection.DurationSec).ToString("0.###", CultureInfo.InvariantCulture);
            var args = $"-y -hide_banner -loglevel error -ss {st} -t {du} -i \"{sourceFile}\" -vn -sn -dn -ac 1 -ar 32000 -f wav \"{dst}\"";
            var r = await ProcessUtil.RunCaptureAsync(ffmpegExe, args, Directory.GetCurrentDirectory(), CancellationToken.None);
            if (r.ExitCode != 0 || !File.Exists(dst))
                throw new InvalidOperationException(T("error.sampleRangeExtractionFailed"));
        }
        else
        {
            if (Path.GetExtension(sourceFile).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFile, dst, overwrite: true);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ffmpegExe) || !File.Exists(ffmpegExe))
                    throw new InvalidOperationException(T("error.sampleImportNeedsFfmpeg"));
                var args = $"-y -hide_banner -loglevel error -i \"{sourceFile}\" -vn -sn -dn -ac 1 -ar 32000 -f wav \"{dst}\"";
                var r = await ProcessUtil.RunCaptureAsync(ffmpegExe, args, Directory.GetCurrentDirectory(), CancellationToken.None);
                if (r.ExitCode != 0 || !File.Exists(dst))
                    throw new InvalidOperationException(T("error.sampleImportFailed"));
            }
        }

        var info = ProbeWavInfo(dst);
        var srcStart = selection?.StartSec ?? 0;
        var srcDuration = selection?.DurationSec ?? info.DurationSec;
        var now = DateTime.UtcNow;
        var item = new SampleAssetItem
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? $"sample_{_sampleAssets.Count + 1:000}" : name.Trim(),
            SourceFilePath = sourceFile,
            SourceStartSec = srcStart,
            SourceDurationSec = srcDuration,
            RelativeWavPath = ToRelativeUnderOutput(dst),
            Signature = ComputeFileSha256Hex(dst),
            HashAlgorithmVersion = SampleHashAlgorithmVersion,
            DurationSec = info.DurationSec,
            SampleRateHz = info.SampleRateHz,
            Channels = info.Channels,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ExtractedAtUtc = now,
            IsDeleted = false,
        };
        _sampleAssets.Add(item);
        SaveSampleAssetsCatalog();
        if (!silent)
            AppendLog($"sample added: {item.Name} ({item.Signature[..Math.Min(12, item.Signature.Length)]})");
        return item;
    }

    private void TouchSelectedSampleAssetsAsUsed()
    {
        // Persist "last used" only for the currently assigned logical roles.
        var now = DateTime.UtcNow;
        var changed = false;
        foreach (var id in new[] { _normalSampleAssetId, _eroSampleAssetId }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = _sampleAssets.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted);
            if (item == null)
                continue;
            item.LastUsedAtUtc = now;
            item.UpdatedAtUtc = now;
            changed = true;
        }
        if (changed)
            SaveSampleAssetsCatalog();
    }
}

