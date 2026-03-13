using AssetsTools.NET;
using AssetsTools.NET.Extra;

static bool IsUsableField(AssetTypeValueField? field)
{
    return field != null && !field.IsDummy;
}

static bool TrySetByteArray(AssetTypeValueField? field, byte[] payload)
{
    if (!IsUsableField(field))
        return false;

    try
    {
        field!.AsByteArray = payload;
        return true;
    }
    catch
    {
        return false;
    }
}

static bool TryAssignAudioPayload(AssetTypeValueField? audioField, byte[] payloadBytes)
{
    if (!IsUsableField(audioField))
        return false;

    if (TrySetByteArray(audioField, payloadBytes))
        return true;

    var directKeys = new[] { "Array", "array", "data", "m_Data", "bytes", "m_Bytes" };
    foreach (var key in directKeys)
    {
        var child = audioField![key];
        if (TrySetByteArray(child, payloadBytes))
            return true;
    }

    if (audioField!.Children != null)
    {
        foreach (var child in audioField.Children)
        {
            if (!IsUsableField(child))
                continue;
            if (TrySetByteArray(child, payloadBytes))
                return true;
        }
    }

    return false;
}

static void DumpFieldTree(AssetTypeValueField? field, int depth, int maxDepth)
{
    if (field == null)
    {
        Console.WriteLine(new string(' ', depth * 2) + "<null>");
        return;
    }
    if (field.IsDummy)
    {
        Console.WriteLine(new string(' ', depth * 2) + $"<{field.FieldName}:{field.TypeName}> <dummy>");
        return;
    }

    string valueType;
    try
    {
        valueType = field.Value.ValueType.ToString();
    }
    catch
    {
        valueType = "n/a";
    }
    var line = $"{field.FieldName}:{field.TypeName} valueType={valueType}";
    if (valueType == "String")
    {
        try { line += $" value=\"{field.AsString}\""; } catch { }
    }
    else if (valueType == "UInt64")
    {
        try { line += $" value={field.AsULong}"; } catch { }
    }
    else if (valueType == "Int64")
    {
        try { line += $" value={field.AsLong}"; } catch { }
    }
    else if (valueType == "UInt32")
    {
        try { line += $" value={field.AsUInt}"; } catch { }
    }
    else if (valueType == "Int32")
    {
        try { line += $" value={field.AsInt}"; } catch { }
    }
    Console.WriteLine(new string(' ', depth * 2) + line);

    if (depth >= maxDepth || field.Children == null)
        return;

    foreach (var child in field.Children)
    {
        DumpFieldTree(child, depth + 1, maxDepth);
    }
}

static string? GetResourceEntryName(string? source)
{
    if (string.IsNullOrWhiteSpace(source))
        return null;
    var idx = source.LastIndexOf('/');
    if (idx >= 0 && idx + 1 < source.Length)
        return source[(idx + 1)..];
    return source;
}

static byte[] ReadBundleEntryBytes(AssetBundleFile bundleFile, string entryName)
{
    var idx = bundleFile.GetFileIndex(entryName);
    if (idx < 0)
        throw new InvalidOperationException($"Bundle entry not found: {entryName}");

    bundleFile.GetFileRange(idx, out long offset, out long size);
    if (size < 0 || size > int.MaxValue)
        throw new InvalidOperationException($"Unsupported entry size for {entryName}: {size}");

    bundleFile.DataReader.Position = offset;
    return bundleFile.DataReader.ReadBytes((int)size);
}

static string HexHead(byte[] bytes, int offset, int count)
{
    if (offset < 0 || offset >= bytes.Length || count <= 0)
        return "";
    var max = Math.Min(count, bytes.Length - offset);
    var parts = new string[max];
    for (var i = 0; i < max; i++)
        parts[i] = bytes[offset + i].ToString("X2");
    return string.Join(" ", parts);
}

if (args.Length < 5)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  UabAudioClipPatcher <bundlePath> <classdata.tpk> <payloadDir> <outputBundlePath> <payloadExt=.wav>");
    return 1;
}

var bundlePath = args[0];
var classDataTpkPath = args[1];
var payloadDir = args[2];
var outputBundlePath = args[3];
var payloadExt = args.Length >= 5 ? args[4] : ".wav";
if (!payloadExt.StartsWith(".", StringComparison.Ordinal))
{
    payloadExt = "." + payloadExt;
}

if (!File.Exists(bundlePath))
{
    Console.WriteLine($"Bundle not found: {bundlePath}");
    return 2;
}
if (!File.Exists(classDataTpkPath))
{
    Console.WriteLine($"classdata.tpk not found: {classDataTpkPath}");
    return 3;
}
if (!Directory.Exists(payloadDir))
{
    Console.WriteLine($"Payload dir not found: {payloadDir}");
    return 4;
}

var payloadMap = Directory
    .GetFiles(payloadDir, "*" + payloadExt, SearchOption.TopDirectoryOnly)
    .ToDictionary(
        p => Path.GetFileNameWithoutExtension(p),
        p => p,
        StringComparer.Ordinal);

