namespace HS2VoiceReplace;

// Centralizes how source builds locate the separately-supplied AssetsTools.NET.dll.
internal static class AssetsToolsReferenceUtil
{
    public const string EnvVarName = "HS2VR_ASSETSTOOLS_NET_PATH";
    public const string DefaultRelativePath = @"..\..\_tools\uabea\v8\AssetsTools.NET.dll";

    public static string GetDefaultPath(string projectDir)
        => Path.GetFullPath(Path.Combine(projectDir, DefaultRelativePath));

    public static string? ResolveBuildReferencePath(string projectDir, out bool fromEnvironment)
    {
        var envValue = Environment.GetEnvironmentVariable(EnvVarName)?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(envValue) && File.Exists(envValue))
        {
            fromEnvironment = true;
            return Path.GetFullPath(envValue);
        }

        var defaultPath = GetDefaultPath(projectDir);
        if (File.Exists(defaultPath))
        {
            fromEnvironment = false;
            return defaultPath;
        }

        fromEnvironment = false;
        return null;
    }
}
