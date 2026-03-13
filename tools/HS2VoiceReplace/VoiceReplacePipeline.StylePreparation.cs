using System.Globalization;
using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Style preparation normalizes user-provided reference audio into the exact wav inputs

    // expected by Seed-VC, including manual range extraction and auto-selection flows.

private static async Task<StyleWavPair> PrepareStyleWavsAsync(
        PipelineOptions o,
        string pyExe,
        string selectScript,
        string workRoot,
        IReadOnlyDictionary<string, string> pyEnv,
        string styleRoot,
        Action<string> log,
        CancellationToken ct)
    {
        var styleNormalWav = Path.Combine(styleRoot, "style_normal.wav");
        var styleEroWav = Path.Combine(styleRoot, "style_ero.wav");

        var hasManualNormal = o.StyleNormalSegment != null;
        var hasManualEro = o.StyleEroSegment != null;
        var sameSample = string.Equals(o.StyleNormalSample, o.StyleEroSample, StringComparison.OrdinalIgnoreCase);

        if (hasManualNormal || hasManualEro)
        {
            var ffmpegExe = ResolveFfmpegExePath(o);
            if (hasManualNormal)
            {
                var seg = o.StyleNormalSegment!;
                log($"  manual normal: {seg.ToShortString()} src={seg.SourceFile}");
                await ExtractStyleSegmentAsync(ffmpegExe, seg.SourceFile, seg.StartSec, seg.DurationSec, styleNormalWav, workRoot, log, ct);
            }
            else
            {
                await ProcessUtil.RunAsync(pyExe,
                    $"\"{selectScript}\" --input \"{o.StyleNormalSample}\" --output \"{styleNormalWav}\" --report \"{Path.Combine(styleRoot, "style_normal.csv")}\" --target-sec 18 --min-sec 8 --speech-clarity-bias 1.2 --top-k 1",
                    workRoot, log, ct, pyEnv);
            }

            if (hasManualEro)
            {
                var seg = o.StyleEroSegment!;
                log($"  manual ero: {seg.ToShortString()} src={seg.SourceFile}");
                await ExtractStyleSegmentAsync(ffmpegExe, seg.SourceFile, seg.StartSec, seg.DurationSec, styleEroWav, workRoot, log, ct);
            }
            else if (sameSample && hasManualNormal)
            {
                File.Copy(styleNormalWav, styleEroWav, overwrite: true);
                log(L("log.reuseNormalForEro"));
            }
            else
            {
                await ProcessUtil.RunAsync(pyExe,
                    $"\"{selectScript}\" --input \"{o.StyleEroSample}\" --output \"{styleEroWav}\" --report \"{Path.Combine(styleRoot, "style_ero.csv")}\" --target-sec 18 --min-sec 8 --speech-clarity-bias 0.0 --top-k 1",
                    workRoot, log, ct, pyEnv);
            }
        }
        else if (sameSample)
        {
            var pairReport = Path.Combine(styleRoot, "style_pair.csv");
            var pairCandidates = Path.Combine(styleRoot, "candidates");
            await ProcessUtil.RunAsync(pyExe,
                $"\"{selectScript}\" --input \"{o.StyleNormalSample}\" --normal-output \"{styleNormalWav}\" --ero-output \"{styleEroWav}\" --report \"{pairReport}\" --target-sec 10 --min-sec 5 --speech-clarity-bias 0.5 --top-k 24 --pair-max-overlap-ratio 0.25 --erotic-order auto --export-candidates-dir \"{pairCandidates}\"",
                workRoot, log, ct, pyEnv);
        }
        else
        {
            await ProcessUtil.RunAsync(pyExe,
                $"\"{selectScript}\" --input \"{o.StyleNormalSample}\" --output \"{styleNormalWav}\" --report \"{Path.Combine(styleRoot, "style_normal.csv")}\" --target-sec 18 --min-sec 8 --speech-clarity-bias 1.2 --top-k 1",
                workRoot, log, ct, pyEnv);

            await ProcessUtil.RunAsync(pyExe,
                $"\"{selectScript}\" --input \"{o.StyleEroSample}\" --output \"{styleEroWav}\" --report \"{Path.Combine(styleRoot, "style_ero.csv")}\" --target-sec 18 --min-sec 8 --speech-clarity-bias 0.0 --top-k 1",
                workRoot, log, ct, pyEnv);
        }

        if (!File.Exists(styleNormalWav) || !File.Exists(styleEroWav))
            throw new InvalidOperationException(L("error.styleWavBuildFailed"));
        return new StyleWavPair(styleNormalWav, styleEroWav);
    }

    private static string ResolveFfmpegExePath(PipelineOptions o)
    {
        var bin = FindFfmpegBinDir(o);
        if (!string.IsNullOrWhiteSpace(bin))
        {
            var exe = Path.Combine(bin, "ffmpeg.exe");
            if (File.Exists(exe))
                return exe;
        }
        throw new InvalidOperationException(L("error.ffmpegMissing"));
    }

    private static async Task ExtractStyleSegmentAsync(
        string ffmpegExe,
        string sourceFile,
        double startSec,
        double durationSec,
        string outWav,
        string workRoot,
        Action<string> log,
        CancellationToken ct)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException(L("error.styleSourceMissing"), sourceFile);
        if (durationSec <= 0.2)
            throw new InvalidOperationException(L("error.styleRangeTooShort"));

        var st = Math.Max(0, startSec).ToString("0.###", CultureInfo.InvariantCulture);
        var du = Math.Max(0.2, durationSec).ToString("0.###", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(Path.GetDirectoryName(outWav)!);

        var args = $"-y -hide_banner -loglevel error -ss {st} -t {du} -i \"{sourceFile}\" -vn -sn -dn -ac 1 -ar 32000 -f wav \"{outWav}\"";
        await ProcessUtil.RunAsync(ffmpegExe, args, workRoot, log, ct);
    }

    private static bool StyleReportInputMatches(string reportCsv, string expectedSample)
    {
        if (!File.Exists(reportCsv))
            return true;
        if (string.IsNullOrWhiteSpace(expectedSample))
            return false;
        try
        {
            var lines = File.ReadAllLines(reportCsv, Encoding.UTF8);
            if (lines.Length < 2)
                return false;
            var header = ParseCsvLine(lines[0]);
            var idxInput = header.FindIndex(h => string.Equals(h, "input", StringComparison.OrdinalIgnoreCase));
            if (idxInput < 0)
                return false;
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var cols = ParseCsvLine(line);
                if (idxInput >= cols.Count)
                    continue;
                var input = cols[idxInput];
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                var inputFull = Path.GetFullPath(input);
                var expectedFull = Path.GetFullPath(expectedSample);
                return string.Equals(inputFull, expectedFull, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}











