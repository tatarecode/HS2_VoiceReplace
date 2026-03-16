namespace HS2VoiceReplace;

// Keeps HS2 root migration rules in one place so UI settings can remain backward-compatible.
internal static class Hs2RootSettingsUtil
{
    public static string ResolvePersistedHs2Root(PersistedUiSettings? settings)
    {
        if (settings == null)
            return string.Empty;

        return FirstNonEmpty(
            settings.Hs2Root,
            settings.SourceHs2Root,
            settings.DeployHs2Root);
    }

    public static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