if (payloadMap.Count == 0)
{
    Console.WriteLine($"No payload files found in: {payloadDir} ({payloadExt})");
    return 5;
}

var manager = new AssetsManager();
manager.LoadClassPackage(classDataTpkPath);

var bunInst = manager.LoadBundleFile(bundlePath, true);
if (bunInst == null)
{
    Console.WriteLine("Failed to load bundle.");
    return 6;
}

var bundleReplacers = new List<BundleReplacer>();
var totalReplaced = 0;
var totalFailed = 0;
var printedDiagnostics = false;
var streamedResourceCandidates = new List<(
    string AssetsFileName,
    AssetsFileInstance AssetsFileInstance,
    AssetFileInfo AssetInfo,
    AssetTypeValueField BaseField,
    AssetTypeValueField ResourceField,
    string ClipName,
    string ResourceEntryName,
    long OldOffset,
    long OldSize,
    bool HasReplacement,
    byte[]? PayloadBytes
)>();
var replacersByAssetsFile = new Dictionary<string, List<AssetsReplacer>>(StringComparer.Ordinal);
var assetsFileByName = new Dictionary<string, AssetsFileInstance>(StringComparer.Ordinal);

foreach (var afName in bunInst.file.GetAllFileNames())
{
    var afInst = manager.LoadAssetsFileFromBundle(bunInst, afName, true);
    if (afInst == null)
        continue;

    var infos = afInst.file.GetAssetsOfType(83); // AudioClip
    if (infos == null || infos.Count == 0)
        continue;

    foreach (var info in infos)
    {
        var baseField = manager.GetBaseField(afInst, info);
        if (baseField == null)
            continue;

        var nameField = baseField["m_Name"];
        var audioField = baseField["m_AudioData"];
        var resourceField = baseField["m_Resource"];
        if (nameField == null || audioField == null)
            continue;

        var clipName = nameField.AsString;
        if (string.IsNullOrEmpty(clipName))
            continue;

        if (!IsUsableField(audioField) && IsUsableField(resourceField))
        {
            var srcField = resourceField!["m_Source"];
            var offField = resourceField["m_Offset"];
            var sizeField = resourceField["m_Size"];
            if (!IsUsableField(srcField) || !IsUsableField(offField) || !IsUsableField(sizeField))
            {
                totalFailed++;
                Console.WriteLine($"Skip clip '{clipName}': m_Resource fields are incomplete.");
                continue;
            }
            var resourceEntryName = GetResourceEntryName(srcField!.AsString);
            if (string.IsNullOrWhiteSpace(resourceEntryName))
            {
                totalFailed++;
                Console.WriteLine($"Skip clip '{clipName}': m_Source is empty.");
                continue;
            }

            byte[]? payloadBytesStreamed = null;
            var hasReplacement = payloadMap.TryGetValue(clipName, out var replacementPath);
            if (hasReplacement)
            {
                payloadBytesStreamed = File.ReadAllBytes(replacementPath!);
                Console.WriteLine($"[map] clip={clipName} payload={replacementPath} bytes={payloadBytesStreamed.LongLength}");
                totalReplaced++;
            }
            streamedResourceCandidates.Add((
                afName,
                afInst,
                info,
                baseField,
                resourceField!,
                clipName,
                resourceEntryName!,
                (long)offField!.AsULong,
                (long)sizeField!.AsULong,
                hasReplacement,
                payloadBytesStreamed
            ));

            if (hasReplacement && !printedDiagnostics)
            {
                printedDiagnostics = true;
                Console.WriteLine("[diag] AudioClip uses StreamedResource. m_AudioData is dummy for this bundle format.");
                Console.WriteLine($"[diag] bundle={bundlePath}");
                Console.WriteLine($"[diag] assetsFile={afName} clip={clipName}");
                Console.WriteLine("[diag] m_Resource subtree:");
                DumpFieldTree(resourceField, 0, 3);
            }
            continue;
        }

        if (!payloadMap.TryGetValue(clipName, out var payloadPath))
            continue;

        var payloadBytes = File.ReadAllBytes(payloadPath);
        var assigned = TryAssignAudioPayload(audioField, payloadBytes);

        if (!assigned)
        {
            totalFailed++;
            Console.WriteLine($"Skip clip '{clipName}': unable to assign payload bytes.");
            if (!printedDiagnostics)
            {
                printedDiagnostics = true;
                Console.WriteLine($"[diag] bundle={bundlePath}");
                Console.WriteLine($"[diag] assetsFile={afName} clip={clipName}");
                Console.WriteLine("[diag] baseField top-level:");
                DumpFieldTree(baseField, 0, 1);
                Console.WriteLine("[diag] m_AudioData subtree:");
                DumpFieldTree(audioField, 0, 3);
            }
            continue;
        }

        if (!replacersByAssetsFile.TryGetValue(afName, out var directReplacers))
        {
            directReplacers = new List<AssetsReplacer>();
            replacersByAssetsFile[afName] = directReplacers;
            assetsFileByName[afName] = afInst;
        }
        directReplacers.Add(new AssetsReplacerFromMemory(afInst.file, info, baseField));
        totalReplaced++;
    }
}

