using System.Text.Json;

namespace HS2VoiceReplace;

internal sealed class PythonRuntimeManifest
{
    public string EmbedVersion { get; init; } = "3.10.11";
    public string EmbedZipUrl { get; init; } = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip";
    public string GetPipUrl { get; init; } = "https://bootstrap.pypa.io/get-pip.py";
    public string RepoLocalPythonRelativePath { get; init; } = Path.Combine("_tools", "python310", "python.exe");

    public static PythonRuntimeManifest Load(IEnumerable<string> roots)
    {
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var candidate in EnumerateManifestCandidates(root))
            {
                try
                {
                    if (!File.Exists(candidate))
                        continue;

                    var json = File.ReadAllText(candidate);
                    var loaded = JsonSerializer.Deserialize<PythonRuntimeManifestFile>(json);
                    if (loaded == null)
                        continue;

                    return new PythonRuntimeManifest
                    {
                        EmbedVersion = string.IsNullOrWhiteSpace(loaded.EmbedVersion) ? "3.10.11" : loaded.EmbedVersion.Trim(),
                        EmbedZipUrl = string.IsNullOrWhiteSpace(loaded.EmbedZipUrl) ? "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip" : loaded.EmbedZipUrl.Trim(),
                        GetPipUrl = string.IsNullOrWhiteSpace(loaded.GetPipUrl) ? "https://bootstrap.pypa.io/get-pip.py" : loaded.GetPipUrl.Trim(),
                        RepoLocalPythonRelativePath = string.IsNullOrWhiteSpace(loaded.RepoLocalPythonRelativePath)
                            ? Path.Combine("_tools", "python310", "python.exe")
                            : NormalizeRelativePath(loaded.RepoLocalPythonRelativePath),
                    };
                }
                catch
                {
                }
            }
        }

        return new PythonRuntimeManifest();
    }

    public string GetEmbedZipFileName()
    {
        try
        {
            return Path.GetFileName(new Uri(EmbedZipUrl, UriKind.Absolute).LocalPath);
        }
        catch
        {
            return $"python-{EmbedVersion}-embed-amd64.zip";
        }
    }

    public string GetRepoLocalPythonFullPath(string root)
        => Path.GetFullPath(Path.Combine(root, NormalizeRelativePath(RepoLocalPythonRelativePath)));

    private static IEnumerable<string> EnumerateManifestCandidates(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        yield return Path.Combine(fullRoot, "tools", "python_runtime_manifest.json");
        yield return Path.Combine(fullRoot, "python_runtime_manifest.json");
    }

    private static string NormalizeRelativePath(string path)
    {
        var parts = path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        return parts.Length == 0 ? Path.Combine("_tools", "python310", "python.exe") : Path.Combine(parts);
    }
}

internal sealed class PythonRuntimeManifestFile
{
    public string? EmbedVersion { get; set; }
    public string? EmbedZipUrl { get; set; }
    public string? GetPipUrl { get; set; }
    public string? RepoLocalPythonRelativePath { get; set; }
}
