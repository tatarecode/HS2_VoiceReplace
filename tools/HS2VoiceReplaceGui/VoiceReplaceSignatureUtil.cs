using System.Security.Cryptography;
using System.Text;

namespace HS2VoiceReplace;

// Provides stable content-based signatures used by resume/skip decisions.
internal static class VoiceReplaceSignatureUtil
{
    public static string BuildStyleSignature(PipelineOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine("style_signature_v2");
        sb.AppendLine("normal:" + BuildSampleInputSignature(o, isEro: false));
        sb.AppendLine("ero:" + BuildSampleInputSignature(o, isEro: true));
        return sb.ToString();
    }

    public static string BuildSampleInputSignature(PipelineOptions o, bool isEro)
    {
        static string FileSig(string p)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (!File.Exists(full))
                    return $"missing:{full}";
                using var fs = File.OpenRead(full);
                var sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
                return $"sha256={sha}";
            }
            catch
            {
                return $"invalid:{p}";
            }
        }

        static string SegSig(string key, StyleSegmentSelection? seg)
        {
            if (seg == null) return $"{key}=none";
            return $"{key}={FileSig(seg.SourceFile)}|{seg.StartSec:0.######}|{seg.DurationSec:0.######}";
        }

        var sample = isEro ? o.StyleEroSample : o.StyleNormalSample;
        var seg = isEro ? o.StyleEroSegment : o.StyleNormalSegment;
        var segKey = isEro ? "eroSeg" : "normalSeg";
        return $"{FileSig(sample)}|{SegSig(segKey, seg)}";
    }

    public static string ComputeTextSha256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeSeedVcSignature(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("seedvc_signature_v", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add("seedvc_signature");
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length >= 4 &&
                (string.Equals(parts[1], "normal", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "ero", StringComparison.OrdinalIgnoreCase)))
            {
                var last = parts[^1];
                if (long.TryParse(last, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var length))
                {
                    normalized.Add($"{parts[0]}|{parts[1]}|size={length}");
                    continue;
                }

                if (parts.Length >= 3 && last.StartsWith("size=", StringComparison.OrdinalIgnoreCase))
                {
                    normalized.Add($"{parts[0]}|{parts[1]}|{last.ToLowerInvariant()}");
                    continue;
                }
            }

            normalized.Add(line);
        }

        return string.Join("\n", normalized);
    }

    public static string BuildSeedVcSignature(PipelineOptions o, IEnumerable<(string RelativePath, string Bucket, string SourceFile)> rows, string styleSigCurrent)
    {
        static string FileSig(string p)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (!File.Exists(full))
                    return "missing";
                var fi = new FileInfo(full);
                return $"size={fi.Length}";
            }
            catch
            {
                return "invalid";
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("seedvc_signature_v4");
        sb.AppendLine(styleSigCurrent);
        sb.AppendLine($"engine={o.SeedVc.Engine}");
        sb.AppendLine($"seed={o.SeedVc.DiffusionSteps}|{o.SeedVc.LengthAdjust:0.######}|{o.SeedVc.IntelligibilityCfgRate:0.######}|{o.SeedVc.SimilarityCfgRate:0.######}|{o.SeedVc.TopP:0.######}|{o.SeedVc.Temperature:0.######}|{o.SeedVc.RepetitionPenalty:0.######}");
        sb.AppendLine($"post={o.SeedVc.NrStylePre}|{o.SeedVc.NrOutPost}|{o.SeedVc.NrStylePropDecrease:0.######}|{o.SeedVc.NrOutPropDecrease:0.######}|{o.SeedVc.NrTimeMaskSmoothMs:0.######}|{o.SeedVc.NrFreqMaskSmoothHz:0.######}|{o.SeedVc.HarshFix}|{o.SeedVc.BreathPassThrough}|{o.SeedVc.GlobalHfBlend}|{o.SeedVc.GlobalDeEsser}|{o.SeedVc.AudioSrPost}");
        foreach (var row in rows.OrderBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"{row.RelativePath}|{row.Bucket}|{FileSig(row.SourceFile)}");
        return sb.ToString();
    }
}

