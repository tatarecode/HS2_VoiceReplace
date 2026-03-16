namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Validation is isolated so orchestration code can assume its inputs are already coherent

    // and focus only on execution order and resumability.

private static string SanitizeWavFileName(string fileName)
    {
        var baseName = string.IsNullOrWhiteSpace(fileName) ? "single.wav" : fileName;
        var cleaned = string.Concat(baseName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (!cleaned.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            cleaned = Path.GetFileNameWithoutExtension(cleaned) + ".wav";
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "single.wav";
        return cleaned;
    }

    private static void ValidateOptions(PipelineOptions o, PipelineMode mode)
    {
        if (!Directory.Exists(o.BundleRoot) && (string.IsNullOrWhiteSpace(o.ExternalToolsRoot) || !Directory.Exists(o.ExternalToolsRoot)))
            throw new InvalidOperationException(L("error.dependencyRootMissing"));
        if (!Directory.Exists(o.OutputBaseRoot)) Directory.CreateDirectory(o.OutputBaseRoot);
        if (mode != PipelineMode.DeployOnly)
        {
            if (!Directory.Exists(o.Hs2Root))
                throw new DirectoryNotFoundException(o.Hs2Root);
        }
        if (mode == PipelineMode.PreviewOnly || mode == PipelineMode.BuildOnly)
        {
            if (!File.Exists(o.StyleNormalSample))
                throw new FileNotFoundException(o.StyleNormalSample);
            if (!File.Exists(o.StyleEroSample))
                throw new FileNotFoundException(o.StyleEroSample);
        }

        if (mode != PipelineMode.DeployOnly)
        {
            var pid = $"c{o.TargetPersonalityId:00}";
            var pcm = Path.Combine(o.Hs2Root, "abdata", "sound", "data", "pcm", pid);
            if (!Directory.Exists(pcm))
                throw new InvalidOperationException(L("error.personalitySourceMissing", pcm));
        }

        var required = new List<string>();
        if (mode is PipelineMode.ExtractOnly or PipelineMode.PreviewOnly or PipelineMode.BuildOnly)
            required.Add(Path.Combine("uabea", "classdata.tpk"));
        if (mode is PipelineMode.ExtractOnly or PipelineMode.PreviewOnly)
            required.Add(Path.Combine("vgmstream", "vgmstream-cli.exe"));
        if (mode is PipelineMode.PreviewOnly or PipelineMode.BuildOnly)
        {
            required.Add("seed_vc_v2");
            required.Add(Path.Combine("scripts", GetSeedVcInferScriptName(o.SeedVc)));
        }
        if (mode == PipelineMode.BuildOnly)
        {
            required.Add(Path.Combine("mods_template", VoiceReplaceNames.RuntimeTemplateDirName));
            required.Add(Path.Combine("tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe"));
        }
        if (mode == PipelineMode.DeployOnly && !RuntimePluginExists(o))
            required.Add(Path.Combine("plugins", RuntimePluginFileName));

        if (mode is PipelineMode.PreviewOnly or PipelineMode.BuildOnly)
        {
            var hasManualNormal = o.StyleNormalSegment != null;
            var hasManualEro = o.StyleEroSegment != null;
            var sameSample = string.Equals(o.StyleNormalSample, o.StyleEroSample, StringComparison.OrdinalIgnoreCase);
            var autoNormalNeeded = !hasManualNormal;
            var autoEroNeeded = !hasManualEro && !(hasManualNormal && sameSample);
            if (autoNormalNeeded || autoEroNeeded)
                required.Add(Path.Combine("scripts", "select_voice_style_segment.py"));
        }

        foreach (var r in required)
        {
            if (!DependencyExists(o, r))
                throw new InvalidOperationException(L("error.dependencyFileMissing", r));
        }

    }

    private static void ValidateSingleOptions(PipelineOptions o, string sourceWav, string modelBucket)
    {
        if (!Directory.Exists(o.BundleRoot) && (string.IsNullOrWhiteSpace(o.ExternalToolsRoot) || !Directory.Exists(o.ExternalToolsRoot)))
            throw new InvalidOperationException(L("error.dependencyRootMissing"));
        if (!Directory.Exists(o.OutputBaseRoot))
            Directory.CreateDirectory(o.OutputBaseRoot);

        if (string.IsNullOrWhiteSpace(sourceWav) || !File.Exists(sourceWav))
            throw new FileNotFoundException(L("error.inputWavMissing"), sourceWav);
        if (!sourceWav.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("error.inputWavMustBeWav"));

        if (!File.Exists(o.StyleNormalSample))
            throw new FileNotFoundException(L("error.normalSampleMissingFile"), o.StyleNormalSample);
        if (!File.Exists(o.StyleEroSample))
            throw new FileNotFoundException(L("error.eroSampleMissingFile"), o.StyleEroSample);

        if (!string.Equals(modelBucket, "normal", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(modelBucket, "ero", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("error.modelBucketInvalid"));

        var required = new List<string>
        {
            "seed_vc_v2",
            Path.Combine("scripts", GetSeedVcInferScriptName(o.SeedVc)),
        };

        var hasManualNormal = o.StyleNormalSegment != null;
        var hasManualEro = o.StyleEroSegment != null;
        var sameSample = string.Equals(o.StyleNormalSample, o.StyleEroSample, StringComparison.OrdinalIgnoreCase);
        var autoNormalNeeded = !hasManualNormal;
        var autoEroNeeded = !hasManualEro && !(hasManualNormal && sameSample);
        if (autoNormalNeeded || autoEroNeeded)
            required.Add(Path.Combine("scripts", "select_voice_style_segment.py"));

        foreach (var r in required)
        {
            if (!DependencyExists(o, r))
                throw new InvalidOperationException(L("error.dependencyFileMissing", r));
        }
    }

    private static void ValidatePartialRebuildOptions(PipelineOptions o, string runRoot, string inputWav, string modelBucket)
    {
        if (!Directory.Exists(o.BundleRoot) && (string.IsNullOrWhiteSpace(o.ExternalToolsRoot) || !Directory.Exists(o.ExternalToolsRoot)))
            throw new InvalidOperationException(L("error.dependencyRootMissing"));
        if (!Directory.Exists(o.OutputBaseRoot))
            Directory.CreateDirectory(o.OutputBaseRoot);
        if (!Directory.Exists(o.Hs2Root))
            throw new DirectoryNotFoundException(o.Hs2Root);

        var runRootFull = Path.GetFullPath(runRoot);
        if (!Directory.Exists(runRootFull))
            throw new DirectoryNotFoundException(L("error.fullRunRootMissing", runRootFull));
        var manifest = Path.Combine(runRootFull, "rvc_batches", "routing_manifest.csv");
        if (!File.Exists(manifest))
            throw new FileNotFoundException(L("error.routingManifestMissing", manifest));

        if (string.IsNullOrWhiteSpace(inputWav) || !File.Exists(inputWav))
            throw new FileNotFoundException(L("error.inputWavMissing"), inputWav);
        if (!inputWav.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("error.inputWavMustBeWav"));

        if (!File.Exists(o.StyleNormalSample))
            throw new FileNotFoundException(L("error.normalSampleMissingFile"), o.StyleNormalSample);
        if (!File.Exists(o.StyleEroSample))
            throw new FileNotFoundException(L("error.eroSampleMissingFile"), o.StyleEroSample);

        if (!string.Equals(modelBucket, "normal", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(modelBucket, "ero", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L("error.modelBucketInvalid"));

        var required = new List<string>
        {
            "seed_vc_v2",
            Path.Combine("uabea", "classdata.tpk"),
            Path.Combine("scripts", GetSeedVcInferScriptName(o.SeedVc)),
            Path.Combine("mods_template", VoiceReplaceNames.RuntimeTemplateDirName),
            Path.Combine("tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe"),
        };

        var hasManualNormal = o.StyleNormalSegment != null;
        var hasManualEro = o.StyleEroSegment != null;
        var sameSample = string.Equals(o.StyleNormalSample, o.StyleEroSample, StringComparison.OrdinalIgnoreCase);
        var autoNormalNeeded = !hasManualNormal;
        var autoEroNeeded = !hasManualEro && !(hasManualNormal && sameSample);
        if (autoNormalNeeded || autoEroNeeded)
            required.Add(Path.Combine("scripts", "select_voice_style_segment.py"));

        foreach (var r in required)
        {
            if (!DependencyExists(o, r))
                throw new InvalidOperationException(L("error.dependencyFileMissing", r));
        }

    }
}











