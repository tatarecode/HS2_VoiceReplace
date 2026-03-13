using System.Security.Cryptography;
using System.Text;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    private void EditSeedVcSettings()
    {
        using var dlg = new SeedVcSettingsDialog(_seedVc.Clone());
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        _seedVc = dlg.Settings.Clone();
        _lblSeedVcSummary.Text = _seedVc.ToSummaryString();
        SaveUiSettings();
        AppendLog(T("log.seedVcUpdated"));
    }

    private static void ApplyV1NaturalPreset(SeedVcUiSettings seed)
    {
        if (seed.Engine != SeedVcEngine.V1)
            return;

        // Fixed "natural" preset for v1 operation.
        seed.DiffusionSteps = 25;
        seed.LengthAdjust = 1.0;
        seed.IntelligibilityCfgRate = 0.7;
        seed.SimilarityCfgRate = 0.7;
        seed.TopP = 0.9;
        seed.Temperature = 0.95;
        seed.RepetitionPenalty = 1.0;

        seed.NrStylePre = false;
        seed.NrOutPost = false;
        seed.NrStylePropDecrease = 0.6;
        seed.NrOutPropDecrease = 0.5;
        seed.NrTimeMaskSmoothMs = 40.0;
        seed.NrFreqMaskSmoothHz = 200.0;

        seed.HarshFix = false;
        seed.HarshHfCutoff = 4000.0;
        seed.HarshSrcHfMix = 0.65;
        seed.HarshOverFactor = 1.55;
        seed.HarshFlatnessTh = 0.34;
        seed.HarshMinSegmentMs = 18.0;

        seed.BreathPassThrough = false;
        seed.BreathFlatnessTh = 0.42;
        seed.BreathRmsMax = 0.22;
        seed.BreathMix = 1.0;

        seed.GlobalHfBlend = false;
        seed.GlobalHfCutoff = 3500.0;
        seed.GlobalHfSrcMix = 0.30;

        seed.GlobalDeEsser = false;
        seed.DeEsserLowHz = 6000.0;
        seed.DeEsserHighHz = 10000.0;
        seed.DeEsserStrength = 0.28;

        seed.AudioSrPost = false;
    }

    private void RefreshSampleSignatureDisplay()
    {
        try
        {
            var sig = ComputeCurrentSampleSignatures();
            _lblSampleSignature.Text = $"N:{sig.Normal} E:{sig.Ero}";
            if (_lblSampleSignatureInDialog != null && !_lblSampleSignatureInDialog.IsDisposed)
                _lblSampleSignatureInDialog.Text = _lblSampleSignature.Text;
            RefreshActionAvailability();
        }
        catch (Exception ex)
        {
            _lblSampleSignature.Text = T("core.sampleSignatureUnavailable");
            if (_lblSampleSignatureInDialog != null && !_lblSampleSignatureInDialog.IsDisposed)
                _lblSampleSignatureInDialog.Text = _lblSampleSignature.Text;
            AppendLog(T("log.sampleSignatureRefreshFailed", ex.Message));
            RefreshActionAvailability();
        }
    }

    private string BuildSampleSignatureRaw(string samplePath, StyleSegmentSelection? segment, string segKey)
    {
        static string FileSig(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "empty";
            try
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
                if (!File.Exists(full))
                    return $"missing:{full}";
                using var fs = File.OpenRead(full);
                var sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
                return $"sha256={sha}";
            }
            catch
            {
                return $"invalid:{path}";
            }
        }

        static string SegSig(string key, StyleSegmentSelection? seg)
        {
            if (seg == null) return $"{key}=none";
            return $"{key}={FileSig(seg.SourceFile)}|{seg.StartSec:0.######}|{seg.DurationSec:0.######}";
        }

        return $"{FileSig(samplePath)}|{SegSig(segKey, segment)}";
    }

    private (string Normal, string Ero, string Combined) ComputeCurrentSampleSignatures()
    {
        EnsureSelectedSampleAssets();
        var normalItem = GetSampleAssetById(_normalSampleAssetId);
        var eroItem = GetSampleAssetById(_eroSampleAssetId) ?? normalItem;
        var normalRaw = normalItem != null
            ? $"id={normalItem.Id}|alg={normalItem.HashAlgorithmVersion}|sig={normalItem.Signature}"
            : BuildSampleSignatureRaw(_txtNormalSample.Text, _manualNormalSegment, "normalSeg");
        var eroRaw = eroItem != null
            ? $"id={eroItem.Id}|alg={eroItem.HashAlgorithmVersion}|sig={eroItem.Signature}"
            : BuildSampleSignatureRaw(string.IsNullOrWhiteSpace(_txtEroSample.Text) ? _txtNormalSample.Text : _txtEroSample.Text, _manualEroSegment, "eroSeg");
        var normal = ComputeSha256Hex(normalRaw);
        var ero = ComputeSha256Hex(eroRaw);
        var combined = ComputeSha256Hex("normal=" + normal + "|ero=" + ero);
        return (normal, ero, combined);
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

