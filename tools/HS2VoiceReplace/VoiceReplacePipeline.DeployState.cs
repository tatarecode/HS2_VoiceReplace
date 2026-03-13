using System.Text;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Deploy-state helpers persist reversible deployment metadata so install/uninstall can operate

    // per-personality without assuming a clean mods/plugins directory.

private static void Deploy(PipelineOptions o, string runtimeDll, IReadOnlyList<string> zipmods, Action<string> log)
    {
        var modsDir = Path.Combine(o.DeployHs2Root, "mods");
        var pluginDir = Path.Combine(o.DeployHs2Root, "BepInEx", "plugins");
        var pid = $"c{o.TargetPersonalityId:00}";
        Directory.CreateDirectory(modsDir);
        Directory.CreateDirectory(pluginDir);

        UndeployCore(o.DeployHs2Root, o.TargetPersonalityId, log, keepManifestBackup: false);

        var disabledZipmods = new List<string>();
        foreach (var f in Directory.GetFiles(modsDir, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly))
        {
            var off = f + ".off";
            if (!File.Exists(off))
            {
                File.Move(f, off);
                disabledZipmods.Add(Path.GetFileName(off));
                log($"  disabled: {Path.GetFileName(f)} -> {Path.GetFileName(off)}");
            }
            else
            {
                File.Delete(f);
                log($"  removed duplicate active zipmod: {Path.GetFileName(f)}");
            }
        }

        if (File.Exists(runtimeDll))
        {
            var dstDll = Path.Combine(pluginDir, RuntimePluginFileName);
            File.Copy(runtimeDll, dstDll, true);
            log($"  deployed: {RuntimePluginFileName}");
        }

        var deployedZipmods = new List<string>();
        foreach (var z in zipmods)
        {
            var fileName = Path.GetFileName(z);
            var dst = Path.Combine(modsDir, fileName);
            File.Copy(z, dst, true);
            deployedZipmods.Add(fileName);
            log($"  deployed: {fileName}");
        }

        SaveDeployState(o.DeployHs2Root, o.TargetPersonalityId, new DeployStateManifest
        {
            RuntimeDllFileName = RuntimePluginFileName,
            DeployedZipmods = deployedZipmods,
            DisabledZipmods = disabledZipmods,
            PersonalityId = o.TargetPersonalityId,
            RunRoot = string.IsNullOrWhiteSpace(o.ResumeRunRoot) ? null : Path.GetFullPath(o.ResumeRunRoot),
            DeployedAtUtc = DateTime.UtcNow,
        });
    }

    private static void Undeploy(string deployRoot, int personalityId, Action<string> log)
        => UndeployCore(deployRoot, personalityId, log, keepManifestBackup: false);

    private static void UndeployCore(string deployRoot, int personalityId, Action<string> log, bool keepManifestBackup)
    {
        var modsDir = Path.Combine(deployRoot, "mods");
        var pluginDir = Path.Combine(deployRoot, "BepInEx", "plugins");
        var statePath = GetDeployStatePath(deployRoot, personalityId);
        var state = LoadDeployState(deployRoot, personalityId);
        var pid = $"c{personalityId:00}";

        if (state != null)
        {
            foreach (var name in state.DeployedZipmods.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = Path.Combine(modsDir, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    log($"  removed deployed zipmod: {name}");
                }
            }

            foreach (var offName in state.DisabledZipmods.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var offPath = Path.Combine(modsDir, offName);
                if (!File.Exists(offPath))
                    continue;

                var restoredPath = offPath.EndsWith(".off", StringComparison.OrdinalIgnoreCase)
                    ? offPath[..^4]
                    : offPath + ".restored";

                if (File.Exists(restoredPath))
                {
                    log($"  restore skipped (destination exists): {Path.GetFileName(restoredPath)}");
                    continue;
                }

                File.Move(offPath, restoredPath);
                log($"  restored: {Path.GetFileName(restoredPath)}");
            }
        }
        else if (Directory.Exists(modsDir))
        {
            foreach (var path in Directory.GetFiles(modsDir, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly))
            {
                File.Delete(path);
                log($"  removed deployed zipmod: {Path.GetFileName(path)}");
            }
        }

        if (Directory.Exists(pluginDir) && !HasAnyInstalledDeployArtifactsExceptMaybeDll(deployRoot, personalityId))
        {
            foreach (var dllName in EnumerateRuntimePluginFileNames())
            {
                var dllPath = Path.Combine(pluginDir, dllName);
                if (!File.Exists(dllPath))
                    continue;
                File.Delete(dllPath);
                log($"  removed plugin: {dllName}");
            }
        }

        if (File.Exists(statePath))
        {
            if (keepManifestBackup)
            {
                var backup = statePath + ".bak";
                File.Copy(statePath, backup, true);
            }
            File.Delete(statePath);
        }
    }

    internal static bool HasInstalledDeployArtifacts(string deployRoot, int personalityId)
    {
        if (string.IsNullOrWhiteSpace(deployRoot))
            return false;

        var modsDir = Path.Combine(deployRoot, "mods");
        var pluginDir = Path.Combine(deployRoot, "BepInEx", "plugins");
        var pid = $"c{personalityId:00}";
        if (File.Exists(GetDeployStatePath(deployRoot, personalityId)))
            return true;
        if (Directory.Exists(modsDir) &&
            Directory.GetFiles(modsDir, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly).Length > 0)
            return true;

        if (Directory.Exists(pluginDir) && !HasAnyInstalledDeployArtifactsExceptMaybeDll(deployRoot, personalityId))
        {
            foreach (var dllName in EnumerateRuntimePluginFileNames())
            {
                if (File.Exists(Path.Combine(pluginDir, dllName)))
                    return true;
            }
        }

        return false;
    }

    private static string GetDeployStatePath(string deployRoot, int personalityId)
        => Path.Combine(deployRoot, "mods", $"HS2VoiceReplace_deploy_state_c{personalityId:00}.json");

    private static void SaveDeployState(string deployRoot, int personalityId, DeployStateManifest state)
    {
        var path = GetDeployStatePath(deployRoot, personalityId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static DeployStateManifest? LoadDeployState(string deployRoot, int personalityId)
    {
        var path = GetDeployStatePath(deployRoot, personalityId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return System.Text.Json.JsonSerializer.Deserialize<DeployStateManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasAnyInstalledDeployArtifactsExceptMaybeDll(string deployRoot, int excludedPersonalityId)
    {
        var modsDir = Path.Combine(deployRoot, "mods");
        if (!Directory.Exists(modsDir))
            return false;

        foreach (var path in Directory.GetFiles(modsDir, "HS2VoiceReplace_c??_*.zipmod", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            var match = Regex.Match(fileName, @"^HS2VoiceReplace_c(?<id>\d{2})_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
                continue;
            if (!int.TryParse(match.Groups["id"].Value, out var id))
                continue;
            if (id != excludedPersonalityId)
                return true;
        }

        for (var i = 0; i <= 99; i++)
        {
            if (i == excludedPersonalityId)
                continue;
            if (File.Exists(GetDeployStatePath(deployRoot, i)))
                return true;
        }
        return false;
    }
}











