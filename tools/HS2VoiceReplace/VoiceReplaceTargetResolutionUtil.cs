using System.Globalization;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

// Provides pure helpers for resolving personality ids and bundle filename preferences.
internal static class VoiceReplaceTargetResolutionUtil
{
    private static readonly Regex RunRootPersonalityRegex = new(
        @"(?:^|[_\-])c(?<id>\d{1,2})(?:$|[_\-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int ResolvePersonalityIdFromRunRoot(string runRoot, int fallback)
    {
        try
        {
            var name = new DirectoryInfo(Path.GetFullPath(runRoot)).Name;
            var m = RunRootPersonalityRegex.Match(name);
            if (m.Success && int.TryParse(m.Groups["id"].Value, out var id) && id >= 0 && id <= 99)
                return id;
        }
        catch
        {
        }

        return fallback;
    }

    public static bool TryParsePersonalityId(string pid, out int value)
    {
        value = -1;
        if (string.IsNullOrWhiteSpace(pid))
            return false;
        var s = pid.Trim();
        if (s.StartsWith("c", StringComparison.OrdinalIgnoreCase))
            s = s[1..];
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            value = -1;
            return false;
        }
        return true;
    }

    public static string ChooseBundleFileName(IEnumerable<string> candidatePaths, string preferredRelativeName, string pid)
    {
        var cands = candidatePaths
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (cands.Length == 0)
            throw new FileNotFoundException("No bundle candidates were provided.");

        static int ParseStemNum(string path)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            return int.TryParse(stem, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
        }

        var preferredName = Path.GetFileName(preferredRelativeName);
        if (TryParsePersonalityId(pid, out var pidNum))
            preferredName = pidNum <= 9 ? "30.unity3d" : "50.unity3d";

        var chosen = cands.FirstOrDefault(x => string.Equals(Path.GetFileName(x), preferredName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(chosen))
        {
            chosen = cands
                .OrderByDescending(ParseStemNum)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        return Path.GetFileName(chosen);
    }
}

