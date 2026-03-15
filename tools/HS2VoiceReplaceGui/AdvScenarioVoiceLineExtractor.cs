using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace HS2VoiceReplace;

// Supplements voice-line text by pairing ADV voice-play commands with nearby dialogue commands.
internal static class AdvScenarioVoiceLineExtractor
{
    internal readonly record struct ScenarioCommand(int Command, IReadOnlyList<string> Args);

    public static Dictionary<string, string> ExtractVoiceLineMap(string sourceHs2Root, string classDataPath, string personalityId)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bundleFiles = EnumerateScenarioBundleFiles(sourceHs2Root, personalityId);
        if (bundleFiles.Count == 0 || !File.Exists(classDataPath))
            return map;

        var manager = new AssetsManager();
        manager.LoadClassPackage(classDataPath);

        foreach (var bundlePath in bundleFiles)
        {
            try
            {
                foreach (var kv in ExtractVoiceLineMapFromBundle(manager, bundlePath, personalityId))
                {
                    if (!map.ContainsKey(kv.Key))
                        map[kv.Key] = kv.Value;
                }
            }
            catch
            {
            }
        }

        return map;
    }

    internal static List<string> EnumerateScenarioBundleFiles(string sourceHs2Root, string personalityId)
    {
        var files = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceHs2Root) || string.IsNullOrWhiteSpace(personalityId))
            return files;

        var scenarioRoot = Path.Combine(sourceHs2Root, "abdata", "adv", "scenario", personalityId);
        if (!Directory.Exists(scenarioRoot))
            return files;

        foreach (var phase in new[] { "30", "50" })
        {
            var phaseRoot = Path.Combine(scenarioRoot, phase);
            if (!Directory.Exists(phaseRoot))
                continue;

            try
            {
                files.AddRange(Directory.GetFiles(phaseRoot, "*.unity3d", SearchOption.TopDirectoryOnly));
            }
            catch
            {
            }
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static Dictionary<string, string> BuildVoiceLineMapFromScenarioCommands(
        IReadOnlyList<ScenarioCommand> commands,
        string personalityId,
        int maxLookahead = 8)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clipPrefix = BuildClipPrefix(personalityId);
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (command.Command != 17)
                continue;

            if (!TryExtractVoiceCommand(command.Args, clipPrefix, out var bundlePath, out var clipName, out var relativePath))
                continue;

            var voiceLine = FindNearbyDialogue(commands, i + 1, maxLookahead);
            if (string.IsNullOrWhiteSpace(voiceLine))
                continue;

            if (!map.ContainsKey(relativePath))
                map[relativePath] = voiceLine;
        }

        return map;
    }

    private static Dictionary<string, string> ExtractVoiceLineMapFromBundle(AssetsManager manager, string bundlePath, string personalityId)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bundle = manager.LoadBundleFile(bundlePath, true);
        if (bundle?.file == null)
            return map;

        foreach (var assetsFileName in bundle.file.GetAllFileNames())
        {
            var assetsFile = manager.LoadAssetsFileFromBundle(bundle, assetsFileName, true);
            if (assetsFile?.file == null)
                continue;

            var monos = assetsFile.file.GetAssetsOfType(114);
            if (monos == null || monos.Count == 0)
                continue;

            foreach (var mono in monos)
            {
                var baseField = manager.GetBaseField(assetsFile, mono);
                if (baseField == null)
                    continue;

                var commands = ReadScenarioCommands(baseField);
                if (commands.Count == 0)
                    continue;

                foreach (var kv in BuildVoiceLineMapFromScenarioCommands(commands, personalityId))
                {
                    if (!map.ContainsKey(kv.Key))
                        map[kv.Key] = kv.Value;
                }
            }
        }

        return map;
    }

    internal static List<ScenarioCommand> ReadScenarioCommands(AssetTypeValueField baseField)
    {
        var commands = new List<ScenarioCommand>();
        try
        {
            var listArray = baseField["list"]?["Array"];
            if (listArray == null || listArray.IsDummy)
                return commands;

            foreach (var item in listArray.Children)
            {
                if (item == null || item.IsDummy)
                    continue;

                var commandField = item["_command"];
                var argsArray = item["_args"]?["Array"];
                if (commandField == null || commandField.IsDummy || argsArray == null || argsArray.IsDummy)
                    continue;

                var args = new List<string>(argsArray.Children.Count);
                foreach (var arg in argsArray.Children)
                {
                    try
                    {
                        args.Add(arg.AsString ?? "");
                    }
                    catch
                    {
                        args.Add("");
                    }
                }

                commands.Add(new ScenarioCommand(commandField.AsInt, args));
            }
        }
        catch
        {
        }

        return commands;
    }

    internal static string? FindNearbyDialogue(IReadOnlyList<ScenarioCommand> commands, int startIndex, int maxLookahead)
    {
        var endExclusive = Math.Min(commands.Count, startIndex + Math.Max(0, maxLookahead));
        for (var i = startIndex; i < endExclusive; i++)
        {
            var cmd = commands[i];
            if (cmd.Command == 16)
                return ExtractDialogueText(cmd.Args);

            if (cmd.Command is 17 or 12 or 24)
                return null;
        }

        return null;
    }

    internal static string? ExtractDialogueText(IReadOnlyList<string> args)
    {
        for (var i = args.Count - 1; i >= 0; i--)
        {
            var value = (args[i] ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }

    internal static bool TryExtractVoiceCommand(
        IReadOnlyList<string> args,
        string clipPrefix,
        out string bundlePath,
        out string clipName,
        out string relativePath)
    {
        bundlePath = "";
        clipName = "";
        relativePath = "";

        if (args.Count == 0)
            return false;

        clipName = (args[^1] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(clipName) ||
            !clipName.StartsWith(clipPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        for (var i = args.Count - 2; i >= 0; i--)
        {
            var candidate = (args[i] ?? "").Trim().Replace('\\', '/');
            if (candidate.IndexOf("sound/data/pcm/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bundlePath = candidate;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(bundlePath))
            return false;

        return PartialRebuildGridDataUtil.TryBuildRelativePathFromVoiceTextAsset(bundlePath, clipName, out relativePath);
    }

    internal static string BuildClipPrefix(string personalityId)
    {
        var normalized = (personalityId ?? "").Trim().ToLowerInvariant();
        if (normalized.StartsWith("c"))
            normalized = normalized[1..];
        return $"hsa_{normalized}_";
    }
}
