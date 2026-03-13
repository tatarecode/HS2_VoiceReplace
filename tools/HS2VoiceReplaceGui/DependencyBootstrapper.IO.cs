using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace HS2VoiceReplace;

// Contains low-level file-system helpers used by dependency setup to create, copy, and validate tool directories.

internal static partial class DependencyBootstrapper
{
    private static void PatchEmbeddedPythonPth(string pythonDir, Action<string> log)
    {
        var pth = Directory.GetFiles(pythonDir, "python*._pth", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (pth == null) return;

        var lines = File.ReadAllLines(pth).ToList();
        var hasImportSite = false;
        var hasSitePackages = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Equals("#import site", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = "import site";
                hasImportSite = true;
            }
            else if (trimmed.Equals("import site", StringComparison.OrdinalIgnoreCase))
            {
                hasImportSite = true;
            }

            if (trimmed.Equals(@"Lib\site-packages", StringComparison.OrdinalIgnoreCase))
                hasSitePackages = true;
        }

        if (!hasSitePackages) lines.Add(@"Lib\site-packages");
        if (!hasImportSite) lines.Add("import site");

        File.WriteAllLines(pth, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        log(L("log.adjustedPythonPth"));
    }

    private static async Task DownloadFileAsync(string url, string dst, Action<string> log, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        if (File.Exists(dst) && new FileInfo(dst).Length > 0)
        {
            log($"download skip: {Path.GetFileName(dst)}");
            return;
        }

        log($"download: {url}");
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None);

        var buf = new byte[1024 * 128];
        long done = 0;
        var sw = Stopwatch.StartNew();
        var nextLog = TimeSpan.FromSeconds(2);
        while (true)
        {
            var read = await src.ReadAsync(buf, ct);
            if (read <= 0) break;
            await outFs.WriteAsync(buf.AsMemory(0, read), ct);
            done += read;

            if (sw.Elapsed >= nextLog)
            {
                if (total.HasValue && total.Value > 0)
                {
                    var pct = (double)done * 100.0 / total.Value;
                    log($"  progress {pct:F1}% ({done / 1024 / 1024}MB/{total.Value / 1024 / 1024}MB)");
                }
                else
                {
                    log($"  progress {done / 1024 / 1024}MB");
                }
                nextLog += TimeSpan.FromSeconds(2);
            }
        }
    }

    private static void ExtractZip(string zipPath, string outDir, bool stripSingleRoot)
    {
        Directory.CreateDirectory(outDir);
        using var z = ZipFile.OpenRead(zipPath);
        string? rootPrefix = null;
        if (stripSingleRoot)
        {
            var top = z.Entries
                .Select(e => e.FullName.Replace('\\', '/'))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length > 0)
                .Select(parts => parts[0])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (top.Length == 1) rootPrefix = top[0] + "/";
        }

        var outDirFull = Path.GetFullPath(outDir) + Path.DirectorySeparatorChar;
        foreach (var entry in z.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (rootPrefix != null && name.StartsWith(rootPrefix, StringComparison.Ordinal))
                name = name[rootPrefix.Length..];
            if (string.IsNullOrWhiteSpace(name)) continue;

            var dst = Path.Combine(outDir, name.Replace('/', Path.DirectorySeparatorChar));
            var dstFull = Path.GetFullPath(dst);
            if (!dstFull.StartsWith(outDirFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L("error.invalidZipPath"));

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(dstFull);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dstFull)!);
            entry.ExtractToFile(dstFull, true);
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var d in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dst, Path.GetRelativePath(src, d)));

        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var outFile = Path.Combine(dst, Path.GetRelativePath(src, f));
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
            File.Copy(f, outFile, true);
        }
    }
}


