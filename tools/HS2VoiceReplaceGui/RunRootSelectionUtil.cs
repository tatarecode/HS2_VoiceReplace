using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

// Centralizes how the UI keeps or replaces the current run root when the target personality changes.
internal static class RunRootSelectionUtil
{
    private static readonly Regex AutoResumeRunRootRegex = new(
        @"[\\/]+gui_runs[\\/]+resume_c\d{2}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string ResolveSuggestedRunRoot(string? currentRunRoot, string activeOutputRoot, int targetPersonalityId)
    {
        var suggested = Path.Combine(activeOutputRoot, "gui_runs", $"resume_c{targetPersonalityId:00}");
        if (string.IsNullOrWhiteSpace(currentRunRoot))
            return suggested;

        var normalizedCurrent = Path.GetFullPath(currentRunRoot.Trim());
        if (AutoResumeRunRootRegex.IsMatch(normalizedCurrent))
            return suggested;

        return normalizedCurrent;
    }
}
