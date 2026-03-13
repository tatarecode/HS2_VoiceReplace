using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // The sample-signature map persists which normal/ero signatures produced each output wav.

    // This lets the pipeline resume only the rows whose sample inputs truly changed.

private static void WriteSampleSignatureFile(string runRoot, PipelineOptions o)
    {
        var normalRaw = BuildSampleInputSignature(o, isEro: false);
        var eroRaw = BuildSampleInputSignature(o, isEro: true);
        var normal = ComputeTextSha256(normalRaw);
        var ero = ComputeTextSha256(eroRaw);
        var combined = ComputeTextSha256("normal=" + normal + "|ero=" + ero);
        var outFile = Path.Combine(runRoot, "sample_signature.csv");
        var lines = new List<string>
        {
            "kind,sha256",
            $"\"normal\",\"{normal}\"",
            $"\"ero\",\"{ero}\"",
            $"\"combined\",\"{combined}\"",
        };
        File.WriteAllLines(outFile, lines, new UTF8Encoding(false));
    }

    private static void EnsureSampleSignatureMap(string runRoot, string manifestCsv, string outWavRoot)
    {
        if (!File.Exists(manifestCsv))
            return;

        var outFile = Path.Combine(runRoot, "sample_signature_map.csv");
        if (File.Exists(outFile))
            return;

        var lines = new List<string> { "relative_path,bucket,output_file,sig_normal,sig_ero,sig_used" };
        foreach (var row in LoadManifestRows(manifestCsv).OrderBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(row.RelativePath))
                continue;
            var bucket = string.Equals(row.Bucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
            var outPath = Path.Combine(outWavRoot, row.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            lines.Add($"\"{row.RelativePath}\",\"{bucket}\",\"{outPath.Replace("\"", "\"\"")}\",\"\",\"\",\"\"");
        }
        File.WriteAllLines(outFile, lines, new UTF8Encoding(false));
    }

    private static Dictionary<string, VoiceReplaceFreshnessUtil.SignatureMapRow> LoadSignatureMapRows(string runRoot, string manifestCsv, string outWavRoot)
    {
        var map = new Dictionary<string, VoiceReplaceFreshnessUtil.SignatureMapRow>(StringComparer.OrdinalIgnoreCase);
        var mapFile = Path.Combine(runRoot, "sample_signature_map.csv");
        if (File.Exists(mapFile))
        {
            foreach (var line in File.ReadLines(mapFile).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var cols = ParseCsvLine(line);
                if (cols.Count < 6)
                    continue;
                var rel = cols[0].Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(rel))
                    continue;
                var bucket = string.Equals(cols[1], "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
                var outPath = cols[2];
                map[rel] = new VoiceReplaceFreshnessUtil.SignatureMapRow(rel, bucket, outPath, cols[3], cols[4], cols[5]);
            }
        }

        foreach (var row in LoadManifestRows(manifestCsv))
        {
            if (string.IsNullOrWhiteSpace(row.RelativePath))
                continue;
            var rel = row.RelativePath.Replace('\\', '/');
            if (map.ContainsKey(rel))
                continue;
            var bucket = string.Equals(row.Bucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
            var outPath = Path.Combine(outWavRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            map[rel] = new VoiceReplaceFreshnessUtil.SignatureMapRow(rel, bucket, outPath, "", "", "");
        }
        return map;
    }

    private static void SaveSignatureMapRows(string runRoot, IReadOnlyDictionary<string, VoiceReplaceFreshnessUtil.SignatureMapRow> rows)
    {
        var mapFile = Path.Combine(runRoot, "sample_signature_map.csv");
        var lines = new List<string> { "relative_path,bucket,output_file,sig_normal,sig_ero,sig_used" };
        foreach (var row in rows.Values.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(
                $"\"{row.RelativePath}\",\"{row.Bucket}\",\"{row.OutputFile.Replace("\"", "\"\"")}\",\"{row.SigNormal}\",\"{row.SigEro}\",\"{row.SigUsed}\"");
        }
        File.WriteAllLines(mapFile, lines, new UTF8Encoding(false));
    }

    private static void UpdateSampleSignatureMapFromProcessedManifest(
        string runRoot,
        string fullManifestCsv,
        string processedManifestCsv,
        string outWavRoot,
        string currentNormalSig,
        string currentEroSig)
    {
        var map = LoadSignatureMapRows(runRoot, fullManifestCsv, outWavRoot);
        foreach (var row in LoadManifestRows(processedManifestCsv))
        {
            if (string.IsNullOrWhiteSpace(row.RelativePath))
                continue;
            var rel = row.RelativePath.Replace('\\', '/');
            var bucket = string.Equals(row.Bucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
            var outPath = Path.Combine(outWavRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(outPath))
                continue;
            var used = bucket == "ero" ? currentEroSig : currentNormalSig;
            map[rel] = new VoiceReplaceFreshnessUtil.SignatureMapRow(rel, bucket, outPath, currentNormalSig, currentEroSig, used);
        }
        SaveSignatureMapRows(runRoot, map);
    }
}