if (streamedResourceCandidates.Count > 0)
{
    var byEntry = streamedResourceCandidates.GroupBy(c => c.ResourceEntryName, StringComparer.Ordinal);
    foreach (var entryGroup in byEntry)
    {
        if (!entryGroup.Any(c => c.HasReplacement))
            continue;

        var entryName = entryGroup.Key;
        var oldResourceBytes = ReadBundleEntryBytes(bunInst.file, entryName);
        var ordered = entryGroup.OrderBy(c => c.OldOffset).ToList();
        var newOffsets = new List<long>(ordered.Count);
        var newSizes = new List<long>(ordered.Count);

        long cursor = 0;
        using var ms = new MemoryStream(oldResourceBytes.Length);
        foreach (var c in ordered)
        {
            if (c.OldOffset < cursor || c.OldOffset + c.OldSize > oldResourceBytes.LongLength)
                throw new InvalidOperationException($"Invalid StreamedResource range: {c.ClipName} off={c.OldOffset} size={c.OldSize} entrySize={oldResourceBytes.LongLength}");

            var keepLen = c.OldOffset - cursor;
            if (keepLen > 0)
                ms.Write(oldResourceBytes, (int)cursor, (int)keepLen);

            var newOffset = ms.Position;
            newOffsets.Add(newOffset);
            if (c.HasReplacement && c.PayloadBytes != null)
            {
                ms.Write(c.PayloadBytes, 0, c.PayloadBytes.Length);
                newSizes.Add(c.PayloadBytes.LongLength);
            }
            else
            {
                ms.Write(oldResourceBytes, (int)c.OldOffset, (int)c.OldSize);
                newSizes.Add(c.OldSize);
            }
            cursor = c.OldOffset + c.OldSize;
        }

        if (cursor < oldResourceBytes.LongLength)
            ms.Write(oldResourceBytes, (int)cursor, (int)(oldResourceBytes.LongLength - cursor));
        var newResourceBytes = ms.ToArray();

        var entryIndex = bunInst.file.GetFileIndex(entryName);
        if (entryIndex < 0)
            throw new InvalidOperationException($"Bundle entry not found: {entryName}");
        bundleReplacers.Add(new BundleReplacerFromMemory(entryName, entryName, false, newResourceBytes, newResourceBytes.LongLength, entryIndex));

        for (var i = 0; i < ordered.Count; i++)
        {
            var c = ordered[i];
            c.ResourceField["m_Offset"]!.AsULong = (ulong)newOffsets[i];
            c.ResourceField["m_Size"]!.AsULong = (ulong)newSizes[i];
            if (ordered.Count <= 64)
            {
                var oldHead = HexHead(oldResourceBytes, (int)c.OldOffset, 12);
                var newHead = c.HasReplacement && c.PayloadBytes != null
                    ? HexHead(c.PayloadBytes, 0, 12)
                    : HexHead(oldResourceBytes, (int)c.OldOffset, 12);
                Console.WriteLine(
                    $"[stream-map] clip={c.ClipName} oldOff={c.OldOffset} oldSize={c.OldSize} " +
                    $"newOff={newOffsets[i]} newSize={newSizes[i]} replaced={c.HasReplacement} " +
                    $"oldHead=[{oldHead}] newHead=[{newHead}]");
            }

            if (!replacersByAssetsFile.TryGetValue(c.AssetsFileName, out var list))
            {
                list = new List<AssetsReplacer>();
                replacersByAssetsFile[c.AssetsFileName] = list;
                assetsFileByName[c.AssetsFileName] = c.AssetsFileInstance;
            }
            list.Add(new AssetsReplacerFromMemory(c.AssetsFileInstance.file, c.AssetInfo, c.BaseField));
        }

        var replacedCount = ordered.Count(c => c.HasReplacement);
        Console.WriteLine($"Prepared StreamedResource replacer '{entryName}' clips={ordered.Count} replaced={replacedCount} oldSize={oldResourceBytes.LongLength} newSize={newResourceBytes.LongLength}.");
    }
}

foreach (var kv in replacersByAssetsFile)
{
    var afName = kv.Key;
    var replacers = kv.Value;
    var afInst = assetsFileByName[afName];
    var fileId = bunInst.file.GetFileIndex(afName);
    bundleReplacers.Add(new BundleReplacerFromAssets(afName, afName, afInst.file, replacers, fileId, null));
    Console.WriteLine($"Prepared {replacers.Count} asset replacers in assets file '{afName}'.");
}

if (totalReplaced == 0 || bundleReplacers.Count == 0)
{
    Console.WriteLine("No matching AudioClip payloads found in bundle.");
    return 7;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputBundlePath))!);
using (var fs = File.Create(outputBundlePath))
using (var writer = new AssetsFileWriter(fs))
{
    bunInst.file.Write(writer, bundleReplacers, null);
}

Console.WriteLine($"Patched bundle written: {outputBundlePath}");
Console.WriteLine($"Total replaced clips: {totalReplaced}");
if (totalFailed > 0)
{
    Console.WriteLine($"Total failed clips: {totalFailed}");
}
return 0;
