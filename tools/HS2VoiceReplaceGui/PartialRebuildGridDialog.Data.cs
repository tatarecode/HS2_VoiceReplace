using System.Text;

namespace HS2VoiceReplace;

internal sealed partial class PartialRebuildGridDialog
{
    private void ReloadRows()
    {
        try
        {
            var runRoot = _txtRunRoot.Text.Trim();
            if (string.IsNullOrWhiteSpace(runRoot))
            {
                _rows.Clear();
                _grid.ClearSelection();
                _lblStatus.Text = T("dialog.partialGrid.status.runRootRequired");
                UpdateEmptyState(T("dialog.partialGrid.empty.extractFirst"), visible: true);
                RefreshSelectionActionAvailability();
                return;
            }

            var runFull = Path.GetFullPath(runRoot);
            var manifest = Path.Combine(runFull, "rvc_batches", "routing_manifest.csv");
            if (!File.Exists(manifest))
            {
                _rows.Clear();
                _grid.ClearSelection();
                _lblStatus.Text = T("dialog.partialGrid.empty.extractFirst");
                UpdateEmptyState(T("dialog.partialGrid.empty.extractFirst"), visible: true);
                RefreshSelectionActionAvailability();
                return;
            }
            var statusMap = LoadStatusMap(runFull);
            var sigMap = LoadSampleSignatureMap(runFull);
            var runSig = LoadRunLevelSampleSignatures(runFull);
            var voiceLineMap = LoadVoiceLineMap(runFull);
            var previousRows = _rows.ToDictionary(x => x.RelativePath, x => x, StringComparer.OrdinalIgnoreCase);

            var outRoot = Path.Combine(runFull, "voice_replace_wav");
            var loaded = 0;
            _rows.Clear();
            _rowByRel.Clear();
            _rowIndexByRel.Clear();
            _relByProgressIndex.Clear();
            foreach (var line in File.ReadLines(manifest).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var cols = PartialRebuildGridDataUtil.ParseCsvLine(line);
                if (cols.Count < 3)
                    continue;

                var rel = cols[0];
                var bucket = (cols[1] ?? "").Trim().ToLowerInvariant();
                if (bucket != "ero") bucket = "normal";
                var src = cols[2];
                var dst = Path.Combine(outRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                var convertedExists = File.Exists(dst);
                previousRows.TryGetValue(rel, out var previousRow);
                sigMap.TryGetValue(rel, out var sigEntry);
                var resolvedSignatures = PartialRebuildGridDataUtil.ResolveDisplayedRowSampleSignatures(
                    bucket,
                    new PartialRebuildGridDataUtil.RunSampleSignatures(runSig.Normal, runSig.Ero, runSig.NormalName, runSig.EroName, runSig.SeedVcSummary),
                    sigMap.ContainsKey(rel)
                        ? new PartialRebuildGridDataUtil.RowSampleSignature(sigEntry.Normal, sigEntry.Ero, sigEntry.Used, sigEntry.NormalName, sigEntry.EroName, sigEntry.UsedName, sigEntry.SeedVcSummary)
                        : null,
                    previousRow == null
                        ? null
                        : new PartialRebuildGridDataUtil.RowSampleSignature(
                            previousRow.SampleSignatureNormal,
                            previousRow.SampleSignatureEro,
                            previousRow.SampleSignatureUsed,
                            previousRow.SampleNameNormal,
                            previousRow.SampleNameEro,
                            previousRow.SampleNameUsed,
                            previousRow.SeedVcSummaryStored));

                var displayUsedSampleName = convertedExists
                    ? PartialRebuildGridDataUtil.PreferNonEmpty(
                        sigEntry.UsedName,
                        resolvedSignatures.UsedName,
                        previousRow?.SampleNameUsed)
                    : "";
                var displayUsedSignature = convertedExists
                    ? PartialRebuildGridDataUtil.PreferNonEmpty(
                        sigEntry.Used,
                        resolvedSignatures.Used,
                        previousRow?.SampleSignatureUsed)
                    : "";
                var displaySeedVcSummary = convertedExists
                    ? PartialRebuildGridDataUtil.PreferNonEmpty(
                        sigEntry.SeedVcSummary,
                        resolvedSignatures.SeedVcSummary,
                        previousRow?.SeedVcSummary)
                    : "";

                var item = new PartialRebuildGridRow
                {
                    RunRoot = runFull,
                    RelativePath = rel,
                    Bucket = bucket,
                    SourceFile = src,
                    ConvertedFile = dst,
                    SourceExists = File.Exists(src),
                    ConvertedExists = convertedExists,
                    Status = BuildDisplayStatus(
                        PartialRebuildGridDataUtil.ResolveDisplayRawStatus(
                            bucket,
                            statusMap.TryGetValue(rel, out var st) ? st : "",
                            convertedExists,
                            resolvedSignatures),
                        convertedExists),
                    VoiceLine = PartialRebuildGridDataUtil.PreferNonEmpty(
                        voiceLineMap.TryGetValue(rel, out var lineText) ? lineText : "",
                        previousRow?.VoiceLine),
                    SampleNameNormal = resolvedSignatures.NormalName,
                    SampleNameEro = resolvedSignatures.EroName,
                    SampleNameUsed = displayUsedSampleName,
                    SampleSignatureNormal = resolvedSignatures.Normal,
                    SampleSignatureEro = resolvedSignatures.Ero,
                    SampleSignatureUsed = displayUsedSignature,
                    SeedVcSummary = displaySeedVcSummary,
                    SeedVcSummaryStored = resolvedSignatures.SeedVcSummary
                };
                _rows.Add(item);
                _rowByRel[rel] = item;
                _rowIndexByRel[rel] = _rows.Count - 1;
                loaded++;
            }

            var visibleVoiceLineMap = _rows
                .Where(x => !string.IsNullOrWhiteSpace(x.VoiceLine))
                .ToDictionary(x => x.RelativePath, x => x.VoiceLine, StringComparer.OrdinalIgnoreCase);
            if (visibleVoiceLineMap.Count > 0 && visibleVoiceLineMap.Count != voiceLineMap.Count)
                SaveVoiceLineMapCsv(runFull, visibleVoiceLineMap);

            _lblStatus.Text = loaded > 0
                ? T("dialog.partialGrid.status.loadedRows", loaded)
                : T("dialog.partialGrid.empty.noRows");
            UpdateEmptyState(
                loaded > 0 ? string.Empty : T("dialog.partialGrid.empty.noRows"),
                visible: loaded == 0);
            _grid.ClearSelection();
            RefreshSelectionActionAvailability();
            _onLog($"grid reload: run_root={runFull}, rows={loaded}");
        }
        catch (Exception ex)
        {
            _rows.Clear();
            _grid.ClearSelection();
            _lblStatus.Text = T("dialog.partialGrid.status.errorWithMessage", ex.Message);
            UpdateEmptyState(T("dialog.partialGrid.empty.extractFirst"), visible: true);
            RefreshSelectionActionAvailability();
        }
    }

    private void UpdateEmptyState(string message, bool visible)
    {
        _lblEmptyState.Text = message;
        _lblEmptyState.Visible = visible;
        _grid.Visible = !visible;
    }

    private Dictionary<string, string> LoadStatusMap(string runRoot)
    {
        var report = Path.Combine(runRoot, "seed_vc_report.csv");
        if (!File.Exists(report))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return GridStatusMapUtil.ParseStatusMap(File.ReadLines(report));
        }
        catch (Exception ex)
        {
            _onLog("grid status map load failed: " + ex.Message);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, string> LoadVoiceLineMap(string runRoot)
    {
        var cached = LoadVoiceLineMapFromCsv(runRoot);
        var shouldRebuild = cached.Count == 0 || cached.Count < 40000;
        if (!shouldRebuild)
            return cached;

        // Rebuild from extracted TextAssets when the cached CSV is missing or obviously incomplete.
        var built = BuildVoiceLineMapFromTextAssets(runRoot);
        if (built.Count > 0)
        {
            if (built.Count != cached.Count)
                SaveVoiceLineMapCsv(runRoot, built);
            return built;
        }

        return cached;
    }

    private Dictionary<string, string> LoadVoiceLineMapFromCsv(string runRoot)
    {
        var p = Path.Combine(runRoot, "voice_line_map.csv");
        if (!File.Exists(p))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return VoiceLineMapUtil.ParseVoiceLineMapCsv(File.ReadLines(p, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            _onLog("load voice line map failed: " + ex.Message);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, string> BuildVoiceLineMapFromTextAssets(string runRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();
        void AddListVoiceDirs(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;
            candidates.Add(Path.Combine(root, "list_h_sound_voice_30"));
            candidates.Add(Path.Combine(root, "list_h_sound_voice_50"));
            try
            {
                foreach (var d in Directory.GetDirectories(root, "list_h_sound_voice_*", SearchOption.TopDirectoryOnly))
                    candidates.Add(d);
            }
            catch
            {
            }
        }

        AddListVoiceDirs(runRoot);
        AddListVoiceDirs(Path.Combine(runRoot, "payload"));
        AddListVoiceDirs(Path.Combine(runRoot, "c14_try_export"));

        try
        {
            var baseDir = AppContext.BaseDirectory;
            var probe = baseDir;
            for (var i = 0; i < 6; i++)
            {
                var parent = Directory.GetParent(probe);
                if (parent == null)
                    break;
                probe = parent.FullName;
            }
        }
        catch
        {
        }

        var textDirs = candidates
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (textDirs.Count == 0)
            return map;

        foreach (var dir in textDirs)
        {
            foreach (var file in Directory.GetFiles(dir, "*.TextAsset", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    foreach (var raw in File.ReadLines(file, Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(raw))
                            continue;
                        var cols = raw.Split('\t');
                        if (cols.Length < 4)
                            continue;

                        var lineText = (cols[0] ?? "").Trim();
                        var bundlePath = (cols[2] ?? "").Trim().Replace('\\', '/');
                        var clipName = (cols[3] ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(lineText) || string.IsNullOrWhiteSpace(bundlePath) || string.IsNullOrWhiteSpace(clipName))
                            continue;

                        foreach (var kv in VoiceLineMapUtil.BuildVoiceLineMapFromTextAssetLines(new[] { raw }))
                            if (!map.ContainsKey(kv.Key))
                                map[kv.Key] = kv.Value;
                    }
                }
                catch (Exception ex)
                {
                    _onLog($"voice line parse failed: {file} :: {ex.Message}");
                }
            }
        }

        if (map.Count > 0)
            _onLog($"voice line map built: {map.Count} entries");
        return map;
    }

    private void SaveVoiceLineMapCsv(string runRoot, Dictionary<string, string> map)
    {
        try
        {
            var p = Path.Combine(runRoot, "voice_line_map.csv");
            VoiceLineMapUtil.SaveVoiceLineMapCsv(p, map);
        }
        catch (Exception ex)
        {
            _onLog("save voice line map failed: " + ex.Message);
        }
    }

    private string BuildDisplayStatus(string rawStatus, bool convertedExists)
        => PartialRebuildGridDataUtil.BuildDisplayStatus(Language, rawStatus, convertedExists);

    private RunSampleSignatures LoadRunLevelSampleSignatures(string runRoot)
    {
        var p = Path.Combine(runRoot, "sample_signature.csv");
        if (!File.Exists(p))
            return new RunSampleSignatures("", "");
        try
        {
            var parsed = PartialRebuildGridDataUtil.ParseRunLevelSampleSignatures(File.ReadLines(p));
            return new RunSampleSignatures(parsed.Normal, parsed.Ero, parsed.NormalName, parsed.EroName, parsed.SeedVcSummary);
        }
        catch (Exception ex)
        {
            _onLog("load sample signatures failed: " + ex.Message);
            return new RunSampleSignatures("", "");
        }
    }

    private Dictionary<string, RowSampleSignature> LoadSampleSignatureMap(string runRoot)
    {
        var map = new Dictionary<string, RowSampleSignature>(StringComparer.OrdinalIgnoreCase);
        var p = Path.Combine(runRoot, "sample_signature_map.csv");
        if (!File.Exists(p))
            return map;
        try
        {
            foreach (var entry in PartialRebuildGridDataUtil.ParseSampleSignatureMap(File.ReadLines(p)))
                map[entry.Key] = new RowSampleSignature(
                    entry.Value.Normal,
                    entry.Value.Ero,
                    entry.Value.Used,
                    entry.Value.NormalName,
                    entry.Value.EroName,
                    entry.Value.UsedName,
                    entry.Value.SeedVcSummary);
        }
        catch (Exception ex)
        {
            _onLog("load sample signature map failed: " + ex.Message);
        }
        return map;
    }

    private void SaveSampleSignatureMapForRun(string runRoot)
    {
        try
        {
            // Store per-row signatures so "run all" can skip rows whose source sample and role
            // assignment have not changed since the previous conversion.
            var p = Path.Combine(runRoot, "sample_signature_map.csv");
            var lines = new List<string> { "relative_path,bucket,output_file,sig_normal,sig_ero,sig_used,sample_normal_name,sample_ero_name,sample_used_name,seed_vc_summary" };
            foreach (var r in _rows.Where(x => string.Equals(x.RunRoot, runRoot, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(
                    $"\"{r.RelativePath}\",\"{r.Bucket}\",\"{r.ConvertedFile.Replace("\"", "\"\"")}\",\"{r.SampleSignatureNormal}\",\"{r.SampleSignatureEro}\",\"{r.SampleSignatureUsed}\",\"\",\"\",\"{EscapeCsvValue(r.SampleNameUsed)}\",\"{EscapeCsvValue(r.SeedVcSummary)}\"");
            }
            File.WriteAllLines(p, lines, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            _onLog("save sample signature map failed: " + ex.Message);
        }
    }

    private static string EscapeCsvValue(string? value)
        => (value ?? "").Replace("\"", "\"\"");

}

