using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Text;

namespace HS2VoiceReplace;

// Extracts FMOD/AudioClip payloads from Unity bundles into raw audio files used by later pipeline stages.

internal static class AudioBundleExtractor
{
    public static int ExtractAudioPayloads(string bundlePath, string classDataPath, string outDir)
    {
        if (!File.Exists(bundlePath)) throw new FileNotFoundException(bundlePath);
        if (!File.Exists(classDataPath)) throw new FileNotFoundException(classDataPath);
        Directory.CreateDirectory(outDir);

        var manager = new AssetsManager();
        manager.LoadClassPackage(classDataPath);
        var bun = manager.LoadBundleFile(bundlePath, true) ?? throw new InvalidOperationException("Failed to load the bundle file.");

        int count = 0;
        foreach (var afName in bun.file.GetAllFileNames())
        {
            var af = manager.LoadAssetsFileFromBundle(bun, afName, true);
            if (af == null) continue;

            var clips = af.file.GetAssetsOfType(83);
            if (clips == null || clips.Count == 0) continue;

            foreach (var info in clips)
            {
                var baseField = manager.GetBaseField(af, info);
                if (baseField == null) continue;

                var nameField = baseField["m_Name"];
                if (!IsUsable(nameField)) continue;
                var clipName = nameField!.AsString;
                if (string.IsNullOrWhiteSpace(clipName)) continue;
                var safe = Sanitize(clipName);

                var resource = baseField["m_Resource"];
                if (IsUsable(resource))
                {
                    var source = resource!["m_Source"];
                    var offset = resource["m_Offset"];
                    var size = resource["m_Size"];
                    if (IsUsable(source) && IsUsable(offset) && IsUsable(size))
                    {
                        var entryName = ResourceEntryName(source!.AsString);
                        if (!string.IsNullOrWhiteSpace(entryName))
                        {
                            var bytes = ReadEntryBytes(bun.file, entryName!);
                            var off = checked((int)offset!.AsULong);
                            var len = checked((int)size!.AsULong);
                            if (off >= 0 && len > 0 && off + len <= bytes.Length)
                            {
                                var payload = new byte[len];
                                Buffer.BlockCopy(bytes, off, payload, 0, len);
                                File.WriteAllBytes(Path.Combine(outDir, safe + DetectExt(payload)), payload);
                                count++;
                                continue;
                            }
                        }
                    }
                }

                var audio = baseField["m_AudioData"];
                if (IsUsable(audio))
                {
                    try
                    {
                        var bytes = audio!.AsByteArray;
                        if (bytes is { Length: > 0 })
                        {
                            File.WriteAllBytes(Path.Combine(outDir, safe + DetectExt(bytes)), bytes);
                            count++;
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        return count;
    }

    private static bool IsUsable(AssetTypeValueField? f) => f != null && !f.IsDummy;
    private static string? ResourceEntryName(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var idx = source.LastIndexOf('/');
        return idx >= 0 && idx + 1 < source.Length ? source[(idx + 1)..] : source;
    }

    private static byte[] ReadEntryBytes(AssetBundleFile bundle, string entryName)
    {
        var idx = bundle.GetFileIndex(entryName);
        if (idx < 0) throw new InvalidOperationException($"Bundle entry was not found: {entryName}");
        bundle.GetFileRange(idx, out long offset, out long size);
        if (size <= 0 || size > int.MaxValue) throw new InvalidOperationException($"Bundle entry has an invalid size: {entryName}");
        bundle.DataReader.Position = offset;
        return bundle.DataReader.ReadBytes((int)size);
    }

    private static string DetectExt(byte[] payload)
    {
        if (payload.Length >= 4)
        {
            var h4 = Encoding.ASCII.GetString(payload, 0, 4);
            if (h4.StartsWith("FSB", StringComparison.Ordinal)) return ".fsb";
            if (h4 == "RIFF") return ".wav";
            if (h4 == "OggS") return ".ogg";
        }
        return ".fsb";
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString();
    }
}


