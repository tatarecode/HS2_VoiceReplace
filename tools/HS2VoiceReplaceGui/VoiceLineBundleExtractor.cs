using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Text;

namespace HS2VoiceReplace;

// Exports voice-line TextAssets into run_root/list_h_sound_voice_* so the existing
// voice-line cache builder can treat them as the canonical primary source.
internal static class VoiceLineBundleExtractor
{
    public static int ExportVoiceLineTextAssets(string sourceHs2Root, string classDataPath, string runRoot)
    {
        var voiceBundles = EnumerateVoiceLineBundleFiles(sourceHs2Root);
        if (voiceBundles.Count == 0 || !File.Exists(classDataPath))
            return 0;

        var manager = new AssetsManager();
        manager.LoadClassPackage(classDataPath);

        var exported = 0;
        foreach (var bundlePath in voiceBundles)
        {
            try
            {
                exported += ExportBundleTextAssets(manager, bundlePath, runRoot);
            }
            catch
            {
            }
        }

        return exported;
    }

    internal static List<string> EnumerateVoiceLineBundleFiles(string sourceHs2Root)
    {
        var files = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceHs2Root))
            return files;

        var voiceRoot = Path.Combine(sourceHs2Root, "abdata", "list", "h", "sound", "voice");
        if (!Directory.Exists(voiceRoot))
            return files;
        foreach (var phase in new[] { "30", "50" })
        {
            var bundlePath = Path.Combine(voiceRoot, $"{phase}.unity3d");
            if (File.Exists(bundlePath))
                files.Add(bundlePath);
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ExportBundleTextAssets(AssetsManager manager, string bundlePath, string runRoot)
    {
        var bundle = manager.LoadBundleFile(bundlePath, true);
        if (bundle?.file == null)
            return 0;

        var phaseDir = GetVoiceLinePhaseName(bundlePath);
        var outDir = Path.Combine(runRoot, $"list_h_sound_voice_{phaseDir}");
        Directory.CreateDirectory(outDir);

        var exported = 0;
        foreach (var assetsFileName in bundle.file.GetAllFileNames())
        {
            var assetsFile = manager.LoadAssetsFileFromBundle(bundle, assetsFileName, true);
            if (assetsFile?.file == null)
                continue;

            var textAssets = assetsFile.file.GetAssetsOfType(49);
            if (textAssets == null || textAssets.Count == 0)
                continue;

            var unnamedIndex = 0;
            foreach (var info in textAssets)
            {
                var baseField = manager.GetBaseField(assetsFile, info);
                if (baseField == null)
                    continue;

                var text = ReadTextAssetText(baseField["m_Script"]);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var assetName = ReadTextAssetName(baseField);
                var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(assetName) ? $"textasset_{unnamedIndex++:0000}" : assetName);
                var outPath = Path.Combine(outDir, safeName + ".TextAsset");
                File.WriteAllText(outPath, text, new UTF8Encoding(false));
                exported++;
            }
        }

        return exported;
    }

    internal static string GetVoiceLinePhaseName(string bundlePath)
    {
        var phase = Path.GetFileNameWithoutExtension(bundlePath);
        if (string.Equals(phase, "50", StringComparison.OrdinalIgnoreCase))
            return "50";
        return "30";
    }

    private static string? ReadTextAssetName(AssetTypeValueField baseField)
    {
        try
        {
            var name = baseField["m_Name"]?.AsString;
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch
        {
        }
        return null;
    }

    private static string? ReadTextAssetText(AssetTypeValueField? scriptField)
    {
        if (scriptField == null || scriptField.IsDummy)
            return null;

        try
        {
            var text = scriptField.AsString;
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        catch
        {
        }

        try
        {
            var bytes = scriptField.AsByteArray;
            if (bytes is { Length: > 0 })
                return Encoding.UTF8.GetString(bytes).Trim('\uFEFF', '\0');
        }
        catch
        {
        }

        return null;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString();
    }
}
