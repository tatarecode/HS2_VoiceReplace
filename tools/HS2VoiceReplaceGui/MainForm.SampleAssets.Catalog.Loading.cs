using System.Text;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Load the persisted sample asset catalog and repair incomplete metadata in place.
    private void LoadSampleAssets()
    {
        try
        {
            Directory.CreateDirectory(SampleAssetsRoot);
            Directory.CreateDirectory(SampleAssetsActiveRoot);
            Directory.CreateDirectory(SampleAssetsTrashRoot);

            _sampleAssets.Clear();
            var catalogTouched = false;
            string? catalogNormalId = null;
            string? catalogEroId = null;
            if (File.Exists(SampleAssetsCatalogPath))
            {
                var json = File.ReadAllText(SampleAssetsCatalogPath, Encoding.UTF8);
                var cat = System.Text.Json.JsonSerializer.Deserialize<SampleAssetsCatalog>(json);
                catalogNormalId = cat?.NormalSampleAssetId;
                catalogEroId = cat?.EroSampleAssetId;
                if (cat?.Items != null)
                {
                    foreach (var item in cat.Items)
                    {
                        if (string.IsNullOrWhiteSpace(item.Id))
                        {
                            item.Id = Guid.NewGuid().ToString("N");
                            catalogTouched = true;
                        }
                        if (string.IsNullOrWhiteSpace(item.HashAlgorithmVersion))
                        {
                            item.HashAlgorithmVersion = SampleHashAlgorithmVersion;
                            catalogTouched = true;
                        }
                        try
                        {
                            var p = GetAssetAbsolutePath(item);
                            if (File.Exists(p) &&
                                (string.IsNullOrWhiteSpace(item.Signature) ||
                                 !string.Equals(item.HashAlgorithmVersion, SampleHashAlgorithmVersion, StringComparison.OrdinalIgnoreCase) ||
                                 item.DurationSec <= 0 || item.SampleRateHz <= 0 || item.Channels <= 0))
                            {
                                item.Signature = ComputeFileSha256Hex(p);
                                item.HashAlgorithmVersion = SampleHashAlgorithmVersion;
                                var info = ProbeWavInfo(p);
                                item.DurationSec = info.DurationSec;
                                item.SampleRateHz = info.SampleRateHz;
                                item.Channels = info.Channels;
                                item.UpdatedAtUtc = DateTime.UtcNow;
                                catalogTouched = true;
                            }
                            var hasDefaultFullRange =
                                item.SourceStartSec.HasValue &&
                                item.SourceDurationSec.HasValue &&
                                Math.Abs(item.SourceStartSec.Value) < 0.0001 &&
                                item.DurationSec > 0 &&
                                Math.Abs(item.SourceDurationSec.Value - item.DurationSec) < 0.05;
                            // Backfill the original cut range from prior run metadata when the catalog only
                            // knows the exported WAV. This keeps the UI self-contained without importing
                            // obsolete per-user settings.
                            if ((!item.SourceStartSec.HasValue || !item.SourceDurationSec.HasValue || hasDefaultFullRange) && item.DurationSec > 0)
                            {
                                var preferEro = string.Equals(item.Name, "ero", StringComparison.OrdinalIgnoreCase);
                                var seg = ResolveSegmentFromRunHistory(item.SourceFilePath, preferEro: preferEro);
                                if (seg != null)
                                {
                                    item.SourceStartSec = seg.StartSec;
                                    item.SourceDurationSec = seg.DurationSec;
                                }
                                else
                                {
                                    item.SourceStartSec = 0;
                                    item.SourceDurationSec = item.DurationSec;
                                }
                                item.UpdatedAtUtc = DateTime.UtcNow;
                                catalogTouched = true;
                            }
                        }
                        catch
                        {
                        }
                        _sampleAssets.Add(item);
                    }
                }
            }

            bool IsActiveId(string id)
                => !string.IsNullOrWhiteSpace(id) &&
                   _sampleAssets.Any(x => !x.IsDeleted && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

            if (!IsActiveId(_normalSampleAssetId) && IsActiveId(catalogNormalId ?? ""))
                _normalSampleAssetId = catalogNormalId!;
            if (!IsActiveId(_eroSampleAssetId) && IsActiveId(catalogEroId ?? ""))
                _eroSampleAssetId = catalogEroId!;

            var beforeNormal = _normalSampleAssetId;
            var beforeEro = _eroSampleAssetId;
            EnsureSelectedSampleAssets();
            var selectionTouched =
                !string.Equals(beforeNormal, _normalSampleAssetId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(beforeEro, _eroSampleAssetId, StringComparison.OrdinalIgnoreCase);
            if (catalogTouched || selectionTouched)
            {
                SaveSampleAssetsCatalog();
                SaveUiSettings();
            }
            RefreshSampleSignatureDisplay();
        }
        catch (Exception ex)
        {
            AppendLog(T("log.sampleCatalogLoadFailed", ex.Message));
        }
    }

    private void EnsureSelectedSampleAssets()
    {
        var active = _sampleAssets.Where(x => !x.IsDeleted).ToList();
        bool IsValid(string id) => active.Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

        if (!IsValid(_normalSampleAssetId))
        {
            _normalSampleAssetId = active.FirstOrDefault(x => string.Equals(x.Name, "normal", StringComparison.OrdinalIgnoreCase))?.Id
                ?? active.FirstOrDefault()?.Id
                ?? string.Empty;
        }

        if (!IsValid(_eroSampleAssetId))
        {
            _eroSampleAssetId = active.FirstOrDefault(x => string.Equals(x.Name, "ero", StringComparison.OrdinalIgnoreCase))?.Id
                ?? _normalSampleAssetId;
        }
    }
}

